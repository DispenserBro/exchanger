using Exchanger.Core.Audio;
using Exchanger.Core.Configuration;
using Exchanger.Core.Logging;
using Godot;
using System;
using System.IO;

namespace Exchanger.UI.AdvertisementPlayer;

/// <summary>
/// Пассивный промо-блок. Декодирование промо-ролика выполняется только пока
/// компонент видим; при отсутствии корректного промо-ролика остаётся постер или текст.
/// </summary>
public partial class AdvertisementPlayer : Control
{
    private VideoStreamPlayer _videoPlayer = null!;
    private TextureRect _poster = null!;
    private Label _fallbackLabel = null!;
    private AdvertisementSettings _settings = new();

    private int _nextVideoIndex;
    private bool _finishedSubscribed;
    private bool _settingsApplied;
    private AdvertisementPlaybackSettingsSnapshot? _appliedSettings;
    private bool _pendingSettingsApply;
    private bool _pendingSettingsApplyScheduled;
    private int _settingsRevision;
    private bool _exiting;

    public override void _EnterTree()
    {
        _exiting = false;
        if (!IsNodeReady())
        {
            return;
        }

        SubscribeVideoFinished();
        VisibilityChanged -= OnVisibilityChanged;
        VisibilityChanged += OnVisibilityChanged;
    }

    public override void _Ready()
    {
        _videoPlayer = GetNode<VideoStreamPlayer>("VideoPlayer");
        _poster = GetNode<TextureRect>("Poster");
        _fallbackLabel = GetNode<Label>("FallbackLabel");
        _videoPlayer.Bus = AudioSettingsApplier.AdvertisementBusName;

        SubscribeVideoFinished();
        VisibilityChanged += OnVisibilityChanged;
        ApplySettings();
    }

    public override void _ExitTree()
    {
        _exiting = true;
        UnsubscribeVideoFinished();
        StopPlayback();

        VisibilityChanged -= OnVisibilityChanged;
    }

    public void Configure(AdvertisementSettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }
        bool unchanged = _settingsApplied && _appliedSettings?.Matches(settings) == true;
        _settings = settings;
        if (!IsNodeReady() || unchanged)
        {
            return;
        }

        _nextVideoIndex = 0;
        _settingsRevision++;
        _pendingSettingsApply = true;
        if (IsPlaybackHostVisible())
        {
            SchedulePendingSettingsApply();
        }
    }

    private void ApplySettings(bool playbackWasStopped = false)
    {
        if (!IsNodeReady())
        {
            return;
        }

        _settingsApplied = true;
        _appliedSettings = AdvertisementPlaybackSettingsSnapshot.Capture(_settings);
        if (!playbackWasStopped)
        {
            StopPlayback();
        }

        _fallbackLabel.Text = _settings.Enabled
            ? _settings.FallbackText
            : "ПРОМО ОТКЛЮЧЕНО";

        if (!_settings.Enabled)
        {
            _poster.Texture = null;
            ShowFallback();
            return;
        }

        LoadPoster();

        if (IsVisibleInTree())
        {
            TryPlayNextVideo();
        }
    }

    private void LoadPoster()
    {
        _poster.Texture = null;
        if (!string.IsNullOrWhiteSpace(_settings.PosterPath))
        {
            _poster.Texture = TryLoadPoster(_settings.PosterPath);
        }

        ShowFallback();
    }

    private bool IsPlaybackHostVisible() => IsVisibleInTree();

    private void OnVisibilityChanged()
    {
        if (_exiting)
        {
            return;
        }

        if (!IsVisibleInTree())
        {
            StopPlayback();
            return;
        }

        if (_pendingSettingsApply)
        {
            SchedulePendingSettingsApply();
            return;
        }

        if (!_settings.Enabled)
        {
            return;
        }

        if (_videoPlayer.Stream is not null && _videoPlayer.IsPlaying())
        {
            _videoPlayer.VolumeDb = _settings.Muted
                ? AudioSettingsApplier.MinimumDecibels
                : 0.0f;
            return;
        }

        TryPlayNextVideo();
    }

    private void SubscribeVideoFinished()
    {
        if (_finishedSubscribed)
        {
            return;
        }

        _videoPlayer.Finished += OnVideoFinished;
        _finishedSubscribed = true;
    }

    private void UnsubscribeVideoFinished()
    {
        if (_finishedSubscribed && GodotObject.IsInstanceValid(_videoPlayer))
        {
            _videoPlayer.Finished -= OnVideoFinished;
        }

        _finishedSubscribed = false;
    }

    private void OnVideoFinished()
    {
        if (_exiting
            || !_settings.Enabled
            || !IsPlaybackHostVisible()
            || _videoPlayer.Paused)
        {
            return;
        }

        TryPlayNextVideo();
    }

    private void SchedulePendingSettingsApply()
    {
        if (_exiting || _pendingSettingsApplyScheduled || !IsPlaybackHostVisible())
        {
            return;
        }

        _pendingSettingsApplyScheduled = true;
        ApplyPendingSettingsWhenSafeAsync();
    }

    private async void ApplyPendingSettingsWhenSafeAsync()
    {
        int scheduledRevision = _settingsRevision;
        try
        {
            // VideoStreamPlayer creates and destroys the Native Video playback synchronously.
            // Do not do that from the Settings save stack or from VisibilityChanged itself.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_exiting
                || !_pendingSettingsApply
                || scheduledRevision != _settingsRevision
                || !IsPlaybackHostVisible())
            {
                return;
            }

            StopPlayback();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_exiting
                || !_pendingSettingsApply
                || scheduledRevision != _settingsRevision
                || !IsPlaybackHostVisible())
            {
                return;
            }

            _pendingSettingsApply = false;
            ApplySettings(playbackWasStopped: true);
        }
        finally
        {
            _pendingSettingsApplyScheduled = false;
            if (!_exiting && _pendingSettingsApply && IsPlaybackHostVisible())
            {
                SchedulePendingSettingsApply();
            }
        }
    }

    private void TryPlayNextVideo()
    {
        if (_exiting || !_settings.Enabled)
        {
            return;
        }

        StopPlayback();
        string[] paths = _settings.VideoPaths ?? Array.Empty<string>();
        for (int attempt = 0; attempt < paths.Length; attempt++)
        {
            string path = paths[_nextVideoIndex % paths.Length];
            _nextVideoIndex = (_nextVideoIndex + 1) % paths.Length;

            VideoStream? stream = TryLoadVideo(path);
            if (stream is null)
            {
                AppLogger.Warning("Advertisement", "Промо-ролик не найден или не поддерживается.");
                continue;
            }

            try
            {
                _videoPlayer.Stream = stream;
                _videoPlayer.VolumeDb = _settings.Muted
                    ? AudioSettingsApplier.MinimumDecibels
                    : 0.0f;
                _videoPlayer.Visible = true;
                _poster.Visible = false;
                _fallbackLabel.Visible = false;
                _videoPlayer.Play();
                return;
            }
            catch (Exception exception)
            {
                AppLogger.Error("Advertisement", "Не удалось запустить промо-ролик; выполняется переход к следующему.", exception);
            }
        }

        ShowFallback();
    }

    private static Texture2D? TryLoadPoster(string path)
    {
        if (ResourceLoader.Exists(path, "Texture2D"))
        {
            return ResourceLoader.Load<Texture2D>(path);
        }

        if (!Godot.FileAccess.FileExists(path))
        {
            return null;
        }

        Image image = Image.LoadFromFile(path);
        return image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
    }

    private static VideoStream? TryLoadVideo(string path)
    {
        string playbackPath = AdvertisementVideoLibrary.NormalizeSelectedVideo(path);

        try
        {
            if (ResourceLoader.Exists(playbackPath, "VideoStream"))
            {
                return ResourceLoader.Load<VideoStream>(playbackPath);
            }

            if (!Godot.FileAccess.FileExists(playbackPath)
                || !Path.GetExtension(playbackPath).Equals(".ogv", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new VideoStreamTheora { File = playbackPath };
        }
        catch (Exception exception)
        {
            AppLogger.Error("Advertisement", "Не удалось загрузить промо-ролик; файл будет пропущен.", exception);
            return null;
        }
    }

    private void StopPlayback()
    {
        if (!GodotObject.IsInstanceValid(_videoPlayer))
        {
            return;
        }

        _videoPlayer.Stop();
        _videoPlayer.Stream = null;
        _videoPlayer.Paused = false;
        _videoPlayer.VolumeDb = AudioSettingsApplier.MinimumDecibels;
        _videoPlayer.Visible = false;
    }

    private void ShowFallback()
    {
        _poster.Visible = _poster.Texture is not null;
        _fallbackLabel.Visible = _poster.Texture is null;
    }
}

using Exchanger.Core.Audio;
using Exchanger.Core.Configuration;
using Exchanger.Core.Logging;
using Godot;
using System;
using System.IO;

namespace Exchanger.UI.AdvertisementPlayer;

/// <summary>
/// Функциональный адаптер готовых промо-узлов. Не создаёт Control и не меняет
/// оформление темы: управляет только контентом и состоянием связанных узлов.
/// </summary>
public sealed class AdvertisementPresenter : IDisposable
{
    private readonly Control _host;
    private VideoStreamPlayer _videoPlayer;
    private readonly TextureRect _poster;
    private readonly Label _fallbackLabel;
    private AdvertisementSettings _settings = new();

    private int _nextVideoIndex;
    private bool _settingsApplied;
    private AdvertisementPlaybackSettingsSnapshot? _appliedSettings;
    private bool _pendingSettingsApply;
    private bool _pendingSettingsApplyScheduled;
    private int _settingsRevision;
    private bool _disposed;

    public AdvertisementPresenter(
        Control host,
        VideoStreamPlayer videoPlayer,
        TextureRect poster,
        Label fallbackLabel)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _videoPlayer = videoPlayer ?? throw new ArgumentNullException(nameof(videoPlayer));
        _poster = poster ?? throw new ArgumentNullException(nameof(poster));
        _fallbackLabel = fallbackLabel ?? throw new ArgumentNullException(nameof(fallbackLabel));
        _videoPlayer.Bus = AudioSettingsApplier.AdvertisementBusName;
        _videoPlayer.Finished += OnVideoFinished;
        _host.VisibilityChanged += OnVisibilityChanged;
    }

    public void Configure(AdvertisementSettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }
        bool unchanged = _settingsApplied && _appliedSettings?.Matches(settings) == true;
        _settings = settings;
        if (unchanged)
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (GodotObject.IsInstanceValid(_videoPlayer))
        {
            _videoPlayer.Finished -= OnVideoFinished;
        }
        StopPlayback();

        if (GodotObject.IsInstanceValid(_host))
        {
            _host.VisibilityChanged -= OnVisibilityChanged;
        }
    }

    private void ApplySettings(bool playbackWasStopped = false)
    {
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

        _poster.Texture = string.IsNullOrWhiteSpace(_settings.PosterPath)
            ? null
            : TryLoadPoster(_settings.PosterPath);
        ShowFallback();
        if (_host.IsVisibleInTree())
        {
            TryPlayNextVideo();
        }
    }

    private bool IsPlaybackHostVisible() => _host.IsVisibleInTree();

    private void OnVisibilityChanged()
    {
        if (_disposed)
        {
            return;
        }

        if (!_host.IsVisibleInTree())
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

    private void OnVideoFinished()
    {
        if (_disposed
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
        if (_disposed || _pendingSettingsApplyScheduled || !IsPlaybackHostVisible())
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
            await _host.ToSignal(_host.GetTree(), SceneTree.SignalName.ProcessFrame);
            await _host.ToSignal(_host.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_disposed
                || !_pendingSettingsApply
                || scheduledRevision != _settingsRevision
                || !IsPlaybackHostVisible())
            {
                return;
            }

            StopPlayback();
            await _host.ToSignal(_host.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_disposed
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
            if (!_disposed && _pendingSettingsApply && IsPlaybackHostVisible())
            {
                SchedulePendingSettingsApply();
            }
        }
    }

    private void TryPlayNextVideo()
    {
        if (_disposed || !_settings.Enabled)
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

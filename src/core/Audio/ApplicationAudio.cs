using Exchanger.Core.Logging;
using Godot;
using System;
using System.Collections.Generic;

namespace Exchanger.Core.Audio;

/// <summary>
/// Воспроизводит системную музыку и эффекты приложения вне зависимости от активной DLC-темы.
/// </summary>
public partial class ApplicationAudio : Node
{
    private const string BackgroundMusicPath = "res://sounds/bg_music.mp3";
    private const string ButtonClickPath = "res://sounds/click.ogg";
    private const string CashAddedPath = "res://sounds/cash_added.wav";
    private const string PaymentSuccessPath = "res://sounds/confirm_payment.wav";
    private const string PaymentErrorPath = "res://sounds/error_style_5_echo_001.wav";
    private const string PetHeldPath = "res://sounds/hold_pet.wav";
    private const string PetReleasedPath = "res://sounds/release_pet.wav";

    private readonly HashSet<ulong> _boundButtonIds = [];
    private AudioStreamPlayer _musicPlayer = null!;
    private AudioStreamPlayer _buttonPlayer = null!;
    private AudioStreamPlayer _cashAddedPlayer = null!;
    private AudioStreamPlayer _paymentSuccessPlayer = null!;
    private AudioStreamPlayer _paymentErrorPlayer = null!;
    private AudioStreamPlayer _petHeldPlayer = null!;
    private AudioStreamPlayer _petReleasedPlayer = null!;

    public override void _Ready()
    {
        _musicPlayer = CreatePlayer("BackgroundMusic", BackgroundMusicPath, AudioSettingsApplier.MusicBusName);
        _buttonPlayer = CreatePlayer("ButtonClick", ButtonClickPath, AudioSettingsApplier.EffectsBusName);
        _cashAddedPlayer = CreatePlayer("CashAdded", CashAddedPath, AudioSettingsApplier.EffectsBusName);
        _paymentSuccessPlayer = CreatePlayer("PaymentSuccess", PaymentSuccessPath, AudioSettingsApplier.EffectsBusName);
        _paymentErrorPlayer = CreatePlayer("PaymentError", PaymentErrorPath, AudioSettingsApplier.EffectsBusName);
        _petHeldPlayer = CreatePlayer("PetHeld", PetHeldPath, AudioSettingsApplier.EffectsBusName);
        _petReleasedPlayer = CreatePlayer("PetReleased", PetReleasedPath, AudioSettingsApplier.EffectsBusName);

        _musicPlayer.Finished += RestartBackgroundMusic;
        GetTree().NodeAdded += OnNodeAdded;
        BindButtons(GetTree().Root);
        RestartBackgroundMusic();
    }

    public override void _ExitTree()
    {
        if (GetTree() is SceneTree tree)
        {
            tree.NodeAdded -= OnNodeAdded;
        }

        if (GodotObject.IsInstanceValid(_musicPlayer))
        {
            _musicPlayer.Finished -= RestartBackgroundMusic;
        }

        _boundButtonIds.Clear();
    }

    public void PlayCashAdded() => PlayEffect(_cashAddedPlayer);

    public void PlayPaymentSuccess() => PlayEffect(_paymentSuccessPlayer);

    public void PlayPaymentError() => PlayEffect(_paymentErrorPlayer);

    public void PlayPetHeld() => PlayEffect(_petHeldPlayer);

    public void PlayPetReleased() => PlayEffect(_petReleasedPlayer);

    private AudioStreamPlayer CreatePlayer(string nodeName, string resourcePath, string busName)
    {
        var player = new AudioStreamPlayer
        {
            Name = nodeName,
            Bus = busName,
            Stream = ResourceLoader.Load<AudioStream>(resourcePath),
        };
        AddChild(player);
        if (player.Stream is null)
        {
            AppLogger.Warning("Audio", $"Не удалось загрузить системный звук {resourcePath}.");
        }

        return player;
    }

    private void OnNodeAdded(Node node)
    {
        if (node is BaseButton button)
        {
            BindButton(button);
        }
    }

    private void BindButtons(Node node)
    {
        if (node is BaseButton button)
        {
            BindButton(button);
        }

        foreach (Node child in node.GetChildren())
        {
            BindButtons(child);
        }
    }

    private void BindButton(BaseButton button)
    {
        if (!_boundButtonIds.Add(button.GetInstanceId()))
        {
            return;
        }

        button.Pressed += PlayButtonClick;
    }

    private void PlayButtonClick() => PlayEffect(_buttonPlayer);

    private static void PlayEffect(AudioStreamPlayer player)
    {
        if (!GodotObject.IsInstanceValid(player) || player.Stream is null)
        {
            return;
        }

        player.Stop();
        player.Play();
    }

    private void RestartBackgroundMusic()
    {
        if (GodotObject.IsInstanceValid(_musicPlayer)
            && _musicPlayer.Stream is not null
            && !_musicPlayer.Playing)
        {
            _musicPlayer.Play();
        }
    }
}

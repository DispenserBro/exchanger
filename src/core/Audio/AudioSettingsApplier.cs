using Exchanger.Core.Configuration;
using Godot;
using System;

namespace Exchanger.Core.Audio;

/// <summary>Применяет нормализованные настройки звука к Godot audio buses.</summary>
public static class AudioSettingsApplier
{
    public const float MinimumDecibels = -80.0f;
    private const float LowestAudibleDecibels = -60.0f;
    public const string MasterBusName = "Master";
    public const string AdvertisementBusName = "Advertisement";
    public const string MusicBusName = "Music";
    public const string EffectsBusName = "Effects";

    public static void Apply(AudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ApplyBus(
            MasterBusName,
            settings.MasterVolumePercent,
            settings.Muted || settings.MasterVolumePercent <= 0);
        ApplyBus(
            AdvertisementBusName,
            settings.AdvertisementVolumePercent,
            settings.AdvertisementVolumePercent <= 0);
        ApplyBus(
            MusicBusName,
            settings.MusicVolumePercent,
            settings.MusicVolumePercent <= 0);
        ApplyBus(
            EffectsBusName,
            settings.SoundEffectsVolumePercent,
            settings.SoundEffectsVolumePercent <= 0);
    }

    public static float PercentToDecibels(int percent)
    {
        int normalized = Math.Clamp(percent, 0, 100);
        if (normalized == 0)
        {
            return MinimumDecibels;
        }

        // «Процент громкости» в интерфейсе — это воспринимаемая пользователем
        // шкала, а не линейная амплитуда. Равномерный диапазон -60..0 dB
        // позволяет низким значениям быть действительно тихими: 3 % ≈ -58 dB.
        return LowestAudibleDecibels
            + (0.0f - LowestAudibleDecibels) * normalized / 100.0f;
    }

    private static void ApplyBus(string busName, int volumePercent, bool muted)
    {
        int busIndex = AudioServer.GetBusIndex(busName);
        if (busIndex < 0)
        {
            GD.PushWarning($"Не найден аудиоканал {busName}; настройки громкости не применены.");
            return;
        }

        AudioServer.SetBusVolumeDb(busIndex, PercentToDecibels(volumePercent));
        AudioServer.SetBusMute(busIndex, muted);
    }
}

using System;
using System.Linq;

namespace Exchanger.Core.Configuration;

/// <summary>
/// Неизменяемый снимок параметров, влияющих на уже созданный промо-поток.
/// Нужен, потому что Settings может изменить исходный объект до вызова Configure.
/// </summary>
internal sealed class AdvertisementPlaybackSettingsSnapshot
{
    private AdvertisementPlaybackSettingsSnapshot(AdvertisementSettings settings)
    {
        Enabled = settings.Enabled;
        Muted = settings.Muted;
        FallbackText = settings.FallbackText ?? string.Empty;
        PosterPath = AdvertisementVideoLibrary.NormalizeSelectedVideo(settings.PosterPath ?? string.Empty);
        VideoPaths = (settings.VideoPaths ?? Array.Empty<string>())
            .Select(AdvertisementVideoLibrary.NormalizeSelectedVideo)
            .ToArray();
    }

    private bool Enabled { get; }
    private bool Muted { get; }
    private string FallbackText { get; }
    private string PosterPath { get; }
    private string[] VideoPaths { get; }

    public static AdvertisementPlaybackSettingsSnapshot Capture(AdvertisementSettings settings) => new(settings);

    public bool Matches(AdvertisementSettings settings)
    {
        return Enabled == settings.Enabled
               && Muted == settings.Muted
               && string.Equals(FallbackText, settings.FallbackText ?? string.Empty, StringComparison.Ordinal)
               && string.Equals(
                   AdvertisementVideoLibrary.GetComparisonKey(PosterPath),
                   AdvertisementVideoLibrary.GetComparisonKey(settings.PosterPath ?? string.Empty),
                   StringComparison.Ordinal)
               && VideoPaths.SequenceEqual(
                   (settings.VideoPaths ?? Array.Empty<string>())
                       .Select(AdvertisementVideoLibrary.NormalizeSelectedVideo)
                       .Select(AdvertisementVideoLibrary.GetComparisonKey),
                   StringComparer.Ordinal);
    }
}

using System;

namespace Exchanger.Core.Configuration;

/// <summary>
/// Нормализует выбранный путь, не копируя и не переименовывая внешний промо-ролик.
/// Native Video передаёт абсолютные Windows drive-path в Media Foundation как
/// UTF-16 file URI, поэтому Unicode-пути не требуют служебных ссылок.
/// </summary>
public static class AdvertisementVideoLibrary
{
    public static string NormalizeSelectedVideo(string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        return sourcePath.Trim().Replace('\\', '/');
    }

    /// <summary>
    /// Возвращает ключ для поиска одного и того же файла в плейлисте и кэше.
    /// На Windows имена файлов регистронезависимы, на Linux регистр сохраняется.
    /// </summary>
    public static string GetComparisonKey(string sourcePath)
    {
        string normalized = NormalizeSelectedVideo(sourcePath);
        return OperatingSystem.IsWindows()
            ? normalized.ToUpperInvariant()
            : normalized;
    }
}

using Exchanger.Core.Configuration;
using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Exchanger.Core.Logging;

/// <summary>JSONL-аудит хранит только факт изменения секций, без старых и новых значений.</summary>
public sealed class ConfigurationAuditStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _path;

    public ConfigurationAuditStore(string path)
    {
        _path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
    }

    public void Record(SettingsChangeSummary summary, string role, DateTimeOffset? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (!summary.HasChanges)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? string.Empty);
        var entry = new
        {
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            Role = NormalizeRole(role),
            Sections = summary.ChangedSections,
            summary.RestartRequired,
        };
        File.AppendAllText(_path, JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine);
    }

    private static string NormalizeRole(string role) => role switch
    {
        "Engineer" => "Engineer",
        "Debug" => "Debug",
        _ => "Operator",
    };
}

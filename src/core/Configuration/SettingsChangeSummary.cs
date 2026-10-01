using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Exchanger.Core.Configuration;

public sealed record SettingsChangeSummary(
    IReadOnlyList<string> ChangedSections,
    IReadOnlyList<string> RestartRequiredSections)
{
    public bool HasChanges => ChangedSections.Count > 0;

    public bool RestartRequired => RestartRequiredSections.Count > 0;
}

/// <summary>Сравнивает секции без раскрытия значений в журнале изменений.</summary>
public static class SettingsChangeDetector
{
    public static SettingsChangeSummary Compare(AppSettings before, AppSettings after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var changed = new List<string>();
        var restart = new List<string>();

        AddIfChanged("Общие", new { before.Branding, before.AnimatedBannerTexts, before.Security }, new { after.Branding, after.AnimatedBannerTexts, after.Security }, changed);
        AddIfChanged("Звук", before.Audio, after.Audio, changed);
        AddIfChanged("Тарифы", before.Pricing, after.Pricing, changed);
        AddIfChanged("Оплата", new { before.Payments, before.Timeouts }, new { after.Payments, after.Timeouts }, changed);
        AddIfChanged("Промо", before.Advertisement, after.Advertisement, changed);
        AddIfChanged(
            "Сервис",
            new { before.MenuVisibility, before.TokenInventory },
            new { after.MenuVisibility, after.TokenInventory },
            changed);
        AddIfChanged("Диагностика", before.Logging, after.Logging, changed);

        AddRestartIfChanged("Оформление", before.Themes, after.Themes, changed, restart);
        AddRestartIfChanged("Оборудование", before.Hardware, after.Hardware, changed, restart);
        AddRestartIfChanged("Окно", before.Window, after.Window, changed, restart);

        return new SettingsChangeSummary(changed, restart);
    }

    private static void AddIfChanged(string name, object before, object after, ICollection<string> changed)
    {
        if (!Serialize(before).Equals(Serialize(after), StringComparison.Ordinal))
        {
            changed.Add(name);
        }
    }

    private static void AddRestartIfChanged(
        string name,
        object before,
        object after,
        ICollection<string> changed,
        ICollection<string> restart)
    {
        if (Serialize(before).Equals(Serialize(after), StringComparison.Ordinal))
        {
            return;
        }

        changed.Add(name);
        restart.Add(name);
    }

    private static string Serialize(object value) => JsonSerializer.Serialize(value);
}

using Exchanger.Core.Configuration;
using Godot;
using System;

namespace Exchanger.UI.KioskFooter;

/// <summary>
/// Общий footer пользовательских экранов. Получает только публичные данные бренда.
/// </summary>
public partial class KioskFooter : Control
{
    private Label _brand = null!;
    private Label _support = null!;
    private BrandingSettings? _branding;

    public override void _Ready()
    {
        _brand = GetNode<Label>("Brand");
        _support = GetNode<Label>("Support");
        ApplyBranding();
    }

    public void Configure(BrandingSettings branding)
    {
        ArgumentNullException.ThrowIfNull(branding);
        _branding = branding;
        if (!IsNodeReady())
        {
            return;
        }

        ApplyBranding();
    }

    private void ApplyBranding()
    {
        if (_branding is null)
        {
            return;
        }

        _brand.Text = _branding.ApplicationName.Replace(" ", "\n", StringComparison.Ordinal);
        _support.Text = $"ТЕХПОДДЕРЖКА:\n{_branding.SupportPhone}";
    }
}

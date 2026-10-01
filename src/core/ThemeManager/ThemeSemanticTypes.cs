using Godot;

namespace Exchanger.Core.Theming;

/// <summary>
/// Стабильные семантические типы host-интерфейса. Внешняя DLC-тема оформляет
/// их без привязки к именам узлов и структуре конкретной сцены.
/// </summary>
public static class ThemeSemanticTypes
{
    public static readonly StringName ScreenTitle = "ScreenTitle";
    public static readonly StringName ScreenDescription = "ScreenDescription";
    public static readonly StringName PanelTitle = "PanelTitle";
    public static readonly StringName PanelText = "PanelText";
    public static readonly StringName FieldLabel = "FieldLabel";
    public static readonly StringName SupportText = "SupportText";
    public static readonly StringName StatusText = "StatusText";
    public static readonly StringName WarningText = "WarningText";
    public static readonly StringName ErrorText = "ErrorText";
    public static readonly StringName PrimaryButton = "PrimaryButton";
    public static readonly StringName CompactButton = "CompactButton";
    public static readonly StringName BackButton = "BackButton";
    public static readonly StringName SectionTabButton = "SectionTabButton";
    public static readonly StringName SecondaryButton = "SecondaryButton";
    public static readonly StringName DangerButton = "DangerButton";
    public static readonly StringName WhitePanel = "WhitePanel";
    public static readonly StringName AdvertisementPanel = "AdvertisementPanel";
    public static readonly StringName FooterPanel = "FooterPanel";
    public static readonly StringName StatusPanel = "StatusPanel";

    public static void RegisterFallbackBaseTypes(Theme theme)
    {
        theme.SetTypeVariation(ScreenTitle, "Label");
        theme.SetTypeVariation(ScreenDescription, "Label");
        theme.SetTypeVariation(PanelTitle, "Label");
        theme.SetTypeVariation(PanelText, "Label");
        theme.SetTypeVariation(FieldLabel, "Label");
        theme.SetTypeVariation(SupportText, "Label");
        theme.SetTypeVariation(StatusText, "Label");
        theme.SetTypeVariation(WarningText, "Label");
        theme.SetTypeVariation(ErrorText, "Label");
        theme.SetTypeVariation(PrimaryButton, "Button");
        theme.SetTypeVariation(CompactButton, "Button");
        theme.SetTypeVariation(BackButton, "Button");
        theme.SetTypeVariation(SectionTabButton, "Button");
        theme.SetTypeVariation(SecondaryButton, "Button");
        theme.SetTypeVariation(DangerButton, "Button");
        theme.SetTypeVariation(WhitePanel, "PanelContainer");
        theme.SetTypeVariation(AdvertisementPanel, "PanelContainer");
        theme.SetTypeVariation(FooterPanel, "PanelContainer");
        theme.SetTypeVariation(StatusPanel, "PanelContainer");
    }
}

using Godot;

namespace Exchanger.Core.Theming;

/// <summary>
/// Минимальная аварийно-устойчивая тема приложения. Это не редактируемый
/// theme-проект: все разрабатываемые визуальные темы живут во внешнем DLC.
/// </summary>
public static class BuiltInFallbackTheme
{
    public static VendingThemeDefinition Create()
    {
        var uiTheme = new Theme
        {
            DefaultFontSize = 28,
        };
        ThemeSemanticTypes.RegisterFallbackBaseTypes(uiTheme);

        StyleBoxFlat buttonNormal = CreateButtonStyle(
            new Color("9ee04d"),
            new Color("d1ff85"),
            topMargin: 18,
            bottomMargin: 18);
        StyleBoxFlat buttonHover = CreateButtonStyle(
            new Color("b8f566"),
            new Color("e5ffad"),
            topMargin: 18,
            bottomMargin: 18,
            borderWidth: 3);
        StyleBoxFlat buttonPressed = CreateButtonStyle(
            new Color("73b833"),
            new Color("c2f56b"),
            topMargin: 20,
            bottomMargin: 16,
            borderWidth: 3);
        var panel = new StyleBoxFlat
        {
            BgColor = new Color(0.025f, 0.075f, 0.16f, 0.94f),
            BorderColor = new Color(0.62f, 0.88f, 0.3f, 0.82f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 32,
            CornerRadiusTopRight = 32,
            CornerRadiusBottomRight = 32,
            CornerRadiusBottomLeft = 32,
            ShadowColor = new Color(0, 0.01f, 0.04f, 0.55f),
            ShadowSize = 22,
        };

        uiTheme.SetColor("font_color", "Button", new Color(0.005f, 0.03f, 0.08f));
        uiTheme.SetColor("font_hover_color", "Button", new Color(0, 0.015f, 0.05f));
        uiTheme.SetColor("font_pressed_color", "Button", new Color(0.94f, 1, 0.86f));
        uiTheme.SetColor("font_outline_color", "Button", Colors.Transparent);
        uiTheme.SetConstant("outline_size", "Button", 0);
        uiTheme.SetFontSize("font_size", "Button", 32);
        uiTheme.SetStylebox("normal", "Button", buttonNormal);
        uiTheme.SetStylebox("hover", "Button", buttonHover);
        uiTheme.SetStylebox("pressed", "Button", buttonPressed);
        uiTheme.SetStylebox("focus", "Button", buttonHover);
        uiTheme.SetColor("font_color", "Label", Colors.White);
        uiTheme.SetColor("font_shadow_color", "Label", Colors.Transparent);
        uiTheme.SetConstant("shadow_offset_x", "Label", 0);
        uiTheme.SetConstant("shadow_offset_y", "Label", 0);
        uiTheme.SetFontSize("font_size", "Label", 28);
        uiTheme.SetStylebox("panel", "PanelContainer", panel);

        return new VendingThemeDefinition
        {
            ThemeId = "default",
            DisplayName = "Встроенная fallback-тема",
            UiTheme = uiTheme,
            FallbackColor = new Color(0.012f, 0.035f, 0.105f),
        };
    }

    private static StyleBoxFlat CreateButtonStyle(
        Color background,
        Color border,
        float topMargin,
        float bottomMargin,
        int borderWidth = 2)
    {
        return new StyleBoxFlat
        {
            ContentMarginLeft = 30,
            ContentMarginTop = topMargin,
            ContentMarginRight = 30,
            ContentMarginBottom = bottomMargin,
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = 22,
            CornerRadiusTopRight = 22,
            CornerRadiusBottomRight = 22,
            CornerRadiusBottomLeft = 22,
        };
    }
}

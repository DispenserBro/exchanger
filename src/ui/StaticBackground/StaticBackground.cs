using Exchanger.Core.Theming;
using Godot;

namespace Exchanger.UI.StaticBackground;

/// <summary>
/// Отображает legacy-фон текущей темы и инстанцирует доверенную runtime decoration-сцену.
/// </summary>
public partial class StaticBackground : Control
{
    private ColorRect _backgroundColor = null!;
    private TextureRect _backgroundImage = null!;
    private Node2D _decorationRoot = null!;
    private ThemeManager _themeManager = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _backgroundColor = GetNode<ColorRect>("BackgroundColor");
        _backgroundImage = GetNode<TextureRect>("BackgroundImage");
        _decorationRoot = GetNode<Node2D>("DecorationRoot");

        _themeManager = GetNode<ThemeManager>("/root/ThemeManager");
        _themeManager.ThemeChanged += ApplyTheme;

        if (_themeManager.CurrentTheme is not null)
        {
            ApplyTheme(_themeManager.CurrentTheme);
        }
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_themeManager))
        {
            _themeManager.ThemeChanged -= ApplyTheme;
        }
    }

    private void ApplyTheme(VendingThemeDefinition definition)
    {
        ClearDecorations();

        _backgroundColor.Color = definition.FallbackColor;
        _backgroundImage.Texture = definition.BackgroundTexture;
        _backgroundImage.TextureFilter = definition.UseNearestTextureFilter
            ? CanvasItem.TextureFilterEnum.Nearest
            : CanvasItem.TextureFilterEnum.ParentNode;
        _backgroundImage.Visible = definition.BackgroundTexture is not null;

        if (!_themeManager.ReducedEffects && definition.BackgroundDecoration is not null)
        {
            Node decoration = definition.BackgroundDecoration.Instantiate();
            if (decoration is Node2D decoration2D)
            {
                decoration2D.Scale *= Vector2.One * definition.BackgroundDecorationScale;
            }
            else if (decoration is Control decorationControl)
            {
                decorationControl.Scale *= Vector2.One * definition.BackgroundDecorationScale;
            }

            _decorationRoot.AddChild(decoration);
        }
    }

    private void ClearDecorations()
    {
        foreach (Node child in _decorationRoot.GetChildren())
        {
            child.QueueFree();
        }
    }
}

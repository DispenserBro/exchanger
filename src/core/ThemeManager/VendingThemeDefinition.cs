using Godot;
using Exchanger.Core.Navigation;
using System.Collections.Generic;

namespace Exchanger.Core.Theming;

/// <summary>
/// Единое описание визуальной темы: стили Control, статический фон и
/// необязательные тематические 2D-декорации.
/// </summary>
[GlobalClass]
public partial class VendingThemeDefinition : Resource
{
    [Export]
    public string ThemeId { get; set; } = "base";

    [Export]
    public string DisplayName { get; set; } = "Базовая";

    [Export]
    public Theme? UiTheme { get; set; }

    [Export]
    public Texture2D? BackgroundTexture { get; set; }

    [Export]
    public PackedScene? BackgroundDecoration { get; set; }

    [Export]
    public PackedScene? InteractivePetScene { get; set; }

    public float BackgroundDecorationScale { get; set; } = 1f;

    [Export]
    public Color FallbackColor { get; set; } = new("101827");

    [Export]
    public bool UseNearestTextureFilter { get; set; }

    public IReadOnlyDictionary<string, PackedScene> VisualSlots { get; set; }
        = new Dictionary<string, PackedScene>();

    public IReadOnlyDictionary<ScreenId, PackedScene> ScreenScenes { get; set; }
        = new Dictionary<ScreenId, PackedScene>();

    public IReadOnlyDictionary<ScreenId, ThemeScreenBindingContract> ScreenBindingContracts { get; set; }
        = new Dictionary<ScreenId, ThemeScreenBindingContract>();

    public bool SelfContainedScreenVisuals { get; set; }

}

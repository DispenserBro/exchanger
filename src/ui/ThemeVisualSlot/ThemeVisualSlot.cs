using Exchanger.Core.Theming;
using Godot;

namespace Exchanger.UI.ThemeVisualSlot;

/// <summary>
/// Точка расширения для тематической сцены конкретного экрана.
/// Доверенный DLC может содержать собственный GDScript и runtime-узлы.
/// </summary>
public partial class ThemeVisualSlot : Control
{
    [Export]
    public string SlotId { get; set; } = string.Empty;

    private ThemeManager _themeManager = null!;
    private Node? _activeVisual;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
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
        if (GodotObject.IsInstanceValid(_activeVisual))
        {
            _activeVisual!.QueueFree();
            _activeVisual = null;
        }

        if (string.IsNullOrWhiteSpace(SlotId)
            || !definition.VisualSlots.TryGetValue(SlotId, out PackedScene? packedScene))
        {
            return;
        }

        Node visual = packedScene.Instantiate();
        AddChild(visual);
        MoveChild(visual, 0);
        _activeVisual = visual;
    }
}

using Exchanger.Core.Configuration;
using Exchanger.Core.Logging;
using Godot;

namespace Exchanger.Core.Theming;

/// <summary>
/// Загружает единственную встроенную fallback-тему и при наличии дополняет её
/// одной проверенной темой из внешнего DLC resource pack.
/// </summary>
public partial class ThemeManager : Node
{
    [Signal]
    public delegate void ThemeChangedEventHandler(VendingThemeDefinition theme);

    private VendingThemeDefinition? _currentTheme;

    public VendingThemeDefinition? CurrentTheme => _currentTheme;

    public bool ReducedEffects { get; private set; }

    public bool IsExternalDlcActive { get; private set; }

    public void Configure(ThemeSettings settings)
    {
        ReducedEffects = settings.ReducedEffects;
        IsExternalDlcActive = false;
        _currentTheme = BuiltInFallbackTheme.Create();

        if (settings.ExternalDlcEnabled)
        {
            ThemeDlcLoadResult result = ThemeDlcLoader.Load(settings.ExternalDlcPath);
            if (result.Status == ThemeDlcLoadStatus.Loaded && result.Theme is not null)
            {
                _currentTheme = result.Theme;
                IsExternalDlcActive = true;
                AppLogger.Info("Theme", $"Активирована внешняя DLC-тема {result.Theme.ThemeId} ({result.Theme.DisplayName}).");
            }
            else if (result.Status == ThemeDlcLoadStatus.NotFound)
            {
                AppLogger.Info("Theme", "Внешний DLC темы не найден; используется встроенная тема.");
            }
            else if (result.Status != ThemeDlcLoadStatus.Disabled)
            {
                string contractDetails = result.BindingContractFailure == ThemeBindingContractLoadFailure.None
                    ? string.Empty
                    : $", контракт: {result.BindingContractFailure}";
                string sceneDetails = result.ScreenSceneFailure == ThemeScreenSceneFailure.None
                    ? string.Empty
                    : $", сцена {result.ScreenBindingId}: {result.ScreenSceneFailure}";
                AppLogger.Warning(
                    "Theme",
                    $"DLC темы отклонён ({result.Status}{contractDetails}{sceneDetails}); используется встроенная тема.");
            }
        }

        if (_currentTheme.UiTheme is not null)
        {
            // Manifest v1 themes remain forward-compatible when the host adds a
            // semantic role: an unstyled role safely inherits its Godot base type.
            ThemeSemanticTypes.RegisterFallbackBaseTypes(_currentTheme.UiTheme);
        }

        AppLogger.Info(
            "Theme",
            IsExternalDlcActive
                ? "Тема загружена из внешнего DLC."
                : "Активирована единственная встроенная тема по умолчанию.");
        EmitSignal(SignalName.ThemeChanged, _currentTheme);
    }

    public void SetReducedEffects(bool reducedEffects)
    {
        if (ReducedEffects == reducedEffects)
        {
            return;
        }

        ReducedEffects = reducedEffects;
        AppLogger.Info("Theme", reducedEffects
            ? "Включён облегчённый режим визуальных эффектов."
            : "Включён полный режим визуальных эффектов.");

        if (_currentTheme is not null)
        {
            EmitSignal(SignalName.ThemeChanged, _currentTheme);
        }
    }

    public void ApplyTo(Control root)
    {
        if (root is ThemeBindableScreen { UsesBuiltInFallbackVisuals: true })
        {
            root.TextureFilter = CanvasItem.TextureFilterEnum.ParentNode;
            root.Theme = BuiltInFallbackTheme.Create().UiTheme;
            return;
        }

        if (root is ThemeBindableScreen { UsesSelfContainedThemeScene: true })
        {
            // Полносценовая тема уже содержит собственные Theme, фильтрацию,
            // геометрию и type variations. Host не должен менять её визуалы.
            return;
        }

        // Pixel-art DLC resources are scaled by controls owned by the host
        // application, so their filter cannot be selected from inside the PCK.
        // Keep the built-in theme on the project default and make an external
        // theme crisp without changing the global renderer configuration.
        root.TextureFilter = _currentTheme?.UseNearestTextureFilter == true
            ? CanvasItem.TextureFilterEnum.Nearest
            : CanvasItem.TextureFilterEnum.ParentNode;

        if (_currentTheme?.UiTheme is not null)
        {
            root.Theme = _currentTheme.UiTheme;
        }
    }
}

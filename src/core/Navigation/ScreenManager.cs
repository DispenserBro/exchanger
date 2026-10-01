using Exchanger.Core.Logging;
using Exchanger.Core.Theming;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Exchanger.Core.Navigation;

/// <summary>
/// Владеет экземплярами экранов и выполняет защищённые UI-переходы.
/// Бизнес-логика оплаты здесь отсутствует.
/// </summary>
public partial class ScreenManager : Node
{
    [Signal]
    public delegate void ScreenChangedEventHandler(string screenId);

    private const float FadeSeconds = 0.16f;

    private readonly Dictionary<ScreenId, Control> _screens = new();
    private Control? _currentScreen;
    private Control? _inputBlocker;
    private bool _transitionInProgress;

    public ScreenId? CurrentScreenId { get; private set; }

    public bool TransitionInProgress => _transitionInProgress;

    public void Initialize(
        Control host,
        IReadOnlyDictionary<ScreenId, PackedScene> registry,
        IReadOnlyDictionary<ScreenId, PackedScene>? themeScenes = null,
        IReadOnlyDictionary<ScreenId, ThemeScreenBindingContract>? themeBindingContracts = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(registry);

        foreach (Node child in host.GetChildren())
        {
            host.RemoveChild(child);
            child.QueueFree();
        }

        _screens.Clear();
        _currentScreen = null;
        _inputBlocker = null;
        CurrentScreenId = null;
        _transitionInProgress = false;

        foreach ((ScreenId id, PackedScene packedScene) in registry)
        {
            Control screen = packedScene.Instantiate<Control>();
            if (themeScenes is not null
                && themeScenes.TryGetValue(id, out PackedScene? themePackedScene))
            {
                if (screen is not ThemeBindableScreen bindableScreen)
                {
                    throw new InvalidOperationException(
                        $"Контроллер экрана {id} не поддерживает полносценовые темы.");
                }

                Control themeView = themePackedScene.Instantiate<Control>();
                if (themeBindingContracts is null
                    || !themeBindingContracts.TryGetValue(id, out ThemeScreenBindingContract? bindingContract))
                {
                    themeView.Free();
                    bindableScreen.UseBuiltInFallbackVisuals();
                    AppLogger.Warning(
                        "Theme",
                        $"Для экрана {id} нет каталога биндингов; используется встроенный экран.");
                }
                else if (!bindableScreen.TryInstallThemeView(
                             themeView,
                             ThemeSceneContract.Screens.First(descriptor => descriptor.ScreenId == id),
                             bindingContract,
                             out int fallbackBindingCount,
                             out string fallbackFailure))
                {
                    themeView.Free();
                    AppLogger.Warning(
                        "Theme",
                        $"Экран {id} не удалось дополнить встроенными элементами ({fallbackFailure}); "
                        + "используется встроенный экран целиком.");
                }
                else if (fallbackBindingCount > 0)
                {
                    AppLogger.Info(
                        "Theme",
                        $"Экран {id}: восстановлена совместимость биндингов — {fallbackBindingCount}.");
                }
            }

            screen.Name = id.ToString();
            screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            screen.Visible = false;
            screen.ProcessMode = ProcessModeEnum.Disabled;
            screen.MouseFilter = Control.MouseFilterEnum.Stop;
            host.AddChild(screen);
            if (screen is ThemeBindableScreen themedScreen && themedScreen.UsesExternalThemeScene)
            {
                ThemeManager themeManager = GetNode<ThemeManager>("/root/ThemeManager");
                themedScreen.BindThemeRuntime(themeManager);
            }
            _screens.Add(id, screen);
        }

        _inputBlocker = new Control
        {
            Name = "TransitionInputBlocker",
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.None,
            Visible = false,
        };
        _inputBlocker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        host.AddChild(_inputBlocker);
    }

    public T GetScreen<T>(ScreenId id) where T : Control
    {
        if (!_screens.TryGetValue(id, out Control? screen))
        {
            throw new InvalidOperationException($"Экран {id} не зарегистрирован.");
        }

        if (screen is not T typedScreen)
        {
            throw new InvalidOperationException($"Экран {id} имеет тип {screen.GetType().Name}, ожидался {typeof(T).Name}.");
        }

        return typedScreen;
    }

    public bool Show(ScreenId id, bool animate = true)
    {
        if (_transitionInProgress || CurrentScreenId == id)
        {
            return false;
        }

        if (!_screens.TryGetValue(id, out Control? nextScreen))
        {
            AppLogger.Error("Navigation", $"Попытка открыть незарегистрированный экран {id}.");
            return false;
        }

        if (_currentScreen is not null)
        {
            _currentScreen.Visible = false;
            _currentScreen.ProcessMode = ProcessModeEnum.Disabled;
            _currentScreen.Modulate = Colors.White;
        }

        _currentScreen = nextScreen;
        CurrentScreenId = id;
        nextScreen.ProcessMode = ProcessModeEnum.Inherit;
        nextScreen.Visible = true;
        nextScreen.MoveToFront();

        if (!animate || !IsInsideTree())
        {
            nextScreen.Modulate = Colors.White;
            EmitSignal(SignalName.ScreenChanged, id.ToString());
            return true;
        }

        _transitionInProgress = true;
        if (_inputBlocker is not null)
        {
            _inputBlocker.Visible = true;
            _inputBlocker.MoveToFront();
        }

        nextScreen.Modulate = new Color(1f, 1f, 1f, 0f);
        Tween tween = CreateTween();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(nextScreen, "modulate:a", 1f, FadeSeconds)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(nextScreen))
            {
                nextScreen.Modulate = Colors.White;
            }

            _transitionInProgress = false;
            if (GodotObject.IsInstanceValid(_inputBlocker))
            {
                _inputBlocker.Visible = false;
            }
        };

        EmitSignal(SignalName.ScreenChanged, id.ToString());
        AppLogger.Info("Navigation", $"Открыт экран {id}.");
        return true;
    }
}

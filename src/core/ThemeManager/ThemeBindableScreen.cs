using Godot;
using System;
using System.Collections.Generic;

namespace Exchanger.Core.Theming;

/// <summary>
/// App-owned контроллер экрана. DLC заменяет визуальный корень и может выполнять
/// доверенный GDScript, а обязательная логика оплаты, навигации и оборудования
/// остаётся подключённой C#-контроллером через binding-id.
/// </summary>
public abstract partial class ThemeBindableScreen : Control
{
    private Control? _externalThemeView;
    private IReadOnlyDictionary<string, Node>? _externalBindings;
    private CanvasItem? _selfContainedDecoration;
    private ThemeManager? _runtimeThemeManager;

    protected abstract string ThemeScreenKey { get; }

    public bool UsesExternalThemeScene => _externalThemeView is not null;

    public bool UsesBuiltInFallbackVisuals { get; private set; }

    internal void UseBuiltInFallbackVisuals() => UsesBuiltInFallbackVisuals = true;

    /// <summary>
    /// Самодостаточная сцена владеет своим визуальным деревом. Host меняет только
    /// функциональные значения и подключает сигналы по binding-id. Исключение —
    /// отсутствующий обязательный binding: для обратной совместимости он дополняется
    /// готовым узлом встроенного экрана по закреплённому fallback-path.
    /// </summary>
    public bool UsesSelfContainedThemeScene =>
        _externalThemeView is not null
        && _externalThemeView.HasMeta(ThemeSceneContract.SelfContainedVisualsMetadata)
        && _externalThemeView.GetMeta(ThemeSceneContract.SelfContainedVisualsMetadata).VariantType
            == Variant.Type.Bool
        && _externalThemeView.GetMeta(ThemeSceneContract.SelfContainedVisualsMetadata).AsBool();

    public virtual bool TryInstallThemeView(
        Control themeView,
        ThemeScreenDescriptor descriptor,
        ThemeScreenBindingContract bindingContract,
        out int fallbackBindingCount,
        out string failure)
    {
        ArgumentNullException.ThrowIfNull(themeView);
        ArgumentNullException.ThrowIfNull(bindingContract);
        fallbackBindingCount = 0;
        failure = string.Empty;
        if (IsInsideTree())
        {
            throw new InvalidOperationException("Тематический экран необходимо установить до входа контроллера в дерево.");
        }

        Control? defaultView = base.GetNodeOrNull<Control>("SafeMargin");
        Control? defaultFooter = base.GetNodeOrNull<Control>("Footer");
        if (defaultView is null)
        {
            UsesBuiltInFallbackVisuals = true;
            failure = "во встроенном экране отсутствует SafeMargin";
            return false;
        }

        if (!ThemeBindingFallbackComposer.TryCompose(
                this,
                themeView,
                descriptor,
                bindingContract,
                out IReadOnlyDictionary<string, Node> bindings,
                out fallbackBindingCount,
                out failure))
        {
            UsesBuiltInFallbackVisuals = true;
            return false;
        }

        RemoveChild(defaultView);
        defaultView.Free();
        if (defaultFooter is not null && defaultFooter.GetParent() == this)
        {
            RemoveChild(defaultFooter);
            defaultFooter.Free();
        }

        PrepareSelfContainedVisuals(themeView);
        AddChild(themeView);
        _externalThemeView = themeView;
        _externalBindings = bindings;
        return true;
    }

    public void BindThemeRuntime(ThemeManager themeManager)
    {
        ArgumentNullException.ThrowIfNull(themeManager);
        if (_runtimeThemeManager is not null)
        {
            throw new InvalidOperationException("Runtime темы уже подключён к контроллеру экрана.");
        }

        _runtimeThemeManager = themeManager;
        _runtimeThemeManager.ThemeChanged += OnRuntimeThemeChanged;
        TreeExiting += OnThemeScreenTreeExiting;
        ApplyReducedEffects(themeManager.ReducedEffects);
    }

    public new T GetNode<T>(NodePath path) where T : Node
    {
        string pathText = path.ToString();
        if (_externalThemeView is null
            || pathText.StartsWith("/", StringComparison.Ordinal))
        {
            return base.GetNode<T>(path);
        }

        string bindingId = ThemeBindingResolver.FromFallbackPath(ThemeScreenKey, path);
        return _externalBindings!.ContainsKey(bindingId)
            ? ThemeBindingResolver.Resolve<T>(_externalBindings, bindingId)
            : base.GetNode<T>(path);
    }

    public new Node GetNode(NodePath path)
    {
        string pathText = path.ToString();
        if (_externalThemeView is null
            || pathText.StartsWith("/", StringComparison.Ordinal))
        {
            return base.GetNode(path);
        }

        string bindingId = ThemeBindingResolver.FromFallbackPath(ThemeScreenKey, path);
        return _externalBindings!.ContainsKey(bindingId)
            ? ThemeBindingResolver.Resolve<Node>(_externalBindings, bindingId)
            : base.GetNode(path);
    }

    protected T GetThemeBinding<T>(string bindingId) where T : Node
    {
        if (_externalBindings is null)
        {
            throw new InvalidOperationException(
                $"Binding {bindingId} доступен только после установки внешней сцены темы.");
        }

        return ThemeBindingResolver.Resolve<T>(_externalBindings, bindingId);
    }

    protected T? GetOptionalThemeBinding<T>(string bindingId) where T : Node
    {
        if (_externalBindings is null)
        {
            throw new InvalidOperationException(
                $"Binding {bindingId} доступен только после установки внешней сцены темы.");
        }

        return _externalBindings.TryGetValue(bindingId, out Node? node)
            ? node as T
            : null;
    }

    private void PrepareSelfContainedVisuals(Control themeView)
    {
        if (!themeView.HasMeta(ThemeSceneContract.SelfContainedVisualsMetadata)
            || !themeView.GetMeta(ThemeSceneContract.SelfContainedVisualsMetadata).AsBool())
        {
            return;
        }

        _selfContainedDecoration = themeView.GetNodeOrNull<CanvasItem>(
            ThemeSceneContract.SelfContainedDecorationNode);
        if (_selfContainedDecoration is null)
        {
            return;
        }

    }

    private void OnRuntimeThemeChanged(VendingThemeDefinition definition)
    {
        if (_runtimeThemeManager is not null)
        {
            ApplyReducedEffects(_runtimeThemeManager.ReducedEffects);
        }
    }

    private void ApplyReducedEffects(bool reducedEffects)
    {
        if (!GodotObject.IsInstanceValid(_selfContainedDecoration))
        {
            return;
        }

        _selfContainedDecoration!.Visible = !reducedEffects;
        _selfContainedDecoration.ProcessMode = reducedEffects
            ? ProcessModeEnum.Disabled
            : ProcessModeEnum.Inherit;
    }

    private void OnThemeScreenTreeExiting()
    {
        TreeExiting -= OnThemeScreenTreeExiting;
        if (_runtimeThemeManager is not null)
        {
            _runtimeThemeManager.ThemeChanged -= OnRuntimeThemeChanged;
            _runtimeThemeManager = null;
        }
    }
}

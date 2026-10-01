using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Exchanger.Core.Theming;

/// <summary>
/// Дополняет неполную внешнюю сцену готовыми узлами встроенного экрана.
/// Узлы переносятся только по объявленным fallback-path и сохраняют встроенную Theme.
/// </summary>
internal static class ThemeBindingFallbackComposer
{
    public static bool TryCompose(
        Control fallbackHost,
        Control externalView,
        ThemeScreenDescriptor descriptor,
        ThemeScreenBindingContract bindingContract,
        out IReadOnlyDictionary<string, Node> bindings,
        out int fallbackBindingCount,
        out string failure)
    {
        ArgumentNullException.ThrowIfNull(fallbackHost);
        ArgumentNullException.ThrowIfNull(externalView);
        ArgumentNullException.ThrowIfNull(bindingContract);

        IReadOnlyDictionary<string, Node> externalBindings =
            ThemeBindingResolver.BuildIndex(externalView);
        bindings = externalBindings;
        fallbackBindingCount = 0;
        failure = string.Empty;

        ThemeSceneContract.FallbackBindingRequirement[] requirements =
            ThemeSceneContract.GetFallbackBindings(descriptor.ScreenId, bindingContract)
                .GroupBy(requirement => requirement.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();

        ThemeSceneContract.FallbackBindingRequirement[] missing = requirements
            .Where(requirement => requirement.Required && !externalBindings.ContainsKey(requirement.Id))
            .OrderBy(requirement => GetPathDepth(requirement.FallbackPath))
            .ToArray();
        if (missing.Length == 0)
        {
            return true;
        }

        int compatibilityBindingCount = missing.Length;
        foreach (ThemeSceneContract.FallbackBindingRequirement requirement in missing)
        {
            Node? legacyNode = externalView.GetNodeOrNull<Node>(requirement.FallbackPath);
            if (legacyNode is not null
                && !legacyNode.HasMeta(ThemeSceneContract.ElementBindingMetadata)
                && requirement.Types.Any(type => legacyNode.IsClass(type)))
            {
                legacyNode.SetMeta(ThemeSceneContract.ElementBindingMetadata, requirement.Id);
            }
        }

        externalBindings = ThemeBindingResolver.BuildIndex(externalView);
        bindings = externalBindings;
        missing = requirements
            .Where(requirement => requirement.Required && !externalBindings.ContainsKey(requirement.Id))
            .OrderBy(requirement => GetPathDepth(requirement.FallbackPath))
            .ToArray();
        if (missing.Length == 0)
        {
            fallbackBindingCount = compatibilityBindingCount;
            return true;
        }

        var fallbackNodes = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (ThemeSceneContract.FallbackBindingRequirement requirement in missing)
        {
            if (string.IsNullOrWhiteSpace(requirement.FallbackPath))
            {
                failure = $"для обязательного биндинга {requirement.Id} не объявлен fallback-path";
                return false;
            }

            Node? fallbackNode = fallbackHost.GetNodeOrNull<Node>(requirement.FallbackPath);
            if (fallbackNode is null)
            {
                failure = $"во встроенной сцене отсутствует fallback {requirement.FallbackPath}";
                return false;
            }

            if (!requirement.Types.Any(type => fallbackNode.IsClass(type)))
            {
                failure = $"fallback {requirement.FallbackPath} имеет несовместимый тип {fallbackNode.GetClass()}";
                return false;
            }

            fallbackNodes.Add(requirement.Id, fallbackNode);
        }

        ThemeSceneContract.FallbackBindingRequirement[] transplantRoots =
            SelectTransplantRoots(missing);
        var targetParents = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (ThemeSceneContract.FallbackBindingRequirement rootRequirement in transplantRoots)
        {
            string parentPath = GetParentPath(rootRequirement.FallbackPath);
            Node? targetParent = string.IsNullOrEmpty(parentPath)
                ? externalView
                : externalView.GetNodeOrNull<Node>(parentPath);
            if (targetParent is null)
            {
                string parentBindingId = ThemeBindingResolver.FromFallbackPath(descriptor.Key, parentPath);
                externalBindings.TryGetValue(parentBindingId, out targetParent);
            }

            if (targetParent is null)
            {
                failure = $"в тематической сцене отсутствует родитель для fallback {rootRequirement.FallbackPath}";
                return false;
            }

            targetParents.Add(rootRequirement.Id, targetParent);
        }

        foreach (ThemeSceneContract.FallbackBindingRequirement requirement in missing)
        {
            fallbackNodes[requirement.Id].SetMeta(
                ThemeSceneContract.ElementBindingMetadata,
                requirement.Id);
        }

        Theme fallbackTheme = BuiltInFallbackTheme.Create().UiTheme!;
        foreach (ThemeSceneContract.FallbackBindingRequirement rootRequirement in transplantRoots)
        {
            Node fallbackNode = fallbackNodes[rootRequirement.Id];
            Node? oldParent = fallbackNode.GetParent();
            int fallbackIndex = fallbackNode.GetIndex();
            oldParent?.RemoveChild(fallbackNode);
            ClearOwners(fallbackNode);
            targetParents[rootRequirement.Id].AddChild(fallbackNode, forceReadableName: true);
            PlaceFallbackNodeAtBuiltInPosition(fallbackNode, targetParents[rootRequirement.Id], oldParent, fallbackIndex);
            ApplyBuiltInTheme(fallbackNode, fallbackTheme);
        }

        bindings = ThemeBindingResolver.BuildIndex(externalView);
        foreach (ThemeSceneContract.FallbackBindingRequirement requirement in missing)
        {
            if (!bindings.TryGetValue(requirement.Id, out Node? node)
                || !requirement.Types.Any(type => node.IsClass(type)))
            {
                failure = $"не удалось подключить встроенный fallback для {requirement.Id}";
                return false;
            }
        }

        fallbackBindingCount = compatibilityBindingCount;
        return true;
    }

    internal static ThemeSceneContract.FallbackBindingRequirement[] SelectTransplantRoots(
        IReadOnlyCollection<ThemeSceneContract.FallbackBindingRequirement> missing)
    {
        return missing
            .Where(candidate => !missing.Any(other =>
                !candidate.Id.Equals(other.Id, StringComparison.Ordinal)
                && IsDescendantPath(candidate.FallbackPath, other.FallbackPath)))
            .OrderBy(candidate => GetPathDepth(candidate.FallbackPath))
            .ToArray();
    }

    /// <summary>
    /// Внешняя старая тема может содержать следующую готовую панель, но не новую
    /// supplemental-панель. Вставляем fallback перед ближайшим известным соседним
    /// узлом встроенной сцены, а не добавляем его в конец. Так порядок Settings
    /// не меняется: например, проверка звука остаётся после всех ползунков.
    /// </summary>
    private static void PlaceFallbackNodeAtBuiltInPosition(
        Node fallbackNode,
        Node targetParent,
        Node? fallbackParent,
        int fallbackIndex)
    {
        if (fallbackParent is null)
        {
            return;
        }

        for (int index = fallbackIndex; index < fallbackParent.GetChildCount(); index++)
        {
            Node builtInSibling = fallbackParent.GetChild(index);
            Node? targetSibling = targetParent.GetNodeOrNull<Node>(builtInSibling.Name.ToString());
            if (targetSibling is not null && targetSibling.GetParent() == targetParent)
            {
                targetParent.MoveChild(fallbackNode, targetSibling.GetIndex());
                return;
            }
        }
    }

    private static void ApplyBuiltInTheme(Node root, Theme fallbackTheme)
    {
        if (root is Control control)
        {
            control.Theme = fallbackTheme;
        }

        foreach (Node child in root.GetChildren())
        {
            ApplyBuiltInTheme(child, fallbackTheme);
        }
    }

    private static void ClearOwners(Node root)
    {
        root.Owner = null;
        foreach (Node child in root.GetChildren())
        {
            ClearOwners(child);
        }
    }

    private static bool IsDescendantPath(string candidate, string ancestor) =>
        candidate.StartsWith(ancestor + "/", StringComparison.Ordinal);

    private static int GetPathDepth(string path) =>
        path.Count(character => character == '/');

    private static string GetParentPath(string path)
    {
        int separator = path.LastIndexOf('/');
        return separator > 0 ? path[..separator] : string.Empty;
    }
}

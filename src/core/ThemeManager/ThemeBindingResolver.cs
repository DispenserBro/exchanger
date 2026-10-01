using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace Exchanger.Core.Theming;

/// <summary>Разрешает узлы тематической сцены по стабильным metadata binding-id.</summary>
public static class ThemeBindingResolver
{
    public static string FromFallbackPath(string screenKey, NodePath fallbackPath)
        => FromFallbackPath(screenKey, fallbackPath.ToString());

    public static string FromFallbackPath(string screenKey, string fallbackPath)
    {
        string path = fallbackPath.Trim();
        if (path.StartsWith("SafeMargin/", StringComparison.Ordinal))
        {
            path = path["SafeMargin/".Length..];
        }

        var result = new StringBuilder(screenKey);
        foreach (string segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            result.Append('.');
            AppendSnakeCase(result, segment);
        }

        return result.ToString();
    }

    public static T Resolve<T>(Node bindingRoot, string bindingId) where T : Node
    {
        ArgumentNullException.ThrowIfNull(bindingRoot);

        Node? match = null;
        Find(bindingRoot, bindingId, ref match);
        if (match is T typed)
        {
            return typed;
        }

        if (match is not null)
        {
            throw new InvalidOperationException(
                $"Биндинг темы '{bindingId}' имеет тип {match.GetClass()}, ожидался {typeof(T).Name}.");
        }

        throw new InvalidOperationException($"В тематической сцене отсутствует обязательный биндинг '{bindingId}'.");
    }

    public static T Resolve<T>(IReadOnlyDictionary<string, Node> bindings, string bindingId) where T : Node
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (!bindings.TryGetValue(bindingId, out Node? match))
        {
            throw new InvalidOperationException($"В тематической сцене отсутствует обязательный биндинг '{bindingId}'.");
        }

        if (match is not T typed)
        {
            throw new InvalidOperationException(
                $"Биндинг темы '{bindingId}' имеет тип {match.GetClass()}, ожидался {typeof(T).Name}.");
        }

        return typed;
    }

    public static IReadOnlyDictionary<string, Node> BuildIndex(Node bindingRoot)
    {
        ArgumentNullException.ThrowIfNull(bindingRoot);
        var result = new Dictionary<string, Node>(StringComparer.Ordinal);
        Index(bindingRoot, result);
        return result;
    }

    private static void Find(Node node, string bindingId, ref Node? match)
    {
        if (match is not null)
        {
            return;
        }

        if (node.HasMeta(ThemeSceneContract.ElementBindingMetadata)
            && node.GetMeta(ThemeSceneContract.ElementBindingMetadata).AsString()
                .Equals(bindingId, StringComparison.Ordinal))
        {
            match = node;
            return;
        }

        foreach (Node child in node.GetChildren())
        {
            Find(child, bindingId, ref match);
        }
    }

    private static void Index(Node node, IDictionary<string, Node> result)
    {
        if (node.HasMeta(ThemeSceneContract.ElementBindingMetadata))
        {
            string bindingId = node.GetMeta(ThemeSceneContract.ElementBindingMetadata).AsString();
            if (!string.IsNullOrWhiteSpace(bindingId) && !result.TryAdd(bindingId, node))
            {
                throw new InvalidOperationException($"Биндинг темы '{bindingId}' указан более одного раза.");
            }
        }

        foreach (Node child in node.GetChildren())
        {
            Index(child, result);
        }
    }

    private static void AppendSnakeCase(StringBuilder result, string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (char.IsUpper(character) && index > 0
                && (char.IsLower(value[index - 1]) || char.IsDigit(value[index - 1])))
            {
                result.Append('_');
            }

            result.Append(char.ToLowerInvariant(character));
        }
    }
}

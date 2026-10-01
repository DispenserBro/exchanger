using Godot;
using System;
using System.Collections.Generic;

namespace Exchanger.Core.Navigation;

/// <summary>
/// Единственная точка сопоставления логических экранов и сцен.
/// </summary>
public static class ScreenRegistry
{
    private static readonly IReadOnlyDictionary<ScreenId, string> ScenePaths =
        new Dictionary<ScreenId, string>
        {
            [ScreenId.Home] = "res://src/scenes/HomeScreen/HomeScreen.tscn",
            [ScreenId.CashPayment] = "res://src/scenes/CashPaymentScreen/CashPaymentScreen.tscn",
            [ScreenId.CardAmount] = "res://src/scenes/CardAmountScreen/CardAmountScreen.tscn",
            [ScreenId.CardCustomAmount] = "res://src/scenes/CardCustomAmountScreen/CardCustomAmountScreen.tscn",
            [ScreenId.CardTerminal] = "res://src/scenes/CardTerminalScreen/CardTerminalScreen.tscn",
            [ScreenId.Success] = "res://src/scenes/SuccessScreen/SuccessScreen.tscn",
            [ScreenId.Error] = "res://src/scenes/ErrorScreen/ErrorScreen.tscn",
            [ScreenId.ServiceAccess] = "res://src/scenes/ServiceAccessScreen/ServiceAccessScreen.tscn",
            [ScreenId.Settings] = "res://src/scenes/SettingsScreen/SettingsScreen.tscn",
        };

    public static IReadOnlyDictionary<ScreenId, PackedScene> LoadDefault()
    {
        var result = new Dictionary<ScreenId, PackedScene>();
        foreach ((ScreenId id, string path) in ScenePaths)
        {
            PackedScene? scene = ResourceLoader.Load<PackedScene>(path);
            if (scene is null)
            {
                throw new InvalidOperationException($"Не удалось загрузить экран {id}: {path}");
            }

            result.Add(id, scene);
        }

        return result;
    }
}

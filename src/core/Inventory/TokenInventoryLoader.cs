using Exchanger.Core.Logging;
using Godot;

namespace Exchanger.Core.Inventory;

public static class TokenInventoryLoader
{
    private const string UserInventoryPath = "user://data/token_inventory.json";

    public static TokenInventoryStore CreateStore() => new(
        ProjectSettings.GlobalizePath(UserInventoryPath),
        warningSink: message => AppLogger.Warning("TokenInventory", message));
}

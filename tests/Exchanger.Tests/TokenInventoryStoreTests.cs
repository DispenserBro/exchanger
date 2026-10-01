using Exchanger.Core.Inventory;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class TokenInventoryStoreTests
{
    private string _directory = null!;
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Exchanger.Tests", Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_directory, "token_inventory.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void FirstLoad_CreatesZeroInventory()
    {
        var store = CreateStore();

        TokenInventorySnapshot snapshot = store.Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.Count, Is.Zero);
            Assert.That(snapshot.Revision, Is.Zero);
            Assert.That(File.Exists(_path), Is.True);
        }
    }

    [Test]
    public void ManualAddition_IsPersistedAcrossStoreInstances()
    {
        var store = CreateStore();
        store.Load();
        store.Add(42);

        TokenInventorySnapshot restored = CreateStore().Load();

        Assert.That(restored.Count, Is.EqualTo(42));
    }

    [Test]
    public void Recount_ReplacesPreviousInventory()
    {
        var store = CreateStore();
        store.Load();
        store.Add(100);

        TokenInventorySnapshot replaced = store.SetCount(37);

        Assert.That(replaced.Count, Is.EqualTo(37));
    }

    [Test]
    public void Dispense_DecrementsInventoryWithoutGoingBelowZero()
    {
        var store = CreateStore();
        store.Load();
        store.Add(5);

        TokenInventorySnapshot result = store.RemoveDispensed(12);

        Assert.That(result.Count, Is.Zero);
    }

    [Test]
    public void HopperOperations_PersistSeparateCountsAndCombinedTotal()
    {
        var store = CreateStore();
        store.Load();
        store.Add(TokenHopper.Hopper1, 120);
        store.Add(TokenHopper.Hopper2, 350);
        store.RemoveDispensed(TokenHopper.Hopper2, 50);

        TokenInventorySnapshot restored = CreateStore().Load();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(restored.Hopper1Count, Is.EqualTo(120));
            Assert.That(restored.Hopper2Count, Is.EqualTo(300));
            Assert.That(restored.Count, Is.EqualTo(420));
        }
    }

    [Test]
    public void Recount_ReplacesOnlySelectedHopper()
    {
        var store = CreateStore();
        store.Load();
        store.SetCounts(100, 200);

        TokenInventorySnapshot result = store.SetHopperCount(TokenHopper.Hopper1, 75);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Hopper1Count, Is.EqualTo(75));
            Assert.That(result.Hopper2Count, Is.EqualTo(200));
            Assert.That(result.Count, Is.EqualTo(275));
        }
    }

    [Test]
    public void CorruptPrimaryFile_IsRecoveredFromBackup()
    {
        var store = CreateStore();
        store.Load();
        store.SetCount(10);
        store.SetCount(20);
        File.WriteAllText(_path, "not-json");

        TokenInventorySnapshot recovered = CreateStore().Load();

        Assert.That(recovered.Count, Is.EqualTo(10));
    }

    private TokenInventoryStore CreateStore() => new(
        _path,
        () => new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero));
}

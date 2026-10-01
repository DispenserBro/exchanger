using Exchanger.Core.Logging;
using Exchanger.Core.Session;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class OperationJournalStoreTests
{
    private string _directory = null!;
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Exchanger.Tests", Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_directory, "operations.jsonl");
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
    public void CompletedOperation_LeavesNoUnfinishedRecord()
    {
        var store = CreateStore();
        SessionOperationSnapshot start = Snapshot("operation-1", OperationOutcome.InProgress);
        SessionOperationSnapshot finish = Snapshot("operation-1", OperationOutcome.Completed);

        store.RecordStarted(start);
        store.RecordFinished(finish);

        Assert.That(store.FindUnfinishedOperations(), Is.Empty);
        string journal = File.ReadAllText(_path);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(journal, Does.Contain("\"OperationId\":\"operation-1\""));
            Assert.That(journal, Does.Contain("\"Outcome\":\"Completed\""));
            Assert.That(journal, Does.Not.Contain("card_number"));
        }
    }

    [Test]
    public void RestartRecovery_ClosesUnfinishedOperationAsUnknownWithoutRetry()
    {
        var store = CreateStore();
        store.RecordStarted(Snapshot("operation-crashed", OperationOutcome.InProgress));

        IReadOnlyList<OperationJournalEntry> recovered = store.RecoverUnfinishedOperations();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recovered.Select(entry => entry.OperationId), Is.EqualTo(new[] { "operation-crashed" }));
            Assert.That(store.FindUnfinishedOperations(), Is.Empty);
            Assert.That(File.ReadAllText(_path), Does.Contain("\"Outcome\":\"UnknownAfterRestart\""));
        }
    }

    [Test]
    public void CorruptedLine_DoesNotHideValidUnfinishedOperation()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "not-json" + Environment.NewLine);
        var store = CreateStore();
        store.RecordStarted(Snapshot("operation-valid", OperationOutcome.InProgress));

        IReadOnlyList<OperationJournalEntry> unfinished = store.FindUnfinishedOperations();

        Assert.That(unfinished.Select(entry => entry.OperationId), Is.EqualTo(new[] { "operation-valid" }));
    }

    private OperationJournalStore CreateStore()
    {
        DateTimeOffset timestamp = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        return new OperationJournalStore(_path, () => timestamp);
    }

    private static SessionOperationSnapshot Snapshot(string id, OperationOutcome outcome) => new(
        id,
        PaymentMethod.Card,
        100,
        10,
        2,
        outcome,
        SessionErrorCode.None);
}

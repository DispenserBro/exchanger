using Exchanger.UI.InteractivePet;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class InteractivePetHostBehaviorTests
{
    [Test]
    public void LandingPhase_IsNotSkippedWhenScreenFloorAlreadyMatchesPetAnchor()
    {
        string host = File.ReadAllText(Path.Combine(
            ProjectPaths.FindRoot(),
            "src",
            "ui",
            "InteractivePet",
            "InteractivePetHost.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(host, Does.Contain("private void BeginLanding()"));
            Assert.That(host, Does.Contain("_currentSurfaceId = floor.Id;\n            BeginLanding();"));
            Assert.That(host, Does.Contain("_landingSeconds = FallbackLandingSeconds;"));
            Assert.That(host, Does.Contain("InteractivePetContract.AnimationCompletedSignal"));
            Assert.That(host, Does.Contain("private void OnPetAnimationCompleted(StringName animation)"));
            Assert.That(host, Does.Contain("_waitingForLandingCompletion = false;"));
            Assert.That(host, Does.Contain("LandingCompletionGraceSeconds"));
        }
    }

    [Test]
    public void IdleAfterWalkProbability_IsThirtyFivePercent()
    {
        Assert.That(InteractivePetHost.IdleAfterWalkProbability, Is.EqualTo(0.35f));
    }

    [TestCase(0f, true)]
    [TestCase(0.3499f, true)]
    [TestCase(0.35f, false)]
    [TestCase(0.9999f, false)]
    [TestCase(-0.01f, false)]
    public void ShouldUseIdleAfterWalk_UsesStableProbabilityBoundary(float roll, bool expected)
    {
        Assert.That(InteractivePetHost.ShouldUseIdleAfterWalk(roll), Is.EqualTo(expected));
    }

    [Test]
    public void ShouldUseIdleAfterWalk_RejectsNonFiniteRolls()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(InteractivePetHost.ShouldUseIdleAfterWalk(float.NaN), Is.False);
            Assert.That(InteractivePetHost.ShouldUseIdleAfterWalk(float.PositiveInfinity), Is.False);
            Assert.That(InteractivePetHost.ShouldUseIdleAfterWalk(float.NegativeInfinity), Is.False);
        }
    }

    [TestCase(0f, 0.2f)]
    [TestCase(0.25f, 0.35f)]
    [TestCase(0.5f, 0.5f)]
    [TestCase(0.75f, 0.65f)]
    [TestCase(1f, 0.8f)]
    [TestCase(-1f, 0.2f)]
    [TestCase(2f, 0.8f)]
    public void SelectClimbActionProgress_KeepsTriggerInsideClimb(
        float roll,
        float expected)
    {
        Assert.That(
            InteractivePetHost.SelectClimbActionProgress(roll),
            Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void SelectClimbActionProgress_UsesMiddleForNonFiniteRolls()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                InteractivePetHost.SelectClimbActionProgress(float.NaN),
                Is.EqualTo(0.5f));
            Assert.That(
                InteractivePetHost.SelectClimbActionProgress(float.PositiveInfinity),
                Is.EqualTo(0.5f));
            Assert.That(
                InteractivePetHost.SelectClimbActionProgress(float.NegativeInfinity),
                Is.EqualTo(0.5f));
        }
    }

    [TestCase(0f, 0)]
    [TestCase(0.3332f, 0)]
    [TestCase(1f / 3f, 1)]
    [TestCase(0.6665f, 1)]
    [TestCase(2f / 3f, 2)]
    [TestCase(0.9999f, 2)]
    [TestCase(-0.01f, 2)]
    public void SelectPostLandingBehavior_SplitsValidRollsIntoThreeEqualBranches(
        float roll,
        int expected)
    {
        Assert.That(InteractivePetHost.SelectPostLandingBehavior(roll), Is.EqualTo(expected));
    }

}

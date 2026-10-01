using Exchanger.UI.InteractivePet;
using Godot;
using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class InteractivePetNavigationTests
{
    [Test]
    public void FindClimbPath_UsesConnectedEdgesInBothDirections()
    {
        InteractivePetTopology topology = CreateTopology();

        var upward = InteractivePetNavigation.FindClimbPath(topology, "floor", "window");
        var downward = InteractivePetNavigation.FindClimbPath(topology, "window", "floor");

        Assert.That(upward, Has.Count.EqualTo(2));
        Assert.That(upward[0].FromSurfaceId, Is.EqualTo("floor"));
        Assert.That(upward[1].ToSurfaceId, Is.EqualTo("window"));
        Assert.That(downward, Has.Count.EqualTo(2));
        Assert.That(downward[0].Reversed, Is.True);
        Assert.That(downward[1].ToSurfaceId, Is.EqualTo("floor"));
    }

    [Test]
    public void FindClimbPath_ReturnsEmptyForDisconnectedSurface()
    {
        InteractivePetTopology topology = CreateTopology();

        Assert.That(
            InteractivePetNavigation.FindClimbPath(topology, "floor", "detached"),
            Is.Empty);
    }

    [Test]
    public void FindDropSurface_SelectsNearestSurfaceBelowRelease()
    {
        InteractivePetTopology topology = CreateTopology();

        InteractivePetSurface? surface = InteractivePetNavigation.FindDropSurface(
            topology,
            new Vector2(320f, 300f));

        Assert.That(surface?.Id, Is.EqualTo("window"));
    }

    [Test]
    public void FindDropSurface_FallsBackToDefaultFloorOutsidePlatforms()
    {
        InteractivePetTopology topology = CreateTopology();

        InteractivePetSurface? surface = InteractivePetNavigation.FindDropSurface(
            topology,
            new Vector2(700f, 300f));

        Assert.That(surface?.Id, Is.EqualTo("floor"));
    }

    [Test]
    public void ClampAndLandingAnchor_KeepPetInsideDeclaredNavigationGeometry()
    {
        InteractivePetTopology topology = CreateTopology();
        InteractivePetSurface button = topology.Surfaces["button"];

        Assert.That(
            InteractivePetNavigation.ClampAnchor(new Vector2(-50f, 1400f), topology.RoamingBounds),
            Is.EqualTo(new Vector2(0f, 1280f)));
        Assert.That(
            InteractivePetNavigation.LandingAnchor(button, 999f),
            Is.EqualTo(new Vector2(500f, 700f)));
    }

    [Test]
    public void FindHighestSurface_SelectsTopmostSurfaceAndNearestTie()
    {
        InteractivePetTopology topology = CreateTopology();

        InteractivePetSurface? highest = InteractivePetNavigation.FindHighestSurface(
            topology,
            650f);

        Assert.That(highest?.Id, Is.EqualTo("detached"));
    }

    private static InteractivePetTopology CreateTopology()
    {
        var surfaces = new Dictionary<string, InteractivePetSurface>
        {
            ["floor"] = new("floor", 0f, 720f, 1100f, new[] { 360f }),
            ["button"] = new("button", 200f, 500f, 700f, new[] { 350f }),
            ["window"] = new("window", 100f, 600f, 420f, new[] { 320f }),
            ["detached"] = new("detached", 620f, 700f, 250f, new[] { 660f }),
        };
        var edges = new[]
        {
            new InteractivePetClimbEdge(
                "left-floor-button", "floor", "button", new Vector2(32f, 1100f), new Vector2(32f, 700f), "left"),
            new InteractivePetClimbEdge(
                "right-button-window", "button", "window", new Vector2(688f, 700f), new Vector2(688f, 420f), "right"),
        };
        return new InteractivePetTopology(
            new Rect2(0f, 0f, 720f, 1280f),
            "floor",
            surfaces,
            edges);
    }
}

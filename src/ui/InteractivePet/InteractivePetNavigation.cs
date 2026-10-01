using Godot;
using System;
using System.Collections.Generic;

namespace Exchanger.UI.InteractivePet;

public sealed record InteractivePetSurface(
    string Id,
    float FromX,
    float ToX,
    float Y,
    IReadOnlyList<float> SitPoints);

public sealed record InteractivePetClimbEdge(
    string Id,
    string FromSurfaceId,
    string ToSurfaceId,
    Vector2 FromAnchor,
    Vector2 ToAnchor,
    string Side);

public readonly record struct InteractivePetClimbTraversal(
    InteractivePetClimbEdge Edge,
    bool Reversed)
{
    public string FromSurfaceId => Reversed ? Edge.ToSurfaceId : Edge.FromSurfaceId;

    public string ToSurfaceId => Reversed ? Edge.FromSurfaceId : Edge.ToSurfaceId;

    public Vector2 FromAnchor => Reversed ? Edge.ToAnchor : Edge.FromAnchor;

    public Vector2 ToAnchor => Reversed ? Edge.FromAnchor : Edge.ToAnchor;
}

public sealed class InteractivePetTopology
{
    public InteractivePetTopology(
        Rect2 roamingBounds,
        string defaultSurfaceId,
        IReadOnlyDictionary<string, InteractivePetSurface> surfaces,
        IReadOnlyList<InteractivePetClimbEdge> climbEdges)
    {
        RoamingBounds = roamingBounds;
        DefaultSurfaceId = defaultSurfaceId;
        Surfaces = surfaces;
        ClimbEdges = climbEdges;
    }

    public Rect2 RoamingBounds { get; }

    public string DefaultSurfaceId { get; }

    public IReadOnlyDictionary<string, InteractivePetSurface> Surfaces { get; }

    public IReadOnlyList<InteractivePetClimbEdge> ClimbEdges { get; }
}

public static class InteractivePetNavigation
{
    public static InteractivePetSurface? FindHighestSurface(
        InteractivePetTopology topology,
        float desiredX)
    {
        ArgumentNullException.ThrowIfNull(topology);
        InteractivePetSurface? highest = null;
        float bestHorizontalDistance = float.PositiveInfinity;
        foreach (InteractivePetSurface surface in topology.Surfaces.Values)
        {
            float clampedX = Mathf.Clamp(desiredX, surface.FromX, surface.ToX);
            float horizontalDistance = Mathf.Abs(desiredX - clampedX);
            if (highest is null
                || surface.Y < highest.Y
                || (Mathf.IsEqualApprox(surface.Y, highest.Y)
                    && horizontalDistance < bestHorizontalDistance))
            {
                highest = surface;
                bestHorizontalDistance = horizontalDistance;
            }
        }

        return highest;
    }

    public static IReadOnlyList<InteractivePetClimbTraversal> FindClimbPath(
        InteractivePetTopology topology,
        string fromSurfaceId,
        string toSurfaceId)
    {
        ArgumentNullException.ThrowIfNull(topology);
        if (fromSurfaceId.Equals(toSurfaceId, StringComparison.Ordinal))
        {
            return Array.Empty<InteractivePetClimbTraversal>();
        }

        var queue = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { fromSurfaceId };
        var previous = new Dictionary<string, (string SurfaceId, InteractivePetClimbTraversal Traversal)>(
            StringComparer.Ordinal);
        queue.Enqueue(fromSurfaceId);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (InteractivePetClimbEdge edge in topology.ClimbEdges)
            {
                InteractivePetClimbTraversal? traversal = null;
                if (edge.FromSurfaceId.Equals(current, StringComparison.Ordinal))
                {
                    traversal = new InteractivePetClimbTraversal(edge, Reversed: false);
                }
                else if (edge.ToSurfaceId.Equals(current, StringComparison.Ordinal))
                {
                    traversal = new InteractivePetClimbTraversal(edge, Reversed: true);
                }

                if (traversal is not { } nextTraversal
                    || !visited.Add(nextTraversal.ToSurfaceId))
                {
                    continue;
                }

                previous[nextTraversal.ToSurfaceId] = (current, nextTraversal);
                if (nextTraversal.ToSurfaceId.Equals(toSurfaceId, StringComparison.Ordinal))
                {
                    return ReconstructPath(previous, fromSurfaceId, toSurfaceId);
                }

                queue.Enqueue(nextTraversal.ToSurfaceId);
            }
        }

        return Array.Empty<InteractivePetClimbTraversal>();
    }

    public static Vector2 ClampAnchor(Vector2 anchor, Rect2 roamingBounds)
    {
        return new Vector2(
            Mathf.Clamp(anchor.X, roamingBounds.Position.X, roamingBounds.End.X),
            Mathf.Clamp(anchor.Y, roamingBounds.Position.Y, roamingBounds.End.Y));
    }

    public static Vector2 LandingAnchor(InteractivePetSurface surface, float desiredX)
    {
        ArgumentNullException.ThrowIfNull(surface);
        return new Vector2(
            Mathf.Clamp(desiredX, surface.FromX, surface.ToX),
            surface.Y);
    }

    public static InteractivePetSurface? FindDropSurface(
        InteractivePetTopology topology,
        Vector2 releasedAnchor,
        float upwardSnapTolerance = 24f)
    {
        ArgumentNullException.ThrowIfNull(topology);
        InteractivePetSurface? best = null;
        float bestY = float.PositiveInfinity;
        foreach (InteractivePetSurface surface in topology.Surfaces.Values)
        {
            if (releasedAnchor.X < surface.FromX
                || releasedAnchor.X > surface.ToX
                || surface.Y < releasedAnchor.Y - upwardSnapTolerance
                || surface.Y >= bestY)
            {
                continue;
            }

            best = surface;
            bestY = surface.Y;
        }

        if (best is not null)
        {
            return best;
        }

        return topology.Surfaces.TryGetValue(topology.DefaultSurfaceId, out InteractivePetSurface? fallback)
            ? fallback
            : null;
    }

    private static IReadOnlyList<InteractivePetClimbTraversal> ReconstructPath(
        IReadOnlyDictionary<string, (string SurfaceId, InteractivePetClimbTraversal Traversal)> previous,
        string fromSurfaceId,
        string toSurfaceId)
    {
        var reversed = new List<InteractivePetClimbTraversal>();
        string current = toSurfaceId;
        while (!current.Equals(fromSurfaceId, StringComparison.Ordinal))
        {
            if (!previous.TryGetValue(current, out var link))
            {
                return Array.Empty<InteractivePetClimbTraversal>();
            }

            reversed.Add(link.Traversal);
            current = link.SurfaceId;
        }

        reversed.Reverse();
        return reversed;
    }
}

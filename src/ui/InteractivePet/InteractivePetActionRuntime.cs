using Godot;
using Godot.Collections;
using System;

namespace Exchanger.UI.InteractivePet;

/// <summary>
/// Stable, run-scoped bridge exposed to trusted theme-owned composite pet actions.
/// It keeps world geometry and the logical anchor app-owned while allowing the
/// theme to temporarily drive movement through neutral policies.
/// </summary>
public sealed partial class InteractivePetActionRuntime : RefCounted
{
    public const string NavigationSuspended = "suspended";
    public const string NavigationThemeDriven = "theme_driven";
    public const string SupportSolid = "solid";
    public const string SupportIgnore = "ignore";
    public const string SupportAfterVerticalWrap = "after_vertical_wrap";
    public const string BoundsClamp = "clamp";
    public const string BoundsAllow = "allow";
    public const string BoundsWrapVerticalOnce = "wrap_vertical_once";

    private InteractivePetHost? _host;
    private readonly long _runId;
    private string _navigation = NavigationSuspended;
    private string _supportContacts = SupportIgnore;
    private string _screenBounds = BoundsAllow;
    private string _landingSurfaceId = string.Empty;
    private float _wrapMargin;
    private bool _inputEnabled;
    private bool _verticalWrapped;

    internal InteractivePetActionRuntime(InteractivePetHost host, long runId)
    {
        _host = host;
        _runId = runId;
    }

    public long GetRunId() => _runId;

    public bool IsActive() => TryGetHost(out _);

    public Rect2 GetViewportRect() =>
        TryGetHost(out InteractivePetHost host) ? host.GetActionViewportRect() : default;

    public Vector2 GetAnchor() =>
        TryGetHost(out InteractivePetHost host) ? host.GetActionAnchor() : Vector2.Zero;

    public Dictionary GetHighestSupport()
    {
        var result = NewResult(accepted: false);
        if (!TryGetHost(out InteractivePetHost host)
            || !host.TryGetHighestActionSupport(out InteractivePetSurface surface, out Vector2 anchor))
        {
            return result;
        }

        result["accepted"] = true;
        result["valid"] = true;
        result["surface_id"] = surface.Id;
        result["anchor"] = anchor;
        return result;
    }

    public bool SetMotionPolicy(Dictionary policy)
    {
        if (!TryGetHost(out InteractivePetHost host))
        {
            return false;
        }

        string navigation = ReadString(policy, "navigation", _navigation);
        string supportContacts = ReadString(policy, "support_contacts", _supportContacts);
        string screenBounds = ReadString(policy, "screen_bounds", _screenBounds);
        if (navigation is not (NavigationSuspended or NavigationThemeDriven)
            || supportContacts is not (SupportSolid or SupportIgnore or SupportAfterVerticalWrap)
            || screenBounds is not (BoundsClamp or BoundsAllow or BoundsWrapVerticalOnce))
        {
            return false;
        }

        bool enteringVerticalWrap = screenBounds == BoundsWrapVerticalOnce
            && _screenBounds != BoundsWrapVerticalOnce;
        _navigation = navigation;
        _supportContacts = supportContacts;
        _screenBounds = screenBounds;
        _landingSurfaceId = ReadString(policy, "landing_surface_id", _landingSurfaceId);
        _wrapMargin = Mathf.Max(0f, ReadFloat(policy, "wrap_margin", _wrapMargin));
        _inputEnabled = ReadBool(policy, "input_enabled", _inputEnabled);
        if (enteringVerticalWrap)
        {
            _verticalWrapped = false;
        }

        host.SetThemeActionInputEnabled(this, _inputEnabled);
        return true;
    }

    public Dictionary SetAnchor(Vector2 anchor)
    {
        if (!TryGetHost(out InteractivePetHost host))
        {
            return NewResult(accepted: false);
        }

        return ApplyAnchor(host, anchor);
    }

    public Dictionary MoveAnchor(Vector2 offset)
    {
        if (!TryGetHost(out InteractivePetHost host)
            || _navigation != NavigationThemeDriven
            || !IsFinite(offset))
        {
            return NewResult(accepted: false);
        }

        return ApplyAnchor(host, host.GetActionAnchor() + offset);
    }

    public Dictionary LandOnSupport(string surfaceId)
    {
        if (!TryGetHost(out InteractivePetHost host)
            || !host.TryGetActionSurface(surfaceId, out InteractivePetSurface surface))
        {
            return NewResult(accepted: false);
        }

        Vector2 anchor = InteractivePetNavigation.LandingAnchor(surface, host.GetActionAnchor().X);
        host.ApplyThemeActionAnchor(this, anchor);
        host.CommitThemeActionSurface(this, surface.Id);
        Dictionary result = NewResult(accepted: true, landed: true);
        result["anchor"] = anchor;
        result["surface_id"] = surface.Id;
        return result;
    }

    internal bool InputEnabled => _inputEnabled;

    internal void Invalidate()
    {
        _host = null;
    }

    private Dictionary ApplyAnchor(InteractivePetHost host, Vector2 requestedAnchor)
    {
        if (!IsFinite(requestedAnchor))
        {
            return NewResult(accepted: false);
        }

        Vector2 previousAnchor = host.GetActionAnchor();
        Vector2 anchor = requestedAnchor;
        bool wrapped = false;
        Rect2 viewport = host.GetActionViewportRect();
        if (_screenBounds == BoundsClamp)
        {
            anchor = InteractivePetNavigation.ClampAnchor(anchor, viewport);
        }
        else if (_screenBounds == BoundsWrapVerticalOnce
            && !_verticalWrapped
            && anchor.Y > viewport.End.Y + _wrapMargin)
        {
            float overshoot = anchor.Y - (viewport.End.Y + _wrapMargin);
            anchor.Y = viewport.Position.Y - _wrapMargin + overshoot;
            previousAnchor = anchor;
            _verticalWrapped = true;
            wrapped = true;
        }

        bool supportsEnabled = _supportContacts == SupportSolid
            || (_supportContacts == SupportAfterVerticalWrap && _verticalWrapped);
        InteractivePetSurface? landedSurface = supportsEnabled
            ? FindCrossedSurface(host, previousAnchor, anchor)
            : null;
        if (landedSurface is not null)
        {
            anchor = InteractivePetNavigation.LandingAnchor(landedSurface, anchor.X);
            host.CommitThemeActionSurface(this, landedSurface.Id);
        }

        host.ApplyThemeActionAnchor(this, anchor);
        Dictionary result = NewResult(accepted: true, wrapped: wrapped, landed: landedSurface is not null);
        result["anchor"] = anchor;
        result["surface_id"] = landedSurface?.Id ?? string.Empty;
        result["vertical_wrap_count"] = _verticalWrapped ? 1 : 0;
        return result;
    }

    private InteractivePetSurface? FindCrossedSurface(
        InteractivePetHost host,
        Vector2 previousAnchor,
        Vector2 nextAnchor)
    {
        if (nextAnchor.Y < previousAnchor.Y)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_landingSurfaceId))
        {
            return host.TryGetActionSurface(_landingSurfaceId, out InteractivePetSurface target)
                && nextAnchor.X >= target.FromX
                && nextAnchor.X <= target.ToX
                && previousAnchor.Y <= target.Y
                && nextAnchor.Y >= target.Y
                    ? target
                    : null;
        }

        InteractivePetSurface? closest = null;
        foreach (InteractivePetSurface surface in host.GetActionSurfaces())
        {
            if (nextAnchor.X < surface.FromX
                || nextAnchor.X > surface.ToX
                || previousAnchor.Y > surface.Y
                || nextAnchor.Y < surface.Y
                || (closest is not null && surface.Y >= closest.Y))
            {
                continue;
            }

            closest = surface;
        }

        return closest;
    }

    private bool TryGetHost(out InteractivePetHost host)
    {
        InteractivePetHost? candidate = _host;
        if (candidate is not null
            && GodotObject.IsInstanceValid(candidate)
            && candidate.IsThemeActionRuntimeActive(this, _runId))
        {
            host = candidate;
            return true;
        }

        host = null!;
        return false;
    }

    private static Dictionary NewResult(bool accepted, bool wrapped = false, bool landed = false) => new()
    {
        ["accepted"] = accepted,
        ["valid"] = false,
        ["wrapped"] = wrapped,
        ["landed"] = landed,
        ["anchor"] = Vector2.Zero,
        ["surface_id"] = string.Empty,
        ["vertical_wrap_count"] = 0,
    };

    private static string ReadString(Dictionary values, string key, string fallback)
    {
        if (!values.TryGetValue(key, out Variant value))
        {
            return fallback;
        }

        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    private static float ReadFloat(Dictionary values, string key, float fallback)
    {
        if (!values.TryGetValue(key, out Variant value))
        {
            return fallback;
        }

        double number = value.VariantType switch
        {
            Variant.Type.Float => value.AsDouble(),
            Variant.Type.Int => value.AsInt64(),
            _ => double.NaN,
        };
        return double.IsFinite(number) ? (float)number : fallback;
    }

    private static bool ReadBool(Dictionary values, string key, bool fallback) =>
        values.TryGetValue(key, out Variant value) && value.VariantType == Variant.Type.Bool
            ? value.AsBool()
            : fallback;

    private static bool IsFinite(Vector2 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);
}

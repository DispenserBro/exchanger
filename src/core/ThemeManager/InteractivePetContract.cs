using Godot;

namespace Exchanger.Core.Theming;

public static class InteractivePetContract
{
    public const int NavigationVersion = 1;

    public static readonly StringName SurfaceMetadata = "exchanger_pet_surface";
    public static readonly StringName NavigationRootMetadata = "exchanger_pet_navigation_root";
    public static readonly StringName NavigationVersionMetadata = "exchanger_pet_navigation_version";
    public static readonly StringName DefaultSurfaceMetadata = "exchanger_pet_default_surface_id";
    public static readonly StringName RoamingBoundsMetadata = "exchanger_pet_roaming_bounds";
    public static readonly StringName SurfaceIdMetadata = "exchanger_pet_surface_id";
    public static readonly StringName SurfaceFromXMetadata = "exchanger_pet_surface_from_x";
    public static readonly StringName SurfaceToXMetadata = "exchanger_pet_surface_to_x";
    public static readonly StringName SurfaceYMetadata = "exchanger_pet_surface_y";
    public static readonly StringName SurfaceSitPointsMetadata = "exchanger_pet_surface_sit_points";
    public static readonly StringName SurfaceVisibilityBindingMetadata =
        "exchanger_pet_surface_visibility_binding_id";
    public static readonly StringName ClimbEdgeMetadata = "exchanger_pet_climb_edge";
    public static readonly StringName ClimbEdgeIdMetadata = "exchanger_pet_climb_edge_id";
    public static readonly StringName ClimbFromSurfaceMetadata = "exchanger_pet_climb_from_surface_id";
    public static readonly StringName ClimbToSurfaceMetadata = "exchanger_pet_climb_to_surface_id";
    public static readonly StringName ClimbXMetadata = "exchanger_pet_climb_x";
    public static readonly StringName ClimbFromYMetadata = "exchanger_pet_climb_from_y";
    public static readonly StringName ClimbToYMetadata = "exchanger_pet_climb_to_y";
    public static readonly StringName ClimbSideMetadata = "exchanger_pet_climb_side";
    public static readonly StringName AnimationStartedSignal = "pet_animation_started";
    public static readonly StringName AnimationCompletedSignal = "pet_animation_completed";
    public static readonly StringName GetActionAnimationMethod = "get_pet_action_animation";
    public static readonly StringName GetFramesPerSecondMethod = "get_pet_animation_frames_per_second";
    public static readonly StringName GetPreviewSizeMethod = "get_pet_preview_size";
    public static readonly StringName GetRuntimeScaleMethod = "get_pet_runtime_scale";
    public static readonly StringName GetWalkSpeedMethod = "get_pet_walk_speed";
    public static readonly StringName GetClimbSpeedMethod = "get_pet_climb_speed";
    public static readonly StringName GetHitSizeMethod = "get_pet_hit_size";
    public static readonly StringName GetGroundOffsetMethod = "get_pet_ground_offset";
    public static readonly StringName HasCompositeActionMethod = "has_pet_composite_action";
    public static readonly StringName StartCompositeActionMethod = "start_pet_composite_action";
    public static readonly StringName CancelCompositeActionMethod = "cancel_pet_composite_action";
    public static readonly StringName CompositeActionStartedSignal = "pet_composite_action_started";
    public static readonly StringName CompositeActionFinishedSignal = "pet_composite_action_finished";

    public static readonly StringName[] RequiredMethods =
    {
        "get_pet_animation_names",
        "get_pet_animation_frame_count",
        "has_pet_animation",
        "play_pet_animation",
        "pause_pet_animation",
        "stop_pet_animation",
        "get_current_pet_animation",
        "get_current_pet_frame",
    };

    public static readonly StringName[] CompositeActionMethods =
    {
        HasCompositeActionMethod,
        StartCompositeActionMethod,
        CancelCompositeActionMethod,
    };

    public static bool IsCompatible(PackedScene scene)
    {
        Node instance = scene.Instantiate();
        try
        {
            if (instance is not Node2D pet)
            {
                return false;
            }

            foreach (StringName method in RequiredMethods)
            {
                if (!pet.HasMethod(method))
                {
                    return false;
                }
            }

            return pet.HasSignal(AnimationStartedSignal)
                && pet.HasSignal(AnimationCompletedSignal);
        }
        finally
        {
            instance.Free();
        }
    }

    public static bool HasCompositeActionContract(Node pet)
    {
        foreach (StringName method in CompositeActionMethods)
        {
            if (!pet.HasMethod(method))
            {
                return false;
            }
        }

        return pet.HasSignal(CompositeActionStartedSignal)
            && pet.HasSignal(CompositeActionFinishedSignal);
    }
}

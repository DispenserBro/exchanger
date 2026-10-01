using Exchanger.Core.Logging;
using Exchanger.Core.Navigation;
using Exchanger.Core.Theming;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Exchanger.UI.InteractivePet;

/// <summary>
/// App-owned movement and input host for a theme-owned interactive pet scene.
/// The theme declares neutral navigation geometry; the host never changes its visuals.
/// </summary>
public partial class InteractivePetHost : CanvasLayer
{
    internal const float IdleAfterWalkProbability = 0.35f;
    internal const float MinClimbActionProgress = 0.2f;
    internal const float MaxClimbActionProgress = 0.8f;
    private const float FallbackLandingSeconds = 0.5f;
    private const float LandingCompletionGraceSeconds = 0.25f;
    private const float FallbackClimbActionSeconds = 0.5f;
    private const float ClimbActionCompletionGraceSeconds = 0.25f;

    [Signal]
    public delegate void PetInteractedEventHandler();

    public event Action? PetDragStarted;

    public event Action? PetDragReleased;

    private enum PetMotionState
    {
        Sitting,
        Walking,
        ClimbStarting,
        Climbing,
        ClimbActionStarting,
        ClimbActionPlaying,
        ClimbActionStopping,
        Dragging,
        Dropping,
        Landing,
        Interacting,
        Celebrating,
        ThemeAction,
    }

    private readonly RandomNumberGenerator _random = new();
    private readonly Dictionary<ScreenId, InteractivePetTopology> _screenTopologies = new();
    private Node2D _petStage = null!;
    private Node2D? _pet;
    private ScreenManager? _screenManager;
    private ThemeManager? _themeManager;
    private InteractivePetTopology? _topology;
    private IReadOnlyList<InteractivePetClimbTraversal> _route
        = Array.Empty<InteractivePetClimbTraversal>();
    private PetMotionState _state = PetMotionState.Sitting;
    private string _currentSurfaceId = string.Empty;
    private string _destinationSurfaceId = string.Empty;
    private int _routeIndex;
    private Vector2 _anchor;
    private Vector2 _motionTarget;
    private Vector2 _finalDestination;
    private Vector2 _pointerOffset;
    private Vector2 _pressPosition;
    private Vector2 _hitSize = new(128f, 128f);
    private Vector2 _groundOffset = new(64f, 120f);
    private float _runtimeScale = 1f;
    private float _walkSpeed = 90f;
    private float _climbSpeed = 75f;
    private float _idleSeconds;
    private float _interactionSeconds;
    private float _landingSeconds;
    private bool _waitingForLandingCompletion;
    private StringName _landingAnimation = new();
    private float _celebrationCycleSeconds;
    private float _celebrationSeconds;
    private float _climbTransitionSeconds;
    private float _climbActionSeconds;
    private bool _waitingForClimbActionCompletion;
    private bool _climbActionScheduled;
    private Vector2 _climbActionAnchor;
    private StringName _activeClimbAnimation = new();
    private StringName _climbActionStartAnimation = new();
    private StringName _climbActionAnimation = new();
    private StringName _climbActionStopAnimation = new();
    private StringName _celebrationAnimation = new();
    private int _celebrationCyclesRemaining;
    private int _pendingCelebrationRepetitions;
    private InteractivePetActionRuntime? _themeActionRuntime;
    private long _themeActionRunId;
    private long _nextThemeActionRunId;
    private string _currentRestAction = "idle";
    private bool _themeActionInputEnabled;
    private bool _reducedEffects;
    private bool _pointerMoved;
    private bool _mouseDrag;
    private int _touchIndex = -1;
    private bool _enabled = true;

    public bool HasPet => _pet is not null;

    public bool HasActivePet() => HasPet && _topology is not null;

    public Vector2 PetAnchor => _anchor;

    public string CurrentSurfaceId => _currentSurfaceId;

    public string MotionState => _state.ToString();

    public bool HasActiveCompositeAction => _themeActionRuntime is not null;

    public override void _Ready()
    {
        _petStage = GetNode<Node2D>("PetStage");
        _random.Randomize();
        Visible = false;
        SetProcess(true);
        SetProcessInput(true);
    }

    public override void _Process(double delta)
    {
        if (!Visible || _pet is null || _topology is null || _state == PetMotionState.Dragging)
        {
            return;
        }

        float elapsed = (float)delta;
        switch (_state)
        {
            case PetMotionState.Sitting:
                if (_reducedEffects)
                {
                    return;
                }

                _idleSeconds -= elapsed;
                if (_idleSeconds <= 0f)
                {
                    BeginAutonomousMove();
                }

                break;
            case PetMotionState.Walking:
                MoveTowardsTarget(_walkSpeed, elapsed, OnWalkTargetReached);
                break;
            case PetMotionState.Climbing:
                AdvanceClimb(elapsed);
                break;
            case PetMotionState.ClimbActionStarting:
                AdvanceClimbActionTransition(elapsed, BeginClimbActionLoop);
                break;
            case PetMotionState.ClimbActionPlaying:
                _climbActionSeconds -= elapsed;
                if (_climbActionSeconds <= 0f)
                {
                    BeginClimbActionStop();
                }

                break;
            case PetMotionState.ClimbActionStopping:
                AdvanceClimbActionTransition(elapsed, ResumeClimbAfterAction);
                break;
            case PetMotionState.ClimbStarting:
                _climbTransitionSeconds -= elapsed;
                if (_climbTransitionSeconds <= 0f)
                {
                    BeginClimb();
                }

                break;
            case PetMotionState.Dropping:
                MoveTowardsTarget(_climbSpeed * 1.8f, elapsed, FinishDrop);
                break;
            case PetMotionState.Landing:
                _landingSeconds -= elapsed;
                if (!_waitingForLandingCompletion || _landingSeconds <= 0f)
                {
                    FinishLanding();
                }

                break;
            case PetMotionState.Interacting:
                _interactionSeconds -= elapsed;
                if (_interactionSeconds <= 0f)
                {
                    EnterSitting();
                }

                break;
            case PetMotionState.Celebrating:
                AdvanceCelebration(elapsed);
                break;
        }
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!Visible || _pet is null || _topology is null)
        {
            return;
        }

        if (_state == PetMotionState.ThemeAction && !_themeActionInputEnabled)
        {
            return;
        }

        switch (inputEvent)
        {
            case InputEventScreenTouch touch
                when touch.Pressed && touch.Index == 0 && _touchIndex < 0 && !_mouseDrag:
                if (TryBeginDrag(touch.Position))
                {
                    _touchIndex = touch.Index;
                    GetViewport().SetInputAsHandled();
                }

                break;
            case InputEventScreenDrag drag when drag.Index == _touchIndex:
                ContinueDrag(drag.Position);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventScreenTouch touch when !touch.Pressed && touch.Index == _touchIndex:
                EndDrag(touch.Position);
                _touchIndex = -1;
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton mouse
                when mouse.ButtonIndex == MouseButton.Left
                    && mouse.Pressed
                    && !_mouseDrag
                    && _touchIndex < 0:
                if (TryBeginDrag(mouse.Position))
                {
                    _mouseDrag = true;
                    GetViewport().SetInputAsHandled();
                }

                break;
            case InputEventMouseMotion motion when _mouseDrag:
                ContinueDrag(motion.Position);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton mouse
                when mouse.ButtonIndex == MouseButton.Left && !mouse.Pressed && _mouseDrag:
                EndDrag(mouse.Position);
                _mouseDrag = false;
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    public void Configure(ThemeManager themeManager, ScreenManager screenManager)
    {
        ArgumentNullException.ThrowIfNull(themeManager);
        ArgumentNullException.ThrowIfNull(screenManager);
        ClearPet();
        _themeManager = themeManager;
        _screenManager = screenManager;
        _screenManager.ScreenChanged += OnScreenChanged;
        _themeManager.ThemeChanged += OnThemeChanged;
        _reducedEffects = themeManager.ReducedEffects;

        LoadPet(themeManager.CurrentTheme, screenManager);
    }

    private void LoadPet(VendingThemeDefinition? theme, ScreenManager screenManager)
    {
        if (theme?.InteractivePetScene is null
            || !InteractivePetContract.IsCompatible(theme.InteractivePetScene))
        {
            Visible = false;
            return;
        }

        _screenTopologies.Clear();
        foreach (ScreenId screenId in Enum.GetValues<ScreenId>())
        {
            if (screenId == ScreenId.Settings)
            {
                continue;
            }

            Control screen = screenManager.GetScreen<Control>(screenId);
            if (TryReadTopology(screen, out InteractivePetTopology? topology))
            {
                _screenTopologies[screenId] = topology!;
            }
        }

        if (_screenTopologies.Count == 0)
        {
            Visible = false;
            return;
        }

        _pet = theme.InteractivePetScene.Instantiate<Node2D>();
        _petStage.AddChild(_pet);
        _pet.Connect(
            InteractivePetContract.AnimationCompletedSignal,
            Callable.From<StringName>(OnPetAnimationCompleted));
        if (InteractivePetContract.HasCompositeActionContract(_pet))
        {
            _pet.Connect(
                InteractivePetContract.CompositeActionFinishedSignal,
                Callable.From<StringName, long, bool>(OnPetCompositeActionFinished));
        }
        _runtimeScale = ReadPositiveFloat(InteractivePetContract.GetRuntimeScaleMethod, 1f);
        _walkSpeed = ReadPositiveFloat(InteractivePetContract.GetWalkSpeedMethod, 90f);
        _climbSpeed = ReadPositiveFloat(InteractivePetContract.GetClimbSpeedMethod, 75f);
        _hitSize = ReadPositiveVector(InteractivePetContract.GetHitSizeMethod, new Vector2(128f, 128f));
        _groundOffset = ReadPositiveVector(
            InteractivePetContract.GetGroundOffsetMethod,
            _hitSize * new Vector2(0.5f, 0.94f));
        _pet.Scale = Vector2.One * _runtimeScale;

        _topology = _screenTopologies.TryGetValue(ScreenId.Home, out InteractivePetTopology? homeTopology)
            ? homeTopology
            : _screenTopologies.Values.First();
        _currentSurfaceId = _topology.DefaultSurfaceId;
        InteractivePetSurface startSurface = _topology.Surfaces[_currentSurfaceId];
        float startX = startSurface.SitPoints.Count > 0
            ? startSurface.SitPoints[_random.RandiRange(0, startSurface.SitPoints.Count - 1)]
            : (startSurface.FromX + startSurface.ToX) * 0.5f;
        SetAnchor(InteractivePetNavigation.LandingAnchor(startSurface, startX));
        EnterSitting();
        OnScreenChanged(_screenManager?.CurrentScreenId?.ToString() ?? string.Empty);
        AppLogger.Info("InteractivePet", $"Питомец темы {theme.ThemeId} активирован.");
    }

    public void SetReducedEffects(bool reducedEffects)
    {
        _reducedEffects = reducedEffects;
        if (_pet is null || _state is PetMotionState.Dragging or PetMotionState.Dropping)
        {
            return;
        }

        if (_state == PetMotionState.Sitting)
        {
            PlayRestAction();
        }
    }

    /// <summary>
    /// Временно включает или скрывает theme-owned питомца, не меняя сцену темы.
    /// При повторном включении он заново входит на текущем экране с пола.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled)
        {
            return;
        }

        _enabled = enabled;
        _mouseDrag = false;
        _touchIndex = -1;
        if (!_enabled)
        {
            CancelThemeAction("pet_disabled", playIdle: false);
            Visible = false;
            return;
        }

        OnScreenChanged(_screenManager?.CurrentScreenId?.ToString() ?? string.Empty);
    }

    public override void _ExitTree()
    {
        ClearPet();
    }

    private bool TryBeginDrag(Vector2 pointerPosition)
    {
        if (!GetPetHitRect().HasPoint(pointerPosition))
        {
            return false;
        }

        CancelThemeAction("drag_started", playIdle: false);
        ResetClimbActionSequence();

        _route = Array.Empty<InteractivePetClimbTraversal>();
        _pendingCelebrationRepetitions = 0;
        _state = PetMotionState.Dragging;
        _pressPosition = pointerPosition;
        _pointerOffset = _anchor - pointerPosition;
        _pointerMoved = false;
        PlayAction("drag");
        PetDragStarted?.Invoke();
        EmitSignal(SignalName.PetInteracted);
        return true;
    }

    private void ContinueDrag(Vector2 pointerPosition)
    {
        _pointerMoved |= pointerPosition.DistanceTo(_pressPosition) > 10f;
        SetAnchor(InteractivePetNavigation.ClampAnchor(
            pointerPosition + _pointerOffset,
            _topology!.RoamingBounds));
    }

    private void EndDrag(Vector2 pointerPosition)
    {
        ContinueDrag(pointerPosition);
        PetDragReleased?.Invoke();
        if (!_pointerMoved)
        {
            BeginInteraction();
            return;
        }

        InteractivePetSurface? landing = FindLandingSurface();
        if (landing is null)
        {
            EnterSitting();
            return;
        }

        _destinationSurfaceId = landing.Id;
        _motionTarget = InteractivePetNavigation.LandingAnchor(landing, _anchor.X);
        _state = PetMotionState.Dropping;
        PlayAction("drop");
    }

    public void RequestInteraction(int actionRepetitions = 3)
    {
        if (!Visible || _pet is null || _state == PetMotionState.Dragging)
        {
            return;
        }

        BeginInteraction(actionRepetitions);
    }

    public bool RequestDispenseCelebration(int repetitions = 4)
    {
        if (!Visible || _pet is null || _topology is null || repetitions <= 0)
        {
            return false;
        }

        int safeRepetitions = Math.Clamp(repetitions, 1, 16);
        if (_state == PetMotionState.ThemeAction)
        {
            CancelThemeAction("celebration_requested", playIdle: false);
        }
        if (_state is PetMotionState.Dropping or PetMotionState.Landing)
        {
            _pendingCelebrationRepetitions = safeRepetitions;
            return true;
        }

        return BeginCelebration(safeRepetitions);
    }

    public bool RequestMoveToSurface(string surfaceId)
    {
        if (!Visible
            || _pet is null
            || _topology is null
            || _state == PetMotionState.Dragging
            || !_topology.Surfaces.TryGetValue(surfaceId, out InteractivePetSurface? destination))
        {
            return false;
        }


        CancelThemeAction("move_requested", playIdle: false);
        ResetClimbActionSequence();

        _pendingCelebrationRepetitions = 0;

        IReadOnlyList<InteractivePetClimbTraversal> route =
            InteractivePetNavigation.FindClimbPath(_topology, _currentSurfaceId, surfaceId);
        if (!_currentSurfaceId.Equals(surfaceId, StringComparison.Ordinal) && route.Count == 0)
        {
            return false;
        }

        float destinationX = destination.SitPoints.Count > 0
            ? destination.SitPoints[0]
            : (destination.FromX + destination.ToX) * 0.5f;
        _destinationSurfaceId = destination.Id;
        _finalDestination = InteractivePetNavigation.LandingAnchor(destination, destinationX);
        _route = route;
        _routeIndex = 0;
        BeginWalkTo(_route.Count > 0 ? _route[0].FromAnchor : _finalDestination);
        return true;
    }

    private void BeginAutonomousMove()
    {
        if (_topology is null || !_topology.Surfaces.TryGetValue(
                _currentSurfaceId,
                out InteractivePetSurface? currentSurface))
        {
            return;
        }

        InteractivePetSurface[] reachable = _topology.Surfaces.Values
            .Where(surface => surface.Id == currentSurface.Id
                || InteractivePetNavigation.FindClimbPath(
                    _topology,
                    currentSurface.Id,
                    surface.Id).Count > 0)
            .ToArray();
        if (reachable.Length == 0)
        {
            EnterSitting();
            return;
        }

        InteractivePetSurface destination = reachable[_random.RandiRange(0, reachable.Length - 1)];
        float destinationX = destination.SitPoints.Count > 0
            ? destination.SitPoints[_random.RandiRange(0, destination.SitPoints.Count - 1)]
            : _random.RandfRange(destination.FromX, destination.ToX);
        _destinationSurfaceId = destination.Id;
        _finalDestination = InteractivePetNavigation.LandingAnchor(destination, destinationX);
        _route = InteractivePetNavigation.FindClimbPath(
            _topology,
            _currentSurfaceId,
            _destinationSurfaceId);
        _routeIndex = 0;

        if (_currentSurfaceId != _destinationSurfaceId && _route.Count == 0)
        {
            EnterSitting();
            return;
        }

        BeginWalkTo(_route.Count > 0 ? _route[0].FromAnchor : _finalDestination);
    }

    private bool TryBeginPostLandingClimb()
    {
        if (_topology is null || !_topology.Surfaces.TryGetValue(
                _currentSurfaceId,
                out InteractivePetSurface? currentSurface))
        {
            return false;
        }

        InteractivePetSurface[] elevated = _topology.Surfaces.Values
            .Where(surface => surface.Y < currentSurface.Y
                && InteractivePetNavigation.FindClimbPath(
                    _topology,
                    currentSurface.Id,
                    surface.Id).Count > 0)
            .ToArray();
        if (elevated.Length == 0)
        {
            return false;
        }

        InteractivePetSurface destination = elevated[_random.RandiRange(0, elevated.Length - 1)];
        _destinationSurfaceId = destination.Id;
        _route = InteractivePetNavigation.FindClimbPath(
            _topology,
            currentSurface.Id,
            destination.Id);
        _routeIndex = 0;
        _finalDestination = InteractivePetNavigation.LandingAnchor(
            destination,
            destination.SitPoints.Count > 0
                ? destination.SitPoints[_random.RandiRange(0, destination.SitPoints.Count - 1)]
                : (destination.FromX + destination.ToX) * 0.5f);
        BeginWalkTo(_route[0].FromAnchor);
        return true;
    }

    private void BeginWalkTo(Vector2 target)
    {
        _motionTarget = target;
        if (_anchor.DistanceSquaredTo(target) <= 0.25f)
        {
            SetAnchor(target);
            OnWalkTargetReached();
            return;
        }

        _state = PetMotionState.Walking;
        PlayAction(target.X < _anchor.X ? "walk_left" : "walk_right");
    }

    private void OnWalkTargetReached()
    {
        if (_routeIndex < _route.Count)
        {
            _state = PetMotionState.ClimbStarting;
            _climbTransitionSeconds = 0.32f;
            PlayAction("climb_start");
            return;
        }

        _currentSurfaceId = _destinationSurfaceId;
        EnterSittingAfterWalk();
    }

    private void BeginClimb()
    {
        InteractivePetClimbTraversal traversal = _route[_routeIndex];
        _motionTarget = traversal.ToAnchor;
        _state = PetMotionState.Climbing;
        ResetClimbActionSequence();
        string climbAction = traversal.ToAnchor.Y < traversal.FromAnchor.Y
            ? "climb_up"
            : "climb_down";
        TryResolveActionAnimation(climbAction, out _activeClimbAnimation);
        PrepareClimbActionSequence(traversal);
        PlayAnimation(_activeClimbAnimation);
    }

    private void OnClimbTargetReached()
    {
        ResetClimbActionSequence();
        InteractivePetClimbTraversal traversal = _route[_routeIndex];
        _currentSurfaceId = traversal.ToSurfaceId;
        _routeIndex++;
        ContinueRouteAfterClimb();
    }

    private void ContinueRouteAfterClimb()
    {
        if (_routeIndex < _route.Count)
        {
            BeginWalkTo(_route[_routeIndex].FromAnchor);
            return;
        }

        BeginWalkTo(_finalDestination);
    }

    private void AdvanceClimb(float elapsed)
    {
        if (_climbActionScheduled)
        {
            MoveTowardsPoint(_climbActionAnchor, _climbSpeed, elapsed);
            if (_anchor.DistanceSquaredTo(_climbActionAnchor) <= 0.25f)
            {
                SetAnchor(_climbActionAnchor);
                BeginClimbActionStart();
            }

            return;
        }

        MoveTowardsTarget(_climbSpeed, elapsed, OnClimbTargetReached);
    }

    private void PrepareClimbActionSequence(InteractivePetClimbTraversal traversal)
    {
        if (traversal.FromAnchor.DistanceSquaredTo(traversal.ToAnchor) <= 0.25f
            || !TryResolveActionAnimation(
                "climbing_action_start",
                out _climbActionStartAnimation)
            || !TryResolveActionAnimation("climbing_action", out _climbActionAnimation)
            || !TryResolveActionAnimation(
                "climbing_action_stop",
                out _climbActionStopAnimation))
        {
            return;
        }

        float progress = SelectClimbActionProgress(_random.Randf());
        _climbActionAnchor = traversal.FromAnchor.Lerp(traversal.ToAnchor, progress);
        _climbActionScheduled = true;
    }

    private void BeginClimbActionStart()
    {
        _climbActionScheduled = false;
        _state = PetMotionState.ClimbActionStarting;
        _waitingForClimbActionCompletion = true;
        _climbActionSeconds = Math.Max(
            ReadAnimationCycleSeconds(
                _climbActionStartAnimation,
                FallbackClimbActionSeconds) + ClimbActionCompletionGraceSeconds,
            ClimbActionCompletionGraceSeconds);
        PlayAnimation(_climbActionStartAnimation);
    }

    private void BeginClimbActionLoop()
    {
        _state = PetMotionState.ClimbActionPlaying;
        _waitingForClimbActionCompletion = false;
        _climbActionSeconds = ReadAnimationCycleSeconds(
            _climbActionAnimation,
            FallbackClimbActionSeconds);
        PlayAnimation(_climbActionAnimation);
    }

    private void BeginClimbActionStop()
    {
        _state = PetMotionState.ClimbActionStopping;
        _waitingForClimbActionCompletion = true;
        _climbActionSeconds = Math.Max(
            ReadAnimationCycleSeconds(
                _climbActionStopAnimation,
                FallbackClimbActionSeconds) + ClimbActionCompletionGraceSeconds,
            ClimbActionCompletionGraceSeconds);
        PlayAnimation(_climbActionStopAnimation);
    }

    private void AdvanceClimbActionTransition(float elapsed, Action completed)
    {
        _climbActionSeconds -= elapsed;
        if (!_waitingForClimbActionCompletion || _climbActionSeconds <= 0f)
        {
            completed();
        }
    }

    private void ResumeClimbAfterAction()
    {
        StringName climbAnimation = _activeClimbAnimation;
        ResetClimbActionSequence();
        _state = PetMotionState.Climbing;
        PlayAnimation(climbAnimation);
    }

    private void ResetClimbActionSequence()
    {
        _climbActionSeconds = 0f;
        _waitingForClimbActionCompletion = false;
        _climbActionScheduled = false;
        _climbActionAnchor = Vector2.Zero;
        _activeClimbAnimation = new StringName();
        _climbActionStartAnimation = new StringName();
        _climbActionAnimation = new StringName();
        _climbActionStopAnimation = new StringName();
    }

    private void MoveTowardsPoint(Vector2 target, float speed, float delta)
    {
        SetAnchor(_anchor.MoveToward(target, speed * delta));
    }

    private void MoveTowardsTarget(float speed, float delta, Action reached)
    {
        SetAnchor(_anchor.MoveToward(_motionTarget, speed * delta));
        if (_anchor.DistanceSquaredTo(_motionTarget) <= 0.25f)
        {
            SetAnchor(_motionTarget);
            reached();
        }
    }

    private void FinishDrop()
    {
        _currentSurfaceId = _destinationSurfaceId;
        BeginLanding();
    }

    private void BeginLanding()
    {
        _state = PetMotionState.Landing;
        _waitingForLandingCompletion = false;
        _landingAnimation = new StringName();
        if (TryResolveActionAnimation("landing", out StringName animation))
        {
            _landingAnimation = animation;
            _waitingForLandingCompletion = true;
            _landingSeconds = Math.Max(
                ReadAnimationCycleSeconds(animation, FallbackLandingSeconds)
                    + LandingCompletionGraceSeconds,
                LandingCompletionGraceSeconds);
            PlayAnimation(animation);
            return;
        }

        _landingSeconds = FallbackLandingSeconds;
        AppLogger.Warning(
            "InteractivePet",
            "Питомец темы не содержит анимацию landing; выдерживается безопасная фаза приземления.");
    }

    private void FinishLanding()
    {
        _landingSeconds = 0f;
        _waitingForLandingCompletion = false;
        _landingAnimation = new StringName();
        if (_pendingCelebrationRepetitions > 0)
        {
            int repetitions = _pendingCelebrationRepetitions;
            _pendingCelebrationRepetitions = 0;
            if (BeginCelebration(repetitions))
            {
                return;
            }
        }

        if (_reducedEffects)
        {
            EnterSitting();
            return;
        }

        switch (SelectPostLandingBehavior(_random.Randf()))
        {
            case 0:
                EnterSitting(playIdle: false);
                _currentRestAction = "sit";
                _idleSeconds = _random.RandfRange(1.5f, 3.5f);
                PlayRestAction();
                return;
            case 1:
                if (TryBeginPostLandingClimb())
                {
                    return;
                }

                break;
        }

        EnterSitting();
    }

    private void OnPetAnimationCompleted(StringName animation)
    {
        if (_state == PetMotionState.Landing && animation == _landingAnimation)
        {
            // Сигнал приходит после последнего кадра. FinishLanding сработает на следующем
            // _Process, когда тема уже завершит собственный callback анимации.
            _waitingForLandingCompletion = false;
        }

        if (_state == PetMotionState.ClimbActionStarting
            && animation == _climbActionStartAnimation)
        {
            _waitingForClimbActionCompletion = false;
        }
        else if (_state == PetMotionState.ClimbActionStopping
            && animation == _climbActionStopAnimation)
        {
            _waitingForClimbActionCompletion = false;
        }
    }

    private bool BeginCelebration(int repetitions)
    {
        if (!TryResolveActionAnimation("celebrate", out StringName animation)
            && !TryResolveActionAnimation("interact", out animation))
        {
            return false;
        }

        ResetClimbActionSequence();
        _route = Array.Empty<InteractivePetClimbTraversal>();
        _routeIndex = 0;
        _celebrationAnimation = animation;
        _celebrationCyclesRemaining = repetitions;
        _celebrationCycleSeconds = ReadAnimationCycleSeconds(animation, 1.6f);
        _celebrationSeconds = _celebrationCycleSeconds;
        _state = PetMotionState.Celebrating;
        PlayAnimation(animation);
        return true;
    }

    private void AdvanceCelebration(float elapsed)
    {
        _celebrationSeconds -= elapsed;
        while (_state == PetMotionState.Celebrating && _celebrationSeconds <= 0f)
        {
            _celebrationCyclesRemaining--;
            if (_celebrationCyclesRemaining <= 0)
            {
                EnterSitting();
                return;
            }

            _celebrationSeconds += _celebrationCycleSeconds;
            PlayAnimation(_celebrationAnimation);
        }
    }

    private InteractivePetSurface? FindLandingSurface()
    {
        if (_topology is null)
        {
            return null;
        }

        return InteractivePetNavigation.FindDropSurface(_topology, _anchor);
    }

    private void EnterSitting(bool playIdle = true)
    {
        ResetClimbActionSequence();
        _state = PetMotionState.Sitting;
        _route = Array.Empty<InteractivePetClimbTraversal>();
        _routeIndex = 0;
        _celebrationCyclesRemaining = 0;
        _celebrationSeconds = 0f;
        _landingSeconds = 0f;
        _currentRestAction = "idle";
        _idleSeconds = _random.RandfRange(1.5f, 4f);
        if (playIdle)
        {
            PlayRestAction();
        }
    }

    private void EnterSittingAfterWalk()
    {
        EnterSitting(playIdle: false);
        bool useIdle = ShouldUseIdleAfterWalk(_random.Randf());
        _currentRestAction = useIdle ? "idle" : "sitting";
        _idleSeconds = useIdle
            ? _random.RandfRange(3f, 5f)
            : _random.RandfRange(1.5f, 3.5f);
        PlayRestAction();
    }

    internal static bool ShouldUseIdleAfterWalk(float roll) =>
        float.IsFinite(roll)
        && roll >= 0f
        && roll < IdleAfterWalkProbability;

    internal static float SelectClimbActionProgress(float roll)
    {
        float normalizedRoll = float.IsFinite(roll)
            ? Math.Clamp(roll, 0f, 1f)
            : 0.5f;
        return MinClimbActionProgress
            + (MaxClimbActionProgress - MinClimbActionProgress) * normalizedRoll;
    }

    internal static int SelectPostLandingBehavior(float roll)
    {
        if (!float.IsFinite(roll) || roll < 0f)
        {
            return 2;
        }

        return roll < 1f / 3f
            ? 0
            : roll < 2f / 3f
                ? 1
                : 2;
    }

    private void BeginInteraction(int actionRepetitions = 3)
    {
        ResetClimbActionSequence();
        _route = Array.Empty<InteractivePetClimbTraversal>();
        _pendingCelebrationRepetitions = 0;
        var parameters = new Godot.Collections.Dictionary
        {
            ["action_repetitions"] = Math.Clamp(actionRepetitions, 1, 16),
            ["reduced_effects"] = _reducedEffects,
        };
        if (TryBeginThemeAction("interact", parameters))
        {
            return;
        }

        _state = PetMotionState.Interacting;
        _interactionSeconds = 1.6f;
        PlayAction("interact");
    }

    private bool TryBeginThemeAction(string action, Godot.Collections.Dictionary parameters)
    {
        if (_pet is null
            || !InteractivePetContract.HasCompositeActionContract(_pet)
            || !_pet.Call(
                InteractivePetContract.HasCompositeActionMethod,
                new StringName(action)).AsBool())
        {
            return false;
        }

        CancelThemeAction("replaced", playIdle: false);
        _nextThemeActionRunId = _nextThemeActionRunId == long.MaxValue
            ? 1
            : _nextThemeActionRunId + 1;
        long runId = _nextThemeActionRunId;
        var runtime = new InteractivePetActionRuntime(this, runId);
        _themeActionRunId = runId;
        _themeActionRuntime = runtime;
        _themeActionInputEnabled = false;
        _state = PetMotionState.ThemeAction;

        Variant started = _pet.Call(
            InteractivePetContract.StartCompositeActionMethod,
            new StringName(action),
            runtime,
            parameters);
        long returnedRunId = started.VariantType == Variant.Type.Int
            ? started.AsInt64()
            : 0;
        if (returnedRunId == runId)
        {
            return true;
        }

        CancelThemeAction("start_rejected", playIdle: false);
        return false;
    }

    private void OnPetCompositeActionFinished(StringName action, long runId, bool completed)
    {
        if (_themeActionRuntime is null || runId != _themeActionRunId)
        {
            return;
        }

        _themeActionRuntime.Invalidate();
        _themeActionRuntime = null;
        _themeActionRunId = 0;
        _themeActionInputEnabled = false;
        EnterSitting(playIdle: !completed);
        AppLogger.Info(
            "InteractivePet",
            $"Составное действие питомца {action} завершено (completed={completed}).");
    }

    private void CancelThemeAction(string reason, bool playIdle)
    {
        if (_themeActionRuntime is null)
        {
            return;
        }

        long runId = _themeActionRunId;
        InteractivePetActionRuntime runtime = _themeActionRuntime;
        _themeActionRuntime = null;
        _themeActionRunId = 0;
        _themeActionInputEnabled = false;
        runtime.Invalidate();
        if (_pet is not null
            && GodotObject.IsInstanceValid(_pet)
            && _pet.HasMethod(InteractivePetContract.CancelCompositeActionMethod))
        {
            _pet.Call(
                InteractivePetContract.CancelCompositeActionMethod,
                runId,
                new StringName(reason));
        }

        if (playIdle)
        {
            EnterSitting();
        }
    }

    internal bool IsThemeActionRuntimeActive(InteractivePetActionRuntime runtime, long runId) =>
        ReferenceEquals(_themeActionRuntime, runtime)
        && _themeActionRunId == runId
        && _state == PetMotionState.ThemeAction;

    internal Rect2 GetActionViewportRect()
    {
        Rect2 viewport = GetViewport().GetVisibleRect();
        return viewport.Size.X > 0f && viewport.Size.Y > 0f
            ? viewport
            : _topology?.RoamingBounds ?? new Rect2(Vector2.Zero, new Vector2(720f, 1280f));
    }

    internal Vector2 GetActionAnchor() => _anchor;

    internal IEnumerable<InteractivePetSurface> GetActionSurfaces() =>
        _topology?.Surfaces.Values ?? Array.Empty<InteractivePetSurface>();

    internal bool TryGetActionSurface(string surfaceId, out InteractivePetSurface surface)
    {
        if (_topology is not null
            && !string.IsNullOrWhiteSpace(surfaceId)
            && _topology.Surfaces.TryGetValue(surfaceId, out InteractivePetSurface? found))
        {
            surface = found;
            return true;
        }

        surface = null!;
        return false;
    }

    internal bool TryGetHighestActionSupport(
        out InteractivePetSurface surface,
        out Vector2 anchor)
    {
        InteractivePetSurface? found = _topology is null
            ? null
            : InteractivePetNavigation.FindHighestSurface(_topology, _anchor.X);
        if (found is null)
        {
            surface = null!;
            anchor = Vector2.Zero;
            return false;
        }

        surface = found;
        anchor = InteractivePetNavigation.LandingAnchor(surface, _anchor.X);
        return true;
    }

    internal void SetThemeActionInputEnabled(
        InteractivePetActionRuntime runtime,
        bool inputEnabled)
    {
        if (ReferenceEquals(_themeActionRuntime, runtime))
        {
            _themeActionInputEnabled = inputEnabled;
        }
    }

    internal void ApplyThemeActionAnchor(InteractivePetActionRuntime runtime, Vector2 anchor)
    {
        if (ReferenceEquals(_themeActionRuntime, runtime))
        {
            SetAnchor(anchor);
        }
    }

    internal void CommitThemeActionSurface(InteractivePetActionRuntime runtime, string surfaceId)
    {
        if (ReferenceEquals(_themeActionRuntime, runtime))
        {
            _currentSurfaceId = surfaceId;
            _destinationSurfaceId = surfaceId;
        }
    }

    private void SetAnchor(Vector2 anchor)
    {
        _anchor = anchor;
        if (_pet is not null)
        {
            _pet.Position = anchor - _groundOffset * _runtimeScale;
        }
    }

    private Rect2 GetPetHitRect()
    {
        Vector2 topLeft = _anchor - _groundOffset * _runtimeScale;
        return new Rect2(topLeft, _hitSize * _runtimeScale);
    }

    private void PlayRestAction()
    {
        if (!PlayAction(_currentRestAction) && _currentRestAction != "idle")
        {
            _currentRestAction = "idle";
            PlayAction("idle");
        }
    }

    private bool PlayAction(string action)
    {
        if (!TryResolveActionAnimation(action, out StringName animation))
        {
            return false;
        }

        PlayAnimation(animation);
        if (_reducedEffects && (action == "idle" || action == "sit") && _pet is not null)
        {
            _pet.Call("pause_pet_animation");
        }

        return true;
    }

    private bool TryResolveActionAnimation(string action, out StringName animation)
    {
        animation = new StringName(action);
        if (_pet is null)
        {
            return false;
        }

        if (_pet.HasMethod(InteractivePetContract.GetActionAnimationMethod))
        {
            Variant mapped = _pet.Call(
                InteractivePetContract.GetActionAnimationMethod,
                new StringName(action));
            animation = mapped.VariantType == Variant.Type.StringName
                ? mapped.AsStringName()
                : new StringName(mapped.AsString());
        }

        return !animation.IsEmpty
            && _pet.Call("has_pet_animation", animation).AsBool();
    }

    private void PlayAnimation(StringName animation)
    {
        if (_pet is not null && _pet.Call("has_pet_animation", animation).AsBool())
        {
            _pet.Call("play_pet_animation", animation);
        }
    }

    private float ReadAnimationCycleSeconds(StringName animation, float fallback)
    {
        if (_pet is null)
        {
            return fallback;
        }

        Variant frameCountValue = _pet.Call("get_pet_animation_frame_count", animation);
        long frameCount = frameCountValue.VariantType == Variant.Type.Int
            ? frameCountValue.AsInt64()
            : 0;
        if (frameCount <= 0 || !_pet.HasMethod(InteractivePetContract.GetFramesPerSecondMethod))
        {
            return fallback;
        }

        Variant framesPerSecondValue = _pet.Call(
            InteractivePetContract.GetFramesPerSecondMethod,
            animation);
        double framesPerSecond = framesPerSecondValue.VariantType switch
        {
            Variant.Type.Float => framesPerSecondValue.AsDouble(),
            Variant.Type.Int => framesPerSecondValue.AsInt64(),
            _ => double.NaN,
        };
        double seconds = frameCount / framesPerSecond;
        return double.IsFinite(seconds) && seconds > 0d && seconds <= 60d
            ? (float)seconds
            : fallback;
    }

    private float ReadPositiveFloat(StringName method, float fallback)
    {
        if (_pet is null || !_pet.HasMethod(method))
        {
            return fallback;
        }

        Variant value = _pet.Call(method);
        double number = value.VariantType switch
        {
            Variant.Type.Float => value.AsDouble(),
            Variant.Type.Int => value.AsInt64(),
            _ => double.NaN,
        };
        return double.IsFinite(number) && number > 0d && number <= 1000d
            ? (float)number
            : fallback;
    }

    private Vector2 ReadPositiveVector(StringName method, Vector2 fallback)
    {
        if (_pet is null || !_pet.HasMethod(method))
        {
            return fallback;
        }

        Variant value = _pet.Call(method);
        if (value.VariantType != Variant.Type.Vector2)
        {
            return fallback;
        }

        Vector2 vector = value.AsVector2();
        return IsFinite(vector) && vector.X >= 0f && vector.Y >= 0f
            ? vector
            : fallback;
    }

    private void OnScreenChanged(string screenId)
    {
        if (!_enabled || _pet is null)
        {
            Visible = false;
            return;
        }


        CancelThemeAction("screen_changed", playIdle: false);
        ResetClimbActionSequence();

        bool isKnownScreen = Enum.TryParse(screenId, out ScreenId parsedScreen);
        if (!isKnownScreen
            || parsedScreen == ScreenId.Settings
            || _screenManager is null
            || !TryReadTopology(
                _screenManager.GetScreen<Control>(parsedScreen),
                out InteractivePetTopology? topology))
        {
            _route = Array.Empty<InteractivePetClimbTraversal>();
            _routeIndex = 0;
            _pendingCelebrationRepetitions = 0;
            _state = PetMotionState.Sitting;
            _mouseDrag = false;
            _touchIndex = -1;
            Visible = false;
            _pet.Call("pause_pet_animation");
            return;
        }

        _screenTopologies[parsedScreen] = topology!;
        _topology = topology;
        Visible = true;
        _route = Array.Empty<InteractivePetClimbTraversal>();
        _routeIndex = 0;
        _pendingCelebrationRepetitions = 0;
        _mouseDrag = false;
        _touchIndex = -1;

        if (_topology is null
            || !_topology.Surfaces.TryGetValue(
                _topology.DefaultSurfaceId,
                out InteractivePetSurface? floor))
        {
            EnterSitting();
            return;
        }

        _destinationSurfaceId = floor.Id;
        _motionTarget = InteractivePetNavigation.LandingAnchor(floor, _anchor.X);
        if (_anchor.DistanceSquaredTo(_motionTarget) <= 0.25f)
        {
            SetAnchor(_motionTarget);
            _currentSurfaceId = floor.Id;
            BeginLanding();
            return;
        }

        _state = PetMotionState.Dropping;
        PlayAction("drop");
    }

    private void OnThemeChanged(VendingThemeDefinition theme)
    {
        if (_themeManager is not null)
        {
            SetReducedEffects(_themeManager.ReducedEffects);
        }
    }

    private void ClearPet()
    {
        CancelThemeAction("pet_unloaded", playIdle: false);
        if (GodotObject.IsInstanceValid(_screenManager))
        {
            _screenManager!.ScreenChanged -= OnScreenChanged;
        }

        if (GodotObject.IsInstanceValid(_themeManager))
        {
            _themeManager!.ThemeChanged -= OnThemeChanged;
        }

        if (_pet is not null && GodotObject.IsInstanceValid(_pet))
        {
            _pet.QueueFree();
        }

        _pet = null;
        _topology = null;
        _screenTopologies.Clear();
        _screenManager = null;
        _themeManager = null;
        _mouseDrag = false;
        _touchIndex = -1;
        _pendingCelebrationRepetitions = 0;
        _celebrationCyclesRemaining = 0;
        _landingSeconds = 0f;
        _waitingForLandingCompletion = false;
        _landingAnimation = new StringName();
        ResetClimbActionSequence();
        _themeActionRuntime = null;
        _themeActionRunId = 0;
        _themeActionInputEnabled = false;
        Visible = false;
    }

    private static bool TryReadTopology(Node homeScreen, out InteractivePetTopology? topology)
    {
        List<Node> nodes = EnumerateTree(homeScreen).ToList();
        Node? root = nodes.FirstOrDefault(node =>
            TryReadBool(node, InteractivePetContract.NavigationRootMetadata, out bool marker)
            && marker);
        if (root is null
            || !TryReadInt(root, InteractivePetContract.NavigationVersionMetadata, out long version)
            || version != InteractivePetContract.NavigationVersion
            || !TryReadString(root, InteractivePetContract.DefaultSurfaceMetadata, out string defaultSurfaceId)
            || !TryReadRect(root, InteractivePetContract.RoamingBoundsMetadata, out Rect2 bounds)
            || !IsFinite(bounds.Position)
            || !IsFinite(bounds.Size)
            || bounds.Size.X <= 0f
            || bounds.Size.Y <= 0f)
        {
            topology = null;
            AppLogger.Warning("InteractivePet", "Тема не содержит корректный корневой marker навигации питомца.");
            return false;
        }

        var visibilityBindings = new Dictionary<string, CanvasItem>(StringComparer.Ordinal);
        foreach (Node node in nodes)
        {
            if (node is CanvasItem canvasItem
                && TryReadString(node, ThemeSceneContract.ElementBindingMetadata, out string bindingId)
                && !string.IsNullOrWhiteSpace(bindingId))
            {
                visibilityBindings.TryAdd(bindingId, canvasItem);
            }
        }

        var surfaces = new Dictionary<string, InteractivePetSurface>(StringComparer.Ordinal);
        var seenSurfaceIds = new HashSet<string>(StringComparer.Ordinal);
        var suppressedSurfaceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (Node node in nodes)
        {
            if (!TryReadBool(node, InteractivePetContract.SurfaceMetadata, out bool isSurface)
                || !isSurface)
            {
                continue;
            }

            if (!TryReadString(node, InteractivePetContract.SurfaceIdMetadata, out string id)
                || string.IsNullOrWhiteSpace(id)
                || !seenSurfaceIds.Add(id)
                || !TryReadFloat(node, InteractivePetContract.SurfaceFromXMetadata, out float fromX)
                || !TryReadFloat(node, InteractivePetContract.SurfaceToXMetadata, out float toX)
                || !TryReadFloat(node, InteractivePetContract.SurfaceYMetadata, out float y)
                || !float.IsFinite(fromX)
                || !float.IsFinite(toX)
                || !float.IsFinite(y)
                || fromX > toX
                || !TryReadSitPoints(node, fromX, toX, out IReadOnlyList<float> sitPoints))
            {
                topology = null;
                AppLogger.Warning("InteractivePet", "Тема содержит некорректную поверхность питомца.");
                return false;
            }

            surfaces.Add(id, new InteractivePetSurface(id, fromX, toX, y, sitPoints));

            if (node.HasMeta(InteractivePetContract.SurfaceVisibilityBindingMetadata))
            {
                if (!TryReadString(
                        node,
                        InteractivePetContract.SurfaceVisibilityBindingMetadata,
                        out string visibilityBindingId)
                    || !visibilityBindings.TryGetValue(
                        visibilityBindingId,
                        out CanvasItem? visibilityTarget))
                {
                    topology = null;
                    AppLogger.Warning(
                        "InteractivePet",
                        "Поверхность питомца ссылается на некорректный биндинг видимости.");
                    return false;
                }

                if (!visibilityTarget.Visible)
                {
                    surfaces.Remove(id);
                    suppressedSurfaceIds.Add(id);
                }
            }
        }

        if (!surfaces.ContainsKey(defaultSurfaceId))
        {
            topology = null;
            AppLogger.Warning("InteractivePet", "В теме не найдена поверхность питомца по умолчанию.");
            return false;
        }

        var edgeIds = new HashSet<string>(StringComparer.Ordinal);
        var edges = new List<InteractivePetClimbEdge>();
        foreach (Node node in nodes)
        {
            if (!TryReadBool(node, InteractivePetContract.ClimbEdgeMetadata, out bool isEdge)
                || !isEdge)
            {
                continue;
            }

            if (!TryReadString(node, InteractivePetContract.ClimbEdgeIdMetadata, out string id)
                || string.IsNullOrWhiteSpace(id)
                || !edgeIds.Add(id)
                || !TryReadString(node, InteractivePetContract.ClimbFromSurfaceMetadata, out string fromId)
                || !TryReadString(node, InteractivePetContract.ClimbToSurfaceMetadata, out string toId)
                || fromId == toId
                || !TryReadString(node, InteractivePetContract.ClimbSideMetadata, out string side)
                || side is not ("left" or "right")
                || !TryReadFloat(node, InteractivePetContract.ClimbXMetadata, out float x)
                || !TryReadFloat(node, InteractivePetContract.ClimbFromYMetadata, out float fromY)
                || !TryReadFloat(node, InteractivePetContract.ClimbToYMetadata, out float toY)
                || !float.IsFinite(x)
                || !float.IsFinite(fromY)
                || !float.IsFinite(toY))
            {
                topology = null;
                AppLogger.Warning("InteractivePet", "Тема содержит некорректный переход питомца.");
                return false;
            }


            if (suppressedSurfaceIds.Contains(fromId) || suppressedSurfaceIds.Contains(toId))
            {
                continue;
            }

            if (!surfaces.TryGetValue(fromId, out InteractivePetSurface? fromSurface)
                || !surfaces.TryGetValue(toId, out InteractivePetSurface? toSurface)
                || !Mathf.IsEqualApprox(fromY, fromSurface.Y)
                || !Mathf.IsEqualApprox(toY, toSurface.Y))
            {
                topology = null;
                AppLogger.Warning("InteractivePet", "Тема содержит некорректный переход питомца.");
                return false;
            }

            edges.Add(new InteractivePetClimbEdge(
                id,
                fromId,
                toId,
                new Vector2(x, fromY),
                new Vector2(x, toY),
                side));
        }

        topology = new InteractivePetTopology(bounds, defaultSurfaceId, surfaces, edges);
        return true;
    }

    private static IEnumerable<Node> EnumerateTree(Node root)
    {
        yield return root;
        foreach (Node child in root.GetChildren())
        {
            foreach (Node descendant in EnumerateTree(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool TryReadBool(Node node, StringName key, out bool value)
    {
        value = false;
        if (!node.HasMeta(key) || node.GetMeta(key).VariantType != Variant.Type.Bool)
        {
            return false;
        }

        value = node.GetMeta(key).AsBool();
        return true;
    }

    private static bool TryReadInt(Node node, StringName key, out long value)
    {
        value = 0;
        if (!node.HasMeta(key) || node.GetMeta(key).VariantType != Variant.Type.Int)
        {
            return false;
        }

        value = node.GetMeta(key).AsInt64();
        return true;
    }

    private static bool TryReadString(Node node, StringName key, out string value)
    {
        value = string.Empty;
        if (!node.HasMeta(key))
        {
            return false;
        }

        Variant variant = node.GetMeta(key);
        if (variant.VariantType == Variant.Type.StringName)
        {
            value = variant.AsStringName().ToString();
            return true;
        }

        if (variant.VariantType == Variant.Type.String)
        {
            value = variant.AsString();
            return true;
        }

        return false;
    }

    private static bool TryReadFloat(Node node, StringName key, out float value)
    {
        value = 0f;
        if (!node.HasMeta(key))
        {
            return false;
        }

        Variant variant = node.GetMeta(key);
        if (variant.VariantType == Variant.Type.Float)
        {
            value = (float)variant.AsDouble();
            return true;
        }

        if (variant.VariantType == Variant.Type.Int)
        {
            value = variant.AsInt64();
            return true;
        }

        return false;
    }

    private static bool TryReadRect(Node node, StringName key, out Rect2 value)
    {
        value = default;
        if (!node.HasMeta(key) || node.GetMeta(key).VariantType != Variant.Type.Rect2)
        {
            return false;
        }

        value = node.GetMeta(key).AsRect2();
        return true;
    }

    private static bool TryReadSitPoints(
        Node node,
        float fromX,
        float toX,
        out IReadOnlyList<float> sitPoints)
    {
        if (!node.HasMeta(InteractivePetContract.SurfaceSitPointsMetadata))
        {
            sitPoints = Array.Empty<float>();
            return true;
        }

        Variant variant = node.GetMeta(InteractivePetContract.SurfaceSitPointsMetadata);
        if (variant.VariantType != Variant.Type.PackedFloat32Array)
        {
            sitPoints = Array.Empty<float>();
            return false;
        }

        float[] points = variant.AsFloat32Array();
        if (points.Any(point => !float.IsFinite(point) || point < fromX || point > toX))
        {
            sitPoints = Array.Empty<float>();
            return false;
        }

        sitPoints = points;
        return true;
    }

    private static bool IsFinite(Vector2 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);
}

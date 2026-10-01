extends Node

const APP_SCENE := preload("res://src/scenes/App/App.tscn")

var _dance_animation_starts := 0
var _animation_starts: Array[StringName] = []


func _ready() -> void:
	var app := APP_SCENE.instantiate()
	add_child(app)
	for _frame in range(8):
		await get_tree().process_frame

	var pet_host := get_node_or_null("App/InteractivePetHost")
	if pet_host == null or not pet_host.HasPet:
		_fail("theme pet was not activated")
		return
	pet_host.SetReducedEffects(true)

	var start_anchor: Vector2 = pet_host.PetAnchor
	var press_position := start_anchor + Vector2(0.0, -30.0)
	var release_position := press_position + Vector2(110.0, -170.0)

	var press := InputEventScreenTouch.new()
	press.index = 0
	press.position = press_position
	press.pressed = true
	get_viewport().push_input(press, true)
	await get_tree().process_frame
	print("PET_SMOKE_AFTER_PRESS state=%s anchor=%s press=%s" % [pet_host.MotionState, pet_host.PetAnchor, press_position])
	if _current_pet_animation(pet_host) != "hanging":
		_fail("dragging did not use the theme hanging animation")
		return

	var drag := InputEventScreenDrag.new()
	drag.index = 0
	drag.position = release_position
	drag.relative = release_position - press_position
	get_viewport().push_input(drag, true)
	await get_tree().process_frame
	print("PET_SMOKE_AFTER_DRAG state=%s anchor=%s release=%s" % [pet_host.MotionState, pet_host.PetAnchor, release_position])

	var dragged_anchor: Vector2 = pet_host.PetAnchor
	if dragged_anchor.distance_to(start_anchor) < 50.0:
		_fail("single-finger drag did not move the pet")
		return

	var release := InputEventScreenTouch.new()
	release.index = 0
	release.position = release_position
	release.pressed = false
	get_viewport().push_input(release, true)
	await get_tree().process_frame
	var drop_animation := _current_pet_animation(pet_host)
	print("PET_SMOKE_AFTER_RELEASE state=%s animation=%s" % [pet_host.MotionState, drop_animation])
	if pet_host.MotionState == "Dropping" and drop_animation != "falling":
		_fail("falling after drag did not use the theme falling animation (actual: %s)" % drop_animation)
		return

	var touch_landed := false
	var saw_touch_landing := false
	for _frame in range(6000):
		await get_tree().process_frame
		if pet_host.MotionState == "Landing":
			saw_touch_landing = true
			if _current_pet_animation(pet_host) != "landing":
				_fail("landing state did not use the theme landing animation")
				return
		if pet_host.MotionState == "Sitting":
			touch_landed = true
			break

	if not touch_landed:
		_fail("pet did not land on a declared surface after touch drag")
		return
	if not saw_touch_landing:
		_fail("pet skipped the landing animation after touch drag")
		return
	var post_landing_animation := _current_pet_animation(pet_host)
	if post_landing_animation != "idle" and post_landing_animation != "sitting":
		_fail("pet used an unexpected rest animation after landing: %s" % post_landing_animation)
		return

	var mouse_start: Vector2 = pet_host.PetAnchor
	var mouse_press_position := mouse_start + Vector2(0.0, -30.0)
	var mouse_release_position := mouse_press_position + Vector2(-70.0, -120.0)
	var mouse_press := InputEventMouseButton.new()
	mouse_press.button_index = MOUSE_BUTTON_LEFT
	mouse_press.position = mouse_press_position
	mouse_press.pressed = true
	get_viewport().push_input(mouse_press, true)
	await get_tree().process_frame

	var mouse_motion := InputEventMouseMotion.new()
	mouse_motion.position = mouse_release_position
	mouse_motion.relative = mouse_release_position - mouse_press_position
	get_viewport().push_input(mouse_motion, true)
	await get_tree().process_frame
	if pet_host.PetAnchor.distance_to(mouse_start) < 50.0:
		_fail("held left mouse drag did not move the pet")
		return

	var mouse_release := InputEventMouseButton.new()
	mouse_release.button_index = MOUSE_BUTTON_LEFT
	mouse_release.position = mouse_release_position
	mouse_release.pressed = false
	get_viewport().push_input(mouse_release, true)

	var mouse_landed := false
	for _frame in range(240):
		await get_tree().process_frame
		if pet_host.MotionState == "Sitting":
			mouse_landed = true
			break

	if not mouse_landed:
		_fail("pet did not land on a declared surface after mouse drag")
		return
	if pet_host.CurrentSurfaceId != "floor":
		if not pet_host.RequestMoveToSurface("floor"):
			_fail("host could not return the pet to the Home floor before climb verification")
			return
		if not await _wait_for_surface(pet_host, "floor", 900):
			_fail("pet did not return to the Home floor before climb verification")
			return

	var pet := _pet_instance(pet_host)
	if pet == null:
		_fail("pet instance is unavailable for climb action verification")
		return
	var animation_started_callback := Callable(self, "_on_pet_animation_started")
	if not pet.is_connected("pet_animation_started", animation_started_callback):
		pet.connect("pet_animation_started", animation_started_callback)
	_animation_starts.clear()

	if not pet_host.RequestMoveToSurface("advertisement_window"):
		_fail("host rejected a connected theme-owned window surface")
		return

	var saw_climb := false
	var saw_hanging_during_climb := false
	var saw_climb_action := false
	var climb_action_anchor := Vector2.ZERO
	for _frame in range(900):
		await get_tree().process_frame
		saw_climb = saw_climb or pet_host.MotionState.begins_with("Climb")
		if pet_host.MotionState.begins_with("Climb") and _current_pet_animation(pet_host) == "hanging":
			saw_hanging_during_climb = true
		if pet_host.MotionState.begins_with("ClimbAction"):
			if not saw_climb_action:
				saw_climb_action = true
				climb_action_anchor = pet_host.PetAnchor
			elif pet_host.PetAnchor.distance_to(climb_action_anchor) > 0.1:
				_fail("pet moved while the climbing action sequence was playing")
				return
		if pet_host.MotionState == "Sitting" and pet_host.CurrentSurfaceId == "advertisement_window":
			if not saw_climb:
				_fail("pet reached an elevated window without entering a climb state")
				return
			if saw_hanging_during_climb:
				_fail("hanging animation played while completing a climb")
				return
			if not saw_climb_action:
				_fail("pet completed the climb without the climbing action sequence")
				return
			var expected_climb_action: Array[StringName] = [
				&"climbing_action_start",
				&"climbing_action",
				&"climbing_action_stop",
			]
			if not _contains_animation_subsequence(_animation_starts, expected_climb_action):
				_fail("unexpected climbing action sequence: %s" % [_animation_starts])
				return
			var post_walk_animation := _current_pet_animation(pet_host)
			if post_walk_animation != "idle" and post_walk_animation != "sitting":
				_fail("post-walk rest used an unexpected animation: %s" % post_walk_animation)
				return
			if not await _verify_screen_integration(pet_host):
				return
			print("INTERACTIVE_PET_SMOKE_OK surface=%s anchor=%s" % [
				pet_host.CurrentSurfaceId,
				pet_host.PetAnchor,
			])
			get_tree().quit(0)
			return

	_fail("pet did not climb to and sit on the declared window surface")


func _verify_screen_integration(pet_host: Node) -> bool:
	var screen_manager := get_node_or_null("/root/ScreenManager")
	if screen_manager == null:
		_fail("screen manager autoload is unavailable")
		return false

	var elevated_anchor: Vector2 = pet_host.PetAnchor
	if not screen_manager.Show(1, false): # CashPayment
		_fail("could not switch from Home to CashPayment")
		return false
	await get_tree().process_frame
	if not pet_host.visible:
		_fail("pet was hidden on CashPayment")
		return false
	if pet_host.MotionState != "Dropping" or _current_pet_animation(pet_host) != "falling":
		_fail("pet did not start falling toward the bottom after a screen change")
		return false

	if not await _wait_for_landing_then_sitting(pet_host, 360):
		_fail("pet did not land after switching from Home to CashPayment")
		return false
	if pet_host.PetAnchor.y <= elevated_anchor.y:
		_fail("pet did not land at the bottom after leaving an elevated Home surface")
		return false

	# Prove that the CashPayment topology is active by completing a climb onto its button.
	if not pet_host.RequestMoveToSurface("dispense_button"):
		_fail("CashPayment topology rejected its dispense button surface")
		return false
	if not await _wait_for_surface(pet_host, "dispense_button", 900):
		_fail("pet did not walk and climb onto the CashPayment dispense button")
		return false

	# Switching from an elevated surface must preserve the old anchor and drop to
	# the default floor declared by the next scene.
	var cash_button_anchor: Vector2 = pet_host.PetAnchor
	if not screen_manager.Show(2, false): # CardAmount
		_fail("could not switch from CashPayment to CardAmount")
		return false
	var anchor_after_switch: Vector2 = pet_host.PetAnchor
	if anchor_after_switch.distance_to(cash_button_anchor) > 0.1:
		_fail("screen change did not preserve the pet anchor before falling")
		return false
	await get_tree().process_frame
	if not pet_host.visible:
		_fail("pet was hidden on CardAmount")
		return false
	if pet_host.MotionState != "Dropping" or _current_pet_animation(pet_host) != "falling":
		_fail("pet did not fall from the old CashPayment position on CardAmount")
		return false
	if not await _wait_for_sitting(pet_host, 300):
		_fail("pet did not land on the CardAmount floor")
		return false
	if not pet_host.RequestMoveToSurface("confirm_button"):
		_fail("CardAmount topology rejected its confirm button surface")
		return false

	# A unique reachable target on every remaining screen proves that the host
	# switched to that scene's theme-owned topology rather than reusing Home.
	var screen_targets := [
		[3, "keypad_action_row", "CardCustomAmount"],
		[4, "cancel_button", "CardTerminal"],
		[5, "home_button", "Success"],
		[6, "home_button", "Error"],
		[7, "cancel_button", "ServiceAccess"],
		[0, "advertisement_window", "Home"],
	]
	for entry in screen_targets:
		var screen_id: int = entry[0]
		var surface_id: String = entry[1]
		var screen_name: String = entry[2]
		if not screen_manager.Show(screen_id, false):
			_fail("could not switch to %s" % screen_name)
			return false
		await get_tree().process_frame
		if not pet_host.visible:
			_fail("pet was hidden on %s" % screen_name)
			return false
		if not pet_host.RequestMoveToSurface(surface_id):
			_fail("%s topology rejected its %s surface" % [screen_name, surface_id])
			return false

	# Preserve an elevated position while Settings hides the pet, then fall on the next screen.
	var press_position: Vector2 = pet_host.PetAnchor + Vector2(0.0, -30.0)
	var held_position: Vector2 = press_position + Vector2(0.0, -180.0)
	var press := InputEventScreenTouch.new()
	press.index = 0
	press.position = press_position
	press.pressed = true
	get_viewport().push_input(press, true)
	await get_tree().process_frame
	var drag := InputEventScreenDrag.new()
	drag.index = 0
	drag.position = held_position
	drag.relative = held_position - press_position
	get_viewport().push_input(drag, true)
	await get_tree().process_frame
	var position_before_settings: Vector2 = pet_host.PetAnchor

	if not screen_manager.Show(8, false): # Settings
		_fail("could not switch to Settings")
		return false
	await get_tree().process_frame
	if pet_host.visible:
		_fail("pet remained visible on Settings")
		return false
	if not pet_host.PetAnchor.is_equal_approx(position_before_settings):
		_fail("Settings changed the pet position while hiding it")
		return false

	if not screen_manager.Show(6, false): # Error
		_fail("could not leave Settings for Error")
		return false
	await get_tree().process_frame
	if not pet_host.visible or pet_host.MotionState != "Dropping":
		_fail("pet did not reappear falling from its saved position after Settings")
		return false
	if _current_pet_animation(pet_host) != "falling":
		_fail("pet did not use falling after leaving Settings")
		return false

	if not await _wait_for_sitting(pet_host, 300):
		_fail("pet did not reach the bottom after leaving Settings")
		return false

	# A dispense celebration requested while the pet is falling must wait for
	# landing and then restart the mapped dance exactly four times.
	if not pet_host.RequestMoveToSurface("home_button"):
		_fail("Error topology rejected its home button before celebration test")
		return false
	if not await _wait_for_surface(pet_host, "home_button", 900):
		_fail("pet did not climb onto the Error home button")
		return false

	var pet := _pet_instance(pet_host)
	if pet == null:
		_fail("pet instance is unavailable for celebration verification")
		return false
	var animation_started_callback := Callable(self, "_on_pet_animation_started")
	if not pet.is_connected("pet_animation_started", animation_started_callback):
		pet.connect("pet_animation_started", animation_started_callback)
	_dance_animation_starts = 0

	if not screen_manager.Show(5, false): # Success
		_fail("could not switch from Error to Success for celebration test")
		return false
	if not pet_host.RequestDispenseCelebration(4):
		_fail("host rejected the four-cycle dispense celebration")
		return false
	if pet_host.MotionState != "Dropping":
		_fail("dispense celebration did not wait for the screen-change drop")
		return false

	var saw_landing_before_celebration := false
	var saw_celebration := false
	for _frame in range(6000):
		await get_tree().process_frame
		if pet_host.MotionState == "Landing":
			saw_landing_before_celebration = true
			if _current_pet_animation(pet_host) != "landing":
				_fail("queued celebration landing state used the wrong animation")
				return false
		if pet_host.MotionState == "Celebrating" and not saw_landing_before_celebration:
			_fail("dispense celebration started before the landing animation completed")
			return false
		saw_celebration = saw_celebration or pet_host.MotionState == "Celebrating"
		if saw_celebration and pet_host.MotionState == "Sitting":
			if not saw_landing_before_celebration:
				_fail("dispense celebration skipped the landing animation")
				return false
			if _dance_animation_starts != 4:
				_fail("dispense celebration started %s dance cycles instead of 4" % _dance_animation_starts)
				return false
			return await _verify_composite_interaction(pet_host)
	_fail("four-cycle dispense celebration did not finish")
	return false


func _wait_for_sitting(pet_host: Node, max_frames: int) -> bool:
	for _frame in range(maxi(max_frames, 1200)):
		await get_tree().process_frame
		if pet_host.MotionState == "Sitting":
			return true
	return false


func _wait_for_landing_then_sitting(pet_host: Node, max_frames: int) -> bool:
	var saw_landing := false
	for _frame in range(maxi(max_frames, 1200)):
		await get_tree().process_frame
		if pet_host.MotionState == "Landing":
			saw_landing = true
			if _current_pet_animation(pet_host) != "landing":
				return false
		if pet_host.MotionState == "Sitting":
			var post_landing_animation := _current_pet_animation(pet_host)
			return saw_landing and (
				post_landing_animation == "idle" or post_landing_animation == "sitting"
			)
	return false


func _verify_composite_interaction(pet_host: Node) -> bool:
	if pet_host.MotionState != "Sitting" and not await _wait_for_sitting(pet_host, 300):
		_fail("pet did not reach an initial surface before the composite action")
		return false
	pet_host.SetReducedEffects(false)

	var pet := _pet_instance(pet_host)
	if pet == null:
		_fail("pet instance is unavailable for composite action verification")
		return false
	var animation_started_callback := Callable(self, "_on_pet_animation_started")
	if not pet.is_connected("pet_animation_started", animation_started_callback):
		pet.connect("pet_animation_started", animation_started_callback)
	if not bool(pet.call("play_pet_animation", &"walking_left")):
		_fail("could not prepare the left-facing composite action branch")
		return false
	await get_tree().process_frame

	_animation_starts.clear()
	pet_host.RequestInteraction(1)
	if pet_host.MotionState != "ThemeAction" or not pet_host.HasActiveCompositeAction:
		_fail("host did not delegate interaction to the theme composite action")
		return false

	var visual_root := pet.get_node_or_null("VisualRoot") as Node2D
	var saw_double_scale := false
	var saw_vertical_wrap := false
	var previous_anchor: Vector2 = pet_host.PetAnchor
	for _frame in range(2400):
		await get_tree().process_frame
		if visual_root != null and visual_root.scale.x >= 1.95:
			saw_double_scale = true
		var current_anchor: Vector2 = pet_host.PetAnchor
		if previous_anchor.y - current_anchor.y > 500.0:
			saw_vertical_wrap = true
		previous_anchor = current_anchor
		if pet_host.MotionState == "Sitting" and not pet_host.HasActiveCompositeAction:
			break

	if pet_host.MotionState != "Sitting" or pet_host.HasActiveCompositeAction:
		_fail("theme composite action did not return control to the host")
		return false
	if not saw_double_scale:
		_fail("come_closer did not enlarge the theme-owned visual to 2x")
		return false
	if not saw_vertical_wrap:
		_fail("falling did not cross the bottom and wrap to the top")
		return false
	if pet_host.CurrentSurfaceId.is_empty() or pet_host.PetAnchor.y >= 200.0:
		_fail("composite action did not land on the highest declared support")
		return false

	var expected: Array[StringName] = [
		&"turn_left",
		&"come_closer",
		&"action",
		&"falling",
		&"landing",
		&"idle",
	]
	if not _contains_animation_subsequence(_animation_starts, expected):
		_fail("unexpected composite animation sequence: %s" % [_animation_starts])
		return false
	return true


func _contains_animation_subsequence(
	actual: Array[StringName],
	expected: Array[StringName]
) -> bool:
	var expected_index := 0
	for animation_name in actual:
		if expected_index < expected.size() and animation_name == expected[expected_index]:
			expected_index += 1
	return expected_index == expected.size()


func _wait_for_surface(pet_host: Node, surface_id: String, max_frames: int) -> bool:
	for _frame in range(max_frames):
		await get_tree().process_frame
		if pet_host.MotionState == "Sitting" and pet_host.CurrentSurfaceId == surface_id:
			return true
	return false


func _current_pet_animation(pet_host: Node) -> String:
	var pet := _pet_instance(pet_host)
	if pet == null:
		return ""
	if not pet.has_method("get_current_pet_animation"):
		return ""
	return String(pet.call("get_current_pet_animation"))


func _pet_instance(pet_host: Node) -> Node:
	var pet_stage := pet_host.get_node_or_null("PetStage")
	if pet_stage == null or pet_stage.get_child_count() != 1:
		return null
	return pet_stage.get_child(0)


func _on_pet_animation_started(animation_name: StringName) -> void:
	_animation_starts.append(animation_name)
	if animation_name == &"dancing":
		_dance_animation_starts += 1


func _fail(message: String) -> void:
	push_error("INTERACTIVE_PET_SMOKE_FAILED: %s" % message)
	get_tree().quit(1)

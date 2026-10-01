extends Node

const INVENTORY_ENABLED_ID := "settings.scroll.content.service_inventory_panel.margin.content.inventory_enabled"
const PURCHASE_LIMIT_ENABLED_ID := "settings.scroll.content.service_inventory_panel.margin.content.purchase_limit_enabled"
const INVENTORY_COUNT_ID := "settings.scroll.content.service_inventory_panel.margin.content.count"
const HOPPER1_LABEL_ID := "settings.scroll.content.service_inventory_panel.margin.content.hopper1_label"
const HOPPER2_LABEL_ID := "settings.scroll.content.service_inventory_panel.margin.content.hopper2_label"
const HOPPER1_RECOUNT_ID := "settings.scroll.content.service_inventory_panel.margin.content.hopper1.recount_button"
const HOPPER1_ADD_ID := "settings.scroll.content.service_inventory_panel.margin.content.hopper1.manual_add_button"
const HOPPER2_RECOUNT_ID := "settings.scroll.content.service_inventory_panel.margin.content.hopper2.recount_button"
const HOPPER2_ADD_ID := "settings.scroll.content.service_inventory_panel.margin.content.hopper2.manual_add_button"
const INVENTORY_STATUS_ID := "settings.scroll.content.service_inventory_panel.margin.content.status"
const HOME_APPROXIMATE_COUNT_ID := "home.scroll.content.stock_status.approximate_count"
const HOME_CASH_BUTTON_ID := "home.scroll.content.payment_panel.margin.content.buttons.cash_button"
const HOME_CARD_BUTTON_ID := "home.scroll.content.payment_panel.margin.content.buttons.card_button"


func _ready() -> void:
	var app_scene: PackedScene = load("res://src/scenes/App/App.tscn")
	var app: Node = app_scene.instantiate()
	add_child(app)
	for _frame in range(10):
		await get_tree().process_frame

	var home := app.get_node_or_null("UiLayer/ScreenHost/Home")
	var settings := app.get_node_or_null("UiLayer/ScreenHost/Settings")
	_assert(home != null and settings != null, "Не созданы Home или Settings")
	_send_action("debug_service_settings", true)
	_send_action("debug_service_settings", false)
	for _frame in range(20):
		await get_tree().process_frame
	_assert(settings.visible, "Физическая сервисная кнопка не открыла Settings")

	var inventory_enabled := _resolve(
		settings,
		INVENTORY_ENABLED_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/InventoryAccountingEnabled") as CheckButton
	var inventory_count := _resolve(
		settings,
		INVENTORY_COUNT_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Count") as Label
	var purchase_limit_enabled := _resolve(
		settings,
		PURCHASE_LIMIT_ENABLED_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/PurchaseLimitEnabled") as CheckButton
	var hopper1_label := _resolve(settings, HOPPER1_LABEL_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper1Label") as Label
	var hopper2_label := _resolve(settings, HOPPER2_LABEL_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper2Label") as Label
	var hopper1_recount := _resolve(settings, HOPPER1_RECOUNT_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper1/RecountButton") as Button
	var hopper1_add := _resolve(settings, HOPPER1_ADD_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper1/ManualAddButton") as Button
	var hopper2_recount := _resolve(settings, HOPPER2_RECOUNT_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper2/RecountButton") as Button
	var hopper2_add := _resolve(settings, HOPPER2_ADD_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper2/ManualAddButton") as Button
	var inventory_status := _resolve(settings, INVENTORY_STATUS_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Status") as Label
	var approximate_count := _resolve(home, HOME_APPROXIMATE_COUNT_ID,
		"SafeMargin/Scroll/Content/StockStatus/Margin/Content/AvailabilityRow/ApproximateCount") as Label
	var cash_button := _resolve(home, HOME_CASH_BUTTON_ID,
		"SafeMargin/Scroll/Content/PaymentPanel/Margin/Content/Buttons/CashButton") as Button
	var card_button := _resolve(home, HOME_CARD_BUTTON_ID,
		"SafeMargin/Scroll/Content/PaymentPanel/Margin/Content/Buttons/CardButton") as Button
	var controls := [inventory_count, hopper1_label, hopper2_label, hopper1_recount,
		hopper1_add, hopper2_recount, hopper2_add, inventory_status]
	_assert(inventory_enabled != null and purchase_limit_enabled != null \
		and approximate_count != null and cash_button != null and card_button != null,
		"Не разрешены переключатели учёта/лимита или элементы Home")
	_assert(controls.all(func(control: Control) -> bool: return control != null),
		"Не разрешены сервисные элементы учёта")
	_assert(inventory_enabled.button_pressed, "Учёт должен быть включён по умолчанию")
	_assert(purchase_limit_enabled.button_pressed,
		"Ограничение покупки должно быть включено по умолчанию")
	_assert(purchase_limit_enabled.visible,
		"Отдельный переключатель ограничения скрыт при включённом учёте")
	_assert(controls.all(func(control: Control) -> bool: return control.visible),
		"Сервисные элементы скрыты при включённом учёте")
	_assert(approximate_count.visible,
		"При включённом учёте скрыт приблизительный остаток на Home")
	_assert(cash_button.disabled and card_button.disabled,
		"Нулевой учтённый остаток не ограничил покупку")

	purchase_limit_enabled.button_pressed = false
	purchase_limit_enabled.emit_signal("toggled", false)
	_assert(controls.all(func(control: Control) -> bool: return control.visible),
		"Отключение ограничения ошибочно скрыло элементы учёта")
	await get_tree().create_timer(0.8).timeout
	_assert(approximate_count.visible,
		"Отключение ограничения ошибочно скрыло приблизительный остаток")
	_assert(not cash_button.disabled and not card_button.disabled,
		"Покупка осталась заблокирована после отключения отдельного ограничения")

	inventory_enabled.button_pressed = false
	inventory_enabled.emit_signal("toggled", false)
	_assert(not purchase_limit_enabled.visible,
		"Ограничение покупки осталось видимым при выключенном учёте")
	_assert(controls.all(func(control: Control) -> bool: return not control.visible),
		"Сервисные элементы не скрылись после отключения учёта")
	await get_tree().create_timer(0.8).timeout
	_assert(not approximate_count.visible,
		"Приблизительный остаток на Home не скрылся после автосохранения")

	print("TOKEN_INVENTORY_ACCOUNTING_RUNTIME_SMOKE_OK")
	get_tree().quit(0)


func _send_action(action: StringName, pressed: bool) -> void:
	var event := InputEventAction.new()
	event.action = action
	event.pressed = pressed
	get_viewport().push_input(event, true)


func _find_binding(root: Node, binding_id: String) -> Node:
	if root.has_meta("exchanger_binding_id") \
			and str(root.get_meta("exchanger_binding_id")) == binding_id:
		return root
	for child in root.get_children(true):
		var match := _find_binding(child, binding_id)
		if match != null:
			return match
	return null


func _resolve(root: Node, binding_id: String, fallback_path: String) -> Node:
	var binding := _find_binding(root, binding_id)
	return binding if binding != null else root.get_node_or_null(fallback_path)


func _assert(condition: bool, message: String) -> void:
	if condition:
		return
	push_error("TOKEN_INVENTORY_ACCOUNTING_RUNTIME_SMOKE_FAILED: " + message)
	get_tree().quit(1)
	assert(condition, message)

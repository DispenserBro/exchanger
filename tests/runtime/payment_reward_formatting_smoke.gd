extends Node

const CARD_BASE_ID := "card_amount.content.reward_panel.content.value"
const CARD_BONUS_ID := "card_amount.content.reward_panel.content.bonus"
const CASH_BASE_ID := "cash_payment.content.summary.margin.values.tokens"
const CASH_BONUS_ID := "cash_payment.content.summary.margin.values.bonus"
const CUSTOM_BASE_ID := "card_custom_amount.content.amount_panel.margin.values.amount"
const CUSTOM_BONUS_ID := "card_custom_amount.content.amount_panel.margin.values.tokens"

var _failed := false


func _ready() -> void:
	var legacy_theme_compatibility := OS.get_cmdline_user_args().has(
		"--legacy-theme-compatibility")
	var app_scene: PackedScene = load("res://src/scenes/App/App.tscn")
	var app: Node = app_scene.instantiate()
	add_child(app)
	for _frame in range(12):
		await get_tree().process_frame
	var screen_host := app.get_node_or_null("UiLayer/ScreenHost")
	_assert(screen_host != null, "Не создан ScreenHost")
	var cash := screen_host.get_node_or_null("CashPayment")
	var card := screen_host.get_node_or_null("CardAmount")
	var custom := screen_host.get_node_or_null("CardCustomAmount")
	var bridge := get_node_or_null("Bridge")
	_assert(cash != null and card != null and custom != null,
		"Не созданы платёжные экраны")
	_assert(bridge != null, "Не создан C# bridge платёжного smoke-теста")
	bridge.ClearPurchaseLimits(cash, card, custom)

	var cash_caption := _find_label_text(cash, "К ВЫДАЧЕ")
	var cash_base := _resolve(cash, CASH_BASE_ID,
		"SafeMargin/Content/Summary/Margin/Values/Tokens") as Label
	var cash_bonus := _resolve(cash, CASH_BONUS_ID,
		"SafeMargin/Content/Summary/Margin/Values/Bonus") as Label
	var card_base := _resolve(card, CARD_BASE_ID,
		"SafeMargin/Content/RewardPanel/Content/Value") as Label
	var card_bonus := _resolve(card, CARD_BONUS_ID,
		"SafeMargin/Content/RewardPanel/Content/Bonus") as Label
	var preset := _find_first_visible_preset(card)
	var custom_base := _resolve(custom, CUSTOM_BASE_ID,
		"SafeMargin/Content/AmountPanel/Margin/Values/Amount") as Label
	var custom_bonus := _resolve(custom, CUSTOM_BONUS_ID,
		"SafeMargin/Content/AmountPanel/Margin/Values/Tokens") as Label
	_assert((legacy_theme_compatibility or cash_caption != null) \
		and cash_base != null and cash_bonus != null \
		and card_base != null and card_bonus != null \
		and preset != null and custom_base != null and custom_bonus != null,
		"Не разрешены платёжные bindings")

	preset.emit_signal("pressed")
	await get_tree().process_frame
	var preset_amount := int(preset.text.split(" ", false, 1)[0])
	bridge.SetCashBalance(cash, preset_amount)
	await get_tree().process_frame
	print("PAYMENT_REWARD_FORMATTING_VALUES preset=", preset.text,
		" base=", card_base.text, " bonus=", card_bonus.text)
	_assert(card_base.text == cash_base.text,
		"Базовые жетоны карты и наличной оплаты имеют разный формат")
	_assert(card_bonus.text == cash_bonus.text,
		"Бонус карты и наличной оплаты имеет разный формат")
	if not legacy_theme_compatibility:
		_assert(card_base.get_global_rect().get_center().x \
			< card_bonus.get_global_rect().get_center().x,
			"Поля жетонов и бонуса карточного пресета расположены не в том порядке")
	_assert(custom_bonus.text == "БЕЗ БОНУСА",
		"Нулевой бонус ручной карточной суммы имеет неверный формат")
	if not legacy_theme_compatibility:
		_assert(custom_base.get_global_rect().get_center().x \
			< custom_bonus.get_global_rect().get_center().x,
			"Поля жетонов и бонуса ручной карточной суммы расположены не в том порядке")

	if _failed:
		get_tree().quit(1)
		return

	print("PAYMENT_REWARD_FORMATTING_RUNTIME_SMOKE_OK")
	get_tree().quit(0)


func _find_binding(root: Node, binding_id: String) -> Node:
	if root.has_meta("exchanger_binding_id") \
			and str(root.get_meta("exchanger_binding_id")) == binding_id:
		return root
	for child in root.get_children(true):
		var match := _find_binding(child, binding_id)
		if match != null:
			return match
	return null


func _find_label_text(root: Node, expected_text: String) -> Label:
	if root is Label and root.text == expected_text:
		return root as Label
	for child in root.get_children(true):
		var match := _find_label_text(child, expected_text)
		if match != null:
			return match
	return null


func _find_first_visible_preset(card: Node) -> Button:
	for index in range(16):
		var binding_id := "card_amount.content.amount_panel.margin.content.preset_grid.slot_%02d" % index
		var button := _find_binding(card, binding_id) as Button
		if button != null and button.visible:
			return button
	var fallback_grid := card.get_node_or_null(
		"SafeMargin/Content/AmountPanel/Margin/Content/PresetGrid")
	if fallback_grid == null:
		return null
	for child in fallback_grid.get_children():
		if child is Button and child.visible:
			return child as Button
	return null


func _resolve(root: Node, binding_id: String, fallback_path: String) -> Node:
	var binding := _find_binding(root, binding_id)
	return binding if binding != null else root.get_node_or_null(fallback_path)


func _assert(condition: bool, message: String) -> void:
	if condition:
		return
	push_error("PAYMENT_REWARD_FORMATTING_RUNTIME_SMOKE_FAILED: " + message)
	_failed = true

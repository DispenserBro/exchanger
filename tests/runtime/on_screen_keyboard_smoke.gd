extends Node

const TEXT_FIELD_ID := "settings.scroll.content.branding_panel.margin.content.short_text"
const APPLICATION_NAME_FIELD_ID := "settings.scroll.content.branding_panel.margin.content.application_name"
const APPLICATION_NAME_ERROR_ID := "settings.scroll.content.branding_panel.margin.content.application_name_error"
const BRANDING_PREVIEW_ID := "settings.scroll.content.branding_preview_panel"
const NUMERIC_FIELD_ID := "settings.scroll.content.price_panel.margin.content.price"
const TEXT_PANEL_ID := "settings.keyboard_overlay.text_keyboard"
const NUMERIC_PANEL_ID := "settings.keyboard_overlay.numeric_keyboard"
const TEXT_KEY_ID := "settings.keyboard_overlay.text_keyboard.content.letter_rows.row0.key_00"
const TEXT_HIDE_ID := "settings.keyboard_overlay.text_keyboard.content.header.hide_button"
const TEXT_SPACE_ID := "settings.keyboard_overlay.text_keyboard.content.secondary_actions.space_button"
const NUMERIC_HIDE_ID := "settings.keyboard_overlay.numeric_keyboard.content.header.hide_button"
const NUMERIC_CLEAR_ID := "settings.keyboard_overlay.numeric_keyboard.content.grid.clear_button"
const BONUS_FIELD_ID := "settings.scroll.content.bonus_panel.margin.content.rows.slot_00.bonus"
const HOME_SPEECH_TEXT_ID := "home.scroll.content.speech_text"
const SHORT_TEXT_FIELD_ID := "settings.scroll.content.branding_panel.margin.content.short_text"
const HOPPER1_LABEL_ID := "settings.scroll.content.service_inventory_panel.margin.content.hopper1_label"
const HOPPER2_LABEL_ID := "settings.scroll.content.service_inventory_panel.margin.content.hopper2_label"
const HOPPER1_ADD_ID := "settings.scroll.content.service_inventory_panel.margin.content.hopper1.manual_add_button"
const INVENTORY_ENABLED_ID := "settings.scroll.content.service_inventory_panel.margin.content.inventory_enabled"
const INVENTORY_COUNT_ID := "settings.scroll.content.service_inventory_panel.margin.content.count"
const HOME_APPROXIMATE_COUNT_ID := "home.scroll.content.stock_status.approximate_count"
const SERVICE_DIALOG_ID := "settings.service_dialog_overlay"
const SERVICE_DIALOG_AMOUNT_ID := "settings.service_dialog_overlay.dialog.margin.content.amount"
const SERVICE_DIALOG_CANCEL_ID := "settings.service_dialog_overlay.dialog.margin.content.buttons.cancel_button"
const SETTINGS_SCROLL_ID := "settings.scroll"
const SETTINGS_FOOTER_ID := "settings.footer"
const SAVE_AND_EXIT_ID := "settings.footer.save_and_exit_button"


func _ready() -> void:
	var app_scene: PackedScene = load("res://src/scenes/App/App.tscn")
	var app: Node = app_scene.instantiate()
	add_child(app)
	for _frame in range(8):
		await get_tree().process_frame

	var home := app.get_node_or_null("UiLayer/ScreenHost/Home")
	_assert(home != null, "HomeScreen не создан")
	var speech_text := _resolve(
		home,
		HOME_SPEECH_TEXT_ID,
		"SafeMargin/Scroll/Content/SpeechText") as Label
	_assert(speech_text != null, "Не найден текст речевого bubble на HOME")
	_assert(not speech_text.text.strip_edges().is_empty(),
		"Короткий текст не подставлен в речевой bubble")
	_assert(speech_text.is_visible_in_tree(), "Речевой bubble скрывает короткий текст")
	await _verify_card_custom_amount_erase(app)

	var settings := app.get_node_or_null("UiLayer/ScreenHost/Settings")
	_assert(settings != null, "SettingsScreen не создан")
	_send_action("debug_service_settings", true)
	_send_action("debug_service_settings", false)
	for _frame in range(20):
		await get_tree().process_frame
	_assert(settings.visible, "Эмуляция физической сервисной кнопки не открыла Settings")
	var hopper1_label := _resolve(
		settings,
		HOPPER1_LABEL_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper1Label") as Label
	var hopper2_label := _resolve(
		settings,
		HOPPER2_LABEL_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper2Label") as Label
	_assert(hopper1_label != null and hopper2_label != null,
		"Не найдены раздельные остатки двух хопперов")
	_assert(hopper1_label.text.begins_with("ХОППЕР 1 — ОСТАТОК: "),
		"Label хоппера 1 не показывает точный остаток")
	_assert(hopper2_label.text.begins_with("ХОППЕР 2 — ОСТАТОК: "),
		"Label хоппера 2 не показывает точный остаток")
	var short_text_field := _resolve(
		settings,
		SHORT_TEXT_FIELD_ID,
		"SafeMargin/Scroll/Content/BrandingPanel/Margin/Content/ShortText") as LineEdit
	_assert(short_text_field != null, "В настройках отсутствует поле «Свой текст»")
	var branding_content := short_text_field.get_parent()
	var short_text_label := branding_content.get_node_or_null("ShortTextLabel") as Label
	var application_name_label := branding_content.get_node_or_null("ApplicationNameLabel") as Label
	var description := branding_content.get_node_or_null("Description") as Label
	var application_name := _resolve(
		settings,
		APPLICATION_NAME_FIELD_ID,
		"SafeMargin/Scroll/Content/BrandingPanel/Margin/Content/ApplicationName") as LineEdit
	var application_name_error := _resolve(
		settings,
		APPLICATION_NAME_ERROR_ID,
		"SafeMargin/Scroll/Content/BrandingPanel/Margin/Content/ApplicationNameError") as Label
	var branding_preview := _resolve(
		settings,
		BRANDING_PREVIEW_ID,
		"SafeMargin/Scroll/Content/BrandingPreviewPanel") as Control
	_assert(short_text_label != null and short_text_label.text == "СВОЙ ТЕКСТ • ДО 48 ЗНАКОВ",
		"Поле пользовательского текста не переименовано в «Свой текст»")
	_assert(description != null and not description.text.to_lower().contains("назван"),
		"Описание раздела всё ещё упоминает название")
	_assert(application_name != null and not application_name.visible,
		"Поле «Название» осталось видимым")
	_assert(application_name_error != null and not application_name_error.visible,
		"Скрытая ошибка поля «Название» осталась видимой")
	_assert(application_name_label != null and not application_name_label.visible,
		"Подпись «Название» осталась видимой")
	_assert(branding_preview != null and not branding_preview.visible,
		"Предпросмотр раздела «Общие» остался видимым")
	var themed_bonus := _find_binding(settings, BONUS_FIELD_ID) as SpinBox
	if themed_bonus != null:
		_assert(themed_bonus.suffix.is_empty(),
			"Тема всё ещё добавляет суффикс к количеству дополнительных жетонов")
	var text_field := _resolve(
		settings,
		TEXT_FIELD_ID,
		"SafeMargin/Scroll/Content/BrandingPanel/Margin/Content/ShortText") as LineEdit
	if text_field == null:
		_dump_tree(app, 0)
	_assert(text_field != null, "SettingsScreen или его текстовое поле не созданы")
	_show_ancestors(text_field)
	await get_tree().process_frame

	var text_panel := _resolve(
		settings, TEXT_PANEL_ID, "SafeMargin/KeyboardOverlay/TextKeyboard") as Control
	var text_key := _resolve(
		settings,
		TEXT_KEY_ID,
		"SafeMargin/KeyboardOverlay/TextKeyboard/Content/LetterRows/Row0/Key_00") as Button
	var text_hide := _resolve(
		settings,
		TEXT_HIDE_ID,
		"SafeMargin/KeyboardOverlay/TextKeyboard/Content/Header/HideButton") as Button
	var text_space := _resolve(
		settings,
		TEXT_SPACE_ID,
		"SafeMargin/KeyboardOverlay/TextKeyboard/Content/SecondaryActions/SpaceButton") as Button
	var footer := _resolve(settings, SETTINGS_FOOTER_ID, "Footer") as Control
	var save_and_exit := _resolve(settings, SAVE_AND_EXIT_ID, "Footer/SaveAndExitButton") as Button
	_assert(text_field != null and text_panel != null and text_key != null and text_hide != null \
		and text_space != null and footer != null and save_and_exit != null,
		"Текстовые биндинги не разрешены")
	if _find_binding(settings, TEXT_HIDE_ID) != null:
		_assert(text_hide.text.strip_edges().is_empty(),
			"Тематическая кнопка скрытия всё ещё содержит текст")
		_assert(text_hide.icon != null,
			"У тематической кнопки скрытия нет иконки")
	var initial_text := text_field.text
	var text_updates: Array[String] = []
	text_field.text_changed.connect(func(value: String) -> void: text_updates.append(value))
	text_field.caret_column = initial_text.length()
	text_field.grab_focus()
	await get_tree().process_frame
	await get_tree().process_frame
	_assert(text_panel.visible, "Текстовая клавиатура не открылась по фокусу")
	_assert(save_and_exit.disabled,
		"«Сохранить и выйти» доступна при открытой текстовой клавиатуре")
	_assert(footer.mouse_filter == Control.MOUSE_FILTER_IGNORE \
		and save_and_exit.mouse_filter == Control.MOUSE_FILTER_IGNORE,
		"Футер остаётся в hit-test при открытой экранной клавиатуре")
	save_and_exit.emit_signal("pressed")
	_assert(text_panel.visible,
		"Программное нажатие «Сохранить и выйти» закрыло открытую клавиатуру")
	text_key.emit_signal("pressed")
	_assert(text_field.text == initial_text + "й", "Символьная клавиша не изменила LineEdit")
	_assert(text_updates.size() == 1 and text_updates.back() == text_field.text,
		"Символьная клавиша не отправила немедленное изменение текстового поля")
	var text_before_bottom_row := text_field.text
	await _press_button_with_mouse(text_space, "Нижний ряд текстовой клавиатуры перекрыт")
	_assert(text_field.text == text_before_bottom_row + " ",
		"Футер перехватил нажатие нижнего ряда текстовой клавиатуры")
	text_hide.emit_signal("pressed")
	_assert(not text_panel.visible, "Кнопка скрытия не закрыла текстовую клавиатуру")
	_assert(not save_and_exit.disabled,
		"«Сохранить и выйти» не включилась после закрытия текстовой клавиатуры")
	_assert(save_and_exit.mouse_filter != Control.MOUSE_FILTER_IGNORE,
		"Кнопка выхода не восстановила обработку ввода после закрытия клавиатуры")

	var numeric_field := _resolve(
		settings,
		NUMERIC_FIELD_ID,
		"SafeMargin/Scroll/Content/PricePanel/Margin/Content/Price") as SpinBox
	var numeric_panel := _resolve(
		settings, NUMERIC_PANEL_ID, "SafeMargin/KeyboardOverlay/NumericKeyboard") as Control
	var numeric_hide := _resolve(
		settings,
		NUMERIC_HIDE_ID,
		"SafeMargin/KeyboardOverlay/NumericKeyboard/Content/Header/HideButton") as Button
	var numeric_clear := _resolve(
		settings,
		NUMERIC_CLEAR_ID,
		"SafeMargin/KeyboardOverlay/NumericKeyboard/Content/Grid/ClearButton") as Button
	_assert(numeric_field != null and numeric_panel != null and numeric_hide != null and numeric_clear != null,
		"Цифровые биндинги не разрешены")
	if _find_binding(settings, NUMERIC_HIDE_ID) != null:
		_assert(numeric_hide.text.strip_edges().is_empty(),
			"Тематическая кнопка скрытия цифровой клавиатуры всё ещё содержит текст")
		_assert(numeric_hide.icon != null,
			"У тематической кнопки скрытия цифровой клавиатуры нет иконки")
	var service_add := _resolve(
		settings,
		HOPPER1_ADD_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper1/ManualAddButton") as Button
	var service_dialog := _resolve(
		settings,
		SERVICE_DIALOG_ID,
		"SafeMargin/ServiceDialogOverlay") as Control
	var service_amount := _resolve(
		settings,
		SERVICE_DIALOG_AMOUNT_ID,
		"SafeMargin/ServiceDialogOverlay/Dialog/Margin/Content/Amount") as SpinBox
	var service_cancel := _resolve(
		settings,
		SERVICE_DIALOG_CANCEL_ID,
		"SafeMargin/ServiceDialogOverlay/Dialog/Margin/Content/Buttons/CancelButton") as Button
	_assert(service_add != null and service_dialog != null and service_amount != null and service_cancel != null,
		"Сервисный диалог ручного пополнения не разрешён")
	service_add.emit_signal("pressed")
	await get_tree().process_frame
	await get_tree().process_frame
	_assert(service_dialog.visible, "Диалог ручного пополнения не открылся")
	_assert(numeric_panel.visible and numeric_panel.is_visible_in_tree(),
		"Цифровая клавиатура не открылась вместе с ручным пополнением хоппера")
	_assert(save_and_exit.disabled,
		"«Сохранить и выйти» доступна при цифровой клавиатуре сервисного диалога")
	_assert(service_amount.get_line_edit().has_focus(),
		"Поле количества хоппера не получило фокус")
	numeric_hide.emit_signal("pressed")
	service_cancel.emit_signal("pressed")
	await get_tree().process_frame
	_assert(not service_dialog.visible and not numeric_panel.visible,
		"Сервисный диалог или клавиатура не закрылись")
	_assert(not save_and_exit.disabled,
		"«Сохранить и выйти» не включилась после закрытия сервисной клавиатуры")
	_show_ancestors(numeric_field)
	await get_tree().process_frame
	var numeric_edit := numeric_field.get_line_edit()
	var numeric_updates: Array[float] = []
	numeric_field.value_changed.connect(func(value: float) -> void: numeric_updates.append(value))
	numeric_edit.grab_focus()
	await get_tree().process_frame
	await get_tree().process_frame
	_assert(numeric_panel.visible, "Цифровая клавиатура не открылась для SpinBox")
	_assert(save_and_exit.disabled,
		"«Сохранить и выйти» доступна при открытой цифровой клавиатуре")
	numeric_clear.emit_signal("pressed")
	var updates_before_digits := numeric_updates.size()
	for digit in [1, 0]:
		var button := _resolve(
			settings,
			"settings.keyboard_overlay.numeric_keyboard.content.grid.digit%d" % digit,
			"SafeMargin/KeyboardOverlay/NumericKeyboard/Content/Grid/Digit%d" % digit) as Button
		_assert(button != null, "Не найдена цифровая клавиша %d" % digit)
		if digit == 0:
			await _assert_button_hit_target(button, "Нижний ряд цифровой клавиатуры перекрыт")
		button.emit_signal("pressed")
	_assert(is_equal_approx(numeric_field.value, 10.0), "SpinBox не применил значение 10")
	_assert(numeric_updates.size() == updates_before_digits + 2,
		"Цифровые клавиши не отправили по одному изменению SpinBox на каждую цифру")
	_assert(is_equal_approx(numeric_updates.back(), 10.0),
		"Последнее изменение SpinBox не содержит введённое значение 10")
	numeric_hide.emit_signal("pressed")
	_assert(not numeric_panel.visible, "Кнопка скрытия не закрыла цифровую клавиатуру")
	_assert(not save_and_exit.disabled,
		"«Сохранить и выйти» не включилась после закрытия цифровой клавиатуры")
	await _verify_settings_drag_scrolling(settings)
	await _verify_inventory_accounting_toggle(home, settings)
	await _verify_physical_service_button_with_keyboard(
		app, home as Control, settings as Control, text_field, text_panel)

	print("ON_SCREEN_KEYBOARD_RUNTIME_SMOKE_OK")
	get_tree().quit(0)


func _verify_card_custom_amount_erase(app: Node) -> void:
	var screen := app.get_node_or_null("UiLayer/ScreenHost/CardCustomAmount")
	_assert(screen != null, "CardCustomAmountScreen не создан")
	var input_value := _resolve(
		screen,
		"card_custom_amount.content.keypad_panel.content.input_value",
		"SafeMargin/Content/KeypadPanel/Content/InputValue") as Label
	var digit1 := _resolve(
		screen,
		"card_custom_amount.content.keypad_panel.content.keypad.digit1",
		"SafeMargin/Content/KeypadPanel/Content/Keypad/Digit1") as Button
	var digit2 := _resolve(
		screen,
		"card_custom_amount.content.keypad_panel.content.keypad.digit2",
		"SafeMargin/Content/KeypadPanel/Content/Keypad/Digit2") as Button
	var erase := _resolve(
		screen,
		"card_custom_amount.content.keypad_panel.content.keypad.clear_button",
		"SafeMargin/Content/KeypadPanel/Content/Keypad/ClearButton") as Button
	_assert(input_value != null and digit1 != null and digit2 != null and erase != null,
		"Не разрешены биндинги ручного ввода суммы")

	digit1.emit_signal("pressed")
	digit2.emit_signal("pressed")
	_assert(input_value.text == "12", "Цифры ручной суммы не добавились")
	erase.emit_signal("pressed")
	_assert(input_value.text == "1", "СТЕРЕТЬ не удалила последнюю цифру")
	erase.emit_signal("pressed")
	_assert(input_value.text == "0", "После удаления последней цифры не показан 0")


func _verify_settings_drag_scrolling(settings: Node) -> void:
	var scroll := _resolve(settings, SETTINGS_SCROLL_ID, "SafeMargin/Scroll") as ScrollContainer
	var footer := _resolve(settings, SETTINGS_FOOTER_ID, "Footer") as Control
	var save_and_exit := _resolve(settings, SAVE_AND_EXIT_ID, "Footer/SaveAndExitButton") as Button
	_assert(scroll != null and footer != null and save_and_exit != null,
		"Не найдены ScrollContainer или фиксированный футер настроек")
	_assert(not scroll.is_ancestor_of(footer), "Футер ошибочно помещён внутрь ScrollContainer")
	_assert(save_and_exit.text == "СОХРАНИТЬ И ВЫЙТИ", "У кнопки футера неверная подпись")
	_show_ancestors(scroll)
	await get_tree().process_frame
	var footer_y := footer.global_position.y

	var start := scroll.get_global_rect().get_center()
	scroll.scroll_vertical = 0
	_send_touch(start, true)
	_send_touch(start, false)
	await get_tree().process_frame
	_assert(scroll.scroll_vertical == 0, "Короткое касание ошибочно прокрутило Settings")

	_send_touch(start, true)
	_send_touch_drag(start - Vector2(0, 120), Vector2(0, -120))
	_send_touch(start - Vector2(0, 120), false)
	await get_tree().process_frame
	var touch_scroll := scroll.scroll_vertical
	_assert(touch_scroll >= 100, "Drag одним пальцем вверх не прокрутил Settings вниз")

	_send_touch(start, true)
	_send_touch_drag(start + Vector2(0, 80), Vector2(0, 80))
	_send_touch(start + Vector2(0, 80), false)
	await get_tree().process_frame
	_assert(scroll.scroll_vertical < touch_scroll, "Drag одним пальцем вниз не прокрутил Settings вверх")

	scroll.scroll_vertical = 0
	_send_mouse_button(start, true)
	_send_mouse_motion(start - Vector2(0, 120), Vector2(0, -120))
	_send_mouse_button(start - Vector2(0, 120), false)
	await get_tree().process_frame
	_assert(scroll.scroll_vertical >= 100, "Drag зажатой ЛКМ не прокрутил Settings")
	_assert(is_equal_approx(footer.global_position.y, footer_y),
		"Футер сместился вместе с прокручиваемым содержимым")


func _verify_inventory_accounting_toggle(home: Node, settings: Node) -> void:
	var inventory_enabled := _resolve(
		settings,
		INVENTORY_ENABLED_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/InventoryAccountingEnabled") as CheckButton
	var inventory_count := _resolve(
		settings,
		INVENTORY_COUNT_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Count") as Label
	var hopper1_label := _resolve(
		settings,
		HOPPER1_LABEL_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper1Label") as Label
	var hopper1_add := _resolve(
		settings,
		HOPPER1_ADD_ID,
		"SafeMargin/Scroll/Content/ServiceInventoryPanel/Margin/Content/Hopper1/ManualAddButton") as Button
	var approximate_count := _resolve(
		home,
		HOME_APPROXIMATE_COUNT_ID,
		"SafeMargin/Scroll/Content/StockStatus/Margin/Content/AvailabilityRow/ApproximateCount") as Label
	_assert(inventory_enabled != null and inventory_count != null and hopper1_label != null \
		and hopper1_add != null and approximate_count != null,
		"Не разрешены элементы управления учётом жетонов")
	_assert(inventory_enabled.button_pressed,
		"Учёт жетонов должен быть включён по умолчанию")
	_assert(inventory_count.visible and hopper1_label.visible and hopper1_add.visible,
		"Элементы учёта скрыты при включённом учёте")
	_assert(approximate_count.visible,
		"При включённом учёте скрыто приблизительное количество на HOME")

	inventory_enabled.button_pressed = false
	inventory_enabled.emit_signal("toggled", false)
	_assert(not inventory_count.visible and not hopper1_label.visible and not hopper1_add.visible,
		"Остатки, пересчёт или пополнение не скрылись после отключения учёта")
	await get_tree().create_timer(0.8).timeout
	_assert(not approximate_count.visible,
		"Приблизительное количество на HOME не скрылось после сохранения настройки")


func _verify_physical_service_button_with_keyboard(
	app: Node,
	home: Control,
	settings: Control,
	text_field: LineEdit,
	text_panel: Control) -> void:
	_assert(settings.visible and not home.visible,
		"Settings должен быть активен перед проверкой физической сервисной кнопки")
	text_field.grab_focus()
	await get_tree().process_frame
	await get_tree().process_frame
	_assert(text_panel.visible,
		"Текстовая клавиатура не открылась перед проверкой физической кнопки")

	_send_action("debug_service_settings", true)
	_send_action("debug_service_settings", false)
	for _frame in range(20):
		await get_tree().process_frame
	_assert(home.visible and not settings.visible,
		"Физическая сервисная кнопка не закрыла Settings при открытой клавиатуре")
	_assert(not text_panel.visible,
		"Физическая сервисная кнопка не закрыла экранную клавиатуру")
	_assert(app != null, "App уничтожен во время выхода физической сервисной кнопкой")


func _send_touch(position: Vector2, pressed: bool) -> void:
	var event := InputEventScreenTouch.new()
	event.index = 0
	event.position = position
	event.pressed = pressed
	get_viewport().push_input(event, true)


func _send_touch_drag(position: Vector2, relative: Vector2) -> void:
	var event := InputEventScreenDrag.new()
	event.index = 0
	event.position = position
	event.relative = relative
	get_viewport().push_input(event, true)


func _send_mouse_button(position: Vector2, pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.button_mask = MOUSE_BUTTON_MASK_LEFT if pressed else 0
	event.position = position
	event.pressed = pressed
	get_viewport().push_input(event, true)


func _send_mouse_motion(position: Vector2, relative: Vector2) -> void:
	var event := InputEventMouseMotion.new()
	event.position = position
	event.relative = relative
	event.button_mask = MOUSE_BUTTON_MASK_LEFT
	get_viewport().push_input(event, true)


func _send_action(action: StringName, pressed: bool) -> void:
	var event := InputEventAction.new()
	event.action = action
	event.pressed = pressed
	get_viewport().push_input(event, true)


func _press_button_with_mouse(button: Button, blocked_message: String) -> void:
	var center := button.get_global_rect().get_center()
	var hover_event := InputEventMouseMotion.new()
	hover_event.position = center
	hover_event.relative = Vector2.ZERO
	hover_event.button_mask = 0
	get_viewport().push_input(hover_event, true)
	await get_tree().process_frame
	var hovered := get_viewport().gui_get_hovered_control()
	_assert(hovered == button,
		"%s: %s" % [blocked_message, hovered.get_path() if hovered != null else "null"])
	_send_mouse_button(center, true)
	await get_tree().process_frame
	_send_mouse_button(center, false)
	await get_tree().process_frame


func _assert_button_hit_target(button: Button, blocked_message: String) -> void:
	var center := button.get_global_rect().get_center()
	var hover_event := InputEventMouseMotion.new()
	hover_event.position = center
	hover_event.relative = Vector2.ZERO
	hover_event.button_mask = 0
	get_viewport().push_input(hover_event, true)
	await get_tree().process_frame
	var hovered := get_viewport().gui_get_hovered_control()
	_assert(hovered == button,
		"%s: %s" % [blocked_message, hovered.get_path() if hovered != null else "null"])


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


func _show_ancestors(node: Node) -> void:
	var current: Node = node
	while current != null and current != self:
		current.process_mode = Node.PROCESS_MODE_INHERIT
		if current is Control:
			(current as Control).visible = true
		current = current.get_parent()


func _dump_tree(node: Node, depth: int) -> void:
	if depth <= 4:
		print("  ".repeat(depth) + str(node.name) + " [" + node.get_class() + "]")
	for child in node.get_children(true):
		_dump_tree(child, depth + 1)


func _assert(condition: bool, message: String) -> void:
	if condition:
		return
	push_error("ON_SCREEN_KEYBOARD_RUNTIME_SMOKE_FAILED: " + message)
	get_tree().quit(1)
	assert(condition, message)

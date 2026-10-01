# Внешние DLC-темы Exchanger

## Архитектура

Основной проект Exchanger не содержит редактируемых ресурсов тем. В нём остаются только:

- `BuiltInFallbackTheme` — единственная минимальная тема, создаваемая кодом;
- `VendingThemeDefinition` — runtime-представление уже загруженной темы;
- `ThemeDlcLoader` и валидатор manifest;
- `ThemeManager`, выбирающий fallback либо один внешний пакет при запуске.
- `ThemeBindableScreen` и `ThemeBindingResolver`, связывающие app-owned логику с визуальными
  сценами DLC по стабильным binding-id.

Все исходники визуальных тем находятся в соседнем проекте `../ExchangerThemeDlcBuilder/`. В Exchanger отсутствуют `themes/`, исходные `.tres`-темы, фоновые картинки и шаблоны их разработки.

## Путь из пользовательских файлов

Монтажный способ выбора темы задаётся в `user://config/technical_settings.toml`:

```toml
[theme]
enabled = true
directory = "user://theme_dlc"
file_name = "active_theme"
```

Путь собирается как `<directory>/<file_name>.pck`; расширение в `file_name` не указывается.
Разрешены `user://` и абсолютный локальный каталог, включая Linux-путь вроде
`/opt/exchanger/themes`. Если optional раздел `[theme]` отсутствует в старом TOML версии 1,
используется прежняя настройка из `appsettings.json`:

Для Windows разрешены `directory = 'D:\Exchanger\themes'` (literal TOML-строка без
экранирования), `directory = "D:/Exchanger/themes"` либо двойные обратные слеши внутри обычной
двойной строки.

Настройка хранится в `%APPDATA%\Godot\app_userdata\Exchanger\config\appsettings.json`. Значение по умолчанию:

```json
{
  "Themes": {
    "ReducedEffects": false,
    "ExternalDlcEnabled": true,
    "ExternalDlcPath": "user://theme_dlc/active_theme.pck"
  }
}
```

На Windows этот `user://` соответствует `%APPDATA%\Godot\app_userdata\Exchanger\theme_dlc\active_theme.pck`. Также разрешён полный абсолютный путь. Относительные пути и `res://` отклоняются. Технический TOML имеет приоритет над JSON, а разовые `--theme-dlc=D:\ExchangerThemes\active_theme.pck` и `--no-theme-dlc` — над TOML.

В debug-сборке эти параметры доступны во вкладке `F10 → Тема`. Корректный путь разрешено
сохранить до появления пакета: при следующем запуске отсутствующий файл безопасно оставит
встроенную тему. Включение DLC и изменение пути применяются только после перезапуска.
`ReducedEffects` имеет немедленный предварительный просмотр; кнопка «Отменить» возвращает
сохранённое значение.

## Формат пакета

DLC является доверенным Godot resource pack `.pck`:

```text
res://exchanger_theme_dlc/
├── manifest.json
├── ui_theme.tres
├── background.png|webp|jpg|svg       # необязательно, только legacy-композиция
├── background_decoration.tscn        # необязательно, только legacy-композиция
├── components/                        # необязательные theme-owned runtime-компоненты
│   └── interactive_pet.tscn           # рекомендуемое имя сцены питомца
├── visuals/                           # необязательные runtime-сцены экранов
├── scripts/                           # GDScript темы
├── contracts/                         # обязательно для manifest v2
│   └── screen_bindings.v2.json
└── scenes/                            # обязательно для manifest v2
    ├── screen_home.tscn
    ├── screen_cash_payment.tscn
    ├── screen_card_amount.tscn
    ├── screen_card_custom_amount.tscn
    ├── screen_card_terminal.tscn
    ├── screen_success.tscn
    ├── screen_error.tscn
    ├── screen_service_access.tscn
    └── screen_settings.tscn
```

Manifest версии 1:

```json
{
  "Format": "ExchangerThemeDlc",
  "FormatVersion": 1,
  "ThemeId": "sample-theme",
  "DisplayName": "Название темы",
  "UiThemePath": "res://exchanger_theme_dlc/ui_theme.tres",
  "BackgroundTexturePath": "res://exchanger_theme_dlc/background.svg",
  "BackgroundDecorationPath": "res://exchanger_theme_dlc/background_decoration.tscn",
  "BackgroundDecorationScale": 1.0,
  "InteractivePetScene": "res://exchanger_theme_dlc/components/interactive_pet.tscn",
  "VisualSlots": {
    "home.top": "res://exchanger_theme_dlc/visuals/home_top.tscn",
    "card.terminal": "res://exchanger_theme_dlc/visuals/card_terminal.tscn"
  },
  "FallbackColor": "#061229",
  "UseNearestTextureFilter": false
}
```

Версия 1 остаётся совместимой и меняет стили, фон и visual slots встроенных экранов. Для новой
темы следует использовать manifest версии 2: он передаёт теме всё видимое дерево каждого экрана.

```json
{
  "Format": "ExchangerThemeDlc",
  "FormatVersion": 2,
  "ThemeId": "sample-theme",
  "DisplayName": "Название темы",
  "UiThemePath": "res://exchanger_theme_dlc/ui_theme.tres",
  "InteractivePetScene": "res://exchanger_theme_dlc/components/interactive_pet.tscn",
  "ScreenScenes": {
    "screen.home": "res://exchanger_theme_dlc/scenes/screen_home.tscn",
    "screen.cash_payment": "res://exchanger_theme_dlc/scenes/screen_cash_payment.tscn",
    "screen.card_amount": "res://exchanger_theme_dlc/scenes/screen_card_amount.tscn",
    "screen.card_custom_amount": "res://exchanger_theme_dlc/scenes/screen_card_custom_amount.tscn",
    "screen.card_terminal": "res://exchanger_theme_dlc/scenes/screen_card_terminal.tscn",
    "screen.success": "res://exchanger_theme_dlc/scenes/screen_success.tscn",
    "screen.error": "res://exchanger_theme_dlc/scenes/screen_error.tscn",
    "screen.service_access": "res://exchanger_theme_dlc/scenes/screen_service_access.tscn",
    "screen.settings": "res://exchanger_theme_dlc/scenes/screen_settings.tscn"
  },
  "VisualSlots": {},
  "FallbackColor": "#061229",
  "UseNearestTextureFilter": true
}
```

`UiThemePath` обязателен. Фон и декорация могут быть пустыми строками.
`UseNearestTextureFilter` необязателен и по умолчанию равен `false`. Его следует включать
только для pixel-art тем; тогда приложение применяет nearest-фильтрацию к фону и Control-узлам,
не меняя глобальные настройки рендера для других тем.
`BackgroundDecorationScale` необязателен, по умолчанию равен `1.0` и допускает значения 0.1–4.0.
Он нужен только для decoration, подготовленной не в координатах host 720×1280.

`InteractivePetScene` необязателен. Он указывает на `PackedScene` внутри
`res://exchanger_theme_dlc/`; рекомендуемый путь — `components/interactive_pet.tscn`, но manifest
может объявить любое другое имя сцены внутри package. Exchanger безопасно проверяет namespace,
расширение и наличие ресурса, загружает сцену в `VendingThemeDefinition.InteractivePetScene`, но
показывает её на всех экранах, кроме `screen.settings`, для которых тема предоставила корректный
навигационный граф. Актуальный Builder требует такой граф в каждой из восьми non-settings сцен
темы с питомцем; host сохраняет совместимость со старыми пакетами и скрывает питомца только на
конкретной сцене без графа.
Обычная навигация и ввод остаются app-owned, а сцена питомца, анимации, размеры и геометрия
доступных поверхностей — theme-owned. Для уникальной многошаговой реакции тема может временно
управлять экранной опорной точкой через scoped runtime, не получая ссылок на внутренние узлы host.

Корень компонента питомца — `Node2D`. Нейтральный API состоит из методов
`get_pet_animation_names`, `get_pet_animation_frame_count`, `has_pet_animation`,
`play_pet_animation`, `pause_pet_animation`, `stop_pet_animation`,
`get_current_pet_animation`, `get_current_pet_frame` и сигналов `pet_animation_started` /
`pet_animation_completed`. Имена и количество анимаций определяет тема. Сцена напрямую ссылается
на свои ресурсы, поэтому каталог текстур может называться как угодно и не входит в контракт.

Для поведения тема дополнительно может реализовать методы `get_pet_action_animation`,
`get_pet_runtime_scale`, `get_pet_walk_speed`, `get_pet_climb_speed`, `get_pet_hit_size` и
`get_pet_ground_offset`. Первый метод сопоставляет нейтральные действия приложения с
фактическими именами клипов конкретной темы; в том числе `sit` для посадки после приземления.
Это сохраняет совместимость с уже выпущенными DLC.

Во время обычного падения host запрашивает действие `drop`, после контакта с объявленной
поверхностью — один полный цикл `landing`, а затем включает `idle`. Даже если при смене экрана
anchor уже совпадает с новой поверхностью, состояние `landing` не пропускается. Если старая тема
не содержит mapping или клип `landing`, host выдерживает безопасную фазу `Landing` в 0,5 секунды
и пишет предупреждение вместо немедленного перехода к `idle`. `celebrate` запускается при переходе
к клиентской выдаче жетонов: host последовательно проигрывает четыре полных цикла. Если тема не
содержит его, для обратной совместимости используется `interact`. Длительность цикла определяется
обязательным `get_pet_animation_frame_count` и optional `get_pet_animation_frames_per_second`; при
отсутствии корректной скорости действует безопасный fallback. Запрошенное во время `falling` или `landing`
празднование ждёт завершения приземления и только затем начинает первый цикл.

После завершения обычного маршрута host выбирает паузу независимо от темы: с вероятностью 35%
он показывает `idle` на 3–5 секунд, в остальных случаях — спокойный `sitting` на 1,5–3,5 секунды.
Темы без `sitting` остаются совместимыми: в этой ветке используется `idle`.

Необязательный составной контракт позволяет теме самой выполнить действие из нескольких клипов,
визуальных преобразований и перемещений. Он принимается только целиком: методы
`has_pet_composite_action`, `start_pet_composite_action`, `cancel_pet_composite_action` и сигналы
`pet_composite_action_started` / `pet_composite_action_finished`. При отсутствии или неполной
реализации Exchanger использует одиночную анимацию с тем же каноническим именем.

`start_pet_composite_action(action, runtime, parameters)` возвращает run id переданного runtime.
Для `interact` параметры содержат `action_repetitions` (1–16) и `reduced_effects`. Scoped runtime
даёт методы `GetRunId`, `IsActive`, `GetViewportRect`, `GetAnchor`, `GetHighestSupport`,
`SetMotionPolicy`, `SetAnchor`, `MoveAnchor` и `LandOnSupport`. Политика отдельно управляет обычной
навигацией (`suspended`/`theme_driven`), контактами с опорами (`ignore`, `solid`,
`after_vertical_wrap`), границами (`clamp`, `allow`, `wrap_vertical_once`) и пользовательским
вводом. Для посадки тема указывает `landing_surface_id`; host проверяет, что такая поверхность
существует и сейчас доступна.

Каждый runtime привязан к одному запуску. Смена экрана/темы, начало drag и конфликтующее действие
инвалидируют его; последующие команды старого runtime отклоняются. После сигнала успешного
завершения host возвращает обычное состояние Sitting. Поэтому GDScript темы обязан проверять
`IsActive()` после каждого `await`, корректно обрабатывать `cancel_pet_composite_action` и не
сохранять runtime для будущих запусков.

Маршруты задаются отдельными невидимыми metadata-маркерами внутри каждой готовой сцены, кроме
`screen.settings`:

- один root с `exchanger_pet_navigation_root = true`, версией, `Rect2` границами и id
  поверхности по умолчанию;
- поверхности с уникальным id, диапазоном X, координатой опорной точки Y и необязательными
  точками посадки;
- двунаправленные рёбра подъёма с id исходной/целевой поверхности, X края и двумя Y.

Поверхность может дополнительно объявить строковую metadata
`exchanger_pet_surface_visibility_binding_id`. Она должна ссылаться на `CanvasItem` этой же
экранной сцены по `exchanger_binding_id`. Если готовый визуальный элемент скрыт пользовательской
настройкой, host исключает такую поверхность и все связанные с ней рёбра из активного графа.

Все координаты описывают экранную опорную точку питомца в системе 720×1280. Host не вычисляет
поверхности из имён или геометрии `Control` и не меняет визуалы темы. Он ходит по объявленным
поверхностям, строит путь по рёбрам, поднимается и спускается у заданных краёв, а после
перетаскивания первым пальцем или зажатой ЛКМ сажает питомца на ближайшую нижнюю поверхность.
На каждом ненулевом участке подъёма или спуска host выбирает случайную точку между 20% и 80%
пути, приостанавливает в ней перемещение и последовательно запрашивает действия
`climbing_action_start`, `climbing_action` и `climbing_action_stop`. Средняя анимация выполняется
один полный цикл, после чего питомец продолжает тот же маршрут и направление. Последовательность
включается только при наличии всех трёх действий и клипов; тема без любого из них сохраняет
обычное непрерывное движение для обратной совместимости.
При переключении экрана host выбирает граф новой сцены, сохраняет текущую экранную позицию
питомца и запускает `falling` до её поверхности по умолчанию; после посадки доступен весь новый граф.
На `screen.settings` питомец скрыт, а его сохранённая позиция используется при выходе из настроек.
`hanging` действует только пока пользователь удерживает питомца, а `falling` — во время его падения
к выбранной поверхности после отпускания.
Режим reduced-effects отключает автономное блуждание, сохраняя явное взаимодействие и drag.

Новые темы используют самодостаточную композицию экранов. В этом режиме каждый root из
`ScreenScenes` имеет metadata `exchanger_self_contained_visuals = true`, назначенный `Theme` и
два прямых дочерних узла: `ThemeBackground` типа `TextureRect` с текстурой и
`BackgroundDecoration` типа `CanvasItem`. Все девять сцен пакета обязаны выбрать одинаковый
режим. `BackgroundTexturePath` и `BackgroundDecorationPath` при этом оставляют пустыми, иначе
приложение отклонит DLC как дублирующую визуальную композицию. Смешивать самодостаточные и
legacy-сцены в одном пакете также нельзя.

После инстанцирования host сохраняет присутствующее визуальное дерево самодостаточной сцены в том
виде, в каком оно собрано в Theme Builder. Он не назначает другой `Theme`, `ThemeTypeVariation`,
размеры, отступы или локальные theme overrides. Разрешены функциональные операции через
binding-id: подключение сигналов, обновление данных, видимости, доступности и состояния. Если
обязательного binding-id нет, host сначала принимает совместимый узел старой темы по закреплённому
`FallbackPath`, а при отсутствии самого узла переносит на его место готовый элемент встроенной
fallback-сцены с её оформлением. Внешние элементы, у которых binding-id присутствует, не меняются.

Для управления видимостью Home приложение использует готовые bindings
`home.scroll.content.stock_status`, `home.scroll.content.advertisement_panel` и необязательный
`home.decoration.custom_text_block`. Последний объединяет theme-owned фон пользовательской
речевой плашки и персонажа справа; для старых тем без этой группы сохранён fallback
`home.decoration.right_character`. Экран Settings предоставляет готовую
панель `settings.scroll.content.menu_visibility_panel` и три `CheckButton` с окончаниями
`show_stock_status`, legacy `show_right_character` (пользовательский текст и персонаж) и
`show_promotion_block`. Host только читает состояния
переключателей и меняет `Visible` соответствующих готовых элементов.

Фиксированный футер Settings задаётся supplemental bindings `settings.footer` и
`settings.footer.save_and_exit_button`; он является корневым соседом `SafeMargin`, а не потомком
`settings.scroll`. В конец прокручиваемого `Content` добавляется
`settings.scroll.content.footer_clearance`, чтобы нижние поля не перекрывались футером. Все три
узла обязательны для новых тем; в уже выпущенной теме без этих ID host переносит готовый футер и
зазор встроенной темы, не переоформляя остальные элементы DLC. Старый полноэкранный Scroll при
этом может проходить под непрозрачным футером; `footer_clearance` позволяет прокрутить последние
поля выше него без изменения геометрии выпущенной сцены.

Сервисный переключатель учёта использует supplemental binding
`settings.scroll.content.service_inventory_panel.margin.content.inventory_enabled` типа
`CheckButton`; независимый переключатель ограничения покупки —
`settings.scroll.content.service_inventory_panel.margin.content.purchase_limit_enabled` того же
типа. Изменяемое пояснение рядом с ними —
`settings.scroll.content.service_inventory_panel.margin.content.description` типа `Label`.
При выключении host меняет только функциональные `Visible`, `Disabled`, `ButtonPressed` и текст
готовых элементов этой панели. Старые PCK без metadata получают совместимые узлы по каноническим
fallback-путям; закреплённый JSON-каталог остаётся на 481 binding.

На всех пользовательских экранах, кроме Home и Settings, предусмотрена готовая кнопка
`home_navigation_button` с изображением дома. Его основа — `KeypadButton`; в `space-pixel` этот
вариант использует готовый `assets/ui/button_square.png`. На CashPayment, CardAmount, CardCustomAmount и
CardTerminal она расположена рядом с `BackButton`; на Success, Error и ServiceAccess занимает
обычную верхнюю левую позицию. Host только подписывается на `pressed`: незавершённую оплату он
безопасно отменяет, а на Success до завершения фактической выдачи кнопка недоступна. Новые темы
обязаны объявить семь соответствующих supplemental ID; старые PCK получают готовые fallback-узлы
из встроенных сцен.

Тексты нижних анимированных плашек с НЛО на CashPayment, CardAmount, CardCustomAmount и
CardTerminal настраиваются в разделе «Общие» через готовую панель темы
`settings.scroll.content.animated_banner_texts_panel`. Текст CashPayment выводится в
`cash_payment.content.banner_text`; для таймера CardTerminal в шаблоне нужно оставить
`{seconds}`, которое host заменяет количеством оставшихся секунд. Эти два новых supplemental ID
не изменяют immutable JSON-каталог; старой DLC-теме host подставляет встроенные узлы.

`VisualSlots` — необязательный словарь именованных `PackedScene`. Поддерживаемые host-слоты:

| Slot | Область preview |
|---|---|
| `home.top` | НЛО и реплика на главном экране |
| `home.advertisement` | тематическая рамка промо-блока |
| `cash.instructions` | купюра и монета |
| `cash.tip` | маскот и предупреждение о сдаче |
| `card.terminal` | анимация карты у терминала |
| `result.success` | анимация успешной выдачи |
| `result.error` | анимация ошибки |

Сцены слотов располагаются в нативной системе координат 720×1280 либо в локальном размере
соответствующего контейнера. Они не заменяют интерактивные узлы приложения: суммы, состояния,
кнопки и переходы всегда принадлежат host.

## Полносценовый контракт v2

Приложение всегда инстанцирует собственный C#-контроллер экрана, содержащий бизнес-логику,
навигацию и обращения к оборудованию. До входа контроллера в SceneTree его встроенный визуальный
корень `SafeMargin` заменяется сценой из DLC. Поэтому тема полностью определяет композицию и
вложенность `Control`, но не получает доступ к оплате, COM-порту или состоянию сессии.

Имена файлов, ключи `ScreenScenes` и имена корневых узлов фиксированы:

| Scene binding | Файл | Имя root |
|---|---|---|
| `screen.home` | `screen_home.tscn` | `ThemeScreen_Home` |
| `screen.cash_payment` | `screen_cash_payment.tscn` | `ThemeScreen_CashPayment` |
| `screen.card_amount` | `screen_card_amount.tscn` | `ThemeScreen_CardAmount` |
| `screen.card_custom_amount` | `screen_card_custom_amount.tscn` | `ThemeScreen_CardCustomAmount` |
| `screen.card_terminal` | `screen_card_terminal.tscn` | `ThemeScreen_CardTerminal` |
| `screen.success` | `screen_success.tscn` | `ThemeScreen_Success` |
| `screen.error` | `screen_error.tscn` | `ThemeScreen_Error` |
| `screen.service_access` | `screen_service_access.tscn` | `ThemeScreen_ServiceAccess` |
| `screen.settings` | `screen_settings.tscn` | `ThemeScreen_Settings` |

Root каждой сцены — `Control` с metadata `exchanger_screen_binding_id`, равной ключу из таблицы.
Интерактивные и динамические узлы получают уникальную metadata `exchanger_binding_id`.
Пример кнопки наличной оплаты:

```text
exchanger_binding_id = "home.scroll.content.payment_panel.margin.content.buttons.cash_button"
```

Binding-id вычисляется из стабильного fallback NodePath: префикс `SafeMargin/` удаляется,
каждый оставшийся сегмент переводится из PascalCase в snake_case, перед ним добавляется ключ
экрана. Фактическое положение узла в тематической сцене на binding-id не влияет. Точный
машиночитаемый каталог находится в `../ExchangerThemeDlcBuilder/contracts/screen_bindings.v2.json`,
а готовый шаблон — в `../ExchangerThemeDlcBuilder/themes/example/`. Preview-overlay является
инструментом Builder и не входит в production-сцены.

Manifest v2 обязан включать точную копию каталога в
`res://exchanger_theme_dlc/contracts/screen_bindings.v2.json`. Приложение закрепляет SHA-256
этого файла и проверяет девять экранов. Для присутствующих биндингов проверяются ID и Godot-тип;
неизвестные ID, дубли и неверные типы отклоняются. Отсутствующий обязательный ID не отключает весь
DLC: он разрешается через канонический `FallbackPath` из закреплённого каталога. Если точечная
подстановка невозможна, встроенная сцена используется только для проблемного экрана. Корневой
контракт сцены, self-contained композиция и подлинность каталога остаются строгими.

Все присутствующие визуальные узлы принадлежат теме. Приложение не переделывает их стили или
компоновку: оно находит узлы по binding-id, подключает сигналы и обновляет функциональные значения.
Единственное исключение — отсутствующий обязательный узел: для обратной совместимости на его
каноническое место переносится готовый элемент встроенной темы-заглушки.

Для итогов наличной оплаты предусмотрены три независимых обязательных `Label`:

- `cash_payment.content.summary.margin.values.balance` получает только внесённую сумму в рублях;
- `cash_payment.content.summary.margin.values.tokens` получает базовое количество жетонов;
- `cash_payment.content.summary.margin.values.bonus` получает бонус в формате
  `+ N ЖЕТОНОВ\nВ ПОДАРОК` либо `БЕЗ БОНУСА`.

Контроллер вычисляет жетоны через общую `PricingPolicy`, заполняет все три узла и не объединяет
значения в одну строку. Геометрия, перенос строк, выравнивание и оформление остаются частью темы.

Панель «К ВЫДАЧЕ» на экране готовых карточных сумм использует существующий обязательный Label
`card_amount.content.reward_panel.content.value` только для базового количества жетонов. Бонус
выводится отдельно через supplemental runtime binding
`card_amount.content.reward_panel.content.bonus` типа `Label` с каноническим fallback-путём
`SafeMargin/Content/RewardPanel/Content/Bonus`. Формат совпадает с наличной оплатой:
`+ N ЖЕТОНОВ\nВ ПОДАРОК` либо `БЕЗ БОНУСА`. Supplemental ID не входит в закреплённый JSON-каталог
481 binding; старая тема получает готовый встроенный Label побиндинговым fallback.

На экране ручной карточной суммы введённое число выводится отдельно через обязательный Label
`card_custom_amount.content.keypad_panel.content.input_value`. Верхние Label
`...amount_panel.margin.values.amount` и `...tokens` получают соответственно базовые и бонусные
жетоны; второй Label использует тот же двухстрочный формат либо текст `БЕЗ БОНУСА`. Таким образом
верхняя панель не содержит рублей, а поле под «ВВЕДИТЕ СУММУ» всегда отражает фактически набранное
значение.

Изменяемые списки представлены заранее созданными фиксированными слотами: 16 кнопок сумм,
16 правил дополнительных жетонов, 16 сумм оплаты и 12 промо-роликов. Неиспользуемые слоты
скрыты. Home аналогично содержит готовые узлы состояния запаса, связи, постера, видео и
резервного текста. Благодаря этому внешний вид полной актуальной темы совпадает с предпросмотром;
дерево дополняется только при отсутствии обязательного binding в старом пакете.

Строка запаса Home состоит из готовых узлов темы: текстового состояния слева, полоски по центру
и Label приблизительного общего остатка справа. Для последнего используется supplemental
runtime binding home.scroll.content.stock_status.approximate_count; приложение меняет только
его Text и скрывает Label при отключённом учёте. В Settings, напротив, сервисная панель получает
точное учётное количество, а при отключённом учёте скрывает остатки, пересчёт и пополнение.

Экран Settings дополнительно содержит две готовые панели `KeyboardOverlay`: текстовую и цифровую.
Они дают 57 обязательных биндингов: 33 символьные клавиши, 10 цифр, две кнопки скрытия и
12 служебных действий. Панели по умолчанию скрыты, все их кнопки имеют `focus_mode = None`,
чтобы нажатие не отнимало каретку у редактируемого поля. Host подключает `Pressed` и меняет только
`Visible`, `Text`, `Disabled` и `ButtonPressed`; создание, перестановка и стилизация узлов runtime
запрещены. В preview Builder предусмотрены состояния `KEYBOARD: OFF/TEXT/NUM`.

## Семантический UI-контракт

Runtime применяет один `Theme` ко всем девяти экранам, включая `ServiceAccess` и `Settings`.
Тема может оформлять следующие стабильные type variations:

| Variation | Базовый Godot-тип | Назначение |
|---|---|---|
| `ScreenTitle` | `Label` | главный заголовок экрана |
| `ScreenDescription` | `Label` | пояснение под заголовком |
| `PanelTitle` | `Label` | заголовок панели |
| `PanelText` | `Label` | крупное содержимое пользовательской панели |
| `FieldLabel` | `Label` | подпись или обычный текст поля настроек |
| `SupportText` | `Label` | подсказка и вторичный текст |
| `StatusText` | `Label` | текущее состояние операции или оборудования |
| `WarningText` | `Label` | предупреждение |
| `ErrorText` | `Label` | ошибка валидации или операции |
| `PrimaryButton` | `Button` | главное действие |
| `CompactButton` | `Button` | цифровая клавиша или компактное действие |
| `BackButton` | `Button` | компактный возврат с пользовательского экрана |
| `SectionTabButton` | `Button` | вкладка сервисных настроек |
| `SecondaryButton` | `Button` | отмена, закрытие или сброс |
| `DangerButton` | `Button` | удаление/необратимое действие |
| `WhitePanel` | `PanelContainer` | основная смысловая панель |
| `AdvertisementPanel` | `PanelContainer` | контейнер постера, текста или промо-ролика OGV/MP4/MOV/M4V |
| `FooterPanel` | `PanelContainer` | техподдержка и служебный footer Home |
| `StatusPanel` | `PanelContainer` | компактный индикатор связи/состояния |

Host всегда регистрирует базовый тип каждой variation. Поэтому старый корректный DLC v1,
в котором новый семантический тип ещё не оформлен, наследует безопасный стандартный стиль и
остаётся совместимым.

Экран настроек дополнительно использует базовые классы `LineEdit`, `SpinBox`, `OptionButton`,
`CheckButton`, `HSlider`, `VScrollBar` и `HScrollBar`. Полноценная тема должна явно задавать их
normal/focus/pressed/disabled-состояния и сохранять читаемость внутри `WhitePanel`.

## Доверенное выполнение и fallback

- Пакет монтируется с `replace_files=false` и не может заменить ресурсы Exchanger.
- Все пути manifest обязаны находиться внутри `res://exchanger_theme_dlc/`; `..`, обратные слеши и ссылки на сцены приложения запрещены.
- Decoration, visual slots и полносценовые view могут содержать GDScript и любые стандартные
  runtime-узлы Godot. Код начинает исполняться при инстанцировании сцены.
- Проверяются формат, версия, ThemeId, цвет, расширения, существование и тип каждого ресурса.
- Для manifest v2 проверяются закреплённый каталог биндингов, namespace, дубли и типы присутствующих
  узлов. Неизвестный либо неверно типизированный биндинг отключает DLC; отсутствующий обязательный
  биндинг восстанавливается по `FallbackPath` встроенным элементом.
- Самодостаточная сцена дополнительно проверяется на собственный `Theme`, текстурированный
  `ThemeBackground` и `BackgroundDecoration`; смешанный режим девяти сцен и одновременное
  объявление глобального фона или decoration запрещены.
- При отсутствии, повреждении или несовместимости DLC приложение продолжает запуск со встроенной fallback-темой.
- В журнал не записывается полный пользовательский путь к DLC.
- Одновременно загружается не более одного пакета. После замены `.pck` приложение нужно перезапустить.

## Runtime-возможности темы

В legacy-композиции `BackgroundTexturePath` задаёт общий фон, а `BackgroundDecorationPath` может
ссылаться на произвольную доверенную `PackedScene`. В самодостаточной композиции те же роли
выполняют `ThemeBackground` и `BackgroundDecoration` внутри каждой сцены.

Разрешены GDScript, сигналы, `Timer`, `Tween`, `AnimatedSprite2D`, `AnimationPlayer`, method tracks,
аудио, видео, частицы, шейдеры, процедурная графика, сетевые и файловые API Godot. Host не
применяет лимиты количества узлов, кадров, FPS или скорости анимации и не ограничивает размер PCK.

При `ReducedEffects=true` глобальная decoration не создаётся, а узел `BackgroundDecoration`
самодостаточного экрана скрывается и перестаёт обрабатываться. Остальная скриптовая логика сцены
продолжает работать; тема при желании может самостоятельно учитывать эту настройку.

## Разработка и сборка

Откройте соседний `ExchangerThemeDlcBuilder/project.godot` в Godot 4.7.1. После редактирования выполните:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-theme.ps1 -ThemeId space-pixel
```

Пакет появится в `build/space-pixel.pck`; совместимая копия будет записана в
`build/active_theme.pck`. Для одновременной установки в стандартный пользовательский путь:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-theme.ps1 -ThemeId space-pixel -InstallToDefaultUserPath
```

Сборщик проверяет, что запущен настоящий Godot 4.7, а не MCP-сервер `godot-ai`. Сборка DLC не требует C#-кода или ссылки на проект Exchanger.

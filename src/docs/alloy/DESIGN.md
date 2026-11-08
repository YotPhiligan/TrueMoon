# Визуальный язык Argentis и редакторов

Согласовано 2026-11-05 при переработке инспектора Aether.Editor.
[UI-план](PLAN.md), [текущий статус](STATUS.md),
[прикладной план инспектора](../engine/EDITOR_PLAN.md#ed4g-compact-transform).
Это общий дизайн-код UI; техническая готовность и фактические проверки ведутся в STATUS/HISTORY.

## Цвет передаёт смысл

Цвет — постоянная часть языка редакторов. Семантическая роль отвечает на вопрос,
что редактируется; состояние показывает, что происходит с конкретным контролом.
Их задают независимо. Палитра принадлежит Theme, композиция и роли — View,
draft/ошибка/доступность — Data/ViewModel. Прямые RGB в прикладных экранах не дублируются.

| Роль | Токен Theme | Применение |
| --- | --- | --- |
| Neutral | Border / Foreground | Обычные контролы без специального редактора |
| Number | NumberAccent | Числовое значение |
| Text | TextAccent | Строка и многострочный текст |
| Boolean | BooleanAccent | Логическое значение |
| Reference | ReferenceAccent | Ссылка на объект или ресурс; роль доступна для будущих consumers |
| AxisX | AxisX | X; Pitch в Transform |
| AxisY | AxisY | Y; Yaw в Transform |
| AxisZ | AxisZ | Z; Roll в Transform |

Оси различаются красным, зелёным и синим акцентами, а также буквами X/Y/Z или
P/Y/R и полным доступным именем. Тип редактора обозначается спокойным оттенком
границы; префикс оси получает полный акцент. Фон остаётся нейтральным, читаемость
числа важнее площади заливки. Dark и Light имеют собственные значения токенов.

## Реакция границ на события

Контролы используют общие `HasError`, `IsModified`, `ColorRole` и существующие
focus/hover/enabled. События меняют состояние; отрисовка разрешает текущую палитру.
Состояния не записывают данные и не запускают команды приложения.

| Приоритет themed-границы | Состояние / событие | Представление |
| --- | --- | --- |
| 1 | Недоступен, включая недоступного родителя | Нейтральная граница; ввод запрещён |
| 2 | Ошибка валидации | Error и красный значок справа у границы TextBox/RichTextEditor; точное пояснение в tooltip |
| 3 | Получен фокус | Focus; дополнительный внутренний контур толщиной 2 |
| 4 | Есть неприменённый draft или pending reset | Modified и маленький маркер изменения |
| 5 | Наведение указателя | BorderHover |
| 6 | Обычное состояние | Спокойная граница с акцентом роли или Border |

Ошибка и фокус могут отображаться одновременно: внешняя граница/маркер ошибки
и внутренний контур фокуса. Disabled гасит взаимодействие, но не скрывает текст
ошибки или наличие draft. Уход указателя не стирает ошибку, потеря фокуса не
применяет значение. После Apply/Cancel состояние снимается моделью, а не таймером.
Read-only сохраняет копирование/выделение и собственный фон TextBox.

CheckBox применяет тот же themed BorderColor к самому квадратному индикатору,
в том числе когда значение включено. Акцент заполнения соответствует роли, а
токен OnEditorAccent обеспечивает отдельный контраст галочки в Dark/Light.
Read-only CheckBox получает фон ReadOnly, принимает фокус, но не переключается
клавишами/мышью и не вызывает Click; программная загрузка значения разрешена.

Сохраняется приоритет local/binding → Style элемента → стиль View → Theme → default.
Явный BorderColor переопределяет themed-границу; независимые маркеры ошибки/правки
и внутренний focus-контур остаются. Смена темы обновляет существующие контролы,
сохраняя draft, фокус, выделение и прокрутку. Цвет не является единственным
обозначением ошибки, оси или доступного действия.

Краткий success flash, busy/pending и анимации цвета могут добавляться отдельными
задачами с явным событием завершения и доступным текстом. В этом срезе нет таймеров,
мигания, автоматического Apply и постоянной перерисовки неподвижных контролов.

## Плотность и формы

Компактный инспектор использует строку ввода 26 logical pixels, текст 12 и подписи 11.
Подпись, префикс и строка ввода центрированы по высоте. TextBox использует полный
измеренный line box; при компактной fixed Height вертикальные отступы уступают место
строке с сохранением top/bottom пропорции. Padding задаёт позицию, вертикальный clip
идёт по внутренней границе border, чтобы не срезать glyph ink/AA края выделения.
Горизонтальный clip сохраняет отступы и исключает validation badge. Natural height
остаётся line+Padding; заведомо слишком низкий fixed Height ограничен внешними Bounds.
Подписи выровнены общей
колонкой; числовые компоненты вектора имеют равную ширину. На узкой панели подпись
переходит над тройкой полей; resize не заменяет редакторы. Ошибка не сдвигает подпись
относительно основной строки ввода. Справа рисуется сплошной квадрат Theme.Error
со стороной в высоту поля; на нём центрирован символ14px цвета Theme.OnError.
Узкое поле ограничивает блок своими границами. Текст, caret, selection и focus-контур
заканчиваются перед блоком. RichText auto-height учитывает его ширину при переносе.
NumberBox принимает HasError/ErrorMessage на себе; внутренний Editor может явно
переопределить состояние через local/style. Hover по всему блоку через500мс показывает точный
ErrorMessage с переносом и прокруткой длинных сообщений. Inline error rows под
текстовыми/числовыми полями убраны. Общая копируемая панель Diagnostics остаётся
для полного адреса поля и keyboard/copy workflow, без изменения самого draft.

Transform имеет три строки: Position XYZ, Rotation Pitch/Yaw/Roll (радианы), Scale XYZ.
С2026-11-08 справа у каждой Transform группы есть reset icon24×24: доступ по
Tab + Enter/Space, Tooltip500мс и accessible name с inheritance/schema смыслом.
При narrow layout shared label переносится над равными XYZ колонками, action остаётся
в строке. Кнопка недоступна без local/draft values, для уже staged reset, при numeric
drag, read-only/busy/conflict. Чистые inherited fields не меняются; local values/drafts
помечаются reset. Linked Scale сбрасывается без proportional пересчёта, preference
сохраняется. Сведения о происхождении/идентичности и отдельные field resets остаются
в «⋯». Ошибки видимы и при закрытых сведениях. Сброс — явный draft через общий
Apply/Cancel и историю документа; canonical defaults0/1 не подставляются поверх наследования.

`NumberBox` хранит ввод как строку во вложенном TextBox: пустая строка, минус и
промежуточные значения не нормализуются автоматически. `Vector3Editor` компонует
три таких редактора; он не знает о Transform, движке или транзакциях. Конкретный
`TransformEditor` живёт в Aether.Editor и привязывается по точному типу/версии схемы.
Остальные компоненты продолжают использовать общий инспектор.

### Остальные редакторы инспектора — 2026-11-05

Все schema-defined Number/Integer/Boolean/String используют общий PropertyEditorRow:
колонка подписи 88, значение справа, nullable switch рядом со значением. На узкой
панели подпись поднимается над строкой значения; длинная подпись/поле не выталкивают
остальные контролы за доступные границы. Числовое поле имеет preferred cap120,
логическое96; это logical pixels. Ввод чисел идёт через NumberBox без префикса,
boolean подписан «Да»/«Нет», текст использует оставшуюся ширину. Все роли цветов
и приоритеты состояний остаются общими с Transform.

Строка сохраняет один RichTextEditor и его выделение/history/scroll; одно- и
многострочное содержимое не меняют тип контрола. В инспекторе content height
автоматически измеряется от18до70 плюс padding4 (итог26–78), включая переносы
длинных строк по реальной ширине. Более длинный текст прокручивается внутри поля.
Общие defaults RichTextEditor вне этого стиля сохраняются.

Nullable имеет явный переключатель «null». При null редактор значения скрыт,
сохраняет свой draft, а строка показывает «Не задано» высотой26. Это отличается
от пустой строки и от числа0. Снятие null возвращает тот же контрол и прежний
draft. Pending reset и временная недоступность не становятся null и не очищают
данные. Сведения и reset раскрываются ниже поля на полной ширине; ошибки всегда
доступны, включая полный копируемый текст диагностики. Никаких новых schema kinds,
reference picker или enum contracts этот срез не вводит.

Переключение градусов/радиан реализовано2026-11-08 в ED4g.4, групповой сброс — ED4g.3.
Под Rotation находится retained checkbox «Углы в градусах», default off/radians.
Label/accessible names/step/adjustment limits следуют display units; шаг0.01rad/1°,
Scale сохраняет0.01. Canonical authoring/wire остаются radians, switch не создаёт
draft/dirty/history и не переписывает canonical text. Degrees не wrap/clamp-ятся.
Invalid/incomplete rotation text (также pending reset) запрещает switch до исправления/
Cancel/Apply reset; unrelated invalid field не мешает. Active drag/read-only/busy/conflict
также запрещают смену единиц. Валидный текст обеих форм сохраняется для exact обратного
switch; gesture cancellation возвращает отдельный display snapshot и canonical draft.
Preference сохраняется при selection/history/Cancel/theme; controls не перестраиваются.
Numeric drag/step и связанный масштаб описаны ниже; явный Apply, Ctrl+Enter,
Cancel/Esc и существующий Undo/Redo сохраняются.

<a id="numeric-gestures"></a>
## Числовые жесты и связанный масштаб — 2026-11-05

В обычном текстовом режиме NumberBox сохраняет любую промежуточную строку.
Opt-in AdjustmentEnabled добавляет geometric drag marker для безымянного числа;
для вектора handle — осевой префикс. ↑/↓ на focused editor меняют число шагом,
4logical pixels горизонтального drag дают один шаг. Нулевое смещение не нормализует
исходную строку. Shift увеличивает шаг×10, Ctrl уменьшает×0.1; целые имеют минимум1.
В Inspector Real step0.1, Integer1, Rotation0.01rad/1°, Scale0.01. Колесо оставлено прокрутке.
Invalid/overflow/границы не заменяются нулём и не clamp-ятся. Текстовый draft
нормализуется только явным adjustment или обычным application commit.

Во время drag controls держат capture и borrowed input context до terminal event.
Release завершает gesture и сохраняет draft; это не Apply. Esc отменяет сначала
только gesture, включая предыдущие групповые scale drafts; следующий Esc — весь
inspector draft. Focus/capture loss, скрытие, disable/read-only и detach отменяют
gesture. Внешний canonical value или устаревшая selection/document identity дают
Interrupted без возврата старой строки. Capture освобождается до callbacks/rollback;
timers, cursor warp и постоянные idle frames не вводятся.

Scale link — явная подпись «Сохранять пропорции XYZ» под тремя Transform rows.
Он выключен по умолчанию, сохраняет различия XYZ и не меняет документ сам по себе.
Например2/3/5 → изменение X до4 даёт4/6/10. Работает для typing, step и drag;
invalid source остаётся локальным, targets проверяются до обновления. Overflow или
незавершённый reset target блокирует Apply и показывает диагностику. Один Apply
остаётся одной общей Undo transaction; preview обновляется после application commit.

## Типографика по умолчанию — 2026-11-08

По запросу пользователя default для controls/text — FontDefaults.Family:
Segoe UI Variable Text. Основной текст regular400, заголовки/осмысленные акценты
semibold600 через scoped IViewStyle. Family, размер, weight и italic остаются
независимыми параметрами; local/binding → element style → view style → Theme →
default сохраняется. Цветовая смена Theme в Editor не сбрасывает typography.

Parameterless Skia policy: requested family → preferred → Segoe UI → system
default. Приложение может добавить свой file fallback либо выбрать file-only
catalog. Системные шрифты используются из ОС, не копируются в пакеты. Measure,
draw, ellipsis/wrap и caret используют одинаковые параметры. Для variable fonts
не подразумевается автоматический opsz; italic зависит от доступных faces.
Детальный task/evidence — [PA8c.3b](PLAN.md#pa8c3b-default-fonts).

## Камера и выбор в viewport — контракт ED5a.0, 2026-11-08

Прикладной [camera/selection contract](../engine/EDITOR_CAMERA_SELECTION.md#ed5a-view-contract-v1)
принадлежит Editor/engine. Для будущего viewport controls/menu/popup/chrome и чужой
capture имеют input priority; backdrop transparency не делает их click-through.
Navigation сохраняет inspector draft, selection intent использует общий owner и
существующую tree policy. Камера/picking пока не реализованы; общие UI capabilities
добавлять по конкретной потребности ED5a consumer.

## Тени панелей и transient surfaces — 2026-11-08

Semantic panels используют Theme.PanelShadow через scoped View style; MenuPopup
по умолчанию использует Theme.MenuShadow. Layout-only containers не затеняются.
BoxShadow задаёт logical sigma/offset/color, local Shadow=null отключает эффект.
Тень рисуется снаружи rounded surface до её содержимого, ограничивается ancestor
clip, не расширяет layout/hit bounds и не темнит прозрачную середину/backdrop.
Opacity охватывает поверхность вместе с halo. Смена темы сохраняет дерево и ввод;
эффект не использует таймеров и не создаёт idle animation.

Validation tooltip — opaque readable surface с MenuShadow; не забирает focus или
capture. Уход/input/смена ошибки/hide/detach закрывают старое пояснение. Для общего
popup layer нужен OverlayHost. Dark/Light значения находятся в Theme; прикладные
RGB не дублируются. [Результаты UIA3d](HISTORY.md#validation-shadows-2026-11-08), [исправленный Vulkan backdrop](HISTORY.md#vulkan-backdrop-2026-11-08).
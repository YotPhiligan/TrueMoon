# RichTextEditor: план дальнейшего развития

Создан 2026-10-17 по запросу пользователя сохранить обсуждённые улучшения отдельно.
**Состояние: запланировано, работа отложена.** Вернуться при реализации других
функций UI-модуля и сверить зависимости с фактически доступными механизмами.
Сверка2026-10-19: ENG1/ENG2 и bounded UI INF завершены; следующий общий code шаг —
ENG3, начиная с compiler/artifact подэтапа в [engine плане](../engine/PLAN.md#eng3-уточнение-плана-перед-реализацией--2026-10-19).
Этот документ не запускает реализацию RTE.

[UI roadmap](PLAN.md), [текущий UI статус](STATUS.md),
[реализованный API и ограничения](RICH_TEXT_EDITOR.md),
[история исходной реализации](HISTORY.md#richtexteditor--2026-10-17).
Подробные задачи редактора ведутся только здесь; общий UI план ссылается на них.

## Цель и исходное состояние

Довести существующий control до удобного повседневного редактирования rich text:
предсказуемая история, привычное выделение и навигация, корректное состояние
toolbar, сохранение оформления и явное поведение на больших документах.

Исходная реализация PA7a: immutable RichTextDocument/Run/Style/EditorOptions,
multiline editor с grapheme selection, formatting, plain clipboard и bounded
snapshot history. Topten.RichTextKit0.6.170 используется внутри Skia adapter;
Argentis остаётся независимым от Skia/Alloy. Есть отдельный demo toolbar.

Предыдущие проверки реализации 2026-10-17: UI563/563 отдельно Debug/Release,
6native demo cases,40package processes и360прежних Settings golden comparisons.
Это историческая исходная точка, не проверки улучшений из данного плана.
В текущем документационном шаге код, build/tests/native runs не выполняются.

## Границы и решения перед реализацией

- Сохранить net10.0/xUnit, owner-thread и явное владение деревом/layout/resources.
  Нейтральные документы и публичные UI contracts остаются в Argentis;
  platform input и Skia/RichTextKit details остаются в адаптерах.
- Toolbar остаётся отдельной композицией. Единое состояние и команды editor
  должны подходить toolbar, shortcuts и будущему context menu.
- Новые имена API ниже — предложения. Перед каждым этапом уточнять совместимость,
  binding/notifications, read-only/disabled semantics и критерии проверки.
- В RTE0 сравнить текущий адаптер с возможностями RichTextKit Editor.TextDocument:
  navigation, selection ranges, edit grouping и composition. Применимость к нашей
  immutable модели и lifetime требует проверки; переход на TextDocument не принят.
  Версию библиотеки и доступные API повторно сверить при возобновлении работы.
- Исходные K1–K9/PA1–PA10, physical DPI, K7, real loss и VRAM ограничения
  сохраняются. Этот план не закрывает alpha/PA7/beta и не блокирует ENG1.

## Этапы

Все этапы ниже **запланированы и отложены**. При возобновлении выбирать один
ограниченный этап, записывать его реализацию и фактические проверки в UI STATUS/
HISTORY. Предлагаемый первый проход: RTE1–RTE3, с отдельной проверкой каждого этапа.

| ID | Результат | Порядок и зависимость |
| --- | --- | --- |
| RTE0 | Сверка исходников, требований consumer и возможностей RichTextKit; уточнение выбранного этапа | Первый шаг при возвращении, до изменения API |
| RTE1 | Удобное редактирование, выделение и история | Первый приоритет после RTE0 |
| RTE2 | Caret, viewport и прокрутка | Вместе с развитием общих UI cursor/scroll/scheduler механизмов |
| RTE3 | Полное состояние форматирования и команды | Согласовать с PA3/toolbar/command механизмами |
| RTE4 | Сохранение документа и rich clipboard | После уточнения document schema и clipboard capabilities |
| RTE5 | Абзацы и поиск/замена | После модели документа RTE4; отдельно от списков/таблиц |
| RTE6 | Измерения больших документов и обоснованные оптимизации | Вместе с PA10; до заявления supported document limits |
| RTE7 | IME, bidi caret/navigation и accessibility | Отдельные подэтапы вместе с PA7/platform integration |

### RTE0. Подготовка к возвращению

- Прочитать этот документ, актуальные UI PLAN/STATUS и API guide; сверить
  реализацию и предыдущие failures с текущими исходниками.
- Выбрать реальные сценарии consumer: размер/языки документа, нужные команды,
  формат сохранения и требуемая платформенная интеграция.
- Составить карту уже существующих command, cursor, scroll, timer, clipboard и
  semantic/input contracts; назначить общим механизмам владельцев в UI плане.
- Проверить совместимость используемых возможностей RichTextKit с UTF-16 public
  indices, grapheme boundaries, immutable snapshots и ownership TrueMoon.

Готовность: выбран следующий ограниченный этап, его API/зависимости и acceptance
записаны явно; перенос editing state между слоями требует отдельного решения.

### RTE1. Редактирование и история

- Объединять последовательный набор и последовательное удаление в Undo-группы.
  Границы: перемещение caret/selection, направление удаления, смена форматирования,
  paste, отдельная команда, focus change и согласованный интервал бездействия.
- Добавить edit transaction для нескольких programmatic изменений одним Undo;
  определить вложенность и поведение при исключении без обещания rollback заранее.
- Добавить двойной клик по слову, тройной по абзацу, Ctrl+Backspace/Delete,
  PageUp/PageDown и соответствующее расширение selection с Shift.
- Уточнить границы слов с пунктуацией и Unicode; текущая whitespace-only политика
  становится явной исходной реализацией для сравнения.

Готовность: набор слова отменяется одной операцией; paste/formatting отделены;
undo/redo восстанавливают текст, styles и selection; новая edit ветка удаляет redo.
Проверить punctuation/Unicode, document boundaries, read-only и disabled states.

### RTE2. Caret и viewport

- Добавить мигание caret с настройками и сбросом при вводе/навигации;
  timer прекращает работу при focus loss/detach/dispose и не удерживает editor.
- Подключить текстовый mouse cursor через общий platform contract.
- Добавить autoscroll при drag selection за границами viewport, включая
  горизонтальное направление при WordWrap=false и остановку при capture loss.
- Подключить видимые scrollbars и горизонтальную прокрутку; сохранить согласованность
  offsets/caret при resize, wrap, theme/font changes и изменении документа.

Готовность: selection можно продолжить за экран; capture/focus cancellation
останавливает drag/autoscroll. Idle unfocused editor не создаёт timer/redraw work;
focused blink перерисовывает с согласованной частотой и корректным cleanup.

### RTE3. Форматирование и команды

- Разделить стиль следующего ввода и сводное форматирование всего выделения.
  Предлагаемый SelectionFormatting отражает on/off/mixed для Boolean styles и
  mixed/uniform для font/size/color; mixed отличается от наследования defaults.
- Добавить ClearFormatting и сброс отдельных attributes к наследованию.
  В текущем patch API null означает «оставить значение»; определить отдельный
  способ reset без неоднозначности и потери совместимости.
- Ввести общее состояние доступности/выполнения editor commands для toolbar,
  shortcuts и context menu. Согласовать уведомления с document/history/selection.

Готовность: смешанное выделение правильно отображается toolbar; toggle имеет
определённое поведение для mixed; reset восстанавливает наследование, поддерживает
undo/redo и корректно реагирует на изменение editor defaults. Toolbar сохраняет
выделение и уважает read-only/disabled, подписки освобождаются с владельцем.

### RTE4. Сохранение и rich clipboard

- Спроектировать версионируемый JSON document codec отдельно от UI control:
  текст, styles/наследование, будущие paragraph attributes, правила совместимости.
  Определить validation/limits и поведение неизвестных или повреждённых данных.
- Добавить собственный clipboard format вместе с обычным Unicode text fallback;
  определить paste-as-plain-text и историю одной paste операции.
- Уточнить revision/saved-state notifications для consumer; файловый путь,
  autosave и storage policy остаются у приложения.

Готовность: round-trip сохраняет текст и оформление; повреждённые данные не
меняют документ частично. Между нашими editor сохраняются styles, внешнему
приложению доступен plain text; clipboard failure не теряет выделение/содержимое.

### RTE5. Абзацы и поиск

- Ввести paragraph attributes: выравнивание отдельных абзацев, отступы,
  интервалы before/after. Определить наследование и split/merge при Enter/Delete.
- Добавить поиск/замену с case/whole-word options, next/previous match и highlight.
  Определить Unicode matching и сохранение formatting при replacement.

Готовность: paragraph formatting и split/merge сохраняются в codec/clipboard/
history; поиск не меняет документ, выбранный результат виден, replace-all является
одной определённой Undo-операцией, highlights обновляются после edits.
Lists/tables, HTML/RTF/Markdown import/export и spellcheck требуют отдельного scope.

### RTE6. Большие документы

- Выбрать повторяемые workloads по объёму текста, числу runs/paragraphs и Unicode.
  Измерить input, wrap/resize, navigation, scroll, undo/redo и repeated sessions.
- Отдельно фиксировать CPU/allocations, managed history memory, native shaping
  caches/resources и GPU work; согласовать supported limits и бюджеты по замерам.
- По результатам выбирать paragraph layout reuse, cached grapheme/offset indices,
  visible-range drawing и history memory budget. Изменения сравнивать с baseline.

Готовность: воспроизводимы до/после measurements, поведение/пиксели сохранены,
прежние source fixtures не перезаписаны. Process-wide RichTextKit caches не
объявляются освобождёнными при session Dispose; long resource trend проверяется отдельно.

### RTE7. Платформенный ввод и доступность

- IME: composition start/update/commit/cancel, replacement range, candidate
  placement, focus/lifetime и единая Undo-операция committed composition.
- Bidi: визуальные caret positions/affinity и selection/navigation в mixed RTL/LTR
  с сохранением нейтральных публичных индексов.
- Accessibility: semantic text/selection/read-only state, text navigation и
  notifications через общие UI/platform contracts.

Готовность: каждый подэтап имеет собственный platform proof на согласованном
окружении. Проверка вызовов adapter не заменяет ввод реальным IME или проверку
screen reader; отсутствие среды остаётся явно записанным ограничением.

## Общая проверка изменений

Для реализуемого этапа: содержательные xUnit regressions поведения и ошибок,
cleanup/binding/focus/selection, raster pixels по необходимости и проверка demo
на raster/OpenGL/Vulkan в согласованных configurations. Изменение native/API/
package delivery требует соответствующего fresh consumer proof. Прежние Settings
golden fixtures/tolerances сохранять; новые editor fixtures добавлять явно.

В UI STATUS записывать дату, выполненные команды, результаты и следующий этап;
подробные runs/failures хранить в HISTORY. Одни только документы, наличие API или
результаты PA7a не считаются успешными проверками этих улучшений.

# TrueMoon.Aether: состояние движка и редактора

Последняя работа — **2026-10-27, ENG8a.3/ENE6d.4 child diagnostic delivery**.
Штатные App factories передают bounded structured logs по отдельному Enerit
endpoint; options копируются в launch, receiver живёт до reap/raw drain.
Host сохраняет Origin/PID/time/trace и добавляет authoritative session context.
Known loss и unknown ACK outcome различаются; replay нет. Local JSONL и raw
stdout/stderr продолжают работать независимо.
[Контракт/API](DIAGNOSTICS_DELIVERY.md),
[матрица/команды и неуспешные попытки](HISTORY.md#child-diagnostic-delivery--2026-10-27).

Новые120Aether cases плюс59Core. Aether1338/1338, Editor741/741, full solution
4285/4285 Debug/Release, failed/skipped0. Builds0errors:
Debug nonincremental1546warnings, Release incremental1316warnings, SDK10.0.403.
[Evidence](../../TestResults/DiagnosticsDelivery-20261027-1/verification.json).
Source proof:3real Preview children одной session,99control commands при log flood,
slow/broken host sinks, bounded local JSONL/raw, все processes reaped/resources released.
Это не gameplay/input/GPU/display/published/package/performance acceptance.

Короткий ordinary fixture cleanup deadline исправлен в тесте; production budgets
не менялись. Прежний CPU FrameReceiver failure не повторился, ENG2 follow-up открыт;
Mithril ready-file race также не объявлен исправленным.
Далее ED6a/ED8a console/export, затем command/frame metrics. Authoring очередь
ED4c.2 → ED4c.3/workflow и прежние hardware gates сохраняются.
Предыдущие [IPC traces](HISTORY.md#ipc-trace-context-в-control-factories--2026-10-26),
[operations](HISTORY.md#операции-и-трассы-диагностики--2026-10-26),
[build retention](HISTORY.md#диагностика-сборок-ограничение-хранения--2026-10-26)
и [managed runs](HISTORY.md#managed-logger-runs-в-editor-и-runtime--2026-10-26)
сохраняют отдельные budgets; общего artifact quota нет.

Предыдущая работа — **2026-10-25, ED4f.2 системная граница окна**.
Editor включает UseSystemAccentBorder: Windows accent, theme fallback при отсутствии
цвета, RGB60% при inactive и focus transition160мс. Style/local BorderColor имеет
приоритет; cached цвет обновляется по native notifications без чтения на frame.
15новыхcases, narrow67/67. Related UI893/Hosting114/Editor741 Debug/Release:
1748/configuration,3496всего,failed/skipped0. Nonincremental Debug solution build
0errors/1532warnings.12native cases/206PNG/16reaped children,validation0/0 и zero
owned UI/GPU/mappings; actual active/inactive border pixels проверены, PNG просмотрены.
[Проверки и границы](HISTORY.md#ed4f2-системная-граница-окна--2026-10-25).
Evidence: TestResults/AccentBorder-20261024/verification.json. Aether/full solution
tests/packages/physical/performance не повторены. Реальная смена Windows accent
не выполнялась; очередь ED4c.2 names/order → ED4c.3 create/delete прежняя.

Предыдущая работа — **2026-10-24, ED4e островки поверх viewport**.
Обозреватель/инспектор закреплены над полной рабочей областью сцены; отступ16,
скругление8, мягкая тень, content height/scroll и независимый Theme.PanelBackdrop.
Контекстный инспектор сохраняет View и появляется/скрывается за160мс без resize
viewport; closing сразу закрывает ввод. Dark/Light/null fallback проверены.
67новыхcases; narrow103/103. Related UI842/Hosting98/Editor699 Debug/Release:
1639/configuration,3278всего,failed/skipped0. Nonincremental Debug solution build
0errors/1532warnings.12native cases/158PNG/16reaped children,validation0/0 и zero
owned UI/GPU/mappings; финальные native PNG просмотрены.
[Проверки и границы](HISTORY.md#ed4e-островки-поверх-viewport--2026-10-24).
Evidence: TestResults/Islands-20261024/verification.json. Aether/full solution tests,
packages/physical/performance не повторены. Свободные drag/resize/docking отдельно.
Следующий engine шаг прежний: ED4c.2 names/order, затем ED4c.3 create/delete.

Предыдущая работа — **2026-10-24, ED4d.1 компактные действия и контекст инспектора**.
Иконки26/reset24, Tooltip500мс (включая disabled), непрозрачный фон обеих тем;
без выбора скрыта вся колонка, ширина передана viewport. Esc в обозревателе
отменяет drag, затем снимает выбор; повторный выбор сохраняет View/chrome.
25новыхcases, narrow181/181. Текущие UI801/Hosting98/Editor673 Debug/Release:
1572/configuration,3144всего,failed/skipped0. Nonincremental Debug solution build
0errors/1532warnings.12native cases/152PNG/16reaped children,validation0/0,
zero owned UI/GPU/mappings; финальные PNG просмотрены.
[Проверки и границы](HISTORY.md#ed4d1-компактные-действия-и-контекст-инспектора--2026-10-24).
Evidence: TestResults/InspectorChrome-20261024/verification.json. Aether/full solution
tests/прочие модули/packages/physical/performance в этом уточнении не повторены.
Далее ED4c.2 имена/порядок, ED4c.3 create/delete/duplicate и контекст создания;
прежние CPU receiver/Mithril issues остаются открытыми.

Предыдущая работа — **2026-10-24, ED4d динамический инспектор компонентов**.
Schema-driven секции только фактических компонентов: Boolean/Int64/Number/String,
nullable, provenance/reset, Add/Remove/Restore; единые drafts/history/preview gates.
Общий ComponentEditBatch использует прежние revision/receipts/resolver/Undo/Redo и
scene JSON v1; definition catalogs не мутируются. Retained View/Data/Style,
App/Cobalt/IFactory и compatibility consumers проверены. Live legacy — read-only.
163новыхcases; текущие Aether1142/UI801/Hosting98/Editor648 Debug/Release
=2689/configuration,5378всего,failed/skipped0. Nonincremental Debug solution build:
0errors/1532warnings.12native source Debug/published Release cases,146PNG,
16reaped children,validation0/0 и zero owned UI/GPU/mappings. Финальные PNG просмотрены.
[Проверки и границы](HISTORY.md#ed4d-динамический-инспектор-компонентов--2026-10-24).
Evidence: TestResults/Inspector-20261024/verification.json. Следом ED4c.2 имена/
порядок, затем ED4c.3 create/delete/duplicate. Runtime ECS World и custom editor
providers остаются отдельными задачами. Full solution tests/прочие модули/packages/
physical/performance не повторены; прежние CPU receiver/Mithril issues открыты.

Предыдущая работа — **2026-10-24, ED4c.1 дерево сущностей и Reparent**.
Общий TreeView с иконками/раскрытием/выбором, переносом мышью и клавиатурой;
Editor View/Data/Style и App/Cobalt/typed factory. Смена родителя сохраняет local
Transform/inheritance, использует общий atomic authoring API/history, revision,
Save/reopen и Edit Preview. IO/runtime/Transform draft и устаревшие жесты закрыты.
130новых cases; текущие Aether1050/UI801/Hosting98/Editor577 Debug/Release
=2526/configuration,failed/skipped0. Nonincremental Debug solution build:
0errors/1532warnings.10native source Debug/published Release cases,118PNG,
14reaped children,validation0/0 и zero owned UI/GPU/mappings. PNG просмотрены.
[Точные проверки и границы](HISTORY.md#ed4c1-дерево-сущностей-и-reparent--2026-10-24).
Evidence: TestResults/Hierarchy-20261024/verification.json.
Следом ED4c.2 — сохраняемые имена/порядок соседей, затем ED4c.3 create/delete/duplicate.
Full solution tests/packages/physical/performance не повторены; прежние CPU receiver/
Mithril fixture issues открыты. Первый срез не включает rename/reorder/multiselect.

Предыдущая работа — **2026-10-24, ED4b выравнивание статусбара**.
Удалена пустая полоса16 под footer: общий WindowFrame размещает grip16 поверх
правого нижнего угла. StatusBar28 и его контейнер имеют одну высоту, нижний край
совпадает с внутренним бордером; details не перекрывается grip. Resize, scale1/1.5/2,
maximize/restore/theme/idle проверены. UI760/Hosting98/Editor530 Debug/Release
=1388/configuration,failed/skipped0; narrow57/57.2native empty cases source Debug/
published Release,20PNG,zero owned resources; Dark/Light screenshots просмотрены.
[Проверки и границы](HISTORY.md#ed4b-выравнивание-статусбара--2026-10-24).
Evidence: TestResults/StatusBarAlignment-20261024/verification.json.
Full solution/headless/игровые native profiles/physical input не повторены;
прежние CPU receiver/Mithril fixture issues открыты. Следом create/delete.

Предыдущая работа — **2026-10-24, ED4b компактная строка состояния**.
Краткий status с иконкой и контекстной отменой слева; справа objects/Edit Preview/
details. Tick/PID, actual preview revision и выбираемые ошибки — в матовом popup.
Edit Preview start/retry/stop перенесены из production inspector в footer;
активный preview показывает «Редактирование сцены», игровые actions — в header.
Responsive overflow/focus/theme/dismissal, retained View/Data/Style и
App/Cobalt/typed factories с обоими DI providers проверены.

Текущие UI759/Hosting98/Editor527 Debug/Release =1384/configuration,80новыхcases,
failed/skipped0. Nonincremental Debug solution build0errors/1532warnings.
Source Debug/published Release вне repo:6native empty/legacy/Transform cases,
68PNG/12reaped children; preview через footer, Undo/Redo/Save/reopen, header
regressions,validation0/0 и zero UI/mapping/GPU ownership. PNG просмотрены.
[План](EDITOR_PLAN.md#ed4b-компактная-строка-состояния--2026-10-24),
[проверки/границы](HISTORY.md#ed4b-компактная-строка-состояния--2026-10-24).
Evidence: TestResults/StatusBar-20261024/verification.json.
Full solution tests/Aether/headless/packages/physical/performance не повторены;
прежние CPU receiver/Mithril fixture issues открыты. Следом create/delete.

Предыдущая работа — **2026-10-24, матовый фон dropdown меню ED4a**.
Editor использует общий BackdropMaterial/Theme.MenuBackdrop из Argentis/Skia:
blur фона текущего окна + tint, текст/иконки чёткие. Dark/Light/null, fallback,
retained popup/focus и прежние команды сохранены, новый прикладной сервис не нужен.
Текущие UI739/Hosting98/Editor467 Debug/Release =1304/configuration,failed/skipped0.
4native cases source Debug/published Release вне repo,28PNG/4reaped children,
validation0/0 и UI sessions/mappings/GPU resources/handles0. File/View PNG
просмотрены, full solution/Aether/headless/прочие native profiles не повторялись.
[Результаты](HISTORY.md#ed4a-матовый-фон-меню--2026-10-24),
[общий механизм и границы](../alloy/PLAN.md#pa8b-матовые-поверхности--2026-10-24).
Evidence: TestResults/Backdrop-20261024/verification.json. Следом create/delete;
прежние CPU receiver/Mithril fixture issues и hardware gates остаются открытыми.

Предыдущая работа — **2026-10-24, ED4a знак TrueMoon и анимированная кнопка меню**.
Слева фиксированный знак TrueMoon, после него кнопка без обычной рамки. Точки
поворачиваются ⋮→⋯ вместе с раскрытием; hover показывает группы, click/Enter/Space
после анимации открывает «Файл». Повторный click закрывает, отмена/detach исключают
late popup. Знак, hit area кнопки и simulation/window actions неподвижны;
caption сдвигается. Retained View/Style, App/Cobalt/IFactory и ownership прежние.
[Дизайн/план](EDITOR_PLAN.md#ed4a-компактная-верхняя-панель--2026-10-23),
[проверки и границы](HISTORY.md#ed4a-знак-truemoon-и-анимированное-раскрытие--2026-10-24).

Текущие UI723/Hosting98/Editor467 Debug/Release:1288/configuration,13новыхcases,
failed/skipped0. Source Debug/published Release вне repo:4native cases/28PNG/
4reaped children; brand/disclosure/hover/keyboard/theme/tooltip/FocusLost и legacy
game commands, validation0/0 и UI sessions/mappings/GPU resources/handles0.
Финальные collapsed/File screenshots просмотрены. Evidence:
TestResults/HeaderBrand-20261024/verification.json. Full solution/Aether/headless/
остальные native profiles не повторены; прежние CPU FrameReceiver и Mithril
fixture failures не объявлены исправленными. Далее create/delete общего API/history.

Предыдущая работа — **2026-10-23, ED4a компактный заголовок и меню**.
⋯ раскрывает пять menu groups с анимацией180мс/leave grace240мс; название сдвигается,
▶/пауза/стоп и стандартные оконные кнопки неподвижны. Общие Menu/Command/Overlay/
frame transition contracts — в Alloy/Argentis. Header View/Style и UI-owner typed
factory подключены через App/Cobalt; окно владеет View, модели/style borrowed.
File/edit/build/simulation/theme используют общие availability/actions. ▶ —
tracked start→play/resume, stop/cancel/close исключают late Play и drains work.
Header shortcuts, path draft, unsaved decision и независимый Edit Preview сохранены.
[Дизайн/план](EDITOR_PLAN.md#ed4a-компактная-верхняя-панель--2026-10-23),
[руководство](../../TrueMoon.Aether.Editor/README.md#компактная-верхняя-панель),
[все проверки и границы](HISTORY.md#ed4a-компактный-заголовок-и-меню--2026-10-23).

Related UI712/Hosting98/Editor465 Debug/Release:1275/configuration,105новыхcases,
failed/skipped0. Source Debug/published Release вне repo:10native/header cases,
64PNG/12reaped children; real menu/icon/document/Transform/preview/exit workflows,
validation0/0 и UI sessions/mappings/GPU resources/handles0.10headless cases отдельно.
Последний full Debug3048/3049 (Mithril ready-file fixture IOException), Release3049/3049;
Aether1008/1008 и related UI прошли. Nonincremental build0errors/1532warnings.
Evidence: TestResults/MenuHeader-20261023/verification.json. Общий green не заявлен.
Ранний CPU FrameReceiver null сохранён отдельной ENG2 quality задачей; повторные
passing не означают его исправление. Fresh packages/physical/full alpha не повторены.

Предыдущий ED3a Open→Transform→Preview→Undo/Redo→Save→reopen реализован и повторён
в новых native workflows. Atomic drafts/history, finite/positive validation,
conflict и latest-only preview coordinator остаются прежними;
[подробности ED3a](HISTORY.md#ed3a-transformhistorypreview-в-ui--2026-10-23).

## Границы приложения и engine

ED1a выбирает один проект через explicit .aetherproj; без него пустая workspace,
legacy .csproj сохраняет игровой demo workflow. Project picker/switch UI,
templates/wizard/recent/preferences ещё нужны. ED2a/b .aetherscene schema1≤4MiB/
depth32; unknown versions отклоняются без migration, resolved/runtime/history не
сохраняются. Atomic store использует SHA256 guard и sidecar для cooperating writers;
arbitrary external rename, links sandbox и power-loss durability не гарантируются.

Document actions доступны при stopped/idle game/compiler или Edit preview; preview
останавливается до document replacement/close. Open document по-прежнему блокирует
Build/Play/Replace до ED6 identity/workflow integration. New создаёт пустую сцену.
Create/delete/reparent/generic mutations и полный ARCH1b открыты. Общая project/runtime
Switch/Close transaction остаётся ED1/ED6; ED2 document Close/Switch/window Exit profile
выполнен. Terminal shutdown forced discard/drain; native user close vetoable.

Preview GPU-only Windows x64/OpenGL UI/Vulkan child, fixed camera,640×360,≤64entities,
новый child при обновлении revision. Authoring hierarchy/properties не обрезаются
этим лимитом; preflight rejection сохраняет документ. Build/Play/Game.Logic для
preview не нужны. Frames не идут через IPC; Enerit — control, Mithril — процессы.
Failed cleanup удерживает ownership для retry; arbitrary driver/dispatcher hang
не прерывается deadline. API experimental, публикации не было. Physical DPI/K3/K7/
K8/VRAM/final alpha открыты; NVIDIA workaround search остановлен по решению пользователя.

## Следующий конкретный шаг

В текущем diagnostics направлении — ED6a/ED8a bounded console/export в Editor
поверх Core reader и child delivery: retained rows, фильтры source/level/session,
явные loss indicators и bounded export без rebuilding View tree. Затем command/frame
metrics; UI/data/style/IFactory ownership остаётся штатным для Editor.
Следующая authoring задача по прежней очереди:

ED4c.2: сохраняемые имена и порядок соседей через общие engine metadata/формат и
authoring history; затем inline rename и before/after reorder в существующем дереве.
Следом ED4c.3 create/delete/duplicate: New → добавить объект → изменить Transform →
удалить/Undo → Save/reopen. Сначала headless atomic/revision/identity/validation proof;
не создавать UI-only мутаций или второй history.
Детали — [ED4c](EDITOR_PLAN.md#ed4c-обозреватель-сущностей--начат-2026-10-24).
Нестабильный CPU FrameReceiver test имеет отдельный ENG2 follow-up; Mithril fixture
ready-file race — отдельную quality задачу. Полный World/ECS не prerequisite.
После create/delete — project picker/switch и ED1/ED6 document/build/play workflow.
# TrueMoon.Aether: состояние движка и редактора

Последняя работа — **2026-10-24, ED4b компактная строка состояния**.
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

Минимальные create/delete entities через общий authoring API/history, затем UI
команды: New → добавить объект → изменить Transform → удалить/Undo → Save/reopen.
Сначала headless atomic/revision/identity/validation proof; не создавать UI-only
мутаций или второй history. Детали — [ED3](EDITOR_PLAN.md#ed3-общие-операции-редактирования-и-undoredo).
Нестабильный CPU FrameReceiver test имеет отдельный ENG2 follow-up; Mithril fixture
ready-file race — отдельную quality задачу. Полный World/ECS не prerequisite.
После create/delete — project picker/switch и ED1/ED6 document/build/play workflow.
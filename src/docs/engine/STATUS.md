# TrueMoon.Aether: состояние движка и редактора

Последняя работа — **2026-10-23, ED2b документы в обычном UI**.
New/Open/Save/SaveAs/Close используют ED2a document API. Панель показывает selected
file, dirty/activity/errors и сохраняет path drafts. Inline Save/Discard/Cancel
обслуживает scene New/Open/Close и native Exit: failed Save vetoes exit, Cancel
сохраняет сцену. Accepted IO и решения отслеживаются, forced shutdown drains без
ожидания пользователя; throwing cancellation callbacks и cleanup retry наблюдаемы.
Nested View/Data/Style и typed factories подключены через App/Cobalt; ownership и
Microsoft DI проверены. [Контракт](EDITOR_PLAN.md#ed2b-document-commands-и-ui--2026-10-23),
[UI guide](../../TrueMoon.Aether.Editor/README.md#работа-с-документом-в-ui-ed2b),
[exact tests/commands/evidence](HISTORY.md#ed2b-документы-в-обычном-ui--2026-10-23).

Workspace показывает raw authoring IDs/parents/definitions и resolved readonly
component properties без legacy64 projection. String controls безопасно показывают
escaped values, stable tree/path/selection сохраняются при Save/theme. Ctrl+S/
Shift+S/W и buttons используют одни actions; decision получает safe Cancel focus.
Общий modal overlay/menu runtime этим срезом не добавлен.

## Фактически выполненные проверки

- Новый focused набор **74/74 Debug**, failed/skipped0,42methods. Save/Discard/Cancel
  на New/Open/Close/Exit, real-store conflict/reopen, ignored IO/callback failure/
  forced upgrade/retry, owner notifications, bindings/style/focus/dispose, both
  providers,130entities без truncation. Assertion/gap review, без production mutations.
- Final full solution **2878/2878 Debug и2878/2878 Release**,10TRX/configuration,
  failed/skipped0: Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/
  UI661/Hosting85/Aether1008/Editor358. Nonincremental Debug build0errors/1531warnings.
- Source Debug/published Release вне repo: document UI по19directed actions/
  4PNG/5actual saved files, real text/button/shortcut/native close input.
  SaveAs/reopen, SHA256 external conflict/recovery, invalid first Save, invalid
  Open with Discard, Save/Discard/Cancel transitions и failed Exit Save проверены.
  Notifications на одном UI owner thread; original fixture неизменён, game/compiler0.
- Empty/project/legacy/documents native: **8cases/18PNG/4reaped children**,
  validation0/0 и UI sessions/mappings/GPU resources/handles0. Legacy по8commands/
  3PNG/2children. Scene/SDK headless10cases повторены;19consumer commands всего,
  включая6ожидаемых negative exits. Published app/project/CWD вне repo без demo DLL.
- Evidence — TestResults/DocumentsUi-20261023/verification.json и HISTORY;
  242solution source/45Editor source/8binary hashes и PNG проверяются.
  Editor non-packable; engine/runtime/wire/UI-library APIs/dependencies не менялись.
  Fresh packages, independent preview drawing, golden/RTE/full performance/physical
  UI/real loss/VRAM этим шагом не повторялись.

## Границы приложения и engine

ED1a выбирает один проект через explicit .aetherproj; без него пустая workspace,
legacy .csproj сохраняет игровой demo workflow. Strict manifest≤64KiB,
dotnet --version preflight без MSBuild, Create для существующей игры.
Project picker/switch UI, templates/wizard/recent/preferences ещё нужны.

ED2a/b .aetherscene schema1≤4MiB/depth32, bounded catalogs/patches/IDs/scalars;
unknown versions отклоняются без migration, resolved/runtime/history не сохраняются.
Dirty по saved ContentRevision учитывает Undo; captured Save допускает новые edits.
Paths lexical-contained в scenes root; trusted-local profile не sandbox поверх links.
Atomic rename/replace и SHA256 guard согласуют cooperating writers через sidecar;
arbitrary external rename между check/replace и power-loss durability не гарантируются.

Document UI actions доступны при stopped/idle runtime/compiler. Open authoring
document блокирует game actions до следующей preview/ED6 integration; inspector
остаётся read-only, New создаёт пустую сцену. Общая project/runtime Switch/Close
transaction остаётся ED1/ED6; ED2 document Close/Switch/window Exit profile выполнен.
Terminal App shutdown явно forced discard/drain; native user close vetoable.
Полный editable Transform/Undo/Redo/preview сценарий ещё не завершён.

ARCH1b.1 предоставляет headless atomic Transform/query/history; create/delete/
reparent/generic mutations открыты. Независимый
[engine Edit preview](PLAN.md#edit-preview-первый-engine-profile--2026-10-23)
готов отдельно: GPU-only Windows x64/OpenGL UI/Vulkan child, fixed camera,
exact presented runtime/surface/document revision, owned child replacement.
Build/Play/Game.Logic не нужны; game session не перехватывается.
Прежние native/package proofs сохраняют свой scope в [HISTORY](HISTORY.md).

Transport явный, automatic fallback нет; pixels не идут через IPC. UI owns retained
copies на context thread; borrowed dispatcher/context живут до cleanup.
Failed cleanup удерживает ownership для retry; arbitrary driver/dispatcher hang
deadline не прерывает. Real loss не инжектировался; PNG не доказывают physical
display color. API experimental, публикации не было. K3/K7/K8/VRAM/final alpha
открыты; NVIDIA workaround search остановлен, Windows/driver settings прежние.

## Следующий конкретный шаг

Подключить минимальный Transform inspector к общему authoring API: draft/commit/
cancel, numeric validation, atomic edit gesture, Undo/Redo и dirty checkpoint.
Затем показывать committed revision через independent Edit preview без Build/Play,
с fencing и сохранением документа при preview failure. Detail checklist —
[ED3–ED5 и первый срез](EDITOR_PLAN.md#первый-сквозной-срез).
Полный World/ECS не prerequisite. При возврате к gameplay — ENG5c.3 collisions
камеры, затем HUD; CPU Debug/reference allocation и full World/edit/MCP scope остаются.

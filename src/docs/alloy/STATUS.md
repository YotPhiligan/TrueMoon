# Alloy / Argentis: текущее состояние и точка продолжения

**2026-11-07 — SkiaSharp/native обновлены до4.155.0.**
DLL пересобрана из exact NuGet source commit с прежним Vulkan extension/ABI v1;
официальный version gate и TrueMoon exports проверены. Source Debug/published Release:
18readbacks/36GPU-композиций/backend comparison каждый, core+sync validation0/0.
Alloy1101/1101, Hosting154/154 и Editor1295/1295 каждый D/R. Fresh10pkg:36App+4direct processes
Debug/Release×build/publish на raster/OpenGL/Vulkan, native delivery/cleanup passed.
[Команды и scope](HISTORY.md#skiasharp-41550-2026-11-07).
[Текущая очередь](PLAN.md#current-ui-queue), [дизайн-код](DESIGN.md),
[прежний STATUS полностью](HISTORY.md#status-before-planning-revision-2026-11-05).

## Состояние реализации

UIA1 typed View/Data/scoped styles и App factories, UIA2 Window, UIA3 compact
NumberBox/Vector3Editor/PropertyEditorRow, RichText auto-height, themed CheckBox
и states реализованы. Numeric adjustment opt-in, draft/capture/cancel outcomes
и stable identity gates сохранены. Platform.Silk button/wheel использует ordered
cursor position и передаёт Shift/Ctrl. Прикладные Transform/history/units — Editor.

INF1–4/6/7 имеют реализацию и bounded checks; INF5 diagnostics остаётся частичным.
TreeView/menu/bindings/appearance/DPI существуют в указанных в HISTORY scopes;
наличие API не закрывает все beta/PA1–PA10, alpha/K1–K9 или физическую приёмку.

## Проверки готовности

Текущий run **2026-11-07**: native source build4.155.0 exit0, managed/native154→155
negative gate exit2, штатный native155 без extension exit2. Bundled DLL, source Debug
и published Release имеют совпадающий SHA; ABI48bytes, readback/layout return/redraw,
external composition и raster/OpenGL/Vulkan comparison прошли. All1101Alloy/154Hosting
cases прошли в каждой Debug/Release, failed/skipped0. Physical/K7/K8 scope не расширен.
Fresh NuGet matrix Completed=true:10packages,36App+4direct cases, exact package-only
graphs, new DLL SHA/delivery/fonts/behavior/cleanup и expected missing-analyzer control.
Initial failures stale direct graph guard и changed README сохранены, final source
hash audit прошёл на frozen snapshot. Это не новая physical Window/release matrix.
Editor1295/1295 D/R также passed; solution nonincremental Debug0errors/1555warnings,
1422distinct warning catalogue unchanged. Full solution/Aether tests не запускались.

Сохранённый run **2026-11-05**: full Alloy1101/1101 и Hosting154/154 каждый
Debug/Release, failed/skipped0,6новых pointer regression cases. Отдельно full
Editor1295/1295 D/R;4native OpenGL runs source Debug/published Release × standard/
Cobalt и embedded/MS DI. В standard Windows linked drag/release, Esc draft cancel,
Ctrl+Enter и Undo прошли; routed arrows/captured Esc — во всех. UI/GPU/handles0,
runtime children0. [Точное evidence](HISTORY.md#uia3c-native-numeric-2026-11-05).

Window delivery через fresh Editor NuGet consumer проверен4ноября; новые numeric
изменения не получили этим новый fresh-package proof. Прежние raster/OpenGL/Vulkan
goldens, Settings/scale/HUD и PA8a checks имеют собственные даты в архиве/HISTORY.
Они не являются текущим full hardware/release проходом.

## Блокеры и ограничения

- Numeric physical arrows/modifiers, Esc с удерживаемой кнопкой и OS pointer
  modifiers не подтверждены. Automation arrows поступали как UiKey.None; mixed
  down/key/up не предоставляется текущим automation API. Требования не отменены.
- K3: physical100/200% и разные DPI мониторов ещё требуют своего proof.
- K7: прежний240-window run failed, GDI23→137/USER26→26; trace локализовал screen
  HDC в NVIDIA ICD32.0.16.1742. Safe app-side fix не установлен, поиск workaround
  остановлен по решению пользователя. AMD raw control не равен full UI K7.
- K8 physical device/context loss, VRAM, resource trends и итоговая alpha matrix
  открыты. OS/driver settings и hardware при документационной ревизии не менялись.
- K9 имеет прежний managed baseline; новые rendering options требуют PA8c.0 и
  текущих измерений. Старые capture/Name-binding failures не объявлены объяснёнными.

## Следующий конкретный шаг

Для Editor — поддержать [ED4g.3/4](../engine/EDITOR_PLAN.md#current-editor-queue)
только теми общими UI capabilities, которые потребуются consumer. Numeric input
acceptance остаётся отдельным доступным proof при подходящем вводе.
Самостоятельная visual-quality задача — **PA8c.0 baseline текста/геометрии**,
затем выбор конкретных PA8c.1/2 исправлений по наблюдаемым дефектам.
Wider Window/backend/fresh package и hardware/release checks не назначаются
повторно как завершённая feature и не блокируют каждое новое поле инспектора.
Full INF5 и отложенный RichTextEditor backlog развивать по отдельному consumer.

<!-- Совместимость прежних ссылок STATUS; записи перенесены в HISTORY. -->
<a id="текущие-проверки--wsi-stages2026-10-10"></a>
<a id="предыдущие-проверки--hosting-dispatcher2026-10-10"></a>
<a id="предыдущие-проверки--wglthread-lifetime2026-10-10"></a>
<a id="предыдущие-проверки--lifecycleresize2026-10-10"></a>
<a id="предыдущие-проверки--textboxname-smoke2026-10-10"></a>
<a id="предыдущие-проверки--интеграция-в-main2026-10-10"></a>
<a id="предыдущие-проверки--агентный-callback-run2026-10-10"></a>
<a id="предыдущие-ui-проверки--code-run2026-10-09"></a>
<a id="последнее-изменение-формата--2026-10-09"></a>
Прежние датированные разделы — в [полном архиве](HISTORY.md#status-before-planning-revision-2026-11-05).

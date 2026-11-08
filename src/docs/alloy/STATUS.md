# Alloy / Argentis: текущее состояние и точка продолжения

**2026-11-08 — UIA3d.3 исправлен TextBox line fit:** Height32 с default Padding8
больше не обрезает низ default16/600 строки. Полный font line box получает место
за счёт вертикальных отступов; clip внутри border отделён от позиции по Padding.
NumberBox получает то же исправление, natural height и error badge сохранены.
Exact raster regression против Height48, selected/unselected, Dark/Light1/1.5/2
прошёл. Новые UI1184/Hosting154/Editor1412 каждый D/R, source/published3backend
по18PNG/12comparisons и Vulkan0/0 прошли. [Причина/команды/scope](HISTORY.md#textbox-line-fit-2026-11-08).
Далее PA8c.0, включая прежний дополнительный shadow-overlap случай; physical input/
DPI/full solution/fresh NuGet consumer этим исправлением не повторялись.

**2026-11-08 — UIA3d.2 завершён:** TextBox/RichTextEditor/NumberBox используют
сплошной квадрат Theme.Error стороной в высоту поля и символ Theme.OnError.
Text/focus viewport и rich auto-height учитывают блок; tooltip активен на всей площади.
Аудит исправил inline rename InvalidName binding и demo message/OverlayHost;
командные/IO diagnostics сохранены. UI1176/Hosting154/Editor1412 каждый D/R,
source/published3backend18PNG/12comparisons каждый и fresh5package Vulkan6cases D/R
прошли, validation0/0. [Изменения/аудит/команды](HISTORY.md#validation-badge-2026-11-08).
Дополнительный lower-field tooltip profile дал 2shadow-overlap pixels delta3/255
вне badge; это отдельный PA8c.0 baseline пункт, основной profile прошёл без смены
порогов. Physical input/DPI/full solution этим срезом не проверялись.

**2026-11-08 — UIA3d.1 Vulkan frosted backdrop исправлен и проверен.**
TextBox/RichTextEditor/NumberBox.Editor резервируют место для красного значка справа.
OverlayHost показывает точный ErrorMessage с delay500ms, wrap/scroll и cleanup;
Inspector сохраняет aggregate Diagnostics, draft/selection/history, inline labels убраны.
BoxShadow/Shadow, themed PanelShadow/MenuShadow и Skia outer image-filter path
подключены к Editor island styles и MenuPopup. Legacy contexts omitting optional
shadow capability совместимы; App/DI/native ABI/dependencies не менялись.
Первоначальные UIA3d opaque/raster proofs сохраняют собственный scope в [HISTORY](HISTORY.md#validation-shadows-2026-11-08); текущие native проверки — ниже.

**UIA3d.1 закрыт:** в pinned native Skia к GENERAL source stage добавлен HOST,
соответствующий существующему HOST_WRITE. DLL пересобрана, patch/hash включены в
reproducible recipe/build metadata. Blur/default backdrop/ABI/ownership сохранены.
Новые source Debug/published Release frosted profiles:18actual blur witnesses,
18PNG/12comparisons,6Vulkan validation0/0 reports каждый. Fresh5package independent
Vulkan consumer D/R по6cases и loaded native SHA прошёл. Старый native и отключённый
blur отрицательные controls отклонены. Interop18 и composition36/leases18 прошли;
UI1170/Hosting154/Editor1412 каждый D/R повторены, failed0/skipped0.
[Причина/команды/хеши/scope](HISTORY.md#vulkan-backdrop-2026-11-08).
Physical input/DPI/multi-monitor, VRAM/K7/K8/swapchain и full solution/release
этим срезом не проверялись. Далее PA8c.0 visual baseline по запросу consumer;
независимый Editor шаг — ED5b contract.

Предыдущие typography PA8c.3b и ED5a.4 результаты сохраняют собственные даты/scope:
[default fonts](HISTORY.md#default-fonts-2026-11-08),
[camera/selection acceptance](../engine/HISTORY.md#ed5a4-acceptance-2026-11-08).
[Текущая очередь](PLAN.md#current-ui-queue), [дизайн-код](DESIGN.md).

## Состояние реализации

PA8c.3b делает preferred family общим default; системный каталог применяется
к parameterless backend и RichText, file-only override сохранён.

SkiaSharp/native4.155.0 и прежний Vulkan ABI сохранены. PA8c.3a включает
FontWeight100..900/FontItalic, styled measure/draw и cache keys; Rich FontWeight,
Bold/Undo и requested→system chain→file fallback. File-only catalogs сохранены.

UIA1 typed View/Data/scoped styles и App factories, UIA2 Window, UIA3 compact
NumberBox/Vector3Editor/PropertyEditorRow, RichText auto-height, themed CheckBox
и states реализованы. Numeric adjustment opt-in, draft/capture/cancel outcomes
и stable identity gates сохранены. Platform.Silk button/wheel использует ordered
cursor position и передаёт Shift/Ctrl. Прикладные Transform/history/units — Editor.

INF1–4/6/7 имеют реализацию и bounded checks; INF5 diagnostics остаётся частичным.
TreeView/menu/bindings/appearance/DPI существуют в указанных в HISTORY scopes;
наличие API не закрывает все beta/PA1–PA10, alpha/K1–K9 или физическую приёмку.

## Проверки готовности

Последний кодовый fonts/defaults запуск **2026-11-07/08**: Alloy1126/1126, Hosting154/154,
Editor1300/1300 каждый D/R, failed0/skipped0. Source Debug/published Release ×
dark/light:4Editor native runs, actual Segoe UI Variable Text600, scale1.5,
header/menu/status/theme/idle/close/drain и UI/GPU/children owners0, validation0/0.
Parameterless backend gallery: по18captures/12complete-frame comparisons в source
Debug и published Release при synthetic1/1.5/2, outside-tolerance pixels0.
Exact fresh4nupkg consumer Debug build/run и Release publish/run подтверждает
default family и file-only override; public example build/publish также прошёл.
[Подробные команды, baseline, initial failures и ограничения](HISTORY.md#default-fonts-2026-11-08).

Предыдущая opt-in fonts matrix: [PA8c.3a](HISTORY.md#system-fonts-2026-11-07).
Прежние Skia update/full-solution/Editor/native numeric результаты сохраняют
свои даты и scopes; новые full Aether/solution/Playing proof не объявлены.

## Блокеры и ограничения

- Fonts: automatic opsz/arbitrary axes и plain-text per-glyph fallback не добавлены.
  Physical DPI, performance/VRAM и fresh package GPU/Hosting matrix не проверялись.

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

UIA3d.1 завершён; следующий UI visual шаг — PA8c.0 baseline по конкретному consumer.
Независимый Editor шаг — [ED5b translation contract](../engine/EDITOR_PLAN.md#ed5b-translation).
Общие controls добавлять в Alloy/Argentis по конкретной потребности consumer.

Независимый visual-quality трек — PA8c.0 baseline на текущем Variable Text,
затем PA8c.1/2 по наблюдаемым дефектам. Default policy и подключение Editor уже
завершены в сохранённом scope. Numeric physical acceptance, K3/K7/K8 и wider
release/fresh package matrix остаются отдельными проверками.

Входящая Studio ui.resizable-panes остаётся proposed: Claim/Decision владельца
ещё нужны, в этой документационной актуализации работа не принята.
INF5 и отложенный RichTextEditor backlog выбирать по конкретному consumer.

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

# Alloy / Argentis: текущее состояние и точка продолжения

**2026-11-11 — CORE7.6: итоговая configuration/package сверка.**
Hosting171/171 и Editor1496/1496 каждый D/R, Failed:0, Skipped:0.
Source/published/fresh16package configuration collection:230checks/profile, оба DI;
окно/GPU/game не активируются. Bundled native Skia/analyzer/targets/README payload
проверены по SHA. Runtime/public API/внешние dependencies не менялись.
[Guide](../core/CONFIGURATION_GUIDE.md), [команды/границы](HISTORY.md#core76-2026-11-10).

Предкоммитная UI suite11ноября: Alloy1224/1224 каждый D/R, failed/skipped0;
[команды](HISTORY.md#precommit-2026-11-11). Прежние native SplitView/Studio/physical/K7
profiles не повторялись. Таблица native/UI ниже относится к09ноября. UI очередь сохранена.
[Предыдущий STATUS целиком](HISTORY.md#status-before-core76-2026-11-10).

## Состояние реализации

- SplitView владеет двумя pane hosts/divider и их content. Constraints заданы на
  hosts; shortage сжимает minima пропорционально, конечные maxima оставляют остаток.
  Preferred размер восстанавливается после расширения. Consumer владеет persistence.
- Drag освобождает capture до callbacks. Esc/focus/capture/context loss отменяют,
  внешняя preference (включая reentrant observer) прерывает без возврата старого значения.
  Tab/axis arrows/Shift/Home/End, Dark/Light и typed/local appearance работают.
- Cobalt App/IFactory demo с отдельными Data/View/Style и воспроизводимый fresh-package
  runner находятся в ManualTests/AlloyTest. Studio/Editor layouts автоматически не мигрированы.
- UIA1 View/Data/styles, UIA2 Window, UIA3 compact/numeric controls, UIA3d validation/
  shadows/frosted fix/TextBox line fit и PA8c.3a/b typography сохранены. SkiaSharp/native
  4.155.0, ABI, App/DI/lifecycle contracts не менялись; INF5 остаётся частичным.

## Проверки готовности

| 2026-11-09, scope | Фактический результат |
| --- | --- |
| UI xUnit | 1224/1224 каждый Debug/Release, failed0/skipped0; 40 новых SplitView cases |
| Hosting xUnit | 154/154 каждый Debug/Release, failed0/skipped0 |
| Editor xUnit | 1412/1412 каждый Debug/Release, failed0/skipped0 |
| Native demo | source Debug/published Release × OpenGL/Vulkan: 4 успешных profiles, nested axes/input/state/idle/disposal; sessions0, Vulkan warning/error0 |
| Independent package consumer | 3 fresh local nupkg, отдельный cache/feed, без ProjectReference; по21checks Debug/Release, build0errors/0warnings |

Native input программно routed в реально рисуемом окне. Screenshot OpenGL Light
просмотрен. Source demo build0errors/3 прежних warnings в View2/SettingsFormSmoke;
в новых C# файлах warnings не наблюдались. Общая рабочая версия0.2.5.2-alpha
не изменена этой сессией; delivery proof использует0.2.5.2-splitview.20261109.1.
Полные solution/Aether tests, actual Studio consumer и release matrix не запускались.

Прежние UI/TextBox/backdrop/fonts результаты сохраняют свои даты и scopes:
[line fit](HISTORY.md#textbox-line-fit-2026-11-08),
[Vulkan backdrop](HISTORY.md#vulkan-backdrop-2026-11-08),
[default fonts](HISTORY.md#default-fonts-2026-11-08).
[Полный предыдущий STATUS](HISTORY.md#ui-status-before-split-view-2026-11-09).

## Блокеры и ограничения

- SplitView: physical held-button/Esc/OS capture, release вне настоящего окна,
  multi-monitor DPI и системный accessibility не доказаны routed tests.
  Collapse/docking/cursor/persistence policy не входят в v1. Synthetic scale1/1.5/2
  проверена отдельно; local CPU package consumer не заменяет fresh-package GPU matrix.
- PA8c.0 current-font visual baseline, включая прежний дополнительный shadow-overlap
  2pixels delta3/255, остаётся. Automatic opsz/arbitrary axes/per-glyph fallback не добавлены.
- Numeric physical arrows/modifiers/captured Esc и K3 physical100/200%/multi-monitor
  по-прежнему требуют своего proof; UiKey.None в automation не считается дефектом контрола.
- K7: прежний240-window run failed, GDI23→137/USER26→26, screen HDC в NVIDIA
  ICD32.0.16.1742. Поиск workaround остановлен пользователем; этим срезом не возобновлён.
- K8 device/context loss, VRAM/resource trends и final alpha/release matrix открыты.
  K9 имеет прежний managed baseline; новые rendering options требуют измерений.

## Следующий конкретный шаг

Передать Studio готовый SplitView API и проверить его actual layout/popup/capture/
draft scenario у consumer. Независимые ветки: [ED5b](../engine/EDITOR_PLAN.md#ed5b-translation),
PA8c.0 по наблюдаемому visual дефекту, INF5 по реальному diagnostics consumer.
K3/K7/K8 и wider release остаются отдельной приёмкой.

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

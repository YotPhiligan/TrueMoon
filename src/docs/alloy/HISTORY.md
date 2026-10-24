# Alloy / Argentis: история работ и проверок

## PA8b.1: group opacity и островки Editor — 2026-10-24

Добавлены Element.Opacity/UiProperties.Opacity с finite0..1 validation и только
render invalidation, optional IOpacityDrawingContext.SaveOpacity/обычный Restore.
Skia применяет alpha один раз ко всему subtree через transient SaveLayer/paint,
сохраняя баланс при nested groups и throwing child. Zero subtree не рисуется,
без capability fractional opacity рисуется opaque. Opacity не меняет layout/input.
Отдельный Theme.PanelBackdrop: sigma8logical, Dark tint#12161E/D0,
Light#F2F5FA/D8, opaque fallback; null consumer использует Surface.
Editor ED4e — anchored content-height islands над viewport, parent-owned shadow/
fade/slide без фоновых timers; nested View scope/retention сохранены.

41новый UI и26Editor cases. Narrow103/103; полные related UI842/Hosting98/
Editor699 Debug/Release =3278passed/0failed/0skipped. Nonincremental Debug solution
build0errors/1532warnings.12native source Debug/published Release cases,158PNG,
16reaped children,validation0/0 и zero owned UI/GPU/mappings. Raster tests покрывают
overlap/image/nested groups/backdrop/current frame/sharp foreground при scale1/1.5/2;
только прямой .5-alpha remainder oracle допускает±1byte квантования, endpoints exact.
Native OpenGL actual preview Dark/Light/null material и geometry/retained fields
проверены; итоговые PNG просмотрены. Полная evidence matrix —
`TestResults/Islands-20261024/verification.json`; engine consumer/history
[ED4e](../engine/HISTORY.md#ed4e-островки-поверх-viewport--2026-10-24).
Aether/full solution tests/packages/physical DPI/Vulkan HUD/performance/VRAM/
final alpha не повторены. Общее effect graph/cache и свободный docking не добавлены.

## ED4d.1: иконки и условный Inspector — 2026-10-24

UiSymbol.Add/Delete добавлены в конец enum; существующий IconButton и OverlayHost
используются consumer Inspector. Actions26/reset24, semantic names, delayed500мс
Tooltip с opaque Theme.Control в обеих темах, focus/input/clip/lifecycle и отсутствие
idle кадров проверены. Это прикладной hint consumer; новый global Tooltip service
или DI lifetime не введён. EditorLayout отдаёт скрытую колонку/gap viewport.

Текущие related UI801/Hosting98/Editor673 Debug/Release:3144passed,failed/skipped0;
25новыхEditorcases, narrow181/181. Nonincremental Debug solution build0errors/
1532warnings после переноса generated output в OS TEMP из-за заполненного E:.
12native cases/152PNG/16reaped children,validation0/0 и zero owned UI/GPU/mappings.
Финальные Dark Tooltip/Light component/hidden PNG просмотрены. Подробные commands,
attempts и [requirement matrix](../engine/HISTORY.md#ed4d1-компактные-действия-и-контекст-инспектора--2026-10-24).
Evidence: `TestResults/InspectorChrome-20261024/verification.json`. Aether/full solution
tests/packages/physical/performance/final alpha не повторены; прежние gates открыты.

## ED4d: Inspector consumer — 2026-10-24

Новый Editor inspector использует существующие CheckBox/TextBox/RichTextEditor,
ScrollViewer, typed bindings и dynamic style roles. Retained section/property identity
и parent-owned View; model/style/typed factory регистрируются через App/Cobalt.
Generic UI contracts не расширялись. Dynamic editors и common authoring APIs —
в Aether/Editor; [прикладной контракт](../engine/EDITOR_PLAN.md#ed4d-динамический-инспектор-компонентов--начат-2026-10-24).

Scoped style: inputs12/height26, label11/width58, reset10/width42, null10/width60
с left padding32, multiline12/height70, metadata10/errors11. Scalar row выделяет
input оставшуюся ширину; theme/refresh сохраняют controls/focus/selection/scroll.
Ctrl+Enter/Esc маршрутизируются из multiline editor, его text undo не перехватывается.

Текущие полные связанные UI801/Hosting98/Editor648 Debug/Release, Aether1142 отдельно:
2689/configuration,5378всего,failed/skipped0. Новые163cases относятся к engine/editor.
Nonincremental Debug solution build:0errors/1532warnings.12native source Debug/
published Release cases вне repo,146PNG/16reaped children,validation0/0 и zero owned
UI/GPU/mappings. Header/status/Transform/hierarchy/documents/legacy regressions
прошли. Финальные dark/light/empty/diagnostic/preview и legacy/documents PNG просмотрены.
Null label spacing исправлен до final run. Existing font glyph coverage не расширялась.

Точные commands/paths/attempts/requirement matrix и границы —
[engine HISTORY](../engine/HISTORY.md#ed4d-динамический-инспектор-компонентов--2026-10-24).
Evidence: `TestResults/Inspector-20261024/verification.json`. Full solution tests/
packages/physical input/DPI/performance/VRAM/final alpha не повторены; прежние
hardware/quality gates сохраняются. Следующий consumer — ED4c.2 names/order.

## PA4c: TreeView и ED4c.1 — 2026-10-24

Общий `TreeView/ITreeView` реализован под первый editor hierarchy consumer.
Frozen TreeItem forest до4096items: stable ID, ParentId, text, UiSymbol. Валидация
duplicate/missing/cycle/labels предшествует замене; контрол не меняет предметный
граф. Один retained Element рисует только строки внутри viewport/ancestor clip.
Expansion/selection сохраняются для surviving IDs; Select раскрывает ancestors
и выполняет ScrollViewer reveal после layout. Up/Down/Home/End/Left/Right/
Enter/Space, parent move drag/drop, Ctrl+Left/Right и blank root drop поддержаны.
Self/descendant/same-parent/outside clipped viewport drop не публикуются.
Esc/FocusLost/disable/detach/геометрия/new snapshot отменяют capture; равный SetItems
тоже отменяет жест, но без лишнего invalidation в idle. Новых timers нет.

Entity/Cube/Interaction/ChevronRight/ChevronDown дописаны в UiSymbol с сохранением
старых enum values; Draw использует геометрию. Editor style: text12/row24/indent16/
icons14/padding8, selectable read-only diagnostics. View/Data/Style и factory
в Editor, domain reparent/history — Aether. Parent root style использует semantic
roles только для legacy частей, не нарушает style scope вложенного hierarchy View.
Full virtualization, rename/reorder/multiselect/typeahead, drag autoexpand/autoscroll
и world-preserving move оставлены следующим consumers.

### Текущие проверки

Narrow `treeview-draw-final.trx`:41cases/0failed/0skipped. Иконки и disclosure
доказаны непосредственно `Draw_DisclosureAndPerItemIconsUseDeclaredSymbolsAndHierarchyDepth`;
`PointerDrag_ParentAndBlankRootDropsPublishRequestsWithoutChangingItems` проверяет
request без UI-only mutation. Unit cases покрывают frozen validation, deep graph,
keyboard/reveal/scroll, selection/theme retention, capture cancellation, clipped
draw и dispose. Это часть71новых methods/130cases engine+UI+Editor.

Текущие полные связанные suites Debug/Release: **UI801/Hosting98/Editor577**,
failed/skipped0; engine1050 отдельно. Всего2526/configuration/8TRX. После последней
правки текстовой подсказки Editor577/configuration повторён отдельно,2TRX.
Финальный nonincremental Debug solution build:0errors/1532warnings.
SDK10.0.402/net10.0/xUnit2/VSTest; commands/reports и [подробный consumer proof](../engine/HISTORY.md#ed4c1-дерево-сущностей-и-reparent--2026-10-24)
не означают новый full solution test baseline.

Native final `TestResults/Hierarchy-20261024/native-092503209/summary.json`:
source Debug/published Release вне repo, **10cases/118PNG/14reaped children**,
empty/legacy/documents/Transform/hierarchy. Во всех reports0owned UI sessions/
mappings/GPU resources/handles и validation0/0, Header5/status4 captures,
BottomAligned/ResizeGripClear. Hierarchy pointer drag/collapse/reveal, keyboard
history, Save/reopen, retained Dark/Light tree/inspector и real parented preview
прошли. Dark hierarchy/published light preview/legacy/documents PNG просмотрены.
Подсказка использует Ctrl+Left/Right, чтобы не зависеть от наличия arrow glyphs.

Промежуточные driver failures сохранены и не засчитаны: старые header wait/focus
assertion и document Capture со старым status format. Исправлены ожидания driver,
source consumer не ослаблен; final10cases повторены после последней source правки.
Подробности/точные paths/команды — в engine HISTORY. Evidence:
`TestResults/Hierarchy-20261024/verification.json`; TRX/log/binlog/JSON/PNG,
source/binary fingerprints и scripts ignored. Планы/статусы/default style/README,
local links/anchors/namespaces/whitespace проверены отдельно.

Full solution tests/прочие модули/packages/physical DPI-input/performance/VRAM/
Vulkan UI material matrix/final alpha не повторены. Предыдущие hardware/quality
ограничения сохраняются; поиск OS/driver workaround не выполнялся. Следующий
consumer — ED4c.2 rename/reorder после persistent engine metadata/history.

## PA4b: выравнивание StatusBar по нижней границе — 2026-10-24

По скриншоту пользователя подтверждена пустая полоса под статусбаром: default
WindowFrame вычитал DesiredSize.Height resize grip из content и добавлял его
к измеренной высоте. Это была отдельная строка16logical, а не лишний padding
самого StatusBar. Предыдущее решение default frame изменено явно в PLAN/DEFAULT_STYLE.

WindowFrame теперь измеряет и размещает content до внутреннего нижнего бордера.
Grip размещается поверх правого нижнего угла, ограничен доступными шириной и
высотой content, рисуется последним и сохраняет native ResizeBottomRight region.
Показ/скрытие grip не резервирует высоту; отдельный footer/API или компенсирующий
отрицательный отступ не добавлены. Editor StatusBar уже резервирует справа24,
поэтому details и preview не пересекаются с grip16. View/Data/Style/DI прежние.

Регрессия сначала воспроизведена на прежнем production:8ожидаемых failures,
включая footer Y755 вместо771 и measured height134 вместо118. После исправления
узкий прогон: **Passed:57,Failed:0,Skipped:0**, exit0. Exact checks:

| Требование | Тест |
| --- | --- |
| Нет лишней высоты контейнера/нижней строки | DefaultStyleTests.DefaultFrame_ResizeGripOverlaysContentWithoutAddingMeasuredHeight |
| StatusBar и Bar совпадают по bounds и доходят до бордера, grip не перекрывает details | EditorStatusBarViewTests.DefaultFrame_StatusBarReachesBottomBorderWithoutExtraRowAndKeepsResizeGripClear (scale1/1.5/2; resize/maximize/restore/theme/idle и native regions) |
| Согласованные capabilities и legacy shell | DefaultFrame_CommandsCapabilitiesRegionsAndIdleFollowObservedWindow; DefaultFrame_LayoutUsesLogicalUnitsAndSettlesWithoutRedraw |

Итоговые related: UI760/Hosting98/Editor530 — **Debug1388/1388,Release1388/1388**,
6TRX,failed/skipped0,4новыхcases. Команды с isolated artifacts/log/binlog/TRX:

    pwsh -NoProfile -File TestResults/StatusBarAlignment-20261024/VerifyNarrow.ps1
    pwsh -NoProfile -File TestResults/StatusBarAlignment-20261024/VerifyFinal.ps1

Native:2empty cases source Debug/published framework-dependent Release вне repo,
20PNG; header/status popup/theme/dismissal и cleanup passing. Status smoke теперь
проверяет footer bottom == inner frame bottom, Bar bounds == View bounds и отсутствие
пересечения details/grip. UI sessions/mappings/GPU resources/handles0,View disposed;
compiler/children не запускались. Dark source и Light published PNG просмотрены:
под footer нет пустой полосы, popup остаётся над footer. Input directed в owned
UiSession; physical OS drag/DPI acceptance этим запуском не подтверждается.

Evidence: TestResults/StatusBarAlignment-20261024/verification.json — exact counters,
methods/commands, source/binary/PNG hashes, native reports и doc checks. SDK10.0.402,
net10.0,xUnit2/VSTest. Full solution build/tests,Aether/headless,legacy/Transform
native workflows,packages/performance/golden/VRAM/final alpha не повторены.
Прежние CPU receiver/Mithril fixture failures открыты; следующий шаг прежний.

## PA4b: StatusBar и ED4b Editor — 2026-10-24

По согласованному дизайну добавлен общий StatusBar/IStatusBar в Argentis:
primary/action, ordered secondary и details. Список частей фиксируется при
создании; distinct/live/unowned validation предшествует adoption. Secondary
скрываются owned slots, child visibility bindings сохраняются. Более ранние
secondary уступают место более поздним. Details/action имеют приоритет, bounds
конечны/неотрицательны, hidden input/Tab исключены. Расширение восстанавливает те
же экземпляры. Focus hidden descendant возвращается на details на owner frame;
detach/dispose отменяют deferred focus, не создают постоянных idle frames.
Новые UiSymbol.Info/Warning/Busy/Objects/Preview используют геометрию.

Editor footer:28logical px, text12, icons14, targets24, top border и right24 под
resize grip. Слева краткий semantic status и доступная Cancel текущей операции,
справа objects/preview/details. Цвет дополняет иконку/текст, Tick/PID/full error
вынесены в подробности. Документ/decision/game operation/preview busy/error имеют
явный приоритет. Активный independent preview показывает «Редактирование сцены»;
его runtime/Tick/PID помечены Runtime Edit Preview в подробностях, game pause
остаётся отдельным состоянием. Фиктивных progress/counters нет.

Типизированные EditorStatusBarViewModel/View/Style и popup factories зарегистрированы
публичным UseAetherEditorStatusBar через App/Cobalt. Model заимствует прежние
runtime/document/Transform models; окно владеет View, OverlayHost — popup, App —
models/styles. Controls создаёт View; нет service locator/per-frame resolution.
Production UseEditing(previewInStatusBar:true) скрывает старые inspector preview
actions/status; standalone compatibility сохранена. Popup использует прежний
committed preview coordinator, а Cancel — существующих owners. Отмена при
busy preview drains его owner; active idle preview останавливается своей кнопкой.
Ошибки нормализованы в LF для readonly selectable RichTextEditor. Esc/Tab/outside/
FocusLost закрывают popup, one-click переключение footer segments сохранено.
Theme/refresh сохраняют controls/selection/focus, overflow закрывает только
принадлежащий скрытому trigger popup, не крадёт фокус replacement popup.

Проверки: Windows x64, SDK10.0.402, net10.0, xUnit2/VSTest.

| Требование | Exact checks |
| --- | --- |
| «текст поменьше», «добавить иконок» | EditorStatusBarViewTests.CompactSegments_Use28PixelBar12PixelText14PixelSymbolsAndRetainIdentityAcrossThemes |
| «вынести какие-то кнопки туда» | CancellationSegment_TracksBusyOperationAndInvokesExistingCommand; PreviewPopup_StartAndStopUseSharedCommittedPreviewAndKeepThePopupAndFocus |
| Runtime/ошибки/настоящий preview | RuntimeSnapshot_MapsSemanticStatusAndRetainsExactTickPidAndEntityCount; DocumentError_PreservesFullRuntimeAndDocumentDiagnosticsAndClearsOnRecovery; ActiveEditPreview_ReportsEditingRatherThanGameSimulationAndRetainsErrorPriorityAndRuntimeDetails |
| Document/game/preview cancellation | PendingDocumentSave_TakesStatusAndCancellationPriorityOverGame; GameCancellation_InvokesExistingCancelExactlyOnceAndIdleCancellationIsInert; BusyPreview_CancelDrainsItsOwnerAndDoesNotCancelRuntime |
| Narrow/overflow/input | SecondaryFit_AtExactWidthKeepsAllAndOnePixelLessCollapsesLowestPriority; OverflowCollapse_PreservesChildVisibilityAndExcludesItFromTabUntilExpanded |
| Focus/detach/replacement | OpenPreviewPopup_WhenItsTriggerOverflowsClosesAndReturnsFocusToDetails; PendingOverflowFocus_DoesNotStealFocusFromAReplacementPopup; DetachingDuringPopupOverflow_CancelsDeferredFocusWithoutDisposingBorrowedModel |
| Theme/full error/read-only | ErrorPopup_NormalizesCrLfAndRetainsDiagnosticSelectionFocusAndIdentityAcrossThemeChanges |
| Cobalt/MS DI/ownership/failed factory cleanup | BothProviders_FactoriesBorrowSharedModelsAndStylesWhileConsumersOwnIndependentProducts; FactoryStyleFailure_DisposesUnpublishedProductPreservesErrorsAndBorrowedModels |

80новых случаев: StatusBar20, Editor60. Итоговые related UI759/Hosting98/Editor527
**Debug1384/1384 и Release1384/1384**,6TRX,failed/skipped0. Два прежних expectations
обновлены под footer28 и structured runtime notifications с точными/no-op checks.
Первый related прогон обнаружил именно эти2устаревших ожидания; они не скрыты.
Новые regression checks выявили deferred focus при detach и сохранение original
factory error при cleanup failure; production исправлен, regression passing.
Первый native proof подсветил неверную подпись game pause у Edit Preview; добавлены
4случая и native assertion, итоговый прогон повторён на исправленном исходнике.

Команды и исходные отчёты:

    pwsh -NoProfile -File TestResults/StatusBar-20261024/VerifyFinal.ps1
    pwsh -NoProfile -File TestResults/StatusBar-20261024/FinalizeEvidence.ps1

VerifyUi запускает три связанных test projects в Debug/Release с isolated artifacts,
уникальными TRX/log/binlog. VerifyFinal затем выполняет nonincremental Debug build
TrueMoon.slnx:0errors/1532warnings. VerifyNative строит Editor Debug/Release,
публикует framework-dependent Release и запускает6cases вне repo:
empty/legacy/Transform × source Debug/published Release.68PNG/12reaped children,
validation errors/warnings0/0; UI sessions/mappings/GPU resources/handles0,
View disposed и child reap. В каждом case5header+4status PNG; empty добавляет1,
legacy3,Transform3. Реальные footer start/stop, committed revision/pixel checks,
Undo/Redo/Save/reopen без game compilation; invalid game csproj fixture не строился.
Header commands/keyboard/theme/dismissal сохранены. Dark/Light/details/Preview/
Transform screenshots просмотрены; popup над footer, inspector preview скрыт.
Это directed input собственного UiSession, не physical mouse/keyboard acceptance.

Evidence: TestResults/StatusBar-20261024/verification.json содержит exact commands,
TRX counters/test methods, source/binary/PNG hashes, native/status/header/cleanup
reports и документационные checks. Источники/бинарники проверены после native runs.

Границы: full solution tests, Aether/headless probes, fresh packages, physical
DPI/OS input, performance/golden/HUD/native Vulkan effect matrix/VRAM/final alpha
не повторены. Прежние CPU FrameReceiver TakeLatest null и Mithril ready JSON
file-sharing race не исправлялись этим UI срезом. Нет новых базовых prerequisite;
следующий прикладной шаг — minimal create/delete общего API/history и UI consumer.
[Дизайн Editor](../engine/EDITOR_PLAN.md#ed4b-компактная-строка-состояния--2026-10-24).

## PA8b: матовые поверхности — 2026-10-24

По запросу «матовую полупрозрачность для задников меню и в целом такой механизм»
добавлены immutable BackdropMaterial (sigma0..64logical, tint alpha, opaque fallback),
UiProperties.Backdrop/Element.Backdrop, WithBackdrop и ViewStyleRule.Backdrop.
Local/element style/view style/theme/default precedence прежний. Material рисуется
до обычного Background и foreground/children. Для эффекта обычный Background
должен быть прозрачным; фокус, layout и input semantics не меняются.

Optional IBackdropDrawingContext не расширяет обязательный IDrawingContext.
Unsupported context рисует fallback. Skia snapshot берётся с текущего surface до
отрисовки foreground; область capture — current visible clip+halo3sigma, ограничена
surface. Gaussian sigma масштабируется в device pixels, rounded clip и translate
учитываются. Snapshot заменяет исходные pixels через Src, затем tint SourceOver:
полупрозрачный исходный фон не смешивается сам с собой второй раз. Draw-local
snapshot/filter/paint освобождаются using; GPU path не вызывает ReadPixels/CPU blur.
Нет предыдущего-frame cache, дополнительных кадров, timers или фоновых задач.
Sigma0 и opaque tint не требуют capture. Other transforms возвращают fallback.
У render/text adapter нет нового App lifecycle; существующий owner surface
заимствуется только на время draw. Vulkan сохраняет публичный Draw(Action<SKCanvas>),
общая реализация получает surface через internal draw lease.

В меню Theme.MenuBackdrop задаёт Dark sigma12/tint#12161E A176 и Light
sigma12/tint#F2F5FA A192; null возвращает opaque Surface. MenuItem idle/disabled
прозрачны, hover/pressed и focus outline прежние. Повторная смена темы сохраняет
popup/focus/команды. Editor не добавляет второй effect или renderer service.

Проверки, SDK10.0.402/net10.0/xUnit2/VSTest/Windows x64:

| Требование | Exact checks |
| --- | --- |
| Матовый фон, чёткий foreground, kernel вне clip | BackdropRenderingTests.FrostedPanel_BlursEarlierContentAndKeepsForegroundSharp (scale1/1.5/2) |
| Alpha, rounded clip, свежий frame, paint order | FrostedPanel_ReplacesSampledAlphaInsteadOfBlendingTheBackdropTwice; RoundedBackdrop_ClipsBlurAndTintAndDoesNotReuseThePreviousFrame; NestedBackdrops_SampleEarlierMaterialsInPaintOrder |
| Общий material/Fluent/styles/fallback | TintOnlyOrOpaqueMaterial_DoesNotRequireASamplingSurface (2cases); UnsupportedSampling_PaintsOpaqueFallbackBeforeForeground; SkiaWithoutSurfaceOrUnsupportedTransform_ReturnsFalseWithoutPainting; Material_RejectsUnboundedOrInvalidBlur (4cases); Material_RequiresOpaqueFallbackAndSupportsStyleOverrideRemoval |
| Меню и Theme без потери popup/focus | MenuInteractionTests.MenuBackdrop_FollowsThemeWithoutReplacingPopupOrFocusAndCanBeDisabled |

16новыхcases (Backdrop15, menu1). Narrow44/44,failed/skipped0:
TestResults/Backdrop-20261024/narrow-195958793/backdrop.trx. Команда:

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --artifacts-path TestResults/Backdrop-20261024/artifacts -c Debug --filter 'FullyQualifiedName~BackdropRenderingTests|FullyQualifiedName~MenuInteractionTests' --logger 'trx;LogFileName=backdrop.trx' --results-directory <narrow-run> /bl:<narrow-run>/tests.binlog -v minimal
```

Финальные related unfiltered Debug/Release: UI739 + Hosting98 + Editor467 =1304
в каждой конфигурации,failed/skipped0,6TRX в managed-200023773. VerifyUi.ps1 выполняет
для каждого из трёх проектов dotnet test <project>/<project>.csproj с тем же
--artifacts-path, -c <configuration>, --logger trx, --results-directory <run> и
unique /bl:<run>/<project>-<configuration>.binlog. Это не full solution/Aether run.

Native-200054605: source Debug/published Release, external cwd, empty/legacy
profiles ×2:4cases/28PNG/4reaped children. Все header/hover/disclosure/keyboard/
theme/tooltip/FocusLost proofs успешны. Empty без compiler/child, legacy по9
прежних игровых команд/2children. ViewDisposed/Reaped=true; UI sessions/mappings/
GPU resources/handles0, Vulkan child validation errors/warnings0 во всех cases.
File/View screenshots source/published просмотрены. OpenGL PNG показывает material,
а не fallback: например, File point(520,325)=15,20,30 при underlying10,15,26;
(400,275)=17,22,31 при том же underlying, opaque fallback18,22,30.
Kernel sampled grid/text behind menu; foreground glyphs/focus outline остаются чёткими.

VerifyFinal.ps1 последовательно запускает VerifyUi.ps1 и VerifyNative.ps1.
Native script собирает Editor Debug/Release --no-restore, публикует Release
--no-build --no-restore --self-contained false вне repo, затем --smoke <case>
--validation, для legacy --project <absolute Game.Logic.csproj>. Все build/test/
publish имеют unique binlogs; stdout/stderr и header/cleanup JSON сохранены.
Пользовательские Editor/Debugger процессы и обычный bin не затронуты.
Aggregate TestResults/Backdrop-20261024/verification.json связывает TRX/native
commands/reports и source/binary/PNG hashes; renderer source/binary fingerprints
дополнительно сняты после native run. Current code соответствует проверенным builds.

Ранний compile выявил отсутствие SKCanvas.BaseLayerSize в закреплённом SkiaSharp;
использован root DeviceClipBounds, зафиксированный при создании контекста до
Element clips. Первые narrow запуски имели одну неверную pixel assertion при150%:
контрольная точка попадала на default Border outline. Тест изолировал material
через BorderWidth0, production blur ради прохождения assertions не менялся.
Финальные narrow/related/native выше выполнены после коррекции.

Vulkan adapter подключён и компилируется, но отдельный native Vulkan/HUD material
proof не выполнялся. Full solution, Aether/headless и остальные native document/
Transform/exit workflows, packages, golden/performance/VRAM/physical/full alpha
не повторены. Прежние CPU receiver/Mithril fixture issues этим не исправлены.
Не заявлены OS desktop blur/window transparency, arbitrary canvas layers/transforms,
saturation/noise/effect graph/cache. Следующий прикладной шаг — create/delete.

API сверены с первичными источниками:
[SKSurface.Snapshot](https://learn.microsoft.com/en-us/dotnet/api/skiasharp.sksurface?view=skiasharp),
[SKImageFilter.CreateBlur](https://learn.microsoft.com/en-us/dotnet/api/skiasharp.skimagefilter.createblur?view=skiasharp-3.119).

## ED4a: знак TrueMoon и анимированное раскрытие — 2026-10-24

MenuBar использует общий reveal для поворота ⋮→⋯; click/Enter/Space открывает первую
группу после transition, повторный click закрывает. Esc/Tab/outside/FocusLost/detach
отменяют pending open, IInputContext не удерживается между кадрами. More сохраняет
hit area, hover/pressed/focus состояния без обычного бордера. F10/heading navigation
прежние. Добавлен геометрический UiSymbol.TrueMoon с прозрачным вырезом; Editor
выбирает Accent через typed ViewStyle и сохраняет Caption region.

UI11/Editor2 новыхcases. Текущие UI723/Hosting98/Editor467 Debug/Release =1288/config,
6TRX/failed0/skipped0. Source Debug/published Release вне repo:4native cases/28PNG/
4reaped children, validation0/0 и UI sessions/mappings/GPU resources/handles0.
После визуальной коррекции AA seams рисунка весь related/native scope повторён;
финальные collapsed/File PNG просмотрены. Full solution/headless/прочие native
profiles/packages/physical/golden/performance/loss/VRAM/final alpha не повторялись;
прежние отдельные CPU receiver/Mithril fixture failures не объявлены исправленными.
[Контракт](PLAN.md#pa3apa4apa8b-меню-и-компактный-анимированный-заголовок--2026-10-23),
[точные тесты, команды, отчёты и границы](../engine/HISTORY.md#ed4a-знак-truemoon-и-анимированное-раскрытие--2026-10-24).
Evidence: TestResults/HeaderBrand-20261024/verification.json.

## ED4a: компактный заголовок и меню — 2026-10-23

Общие menus/commands/overlays/frame transitions и default title header hook реализованы
в bounded PA3a/PA4a/PA8b scope. Первый consumer — Editor через View/Data/Style и
App/Cobalt/IFactory. UI51+Hosting13 новых cases; UI712/Hosting98 Debug/Release.
Editor41 новых cases, related1275/configuration.10native/header proofs,64PNG,
12reaped children, validation0/0 и zero owned resources;10headless cases отдельно.
[Контракт](PLAN.md#pa3apa4apa8b-меню-и-компактный-анимированный-заголовок--2026-10-23),
[реализация, команды, evidence и все failures](../engine/HISTORY.md#ed4a-компактный-заголовок-и-меню--2026-10-23).

Последний full Debug3048/3049 с Mithril ready-file fixture race; последний
Release3049/3049. Прежний CPU FrameReceiver null также записан отдельной задачей.
Passing related UI не означает общий green solution. Nonincremental build0errors/
1532warnings. Related source/native proof не закрывает PA3 modal stack, fresh
packages/HUD/physical DPI/drag/Snap/golden/performance/loss/VRAM/final alpha.

Архив создан2026-10-09 при разделении общего и модульных планов. [Текущий статус](STATUS.md), [UI план](PLAN.md), [общий статус](../STATUS.md).

## UIA1 View, ViewModel и typed styles — 2026-10-22

По запросу пользователя подход закреплён в AGENTS.md и реализован в Argentis и
Aether.Editor. View<TData> сохраняет Fluent-композицию/явные bindings; данные могут
быть обычными, observable ViewModel не требует framework base class. В Argentis
добавлены IViewStyle<TView>, IViewStyleBuilder<TView>, ViewStyleRule и typed
ViewStyleRole<TElement>. WithViewStyle применяет/заменяет/снимает view layer;
WithRole возвращает конкретный тип элемента. Root/Part/Role правила задаются в
порядке precedence; role token использует reference identity, не имя.

Контракт: local/binding → direct element Style → view style → Theme → default.
Каждый View, включая root вложенного View, является отдельной границей. Part
выбирает живой элемент уже собранного текущего View, roles покрывают будущие
элементы. Adopt/orphan обновляют cached layer. Новые правила не выполняются на
обычных кадрах/Get; theme callbacks вычисляются перед commit. Configure/invalid
part/value failures сохраняют предыдущий слой/Theme. Theme validation выполняется
также до adoption, чтобы nested View не оставлял частично изменённое ownership.
Builder/rules замораживаются после Configure. Observer failures сообщаются после
commit. Style definitions не хранят View; per-view scope хранит выбранные части.

EditorView теперь View<EditorViewModel> с Fluent tree и typed bindings. В
EditorViewStyle вынесены padding/spacing/fonts/colors/heights/action tones;
динамические property rows/entities используют роли. EditorViewModel реализует
INotifyPropertyChanged и хранит presentation strings/selection/availability,
guarded actions; controls/themes/container dependencies отсутствуют. Workspace
и EditorCommands заимствуются. View снимает свои subscriptions, ViewModel отдельно
принадлежит App. Constructor rollback освобождает unpublished tree, сохраняя
original cause и aggregate original+cleanup failures. Root создаётся DI-injected
IFactory на owner thread; Cobalt default и Microsoft DI tests сохранены.

### Проверки

Output root `TestResults/ViewStyles-20261022-214241`, pointer
`TestResults/view-style-current.txt` (далее `$viewRun`), SDK10.0.402/net10.0/xUnit/
VSTest/Windows x64. Добавлены28cases в19methods: view styles18, VM6, factory4;
existing EditorScreen tests мигрированы без удаления проверок поведения.

| Требование | Прямая проверка |
| --- | --- |
| Typed root/parts/roles, scope и порядок | ViewStyleTests.StyleScopes_TypedRootPartsAndRolesRespectBoundaryAndDefinitionOrder |
| Приоритеты/replace/remove | StylePriority_LocalThenDirectThenViewThenThemeThenDefault; StyleReplacementAndRemoval_ClearAllOldSetters |
| Dynamic controls/transfer | DynamicRoleControls_UseNearestStyleAndClearOldScopeAfterTransfer |
| Theme без потери tree/focus/selection/scroll | ThemeChange_RecomputesWithoutRebuildingOrLosingFocusSelectionAndScroll |
| Invalid selectors/Configure/values/whole-tree failures | InvalidParts_RejectBeforeChangingAnyEffectiveValues; ConfigureOrThemeFailure_RetainsPreviousStyleAndSettledFrame; FailedThemeChange_KeepsPreviousThemeAndEffectiveValuesAcrossTheTree |
| Failed adoption/immutable builder | FailedAdoption_ValidatesNestedViewThemeBeforeTransferringOwnership; CompletedStyleDeclaration_CannotMutateRulesOrAddPartsAfterApplication |
| Optional Data и explicit bindings | TypedView_OptionalDataSupportsFluentBindingsWithoutOwningTheModel |
| Model notifications и commands | EditorViewModelTests.Selection_PublishesChangedNamesAndRowsWithoutRepeatedNotifications; Refresh_PublishesOnlyChangedStatusErrorAndAvailabilityValues; Execute_RejectsUnavailableCommandsAndForwardsAllowedActions |
| Model stop/borrowed ownership/UI-independent API | StopAsync_ForwardsShutdownAndWaitsForAcceptedCommand; Dispose_DisconnectsModelAndPreservesBorrowedWorkspaceAndCommands; ModelSurface_HasNoControlThemeOrContainerDependencies |
| View input/theme/borrowed disposal | EditorScreenTests.HierarchyInput_UpdatesReadOnlyGridAndThemePreservesSelectionAndFocus; Disposal_UnsubscribesModelAndKeepsBorrowedWorkspaceAndModelUsable |
| DI constructor failure ownership | DiFactory_BorrowsSharedModelAndStyleWhileSessionsOwnIndependentViews; FactoryFailure_DisposesUnpublishedViewAndPreservesBorrowedModelAndErrors — Cobalt/Microsoft DI и original/cleanup causes |

```powershell
$viewRun = Get-Content TestResults/view-style-current.txt
dotnet build TrueMoon.slnx --configuration Debug --no-incremental -m "-bl:$viewRun/nonincremental-debug-{}.binlog"
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory "$viewRun/full-debug" "-bl:$viewRun/full-debug-{}.binlog"
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory "$viewRun/full-release" "-bl:$viewRun/full-release-{}.binlog"
dotnet publish TrueMoon.Aether.Editor/TrueMoon.Aether.Editor.csproj --configuration Release --output "$viewRun/publish" "-bl:$viewRun/editor-publish-{}.binlog"
```

Все exit0. Nonincremental build0errors/1520warnings (не warning-free build).
Full Debug/Release: **2321/2321/configuration**,10TRX/configuration, failed0/skipped0:
Core117/Cobalt179/generator91/Enerit126/generator114/Mithril99/UI661/Hosting85/
Aether800/Editor49. Адресные Debug UI661/Editor49 также прошли. Assertion-quality/
gap review завершены; отмеченный constructor rollback gap закрыт regression.
Editor tests получили reference на существующий DI adapter только для проверки
обоих providers. UI owner-thread tests завершают UI work до asynchronous awaits.

Итоговый Debug apphost запущен из isolated copy последнего build, published Release
из publish: RunNative.ps1 с --smoke fresh directory --validation, VK_LAYER_PATH
только в собственном process tree. Оба exit0; каждый8commands/3PNG/9material pixel
checks/2reaped children, zero UI sessions/mappings/GPU resources/handles,
ViewDisposed=true. Все4runtime reports: validation enabled,0errors/0warnings,
devices/surfaces/slots created=released, LiveSlots/OpenHandles0. Dark PNG визуально
сверен; native assertions проверяют реальный viewport/input/selection/Replace/close.
Initial pre-final Debug smoke также прошёл, но не заменяет final evidence.

Fresh package proof: ValidatePackages.ps1/VerifyPackages.ps1, output
`view-style-packages`, версия`0.2.2.7-alpha.viewstyle.20261022165426831`.
14fresh packages/20commands/4Cobalt+DI consumer runs, все exit0: Debug build и
Release publish, только PackageReference, собственные feed/cache/source mapping.
Actual typed styles/root/part/roles, theme values, future controls, style removal,
retained data/tree/focus и caller-owned factory results выполнялись в каждом
consumer; ViewStyleContract=true у4records. Повторены прежние Aether/viewport/
GPU/lease ownership и actual Aether/Skia/native payload hashes/immutable packages.
Packages не публиковались. README Argentis/Hosting/Editor обновлены до pack.

VerifyFinal.ps1 повторно сверил tests/native/package JSON и source/docs paths,
записал verification.json:6projects/177C#sources/170namespace declarations,
нарушений0;25documents/674local links/missing targets0; git diff --check exit0.
Это path-link/source namespace scope, не полный аудит historical anchors.
Не повторялись полный Settings golden/RTE, physical
DPI/input, device loss/VRAM/K3/K7/K8/final alpha, performance/allocations и все
независимые module consumer matrices. Автоматический DataContext rebind отсутствует:
смена публичного DataContext сама не перенаправляет explicit bindings; модель
созданного EditorView стабильна. CSS cascade/templates/animations/mass migration
и ENG7 property editing остаются за пределами UIA1.

## PA8a дефолтный квадратный стиль — 2026-10-22

По запросу пользователя реализован согласованный PA8a0–2 и автоматический scope
PA8a3. [Палитра, размеры, контракты и команды галереи](DEFAULT_STYLE.md).
Argentis содержит semantic Theme tokens, local/style/theme overrides, action tones,
inside border/focus/error/read-only markers, square check/slider/progress/icons,
scrollbar с pointer capture/cancellation. Theme switch сохраняет editor selection,
focus, values и scroll. Button constructors/factories используют одну тему.

WindowFrame.CreateDefault/WindowTitleBar/WindowResizeGrip остаются в Argentis.
DefaultWindowFrameBinding находится в Alloy.Runtime, автоматическая композиция —
в Hosting, native grip/focus — в Platform.Silk. App путь и system-decorated default
сохранены; explicit Chrome включает стандартную рамку, TitleBarFactory имеет
приоритет, UseDefaultWindowFrame=false оставляет caller root. Кнопка close проходит
через cancellable asynchronous close coordinator. Fixed/maximized capability,
caption/client/Snap/grip regions и отсутствие перекрытия content проверены.

Skia квадратный stroke рисуется четырьмя неперекрывающимися полосами с прежней
centered geometry; rounded override сохраняет round-rect. Это устранило отличия
сглаживания тонкого бордера между CPU/GPU. Отмеченный checkbox рисует одну solid
поверхность вместо наложения fill/stroke на одном краю. Tolerances не увеличены.

Текущие managed проверки, Windows x64/SDK10.0.402:

| Проект | Debug | Release |
| --- | ---: | ---: |
| Alloy / Hosting |643 /79|643 /79|
| Aether |780|780|
| Mithril / Core |95 /107|95 /107|
| Cobalt / Generator |179 /91|179 /91|
| Enerit / Generator |126 /114|126 /114|
| Всего,9TRX |2214|2214|

Failed0/skipped0 в обеих конфигурациях.28новых cases:25DefaultStyleTests и3raster
border cases. Существующие assertions геометрии scroll viewport/progress/slider,
disabled drawing, enum endpoint и pressed color обновлены на точные новые
ожидания. Assertions не заменялись на presence-only проверки.

| Требование | Проверка |
| --- | --- |
| Общие square defaults, factories, overrides | `DefaultStyleTests.SquareDefaults_ApplyToConstructorsFactoriesAndEditors`, `Appearance_LocalStyleThemePriorityAndClearAreStableAcrossThemeChanges`, `InvalidAppearance_RejectsBeforeMutation` |
| Контраст action text normal/hover/pressed | `DefaultStyleTests.EnabledText_ContrastRemainsAtLeastFourPointFiveAcrossActionStates` —8cases, ratio≥4.5 |
| Форма focus/error и полупрозрачный border | `DefaultStyleTests.FocusAndError_UseInsideSquareOutlineAndStructuralErrorMarker`, `RasterRenderingTests.SquareBorder_StaysInsideBoundsAndPaintsTranslucentCornersOnce` —exact bounds/colors/RGBA на3scales |
| Readonly без изменения selection/clipboard | `DefaultStyleTests.ReadOnly_RejectsMutationWithoutTouchingSelectionOrClipboard` —Backspace/Delete/CtrlX/CtrlV, Unicode, copy/navigation |
| Theme state / idle | `DefaultStyleTests.ThemeSwitch_PreservesFocusSelectionValuesAndScrollAndReturnsToIdle` |
| Scroll capture / secondary / hide / cancel | `DefaultStyleTests.Scrollbar_DragClampsIgnoresSecondaryButtonAndCancellationStopsGesture`, `HiddenScrollbar_CancelsCapturedDragBeforeChangingOffset` |
| Capabilities/commands/regions/content/idle | `DefaultStyleTests.DefaultFrame_CommandsCapabilitiesRegionsAndIdleFollowObservedWindow` —fixed/resizable; native proof отдельно |

Команды и evidence:

- `dotnet build TrueMoon.slnx --configuration Debug --no-incremental -m --verbosity quiet`:
  `TestResults/PA8a/build-debug-current.log`, exit0. Existing warnings сохранены.
- `dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/PA8a/tests-debug-current --verbosity quiet`
  и аналог Release в `tests-release-final`; `TestResults/PA8a/test-summary.json`.
- Новый golden record `TestResults/PA8a/golden-record-final/report.json`:90GPU/current
  comparisons, validation0/0. После визуального просмотра Dark/Light/compact/scale
  кадров30PNG+manifest скопированы в `Golden/SettingsSquare`. Прежний Settings
  baseline сохранён; format/tolerances2/edge radius1/edge24 прежние.
- `RunSettingsGolden.ps1 -OutputDirectory TestResults/PA8a/golden-verify -ValidationLayerPath TestResults/ENG4b/validation-layer/Bin`:
  normal180comparisons/configuration, всего360; current raster/golden, GL/Vulkan,
  current backend comparisons,40steps ×3synthetic scales; validation0/0 включая
  teardown. `summary.json` Passed=true, fixtures unchanged.
- `--style-gallery --style-output <fresh>` и `--default-frame --validation`:
  `TestResults/PA8a/native-current-summary.json`, Passed=true. Debug и published
  Release из golden-verify/publish: по6gallery PNG и4native окна OpenGL/Vulkan ×
  fixed/resizable. Commands, caption/client/Snap/grip hit-test, max/restore,
  close veto и asynchronous save/drain, zero registry/hooks/validation messages.
  Финальные Dark100%/Light150% PNG просмотрены; весь набор имеет6файлов/configuration.
- `AlloyFactoryConsumer/RunIntegration.ps1 -Profile Source`:
  `TestResults/AlloyInfrastructure/pa8a-source-20261022/summary.json`, Completed=true,
 16cases/21commands.8source App Cobalt/DI ×GL/Vulkan ×Debug/published Release и
 8Scheduling/Close/Dispatcher/HUD native runs. Read-only VerifyIntegration прошёл.
- `AlloyPackageConsumer/RunPackageSmoke.ps1 -RunDirectory TestResults/AlloyPackages/pa8a-20261022`:
  Completed=true,10nupkgs,36App+4direct renderer cases, omitted-analyzer negative
  control, native/fonts/analyzer/source fingerprints. Version
  `0.2.2.7-alpha.ui.20261022115217348`. Packaged consumer проверяет новый appearance
  API и default frame, переключение темы/состояние; native cases используют рамку.
  Build/publish Debug/Release, Cobalt/DI/DI без consumer analyzer, raster/GL/Vulkan.
  Пакеты не публиковались.

Промежуточные отказы сохранены: первая сборка3CS1061 из-за отсутствия
NativeWindow.IsFocused исправлена cached GLFW focus/events. Первая UI suite:
596passed/19failed из615 — точные ожидания прежнего оформления; после обновления
615passed. Первый новый test build отверг отсутствующий Color.White; использован
явный RGB. Golden `golden-record` failed4comparisons/22outliers суммарно, после
square bands `golden-record-bands` failed2comparisons/2outliers. После удаления
двойного checkbox edge final record и normal verification прошли без ослабления
допусков. Эти failed directories не считаются passing результатами.

Physical multi-monitor DPI/OS input, real device/context loss, VRAM/K7/full alpha,
новые beta controls и расширенный RTE backlog не закрыты. NVIDIA workaround не
возобновлялся. Engine native/3D/performance matrix на этапе PA8a не повторялась.
Следующая engine точка — ENG5c.3 collisions камеры, затем HUD; UI implementation
не переносилась в Aether.

Финальные checks:83pending C# файла (включая прежнюю engine работу), namespace
errors0; локальные ссылки/anchors изменённых Markdown и whitespace diff прошли.
`TestResults/PA8a/completion.json` Passed=true. Test-generation research/plan,
Roslyn pairing и assertion/gap review сохранены в temporary PA8a scratch;
измеренная coverage/mutation score не заявляется.

## ENG4d2 ordinary GPU Editor — 2026-10-20

UI production APIs не менялись. ManualTests ViewportView/Canvas и App binding
подключают GpuViewportTransport к общим GameEditor commands. Owner callback copies
texture в независимый Skia image, ticket хранится рядом, actual FramePresented
подтверждает только текущую presentation. CPU default/legacy API остаются. Viewport
availability/transport caption использует neutral surface; Start ждёт GL ready.
Background transport failure виден пользователю; cleanup удерживает borrowed UI
owner до завершения. Native ordinary GPU proof повторяет selected A/B DLL/
20Replace/checkpoint/rollback/cancel/close pipeline в одном App/window,53children.
Imported ReadPixels0,47devices/141slots balanced, core/sync validation0/0, resources/
handles/mappings/UI sessions0. Full Debug1898/1898 повторяет UI615/Hosting79;
UI615 Release отдельно. Package consumers проверяют managed transport contracts
и transitive Skia native delivery, не actual GPU rendering из nupkg. Команды,
окончательный snapshot и точные limits —
[engine HISTORY](../engine/HISTORY.md#eng4d2-ordinary-gpu-gameeditor--2026-10-20).
Далее ENG4e measured reference baseline; полноценный 3D renderer, golden/RTE/VRAM/
performance/hardware/final alpha не подтверждены. K7 workaround/driver settings
не менялись. Прежние результаты ниже сохраняют собственные scopes.

## ENG4d retained GPU image bridge — 2026-10-20

Rendering.Skia получил `SkiaOpenGLSurface.CopyTexture` без зависимости от Aether:
borrowed RGBA8/premul/stored-sRGB/top-left texture → независимый retained GPU
SKImage. Last source use submitted до return/throw, source не adopted и pixels не
читаются. Image требует исходный Skia surface/context owner для drawing/dispose
до UI teardown; drawing adapter отклоняет raster/foreign context. Size получает
image один раз, failed owner dispose сохраняет image для retry, disposed use
отклоняется. Existing CPU pixel copy/decoder API остаётся.

UI615/615 Debug/Release, Hosting79 Debug в full Debug1858/1858. После review
`ImageContextGuard_RejectsRasterOrForeignSurfaceAndAcceptsOrigin` проверяет сам
SkiaDrawingContext.Image и actual successful red pixel, чтобы removal draw guard
не проходил через отдельный helper-only test. Solution Debug/UI Release повторены.
`ImageOwnerFailure_RetainsImageForDisposeRetryAndThenRejectsUse` проверяет cause,
живой native wrapper при failure, retry/idempotence и use-after-dispose.

Reference viewport per Debug/published Release:17checks/14captures/20GPU copies/
6children. Independent PNG decoder rechecked3,051,552interior canvas pixels при
actual150% DPI: alpha/background/stored sRGB/origin/bilinear/clipping. Imported
ReadPixels0. Retained image переживает reuse/resize/suspend/Stop/crash; return во
время source use отвергается. WM_CLOSE drains App GPU imports до UI disposal.
Core/sync validation0/0; resources/handles/UI sessions0. Проверяется retained UI
surface после actual presentation, не OS compositor/physical display. GPU producer
source extents16²/64×32/320×180; это не performance/VRAM/zero-copy proof.

Fresh12packages/4Cobalt+DI consumers проверили Aether+Skia APIs, exact managed
DLL identity и transitive native payload. GPU native rendering шёл через source
Debug/published Release, не package consumer; full own UI package/native/golden/
RTE/performance/hardware matrix не повторялся. K3/K7/K8/VRAM/final alpha прежние.
Commands, reports и failed initial100%-DPI reference —
[engine HISTORY](../engine/HISTORY.md#eng4d-gpu-viewport-bridge--2026-10-20).
Ordinary CPU GameEditor остаётся default; следующий shared code шаг ENG4d2.

Ниже целиком сохранён прежний STATUS со всеми датированными запусками, решениями, failures, commands, tables и историческим VCS контекстом. Это снимок перед реорганизацией; его формулировки «сейчас» и «следующий шаг» относятся к указанным датам. Актуальная точка продолжения находится в STATUS. Общие build/другие module failures учитываются в общем и соответствующих локальных статусах; архив не является новым passing run.

Новые подробные записи добавлять датированными разделами истории, текущую сводку обновлять отдельно. Согласованные UI критерии сохраняются.

## ENG4a actual GL capabilities — 2026-10-20

Existing Alloy/OpenGL owner context read-only UUID/driver/extensions/procedures/
RGBA8/tiling/fence-polling queries сопоставлены с App-owned Vulkan child. RTX5070Ti
eligibility допускает следующий import-probe; CPU selected/ImportTested=false.
UI production APIs прежние. Full Debug повторил UI613/Hosting79; native old+new
cases Debug/published Release прошли, новые cases по1reaped child/instance destroyed/
mappings/UI sessions0/authoring preserved. [Подробные tests/commands/raw evidence](../engine/HISTORY.md#eng4a-gpu-capabilities-и-transport-decision--2026-10-20).
Actual GPU imports/pixels/completion, own Release UI/golden/RTE/UI packages/
performance/hardware не повторялись; K3/K7/K8/VRAM/final alpha прежние. Далее ENG4b,
drivers/Windows settings не менялись, K7 workaround search не возобновлялся.

## ENG3d Editor commands и replacement proof — 2026-10-20

Viewport example использует AetherHost.Editor commands/immutable context; UI
production API прежние. Full Debug повторил UI613/Hosting79. Debug apphost и
published Release: classic viewport по17cases/27children/20StartStop; отдельный
editor-proof по20A/B Replace при одном App/window,20failure cases,775actual GL
pixel checks,53children/maxlive1/allreaped/released. Проверены body colors,
DLL hashes, полный restored checkpoint/first tick и новый Step, native close при
held replacement/compiler до UI disposal, authoring unchanged, sessions/mappings0.
[Единственные подробные engine tests/команды/evidence](../engine/HISTORY.md#eng3d-команды-editor-и-20replace-proof--2026-10-20).
ENG3 завершён в bounded CPU scope, далее общий ENG4a capabilities/transport decision.
Own Release UI/golden/RTE/UI packages/performance/hardware не повторялись;
K3/K7/K8/VRAM/final alpha и отложенный RichTextEditor backlog сохраняются.

## ENG3c replacement first-present regression — 2026-10-20

ViewportProbe использует AetherHost.Replacement для selected artifact и сравнивает
полный checkpoint/tick после actual GL first-present. UI production API прежние.
Debug apphost/published Release: по17cases/27game children/20StartStop,
137/135pixel checks, compiler/child drain до UI disposal, mappings/sessions0.
Full Debug повторил UI613/Hosting79; own Release UI/golden/RTE/UI packages/
performance/hardware matrix не повторялись. K3/K7/K8/VRAM/final alpha остаются
открытыми; далее общий ENG3d Editor commands/20Replace proof.
[Подробные commands/failures/fingerprints принадлежат engine](../engine/HISTORY.md#eng3c-serialized-replace-и-rollback--2026-10-20).

## ENG3b restored first-present regression — 2026-10-20

ViewportProbe после Step/capture/Stop запускает checkpoint restore на fresh
generation и подтверждает сохранённые tick/camera/interaction после actual GL
first-present.2runs Debug apphost/published Release: по16cases/26game children/
20StartStop,86/84pixel checks, compiler/child cleanup до UI disposal и mappings/
sessions0. Full Debug повторил UI613/Hosting79. UI production code не менялся;
own Release UI/golden/RTE/UI packages/performance/hardware matrix не повторялись.
K3/K7/K8/VRAM/final alpha остаются открыты; следующий общий шаг ENG3c.
[Подробные commands, failures и fingerprints принадлежат engine](../engine/HISTORY.md#eng3b-checkpoint-и-startup-restore--2026-10-20).

## ENG3a compiler close regression — 2026-10-20

ViewportProbe Closing вызывает terminal AetherHost.DisposeAsync до UI disposal.
Новый smoke case при живой game session запускает held compiler и native close;
проверяет отмену build task и reap compiler/PowerShell worker до освобождения
canvas/window. UI production code не менялся.2runs Debug apphost/published Release:
по15cases/25game children/20StartStop, actual OpenGL pixels, mappings/sessions0.
Full Debug повторил UI613/Hosting79; own Release UI/golden/RTE/UI package/
performance/hardware matrix не повторялись. K3/K7/K8/VRAM/final alpha открыты.
[Подробные commands, failures и fingerprints принадлежат engine](../engine/HISTORY.md#eng3a-compiler-artifacts-и-child-loader--2026-10-20).

## Aether App lifecycle regression — 2026-10-19

Viewport example переведён на UseAether и App-owned AetherHost.Supervisor/Frames.
UI close/drain порядок сохранён, UI production code не менялся. Полный Debug
повторил UI613/Hosting79;2native viewport runs Debug apphost/published Release
прошли с actual GL pixels,14cases/25children/20StartStop каждый, native close
до UI disposal и mappings/sessions0/childrenreleased. Own Release UI/golden/RTE/
UI package/performance/hardware matrix не повторялись; K3/K7/K8/VRAM/final alpha
прежние. [Подробные команды/evidence принадлежат engine](../engine/HISTORY.md#app-integration-реализация--2026-10-19).

## Интеграция TrueMoon.Aether — 2026-10-19

Engine implementation/contracts выделены в Aether; Alloy viewport остался в
отдельном example consumer. UI production code не менялся. В full Debug1490
повторены UI613/Hosting79, два native viewport runs Debug apphost/published Release
прошли с actual GL pixels, native-close-before-UI-disposal и mappings/sessions0.
Own Release UI/golden/RTE/package/performance/hardware matrix не повторялись;
K3/K7/K8/VRAM/final alpha прежние. Подробные
[команды/results/evidence принадлежат engine](../engine/HISTORY.md#truemoonaether--2026-10-19).

## Сверка очередности после INF7 — 2026-10-19

Проверка общего плана: bounded INF завершён, full INF5 и RTE backlog остаются
по потребности UI, аппаратные alpha acceptance прежние. В отложенном
RICH_TEXT_EDITOR_PLAN исправлено устаревшее указание next ENG1; текущий шаг —
ENG3a. Подробные [findings и execution plan принадлежат engine](../engine/HISTORY.md#сверка-плана-после-ui-inf7--2026-10-19).
Production/test/project code не менялся, новых build/tests/native/performance runs
нет; прежний INF7 evidence ниже не является новым runtime run этой сверки.

## INF7: воспроизводимый integration runner — 2026-10-19

По запросу следующего шага временные INF6 workflows заменены воспроизводимым
entrypoint `ManualTests/AlloyFactoryConsumer/RunIntegration.ps1`, normal tracked
Integration/Common.ps1/Package.ps1/Verify.ps1 и Consumer.csproj.template. Нет
substring extraction/dynamic-scriptblock imports из TestResults/другого runner.
[Профили/requirements/запуск](../../ManualTests/AlloyFactoryConsumer/README.md#inf7-воспроизводимый-integration-runner).
Production C#/public API/test project wiring не менялись; existing linked tests
сохранены, новые linked copies не добавлялись. Пользовательский RichTextEditor
edit сохранён, staging/commit/push не выполнялись.

Assembly запускает real Hosting и UI suites Debug/Release, явно отклоняя zero/
failed/skipped/inconsistent TRX. Hosting csproj должен иметь actual ProjectReference
и не компилировать linked Hosting production copies. UI legacy linked helpers
не выдаются за independent assembly proof. Source выполняет8factory processes/
48App cases Cobalt/DI×OpenGL/Vulkan и8native scheduling/close/dispatcher/HUD processes.
Package строит fresh dependency graph/feed/cache и8independent factory apphost runs
без source ProjectReference, inherited CPM и Cobalt analyzer/view registration.
Обязательные Cobalt analyzer/targets и Skia native/targets payloads проверяются
отдельно: disabled-analyzer consumer сам по себе не обнаружил бы missing payload.

Manifest фиксирует selected scope/phases, SDK/OS, owned PID/arguments/exit code/
timeout/logs, reports и SHA256 input set/source/binaries/packages. Проверяется весь
набор commands/reports; новый source file также invalidates snapshot. Native
validation обязательна, только process-local VK_LAYER_PATH с restore в finally;
никаких machine/driver changes. Новый output directory строго внутри
src/TestResults/AlloyInfrastructure; existing evidence не overwrite, delete нет.
Timeout180s/command по умолчанию завершает только owned process tree; ошибки и
partial manifest сохраняются. `VerifyIntegration.ps1` read-only, не rebuild/repack/
rewrite hashes; этот drift proof не является подписанной аттестацией.

### Directed runner guards

```powershell
pwsh -NoProfile -File ManualTests/AlloyFactoryConsumer/TestRunner.ps1 -RunDirectory TestResults/AlloyInfrastructure/INF7-runner-checks-reviewed
```

Passedtrue,21checks без GPU/.NET test execution. [Tracked checks](../../ManualTests/AlloyFactoryConsumer/TestRunner.ps1):

| Requirement | Evidence: check name |
| --- | --- |
| Не overwrite прошлый run / path traversal | `Existing evidence is never overwritten`, `Traversal outside output root rejected` |
| Незавершённый/неполный scope не passing | `Incomplete manifest rejected`, `Partial command matrix rejected` |
| Source/artifact integrity | `Unchanged fingerprint accepted`, `Artifact/source drift rejected`, `New source input invalidates snapshot` |
| TRX nonempty/all-passed/results consistency | `TRX empty rejected`, `TRX failed rejected`, `TRX skipped rejected`, `TRX missing-results rejected`, `Real nonempty passing TRX accepted` |
| Pure package graph и обязательные payloads | `Package assets cannot hide a source dependency`, `Missing analyzer/native archive entry rejected` |
| Literal paths/arguments/UTF-8 | `Paths and arguments with spaces/metacharacters preserved` |
| Nonzero exit и сохранение error/logs | `Nonzero exit fails command`, `Failed process retains exit code and logs` |
| Bounded cleanup только owned tree | `Timeout fails and reaps owned process tree`, `Both owned timeout processes exited` |
| Prerequisite failure до dotnet/native build | `Native prerequisite fails before dotnet/build`, `Failed preflight persists incomplete manifest with zero commands` |

Первый self-test INF7-runner-checks-1 обнаружил codepage loss в controlled pwsh
child: Ω превращалась в ?. Transport и child output явно переведены на UTF-8;
INF7-runner-checks-2 прошёл18checks. Review добавил incomplete command matrix/new
source input guards (20checks), затем missing payload (21checks). Не обнаруживался
production UI defect; первоначальные failed logs сохранены.

### Authoritative All proof

Windows x64, PowerShell7.6.7, SDK10.0.402/runtime10.0.13, VSTest/xUnit2.9.3.
Native validation SDK уже существовал в TestResults/VulkanInterop/tools;
его путь передан параметром, scripts из TestResults runner не читает.

```powershell
pwsh -NoProfile -File ManualTests/AlloyFactoryConsumer/RunIntegration.ps1 -Profile All -RunDirectory TestResults/AlloyInfrastructure/INF7-reviewed -ValidationLayerPath TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin
pwsh -NoProfile -File ManualTests/AlloyFactoryConsumer/VerifyIntegration.ps1 -RunDirectory TestResults/AlloyInfrastructure/INF7-reviewed
```

Summary Completedtrue, Assembly/Source/Package Passed,46successful commands,
394source inputs и840artifact hashes.4TRX: Hosting79 +UI613 Debug и те же Release,
1384passed/failed0/skipped0. Counts берутся из фактического TRX, а не hard-coded
test-count target. Это module suites, не новый full solution run.

24native apphost processes:8source consumers/48App cases;8native probes (каждая
конфигурация scheduling2pairs, close2target backends, legacy dispatcher3pairs,
external HUD90frames/36draws/2sessions/5readbacks);8independent factory consumers.
Debug apphost/published Release, actual GL/Vulkan paths, native refresh, queued
work, resize/suspend/resume, close/save/drain/error/ownership и borrowed-device
cleanup assertions прошли. Native probes проверяют validation включая teardown,
registry/hooks0; public factory consumers проверяют собственные session/dependency
lifetime/registry, internal hook counters из них не объявляются прочитанными.

Fresh10nupkgs version `0.2.2.7-alpha.inf7.20261019130522173`, local only. Assets —
10matching package versions/feed, project dependencies0; собственные consumer
Directory.Build/Packages/global.json/NuGet.Config, source mapping/fresh cache.
Actual analyzer dumps0Cobalt generator, view service registration0. Loaded managed
DLL и native Skia в Debug/publish совпадают с fresh archives;4required payload
hashes сохранены и повторно сверены. Это bounded factory/package matrix, прежняя
36App+4direct/font/RTE full package matrix не повторялась.

Отдельный `-Profile Assembly -RunDirectory TestResults/AlloyInfrastructure/INF7-assembly`
прошёл без Vulkan prerequisite:5commands/4TRX/1384passed, затем read-only verifier.
Source/Package phase functions выполнены внутри All; verifier scope branches
дополнительно проверены на in-memory projection фактического All evidence без
новых native executions. Standalone Source/Package CLI отдельно не повторялись.
Повторный All verifier после standalone Assembly подтвердил отсутствие binary drift.

Предыдущие INF7-initial и INF7-final All прошли до усиления archive-payload guard;
их evidence исторический, authoritative final source — INF7-reviewed. Не выполнялись
новые full-solution/nonincremental build/golden/RTE/engine viewport/32headless/
performance/hardware matrix. Read-only31golden fixtures unchanged проверены отдельно;
старые full Debug1490/engine/Golden passing результаты не объявляются текущими.

Итоговая read-only сверка `TestResults/AlloyInfrastructure/INF7-completion.json`:
Passedtrue;10Markdown files/482local links/206anchors,6PowerShell scripts parsed,
31golden fixture hashes unchanged. All/Assembly manifests повторно проверены;
owned helpers завершены, git diff --check прошёл, staged0, пользовательский
RichTextEditor diff1/1 сохранён. Это проверка evidence/документации, не новый native run.

Bounded INF7 завершён. Общая alpha, full INF5 window/queue/resource projection,
K3/K7/K8/VRAM и deferred RTE остаются; следующий общий code шаг ENG3.

## INF6: явные factories и совместимость — 2026-10-19

Следующий инфраструктурный шаг по запросу пользователя: non-generic
UsePresentation(rootFactory, configure), snapshot до Build/service resolution,
UiRendererKind и nullable GraphicsApi, IUiWindowSessionFactory preflight перед
native allocation. [Принятый контракт](PLAN.md#inf3inf6-условия-интеграции),
[guide/matrix/migration](../../TrueMoon.Alloy.Hosting/README.md#явная-фабрика-root-и-совместимость).
Existing generic overload/Cobalt/DI сохранены; для fully factory-based graph view
registration/generated resolver не требуется. Direct custom standalone factory
теперь явно объявляет graphics capability; external IUiSessionFactory не меняется.
Root null/disposed/parented/attached отвергается до передачи ownership; already-owned
дерево не освобождается. Допустимый returned root очищается на owner при закрытии/
subsequent startup failure. Layer dependencies/rendering algorithms не менялись.
Пользовательский RichTextEditor edit сохранён, staging/commit/push не выполнялись.

### Requirement / Evidence

Новые portable checks — actual Hosting ProjectReference,
`TrueMoon.Alloy.Hosting.Tests.AlloyIntegrationTests`, без linked production copies.
Existing linked AlloyOptions font tests сохранены, массового переноса не было.

| Requirement | Evidence: exact test method / consumer scenario |
| --- | --- |
| Known/custom renderer metadata, positive/negative standalone pairs | `Factory_DeclaresOnlySupportedStandalonePairs` (8cases) |
| Unsupported renderer/API до root/native startup | `UnsupportedWindow_RejectsBeforeRootOrNativeStartup` (4cases) |
| Custom standalone capability обязателен | `UndeclaredCustomWindowFactory_RejectsBeforeCallingCreate` |
| Backend/target mismatch без native callbacks и без root ownership | `WrongTarget_RejectsBeforeHostCallbacksOrRootOwnership` (6cases) |
| Renderer/window snapshot | `Snapshot_FreezesRendererAndEveryMutableWindowSetting`; `AlloyFactoryConsumer.Run` мутирует options сразу после UsePresentation, до Build/service resolution |
| Explicit API mismatch до Services/root, generic и factory пути | `Registration_RejectsExplicitPresenterMismatchWithoutServicesOrRoot` (2cases) |
| Lazy root и null argument guards | `ExplicitRootRegistration_IsLazyAndValidatesArgumentsBeforeRegistering` |
| Invalid enum до native/services | `InvalidGraphicsEnum_IsRejectedBeforeRegistrationOrNativeStartup` |
| Cobalt/DI, owner root start/disposal и dependency lifetime | `AlloyFactoryConsumer.Run`, scenarios `factory`/`legacy`, dependency disposal1/session registry0 |
| Factory exception, null/disposed/parented roots | `AlloyFactoryConsumer.Run`, scenarios `throw`/`null`/`disposed`/`parented`, startup/Completion faults, caller-owned root preserved |
| Attached root не забирается вторым окном | `AlloyFactoryConsumer.Run`, scenario `factory`: rejected duplicate window, first root alive/sessioncount1 |
| Factory path без view registration и Cobalt analyzer | independent package `factory` scenario, Cobalt/DI×OpenGL/Vulkan×Debug/Release; analyzer dump содержит0Cobalt generator |

24новых cases к предыдущим55Hosting. Focused Debug24/24 прошёл с первого test run:

```powershell
dotnet test TrueMoon.Alloy.Hosting.Tests/TrueMoon.Alloy.Hosting.Tests.csproj --configuration Debug --filter FullyQualifiedName~AlloyIntegrationTests --logger trx --results-directory TestResults/AlloyInfrastructure/INF6/focused --verbosity quiet
```

IDE findTests недоступен; существующие conventions прочитаны из Hosting tests.
В HostedUiWindowCloseTests NoSessions теперь декларирует supported graphics, чтобы
старый rejected-dispatcher startup test продолжал проверять прежнюю ветку, а не
случайно проходил на новом capability rejection.

### Current final run

Windows x64/net10.0, SDK10.0.402/runtime10.0.13, VSTest/xUnit2.9.3.
`TestResults/AlloyInfrastructure/INF6/RunValidation.ps1`: hidden owned helper,
child timeout180s/consumer deadline60s; existing validation SDK включён только
process-local VK_LAYER_PATH. Machine/driver settings не менялись.
`final/summary.json` Completedtrue,19commands,136source/178binary fingerprints.

```powershell
dotnet test TrueMoon.Alloy.Hosting.Tests/TrueMoon.Alloy.Hosting.Tests.csproj --configuration Release --logger trx --results-directory TestResults/AlloyInfrastructure/INF6/final/hosting-Release --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Release --logger trx --results-directory TestResults/AlloyInfrastructure/INF6/final/ui-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/AlloyInfrastructure/INF6/final/solution-Debug --verbosity quiet
dotnet build ManualTests/AlloyFactoryConsumer/AlloyFactoryConsumer.csproj --configuration Debug --verbosity quiet
dotnet publish ManualTests/AlloyFactoryConsumer/AlloyFactoryConsumer.csproj --configuration Release --output TestResults/AlloyInfrastructure/INF6/final/consumer-publish --verbosity quiet
```

Hosting79/79 и UI613/613 Debug/Release. Full Debug1490/1490,9TRX/failed0/skipped0:
Core107, Cobalt179/generator91, Enerit126/generator114, Mithril94, UI613, Hosting79,
engine87. INF3 Mithril readiness race не повторилась, fix не вносился; passing run
не доказывает устранение race. Новый nonincremental solution build не выполнялся.

Debug apphost/published Release consumer выполнялся для каждой пары Cobalt/DI ×
OpenGL/Vulkan с mode `all` и validation:8processes/48App cases. По6cases/process,
включая legacy generic, четыре root failure scenarios и new factory success с
дополнительным attached-root rejection вторым окном. Settings мутируются до Build;
native logical size/title/API, owner creation/cleanup, dependency disposal1 и
sessions0 проверяются фактически. Source legacy path использует consumer graph/
analyzer; factory path не регистрирует view service. Internal native hook counters
этот public consumer не читает и не объявляет проверенными.

Отдельно actual AlloyVulkanTest Debug apphost/published Release:
`--async-close --validation --close-output <report>` и `--settings-hud --validation`.
Это ещё4native processes, close2backend cases/config с multi-window/resize/suspend/
resume/cancel/save/drain/cleanup failure, HUD90frames/36draws/2sessions/5readbacks/
config с borrowed device usable после UI/App Stop. Validation including teardown,
hooks и session registry0. Всего source/manual12native processes; package8 ниже
считаются отдельно. Engine viewport и legacy dispatcher/scheduling отдельными
CLI в INF6 не повторялись; их предыдущие результаты не переносятся автоматически.

### Independent packages without generated view resolver

`TestResults/AlloyInfrastructure/INF6/RunPackageProbe.ps1 -RunName packages-reviewed`:
fresh10nupkgs, version `0.2.2.7-alpha.inf6.20261019112009540`, isolated feed/cache/
source mapping и собственные Directory.Build/Packages settings; ProjectReference0,
10matching package assets. Тот же Program.cs, mode factory; consumer csproj задаёт
EnableCobaltGenerator=false, фактический analyzer dump Debug/Release содержит0
TrueMoon.Cobalt.Generator.dll. Cobalt/DI ×OpenGL/Vulkan ×Debug/published Release —
8native consumers прошли, без view registration, с registration-time snapshot,
attached-root rejection, dependency lifetime и registry0, validation0. Loaded
Hosting/Alloy/renderer/App DLL SHA совпадают с fresh packages.249source/10package
fingerprints; Completedtrue. Producer version/config не менялись, пакеты локальные,
не опубликованы. Это новая bounded factory matrix, не прежние36App+4direct/font/RTE
package scenarios. Source consumer использует apphost, package consumer — dotnet DLL
из Debug/published output; RID/self-contained/trimmed/AOT не проверялись.

Первый `packages/` run тоже прошёл8cases, но mutation options происходила после
window resolution. Review усилил regression до Build/service resolution; authoritative
evidence — `packages-reviewed` и `final` с одинаковым финальным Program.cs.

### Initial failures and limits

Initial source build использовал internal hook counters и неверный Children.Add:
CS0122/CS1929 исправлены в probe на public API; production из-за этого не менялся.
Первый native `native-initial` упал на сравнении logical width260 с physical native
size при DPI150%; исправлено на viewport/scale и отдельную affinity assertion.
Callback failure теперь завершает probe TCS, а не ждёт deadline. `native-initial-dpi-fixed`
прошёл factory scenario и затем упал на legacy Cobalt missing generated FactoryRoot
resolver. Для source legacy возвращён явный consumer graph/analyzer; новый factory
overload не изменялся. `native-initial-generator-fixed` прошёл все6cases; после review
усилена registration-time mutation и выполнены свежие final/package runs выше.

Финальная сверка `TestResults/AlloyInfrastructure/INF6/CheckCompletion.ps1`: текущие
source/binary/package fingerprints,11TRX, source/package reports,31unchanged golden
fixtures, Markdown links/whitespace, preserved user edit и staged0. Passedtrue:
11Markdownfiles/472local links/197heading links. Rendering code
в INF6 не менялся, нового golden run нет. CPU/GPU/BDN, engine viewport/32headless,
native RTE, full Release solution и K3/K7/K8/VRAM/final alpha не проверялись.
INF6 завершён в данном scope; следующий UI шаг INF7 — воспроизводимый integration
runner для уже существующих real assembly/factory/package checks. Full INF5 частичен;
engine ENG3 и deferred RTE backlog сохраняют собственную очередь.

## INF4: асинхронное закрытие — 2026-10-18

Реализован следующий инфраструктурный шаг по запросу пользователя. Public
HostedUiWindow API: RequestCloseAsync, Closing, UiCloseReason, IsCloseRequested,
IsClosed, CloseError. Contract подробно в [плане](PLAN.md#inf3inf6-условия-интеграции)
и [Hosting guide](../../TrueMoon.Alloy.Hosting/README.md#асинхронное-закрытие).
UiCloseCoordinator находится в real Hosting assembly; thread-safe acceptance,
owner-only polling/handler start, bounded batch64, async continuation только будит
owner. Native IsClosing отменяется до разрешения close. Forced shutdown ждёт save/
drain, игнорирует veto, не повторяет уже начатый handler; все ошибки доходят до
Completion после best-effort owner cleanup. Legacy closed callback остаётся
lifetime notification включая failure, успешный Closed по нему не определяется.
Engine ViewportProbe использует Closing вместо собственного native interception.
Existing user edit RichTextEditor не менялся; коммита/push в этом шаге нет.

### Requirement / Evidence

Новые tests в `TrueMoon.Alloy.Hosting.Tests`, через ProjectReference без linked copies.
Имена ниже относятся к классам UiCloseCoordinatorTests и HostedUiWindowCloseTests.

| Requirement | Evidence: exact test method |
| --- | --- |
| Worker concurrency, один attempt | `WorkerRequests_ShareAttemptWithoutInvokingHandlersOnWorkers` |
| Sequential owner start, save затем drain, repeat join | `ConcurrentRequests_JoinAndDrainSequentialOwnerCallbacks` |
| Veto и новый retry | `Veto_KeepsOpenAndFreshAttemptCanApprove` |
| Sync/async save errors, observable error и retry | `SaveFailure_IsObservableAndRetryable` (2cases) |
| Null task и canceled handler | `NullHandlerTask_FaultsAttemptAndKeepsOpen`, `CanceledHandler_CancelsUserAttemptButForcedShutdownContinues` |
| Upgrade к forced и veto | `ForcedUpgrade_DrainsPendingHandlerAndIgnoresVeto` |
| Ошибки не пропускают remaining drain | `ForcedFailures_PreserveEveryErrorAfterRemainingDrain` |
| Caller wait cancellation | `WaitCancellation_DoesNotCancelSharedAttempt` |
| Snapshot/bounded callback batch | `HandlerBatch_IsBoundedAndSnapshotIsStable` |
| Responsive shared owner, cleanup affinity/success/failure | `OwnerLoop_StaysResponsiveAndCompletesOnlyAfterCleanup` (2cases) |
| Never-started lifetime/command rejection | `RequestBeforeStartup_ClosesWithoutAllocatingOrCallingHandlers` |
| Pre-canceled public request не принимается | `PreCanceledRequest_DoesNotAcceptClose` |
| Startup fault не превращается в successful closed | `RejectedStartup_FaultsCloseWaitAndNeverReportsClosedSuccess` |

Сначала focused coordinator11/11; затем Hosting53/53 Debug. Первая попытка полного
Hosting build остановилась на xUnit2014/CS0619 в новом pre-cancel assertion:
исправлено на await ThrowsAnyAsync, implementation defect этим не заявляется.
После добавления worker/null regressions итог55cases,16новых к INF3.
Логи `TestResults/AlloyInfrastructure/INF4/focused`, `hosting-Debug.log`,
`hosting-Debug-fixed`; authoritative final suite — ниже.

### Итоговые проверки

Windows x64, net10.0, SDK10.0.402/runtime10.0.13, VSTest/xUnit2.9.3.
`TestResults/AlloyInfrastructure/INF4/RunValidation.ps1` запускался hidden owned
helper; каждый child process bounded240s, close probe deadline60s. Vulkan layer
из уже существующего SDK включён только process-local VK_LAYER_PATH; machine
configuration/драйверы не менялись. `final/summary.json`: Completedtrue,13commands,
132source и90binary fingerprints. Финальные команды:

```powershell
dotnet test TrueMoon.Alloy.Hosting.Tests/TrueMoon.Alloy.Hosting.Tests.csproj --configuration Release --logger trx --results-directory TestResults/AlloyInfrastructure/INF4/final/hosting-Release --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Release --logger trx --results-directory TestResults/AlloyInfrastructure/INF4/final/ui-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/AlloyInfrastructure/INF4/final/solution-Debug --verbosity quiet
dotnet build ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --configuration Debug --verbosity quiet
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --configuration Release --output TestResults/AlloyInfrastructure/INF4/final/publish --verbosity quiet
```

Hosting55/55 и UI613/613 в Debug/Release; full Debug1466/1466,9TRX, failed/skipped0:
Core107, Cobalt179/generator91, Enerit126/generator114, Mithril94, UI613, Hosting55,
engine87. INF3 Mithril readiness race здесь не повторилась, fix не вносился.
Это текущий solution test pass; новый nonincremental solution build не выполнялся.

Из Debug apphost и published Release `AlloyVulkanTest.exe` выполнены:

```text
--async-close --validation --close-output <configuration-close.json>
--frame-scheduling --validation --scheduling-output <configuration-scheduling.json>
--hosted-dispatcher --validation
--settings-hud --validation
```

`AsyncCloseProbe.RunAsync`: по2target backend cases/config (GL и Vulkan с sibling
другого backend); native WM_CLOSE veto, save error/retry, concurrent API join,
canceled wait, оба UI обслуживаются при pending save, resize/minimize/posted work/
restore, owner-affine cleanup, forced App Stop ждёт drain и игнорирует veto.
Отдельный fatal command во время pending drain сохраняет root до завершения
handler; native command + forced handler + root cleanup ошибки сохраняются вместе;
faulted Completion никогда не IsClosed. Registry/hooks/validationmessages0.
Initial Debug close probe прошёл до добавления fatal-command scenario; authoritative
обе финальные конфигурации включают и этот scenario.

Legacy dispatcher3pairs/config, scheduling2pairs/config и external HUD90frames/
36draws/2sessions/5readbacks/config прошли; последний сохраняет borrowed device
живым после UI/App Stop. Это8native manual processes, отдельный fake proof ими
не подменяется. Rendering code в INF4 не менялся; read-only golden не повторялся,
его предыдущие360comparisons относятся к INF3.

```powershell
./ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/inf4-native -IncludeViewport -Scenarios @()
```

Ещё2native processes (Debug apphost/published Release):14scenarios/config,
25child launches/config и20Start/Stop, actual GL pixels, native-close drain нового
API до root/canvas disposal, child EOF/crash/first-frame cancel/deadline,
mappings0/UI sessions0/childrenreleased/editorpreserved. Engine manifest Completedtrue,
332source/76binary fingerprints. Это ENG2 integration rerun, ENG3 не начинался,
32headless ENG1 и собственный engine Release suite здесь не повторялись.

### Независимая поставка

`TestResults/AlloyInfrastructure/INF4/RunPackageProbe.ps1`: fresh7runtime dependency
nupkgs, version `0.2.2.7-alpha.inf4.20261018100226980`, isolated source mapping/cache
и собственные Directory.Build/Packages settings. Только PackageReference Hosting,
7matching package assets, source ProjectReference0. Debug и published Release
native Vulkan consumer прошли: native veto, public save exception/retry, pending
concurrent/canceled wait, session.InvokeAsync во время handler, forced dispatcher
drain/veto/remaining handlers, cleanup owner и IsClosed, registry0. Loaded Alloy/
Hosting/Rendering.Skia DLL SHA совпадают с fresh nupkg;174source/7package hashes.
Пакеты только локальные, не опубликованы; full10package/40consumer matrix не
повторялась. Эти2processes отдельно от10manual/engine native processes выше.

Окончательная сверка `TestResults/AlloyInfrastructure/INF4/CheckCompletion.ps1`
проверяет manifests/fingerprints/TRX/native resource reports, Markdown links,
whitespace и сохранение user RichTextEditor edit. Passedtrue:12Markdownfiles/
482local links/202heading links,31golden fixtures unchanged, staged0. CPU/GPU/BDN/native RTE/VRAM/
physical DPI/K7/K8 и full Release solution не проверялись. INF4 завершён в данном
scope; следующий UI шаг INF6, full INF5 частичен, INF7 сопровождает изменения.

## INF3: запросы кадров и scheduling — 2026-10-18

После INF2/минимального INF5 реализованы frame policy Automatic/OnDemand/Continuous,
coalesced UiSession/HostedUiWindow.RequestFrame, private work signal, monotonic
frame cadence и minimum participant wait на shared owner. Public policy enum
находится в нейтральном Alloy runtime. [Единственный контракт/compatibility](PLAN.md#inf3inf6-условия-интеграции),
[API guide](../../TrueMoon.Alloy.Hosting/README.md#запросы-кадров-и-polling).
Пять слоёв/ProjectReference graph и прежние signatures сохранены; runtime friend
access позволяет Hosting прикрепить private non-user wake без telemetry callback API.

Default Automatic сохраняет polling для existing FramePresented observers;
OnDemand их не считает heartbeat, Continuous задаётся явно. Чистый OnDemand не
вызывает Update/draw/present. Native expose представляет cached image; resize/
DPI/restore могут выполняться immediately, input предварительно синхронизирует
geometry. Minimized viewport empty; suspended queue work выполняется, dirty
сохраняется до resume. Native Poll остаётся bounded до16ms; CPU/GPU idle usage
здесь не измерялся. Direct Run/Pump() сохраняют periodic behavior; Pump(false)
отключает только periodic rendering. New manual-loop resize/state callbacks
отключены в direct Run, чтобы не добавлять nested HUD rendering.

Найденное на native flood уточнение: pending queue/future frame request должны
оставлять last completed output доступным. Добавлен NeedsRender, отличающийся от
NeedsUpdate; raster/GL/Vulkan output helpers проверяют actual visual dirty. Прямое
изменение дерева по-прежнему требует Update. Export не исполняет очередь. Этот
contract явно записан в PLAN; painting/layout/fixture tolerance не изменялись.

Новые **19frame-pump +2owner-scheduling +1raster =22cases**:

| Требование | Evidence: точные имена tests |
| --- | --- |
| Idle/observer compatibility/continuous | `UiFramePumpTests.OnDemand_CleanTreeWithObserversDoesNotUpdateOrPresentUntilRequested`; `Modes_PreserveLegacyObserverPollingAndExplicitContinuousMode` (4rows); `Automatic_ObserverRemovalReturnsToIdleAndAddingObserverResumesPolling` |
| Coalesced wake/budget/FIFO-independent frame/cadence | `UiFramePumpTests.WorkSignal_WakesOnceForFloodAndDrainsWithinTheFrameBudget`; `FrameRequests_CoalesceOutsideQueueCapacityAndRespectMonotonicCadence`; `FullQueue_DoesNotRejectAnIndependentFrameRequestOrInvalidateCompletedOutput` |
| Draw-time request/native cached expose/suspend/resume | `UiFramePumpTests.RequestDuringDrawing_IsRetainedForTheNextFrame`; `NativeRefresh_PresentsCachedImageImmediatelyWithoutMeasureOrDraw`; `Suspended_DrainsPostedWorkOnceAndResumesWithTheRetainedRedraw` |
| Retry/failure/fairness/detach/invalid config | `UiFramePumpTests.SkippedPresentation_RetriesAtTheNextDeadlineWithoutDrawingAgain`; `CallbackFailure_PreservesTheCauseAndPendingWorkForRetry`; `SiblingSession_ReceivesFramesWhileTheFirstContinuouslySelfPosts`; `Detach_RemovesWakeTargetWithoutClosingTheSession`; `InvalidPolicy_IsRejectedBeforeAttachingAClockOrSession` (3rows) |
| Real owner assembly: wait wake and participant isolation | `UiOwnerSchedulingTests.Wake_InterruptsParticipantWaitAndStopDrainsOnTheSameOwner`; `DelayFailure_FaultsOnlyItsParticipantAndPreservesCleanup` |
| Actual raster pixels and dirty-output guard | `RasterRenderingTests.PendingCommandsAndFutureFrameRequests_KeepCompletedPixelsExportableUntilApplied` — old red pixels while Post/request pending, blue after Update, direct green mutation rejects export |

Clock-controlled tests используют injected TimeProvider, без real sleep/global
clock overrides; frame-pump/owner checks относятся к реальной Hosting assembly,
не linked copies. Existing linked UiOwnerLoop tests сохранены. Focused Hosting
сначала38, после full-queue case39/39; runtime-focused72/72. Final UI613/613 и
Hosting39/39 **Debug и Release**, failed/skipped0.

**Failures и поправки, не скрытые финальным успехом:**

- Первый Debug native scheduling failed на `Update UI before using its OpenGL
  output`: bounded queue/future request оставляли NeedsUpdate=true при готовой
  image. Исправлен output guard и добавлены вышеупомянутые behavioral regressions.
- Следующий native run прошёл GL portion, но Vulkan startup остановился без
  validation layer. Финальные processes используют прежний manifest
  `TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin` через process-only
  VK_LAYER_PATH; системные settings/driver не менялись.
- Runtime test build не нашёл новый enum в linked AlloyOptions. Нейтральный enum
  перенесён в runtime вместо копирования новых scheduler sources в tests; затем
  runtime-focused72 прошли. После этого использованы fresh package/final binaries.
- Первый full Debug **1449/1450**, единственный failure — Mithril93/94 readiness
  sharing IOException. Targeted method20/20 и повторный full Debug1450/1450 прошли.
  [Точный Mithril failure/scope](../mithril/HISTORY.md#readiness-file-race-при-ui-inf3--2026-10-18).
  Mithril/probe code не менялся; passing retry не объявляется fix этой race.

Фактические команды Windows x64/net10.0/SDK10.0.402/runtime10.0.13/xUnit2:

```powershell
dotnet test TrueMoon.Alloy.Hosting.Tests/TrueMoon.Alloy.Hosting.Tests.csproj --configuration Debug --logger trx --results-directory TestResults/AlloyInfrastructure/INF3/focused2 --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~RasterRenderingTests|FullyQualifiedName~UiDispatchTests|FullyQualifiedName~UiDiagnosticsTests|FullyQualifiedName~UiOwnerLoopTests' --logger trx --results-directory TestResults/AlloyInfrastructure/INF3/runtime-focused2 --verbosity quiet
& TestResults/AlloyInfrastructure/INF3/RunPackageProbe.ps1
& TestResults/AlloyInfrastructure/INF3/RunFinal.ps1
```

RunFinal выполняет UI/Hosting Release, full solution Debug и nonincremental Debug
build, затем build AlloyVulkanTest Debug/publish framework-dependent Release без RID.
Для каждого apphost выполняются последовательно:

```powershell
AlloyVulkanTest.exe --frame-scheduling --validation --scheduling-output <fresh-report.json>
AlloyVulkanTest.exe --hosted-dispatcher --validation
AlloyVulkanTest.exe --settings-hud --validation
AlloyVulkanTest.exe --settings-golden --validation --golden-output <fresh-directory>
& ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/inf3-final-native -IncludeViewport -Scenarios @()
```

Final full Debug **1450/1450**:
Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI613/Hosting39/engine87,
9TRX/failed0/skipped0; с Release UI/Hosting final11TRX/2102passed.
Nonincremental build **0errors/1211warnings**; warnings не подавлялись.

**Independent package API:**7fresh Hosting dependency nupkgs, version
`0.2.2.7-alpha.inf3.20261018091913737`, isolated feed/source mapping/cache и отдельные
Directory.Build/Packages files. Debug/published Release2/2 без ProjectReference
проверили FrameMode/FPS, RequestFrame до window startup, dynamic redraw, NeedsRender
против NeedsUpdate, snapshots и registry0. Loaded Alloy/Hosting hashes совпали
с fresh packages; producer source172files и7nupkg fingerprints сверены. Это не
прежняя full10package/40consumer Cobalt/DI/fonts/analyzer native delivery matrix.

**Native8AlloyVulkanTest +2engine processes =10/10**, Debug apphost/published Release:

- Scheduling: по2pairs GL/GL и GL/Vulkan, OnDemand даже с observers; idle Update/
  Render counters стабильны, периодического present нет. Posted background change
  проверен actual GL centre pixels;1000requests coalesce; cached WM_PAINT не делает
  новый draw. Проверены sibling refresh/context, resize, minimize/zero, suspended
  task drain, restore,200commands versus sibling redraw, close и registry/hooks0.
- Legacy dispatcher: GL/GL,Vulkan/Vulkan,mixed3pairs/config; shared owner, resize/
  retained selection, native sibling refresh/context restoration, close/recreate,
  startup/command fault isolation, separate scopes, cancel wait/close/cleanup.
- External shared Settings HUD: по90GPU compositions/presents,36UI draws,2sessions,
  5readbacks/config; model/list/input, scene passthrough/alpha, resize/suspend/
  restore, registry0 и borrowed host device после UI teardown остаётся usable.
- Read-only Settings golden:180comparisons/config, всего360; прежние31fixture
  files unchanged, raster/GL/Vulkan current/persisted pixels и40-step model/input/
  focus/scroll/layout checks. Validation включая teardown0errors/0warnings.
- Engine viewport явно выбирает Continuous receiver polling и RequestFrame после
  upload: по14scenarios/25child launches/20Start-Stop, actual GL pixels/counter,
  resize16–1920, suspend/resume/slow UI, EOF/crash, first-frame cancel/15s deadline/
  native close drain. Reports: disposed diagnostics, sessions0/mappings0,
  ChildrenReleased/EditorPreserved, Present count matches host.

Provenance после всех запусков:330engine/production source +85manual/fixture files,
76engine binary +90manual binary fingerprints; golden reports дополнительно
сверяют font/native/source/binary hashes. Игнорируемые artifacts:
`TestResults/AlloyInfrastructure/INF3/final-checks-reviewed/summary.json`,11TRX,
native JSON/logs/golden outputs/publish; `packages/summary.json`, `runtime-focused2/`,
`focused2/`, `mithril-targeted/`, initial failures и `CheckCompletion.ps1`/
`completion.json`; engine — `TestResults/EngineSessions/inf3-final-native/summary.json`.
Первая aborted matrix сохранена в `final-checks/`, не смешана с final reviewed pass.

Документы/links/anchors/whitespace/fingerprints проверены. Пользовательское изменение
RichTextEditor.cs сохранено; staging/commit/push не выполнялись. Native checks
functional/bounded, не idle CPU/GPU speedup или alpha acceptance. Новый BDN run не
выполнялся; pre-INF3 overhead не переносится на текущий source. Full Release
solution/32ENG1 headless/прежняя full package/native RTE matrix не повторялись;
K3 physical DPI/K7/K8/VRAM/final alpha остаются. Shared owner всё ещё polls OS events,
а один медленный callback не прерывается. Full INF5 частичен. Далее INF4 async close,
engine ENG3 и отложенный RTE backlog сохраняются отдельно.

## INF5: минимальная диагностика — 2026-10-18

После INF2 выполнен минимальный diagnostics шаг перед INF3. Добавлены process-local
`UiSession.Id`/`HostedUiWindow.Id`, opt-in `EnableDiagnostics`/window option и
thread-safe value snapshots: lifecycle/viewport/first graphics classification,
queue backlog/depth/high-water/outcomes, отдельные Update/root measure/root arrange/
surface render/host present CPU aggregates. Согласованный API/scope/остаток полного
INF5 — в [плане](PLAN.md#inf3inf6-условия-интеграции), использование — в
[Hosting README](../../TrueMoon.Alloy.Hosting/README.md#диагностика-сессии) и package README.
Прежние constructor/Create signatures и graph зависимостей сохранены.

Collector хранит фиксированные скаляры и пять phase aggregates, без history,
callbacks/tree/device/exception references. Queue updates находятся под прежним
dispatcher gate и collector lock, snapshot берёт только collector lock: user
callbacks/observers под этими locks не выполняются. Observation начинается с
InitialPending; поздний Enable внутри callback не считает уже запущенную команду
или partial Update. Snapshot остаётся доступным после cleanup, включая fault
classification. Failed preflight не объявляется Disposing; terminal reporting и
cleanup observers видят соответствующее состояние.

Hosting `UiPresentationBoundary` измеряет output после Update и до FramePresented;
его tests обращаются к реальной Hosting assembly через ProjectReference и friend
access, без Compile-linked copy. Output result/исходная exception сохраняются;
false=Skipped, exception=Failed. OpenGL boundary включает UI blit/SwapBuffers,
Vulkan — существующий presenter. Boundary сама не переводит session в fault:
это по-прежнему делает existing host exception boundary. External host может
сообщить завершённую попытку через `RecordPresentation`; это не GPU command.

Новые **16UiDiagnosticsTests +4UiPresentationBoundaryTests =20cases**:

| Требование | Evidence: точные методы новых tests |
| --- | --- |
| Opt-in, stable IDs, initial backlog и immutable snapshot | `UiDiagnosticsTests.OptIn_PreservesIdentityAndStartsFromCurrentBacklogWithoutInventingHistory`; `Enable_RespectsOwnerLifetimeAndRejectsInvalidWindowIdentityWithoutPartialObservation` |
| Раздельные phases, clean/render-only и zero-size/resume | `UiDiagnosticsTests.Phases_SeparateLayoutRenderCleanUpdateAndHostPresentation`; `Suspended_StillDrainsCommandsButDoesNotClaimLayoutRenderOrPresentation` |
| Queue capacity/cancel/error/close, поздний Enable и concurrent reads | `UiDiagnosticsTests.Queue_AccountsForCapacityCancellationCallbackErrorsAndCloseWithoutChangingSemantics`; `EnableInsideCallback_DoesNotCountTheAlreadyRunningCallbackOrPartialUpdate`; `ConcurrentSnapshots_PreserveQueueAccountingWhileTheOwnerDrainsWork` |
| Callback/layout/render failure и retry | `UiDiagnosticsTests.PostFailure_IsCountedAndRethrownWhileRemainingWorkCanBeRetried`; `LayoutFailure_ReportsTheFailingPhaseAndPreservesRetry` (2rows); `DrawFailure_DistinguishesRetryableErrorFromTerminalGraphicsFailure` (2rows) |
| Lifetime/fault observers/late Enable/retained cause | `UiDiagnosticsTests.Disposal_GuardFailureDoesNotPretendCleanupStartedAndObserversSeeDisposingThenDisposed`; `ExternalFault_DiscardsPendingWorkBeforeNotificationAndRetainsClassificationAfterDisposal`; `LateEnable_AfterTerminalFailureReportsStateWithoutClaimingEarlierPhaseSamples` |
| Exact external present durations/outcomes и invalid-input atomicity | `UiDiagnosticsTests.Presentation_UsesExactHostReportedDurationsAndValidatesInputWithoutChangingCounters` |
| Реальная Hosting boundary: presented/skipped/failure/off | `UiPresentationBoundaryTests.Presentation_TracksOnlyTheOutputBoundaryAndPreservesResult` (2rows); `Presentation_ObservesFailureWithoutReplacingTheOriginalExceptionOrFaultingSessionItself`; `DiagnosticsOff_DoesNotCreateACollectorOrAlterOutputErrors` |

Фактические команды Windows x64/SDK10.0.402/runtime10.0.13/net10.0/xUnit2:

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Debug --no-build --filter 'FullyQualifiedName~UiDiagnosticsTests|FullyQualifiedName~UiDispatchTests|FullyQualifiedName~RenderingFailureTests' --logger trx --results-directory TestResults/AlloyInfrastructure/INF5/focused-final --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Release --logger trx --results-directory TestResults/AlloyInfrastructure/INF5/final-checks/ui-Release --verbosity quiet
dotnet test TrueMoon.Alloy.Hosting.Tests/TrueMoon.Alloy.Hosting.Tests.csproj --configuration Release --logger trx --results-directory TestResults/AlloyInfrastructure/INF5/final-checks/hosting-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/AlloyInfrastructure/INF5/final-checks/solution-Debug --verbosity quiet
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
& ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/inf5-final-native -IncludeViewport -Scenarios @()
& TestResults/AlloyInfrastructure/INF5/RunPackageProbe.ps1
```

Focused **57/57**, UI **612/612 Debug/Release**, real Hosting **18/18 Debug/Release**.
Full Debug **1428/1428**:
Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI612/Hosting18/engine87,
9TRX/failed0/skipped0; final11TRX/2058passed с отдельными Release UI/Hosting.
Nonincremental Debug build **0errors/1211warnings**; предупреждения не подавлялись.
Первый focused54 был до трёх дополнительных lifetime/fault cases, final57 — после
окончательного source. Initial Hosting18 и final full tests scopes разделены в logs.

**Independent API package proof:** семь fresh Hosting dependency nupkgs, версия
`0.2.2.7-alpha.inf5.20261018083803903`, isolated source mapping/cache и собственные
Directory.Build/Packages files. Только PackageReference на Hosting, assets graph
содержит7matching TrueMoon packages и никаких source ProjectReference; metadata
указывает fresh local feed. Debug и framework-dependent published Release без RID
прошли2/2 с portable fake surface: counters/IDs, suspended/fault/disposed state,
external presentation report, retained snapshot и registry0. Loaded Alloy/Hosting
DLL SHA совпал с содержимым fresh nupkg. Producer source169files/package hashes
сверены; packages не публиковались. Это новый diagnostics API proof, не прежняя
10package/40native consumer Cobalt/DI/fonts/analyzer matrix.

**Native 2/2 ENG2 viewport:** diagnostics включены window option до первого Update;
Debug apphost и published framework-dependent Release. Каждый14scenarios,
25child launches/20Start-Stop, actual GL readback/frame counter, resize16–1920,
zero-size/resume/slow UI, crash/live-child EOF, cancel/15s deadline/native close.
Новая acceptance проверяет WindowId, measure/arrange/render>0, Present.Completed
равен HostedUiWindow.PresentedFrames, Present.Failed0, State=Disposed/Queue.Pending0.
Оба reports также подтверждают sessions0/mappings0/ChildrenReleased/EditorPreserved.
Native327source/76binary fingerprints сверены после проверок.

**Overhead:** task-scoped BenchmarkDotNet0.15.8, DefaultJob, same build on/off;
Windows11 build26300.9711, AMD Ryzen9 9900X, .NET10.0.13 x64 RyuJIT. Dry4cases
проверил execution/setup, затем representative Idle.Disabled DefaultJob и final
4cases (idle и posted/layout/render fake-surface Update ×off/on). Setup/disposal,
initial layout и cached delegates вне benchmark body, MemoryDiagnoser включён.
Каждый benchmark возвращает результат Update; manual timing loops не добавлялись.

```powershell
dotnet add TestResults/AlloyInfrastructure/INF5/Overhead/Overhead.csproj package BenchmarkDotNet
& TestResults/AlloyInfrastructure/INF5/RunBenchmarks.ps1 # build, Dry, representative
dotnet TestResults/AlloyInfrastructure/INF5/Overhead/bin/Release/net10.0/Overhead.dll --filter '*' --noOverwrite --artifacts TestResults/AlloyInfrastructure/INF5/final-checks/bdn
```

| Workload | Disabled mean | Enabled mean | Enabled/disabled | Managed allocation/op |
| --- | --- | --- | --- | --- |
| Clean idle Update |9.530ns |60.524ns |6.35 |0B/0B |
| Posted callback +changed width +fake layout/render |107.4ns |352.8ns |3.29 |176B/176B |

Final means/error/stddev/GC columns сохранены в BDN GitHub/CSV reports; delta
около51ns/245ns относится только к этим uncontended headless workloads. Это
измерение opt-in overhead в новом build, не before/after INF2 comparison, реальный
UI idle CPU, GPU time, whole-frame throughput или performance budget acceptance.
Snapshot reads, enabled collector construction, native presentation и contention
отдельно не benchmarked. Выключенный путь не делает timing/collector allocation;
new process IDs/cached host delegate имеют construction cost, не включённый в BDN.

Ignored evidence: `TestResults/AlloyInfrastructure/INF5/RunFinal.ps1`,
`final-checks/summary.json` и TRX/build/BDN reports, `focused-final/`,
`RunPackageProbe.ps1`/`packages/summary.json`, `RunBenchmarks.ps1`/`benchmarks/`,
`CheckCompletion.ps1`/`completion.json`; native —
`TestResults/EngineSessions/inf5-final-native/summary.json` и viewport reports.
Проверены Markdown links/anchors/whitespace и fingerprints; staged0, пользовательское
изменение RichTextEditor.cs сохранено. Commit/push не выполнялись.

Полный INF5 остаётся частичным: window lifecycle до session creation/failed startup,
Hosting native command queue и resource projection не инструментированы; Vulkan
Resources counters не дублировались. Native Vulkan/validation/WSI, прежняя full
package/golden/native RichTextEditor matrix,32ENG1 headless и full Release solution
не повторялись. K3/K7/K8/VRAM/final alpha, engine ENG3 и отложенный RTE backlog
сохраняются. Scheduling/close policy в этом шаге не менялась. Далее bounded INF3
с explicit frame requests/idle-continuous policy и своим native/HUD proof.

## INF2: ограниченный dispatcher — 2026-10-18

Следующий согласованный UI шаг после INF1 выполнен: `UiSession` использует bounded
FIFO очередь и action budget, появился `InvokeAsync(Action, CancellationToken)`.
Принятые defaults/overloads/overflow/cancel/owner-inline/close semantics сохранены
в [единственном контракте INF2](PLAN.md#inf2-queue-и-dispatch-contract), краткий API
пример — в [Hosting README](../../TrueMoon.Alloy.Hosting/README.md#posted-work-и-awaitable-dispatch).
Старые constructor/Create signatures сохранены. Новый options overload validates
limits до attachment/transfer ownership; прямой Create — также до surface creation.

Acceptance/cancellation/close сериализованы одним gate. Pending cancellation
удаляет callback, освобождает capacity и не расходует Update budget. Cancellation
registrations освобождаются вне gate; task continuations асинхронны. При закрытии
ссылки на pending callbacks освобождаются, каждый task получает отдельную ошибку
с нужной причиной. Failed Dispose preflight guards сохраняют рабочую очередь;
принятый Dispose закрывает её до Root/surface cleanup. Root attachment generation
продолжает фильтровать устаревшие Element dispatch. Terminal graphics error также
faults session; обычная awaitable callback ошибка faults только task.

PropertyBinding явно объединяет latest-value notifications per connection;
overflow сбрасывает pending flag и сообщается source caller, позволяя повторить
notification. CollectionBinding и two-way canonical write semantics сохранены.
Произвольные UI commands/input cleanup не объединяются.

Новые regressions: **23dispatch +3property =26cases**, все на настоящих Alloy/
Argentis ProjectReference с portable fake surface. Матрица точных имён:

| Требование | Evidence: методы в TrueMoon.Alloy.Tests |
| --- | --- |
| Bounded self-posting, draw и соседняя session | `UiDispatchTests.SelfPosting_LeavesTimeForRenderingAndAnotherSession`; `DefaultBudget_BoundsTheOriginalTwentyThousandActionReproduction` — первый Update64callbacks вместо прежних20000 |
| FIFO/capacity/overflow и ownership limits | `UiDispatchTests.Capacity_RejectsOverflowAndRetainsFifoAcrossUpdates`; `InvalidLimits_DoNotTakeRootOrSurfaceOwnership` (4rows); `AwaitableOverflow_IsObservableAndDoesNotReplaceAcceptedWork` |
| Owner-inline/nested dispatch и observable worker errors | `UiDispatchTests.OwnerDispatch_RunsImmediatelyEvenWithFullQueueAndReturnsErrorsAsTasks`; `NestedOwnerDispatch_DoesNotWaitForAnotherUpdate`; `WorkerDispatch_CompletesOnOwnerAndReportsCallbackFailureWithoutSkippingNextCommand`; `PostError_PropagatesButRemainingCommandsCanBeRetried` |
| Cancel до/после acceptance/start, capacity/budget | `UiDispatchTests.PreCanceledDispatch_DoesNotUseCapacityEvenWhenFull`; `Cancellation_RemovesPendingCommandImmediatelyAndReleasesCapacity`; `Cancellation_AfterCallbackStartsDoesNotChangeItsResult`; `Cancellation_DoesNotConsumeUpdateBudgetOrReorderRemainingCommands` |
| Pending tasks/owned cleanup/guard retry | `UiDispatchTests.Disposal_ClosesQueueBeforeOwnedCleanupAndSettlesPendingTasks`; `FailedDisposalGuard_DoesNotCloseDispatch` |
| Terminal failure, owner и worker, original cause | `UiDispatchTests.TerminalCallbackFailure_FaultsSessionAndAllPendingTasks` (2rows) |
| Producer flood/accept versus close race | `UiDispatchTests.ConcurrentProducers_RespectCapacityWithoutLosingAcceptedCommands` — 32accepted/368rejected; `ConcurrentClose_SettlesEveryDispatchWithoutExecutingIt` (Dispose/fault, по1000tasks) |
| Property coalescing/rejected request/stale attachment | `PropertyBindingTests.NotificationFlood_CoalescesLatestValueWithoutConsumingTheCommandQueue` — 20000notifications/одно чтение; `RejectedSynchronization_CanBeRequestedAgainAfterOverflow`; `DelayedSynchronization_AfterTransferCannotOverwriteTheNewAttachment` |

Фактически выполнены Windows x64/SDK10.0.402/runtime10.0.13/net10.0/xUnit2:

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Debug --no-build --filter 'FullyQualifiedName~UiDispatchTests|FullyQualifiedName~PropertyBindingTests|FullyQualifiedName~CollectionBindingTests|FullyQualifiedName~TwoWayBindingTests|FullyQualifiedName~RenderingFailureTests' --logger trx --results-directory TestResults/AlloyInfrastructure/INF2/focused-reviewed --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Release --logger trx --results-directory TestResults/AlloyInfrastructure/INF2/final-checks-reviewed/ui-Release --verbosity quiet
dotnet test TrueMoon.Alloy.Hosting.Tests/TrueMoon.Alloy.Hosting.Tests.csproj --configuration Release --logger trx --results-directory TestResults/AlloyInfrastructure/INF2/final-checks-reviewed/hosting-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/AlloyInfrastructure/INF2/final-checks-reviewed/solution-Debug --verbosity quiet
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
& ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/inf2-reviewed-native -IncludeViewport -Scenarios @()
```

Focused **150/150**; UI **596/596 Debug и Release**, real Hosting assembly
**14/14 Debug/Release**. Full Debug **1408/1408**:
Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI596/Hosting14/engine87,
9TRX/failed0/skipped0. Release отдельно UI596/Hosting14; суммарно final11TRX/2018passed.
Nonincremental Debug build **0errors/1211warnings**, предупреждения не подавлялись.

Native **2/2 bounded ENG2 viewport smoke**: Debug apphost и published framework-
dependent Release без RID. Каждый14scenarios/25child launches/20Start-Stop,
actual GL readback/counter, resize16–1920, zero-size suspend/resume, slow UI,
crash/EOF, first-frame cancel/15s deadline и native close drain. Reports:
ActiveUiSessions0/Mappings0/ChildrenReleased=true/EditorPreserved=true;
source fingerprints325 и binary76 сверены после всех запусков. Это functional
integration, не idle/frame-time performance acceptance.

Ignored evidence: `TestResults/AlloyInfrastructure/INF2/RunFinal.ps1`,
`final-checks-reviewed/summary.json`, TRX/build logs, `focused-reviewed/`,
`CheckCompletion.ps1`/`completion.json`; native reports/source/binary hashes —
`TestResults/EngineSessions/inf2-reviewed-native/summary.json`. Первый focused
run147cases был до трёх дополнительных regressions, последующий150cases и первый
full/native pass предшествовали финальному исправлению повторного использования
exception; они сохранены отдельно, final acceptance относится к reviewed paths.
Исходная diagnostic replay с20000callbacks относится к INF0/INF1 истории;
новый default-budget regression непосредственно проверяет64callbacks.

Проверены локальные Markdown links/anchors и whitespace. Пользовательское изменение
RichTextEditor.cs сохранено; staging/commit/push в этом шаге не выполнялись.
Полный Release solution,32ENG1 headless native cases, packages/golden/native RTE,
physical DPI/K7/K8/VRAM/final alpha не повторялись/не закрывались. Hosting owner-loop
commands здесь не ограничивались, новые wake/idle/frame policies не вводились.
Action budget не прерывает один медленный/рекурсивный owner-inline callback.
Следующий UI шаг: минимум INF5 diagnostics, затем INF3 scheduling; engine ENG3 и
отложенный RTE backlog сохраняются.

## Отложенный план RichTextEditor — 2026-10-17

По запросу пользователя обсуждённые улучшения сохранены в отдельном
[RICH_TEXT_EDITOR_PLAN.md](RICH_TEXT_EDITOR_PLAN.md): RTE0 исходная сверка,
RTE1 editing/history, RTE2 caret/viewport, RTE3 formatting/commands,
RTE4 codec/rich clipboard, RTE5 paragraphs/search, RTE6 measurements,
RTE7 IME/bidi/accessibility. Все этапы запланированы и отложены до дальнейшего
развития UI. API names и использование RichTextKit TextDocument остаются
предложениями для сверки, а не принятыми изменениями текущего API.

UI PLAN/STATUS/API guide и индекс документации ссылаются на единственный детальный
план; общий roadmap содержит ссылку без дублирования задач. Следующий code шаг
остаётся ENG1, межмодульные блокеры и readiness не изменились. В этом шаге
implementation/build/tests/native/package checks не выполнялись; предыдущие
результаты2026-10-17 сохранены со своей датой.

Документационная проверка:16Markdown documents, relative links/anchors,
fences/whitespace/conflicts/NUL и наличие восьми этапов RTE0–RTE7 прошли;
scoped git diff --check чистый с Windows CR-at-EOL policy. Существующие10staged
entries сохранены; новый план не staged. Отчёт ignored:
TestResults/RichTextEditor/plan-validation.json. Первый запуск вспомогательного
checker не определил PSScriptRoot в dynamic scriptblock; после исправления
переиспользуется только Markdown-часть проверки с явно заданным workspace.

## RichTextEditor — 2026-10-17

По запросу пользователя перед headless ENG1 реализован отдельный UI control
поверх Topten.RichTextKit0.6.170. [Контракт PA7a](PLAN.md#pa7a-richtexteditor-перед-eng1--2026-10-17),
[API/options/fonts/binding](RICH_TEXT_EDITOR.md),
[manual demo](../../ManualTests/RichTextEditorTest/README.md).
Это расширение не закрывает весь PA7/beta, UI alpha или engine этапы.

### Реализация и ownership

- Argentis: immutable RichTextDocument/Run/Style/EditorOptions, LF/valid UTF-16,
  bindable Document/Text/Options, multiline editor, grapheme selection/navigation,
  plain clipboard, formatting selection/insertion, bounded undo/redo, read-only,
  wrap/alignment/tab/selection color и обычные Element font/color/padding.
  Новые optional rich-layout/drawing capabilities не меняют базовые interfaces.
  Argentis не получил зависимости от Skia/RichTextKit.
- Rendering.Skia: per-paragraph TextBlock adapter с UTF-16/code-point mapping,
  caret/visual line metrics, selection painting и одним layout для hit testing/
  rendering. Owner-thread/surface checks; layouts освобождаются при reflow/detach/
  dispose. Один adapter используется raster/OpenGL/Vulkan.
- SkiaFontCatalog.LoadFaces регистрирует реальные normal/bold/italic variants;
  прежний Load сохранён. Explicit catalogs используют snapshot bytes и U+FFFD
  для missing glyph без system fallback. Synthetic bold из Regular не обещается.
  RichTextKit содержит process-wide native shaper caches: adapter повторно
  использует face identity, а не создаёт retained shaper при каждом input/session.
  Session Dispose не объявлен очисткой глобальных third-party caches.
- Silk key mapping дополнен; UiSession перед Tab traversal даёт focused control
  обработать Tab. Editor вставляет4spaces при AcceptsTab; Shift+Tab/read-only
  сохраняют traversal. Toolbar — отдельные Button/CheckBox в demo, не часть editor.
- Independent App/package и direct-renderer consumers теперь используют rich
  text; проверяется транзитивная доставка Topten/HarfBuzz/native assets.
  Packaged Cobalt example уточнён до существующего App.Builder(_ => {}) API.

### Фактические проверки

Windows x64/net10.0/SDK10.0.402, xUnit2.9.3/VSTest, SkiaSharp4.153.1.
Пользовательские central package версии сохранены. Own headless suites не повторялись.

| Проверка | Результат | Артефакты внутри ignored TestResults |
| --- | --- | --- |
| UI suite Debug |563passed/0failed/0skipped;31новый RichTextEditor case | RichTextEditor/verification/ui-Debug.trx и ui-Debug.stdout.log |
| UI suite Release |563passed/0failed/0skipped | RichTextEditor/verification/ui-Release.trx и ui-Release.stdout.log |
| Full solution nonincremental Debug build |0errors/1200warnings, exit0; не full-solution tests | RichTextEditor/verification/solution-debug.stdout.log, summary.json |
| Rich editor native demo |6/6:Debug и published Release ×raster/OpenGL/Vulkan; format/edit/undo/redo/options/scroll/cleanup | RichTextEditor/smoke-final/summary.json,6reports, initial/final raster PNG и process logs |
| Independent packages |10fresh nupkgs,36App +4direct runtime processes; Cobalt/DI/DI без analyzer, Debug/Release ×build/publish без RID | AlloyPackages/richtext-20261017/summary.json; version0.2.2.7-alpha.ui.20261017144524509 |
| Negative analyzer control |Ожидаемый build exit1/CS0246; в passing runtime count не включён | В том же package summary/logs |
| Прежний Settings read-only golden |360/360,180/configuration; validation0errors/0warnings including teardown, source fixtures unchanged | AlloyGolden/richtext-20261017/summary.json, Debug/Release/report.json/logs/PNG |

Package summary:67commands,266source fingerprints и265evidence entries; финальное
сравнение source/package/analyzer/native/font fingerprints прошло. Пакеты не
публиковались. Новый rich editor не добавлен в прежние Settings golden fixtures:
он имеет отдельные real-raster pixel tests и6native demo cases.

Команды из src:

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Debug --logger "trx;LogFileName=ui-Debug.trx" --results-directory TestResults/RichTextEditor/verification --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Release --logger "trx;LogFileName=ui-Release.trx" --results-directory TestResults/RichTextEditor/verification --verbosity quiet
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
pwsh -NoProfile -File ManualTests/RichTextEditorTest/RunSmoke.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/RichTextEditor/smoke-final
pwsh -NoProfile -File ManualTests/AlloyPackageConsumer/RunPackageSmoke.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/AlloyPackages/richtext-20261017
pwsh -NoProfile -File ManualTests/AlloyVulkanTest/RunSettingsGolden.ps1 -OutputDirectory E:/source/my/TrueMoon/src/TestResults/AlloyGolden/richtext-20261017 -ValidationLayerPath E:/source/my/TrueMoon/src/TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin
```

Регрессии проверяют rich runs/history/insertion style, invalid Unicode/options,
graphemes/empty paragraphs, property observer/binding consistency, clipboard failure,
read-only/disabled, Tab/keyboard/pointer capture/focus, reflow/wheel, real colored
pixels/static redraw, thread/surface checks, detach/reattach/disposal и shared face
identity across catalogs/sessions. Например:
Formatting_ChangesOnlySelectionAndUndoRestoresRunsAndSelection,
Options_EnterAndKeyboardFormattingUseTheSameDocumentHistory,
WordWrapAndResize_ChangeVisualLinesWithoutChangingDocumentOrSelection,
NativePainting_UsesRichColorsAndHasNoStaticRedraw,
RichFontMapper_ReusesProcessShapingFaceAcrossCatalogsAndSurfaceDisposal.

### Failures и границы вывода

Первый focused run18passed/5failed: найден и исправлен реальный caret reveal defect
при wrap/resize; остальные failures — ошибочные exact exception expectations,
UTF-16 InlineData metadata и отсутствие UiSession.Update в incoming binding test.
Следующий21passed/3failed уточнил property validator exception type. Последующий
compile failure в test helper (Elements вместо Items) исправлен. Focused финал
30/30; затем добавлен shared-shaper-face regression и выполнены полные563/563 в
обеих configurations. Initial demo compile failure App.Builder() исправлен до
App.Builder(_ => {}), финальная demo matrix6/6. Исходные logs сохранены.

Initial raster PNG визуально проверен: padded multiline editor, colored text,
italic/underline, независимые toolbar/options. Scripted input не доказывает весь
physical keyboard/mouse/clipboard/IME путь. Полная bidi caret affinity, IME/
accessibility, rich clipboard, RTF/HTML, lists/tables и long memory/VRAM trends
вне scope; наличие emoji grapheme navigation не доказывает каждый emoji glyph.
K3/K7/K8/VRAM/final alpha прежние. Следующий общий code шаг — ENG1; engine code
и собственные engine проверки этим расширением не созданы.

Документация:15Markdown files/452local links/135anchors, fences/whitespace/
conflict/NUL checks прошли. Финальная повторная сверка266source +265evidence
fingerprints прошла; git diff --check с Windows CR-at-EOL policy чистый,
staged entries0. Артефакт: ignored RichTextEditor/completion.json.
Первый вариант checker ошибочно отключил CRLF normalization без cr-at-eol и
пометил CR в пользовательском Directory.Packages.props как trailing whitespace;
исправлена только настройка проверки, package файл не менялся.
Rider connection на финальном docs этапе вернул HTTP502; read-only сверка
завершена резервным терминалом. Уже завершённые runtime checks не повторялись.

## UI package/Hosting quality — 2026-10-15

Выполнен согласованный5.4/5.5 scope после headless quality. Production fix:
`UsePresentation` регистрировал HostedUiDispatcher через constructor registration,
но Hosting не генерирует Cobalt resolvers. Новый independent Cobalt OpenGL consumer
воспроизвёл `No generated resolver for HostedUiDispatcher` в App.Build, до GPU startup.
Регистрация заменена `Singleton<HostedUiDispatcher>(_ => new HostedUiDispatcher())`;
новой зависимости Hosting→Cobalt и изменений dispatcher ownership нет.

Consumer явно регистрирует `Singleton<ConsumerModel>().Transient<ConsumerView>()`:
generic UsePresentation внутри пакета сам по себе не делает concrete consumer
constructor graph видимым source generator. Один consumer binary запускается на
Cobalt/DI; отдельный variant компилируется с EnableCobaltGenerator=false и работает
на DI без generated graph. Compile-time registration type, module initializer и
consumer Model/View resolvers подтверждают фактическую compilation bundled analyzer.
Analyzer DLL/CodeAnalysis отсутствуют в runtime output; standalone generator nupkg
не создаётся (producer IsPackable=false учитывается).

Runner сохраняет exact commands/PIDs/exit codes, stdout/stderr, case deadlines
(pack/build/restore180s, runtime45s), завершает только own launched process trees.
Доставляются собственные Noto Sans/Symbols и OFL; explicit SkiaFontCatalog передан
все backend/Hosting overloads и direct renderer. Catalog Load валидирует fonts
через native Skia до App.Build; Build проверяется на отсутствие sessions/bindings.
Render/update, layout, two-way binding, theme/posted update, nonblank raster/PNG,
native LoadedPath, сохранение модели и отсутствие sessions/subscriptions после
Stop/Dispose проверены. Model singleton без aliases получает1Dispose; равенство
alias disposal counts providers не заявляется.

### Команды и итоговый package scope

```powershell
pwsh -NoProfile -File ManualTests/AlloyPackageConsumer/RunPackageSmoke.ps1 `
  -RunDirectory E:/source/my/TrueMoon/src/TestResults/AlloyPackages/quality-20261015-final
```

Final `summary.json`: Completed=true/Failure=null, version
`0.2.2.7-alpha.ui.20261015171322569`,67commands,263source fingerprints и265evidence
fingerprints. Windows NT10.0.26300.0/x64, SDK10.0.402/runtime10.0.13/net10.0,
framework-dependent без RID. **10nupkgs**, matching producer/package runtime/analyzer
и native hashes; isolated TrueMoon source/cache; **36App +4direct renderer processes**.
Generated Cobalt/DI и DI-only ×Debug/Release ×build/publish ×Raster/OpenGL/Vulkan
прошли. Direct renderer имеет только3local UI runtime packages без App/Hosting/
Core/Contracts/platform;4raster processes прошли. Всего6successful consumer builds
и6publishes, каждый без compiler warnings/errors. Negative control без analyzer
и с compile-time generated reference завершился ожидаемым **CS0246/exit1**; в40runtime
passing cases не включён. Финальные source/nupkg/copied source/generated/native/font
hashes совпали;12raster PNG сохранены. Source code и packaged docs после final run
не менялись; дальнейшие правки planning/status документов в producer inputs не входят.

Pack logs:750warning entries (не unique defects):CS1591=687, nullable
CS8602/03/04/18/25/29=59,CS9113=2,NU5100=1,RS1041=1. NU5100 относится к native DLL
вне managed lib; net10 Cobalt analyzer требует .NET10 compiler. Warnings сохранены,
NoWarn не добавлялся; общий warnings policy/release readiness этим run не закрыты.

### Регрессии и read-only golden

Точные args/exit codes/TRX находятся в
`TestResults/AlloyPackages/verification-20261015-171053691/summary.json`.
Запуск для каждого project и каждой Debug/Release configuration:

```powershell
foreach ($project in @('TrueMoon.Alloy.Tests', 'TrueMoon.Tests', 'TrueMoon.Cobalt.Tests', 'TrueMoon.Cobalt.Generator.Tests')) {
  foreach ($configuration in @('Debug', 'Release')) {
    dotnet test "$project/$project.csproj" --configuration $configuration `
      --logger "trx;LogFileName=$project-$configuration.trx" `
      --results-directory TestResults/AlloyPackages/verification-20261015-171053691 --verbosity quiet
  }
}
```

UI **532**, Core **107**, Cobalt runtime **179**, generator **91** passed отдельно в
каждой configuration;8TRX/**1818passed/0failed/0skipped**, все exit0. Это выбранные
module regressions, не full-solution tests. Production code между этим run и final
package run не менялся; финальные дополнительные harness checks относятся к copied
source fingerprints и raster pixels.

```powershell
pwsh -NoProfile -File ManualTests/AlloyVulkanTest/RunSettingsGolden.ps1 `
  -OutputDirectory E:/source/my/TrueMoon/src/TestResults/AlloyGolden/quality-20261015 `
  -ValidationLayerPath E:/source/my/TrueMoon/src/TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin
```

Debug build и Release publish/probe exit0: **360/360** full comparisons,
180/configuration, fixtures unchanged; ValidationPassedIncludingTeardown=true в
обоих reports. NVIDIA RTX5070Ti/OpenGL3.3/NVIDIA617.42; прежние fonts,30reference PNG,
3logical scales,40-step Settings script и tolerances сохранены. Не physical DPI
transitions и не WSI soak. Это отдельный source consumer proof, а не пакетный run.

### Сохранённые промежуточные failures

- `quality-20261015-a`: consumer build прошёл, runner ожидал неверное имя generated
  resolver file. Проверка заменена на фактический encoded filename pattern.
- `quality-20261015-b`: Cobalt raster прошёл; Cobalt OpenGL/App.Build воспроизвёл
  missing HostedUiDispatcher resolver. Runtime defect исправлен explicit factory.
- `quality-20261015-c`:36+4cases и negative control прошли после fix. Затем добавлены
  final copied-source fingerprints/nonblank raster check и документы; final run
  выполнен из нового feed/cache/version, старые evidence не перезаписаны.

Full solution build, Enerit/Mithril suites, performance, physical DPI/K3,
K7/lifecycle long soak, real loss/K8, VRAM и final alpha matrix здесь не выполнялись.
NVIDIA workaround search остаётся остановлен; driver/Windows settings не менялись.
Пакеты не публиковались. Следующий выбор — доступная UI acceptance на подготовленном
окружении либо отдельно согласованный bounded ENG0; engine/PA1–PA10 не начинаются
автоматически. Общая quality matrix обновлена с сохранением этих ограничений.

Итоговая независимая сверка `final-verification.json`:40package processes,
1818unit passes,360golden comparisons;263source +265evidence fingerprints совпали.
11изменённых Markdown documents/375local links/117heading links:0issues,
fences/conflict markers/NUL/replacement characters проверены;git diff --check
passed, staged changes0. Это validation итоговых artifacts/docs, не новый suite run.

## Package и независимый UI consumer — 2026-10-11

Выполнена подготовка поставки5.4/5.5. Новый `ManualTests/AlloyPackageConsumer` не включён в solution, имеет собственные Directory.Build.props/targets/Directory.Packages.props и только PackageReference на Hosting/App/Microsoft DI adapter. Его собственные model/view проверяют layout, двухстороннюю binding, смену theme, posted update, static raster redraw, сохранение модели, tree/subscription/session registry cleanup и App startup/shutdown/disposal. ProjectReference/compile-link library sources отсутствуют. Скрипт копирует consumer в новый ignored run, пакует source graph, проверяет local package origin в isolated cache и запускает native cases последовательно.

Первый `first-consumer` run: pack/restore/build/publish Debug exit0, но extended native Skia **не доставлена** в consumer output транзитивно через Hosting. Старый contentFiles/copy-to-output и успешные ProjectReference consumers не являлись доказательством transitive NuGet delivery. Исправление renderer: DLL в nupkg `nativeAssets/win-x64/`, canonical `build/TrueMoon.Alloy.Rendering.Skia.targets` с None/TargetPath/PreserveNewest для build/publish и `buildTransitive` forwarder. Runtime output остаётся `truemoon-native/win-x64/libSkiaSharp.dll`, loader/ABI/production ownership не меняются. Первый `transitive-fix` packaging run дополнительно выявил SDK RecursiveDir: folder PackagePath создавал build/build и buildTransitive/buildTransitive; явные PackagePath filenames исправили layout. Эти failures сохранены, не скрыты последующим успехом. Canonical sibling import — обязательный package contract, отсутствие targets приводит к build failure. Custom Copy targets/NoWarn/cache/driver workaround не добавлялись.

В Hosting/Rendering.Skia nupkg добавлены самостоятельные `PACKAGE.md` и descriptions: подключение, controls/binding, backend choice, thread/resource ownership, migration и deployment ограничения. README в nuspec и наличие файлов в окончательных archives сверены. Root package version не изменена; локальные probe версии задаются CLI. Alpha/NuGet публикации нет.

**Финальный текущий run:** Windows x64, .NET SDK10.0.401, net10.0, framework-dependent без RID, Microsoft DI adapter. Команда из src:

```powershell
pwsh -NoProfile -File ManualTests/AlloyPackageConsumer/RunPackageSmoke.ps1 `
  -RunDirectory E:/source/my/TrueMoon/src/TestResults/AlloyPackages/final-contract
```

- Probe package version `0.2.2.7-alpha.consumer.20261011065747061`;11packages собраны,10TrueMoon runtime packages восстановлены именно из local feed в isolated cache. Analyzer package входит в pack graph, но не в restored runtime graph; его delivery/исполнение не проверяются этим Microsoft DI UI consumer.
- Transitive consumer: Debug/Release build и publish — exit0,0warnings/0errors. **12/12 отдельных процессов passed**:3backends ×2configurations ×2output kinds; raster6script steps +PNG, OpenGL/Vulkan20presented frames каждый. Skia не инициализируется на App configuration/Build, после render загружается только output DLL; Vulkan interop ABI проверен. App stop/disposal выполняется после window completion либо owner-thread raster disposal; tree/binding/registry cleanup и сохранение модели подтверждены.
- Direct renderer consumer: только Rendering.Skia PackageReference; dependency graph без Hosting/App/Core/Contracts/window platform. Debug/Release build и publish exit0/0warnings/0errors; **4/4 build/publish raster процессов passed**, загружена output DLL. Это дополнительная проверка прямого package hook и независимого runtime пути.
- SHA-256 native DLL во всех8build/publish output совпал с source: `96525E8EFC1E3BB4B9C64000307D4D11C378FEDAEEB7C1E62D518EF84CDD1ABE`.
- 11pack logs сохраняют **780warning entries**: CS1591=715, CS8602=3, CS8603=35, CS8604=3, CS8618=15, CS8625=1, CS8629=2, CS9113=2, NU5100=1, NU5128=2, RS1041=1. NU5100 относится к native DLL вне managed lib; NU5128/RS1041 — Cobalt generator. Не объявляется zero-warning production pack или закрытие COB4.
- Artifacts: ignored `TestResults/AlloyPackages/final-contract` — summary,245input fingerprints,11nupkg SHA-256, local cache/source metadata, project.assets.json и imports, copied consumer source, Debug/Release output/publish,12JSON reports/4PNG, direct source/output, logs. Предыдущие `initial`, `first-consumer`, `transitive-fix`, `targets-layout-fix`, `final-consumer` сохранены с их собственной областью/версией; только `final-contract` соответствует окончательному обязательному forwarder import.

**Границы:** unit/full solution tests и исходный ProjectReference manual regression не запускались отдельными командами в этом шаге; Release pack builds source dependency graph. Vulkan validation, HUD/полная Settings comparison, full golden/fixed fonts, physical DPI/chrome, controlled/real loss, long lifecycle/VRAM/driver resources не проверялись.4consumer builds/4publishes и16process smokes — проверки поставки, не полный K1–K9 проход. Self-contained/single-file/trimming/AOT/ARM64/другие OS и Core5/COB4 independent headless/analyzer scope открыты. Внешний NVIDIA K7 блокер и решение отложить app-side workaround сохраняются. Следующий независимый UI шаг — fixed-font goldenK2/K4.

## Дальнейшие UI задачи и внешний K7 блокер — 2026-10-10

По решению пользователя текущий NVIDIA GDI defect пока учитывается как внешний блокер, нерешённый со стороны TrueMoon. Дальнейший поиск app-side workaround прекращён; это не утверждение о всех NVIDIA drivers и не отмена K7/alpha acceptance. Private/handles growth сохраняется как отдельная неопределённость. Возврат к полному K7 требует supported driver/ICD решения либо отдельно согласованной и полностью проверенной alternate GPU configuration; raw AMD evidence не заменяет full UI проверку.

Сверены планы, package metadata и текущие consumers. AlloyTest/AlloyVulkanTest используют ProjectReference. Rendering.Skia включает custom native DLL через contentFiles/PackageCopyToOutput, но отдельный package consumer ещё не проверен. Fixed-font assets/full golden fixtures отсутствуют, SkiaDrawingContext загружает системный typeface через SKTypeface.FromFamilyName. Поэтому ближайшая независимая задача — подготовка поставки5.4/5.5: локальный feed, отдельный net10.0 consumer только с PackageReference, transitive/native build/publish доставка, raster/OpenGL/Vulkan и App startup/shutdown Debug/Release, документация подключения/ownership. Следом — K2/K4 fixed fonts и полные golden fixtures. Подготовка пакетов не разрешает публикацию alpha; physical DPI/4.9 matrix, real loss, VRAM и итоговый K1–K9 сохраняются, PA1/PA2 остаются после alpha.

В этом шаге выполнены только чтение исходников/планов и правки документации. Новых build, test, publish или native results нет; предыдущие WSI/dispatcher результаты сохраняют свою дату и область вывода.

---

# TrueMoon.Alloy: состояние реализации и точка продолжения

Дата последней работы: **2026-10-09** — реализован Windows DPI contract4.9d/K3 и выполнены текущие build/tests/GPU/publish. **12/12 focused DPI, 463/463 UI, non-incremental solution build0errors/1341warnings, оба Release publish exit0, regression32/32.** На доступном native DPI144 теперь UiScale1.5. Итоговый alpha проход не завершён: физические100/200% и переходы между разными DPI ещё не проверены.

Исторический запуск **2026-10-08**: **21/21 focused chrome**, **451/451 UI**, **0 ошибок / 1341 предупреждение** при non-incremental solution build, оба Release publish exit0. Тогда physical input, Snap bar, desktop alpha и regression20/20 прошли отдельно в Debug/published Release; validation0/0, hooks0. Эти результаты не заменяют текущие проверки DPI ниже.

## 4.9d / K3: native Windows DPI — реализация и запуск 2026-10-09

**Изменения реализации.** [SilkWindowHost](../../TrueMoon.Alloy.Platform.Silk/SilkWindowHost.cs) создаёт HWND в scoped per-monitor-v2 thread context с восстановлением caller context; размеры создания/Resize — логические96-DPI единицы. UiViewport сохраняет физические framebuffer dimensions и получает Scale=GetDpiForWindow/96 вместо framebuffer/client ratio. Mouse coordinates нормализуются один раз, wheel delta сохраняется. Raw NativeWindow.Size/Position остаются физическими. Первоначальный resize выполняется до показа окна. Process-wide DPI settings новый adapter не изменяет.

Новые [WindowPixelScale](../../TrueMoon.Alloy.Platform.Silk/WindowPixelScale.cs) и [Win32WindowDpi](../../TrueMoon.Alloy.Platform.Silk/Win32WindowDpi.cs) вычисляют размеры и обслуживают WM_GETDPISCALEDSIZE: logical client сохраняется, standard frame добавляется через AdjustWindowRectExForDpi, WM_DPICHANGED rectangle применяет GLFW. DPI subclass/GC root и borrowed HWND имеют owner-thread cleanup; ошибки callbacks откладываются до managed loop. Viewport/Resize/Run защищены от DPI virtualization caller context.

[Win32WindowChrome](../../TrueMoon.Alloy.Platform.Silk/Win32WindowChrome.cs) использует текущий HWND DPI для caption input/menu и track limits; minimum округляется вверх, maximum вниз. Snapshot с прежним размером/Scale не участвует в caption hit testing, при DPI message map/pending input сбрасываются, native maximize capture отменяется. [HostedUiWindow](../../TrueMoon.Alloy.Hosting/HostedUiWindow.cs) синхронизирует viewport/layout/regions до input при изменении размера/Scale. UI tree/model сохраняются. Public API, пакеты, native Skia ABI, net10.0/xUnit и K1–K9 не менялись; уточнение контракта явно внесено в PLAN и README.

**Новые проверки и адаптация probes.** [WindowDpiTests](../../TrueMoon.Alloy.Tests/WindowDpiTests.cs) compile-link только pure coordinate helper, без новой зависимости UI tests на Platform.Silk. [WindowDpiProbe](../../ManualTests/AlloyVulkanTest/WindowDpiProbe.cs), `--window-dpi --dpi-output <JSON>`, проверяет native/session Scale, logical resize, limits, exactly-once client click, focus/selection, доступные мониторы и cleanup. Directed WM_GETDPISCALEDSIZE96/144/192 записывается отдельно от physical DPI transitions. Отдельно проверен DPI-unaware caller: HWND per-monitor-v2, exact viewport/resize, восстановленный thread context и hooks0.

Chrome/Snap probes переводят logical hit targets в physical pixels; raw client edges и hover dismissal уже physical и не масштабируются повторно. Win+Z detector учитывает native right edge, hover остаётся привязан к custom maximize button. Settings HUD перед вводом синхронизирует viewport, использует logical host Resize и масштабирует координаты readback. ProbeWindow пропускает presentation/export при empty viewport после minimize.

**Фактически выполненные проверки текущего кода.** SDK10.0.401, net10.0, xUnit2.9.3/VSTest. Native процессы выполнялись последовательно, validation layer manifest передавался через process VK_LAYER_PATH:

- Focused: `dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --filter FullyQualifiedName~WindowDpiTests --logger "trx;LogFileName=dpi-focused.trx" --results-directory TestResults/AlloyDpi -v:q` — **Passed12/Failed0/Skipped0**. Вся UI suite, тот же project с `--no-build --no-restore`, dpi-ui.trx — **Passed463/Failed0/Skipped0**. Это UI suite, не все solution tests.
- `dotnet build TrueMoon.slnx --no-restore --no-incremental -v:q` — **0errors/1341warnings**. `dotnet publish` обоих manual projects с `-c Release --no-restore` в publish-backends/publish-standalone — exit0. Последний solution build/backend publish выполнен после исправления HUD probe.
- Debug/published Release `--window-dpi --validation` — по **12samples**, native DPI144/Scale1.5, logical600×400→physical900×600, оба backend ×3appearance modes ×standard/custom frame. Logical resize/input, fractional limits, caption hit, retained focus/selection, minimize/restore и hooks0/validation0/0 прошли. По **36directed scaled-size requests**, **1monitor**, **0physical DPI transitions**, unavailable96/192. Преобразования100/200% в unit/directed probes не выдаются за физический проход.
- Debug/published Release `--window-chrome --validation` — по **8windows/116directed hits**, negative coordinates, all resize codes, stale geometry, ceil/floor limits, exact logical resize, bounded/unbounded maximize/work area, owner/disposed guards. Hosting title/queued commands и title-factory failure прошли для GL/Vulkan, registry/hooks0. `--window-snap --validation` — по **8cases** hover/click/cancel/outside release/disabled/keyboard/menu/double-click/lifetime, validation0/0. Directed input не объявляется physical input.
- Debug/published Release `--window-appearance --custom-chrome --native-snap --validation` — по **6cases/80samples/240RGB/48native per-pixel targets**; без custom chrome — по **12cases/160samples/480RGB/96targets** с decorated/borderless. Все Matches=true, source checks и released resources, hooks0/0, validation0/0. Resize/forced paint/minimize-zero-size-restore проходят на двух desktop backgrounds. Это desktop alpha evidence при150%, не fixed-font golden.
- Final `run-regression.ps1` плюс `-RepairFlagCases`: **32/32 actual cases** — все перечисленные DPI/chrome/Snap/alpha/compare/HUD/controlled failure probes и16standalone smoke (Debug/Release ×GL/Vulkan ×ordinary/custom/per-pixel/opacity). Shared comparison:3scales×3backend,40steps/29draws; HUD по90compositions/36UI draws/2sessions/5readbacks. Failure/recreate проверяет terminal injection, borrowed lease, registry/errors/Completion и новые окна, physical device/context loss не индуцируется. Standalone по40frames, disposal/validation0/0; нерегулярный прежний Name mismatch этим успешным прогоном не объявляется исправленным.
- Debug `--window-snap-input --validation --capture-snap --snap-output TestResults/AlloyDpi/debug-input.json` — **6cases/150checks**, failed0, Failure=null, SettingsUnchanged=true, hooks0/0, native DPI144/UiScale1.5. Physical drag/resize, editor exclusion/focus, exactly-once maximize/work area, drag restore/double-click, caption right-click menu, edge/4corner Snap, hover и Win+Z после каждого этапа прошли. По6skipped physical Alt+Space: global PowerToys.PowerLauncher interception. Все12 Debug hover/keyboard context PNG визуально сверены: настоящая Shell grid, Win+Z с цифровыми подсказками. Numeric target HWND не выдаётся за полные flyout bounds. Настройки Windows/PowerToys не менялись, injected keys/buttons и курсор восстановлены.

- Published Release тот же `--window-snap-input --validation --capture-snap --snap-output TestResults/AlloyDpi/release-input.json` — также **6cases/150checks**, failed0,6skipped Alt+Space, Failure=null, SettingsUnchanged=true, hooks0/0, native DPI144/UiScale1.5, validation0/0. Все12 Release hover/keyboard context PNG визуально сверены; вместе с Debug **24PNG** подтвердили настоящую Shell grid/Win+Z. Snap bar этим запуском не проверяется.

Артефакты текущего запуска: ignored `TestResults/AlloyDpi` — focused/UI TRX, solution-build/publish logs, debug/release-{dpi,chrome,snap,alpha,decorated-alpha,compare,hud,failure} logs/JSON, app smoke logs, regression.json и repair log, debug/release input JSON/PNG. regression.json после repair содержит32exit0 и отдельные правильные Arguments у10повторённых cases; scope каждой проверки подтверждён её собственным log/report, одного exit0 недостаточно.

Финальная независимая сверка `verify-docs.ps1` прошла:5Markdown/14source files,223local links, fences/whitespace/conflicts/NUL; TRX12/463 без failed/error/aborted, все текущие DPI/input/alpha JSON counts и32unique exit0 cases с проверкой actual scope10повторённых probes. Scoped git diff --check прошёл. Рабочее дерево:92entries(39modified/53untracked),0staged/0conflicts, против88(39/49) в начале этапа. Четыре новых source files автоматически добавлялись IDE в index; убрано только их staging, рабочие файлы и прежние изменения сохранены. Native probe процессов после завершения нет.

**Промежуточные неуспехи и исправления.** Probe изначально пытался присвоить read-only selection, затем настроен через keyboard input. Empty viewport при minimize обнажил export до первого render: helper теперь пропускает presentation. При переводе awareness scope в allocation-free struct исправлены constructor visibility/default conditional. Дополнительный unit test сначала использовал несуществующий TextBox constructor, заменён на Value initializer. Первый physical Win+Z detector искал меню под custom button: PNG показал реально открытый flyout справа; поиск keyboard target исправлен, финальные Debug/Release matrix прошли. Общий PowerShell runner склеивал single-argument flags с --validation:5types×2configuration тогда запускали default smoke и не считаются соответствующей evidence; аргументы исправлены, все10cases повторены. Настоящий HUD check затем обнаружил logical/physical readback mismatch, исправленный в helper; final Debug/Release HUD прошёл. Ни один промежуточный failure не считается passed.

**Оставшиеся блокеры и следующий конкретный шаг.** Доступна Windows11 build26300/NVIDIA RTX5070Ti, один монитор с DPI144(150%). Native contract реализован и проверен на нём; **полный K3/4.9d ещё не закрыт**. Следующий шаг — physical100/200% и перенос окна между мониторами с разными DPI (туда/обратно, GL/Vulkan ×3modes ×standard/custom): logical size/focus/selection/capture, fresh regions/input, desktop alpha, minimize/restore/cleanup. Использовать подготовленное окружение, не менять пользовательские Windows settings. Затем оставшаяся settings off/Aero Shake/physical cancel matrix4.9, fixed-font golden, long lifecycle/VRAM, real loss и consumer evidence. Текущий запуск не повторяет Snap bar proof2026-10-08 и старые4.9b opacity/legacy capture failures.

| Требование | Свидетельство текущего запуска |
| --- | --- |
| Native Scale и logical client100/150/200% | WindowDpiTests.Scale_UsesNativeDpiEvenWhenFramebufferEqualsClient —3cases; WindowDpiProbe.Run —12samples/configuration только physical150% |
| Input/hit targets и negative capture coordinates | WindowDpiTests.Pointer_PreservesLogicalHitTargetsAndNegativeCapturedCoordinates —3cases; client click/native Snap input на150% |
| Ceil min/floor max/nearest resize | WindowDpiTests.Limits_FractionalSizesRoundInward_ResizeRoundsNearest —3cases; native fractional limits150% |
| Logical size при переходе | WindowDpiTests.Transition_ResizesPhysicalClientAndRetainsLogicalSize; directed scaled-size36/configuration; physical переход не проверен |
| Focus/selection при смене только Scale | WindowDpiTests.Session_DpiChangeWithoutPixelResizeRetainsFocusAndSelection; native resize retained editor150% |
| Invalid/overflow values | WindowDpiTests.InvalidDpiAndUnrepresentableDimensionsAreRejected |

Записи сверки и планирования ниже сделаны до реализации этого этапа; их фразы «следующий шаг — определить DPI contract» являются историческими. Актуальные сводка и точка продолжения обновлены ниже.

## Планирование UI после alpha — 2026-10-09

По запросу пользователя предложения добавлены в [план развития UI после alpha](PLAN.md#развитие-ui-после-alpha). Согласованы цель beta на одном используемом редакторе настроек/ресурсов, приоритет контролов/форм/диагностики, общий overlays/commands механизм и направления больших данных, текста/accessibility, оформления, Vulkan/HUD и производительности. Пункты PA1–PA10 имеют статус **запланировано**; конкретные API, сроки и критерии отдельных расширений уточняются при начале работ.

**Изменения:** только PLAN/STATUS; добавлены порядок, архитектурные ориентиры и планируемые критерии beta. Требования alpha, net10.0/xUnit и её K1–K9 сохраняются; новые механизмы этим изменением не реализованы.

**Проверки:** прочитаны оба актуальных документа и VCS status; после правки успешно проверены точность вставок, сохранность прежнего текста, структура Markdown и новая локальная ссылка. Build/tests/GPU/publish не запускались. Native exec_command снова не смог создать процесс (`helper_unknown_error: apply deny-read ACLs`); документация прочитана и изменена через Rider. Результаты предыдущих запусков остаются датированными.

**Оставшиеся блокеры и следующий конкретный шаг:** полнота alpha по-прежнему открыта, прежде всего native DPI/monitor transitions, оставшиеся window/input проверки, golden/resource/loss/consumer evidence. Следующий шаг кода — native DPI → UiViewport.Scale/input/client min-max contract, затем реальные 100/150/200% и monitor transitions. После завершения alpha начать PA1 и минимальный PA2 на контрольном приложении, затем PA3–PA5; Vulkan/HUD PA9 развивать по потребности конкретного engine host.

## Сверка планов 2026-10-09 — документация и исходники

Подтверждено чтением: Alloy зависит только от Argentis; renderer не ссылается на Platform.Silk; net10.0/xUnit сохраняются. Найдены UiSession.RenderingFailure/RenderingFailed, Win32 clipboard adapter, SkiaDrawingResources с paint/FIFO16fonts, shared Settings backend/HUD/failure probes и Win32WindowChrome. Это подтверждает наличие реализации, а не новый успешный запуск её поведения.

Исправлены устаревшие сводные статусы clipboard, shared scene,2.4/K8,K9 и точки продолжения; добавлен4.9 в таблицу этапа4. Датированные подробные записи прошлых запусков сохранены. План получил актуальный маршрут до alpha; цели/архитектура/критерии готовности не менялись.

Проверки этой сверки: чтение PLAN/STATUS, исходников и csproj, VCS status; после правки — повторное чтение обоих документов и проверка внесённых замен. Native exec_command не смог создать процесс: `helper_unknown_error: apply deny-read ACLs`; использованы read/patch tools Rider. Build/tests/GPU/publish не запускались, старые logs не засчитываются как результаты2026-10-09. Рабочее дерево до правки:88entries (39modified/49untracked),0staged/0conflicts; существующие изменения сохранены.

Ближайший конкретный шаг — native DPI → UiViewport.Scale/input/client min-max contract. В SilkWindowHost.Viewport scale по-прежнему вычисляется как FramebufferSize.X/Size.X, mouse coordinates передаются без преобразования; это согласуется с записанным native DPI144/UiScale1. Затем проверить реальные100/150/200% и monitor transitions с chrome/input/alpha regression. Оставшиеся4.9checks, fixed fonts/golden, long lifecycle/VRAM, real loss и отдельный/package consumer открыты.

## 4.9d: real input, минимальные native styles и desktop alpha — предыдущий запуск 2026-10-08

**Изменения реализации.** [Win32WindowChrome](../../TrueMoon.Alloy.Platform.Silk/Win32WindowChrome.cs) снимает GLFW WS_POPUP и WS_CAPTION, сохраняет SYSMENU/minimize и resizable THICKFRAME/maximize. Overlapped semantics вместе с HTCAPTION обеспечивают реальную system drag loop и Shell Snap. WM_NCCALCSIZE с wParam=true оставляет полный client; actual HWND/client geometry проверяется независимо от style bit. Добавленный в prototype WS_CAPTION не нужен: он расширял unbounded maximize на11pixels с каждой стороны при native DPI144; финальный вариант сохраняет exact work area и application limits. API4.9c/d, immutable snapshots и queued input вне WndProc сохраняются.

Alt+Space обрабатывается также как WM_SYSKEYDOWN/VK_SPACE с Alt context и без autorepeat до GLFW keyboard suppression. Disabled SystemMenu подавляет этот вход. Directed probe проверяет его, SC_KEYMENU, service и caption right-click. Глобальные shortcuts не перехватываются адаптером. Экспериментальные WM_NCACTIVATE/GETTITLEBARINFOEX/global DWM forwarding и неиспользуемый TryGetBounds удалены; production не получил их обходных путей.

[WindowSnapInputProbe](../../ManualTests/AlloyVulkanTest/WindowSnapInputProbe.cs) использует scan-code SendInput, own-HWND foreground/point guards, GUI_INMOVESIZE/MenuOwner и native rectangles. Проверяет Win+Z при создании, после move/resize/drag restore/Snap и после hover. Hover закрывается перемещением в свободную область собственного окна с повторной проверкой WindowFromPoint. Shell targets сверяются по Xaml_WindowedPopupClass и explorer/ShellHost/ShellExperienceHost. HWND цифровой подсказки может отличаться от HWND всего flyout: target rectangle не выдаётся за полные menu bounds. Сохранены отдельные target/context PNG каждого backend/mode, pointer и только введённые keys/buttons восстанавливаются.

`--window-snap-bar` удерживает system drag у верхнего края, показывает bar, выбирает зону и измеряет результат drop. Bar пропускает hit testing, а preview окна перекрывает указатель: автоматический WindowFromPoint detector для неё отвергнут. В текущем окружении все6cases/configuration получили правую треть work area `(2560,0)-(3840,2088)`; системная bar/selected zone подтверждены отдельными PNG до release. Desktop capture использует owned SafeHandle DC/bitmap, BitBlt/GetDIBits, восстанавливает selection до readback и не участвует в presentation.

**Фактически выполненные проверки.** net10.0, SDK10.0.401, xUnit2.9.3/VSTest; packages/native Skia ABI не менялись. Все native процессы запускались последовательно:

- `dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --filter FullyQualifiedName~WindowChromeTests --logger "trx;LogFileName=snap-unlocked-focused.trx" --results-directory TestResults/AlloyTransparency -v:q`: **Passed21, Failed0, Skipped0**. Unfiltered тот же project с `--no-build --no-restore`, snap-unlocked-ui.trx: **Passed451, Failed0, Skipped0**. TRX независимо сверены, error/aborted0. Новых unit tests этого запуска нет; временный тест неиспользуемого API удалён вместе с API.
- `dotnet build TrueMoon.slnx --no-restore --no-incremental -v:q`: **0errors/1341warnings**, snap-unlocked-solution-build.log. Финальные Release publish обоих manual projects exit0, snap-publish/snap-standalone; новые CS/SYSLIB warnings устранены. Последняя правка cast/unused locals устраняла предупреждения и не меняла проверенное x64 behavior.
- Debug/published Release `--window-snap --validation`: по**8cases**, queued hit/hover/press/capture cancel/outside release/disabled/exactly-once button, Space/Enter, double-click и **4menu entry points**, held Alt+Space repeat не открывает меню. MenuOwner/GUI_INMENUMODE проверены на потоке собственного HWND; capture/hooks0 после teardown, Vulkan validation0/0.
- Debug/published Release `--window-chrome --validation`: прежние8configuration/116directed hits, negative coordinates, resize edges, stale geometry, limits/rounding/exact resize, bounded/unbounded maximize/work area, owner/disposed guards. Unbounded rectangle теперь точно совпадает с work area. Hosting GL/Vulkan title button/queued commands и title-factory failure cleanup прошли, registry/hooks0.
- Debug/published Release `--window-snap-input --validation --capture-snap`: по**6cases/150checks**, failed0, Failure=null, SettingsUnchanged=true, hooks0/0. Все25checks на case true: full client, move +80/+70, resize +70, editor exclusion/focus, exactly-once maximize/exact work area, maximized drag restore, double-click maximize/restore, right-click menu, edge/4corner Snap, hover, Win+Z и cleanup/validation. По6skipped Alt+Space: PowerToys.PowerLauncher получает foreground; отдельный стандартный decorated control подтвердил такое же перехватывание. После первого подтверждения остальные5cases пропускают отправку сочетания при том же global shortcut. Это не приложение-specific failure и не успешный физический Alt+Space тест. Настройки Windows/PowerToys не менялись.
- Debug/published Release `--window-snap-bar --validation --capture-snap`: по**6cases**, system modal drag, Snap drop и cleanup true, validation0/0. Все **12bar PNG** визуально сверены; **24hover/keyboard context PNG** отдельно подтверждают реальную Shell grid и цифровые подсказки Win+Z. Visual confirmation не заменена случайным nearby HWND.
- Debug/published Release `--window-appearance --custom-chrome --native-snap --validation`: по**6cases/80samples/240RGB**, Matches=true для всех, **48per-pixel native hit targets** на configuration, source RGBA checks, initial/forced paint/resize/minimize-zero-size-restore на двух известных фонах. Hook/renderer/device teardown прошёл; opaque/opacity clears0, per-pixel clears23/case без очистки на10обычных frames. Оба отчёта независимо сверены, validation0/0, resources Released=true.
- Финальный `run-snap-unlocked-regression.ps1`: **20/20** —16standalone smoke (Debug/Release × GL/Vulkan × ordinary/custom/per-pixel/opacity) и4settings compare/HUD. Frames/model/input/theme/disposal, source comparisons, queued opacity прошли; Vulkan validation0/0, Skia GL NoError. OS clipboard не использовался.

Артефакты этого запуска в ignored `TestResults/AlloyTransparency`: snap-unlocked-{debug,release}-{native,chrome,input,bar,alpha}.log/JSON/PNG, focused/UI TRX, build/publish/regression logs; run-snap-unlocked.ps1 и run-snap-unlocked-regression.ps1. Counts выше взяты из этих текущих reports, а не старых snap logs. Scoped git diff --check, whitespace/conflict scan изменённых source files, Markdown fences и195local links четырёх обновлённых документов прошли.

**Промежуточные неуспехи.** Старый GUI_INMOVESIZE helper измерял флаг до первого move: исправлена проверка после реальных pointer moves. Popup+captionless styles мешали system move/drag restore/Snap; retained-caption prototype исправлял их, но нарушал unbounded maximize. Финальный минимальный вариант убирает только popup semantics и сохраняет отсутствие caption. Post-hover Win+Z initially failed: прежняя dismissal point `(240,250)` лежала внутри hover popup `(135,192)-(681,588)`. Перемещение в собственный свободный client исправило probe; title `offscreen` и WM_NCACTIVATE гипотезы не подтвердились. Первый Alt+Space helper отправлял Escape при удерживаемом Alt; modifiers теперь отпускаются до Escape. Native forwarding сам по себе не устранял global PowerToys shortcut; skipped сохраняется явно. Первый Snap bar detector не находил HWND несмотря на видимую bar; proof теперь разделён на capture и measured drop.

Медленный диагностический screenshot через сотни тысяч GetPixel был остановлен только для проверенного собственного dotnet probe процесса; результат snap-visual-prototype не считается passed. Быстрый capture заменён на BitBlt/GetDIBits. Один chrome diagnostic запуск остановился на отсутствующем VK_LAYER_PATH после успешной geometry/GL части; финальные запуски с указанным validation manifest прошли полностью. Неуспешные prototype logs сохранены; временные trace hooks/metadata API удалены.

**Оставшиеся ограничения и следующий конкретный шаг.** Windows11 build26300/NVIDIA RTX5070Ti, work area3840×2088, native HWND DPI144, **UiScale1**. SilkWindowHost сейчас выводит scale из framebuffer/window ratio, который на этой Windows не равен native content scale. Следующий конкретный шаг — определить native DPI → UiViewport.Scale/coordinates/min-max contract и проверить100/150/200% с переходом между мониторами, затем повторить chrome/input/alpha checks. Settings off/Aero Shake, physical release outside/cancel прозрачных областей, callback fault injection, долгий lifecycle/VRAM, hardware context/device loss, другие OS/GPU и package consumer остаются открытыми. Старые4.9b opacity/legacy capture failures отдельно не расследованы и не объявлены исправленными. Полные4.9d/alpha критерии PLAN сохраняются; текущий physical/desktop этап на доступном desktop пройден.

| Требование | Свидетельство текущего запуска |
| --- | --- |
| Maximize role, disabled/Client/occlusion | WindowChromeTests.Map_MaximizeButtonIsExplicit_AndDisabledOrClientAncestorExcludesIt; Map_MaximizeRoleRespectsTopmostOcclusionAndDisabledAncestor |
| Opt-in/options/menu switch | WindowChromeTests.Options_SnapRequiresResizable_AndSystemMenuCanBeDisabledIndependently |
| Adaptive sample/header/layout readiness | WindowChromeTests.DemoTitleBar_NarrowSnapSizeKeepsWindowButtonsVisibleAndRegionsCorrect; DemoTitleBar_FirstUpdateAndResizeLeaveSessionReadyForPresentation |
| Native menu/keyboard/capture/work area | WindowSnapProbe.Run; WindowChromeProbe.Run/RunAsync, текущие Debug/Release logs |
| Real drag, edges/corners/hover/Win+Z | WindowSnapProbe.RunInput, по150checks и6skipped/configuration;24context PNG |
| Snap bar and drop | WindowSnapProbe.RunBarInput, по6cases/configuration;12визуально сверенных PNG |
| Desktop alpha and per-pixel native target | WindowAppearanceProbe.Run, по80samples/240RGB/48targets/configuration |

Предыдущие bindings подэтапы **3.5a/b / часть3.6**, **2026-10-06**: one-way focused24/24 и тогда UI110/110; two-way focused44/44 и тогда UI154/154, build0errors/1341warnings. Прежние 359 cases (включая фабрики, коллекции, условные области и взаимодействия) повторно прошли в исторических запусках386/410; текущий UI результат указан выше.

Предыдущий подэтап кода — ownership Remove/SetContent/transfer (3.4/2.3), **2026-10-05**: новые ownership tests **27/27**, тогдашняя UI suite **86/86**, сборка **0 errors/1341 warnings**, 9 manual сценариев в Debug и Release publish, Vulkan validation **0 errors/0 warnings**, короткий retirement KHR/legacy с нулевыми ресурсами/подписками/retained references. GPU/manual/publish результаты относятся к предыдущему запуску. Последний полный запуск тестов решения — 2026-09-27. Полный план: [PLAN.md](PLAN.md).

## 4.9d: native contract — предыдущий запуск с LockApp blocker, 2026-10-08

**Этот раздел исторический:** counts, блокеры и слово «текущий» ниже относятся к предыдущему запуску до разблокировки desktop. Новые результаты находятся выше.

**Реализация.** [WindowChromeOptions](../../TrueMoon.Alloy/Runtime/WindowChrome.cs) получил NativeSnapLayouts (defaultfalse, требует Resizable), SystemMenu (defaulttrue) и optional IWindowSystemMenu.ShowSystemMenu с logical client coordinates. Platform adapter требует Windows11 для Snap до allocation; capabilities сообщают platform support, а не состояние Windows settings. [WindowRegionRole.Maximize](../../TrueMoon.Argentis/WindowRegionRole.cs) явно разрешает native maximize роль enabled focusable кнопки; disabled кнопка, disabled ancestor, explicit Client subtree и topmost overlay оставляют Client. Snapshot сохраняет прежнее отсутствие UI references.

[Win32WindowChrome](../../TrueMoon.Alloy.Platform.Silk/Win32WindowChrome.cs) возвращает HTMAXBUTTON при opt-in и актуальной геометрии, пересылает NC hover/leave DwmDefWindowProc. TrackMouseEvent обеспечивает NC leave; native pointer values ставятся в очередь UiInput. Capture используется на maximize press, captured move/up передаются UI; default native maximize на этом press подавлен, командой владеет Argentis OnClick. Отпускание вне кнопки, cancel/capture loss/focus loss/teardown снимают press/capture. Native callback не вызывает Element/input handlers; [SilkWindowHost.DispatchPendingWindowInput](../../TrueMoon.Alloy.Platform.Silk/SilkWindowHost.cs) доставляет очередь после DoEvents, Hosting включает этот шаг автоматически. Keyboard Space/Enter остаются routed UI input. HWND/menu borrowed SafeHandle; interop использует LibraryImport, WM_NCDESTROY/Dispose снимает root/subclass и очищает очередь.

Alt+Space перехватывается до GLFW SC_KEYMENU suppression, caption right-click и menu service открывают настоящий GetSystemMenu/TrackPopupMenuEx. EnableMenuItem учитывает normal/minimized/maximized state и Resizable; выбранный SC command PostMessage адресуется этому HWND. SystemMenu=false отклоняет service call и подавляет оба жеста. [HostedUiWindow.ShowSystemMenu](../../TrueMoon.Alloy.Hosting/HostedUiWindow.cs) валидирует finite coordinates/options и ставит вызов на owner thread. Caption double-click делегируется system default procedure. Windows settings не меняются, native previews не рисуются в Argentis.

[CustomTitleBar](../../ManualTests/AlloyTest/CustomTitleBar.cs) у --custom-frame сохраняет правые3×40 buttons и editor≥160 при minimum330×300; ширина text уменьшается. Layout вычисляет private геометрию без property mutation во время Measure. Maximize кнопка имеет новую роль; оба backend и обе формы прозрачности используют тот же title. Demo --custom-frame теперь включает NativeSnapLayouts и требует Windows11. [WindowSnapProbe](../../ManualTests/AlloyVulkanTest/WindowSnapProbe.cs) и [WindowSnapInputProbe](../../ManualTests/AlloyVulkanTest/WindowSnapInputProbe.cs) разделяют directed messages и physical input. Новый --native-snap flag существующей appearance matrix включает opt-in adapter.

**Фактически выполненные проверки текущего запуска.** net10.0/xUnit/VSTest, packages и native Skia ABI не менялись:

- Focused `dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~WindowChromeTests --logger "trx;LogFileName=snap-focused.trx" --results-directory TestResults/AlloyTransparency -v:q`: **Passed21, Failed0, Skipped0**. Unfiltered та же project: **Passed451, Failed0, Skipped0**, snap-ui.trx/log. Дополнены tests native role/disabled/occlusion/options и linked actual CustomTitleBar source; first Update/resize не оставляют NeedsUpdate, static Update=false.
- `dotnet build TrueMoon.slnx --no-restore --no-incremental -v:q`: **0 errors/1341 warnings**, snap-solution-build.log. Release publish обоих manual projects exit0 в snap-publish и snap-standalone. SYSLIB interop diagnostics отсутствуют.
- Debug/published Release `--window-snap --validation`: по**8 configurations** (GL/Vulkan × Snap on/off × SystemMenu on/off), HTMAXBUTTON/Client opt-in, queued hover/leave/press вне callback, exactly-once click, release outside, cancelled press, disabled exclusion, Space/Enter, caption double-click maximize/restore. В enabled menu cases три входа проверены через GetGUIThreadInfo GUI_INMENUMODE/MenuOwner собственного потока, закрыты адресными Escape/CancelMode; disabled menu отклоняет service и не входит в menu loop. Teardown pressed button освобождает capture/hooks, Vulkan validation0/0 включая device Dispose. Это **не** реальное наведение/перетаскивание и не Shell flyout proof.
- Debug/published Release повторно прошёл `--window-chrome --validation`: прежние8 adapter configurations/116directed hits на configuration, negative coordinates,8 resize edges, exclusions/stale map, limits/fractional rounding/exact resize, bounded/unbounded maximize/work area, команды/owner/disposed guards. Hosting real GPU/title composition, routed button/queued commands и title-factory failure cleanup прошли; registry/hooks0, validation0/0.
- Финальный `run-snap-regression.ps1`: **20/20**, native processes строго последовательно. 16AlloyTest smoke Debug/published Release × GL/Vulkan × ordinary/custom/per-pixel/opacity; ≥40frames, settings/list/theme/input/disposal, queued opacity. Ещё4settings compare/HUD, synthetic scales1/1.5/2, validation0/0. Clipboard не использовался. Это GPU/UI regression, не current desktop RGB proof.
- Debug physical probe: собственный HWND не стал foreground. Published Release повтор подтвердил **SetForegroundWindow=0**, foreground class Windows.UI.Core.CoreWindow, process **LockApp**, own HWND DPI144. В final guarded attempt **foreground_owned=false**, остальные physical checks не запускались; hooks0, SettingsUnchanged=true. Input не отправлялся до отказа guard. Отчёты snap-{debug,release}-input.json/log. Это environment blocker; DPI144 одного HWND не закрывает150% interaction/monitor matrix.
- Debug `--window-appearance --custom-chrome --native-snap --validation` остановился на **Known backdrop is obstructed** до первой desktop alpha sample, snap-debug-alpha.log. Published Release alpha не запускался при том же LockApp blocker. Предыдущие4.9c RGB successes ниже исторические, не результат текущего запуска.
- Scoped git diff --check, whitespace/conflict scan новых source files, Markdown fences и214local file links прошли. TRX независимо сверены:21/451passed, failed/error/aborted0; native logs содержат8cases в каждой configuration.

**Исправления в процессе.** Первый regression остановился на `Update UI before using its OpenGL output`: CustomTitleBar.Measure менял Width/IsVisible и invalidated сессию после layout. Убраны property mutations; добавлен first-update/resize/static test, финальные21/451 и20manual checks прошли. Первичный physical helper делал unconditional key/button cleanup даже при раннем foreground отказе; исправлен на tracking только введённых событий и cursor restore только после собственного движения. Финальный guarded Release run подтвердил остановку до input.

**Блокеры и следующий конкретный шаг.** Разблокировать рабочий desktop и последовательно выполнить --window-snap-input --validation в Debug/published Release, затем --window-appearance --custom-chrome --native-snap. Physical probe пока рассчитан на GL/Vulkan × opaque/per-pixel, settings читает; detected nearby Xaml/Snap HWND — только flyout candidates, нужны visual confirmation и проверка detector на реальном Shell. Даже успешный этот probe не закрывает corners/Snap bar, opacity input, settings off, Aero Shake,100/150/200% monitor transitions и длинный lifecycle. Native maximized drag/edge Snap/hover/Win+Z пока **не подтверждены**; возможные дополнительные требования к native styles должны решаться после реального prototype, не объявлены рабочими по одним сообщениям. Прежние4.9b opacity/legacy capture failures не расследованы.4.9d/alpha readiness остаются открытыми; принятые критерии PLAN не сокращены.

| Требование | Свидетельство текущего запуска |
| --- | --- |
| Явный Maximize, disabled/Client/snapshot | WindowChromeTests.Map_MaximizeButtonIsExplicit_AndDisabledOrClientAncestorExcludesIt |
| Overlay и disabled ancestor | WindowChromeTests.Map_MaximizeRoleRespectsTopmostOcclusionAndDisabledAncestor |
| Snap requires resize, independent menu switch | WindowChromeTests.Options_SnapRequiresResizable_AndSystemMenuCanBeDisabledIndependently |
| Header330/640 и правильные input regions | WindowChromeTests.DemoTitleBar_NarrowSnapSizeKeepsWindowButtonsVisibleAndRegionsCorrect (2 cases) |
| Layout готов к presentation после first Update/resize | WindowChromeTests.DemoTitleBar_FirstUpdateAndResizeLeaveSessionReadyForPresentation |
| Native caption/menu/keyboard/capture lifetime | WindowSnapProbe.Run,8cases/configuration, Debug/published Release |
| Physical Snap/flyout/drag и desktop alpha | Не пройдено: LockApp foreground/obstructed backdrop; logs/input JSON выше |

## 4.9c: собственный заголовок и custom frame — предыдущий запуск 2026-10-08

Все counts/build/native/desktop результаты этого раздела относятся к предыдущему4.9c запуску. Текущие4.9d результаты и блокеры находятся выше.

**Реализация.** [WindowChrome](../../TrueMoon.Alloy/Runtime/WindowChrome.cs) содержит immutable WindowChromeOptions, UiWindowState, необязательные IWindowCommands/IWindowChromeHost, capabilities и WindowRegionMap. Commands/state/geometry не используют Silk/Win32 типы; исходный IWindowHost не изменён. NativeDrag defaultfalse, Resizable defaulttrue, finite border0–64 в96-DPI units, min/max client sizes и запрос resize валидируются. Windows x64 + Decorated=false обязательны; unsupported configuration не переключает backend и не создаёт молча иной frame.

Argentis [WindowRegionRole](../../TrueMoon.Argentis/WindowRegionRole.cs)/Element.WindowRegion задаёт Inherit/Caption/Client. Snapshot копирует paint order/clipping/visibility; Focusable controls, включая disabled Button/TextBox/Slider, и explicit Client subtree остаются client input. Собственные non-focusable interactive controls требуют explicit Client. Snapshot не удерживает Element/delegate/session. [WindowFrame](../../TrueMoon.Argentis/WindowFrame.cs) принимает два unowned subtree, резервирует height title bar и передаёт content оставшуюся область; children read-only, ошибочная конструкция/cleanup сохраняет ownership contract.

[Win32WindowChrome](../../TrueMoon.Alloy.Platform.Silk/Win32WindowChrome.cs) сохраняет нужные SYSMENU/min/max/THICKFRAME styles при отключённом caption, возвращает0 на WM_NCCALCSIZE и HTCAPTION/eight resize codes на WM_NCHITTEST. Caption применяется только при NativeDrag и актуальном размере snapshot; fixed/maximized окна не получают resize edges. Screen coordinates декодируются со знаком. Border использует GetDpiForWindow; min/max track limits используют существующий viewport scale (ceil minimum/floor maximum), work area текущего монитора ограничивает maximize, application maximum учитывается. Прямой client resize делает SetWindowPos: GLFW standard-frame adjustment больше не добавляет невидимую рамку к requested size. SafeHandle HWND borrowed, static delegate/explicit GCHandle живут до RemoveWindowSubclass/WM_NCDESTROY; callback failures сохраняются EDI и сообщаются снаружи native dispatch. Hooks снимаются до HWND destruction на owner thread.

SilkWindowHost предоставляет commands/state/capabilities/geometry/Resize; Hosting фиксирует snapshot Chrome/TitleBarFactory, сообщает capabilities после startup, queue-команды Minimize/Maximize/Restore/Resize доступны с caller thread. TitleBarFactory выполняется на owner thread с IWindowCommands и создаёт header из Argentis; UiSession владеет WindowFrame/title/original view и освобождает все subtree. После UI update Hosting публикует геометрию, native callback не вызывает factory/tree. Factory failure освобождает исходный root и window/device, сообщает original error через StartAsync/Completion. Без Chrome сохраняется исходная рамка/appearance/HUD path. [AlloyTest](../../ManualTests/AlloyTest/Program.cs) получил --custom-frame: text/editor и3window buttons, drag и min760×400; shared form smoke теперь ищет View1 внутри WindowFrame.

**Фактические проверки предыдущего запуска4.9c**, тогдашняя Windows/NVIDIA, native DPI100%:

- `dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --filter FullyQualifiedName~WindowChromeTests --logger "trx;LogFileName=chrome-focused.trx" --results-directory TestResults/AlloyTransparency -v:q`: **Passed15, Failed0, Skipped0**. Последующий unfiltered запуск той же UI project: **Passed445, Failed0, Skipped0**, chrome-ui.trx/log. net10.0/xUnit/VSTest, package changes отсутствуют.
- `dotnet build TrueMoon.slnx --no-restore --no-incremental -v:q`: **0 errors/1341 warnings**, chrome-solution-build.log. Оба Release publish exit0: AlloyVulkanTest→chrome-publish, AlloyTest→chrome-standalone. Interop build diagnostics SYSLIB отсутствуют.
- `--window-chrome --validation` в Debug/published Release: **8GL/Vulkan × NativeDrag × Resizable cases**, по**116directed native hit tests/configuration**, disabled Button/TextBox exclusions, eight border/corner codes, negative screen coordinates, stale-map rejection, native exact size limits и resize700×500. Minimize/restore/close, wrong-thread commands/Dispose, disposed commands, incompatible decoration rejection и hooks0 прошли. Ещё2windows/configuration подтвердили exact unbounded maximize→current monitor work area,2 — fractional min/max inward rounding.
- В том же probe2Hosting cases/configuration: реальный OpenGL/Vulkan rendering, WindowFrame composition, routed Argentis maximize button и queued restore/minimize/client resize; title/content disposed, ActiveSessionCount0, hook0. Ещё2factory-failure cases/configuration: original exception identity в StartAsync/Completion, root disposed, registry/hooks0, Vulkan validation0warnings/errors включая teardown. Directed messages и UiInput не заменяют physical OS drag/resize interaction proof.
- `--window-appearance --custom-chrome --validation`: Debug/published Release, по6production custom-frame combinations,80samples/240RGB/configuration; итого**160samples/480RGB точек**, все Matches=true. Оба backend × Opaque/Opacity/PerPixel, initial/forced paint/resize/minimize-zero-size-restore,2known backdrops, источник premultiplied RGBA. PerPixel clearCount23/case, другие0, обычные frames не вызываютclear. **96native WindowFromPoint targets всего**, ChromeHooks0/transparentHooks0 и Releasedtrue послекаждогоокна. Независимая сверка JSON пересчитала RGB/source/lifetime/validation/counters; successful source assertions не подменены одним report flag.
- `run-chrome-regression.ps1`: **20/20**, все native processes последовательно. Debug/published Release: 16AlloyTest smoke (OpenGL/Vulkan × ordinary/custom/custom-per-pixel/custom-opacity),40frames/case, shared form/list/theme/input assertions; opacity0.5→0.75 через очередь. Ещё4settings compare/HUD scenarios: unchanged raster/GL/Vulkan bounds/input/scales1/1.5/2 и game-owned HUD; Vulkan validation0/0. Scales здесь synthetic UI scale, не physical OS DPI. OS clipboard не использовался.
- Logs/reports в ignored TestResults/AlloyTransparency: chrome-{debug,release}-native.log и chrome-{debug,release}-alpha.json/log, chrome-ui/focused.trx/log, chrome-solution-build.log, publish logs и chrome-regression.log/20individual logs. Scoped diff --check, whitespace/conflict-marker check новых файлов, Markdown fences и204local file links прошли. Результаты ниже4.9b/a относятся к предыдущим запускам.

**Промежуточные failures.** Первичный native probe ошибочно ожидал HTCLIENT на top-left у fixed window при включённом NativeDrag: точка принадлежит Caption и правильно возвращала HTCAPTION. Исправлена ожидаемая роль, production hit-test algorithm не менялся; финальные Debug/Release116checks прошли. Первичный probe build имел CS0170 в диагностическом rectangle check; чтения разделены, финальная сборка прошла. Ошибка округления fractional native maximum исправлена на floor и подтверждена отдельными native limits cases.

**Оставшиеся проверки/следующий шаг.** Реализация4.9c существует и прошла перечисленные native/window/desktop checks; вся interaction/DPI matrix4.9 ещё не закрыта. Следующий конкретный шаг4.9d — maximize-button native role/HTMAXBUTTON, Windows11 Snap Layouts/Win+Z, системное меню/double-click/maximized drag, real pointer move/resize/Snap со включёнными/выключенными Windows settings; затем DPI150/200%/monitor transitions и длинный lifecycle. Settings Windows не менялись. Прежние4.9b intermittent Opacity captures и поздний legacy control-background blocker не расследовались и не повторялись этим этапом; новая successful custom-frame alpha matrix не является их root-cause исправлением. Не проверены hardware loss, других GPU/OS, long VRAM, fault injection native callback и package consumer; alpha не объявлена готовой.

| Требование | Конкретное свидетельство |
| --- | --- |
| Caption/text inheritance, controls/disabled exclusions | WindowChromeTests.Map_CaptionIsInheritedByText_ControlsRemainClientIncludingDisabled; native116checks |
| Explicit subtree exclusion | WindowChromeTests.Map_ClientSubtreeCannotBeMadeCaptionByDescendant |
| Paint order/visibility/stale tree snapshot | WindowChromeTests.Map_TopmostVisibleRegionWins_AndSnapshotSurvivesRemovalAndDisposal |
| Clipping/outside/invalid coordinates | WindowChromeTests.Map_ClipsScrolledDescendants_AndDoesNotExposeOffscreenCaption; Map_EmptyAndInvalidPointsAreClient_NullAndDisposedRootsRejected |
| Header/content layout and ownership | WindowChromeTests.Frame_ReservesTitleHeightAndGivesContentRemainingViewport; Frame_RejectsSameOrOwnedChildWithoutChangingOwnership |
| Enum/options/endpoints/bounds | WindowChromeTests.Role_RejectsUnknownValues; Options_DefaultsAreOptIn_AndSizesIncludeExactEndpoints; Options_InvalidBorderRejected; Options_InvalidAndInvertedSizesRejected |
| Native resize/limits/work area/commands/lifetime | WindowChromeProbe.Run/RunAsync, --window-chrome --validation Debug/published Release |
| Header commands/creation failure | WindowChromeProbe.RunAsync, title button and title-factory failure cases обоих backend |
| Appearance preserved with custom frame | WindowAppearanceProbe.Run --custom-chrome,480RGB/96hit targets; current20/20regression |

## 4.9b: публичная прозрачность и Windows lifecycle — предыдущий запуск 2026-10-08

Все counts/build/native результаты этого раздела относятся к предыдущему4.9b запуску. Текущие4.9c результаты находятся выше.

**Реализация.** [WindowAppearance](../../TrueMoon.Alloy/Runtime/WindowAppearance.cs): Opaque/Opacity/PerPixel, finite initial opacity0–1, независимый Decorated flag; режим и decoration фиксируются при создании, uniform opacity меняется только в Opacity mode. `IWindowAppearanceHost` расширяет IWindowHost опционально, без изменения существующих implementors. Native capabilities не подменяют Vulkan surface capability: [VulkanWindowPresenter](../../TrueMoon.Alloy.Hosting/VulkanWindowPresenter.cs) отдельно проверяет advertised PreMultiplied и отвергает неподдержанные/некорректные flags до allocation/presentation; выбранный alpha повторно проверяется при recreation. Публичный mode-selecting constructor требует Opaque для opaque/opacity и PreMultiplied для per-pixel; прежний no-argument constructor остаётся совместимым с alpha precedence существующих HUD callers.

[SilkWindowHost](../../TrueMoon.Alloy.Platform.Silk/SilkWindowHost.cs) принимает immutable appearance, задаёт TransparentFramebuffer/WindowBorder до Initialize, проверяет actual GLFW attribute, предоставляет owner-thread/disposed guards и SetOpacity с native roundtrip. Uniform opacity объявлена только для Windows. [Win32TransparentFramebuffer](../../TrueMoon.Alloy.Platform.Silk/Win32TransparentFramebuffer.cs) создаёт HWND subclass через SetWindowSubclass; static callback и explicit GCHandle сохраняют target до снятия. При creation/WM_PAINT/WM_SIZE/show/DPI/composition выполняется BLACKNESS очистка client DC; передача в DefSubclassProc сохраняет GLFW event/input/refresh handling. WM_PAINT очищается до GLFW damage callback. Client DC освобождается SafeHandle, HWND borrowed; hooks и GC roots снимаются перед native window destruction на owner thread, WM_NCDESTROY — дополнительный cleanup. Managed callback errors сохраняются EDI и сообщаются вне native dispatch через VerifyWindowAccess/hosting loop.

[Hosting options](../../TrueMoon.Alloy.Hosting/AlloyOptions.cs) валидируют settings; [HostedUiWindow](../../TrueMoon.Alloy.Hosting/HostedUiWindow.cs) фиксирует snapshot, передаёт appearance в host/presenter, предоставляет capabilities и combined SupportsPerPixelTransparency после startup, а SetOpacity ставит owner-thread command. Argentis/native ABI/packages и renderer ownership не менялись. `Decorated=false` реализует только системную рамку off; custom title bar, commands/resize/Snap не реализованы. Клиентские per-pixel alpha0/128/255 точки остаются native hit targets этого окна; managed click-through не включён.

[AlloyTest](../../ManualTests/AlloyTest/Program.cs): --transparent, --opacity <0–1>, --borderless для обоих backend. Полупрозрачность framebuffer не меняет alpha непрозрачного фона view. Smoke проверяет capabilities и queued opacity0.5→0.75. [WindowAppearanceProbe](../../ManualTests/AlloyVulkanTest/WindowAppearanceProbe.cs) использует production host/public presenter, а не ручной redirection clear: оба backend × оба decoration settings × три режима, initial/forced WM_PAINT/resize/minimize-zero-size-restore, два фона, source RGBA и desktop RGB. Проверяет actual WS_CAPTION, owner/disposed guards, source readbacks, per-pixel WindowFromPoint и отсутствие native clear на10 обычных frames. Предыдущий prototype сохранён; early-alpha rejection probe адаптирован к constructor rejection.

**Фактические проверки на текущей Windows/NVIDIA**, source/window scale1, не native DPI150/200%:

- WindowAppearanceTests: **20/20**; вся UI suite: **430/430**, Failed0/Skipped0, TRX appearance-focused.trx/appearance-ui.trx. net10.0, SDK10.0.401, xUnit/VSTest; новых packages нет.
- Non-incremental TrueMoon.slnx build: **0 errors/1341 warnings**, logs appearance-solution-build.log; manual финальный incremental build0errors/14warnings. Release publish AlloyVulkanTest и AlloyTest — exit0, appearance-publish/appearance-standalone. Новых interop SYSLIB diagnostics нет.
- Публичная матрица: Debug/published Release, по12 foreground create/dispose,160 samples/480 RGB точек и160 source readbacks/configuration; все Matches=true. Opaque и per-pixel дают по8samples/case, Opacity по24. Actual frame: начальные420×180, resize480×220; native minimize и zero-size suspension/restore проверены. Принудительный WM_PAINT вызывает clear только у per-pixel, обычные presentation frames — без clear; у opaque/opacity clearCount0. Hooks после каждого окна0.
- Native per-pixel hit target: 96точек/configuration (alpha0/128/255, оба фона/четыре фазы/оба backend/decorations). Это WindowFromPoint proof, не полный physical mouse/capture interaction suite. Native actual WS_CAPTION совпал с Decorated. Wrong-thread Verify/Dispose отвергнуты без потери live window; после Dispose Verify отвергнут.
- Native regression первый проход **28/28**. Последний проход: **27/28**; Release historical transparency matrix — failure на обязательном backdrop control, остальные27сценариев прошли. Debug и published Release: shared settings raster/OpenGL/Vulkan compare, HUD, short KHR/legacy retirement, controlled graphics failure/recreate и ordinary/transparent/borderless/opacity scripted AlloyTest для обоих backend. Все16app cases последнего прохода завершились, включая native queued opacity0.5→0.75 и startup capabilities. Vulkan validation0errors/0warnings включая teardown в завершённых сценариях, Skia GL NoError. Scene/model/input assertions прошли; OS clipboard не использовался. Старый Name binding failure в этом запуске не воспроизведён и не считается исправленным.
- Local ignored artifacts: appearance-debug.json/appearance-release.json, build/UI/TRX/publish logs, appearance-regression.log и run-appearance-regression.ps1, отдельные28logs/old-matrix JSON. Historical 4.9a/a.1 результаты ниже не считаются текущими проверками.

**Промежуточные failures и ограничения.** После первых успешных Debug/Release matrix два последующих Debug runs остановились на desktop RGB assertion в Vulkan Opacity/forced paint: decorated, background1,opacity1; затем borderless,background0,opacity0. Для второго случая nativeOpacity=0 и foreground WindowFromPoint=false, actual RGB(4,4,4)/(4,4,4)/(6,11,4), expected background RGB(40,100,180); без background target logging причина не определена. Добавлены actual/expected RGB, foreground/background native targets и visibility diagnostics. Production alpha/opacity алгоритм не менялся; успешные финальные повторы не являются исправлением root cause. Логи сохранены appearance-debug-intermediate-failure.log/appearance-debug-intermediate-target.log. Расследовать capture/z-order/composition стабильность отдельно и не считать неуспешные runs успешными. Per-pixel/source/validation assertions в завершённых случаях проходили.

Поздний Release legacy matrix отдельно остановился на GetPixel unavailable, затем на backdrop control. Добавлены координаты/state diagnostics, завершение restore перед hide и bounded ожидание только известного фона; alpha результаты не подбираются ожиданием. Последняя диагностика: backgroundVisible=true, backgroundState=Normal, но backgroundTarget=false и actual RGB(3,3,3)/(3,3,3)/(5,9,3), ожидаемый RGB(40,100,180). Следовательно, в этот момент измеряется не контрольное фоновое окно; это не успешная проверка alpha и не основание изменять production composition. Остальные независимые Release app checks выполнены отдельно8/8. Предыдущий успешный28/28 не заменяет последний27/28. Для повторения legacy capture нужен доступный desktop без перекрытия; вопрос об этом направлен пользователю. Публичные Debug/Release reports и их 960 RGB/192hit-target точек прошли независимую сверку; implementation, failed runs и успешные checks разделены.

**Следующий конкретный шаг.** Повторить оставшийся Release legacy capture на доступном desktop, затем4.9c: независимые window commands и drag/resize regions для собственного Argentis title bar, далее4.9d native Snap. Полнота4.9/alpha остаётся открыта: реальные DPI150/200%/monitor transitions, физические mouse interactions прозрачных областей, native callback fault injection, long lifecycle/VRAM, hardware device/context loss, другие GPU/OS и package consumer не проверены этим запуском. Общая opacity и transparent framebuffer не комбинируются, backend fallback отсутствует. Реализация4.9b существует и проходит указанные проверки; portability/DPI/capture stability ещё не закрыты.

| Требование/контракт | Конкретное свидетельство |
| --- | --- |
| Opaque default | WindowAppearanceTests.Default_PreservesOpaqueDecoratedWindow; native --window-appearance Opaque cases |
| Opacity0–1 и validation | OpacityMode_AcceptsEndpointsAndIntermediateValue; Opacity_RejectsOutOfRangeAndNonFiniteValues; Opacity_RejectsValuesImmediatelyOutsideInclusiveBounds |
| Нельзя совместить uniform/per-pixel | NonOpacityModes_RejectUniformOpacity; native host SetOpacity rejection |
| Независимое оформление и валидный mode | Decoration_IsIndependentOfAlphaMode; UnknownMode_IsRejected; actual WS_CAPTION native checks |
| Desktop per-pixel/lifetime | WindowAppearanceProbe.Run, --window-appearance --validation, Debug/published Release; WM_PAINT/hit target/hook/source checks |

## 4.9a.1: изоляция Vulkan и redirection surface — предыдущий запуск 2026-10-08

**Реализация.** [VulkanTransparencyIsolation](../../ManualTests/AlloyVulkanTest/VulkanTransparencyIsolation.cs) запускается через `--vulkan-transparency-isolation` до `SkiaNativeLibrary.Initialize`. Контрольный фон рисуется raw OpenGL; источник — owned RGBA8 GPU image, заполненный `vkCmdClearColorImage`, затем существующий `VulkanWindowPresenter` выполняет GPU blit в swapchain. Ни UiSession, ни native Skia не используются; отсутствие `libSkiaSharp.dll` проверяется по process modules. `VulkanHostReadback` только независимо проверяет premultiplied RGBA всех пикселей источника и actual image layout; CPU upload/readback не участвуют в presentation.

[DesktopCompositionClear](../../ManualTests/AlloyVulkanTest/DesktopCompositionClear.cs) экспериментально очищает client DC через `PatBlt(BLACKNESS)` + GdiFlush/DwmFlush после обработки show/resize, перед измеряемой серией. HWND заимствован у Silk, client DC освобождается SafeHandle до HWND; window class/styles и shared background brush не меняются. Новых callbacks/hooks нет. Это диагностическая инициализация DWM surface, не production WM_PAINT обработчик. Raw probe явно активирует собственные контрольные окна и ограниченно ожидает известный backdrop при переходах show/restore; ожидание не подбирает проверяемый alpha результат. В существующую [Skia matrix](../../ManualTests/AlloyVulkanTest/WindowTransparencyProbe.cs) флаг `--redirection-clear` добавляет восьмой candidate `per-pixel-premultiplied-clear`, сохраняя исходные negative cases и обычные GL/Vulkan пути. Публичный default presenter остаётся Opaque-first.

**Фактический результат**, RTX5070Ti/NVIDIA617.42, Windows build26300, SDK10.0.401/runtime10.0.12, Silk2.23/GLFW3.4; supportedCompositeAlpha Opaque|PreMultiplied, swapchain B8G8R8A8Unorm. В raw matrix совпало в каждой конфигурации:

| Raw Vulkan конфигурация | Стандартная рамка | Без рамки |
| --- | --- | --- |
| Transparent framebuffer, Opaque swapchain, без очистки (negative alpha control) | 18/18 с ожидаемым opaque RGB | 18/18 с ожидаемым opaque RGB |
| Transparent framebuffer, PreMultiplied, без очистки | 6/18: только opaque endpoint | 18/18 |
| Transparent framebuffer, PreMultiplied, BLACKNESS очистка | **18/18** | **18/18** |

Для фонового RGB(40,100,180) и foreground RGB(220,40,80) decorated PreMultiplied без очистки дал RGB(255,255,255), (237,147,167), (220,40,80). После очистки: **(40,100,180), (130,70,130), (220,40,80)** — ожидаемые alpha0/128/255. На втором фоне результат тоже совпал; initial420×180, resize480×220 и minimize/restore прошли. Borderless baseline на этой машине уже работал без очистки; это не обещание для других driver/configurations. Гипотеза об инициализации DWM redirection surface подтверждается A/B измерениями; внутреннее устройство драйвера не исследовалось. Источники: [GLFW PR #2815](https://github.com/glfw/glfw/pull/2815), [PatBlt](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-patblt).

С настоящим Skia UI дополнительный decorated candidate прошёл **6/6 samples / 18 RGB точек на configuration**: три полосы alpha0/128/255, оба фона, initial/resize/minimize-zero-size-restore. Исходные Vulkan Opaque и PreMultiplied без очистки по-прежнему имеют Supported=false; OpenGL opaque/opacity/per-pixel и Vulkan opaque/opacity прошли. Совпадение включает отдельный непрозрачный endpoint — исчезновение всего Vulkan изображения не принимается за прозрачность.

**Фактически выполненные проверки.** Manual project Debug build: 0 errors / 14 warnings (NU1507 и прежние CS1591); publish Release в `TestResults/AlloyTransparency/isolation-publish` — exit0. В Debug и published Release выполнены `--vulkan-transparency-isolation --validation --isolation-output …` и `--window-transparency --redirection-clear --validation --transparency-output …`: 4/4 exit0. Raw — 6 foreground create/dispose/configuration, 108 source readbacks и 108 desktop RGB точек/configuration; Skia — 8 candidates, 72 samples/216 RGB точек/configuration, 3 rejection checks/configuration. Live presenter swapchains/semaphores/fences/command pools после Dispose0; validation0errors/0warnings включая device teardown; raw GL backdrop NoError, Skia GL checks NoError. Локальные ignored artifacts: `isolation-debug.json`, `isolation-release.json`, `skia-clear-debug.json`, `skia-clear-release.json`, соответствующие logs, build/publish logs. Первые диагностические запуски не являются успешными проверками: без VK_LAYER_PATH validation layer не найден; затем слишком широкий поиск имени Skia принял managed SkiaSharp.dll за native DLL — исправлен на точное libSkiaSharp.dll, финальные проверки проходят.

Повтор raw Debug однажды остановился на обязательном backdrop control после restore: фиксированный settling интервал не гарантировал видимость фона; alpha результат не принимался. После явной активации собственных окон и bounded ожидания известного backdrop финальные Debug/published Release выполнены заново. Промежуточный failure не считается успешным запуском или проверкой per-pixel. Независимая PowerShell-сверка пересчитала raw expected RGB по формуле premultiplied composition, Matches из raw/Skia RGB, проверила 216/432 точки, source/lifecycle/validation, отсутствие native Skia и три rejection checks/configuration; прошла. SHA256 текущих probe sources и итоги сохранены в ignored `isolation-verification.json`. Build/publish и текущие native результаты отделены от прежней UI suite. Scoped whitespace/conflict markers/Markdown fences, diff --check и 138 локальных файловых ссылок документов проверены.

**Блокеры и следующий шаг.** Desktop proof существующего GPU пути выполнен; отдельный CPU/layered-window или DirectComposition transport сейчас не требуется. Следующий конкретный шаг4.9b: согласовать и реализовать независимый window configuration/capability contract; связать TransparentFramebuffer и поддержанный PreMultiplied с per-pixel mode, включить redirection initialization в Windows lifecycle и выполнить native paint/resize/recreate regression. Диагностическая очистка после show не заменяет обработку дальнейших WM_PAINT, DPI и monitor transitions. Неподдержанный composite alpha должен сообщать причину, без fallback; общую opacity не совмещать с transparent framebuffer. Input в прозрачных областях, title bar/native Snap, DPI150/200%, monitor transitions и production hooks пока не проверены. Unit/solution tests, обычные scripted standalone interactions, HUD, long lifecycle/VRAM soak, device loss и K9 timings в этом запуске не выполнялись. План пересмотрен явно; net10.0/xUnit/retained UI/HUD ownership сохраняются.

## 4.9a: оконная прозрачность — предыдущий запуск 2026-10-08

**Реализация.** [WindowTransparencyProbe](../../ManualTests/AlloyVulkanTest/WindowTransparencyProbe.cs) и assertion-only [DesktopCompositionNative](../../ManualTests/AlloyVulkanTest/DesktopCompositionNative.cs) добавлены в существующий AlloyVulkanTest, запуск — `--window-transparency --validation`. Окна создаются через тот же Silk 2.23.0 / GLFW 3.4.0 с явным TransparentFramebuffer; renderer и PresentOpenGL/VulkanWindowPresenter прежние. Контрольная сцена — три полосы alpha 0/128/255 либо opaque цвет; GPU readback независимо проверяет premultiplied source RGBA. Другое OpenGL-окно даёт два известных цвета фона. Foreground скрывается для проверки самого backdrop, затем показывается; после bounded settling/DwmFlush GetPixel(screen DC) читает центры полос. Opaque endpoint — отдельный положительный контроль. Проверяются initial 420×180, resize 480×220, native minimize и zero-size UI suspension, restore; во всех фактических samples framebuffer равен logical size (scale 1).

В Hosting добавлен **internal**, доступный только manual assembly через InternalsVisibleTo, alpha-selecting constructor и диагностика реально созданных composite alpha/format. Публичный constructor по-прежнему выбирает Opaque первым; конфигурационный API окон не добавлен. Нулевой, составной и отсутствующий composite-alpha flag отвергаются до CreateSwapchain; три native rejection probes проверяют cleanup и пригодность прежнего UiSession/presenter после отказа. Если driver не объявляет PreMultiplied, prototype записывает явный unsupported result без несовместимого swapchain/fallback; эта ветвь на текущей GPU не выполнялась. HWND заимствован у Silk, screen DC закрывается SafeHandle на потоке чтения; новых message hooks/callbacks нет. Packages/native DLL/ABI, Argentis и публичные backend/window contracts не менялись.

**Фактические возможности данной конфигурации**, одинаковые Debug/published Release:

| Путь | Opaque | Whole-window opacity 0/0.5/1 | Desktop per-pixel alpha |
| --- | --- | --- | --- |
| OpenGL / обычный либо transparent framebuffer | Да | Да, отдельный opaque framebuffer | Да, transparent framebuffer |
| Vulkan / обычный presenter, Opaque | Да | Да, отдельный opaque framebuffer | **Нет**, несмотря на GLFW transparent attribute |
| Vulkan / internal PreMultiplied experiment | Отдельный opaque endpoint прошёл | Не совмещалось с transparent framebuffer | **Нет**, хотя driver объявляет PreMultiplied |

Windows 11 Pro Workstations build 26300 x64; RTX 5070 Ti / NVIDIA 32.0.16.1742 (GL 3.3.0 / NVIDIA 617.42); SDK 10.0.401/runtime 10.0.12. GL default framebuffer alphaBits=8. Vulkan surface supportedCompositeAlpha — Opaque|PreMultiplied; реально созданный swapchain format — B8G8R8A8Unorm / SRGB nonlinear color space. Эти результаты не являются утверждением для других GPU/Windows configurations.

Для фона RGB(40,100,180), UI RGB(220,40,80) ожидаются desktop RGB(40,100,180), (130,70,130), (220,40,80). OpenGL дал эти значения. Vulkan Opaque дал (0,0,0), (110,20,40), (220,40,80); PreMultiplied — (255,255,255), (237,147,167), (220,40,80). Последние значения соответствуют смешиванию с белым, но конкретная причина в Windows/driver presentation не установлена. Замена цвета фонового окна не дала ожидаемого per-pixel результата на Vulkan. GPU source alpha/premultiplication при этом прошли отдельные assertions. Не объявлять advertised flags или прозрачную UI-текстуру доказательством desktop transparency.

**Фактически выполненные проверки**, exit 0:

```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -v quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --logger 'trx;LogFileName=ui.trx' --results-directory TestResults/AlloyTransparency -v quiet
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -c Release --no-restore -o TestResults/AlloyTransparency/publish -v quiet
dotnet publish ManualTests/AlloyTest/AlloyTest.csproj -c Release --no-restore -o TestResults/AlloyTransparency/standalone-publish -v quiet
./TestResults/AlloyTransparency/run-final.ps1
```

- Build: 0 errors / 1339 warnings. UI: Passed: 410, Failed: 0, Skipped: 0; новых xUnit cases нет. Оба publish exit 0.
- Final runner: **14/14**, по 7 Debug/published Release: transparency matrix, Settings comparison raster/OpenGL/Vulkan, HUD, short KHR/legacy retirement, обычный standalone OpenGL/Vulkan. Comparison — scales 1/1.5/2, 40 steps, 29 draws/backend; HUD — 90 compositions/presents, 36 UI draws, 2 sessions, 5 readbacks. Standalone GL — 40/41 frames, Vulkan — 40/40. Все scripted взаимодействия прошли, clipboard не использовался.
- Каждый short soak — 5 sessions / 50 frames / 10 created/destroyed swapchains / 1 minimize-restore; resources/subscriptions/retained references 0. Vulkan validation 0 errors / 0 warnings, включая teardown; GL prototype checks NoError.
- Матрица: 7 путей × 2 configurations, по 66 samples / 198 RGB-точек; 6 negative alpha-request cases, 14 create/dispose foreground sessions, background teardown успешен. Во всех четырёх Vulkan путях на configuration — 3 swapchain generations, live swapchains/semaphores/fences/command pools после Dispose 0.
- Независимая PowerShell-сверка пересчитала Matches из raw Expected/Desktop RGB (допуск 3), проверила 7 строк/66 samples на configuration, effective scale 1, rejection counts, lifecycle/validation, UI TRX и совпадение текущих source hashes. Локальные ignored artifacts: TestResults/AlloyTransparency/debug-final.json, release-final.json, environment.json, ui.trx, native-final.log, отдельные native logs, publish directories. Первый диагностический debug.json был без PreMultiplied candidate/GL error guard; final conclusions используют только final reports. Исходные ошибки компиляции bindings исправлены до успешных сборок.
- Итоговый diff не содержит whitespace errors; новые source files проверены на trailing whitespace/conflict markers. Markdown code fences и 136 локальных целей ссылок PLAN/STATUS/manual README проверены. Git index не менялся. Новых interop SYSLIB diagnostics в final build/publish logs нет.

**Исторический вывод и его пересмотр.** После этой матрицы был предложен отдельный Windows composition adapter с CPU/layered-window proof и последующим GPU interop. Новая изоляция4.9a.1 выше установила рабочую комбинацию существующего GLFW/Vulkan пути: план adapter переведён в резерв, следующим шагом стал4.9b. Публичная configuration/lifecycle интеграция ещё не реализована; renderer/HUD ownership сохраняются.

4.9b validation/input в прозрачных областях, borderless/title bar/Snap 4.9c/d, реальные DPI 150/200% и monitor transitions, длинный lifecycle/VRAM soak, настоящий device/context loss, fixed-font full golden и package consumer не проверялись. Sampled alpha bands не заменяют полноценную оконную демонстрацию. K9 timings в этой сессии не повторялись; нижний K9 report относится к предыдущему запуску. Старый Name binding mismatch не воспроизведён в четырёх текущих standalone runs и не считается исправленным. Полный solution test suite не запускался; alpha не завершён.

## K9: профиль и reuse ресурсов Skia — предыдущий запуск 2026-10-08

**Реализация.** Один session-owned SKPaint и bounded FIFO16 SKFont/metrics по family/size разделяются Measure/Draw на raster/OpenGL/Vulkan surfaces. Resize/scale/suspend сохраняют cache; eviction и Dispose освобождают fonts, teardown/constructor rollback включают cache. Thread/disposed guards защищают cached text service. Mutable canvas/context/device и managed typeface wrappers не заимствуются cache; public API/native ABI/packages не менялись. Доступ тестов — новый AssemblyInfo InternalsVisibleTo. [Полный профиль, команды и результаты](PERFORMANCE_BASELINE.md#профиль-и-оптимизация-2026-10-08), [renderer contract](../../TrueMoon.Alloy.Rendering.Skia/README.md#ресурсы-рисования-и-текста).

**Измерения.** Два свежих Release processes до и два после,12cases×500samples —24000rawframes; trace отдельно от timing. GPU driver сейчас32.0.16.1742, поэтому сравнение сделано между свежими парами, не с прежним driver baseline2026-10-07. Managed Update~120KB →46KB/property и140KB →66KB/list (−61.3%/−53.2%). Median Update в этих runs уменьшилась10–14% raster и33–60% GPU; confidence/budget/GPU throughput не заявляются. Все16static batches:0Measure/Arrange/Draw/Update allocations, stable tree/surface/Vulkan image. Verbose EventPipe traces до/после lost0, подтверждены font/paint allocation stacks; первый trace без allocation ticks не использован для этого вывода. Оставшийся sampled text allocation путь — SKTextBlob/Builder; native CPU/VRAM не профилировались.

**Фактически выполнено**, exit0:

- `dotnet build TrueMoon.slnx --no-restore --no-incremental -v quiet`:0errors/1339warnings.
- `dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --filter FullyQualifiedName~SkiaDrawingResourceTests ...`:6passed/0failed/0skipped.
- `dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --logger 'trx;LogFileName=ui.trx' --results-directory TestResults/AlloyPerformanceProfile -v quiet`:410passed/0failed/0skipped; solution --list-tests видит6новых cases.
- Release publish AlloyVulkanTest/AlloyTest:exit0. До/после MeasureSettings.ps1 по2runs,500samples/100frames+500ms warmup,validation off.
- Итоговый локальный run-native.ps1:16/16, по8Debug/published Release: failure/compare/HUD/baseline correctness/standalone GL/Vulkan/short retirement KHR/legacy. Validation0errors/0warnings включая teardown,GL NoError. Каждый soak5sessions/50frames/10swapchains,resources/subscriptions/retained roots0; long200 не запускался.
- verify.ps1 пересчитал24000rawframes и summary/nesting/counters/fingerprints;current source/afterpublish hashes совпали. Artifacts/TRX/logs/traces/analyzer — ignored TestResults/AlloyPerformanceProfile.
- Итоговый `git diff --check`: exit 0. Парность Markdown code fences и локальные цели ссылок в PLAN/STATUS/PERFORMANCE_BASELINE/renderer README проверены успешно; Git index не менялся.

| Проверяемое требование reuse | Точное доказательство |
| --- | --- |
| Stroke/fill/text, color/alpha/font changes сохраняют raster pixels при scales1/1.5/2 | `ReusedPaintAndFonts_AlternatingCommandsMatchUncachedPixels` (3cases) |
| Bounded cache, eviction, метрики и освобождение всех font handles | `FontCache_EvictsOldFontsAndPreservesMeasurementAcrossFamilyAndSizeChanges` |
| Foreign-thread не мутирует/dispose ресурсы; owner Dispose освобождает font/paint и закрывает доступ | `DrawingResources_ForeignAccessCannotMutateOrDisposeOwnerResources` |
| Text service живёт через resize/suspend, после Dispose Measure отвергается | `SurfaceTextService_ResizeSuspensionAndDisposeRespectResourceLifetime` |

**Оставшийся риск.** Первый native runner получил Name binding mismatch в published Release Vulkan smoke (SettingsFormSmoke.Frame stage2/line83 →VerifyValues/line311),exit−532462766. Отдельный retry того же exe и итоговый16/16pass выполнены без изменения кода, но причина/actual values не установлены и исправление не заявлено. First-failure/first-run/retry/final logs сохранены. Rider debugger tools доступны, но для точного repro нет Program.cs run point и AlloyTest run config не поддерживает dynamic args; IDE setup не менялся, suspended/value evidence не получено. При повторе собрать runtime state/call order/input evidence; этот риск учитывается до alpha.

Полный solution test suite, OS clipboard, fixed fonts/full golden, physical DPI/device loss, long lifecycle/VRAM и consumer/packages в этой сессии не проверялись. K9 profile/одна подтверждённая оптимизация/повтор baseline выполнены; alpha не завершён. Следующий конкретный шаг —4.9a: минимальное прозрачное окно OpenGL/Vulkan и capability matrix. Дополнительный text blob cache или dirty-subtree layout не являются следующим согласованным этапом.

## Планирование 4.9: прозрачность и собственное оформление окна — предыдущая документационная работа 2026-10-08

По запросу пользователя в [PLAN](PLAN.md#подзадача-49-прозрачные-окна-и-собственное-оформление--добавлена-2026-10-08) добавлены4.9a (проверка оконной композиции OpenGL/Vulkan),4.9b (общая opacity и per-pixel alpha как отдельные режимы),4.9c (собственный заголовок/кнопки/resize),4.9d (опциональный native move/resize/Snap и Windows11 Snap Layouts). Это расширение объёма первой версии перед итоговым alpha; текущий ближайший K9 profile сохраняется. Имена API и presentation adapter ещё не выбраны. Platform/renderer ownership и net10.0/xUnit сохраняются.

**Состояние:** требования записаны;4.9 не реализован и не проверен. Статически прочитан SilkWindowHost: окно создаётся через Silk WindowOptions и имеет borrowed Win32 HWND для clipboard; этим поддержка прозрачности/native Snap не подтверждается. Проверена официальная документация GLFW и Microsoft: whole-window opacity и transparent framebuffer нельзя совмещать на GLFW пути; custom non-client hit testing — направление native интеграции. Риски до prototype: фактическая desktop alpha в каждом GPU backend, сохранение системного Snap при скрытом chrome, DPI/resize и lifecycle hooks. Наличие renderer UI alpha не снимает эти риски.

В предыдущем документационном запуске изменены только PLAN/STATUS и перечень следующих шагов. Выполнено повторное чтение сохранённых разделов и сверка новых пунктов/ссылок/порядка. Build, xUnit, native/GPU, clipboard, publish и profiling тогда не запускались; результаты от2026-10-07 ниже являются предыдущими. На тот момент следующим шагом был CPU/allocation profile K9; он выполнен в текущем запуске выше. Первый шаг4.9 — минимальный прозрачный OpenGL/Vulkan оконный prototype и capability matrix. Нижние старые таблицы этапов/K1–K9 содержат промежуточные summaries; актуальные evidence shared scene/clipboard/controlled K8/K9 находятся в датированных разделах выше этих таблиц.

## Settings performance baseline K9 — предыдущий запуск 2026-10-07

**Реализация.** [SettingsPerformanceProbe](../../ManualTests/AlloyVulkanTest/SettingsPerformanceProbe.cs) запускается через `--settings-baseline` в AlloyVulkanTest. Использует actual compile-linked AlloyTest.View1/SettingsModel,32 строки/195 исходных элементов, view720×1000/viewport900×1100/scale1, dark/default density/Ready без фокуса. Четыре варианта (raster, OpenGL, Vulkan offscreen, Vulkan HUD) × static/property-edit/list-edit. Изменение Name и bounded append/remove измеряются отдельно от Update; в list-edit33строки/200элементов на нечётном состоянии. Proxy surface и transparent profiling Panel фиксируют Update/MeasureCore/ArrangeCore/Render, сохраняют identity controls/row models/generated nodes/UI Vulkan image, проверяют no creates/resize за batch и Dispose/unsubscribe. HUD продолжает scene composition и presentation при static UI, swapchain generation1, presenter-owned handles0 после Dispose.

[PerformanceReport](../../ManualTests/AlloyVulkanTest/PerformanceReport.cs) сохраняет все raw frame samples и JSON/CSV/Markdown summaries, timer calibration, warmup/batch/GC counters, scene/runtime/native revision/asset/source/binary/font fingerprints. [MeasureSettings.ps1](../../ManualTests/AlloyVulkanTest/MeasureSettings.ps1) записывает Windows/CPU/GPU/driver/SDK/Git/power metadata и выполняет независимые sequential Release processes. Runtime/Argentis/backend/native ABI/packages этим подэтапом не менялись; новый harness находится только в manual project. Метод отражён в PLAN и [PERFORMANCE_BASELINE.md](PERFORMANCE_BASELINE.md); запускаемые команды добавлены в [manual README](../../ManualTests/AlloyVulkanTest/README.md#performance-baseline-settings-k9).

**Фактически выполненные final checks**, exit0:

```powershell
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -c Release --no-restore -o TestResults/AlloyPerformance/publish -v quiet
$env:VK_LAYER_PATH=(Resolve-Path TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin).Path
./ManualTests/AlloyVulkanTest/MeasureSettings.ps1 -Executable TestResults/AlloyPerformance/publish/AlloyVulkanTest.exe -OutputDirectory TestResults/AlloyPerformance/correctness -Samples 4 -Warmup 2 -WarmupMilliseconds 0 -Runs 1 -Validation
./ManualTests/AlloyVulkanTest/MeasureSettings.ps1 -Executable TestResults/AlloyPerformance/publish/AlloyVulkanTest.exe -OutputDirectory TestResults/AlloyPerformance/baseline
dotnet build TrueMoon.slnx --no-restore --no-incremental -v quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --logger 'trx;LogFileName=ui.trx' --results-directory TestResults/AlloyPerformance
./TestResults/AlloyPerformance/verify-baseline.ps1
./ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.exe --settings-baseline --baseline-samples 4 --baseline-warmup 2 --baseline-warmup-ms 0 --baseline-output TestResults/AlloyPerformance/debug-final.json --validation
./TestResults/AlloyPerformance/publish/AlloyVulkanTest.exe --settings-compare --validation
```

Final solution build **0errors/1339warnings**, UI **404passed/0failed/0skipped**. Release publish exit0. Короткие correctness baseline в Debug и published Release — по12cases×4measured frames, все contract assertions прошли, Vulkan validation **0errors/0warnings включая teardown**, OpenGL GetError=NoError. Release shared backend comparison — scales1/1.5/2, по40interaction steps/29script draws на backend плюс restore; layout/model/input и выбранные RGBA pixels с допуском1 совпали, validation0/0. Полный solution test suite и standalone publish в текущем K9 запуске не выполнялись; их прежние результаты ниже исторические.

Два performance-процесса по12cases×500samples (6000кадров на процесс,12 000 всего), warmup≥100frames **и** ≥500ms. Validation в timing выключен; отдельные validation checks приведены выше. Во всех8static batches —0Measure/Arrange/Render,0B/Update,0Gen0/1/2collections; в16edit batches ровно500Measure/Arrange/Render. UI surface Creates/Resize=0 за batch, исходные195nodes/32rows сохранились, Vulkan image не менялся. Timing/counter assertions не утверждают отсутствие всех native allocations/VRAM. Локальный verifier пересчитал12 000raw frames/216phase summaries/216CSVrows: nearest-rank median/p95/mean/bytes/calls совпали, nested intervals/allocations не превышают Update, warmup/node/resource bounds соблюдены. Source/binary/font/native/scene/runtime fingerprints обоих процессов одинаковы.

Final documentation/source verification:6 документов/178local links включая anchors,63changed/untracked text files без NUL/conflict markers/trailing whitespace; git diff --check exit0, index0. Текущие source/binary SHA256 совпадают с измеренным Release report; UI TRX и3 enabled-validation native logs проверены отдельно. Проверки — TestResults/AlloyPerformance/verify-docs.ps1 и docs-verification.log; новые и предыдущие изменения не staged/committed.

**Результат.** Median Update property-edit: raster3.796/3.798ms,GL1.202/1.183ms,Vulkan0.898/0.878ms,HUD1.239/1.200ms. List-edit: raster3.989/3.913ms,GL1.417/1.217ms,Vulkan0.940/0.910ms,HUD1.293/1.309ms. Mean Update allocations~120KB/property и~140KB/list, mutation368B/248B отдельно; render~86–88KB, measure~31–32KB. Эти вложенные bytes/time нельзя складывать с Update повторно. GL Flush/Vulkan recording имеют разные точки завершения; GPU throughput таким сравнением не доказан. GL completion/Vulkan export-return/HUD composition/presentation вынесены отдельно. Median host presentation13.6–15.2ms; статичное Update около timer floor, no-op median0µs/p950.1µs. OpenGL list median отличается на−14.2% во втором запуске, поэтому малые изменения времени требуют повторных сравнений, без arbitrary performance threshold.

Конфигурация: Windows11build26300x64, Ryzen9 9900X12cores/24threads, RTX5070Ti выбран дляGL/Vulkan, NVIDIA driver32.0.16.1692/GL616.92, SDK10.0.401/runtime.NET10.0.12, Release/net10.0, workstation GC/Interactive, no debugger; affinity/priority/power/GC не менялись. NativeSkia4.153.1/ABI1 и PNG hashes прежние; exact native revisions/source+binary+font hashes — в baseline документе и JSON. Шрифты Segoe/system fallback, ещё не доставляемые fixed-font fixtures. Raw reports/environment/logs/publish/TRX и verifier — ignored TestResults/AlloyPerformance; timing reports20261007-215144-run1/run2, Release correctness20261007-214811-run1, Debugdebug-final. Clipboard не читался.

**Промежуточные попытки.** Первые Debug/Release сборки harness использовали guessed presenter resource member names и не собирались; исправлены по фактическому public API. Короткий ранний Debug dry-run прошёл до окончательной проверки UI image/native/source metadata, поэтому его timings не используются как baseline. Один вспомогательный PowerShell read ошибочно поставил pipeline после foreach statement; исправлен, до замеров/проверок результата не доходил. Финальные результаты выше относятся к текущему исправленному коду.

**Следующий конкретный шаг и границы.** Baseline K9 зафиксирован; основная наблюдаемая фаза allocations/time — surface-render, но конкретные call stacks ещё не получены. Выполнить CPU/allocation profile её и text measure/draw пути на этой же сцене, определить lifetime/ownership кандидата на reuse, реализовать одну подтверждённую оптимизацию и повторить baseline + UI/native correctness. Dirty-subtree layout/partial redraw, native/VRAM профилирование, fixed fonts/goldenK2, physical DPImonitor transitionsK3, long soakK7, физический device/context lossK8 и consumer/package delivery5.4/5.5 остаются отдельными требованиями. Baseline не завершает alpha и не является benchmark confidence/budget claim.

## Отказ графики и явное пересоздание 2.4/K8 — предыдущий запуск 2026-10-07

**Реализация.** [UiRenderingException](../../TrueMoon.Alloy/Runtime/UiRenderingException.cs) хранит backend/operation/kind и original cause; DeviceLost/ContextLost/BackendFailure обозначают подтверждённый terminal failure. [UiSession](../../TrueMoon.Alloy/Runtime/UiSession.cs) сохраняет первый RenderingFailure/IsFaulted, сообщает RenderingFailed один раз на owner thread, отменяет focus/capture/hover и queued posts. Все session rendering/input/export/Post/theme entry points после fault отвергаются; VerifyAccess допускает cleanup. Отказ, сообщённый внутри Post или layout без throw, проверяется до следующего Render. Обычный untyped DrawCore error остаётся retryable и сохраняет invalidation.

[UiCleanup](../../TrueMoon.Alloy/Runtime/UiCleanup.cs) сохраняет operation error и выполняет все переданные безопасные cleanup actions. Session Dispose продолжает освобождение дерева/surface/registry observers при cleanup errors; Root caller-owned при ошибке direct Create. IUiRenderSurface.VerifyAvailable проверяет thread/lifetime/lease отдельно от GPU health. Borrowed frame, foreign thread, notification/Update/reentrant Dispose отвергаются до destructive cleanup. CancelInput пытается обработать независимые focus/capture/hover даже при ошибке одного элемента.

Skia GL/Vulkan abandoned context сообщает ContextLost; session GL/Vulkan export helpers фиксируют typed failures. VulkanDevice.Check(ErrorDeviceLost) сообщает DeviceLost с исходным Result error. VulkanTextureLease.AbandonAfterDeviceLoss разрешён только для подтверждённого DeviceLost, освобождает export handle и abandons context без GPU calls; borrowed device/instance/loader живут до UI context teardown. При обычном fault lease всё ещё требуется вернуть после завершённой GPU работы. Return abandoned context пропускает native state update, сохраняя обязательный host completion. Неизвестный wait/current-context failure сохраняется, Skia cleanup использует abandon и не обещает освобождение driver allocations.

[HostedUiWindow](../../TrueMoon.Alloy.Hosting/HostedUiWindow.cs) сохраняет original operation + cleanup errors, catching Closing/render/input/commands внутри native callback boundary. Explicit report без throw также завершает окно с первым failure. Порядок cleanup: UI → presenter/device → window/input, close callback один раз. Host-owned root освобождается при Create failure. Vulkan presenter сохраняет presentation error при Return failure, при подтверждённом DeviceLost abandons lease; teardown проверяет WaitIdle/fences и сохраняет DeviceLost. Presenter/device не разрушаются, если UI/presenter cleanup не завершился. Registry после thread/lease preflight пытается закрыть все сессии при ошибке одной. Конкретный контракт добавлен в PLAN и [Hosting README](../../TrueMoon.Alloy.Hosting/README.md#отказ-графики-и-явное-пересоздание-24k8); backend-independent Alloy остаётся Argentis+BCL, packages/native ABI/assets не менялись.

**Проверки контракта.** [RenderingFailureTests](../../TrueMoon.Alloy.Tests/RenderingFailureTests.cs): 15 методов/18 xUnit cases. Existing tests не изменялись. Source/assertion review через assertion-quality/test-gap-analysis выполнен ограниченно для новых cases; не является coverage/mutation-tool прогоном. Выявленные пробелы Post/layout continuation, independent input cancellation и zip без проверки количества rows закрыты конкретными assertions. Внутренние research/plan/pairing/review artifacts сохранены вне stageable workspace.

| Требование | Evidence текущего запуска |
| --- | --- |
| Kind/operation/backend/cause и constructor guards | `Exception_PreservesClassificationOperationAndOriginalCause`, `Exception_RejectsMissingCauseNamesAndUnknownKind` |
| Terminal Render/Resize, first failure once, cancellation, no subsequent work | `TerminalRenderOrResize_ReportsOnceAndBlocksFurtherWorkUntilExplicitDisposal` |
| External report, posts discarded, observers all attempted, reentrant Dispose guard | `ExternalFailure_DiscardsPendingPostsAndNotifiesEveryObserverWhilePreventingReentrantDisposal` |
| Untyped draw retry | `OrdinaryDrawError_RemainsRetryableAndDoesNotReportTerminalFailure` |
| Failure inside Post/layout without throw prevents draw | `FailureReportedByPostedWork_PreventsThePendingDrawAndCancelsRemainingPosts`, `FailureReportedDuringLayout_PreventsRenderingAfterMeasurementReturns` |
| Input cancellation error still cancels capture/hover and reaches observers/cleanup | `InputCancellationError_DoesNotSkipCapturedElementHoverOrFailureObservers` |
| Borrowed-frame/thread ownership before cleanup | `BorrowedSurface_FaultDoesNotTransferOwnershipOrPermitPrematureDisposal`, `ForeignThread_ReportAndDisposalRejectBeforeMutation` |
| All cleanup actions, cause identity/order and argument validation | `Cleanup_AttemptsEveryActionAndPreservesPrimaryAndCleanupErrorIdentityInOrder`, `Cleanup_ValidatesAllActionsBeforeStartingAndRethrowsSingleCleanupError` |
| Tree/surface/registry observer cleanup errors preserved, once disposal | `SessionDisposal_ReportsAllCleanupErrorsAndStillDetachesTreeAndNotifiesRegistryOnce` |
| Failed Create preserves caller-owned root and both errors | `CreationRollback_KeepsCallerRootAndPreservesAttachmentAndSurfaceCleanupErrors` |
| Actual View1/SettingsModel recreated with preserved row count/identities, new controls/input and no old subscriptions | `ExplicitRecreation_PreservesCallerModelRowsAndCreatesNewTreeWithoutOldSubscriptionsOrFocus` |
| Real raster/GL/Vulkan surface/readback after injected terminal failure and explicit recreation | `RenderingFailureProbe.Exercise`, Debug + published Release |
| Real Vulkan lease retained at fault; invalid abandonment rejected, normal Return unlocks cleanup; result classification | `RenderingFailureProbe.RunAsync`, healthy actual device + synthetic Check result; validation0/0 |
| Registry best-effort / native callbacks / Ready/Completion/StopAsync / retained-model new windows | `RenderingFailureProbe.RegistryCleanup`, `WindowAsync`, `CreationFailureAsync`; GL/Vulkan draw/report/create failures,4 explicit new windows per configuration |

**Фактически выполненные final checks**, exit0:

```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -v quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --filter 'FullyQualifiedName~RenderingFailureTests' --logger 'trx;LogFileName=focused.trx' --results-directory TestResults/AlloyRenderingFailure
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --logger 'trx;LogFileName=ui.trx' --results-directory TestResults/AlloyRenderingFailure
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -c Release --no-restore -o TestResults/AlloyRenderingFailure/publish-backends -v quiet
dotnet publish ManualTests/AlloyTest/AlloyTest.csproj -c Release --no-restore -o TestResults/AlloyRenderingFailure/publish-standalone -v quiet
./TestResults/AlloyRenderingFailure/run-native.ps1
```

Focused **18 passed/0 failed/0 skipped**, whole UI **404 passed/0 failed/0 skipped** (все прежние386 прошли). Final solution build **0 errors/1339 warnings**, оба publish exit0. Native script последовательно запускает в Debug и published Release `--settings-failure --validation`, `--settings-compare --validation`, `--settings-hud --validation`, standalone `--smoke` и `--vulkan --smoke --validation`: **10/10 exit0**. Comparison3scales×3backend×40steps,29 script draws +restore; HUD90 compositions/36 UI draws/2sessions/5readbacks; standalone GL41frames/Vulkan40frames в обеих конфигурациях. Failure probe: controlled draw/report/create failure, operation/tree/close error identities сохранены, new model windows, registry0. Vulkan validation **0 errors/0 warnings** включая teardown; comparison/failure GL GetError=NoError. OS clipboard не использовался. Логи/TRX/publish/native runner — ignored TestResults/AlloyRenderingFailure; промежуточные logs — TestResults/AlloySharedSettings/k8-*.

**Неуспешные промежуточные попытки.** Первая сборка обнаружила CS1056: UiSession.cs/Rendering.cs/UiRenderingException.cs содержали NUL bytes; причина записи неизвестна. Эти три файла переписаны из уже прочитанного текущего source и внесённых изменений, без Git restore/checkout; final reads/build подтверждают обычный текст. Test/probe compilation сначала использовали несуществующие имена API/internal OpenGL option, исправлены по public API. Две промежуточные unit checks выявили неверный pressed-state fixture и ожидаемую OnInputCancelled ошибку при detach; assertions исправлены. Native checks сначала сравнивали порядок AggregateException.Flatten вместо структуры original/cleanup, затем ожидали pending binding в уже faulted editor; probe исправлен на structural error identity и сохранение caller model отдельно от отменённого binding. Финальные checks после этих исправлений приведены выше. Новый nullable warning HostedUiWindow устранён; final count вернулся к1339.

**Границы и следующий конкретный шаг.** Контракт2.4/K8 и controlled failure/recreate реализованы и проверены. Физические device/context loss, accepted DeviceLost abandonment, native lost-device teardown, driver allocations после abandon и standalone retention при неизвестном completion failure не индуцировались; typed result classification не является доказательством real loss. Пересоздание выполнено на здоровом прежнем device после controlled BackendFailure; настоящий replacement device/context предоставляет host. Полный K8/alpha этим не объявляется завершённым. Следующий шаг по K1–K9/5.4–5.5: **K9 baseline текущей settings сцены** — зафиксировать Windows/GPU/driver/SDK/native revisions, warmup/samples, logical size/element count, измерить layout/draw time и allocations в static/property-edit/list-edit режимах; scene composition/presentation учитывать отдельно. Fixed fonts/full golden/K2/K4, физические DPI transitions, full solution tests, long K7/VRAM и consumer/NuGet delivery остаются. Коммит не создавался.

Final review: `git diff --check` exit0;5 документов,165 local links/anchors и59 changed/new text files проверены на NUL/conflicts/whitespace/final newline. Текущие focused18 и UI404 TRX, все10 final native logs сверены. PNG/native Skia hashes совпали для Debug и обоих publish; delivery.json сохранён с final evidence. Git index0. Последние правки после native runs — тестовые assertions и документы; runtime/backend/host код не менялся.

## Общая сцена raster/OpenGL/Vulkan/HUD 5.1c/5.2–5.3 — предыдущий запуск 2026-10-07

**Реализация.** [AlloyVulkanTest.csproj](../../ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj) compile-link исходные View1/SettingsModel/SettingsRow/LoadStatus/LoadStatusRegion/VolumeMeter/SettingsFormSmoke из AlloyTest; доставляет исходный PNG через PreserveNewest, не зависит от manual executable и не вводит shared assembly/packages. SettingsFormSmoke.Frame принимает optional View1 для вложенной формы HUD, identity checks ограничены этой формой. Standalone caller без аргумента сохраняет прежнее поведение. [AlloyTest Program](../../ManualTests/AlloyTest/Program.cs) выбирает OpenGL по умолчанию или Vulkan через `--vulkan`; тот же View1/model/40-frame script. `--validation` требует Vulkan, bounded smoke считает warning/error messages и отвергает ненулевой результат после teardown.

[SettingsBackendProbe](../../ManualTests/AlloyVulkanTest/SettingsBackendProbe.cs) выполняет40 interaction steps на raster/OpenGL/Vulkan для3 viewports scale1/1.5/2 с logical900×1100. На каждом шаге сохраняет bounds всего дерева и model/rows/focus/selection/scroll/appearance/disabled state; сравнивает все записи и draw count. Readback после static/suspend/restore сравнивает5 selected RGBA samples (theme background, button, PNG, meter, scroll) с tolerance1 на канал; glyph/AA edges не используются. После40steps каждый backend выполняет5 static Update без redraw, zero viewport без draw, restore с одним draw, Dispose формы/branches/generated nodes и model mutation после Dispose без обновления старого editor. Raster/GL CPU image и Vulkan lease/readback используются только для assertions, Vulkan lease возвращается с actual layout в finally.

[SettingsHudProbe](../../ManualTests/AlloyVulkanTest/SettingsHudProbe.cs) включает ту же форму720×1000 в прозрачный Panel game-owned окна1100×1100, alpha metrics overlay/scene button рядом. App регистрирует external Vulkan backend; raw host descriptor заимствует device handles/loader/wait callback. Host owns device/window/compositor/presenter и кадры сцены; UI owns дерево/сессию/Skia output.40-step script тот же, background сцены меняется каждые15 compositions без изменения UI; необработанный и незахваченный ввод идёт сцене. Script проверяет outside passthrough/scene button capture, actual resize1280×1200→minimize/restore→1100×1100, explicit zero UI viewport, первый Dispose с registry0 и новое дерево из той же модели с Unicode Name/Rows. Old focus отменён; новая сессия также маршрутизирует scene input в новые metrics. UI/textures освобождаются перед host device, device.WaitIdle после App stop подтверждает доступность borrowed host. Managed callback failures перехватываются внутри GLFW handlers, после Run становятся managed failure; bound30s. `--settings-hud-demo` интерактивен до Escape/закрытия, scripted assertions отключены.

| Требование | Свежие evidence |
| --- | --- |
| Одни controls/model/list/regions/theme/scroll/input для3 backend | `SettingsBackendProbe.Exercise` + исходный `SettingsFormSmoke.Frame`;3scales×3backend×40steps |
| Layout/state/selected RGBA и retained draw согласованы | `SettingsBackendProbe.Match`,29 script draws на сессию,5 selected samples с tolerance1;5 static Update без draw,+1 restore draw |
| HUD model/list/passthrough/alpha и scene composition отдельно от UI | `SettingsHudProbe.Run`,90 compositions/presents против36 UI draws,5 GPU readbacks |
| Смена UI-сессии из прежней модели, focus cleanup, новое scene routing | `SettingsHudProbe.Run`,2sessions, registry0 между ними и после stop; Unicode Name/Rows сохранены |
| Standalone backend selection на общем script | AlloyTest `--smoke` и `--vulkan --smoke --validation`, по40frames в Debug/Release |
| Borrowed device/validation/lifecycle | HUD raw handles; lease.Return finally, registry/tree0, device.WaitIdle после UI/App teardown; Vulkan0errors/0warnings включая disposal |

**Фактически выполненные проверки.** Все команды exit0 (log redirects опущены). Process-only VK_LAYER_PATH для native Vulkan команд указывает на уже существующий `TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin`; системная конфигурация не менялась.

```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --logger 'trx;LogFileName=ui.trx' --results-directory TestResults/AlloySharedSettings
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -c Release --no-restore -o TestResults/AlloySharedSettings/publish-backends
dotnet publish ManualTests/AlloyTest/AlloyTest.csproj -c Release --no-restore -o TestResults/AlloySharedSettings/publish-standalone
./ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.exe --settings-compare --validation
./ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.exe --settings-hud --validation
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --smoke
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --vulkan --smoke --validation
./TestResults/AlloySharedSettings/publish-backends/AlloyVulkanTest.exe --settings-compare --validation
./TestResults/AlloySharedSettings/publish-backends/AlloyVulkanTest.exe --settings-hud --validation
./TestResults/AlloySharedSettings/publish-standalone/AlloyTest.exe --smoke
./TestResults/AlloySharedSettings/publish-standalone/AlloyTest.exe --vulkan --smoke --validation
```

UI386passed/0failed/0skipped, existing xUnit cases не менялись/не добавлялись. Solution0errors/1339warnings; both publish exit0. Debug/Release comparison каждый3scales×3backend,40steps,29 script draws +1 restore draw; all bounds/states/pixels совпали. HUD каждый90frames/90compositions/36UI draws/2sessions/5readbacks; standalone каждый40frames. Vulkan comparison/HUD/standalone validation0/0 включая teardown, GL comparison GetError=NoError. Первоначальный HUD35draws прошёл до добавления проверки scene routing новой сессии; final HUD36draws повторно прошёл в Debug/Release. Других неуспешных build/test/native attempts этого подэтапа не было.

PNG hash source/Debug-GPU/published GPU/published standalone совпал: `BB07D1AC6AC509E052F45F2304980DCCBB42B3089DA49BE75C52DA99A3089B3E`. Native Skia hash original/both publish совпал: `96525E8EFC1E3BB4B9C64000307D4D11C378FEDAEEB7C1E62D518EF84CDD1ABE`. Логи/TRX/delivery.json/publish — ignored `TestResults/AlloySharedSettings`. PLAN и оба manual README описывают source sharing/запуск/ownership/ограничения. Production runtime/controls/backend/native ABI/assets/packages не менялись; изменения в manual integration/script/docs.

**Пределы и следующий шаг.** Это функциональный shared-view проход5.2/5.3, не итоговая alpha/K1–K9 сертификация. Fixed-font assets/full golden/text raster equivalence не добавлены; используются существующие system fonts и selected solid pixels. Comparison scales — logical simulation, HUD actual resize здесь scale1; физический Windows DPI transition/native OS input не проверен. OS clipboard не использовался; прежний native clipboard pass не повторялся. Full solution tests, legacy probes, device/context-loss, injected backend failure, interactive `--settings-hud-demo`, long K7 soak/VRAM/driver profiling/K9 baseline/NuGet consumer не запускались. Counters не заменяют VRAM/allocations/time measurements; успешная смена UI-сессии не моделирует device loss. Следующий шаг — согласованный2.4/K8: уточнить backend error→host contract, остановку недопустимой работы, best-effort cleanup и explicit new session из сохраняемой модели, с отдельным evidence controlled injection/real device loss. Коммит не создавался.

Final review/проверка: shared sources не скопированы, callback boundary/lease return/owner disposal проверены; old scene metrics после смены сессии заменены новыми, final scripted passthrough проверяет именно новую ветвь. `git diff --check` exit0;47 changed/new text files без whitespace/conflict/final-newline ошибок. Fences5 документов парны,156 local links/anchors существуют; текущий TRX386/386 и все8 Debug/Release native logs сопоставлены с passing evidence. Verification script/log в ignored TestResults/AlloySharedSettings, Git index0. Последние изменения после executable checks — только документы.

## Windows clipboard native fixture 4.3/5.1c — предыдущий запуск 2026-10-07, 17:35–17:36

Пользователь сообщил «скопировал текст… готово», разрешив использование подготовленного заменяемого fixture. Read-only PowerShell `Get-Clipboard -Raw` до запуска подтвердил точное case-sensitive совпадение строки `Alloy clipboard smoke fixture v1`, length32; содержимое произвольного clipboard не выводилось. Native smoke дополнительно проверяет ту же строку внутри UiSession перед любой записью/restore.

**Свежие проверки**, последовательно, все exit0:

```powershell
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --smoke-clipboard
./TestResults/AlloyClipboardWrite/publish/AlloyTest.exe --smoke-clipboard
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --window-lifetime
./TestResults/AlloyClipboardWrite/publish/AlloyTest.exe --window-lifetime
```

Native OS smoke Debug40/Release41 frames: Latin/Cyrillic/emoji ввод, selection целой `🧑‍💻` графемы, Ctrl+C с повторным native чтением той же графемы, Ctrl+X с обновлением модели, Ctrl+V с восстановлением текста и model/UI binding. Оба сценария прошли остальную interaction сцену и Dispose/detach формы, generated rows/status branches; вывод `OS clipboard: verified`. Это actual SilkWindowHost/Win32 Get/Set на UI owner thread в HostedUiWindow, без controlled IUiClipboard. Успешная публикация через SetClipboardData и последующие native чтения теперь проверены, а не только fake ownership protocol.

Lifecycle Debug6/Release6 OpenGL/Vulkan: live HWND через Run/Closing/caught render failure, повторное чтение после Run, invalid null/NUL write и Get/Set с чужого потока отвергнуты до native записи, Get/Set после Dispose отвергнуты, input освобождён до окна, повторный Dispose безопасен. В отличие от предыдущих неуспешных попыток все6 сценариев каждой сборки завершились.

После всех четырёх запусков независимый `Get-Clipboard -Raw` подтвердил **точное восстановление fixture**. Metadata snapshot: sequence103, formats4, open window0; fixture length32. Probe восстанавливает только текст, не исходные произвольные formats, как согласовано. Sequence/format snapshot не является before/after доказательством сохранения форматов; native запись в этом запуске намеренно выполнялась только на разрешённом fixture. Логи: ignored `TestResults/AlloyClipboardWrite/{debug-os-ready,release-os-ready,debug-lifetime-ready,release-lifetime-ready}.log`, `metadata-ready.json`.

**Пределы и продолжение.** Код/PLAN/API/бинарные артефакты не менялись; использованы проверенные ранее Debug и published Release сборки. Build/xUnit/publish/controlled recovery/factory probes сейчас повторно не запускались, их результаты в следующем разделе. Реальный OS отказ записи с последующим восстановлением не индуцировался: error/cleanup/retry evidence остаётся deterministic tests + controlled window smoke. Предыдущий OpenClipboard/error5 сейчас не воспроизводится на fixture; это не устанавливает его причину. Access/fixture блокер native positive checks снят. Следующий шаг — shared View1/model сцена для5.2–5.3; full backend/HUD/font/DPI/OS-events/soak/alpha проход ещё предстоит. STATUS и sample README обновлены, коммит не создавался.

## Windows clipboard запись и recovery 4.3/5.1c — предыдущий запуск 2026-10-07

**Реализация и согласованное решение.** Пользователь разрешил реализовать рекомендации: общий Win32 read/write, явное владение памятью и сохранение UI после operational clipboard errors. [Win32Clipboard](../../TrueMoon.Alloy.Platform.Silk/Win32Clipboard.cs) готовит UTF-16 draft до Open; [Win32ClipboardApi](../../TrueMoon.Alloy.Platform.Silk/Win32ClipboardApi.cs) использует GlobalAlloc(GMEM_MOVEABLE|ZEROINIT), lock/copy/unlock, EmptyClipboard/SetClipboardData. Draft освобождается при отказе до передачи; после успешной публикации принадлежит Windows даже при Close failure. Все operation/cleanup errors сохраняются в nested AggregateException. Open error5 допускает десять10ms waits, другие ошибки не повторяются. Изменение clipboard после EmptyClipboard не откатывается. Это те же replacement semantics, что у прежнего GLFW setter; нового обещания сохранения произвольных форматов нет. Read memory остаётся borrowed. Native ABI renderer/packages не менялись.

[SilkWindowHost](../../TrueMoon.Alloy.Platform.Silk/SilkWindowHost.cs) оборачивает только operational Win32/invalid-data/GLFW errors и их aggregates в [UiClipboardException](../../TrueMoon.Alloy/Runtime/UiClipboardException.cs), сохраняя Read/Write и cause. Owner-thread/disposed/null/NUL guards работают до native записи; programming errors не понижаются до operational. [UiSession](../../TrueMoon.Alloy/Runtime/UiSession.cs) перехватывает только этот тип в routed input, сообщает ClipboardFailed и возвращает handled input. Прямой Get/Set и ошибки подписчика продолжают распространяться; Dispose снимает подписчиков. TextBox уже записывал clipboard до удаления выделения при Cut, изменения контрола для recovery не потребовались. PLAN и Hosting README фиксируют контракт.

Первый setter patch снова отклонён automatic approval review из-за существующих18 форматов. Повторный review принял патч после предъявления явного разрешения пользователя, эквивалентных прежних setter semantics и уже установленного exact-fixture guard. Это разрешение реализации кода, не факт выполнения native записи. Текущего review-блокера реализации нет.

[SettingsFormSmoke](../../ManualTests/AlloyTest/SettingsFormSmoke.cs) допускает native copy/cut/paste **только** если Get возвращает точную строку `Alloy clipboard smoke fixture v1`; проверка стоит до write/restore try-finally. Restore сохраняет только текст fixture. [ClipboardFailureSmoke](../../ManualTests/AlloyTest/ClipboardFailureSmoke.cs) через DI-декоратор IUiSessionFactory подставляет controlled clipboard в настоящий HostedUiWindow: проверяет три ошибки, сохранение редактора, успешные повторы и registry0 после закрытия. OS clipboard этот режим не использует. [WindowLifetimeProbe](../../ManualTests/AlloyTest/WindowLifetimeProbe.cs) дополнен invalid-text/owner-thread/write-after-dispose guards; native read остаётся обязательной частью probe.

**Точное test evidence.** [Win32ClipboardTests](../../TrueMoon.Alloy.Tests/Win32ClipboardTests.cs):19 methods/31 cases, из них новые9/14. [ClipboardInteractionTests](../../TrueMoon.Alloy.Tests/ClipboardInteractionTests.cs):8 methods/13 cases, compile-linked actual View1/TextBox. UI tests также compile-link настоящий Win32ClipboardApi для native memory roundtrip без доступа к системному clipboard; AllowUnsafeBlocks нужен LibraryImport generator, новых dependencies нет.

| Требование | Evidence |
| --- | --- |
| Empty/Unicode draft, Prepare→Open→Publish→Close, transfer без Free | `WriterSuccess_PreparesBeforeOpenTransfersBeforeCloseAndDoesNotFree` |
| Transient1/10, persistent11 и other error; draft освобождён без Publish/Close | `WriterTransientAccessDenied_RetainsDraftUntilPublish`, `WriterOpenFailure_FreesDraftWithoutPublishingOrClosing` |
| Allocation/publish/close/free failures, все causes; успешный transfer необратим | `AllocationFailure_DoesNotOpenOrPublish`, `PublishFailure_ClosesAndFreesUntransferredDraft`, `PublishCloseAndFreeFailures_PreserveAllErrorsInOrder`, `SuccessfulPublishAndCloseFailure_NeverFreeSystemOwnedMemory` |
| null/NUL до Allocate, native UTF-16 empty/Unicode с repeated read/unlock/free | `InvalidWriterText_IsRejectedBeforeAllocation`, `NativeGlobalMemory_Utf16RoundTripAndRepeatedReadReleaseBorrowedLock` |
| Copy/cut/paste failure: value/model/focus/selection/static frame сохранены, notification и успешный retry | `ClipboardFailure_ReportsOperationAndPreservesActualDemoEditorUntilRetry` |
| Без подписчика ввод handled; direct access throws без notification | `ClipboardFailure_WithoutSubscriberRemainsHandledAndSessionUsable`, `DirectClipboardAccess_ThrowsTypedFailureWithoutInputNotification` |
| Programming/observer exceptions распространяются; empty selection не обращается к сервису | `UnexpectedClipboardProviderFailure_IsNotSilenced`, `ClipboardObserverFailure_PropagatesAndDoesNotModifyEditor`, `CopyAndCutWithoutSelection_DoNotAccessClipboard` |
| Clipboard publication может состояться до отказа, editor не меняется; invalid operation/null cause | `FailureAfterPublication_PreservesEditorWithoutPromisingClipboardRollback`, `ClipboardException_RejectsInvalidOperationAndMissingCause` |

Reader/parser evidence прежних17 cases находится в следующем историческом разделе; все они повторно прошли. Final quality review: fake проверяет observable protocol/ownership, UI asserts используют actual demo/model и сохранённые исходные значения; unexpected exceptions и post-publication side effect проверены отдельно. Неподтверждённое native ownership transfer не выдаётся за доказанное тестом памяти. Coverage/empirical mutation не запускались. Roslyn pairing выполнен один раз:1649 sources/212 tests/189 paired/1460 unpaired — эвристика source-name pairing, не измерение coverage. Internal research/plan/status/pairing находятся в Windows temp вне stageable workspace.

**Свежие успешные команды** (все exit0; перенаправление логов опущено):

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --filter 'FullyQualifiedName~Win32ClipboardTests|FullyQualifiedName~ClipboardInteractionTests' --logger 'trx;LogFileName=focused.trx' --results-directory TestResults/AlloyClipboardWrite
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --logger 'trx;LogFileName=full.trx' --results-directory TestResults/AlloyClipboardWrite
dotnet build TrueMoon.slnx --no-restore --no-incremental
dotnet publish ManualTests/AlloyTest/AlloyTest.csproj -c Release --no-restore -o TestResults/AlloyClipboardWrite/publish
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --smoke-clipboard-failure
./TestResults/AlloyClipboardWrite/publish/AlloyTest.exe --smoke-clipboard-failure
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --collection-failure
./TestResults/AlloyClipboardWrite/publish/AlloyTest.exe --collection-failure
```

Focused44 passed/0 failed/0 skipped; UI386 passed/0 failed/0 skipped (359 прежних+27 новых). Solution0errors/1339warnings; Release publish exit0. Recovery smoke Debug41/Release40 frames вместе с обычной interaction сценой, три сообщения Write/Write/Read, successful C/X/V retries, UI disposed/registry0. Factory failure оба режима: initial/update→Completion/StopAsync, subscriptions/registry0, once-only draft/tree disposal. NativeGlobalMemory тесты реально выполнились на Windows, не skipped. На final review assertions дополнены проверкой реального lock count через GlobalFlags после Allocate и обоих Read; focused44 и full386 повторно прошли. Production после solution build/publish не менялась, кроме уточняющего комментария о владении.

**Неуспешные попытки и пределы.** Свежий `--window-lifetime` Debug и published Release остановился на первом Get после Run: UiClipboardException(Read) с Win32Exception5/OpenClipboard. Шесть сценариев целиком в этих запусках не прошли; прежние6+6 pass не засчитываются. Guard checks до первого Get выполнены, after-dispose guard в текущем native запуске не достигнут. Предварительный controlled-native-lock harness трижды не смог получить OpenClipboard(NULL/helper HWND), также error5; настоящий OS отказ записи/восстановление этим не проверены. Последний diagnostic GetOpenClipboardWindow0/pid0/tid0 не устанавливает причину отказа. Harness заменён controlled IUiClipboard, OS content не менялся. First focused compile выявил неверный новый test ctor, исправлен;42/42 перед добавлением post-publication cases не является final44. First demo build имел relative Silk.NET namespace error, исправлен global::Silk.NET до успешных checks. Логи: ignored `TestResults/AlloyClipboardWrite/{focused,full,solution-build,publish,debug-recovery,release-recovery,debug-collection-failure,release-collection-failure,debug-lifetime,release-lifetime,debug-failure}.log`, TRX и publish рядом.

**Блокер и следующий шаг.** Пользователь ещё не сообщил о подготовке fixture; native positive `--smoke-clipboard` в этом запуске не выполнялся. После подготовки точной строки повторить read-only lifetime и positive OS smoke в Debug/published Release из контекста, где clipboard доступен. Текущие metadata sequence61/formats18/open window0; snapshot после проверок, не before/after доказательство неизменности sequence. Native write path/успешный SetClipboardData transfer и реальный OS recovery остаются незакрытыми. Full solution tests, полный raster/OpenGL/Vulkan/HUD/font/DPI/OS-events/soak/GPU validation/native heap проход не выполнялись. После clipboard — общая View1/model сцена для5.2–5.3; 5.1c/alpha не закрыты. Git index пуст, коммит не создавался.

Final static verification: `git diff --check` exit0;42 changed/new text files без whitespace/conflict/final-newline ошибок. Fences четырёх документов парны,133 local file links существуют; fragment anchors в этом запуске отдельно не проверялись. Все27 exact clipboard test methods сопоставлены с44 passing cases свежего focused TRX. Две preliminary ошибки нового verification script (включил fake method и использовал Git root-relative paths из src) исправлены, final script exit0. Скрипт/логи в ignored TestResults/AlloyClipboardWrite; внутренние research/plan/status/pairing вне workspace, index0. Последние изменения после executable checks — test assertions с повторными focused/full pass, уточняющий ownership comment и документация.

## Windows clipboard чтение 4.3/5.1c — предыдущий запуск 2026-10-07

**Диагностика.** Rider остановлен в SilkWindowHost constructor после Initialize/CreateInput (тогда line50): fresh stack `SilkWindowHost..ctor → HostedUiWindow.Run:53`, owner thread `TrueMoon Alloy UI`, managed thread5, `NativeWindow.IsInitialized=true`, keyboards count1, native window pointer nonzero. Вычисление методов отключено (`Implicit evaluation is disabled`), поэтому GetText в debugger не выполнен. Два agent breakpoints удалены, собственная session остановлена;21 user breakpoints не менялись. Текущая loaded GLFW DLL сообщает FileVersion/ProductVersion3.4.0, SHA256 `A107A8E8378527897D5E99F3A53D01A7289EF5FDA564952FBEC2769386ECCD58`; managed Silk.NET2.23.0 revision `94605142f7b7bd6e69c9201e8e721d245c69eb7e`. Это версия/hash бинарного файла, не доказательство точной native build revision.

Read-only native probe открыл clipboard через NULL и helper HWND GLFW успешно; clipboard содержит18 форматов, Unicode text недоступен, GLFW вернул error65545/FormatUnavailable. Отдельный managed probe на owner thread с реальным SilkWindowHost поймал FormatUnavailable; после него `GetOpenClipboardWindow` указывает на HWND собственного процесса (pid42812/thread23632), повторный OpenClipboard(NULL) вернул0/error5. Неизменённый baseline `--smoke-clipboard` также завершился FormatUnavailable до записи. Это подтверждает дефект cleanup: [Silk.NET Windows error callback](https://github.com/dotnet/Silk.NET/blob/v2.23.0/src/Windowing/Silk.NET.GLFW/Glfw.cs) бросает исключение; [GLFW3.4 reader](https://github.com/glfw/glfw/blob/3.4/src/win32_window.c) вызывает error callback до CloseClipboard. Результаты объясняют воспроизводимый lock leak; происхождение каждой прежней AccessDenied попытки отдельно не доказано. Clipboard content не выводилось и не менялось. Попытка создать отдельную Windows window station для изолированного native fixture отказала CreateWindowStationW/error5; GPU/clipboard проверки в ней не состоялись.

**Реализация.** [SilkWindowHost.GetText](../../TrueMoon.Alloy.Platform.Silk/SilkWindowHost.cs) выбирает [Win32Clipboard](../../TrueMoon.Alloy.Platform.Silk/Win32Clipboard.cs) для Windows/Win32 HWND; другие платформы сохраняют прежний Silk keyboard путь. [Win32ClipboardApi](../../TrueMoon.Alloy.Platform.Silk/Win32ClipboardApi.cs) использует net10 LibraryImport, borrowed HWND/HGLOBAL, возвращает null без текстового формата, копирует UTF-16 только в пределах GlobalSize и требует terminator. GlobalUnlock/CloseClipboard выполняются в finally; отсутствующий текст и ошибки не оставляют открытый clipboard. Error5 допускает десять retry waits по10ms; остальные/persistent errors наблюдаемы. Ошибки чтения и закрытия сохраняются обе в AggregateException. GetText после host Dispose отвергается. `IUiClipboard` XML уточняет null/no-text/access-errors; [PLAN](PLAN.md) явно фиксирует это решение.

Новый Windows setter **не применён**: automatic approval review отклонил патч с EmptyClipboard из-за риска удаления существующих18 форматов. SetText остаётся прежним через Silk.NET. [SettingsFormSmoke](../../ManualTests/AlloyTest/SettingsFormSmoke.cs) теперь останавливает `--smoke-clipboard` до записи, если прежний текст отсутствует; probe сохраняет только текст, не все форматы, что явно указано в README. [WindowLifetimeProbe](../../ManualTests/AlloyTest/WindowLifetimeProbe.cs) дважды читает OS clipboard после Run и проверяет отказ после Dispose. Никакой запуск copy/cut/paste после исправления не выполнен. Next setter и native test требуют согласованного fixture, замену которого разрешено выполнить.

Focused tests compile-link только реальный Win32Clipboard helper, без добавления Silk packages/ProjectReference в UI tests. [Win32ClipboardTests](../../TrueMoon.Alloy.Tests/Win32ClipboardTests.cs): **10 methods /17 cases**; fake IWin32ClipboardApi доказывает порядок/error/retry протокол, parser читает настоящие выделенные UTF-16 buffers. Точные требования/evidence:

| Требование | Evidence |
| --- | --- |
| Нетекстовый clipboard/null/repeated release | `NontextClipboard_ReturnsNullAndClosesBeforeNextRead` |
| Error5 transient1/10 failures, persistent11, other error без retry | `TransientAccessDenied_RetriesThenReadsAndCloses`, `PersistentAccessDenied_StopsAfterTenWaitsWithoutReadingOrClosing`, `OtherOpenError_IsReportedImmediatelyWithoutRetryOrClose` |
| Format/read failure cleanup, close failure и обе ошибки | `FormatOrReadFailure_ClosesAndPreservesOriginalError`, `CloseFailure_IsObservableAfterSuccessfulRead`, `ReadAndCloseFailures_PreserveBothErrorsInOrder` |
| Empty/Unicode/graphemes/first terminator | `DecodeText_UsesUtf16AndFirstTerminatorWithinBorrowedBuffer` |
| Size0/1/odd/overflow и missing terminator/bounds | `DecodeText_InvalidSizeIsRejectedBeforeReadingPointer`, `DecodeText_MissingTerminatorDoesNotReadBeyondDeclaredBuffer` |

**Фактические проверки**, команды exit0:

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --verbosity quiet --filter FullyQualifiedName~Win32ClipboardTests --logger 'trx;LogFileName=focused.trx' --results-directory TestResults/AlloyClipboard
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --verbosity quiet --logger 'trx;LogFileName=ui-suite.trx' --results-directory TestResults/AlloyClipboard
dotnet build TrueMoon.slnx --no-restore --no-incremental --verbosity quiet
dotnet publish ManualTests/AlloyTest/AlloyTest.csproj -c Release --no-restore --verbosity quiet -o TestResults/AlloyClipboard/publish
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --window-lifetime
./TestResults/AlloyClipboard/publish/AlloyTest.exe --window-lifetime
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --smoke
./TestResults/AlloyClipboard/publish/AlloyTest.exe --smoke
```

Focused17 passed/0 failed/0 skipped; UI359 passed/0 failed/0 skipped (342 прежних+17). Build0errors/1339warnings, publish exit0. Native lifetime6 scenarios Debug и6 published Release, каждый дважды читает clipboard, сохраняет live handle через Run/Closing/failure, отвергает GetText после Dispose. Release metadata capture: sequence unchanged=true, format count18→18, open clipboard window0, native child exit0. Smoke Debug40/published Release42 frames, оба OS clipboard:not requested. Логи/TRX/metadata/publish — ignored `TestResults/AlloyClipboard`. Два preliminary xUnit analyzer warnings исправлены до финальной UI suite; окончательный focused запуск использует этот test binary. После executable checks изменены только комментарии/XML и документы; текст lifetime output возвращён к проверенной версии.

**Оставшиеся проверки и следующий шаг.** Согласовать Windows clipboard запись и безопасный test fixture; завершить native OS Unicode copy/cut/paste, отказ/cleanup/retry setter и Debug/published Release smoke. Реальный native decoder с CF_UNICODETEXT пока не проверен: текущий clipboard нетекстовый; headless parser Unicode проверен отдельно. Нет полного solution test run, новых backend/HUD/font/DPI/OS event injection/soak/GPU validation/native heap checks. Collection failure probes не повторялись в этом запуске. После clipboard — общая View1/model сцена для raster/OpenGL/Vulkan/HUD5.2–5.3 и alpha проход. 5.1c/alpha не закрыты, Git index пуст; коммит не создавался.

## Взаимодействия 5.1c /4.1–4.7 — предыдущий запуск 2026-10-07

**Реализация.** `UiProperties.Padding`/`Spacing` и Get/Set/Clear на [Element](../../TrueMoon.Argentis/Element.cs)/[StackBase](../../TrueMoon.Argentis/StackBase.cs), [Theme](../../TrueMoon.Argentis/Style.cs) с валидируемыми ButtonPadding/EditorPadding/StackSpacing. Local → style → theme priority применяется к новым spacing properties; default theme сохраняет прежние padding button12/8, checkbox36/8, editor8 и stack0. Button/CheckBox/TextBox получают theme padding без constructor local override; disabled CheckBox/ProgressBar используют Theme.Disabled. ChildClipBounds согласует drawing/hit testing, ScrollViewer исключает padding; BringIntoView и UiSession.Focus раскрывают focused content во вложенных viewport с поправкой на actual delta. Исправлены secondary pointer Slider gesture и очистка focus только от primary unhandled click. Повторный Save/Clip нужен только когда child viewport отличается от Bounds, обычные контейнеры сохраняют прежний путь.

[View1](../../ManualTests/AlloyTest/View1.cs)/[SettingsModel](../../ManualTests/AlloyTest/SettingsModel.cs): Dark/Light и Compact toggle, ControlsEnabled для группы формы, постоянные существующие nodes, inherited theme для generated rows/conditional branches. [VolumeMeter](../../ManualTests/AlloyTest/VolumeMeter.cs) использует IDrawingContext и нормализованное ProgressBar.Value; readonly Bind показывает Volume модели. Image48×48 показывает доставленный [PNG32×32](../../ManualTests/AlloyTest/Assets/alloy-mark.png); source borrowed у Image, decoded Skia source принадлежит View1.Own и освобождается после детей. PNG создан программно как checker двух цветов, external assets не использованы. Build/publish Content PreserveNewest; SHA256 исходного/Debug/published PNG совпал: `BB07D1AC6AC509E052F45F2304980DCCBB42B3089DA49BE75C52DA99A3089B3E`. Окно900×960. Sample sources Compile-linked в существующий UI xUnit проект без manual executable/App/Silk reference или новых packages.

Точное evidence **33 methods /78 cases** из [ThemeSpacingTests](../../TrueMoon.Alloy.Tests/ThemeSpacingTests.cs) (7/26), [ControlInteractionTests](../../TrueMoon.Alloy.Tests/ControlInteractionTests.cs) (20/41), [DemoInteractionTests](../../TrueMoon.Alloy.Tests/DemoInteractionTests.cs) (6/11):

| Требование | Evidence |
| --- | --- |
| Theme defaults, invalid padding/spacing, zero/adjacent positive | `DefaultTokens_PreserveExistingControlPaddingAndMeasuredSizes`, `InvalidPaddingOnEveryEdge_RejectsLocalStyleAndThemeTokensBeforeChanges`, `InvalidSpacing_RejectsLocalStyleAndThemeTokenBeforeChanges`, `ZeroAndNearestPositiveTokens_AreAcceptedAndInherited` |
| Local/style/theme/Clear, inherited children и layout/equality | `TypedPaddingAndSpacing_LocalStyleThemeClearFollowEffectivePriority`, `TypedSetAndClear_InvalidateLayoutOnceAndEqualAssignmentsKeepFrameStatic`, `ThemeTokens_UpdateExistingAndNewControlsWithoutOverridingLocalSpacing` |
| Button/CheckBox repeat, press/cancel, secondary/half-open boundaries | `Button_KeyboardRepeatActivatesOnceAndFocusLossCancelsRelease`, `ButtonAndCheckBox_DisablingDuringPressCancelsInputWithoutActivation`, `Button_NonprimaryPointerAndHalfOpenReleaseEdgeDoNotActivate` |
| Slider bounds/secondary/arrow/no-redraw/disabled cancellation | `Slider_DragOutsideBoundsClampsAndNonprimaryButtonsCannotChangeOrEndGesture`, `Slider_ArrowAtEndpointDoesNotChangeValueOrRedrawAndCancelledDragStaysStopped`, `Slider_DisabledAncestorCancelsCapturedDragBeforeFurtherValueChanges` |
| Progress/disabled visuals/caret/selection/passive focus | `ProgressBar_DrawsExactNormalizedExtentAndDoesNotTakeKeyboardFocus`, `ProgressBarAndSlider_InvalidValuesPreservePriorValueAndDoNotInvalidate`, `DisabledControls_DrawDisabledTextCheckedMarkAndProgressFill`, `TextBox_SelectionAndCaretUseDistinctDrawingGeometryAndDisabledInputCannotEdit` |
| Tab/ShiftTab + demo keyboard/button/checkbox/slider/disabled fields | `TabAndShiftTab_WrapEligibleInteractiveNodesAndSkipHiddenDisabledAndPassiveNodes`, `DemoKeyboard_ThemeCheckboxSliderAndTabDriveModelAndDisabledFieldsStayInert` |
| Wheel bubbling/boundaries/extent shrink/clipped hits | `NestedScroll_WheelBubblesFromChildAndThenFromInnerBoundaryToOuter`, `ScrollClipping_PreventsOverflowingButtonFromStealingSiblingHits`, `ScrollExtentShrinkAndHiddenContent_ClampCurrentOffsetAtEveryRemainingRange`, `PaddedScroll_ClipsDrawingAndHitTestingToSameInnerViewport` |
| BringIntoView validation/minimal delta/clamp/oversized/nested focus | `BringIntoView_UsesPaddedViewportMinimalDeltaAndExtentClamp`, `BringIntoView_InvalidRectanglePreservesOffsetAndDoesNotRedraw`, `FocusNestedOffscreenControl_RevealsItFromInnerToOuterUsingActualScrollDelta` |
| Borrowed images, real decode/Skia pixels/packaged image/background | `Image_SourceReplacementAndControlDisposalKeepBorrowedImagesAlive`, `Image_RealSkiaDecodeDrawAndBorrowedReuseProduceExactPixels`, `RealDemoIcon_RendersPackagedCheckerColorsAndThemeBackgroundThroughSkia` |
| Demo palette/density preserves nodes/focus/graphemes/capture/scroll | `AppearanceAndDensity_ChangeExistingTreeWhilePreservingEditorGraphemesFocusCaptureAndScroll` |
| Latin/Cyrillic/emoji/combining editing и injected clipboard | `DemoEditor_LatinCyrillicEmojiAndCombiningClustersRespectClipboardAndEditingBoundaries` |
| Custom painted meter и once-only/static lifecycle | `CustomMeter_UsesSharedVolumeBindingExactFillAndLocalizedDrawing`, `StaticDemo_DoesNotRedrawAndDisposesEveryRetainedNodeAndOwnedResourceOnce` |

**Фактические финальные проверки**, следующие команды exit0:

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --verbosity quiet --filter 'FullyQualifiedName~ThemeSpacingTests|FullyQualifiedName~ControlInteractionTests|FullyQualifiedName~DemoInteractionTests' --logger 'trx;LogFileName=focused.trx' --results-directory TestResults/AlloyInteractions
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --verbosity quiet --logger 'trx;LogFileName=ui-suite.trx' --results-directory TestResults/AlloyInteractions
dotnet build TrueMoon.slnx --no-restore --no-incremental --verbosity quiet
dotnet publish ManualTests/AlloyTest/AlloyTest.csproj -c Release --no-restore --verbosity quiet -o TestResults/AlloyInteractions/publish
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --smoke
./TestResults/AlloyInteractions/publish/AlloyTest.exe --smoke
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --collection-failure
./TestResults/AlloyInteractions/publish/AlloyTest.exe --collection-failure
```

Focused **78 passed/0 failed/0 skipped**, UI **342 passed/0 failed/0 skipped** (264 прежних +78). Финальный solution build **0 errors/1339 warnings**, publish exit0. Native smoke по **3 последовательных запуска**: Debug40/40/41 frames, published Release40/40/40; editing/form/list/regions, theme/density retention, disabled group/re-enable, Tab/ShiftTab/Space/Enter, Latin/Cyrillic/ZWJ/Delete/Backspace/Home/End, Slider capture/drag outside/clamp/release, wheel Offset36→72, auto-scroll focused last editor, FocusLost, image/meter, static redraw и generated branch/row Dispose/detach прошли. `--smoke` явно сообщает `OS clipboard: not requested`. В обоих final `--collection-failure` initial/update ошибки дошли до Completion/StopAsync, registry/subscriptions0 и drafts+tree disposed once. UI suite запущен после финального production clip/input fix; subsequent executable edits — только nullable annotations и разделение smoke flags, после чего solution build/publish пересобраны и focused78 повторён. Логи/TRX/publish — ignored `TestResults/AlloyInteractions`.

**Отдельная неуспешная проверка OS clipboard:**

```powershell
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --smoke-clipboard
./TestResults/AlloyInteractions/publish/AlloyTest.exe --smoke-clipboard
```

Обе final команды имеют child exit **-532462766**: `Silk.NET.GLFW.GlfwException`, PlatformError/Win32 Failed to open clipboard/AccessDenied в SilkWindowHost.GetText при сохранении clipboard перед сценарием. Copy/cut/paste и restore до этого не дошли; исходное содержимое clipboard не менялось и не выводилось. Подготовленные probes используют UiSession.Get/SetClipboardText и должны сохранить/восстановить прежний текст при успешном доступе. Clipboard через injected IUiClipboard полностью прошёл в actual-demo headless tests; отдельный host PowerShell Get-Clipboard завершился успешно без вывода содержимого. Это **не доказательство общей недоступности Windows clipboard или ошибки модели/TextBox**; причина различия GLFW/host пока не установлена. Аналогичные две preliminary native попытки также не прошли. Попытка IDE terminal launch не вернула result и была отменена, успешным запуском не считается. [GLFW Win32 source](https://github.com/glfw/glfw/blob/3.4/src/win32_window.c) документирует ограниченные OpenClipboard retries, но точная revision текущей native GLFW и причина отказа ещё не сопоставлены; это контекст диагностики, не подтверждённый root cause. Ошибка передана неизменённым Hosting callback boundary, unwind через managed application completion; native crash не заявляется.

**Review/промежуточные результаты.** Assertion-quality и статический pseudo-mutation review завершены; coverage/empirical mutation не запускались. Once narrow Roslyn pairing Argentis44 sources/0 tests: sibling UI tests вне scan root, это не coverage. Internal research/plan/status и pairing.json находятся в temp non-stageable каталоге. First focused run49/51: один ошибочный transparent pixel expectation исправлен в новом test, другой выявил secondary PointerDown focus cancellation в runtime — production исправлена. Первый full run341/342: лишний child Save/Clip нарушил существующий restoration test; production путь исправлен без изменения прежних264 tests. Предварительный solution build имел1341 warnings из-за двух новых nullable предупреждений smoke; они исправлены, финальный build1339. Эти attempts не подменяют final passing results.

Финальная статическая проверка: `git diff --check` exit0; 32 changed/new text files без whitespace/conflict/final-newline ошибок, отдельный PNG проверен decode/pixels/hash delivery (всего33 files). Fences четырёх документов парны, все113 локальные ссылки/anchors существуют, все33 текущих exact test method names сопоставлены со свежим focused TRX. Логи/скрипт в ignored `TestResults/AlloyInteractions`, internal research/plan/status/pairing отсутствуют среди changes, Git index пуст. Последние изменения после исполняемых checks — документация.

**Границы и следующий шаг.** 4.5/4.6 spacing contract и функциональная standalone сцена реализованы/проверены, но **4.3/5.1c не закрыты полностью из-за GLFW OS clipboard**. Следующий конкретный шаг — установить причину AccessDenied на owner thread с точной native GLFW revision/window/thread/desktop evidence, затем повторить `--smoke-clipboard` Debug/published Release. После этого адаптировать ту же View1/model сцену к shared raster/OpenGL/Vulkan comparison/HUD (5.2–5.3). Не запускались полный solution test suite, new Vulkan/HUD/backend comparison, OS event injection/font golden/DPI migration, long soak/native debugger/profiling/package consumer; alpha не завершена. Packages/native ABI/Hosting code/Platform code/Git index не менялись.

## Условные области 3.5d и завершение 5.1b — предыдущий запуск 2026-10-07

**Реализация.** [LoadStatus](../../ManualTests/AlloyTest/LoadStatus.cs) — immutable phase/message snapshot; [LoadStatusRegion](../../ManualTests/AlloyTest/LoadStatusRegion.cs) — внутренний виджет примера на обычных Bind/ContentControl.SetContent. Фабрика готовит ветвь до замены. Ошибка подготовки сохраняет старый child и displayed status; desired property/model уже могут содержать новое состояние, явный RefreshContent позволяет повторить попытку. Виджет очищает принятые unowned drafts и отсоединённую старую ветвь, сохраняет чужое владение, не освобождает accepted child при postcommit notification error. Одиночная ошибка сохраняет тип; notification/cleanup errors агрегируются. Очередь owner thread, attachment guards, coalescing и equality исключают stale updates и лишние rebuild/redraw. Общие ownership/API библиотеки не менялись.

[SettingsModel](../../ManualTests/AlloyTest/SettingsModel.cs)/[View1](../../ManualTests/AlloyTest/View1.cs) показывают loading/error/ready, ProgressBar и действие «Повторить»; кнопки управляют состоянием модели. Реальный сетевой запрос и async cancellation не реализуются в этом подэтапе. Форма и список остаются соседними ветвями. [SettingsFormSmoke](../../ManualTests/AlloyTest/SettingsFormSmoke.cs) расширен transitions/retry, сохранением строк/focus/grapheme selection/scroll и Dispose всех замеченных ветвей. Точные исходники виджета Compile-linked в существующий xUnit проект; новых package/ProjectReference на manual executable/App/Silk нет.

Точное headless evidence из [ConditionalRegionTests](../../TrueMoon.Alloy.Tests/ConditionalRegionTests.cs), **25 methods /35 cases**:

| Требование | Evidence |
| --- | --- |
| Initial Loading/Error до layout | `InitialNondefaultBinding_ReplacesDefaultBranchDuringUpdateBeforeLayout` (2 cases) |
| Все phases/messages, coalescing и retry input | `BoundTransitions_LoadingErrorReadyAndMessageReplaceOnlyTheirBranch`, `Coalescing_SkipsIntermediateBranchesAndFactoryReadsLatestSnapshot`, `EmptyMessage_IsValidForEveryDefinedPhase` (3), `ErrorBranchRetryButton_UpdatesModelAndReplacesBranchOnNextUpdate` |
| Factory failure, retained neighbor state и recovery | `FactoryFailure_KeepsOldBranchAndNeighborInputScrollAndSubscriptionsUntilRefreshRecovers`, `DetachedFactoryFailure_LeavesDesiredStatusAndOldBranchAvailableForExplicitRefresh` |
| Invalid results/foreign ownership/ancestors и cleanup drafts | `InvalidFactoryResult_PreservesBranchAndNeverDisposesForeignOrAncestor` (6), `InvalidCandidateSubtree_IsDisposedWithItsResourcesAfterValidationFailure`, `FactoryThrowBeforeReturn_CleansFactoryOwnedAllocationAndPreservesAcceptedBranch` |
| Once-only old subtree/input/subscriptions cleanup | `AcceptedReplacement_DisposesOldSubtreeSubscriptionsAndInputExactlyOnce` |
| Postcommit observers/accepted child и error aggregation | `OldDetachObserverFailure_CommitsNewBranchNotifiesOthersAndCleansOldWithoutFactoryRetry`, `CandidateAttachmentObserverFailure_KeepsAcceptedLiveChildAndDisplayedSnapshot`, `NotificationAndOldCleanupErrors_AggregateAfterCommitAndFinishAllResources`, `RejectedDraftCleanupFailure_ReportsValidationAndCleanupWhilePreservingOldBranch` |
| Equality/static redraw | `EqualSnapshotOrExplicitRefresh_PreservesBranchAndDoesNotRedraw` |
| Detach/reattach/transfer/background stale work | `DetachReattach_DropsPendingRefreshAndRereadsChangesMadeWhileDetached`, `ReattachWithEqualStatus_PreservesOldChildAndRestoresOneModelSubscription`, `BackgroundNotification_OnlyReadsAndCreatesBranchDuringOwnerUpdate`, `TransferToWorker_DropsOldQueuedAndInflightWorkAndRefreshesOnNewOwner` |
| Detached immediate application/same child reuse | `DetachedStatusChange_AppliesImmediatelyAndSamePreviousNodeCanBeReused` |
| Validation/access/dispatcher retry | `InvalidStatus_RejectsBeforePropertyOrBranchChanges` (3), `InvalidBoundStatus_ReportsDuringUpdateAndNextNotificationRecovers`, `NullFactoryDisposedRegionAndForeignThreadAccess_AreRejected`, `DispatchFailure_RefreshCanRetryWithoutReplacingUnchangedBranch` |

**Фактические проверки**, все exit0:

```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --verbosity quiet --filter 'FullyQualifiedName~ConditionalRegionTests' --logger 'trx;LogFileName=conditional-regions.trx' --results-directory TestResults/AlloyRegions
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --verbosity quiet --logger 'trx;LogFileName=ui-suite.trx' --results-directory TestResults/AlloyRegions
dotnet publish ManualTests/AlloyTest/AlloyTest.csproj -c Release --no-restore --verbosity quiet -o TestResults/AlloyRegions/publish
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --smoke
./TestResults/AlloyRegions/publish/AlloyTest.exe --smoke
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --collection-failure
./TestResults/AlloyRegions/publish/AlloyTest.exe --collection-failure
```

Focused **35 passed/0 failed/0 skipped**, UI **264 passed/0 failed/0 skipped** (229 прежних +35), build **0 errors/1339 warnings**, publish exit0. Финальные smoke выполнены по **3 раза последовательно**: Debug30/31/32 frames, published Release31/31/30. Loading/Error/Ready сменяются при сохранении списка, focus/grapheme selection и Offset36; статические кнопки и retry проходят через UiSession input. Focus удалённой retry ветви снимается; все замеченные branch/row nodes Dispose/detach. Оба текущих collection-failure запуска повторили initial/update ошибки через неизменённый HostedUiWindow: точная ошибка Completion/StopAsync, registry/subscriptions0, drafts/committed nodes disposed once. Ошибки фабрики **условной области** проверены headless; native probe относится к коллекционной фабрике. Логи/TRX/publish: ignored `TestResults/AlloyRegions`. Предварительные demo build/smoke32 и промежуточные focused30 не подменяют финальный набор. Финальный focused запуск пересобрал тестовый проект после добавления последних тестов; production не менялась после non-incremental solution build/publish.

**Review и границы.** Assertion-quality и статический pseudo-mutation review выполнены; coverage/mutation runner не запускались. Narrow Roslyn pairing analyzer для manual demo exit0; отсутствие tests внутри demo directory не является метрикой coverage: реальные тесты находятся в соседнем UI проекте и Compile-link целевые sources. Internal research/plan/status — во временном non-stageable каталоге. Полный solution test suite, новые Vulkan/backend/DPI/font/clipboard comparisons, debugger/long soak, real async loading/cancellation, consumer/package/profiling не запускались. Произвольные некорректные custom Children с чужими owned descendants не являются поддерживаемыми factory drafts. Блокеров3.5d не выявлено; 3.5d/5.1b завершены в заявленных границах, общий этап3/alpha остаётся частичным.

Финальная статическая проверка: `git diff --check` exit0; все18 changed/new files прошли whitespace/conflict-marker/final-newline checks, fences четырёх документов парны, все101 локальная ссылка/anchor существуют, все25 точных test evidence имён текущего раздела присутствуют в свежем focused TRX. Первое выполнение check script остановилось до проверок из-за вложенного массива путей PowerShell; исправленное перечисление прошло. Лог/скрипт — ignored `TestResults/AlloyRegions/static-check.log`/`verify.ps1`, internal scratch не попал в рабочие изменения, Git index пуст. Последние изменения после исполняемых проверок — документация.

**Следующий конкретный шаг:** 5.1c /4.1–4.7 — сверить заявленные controls и дополнить ту же сцену прокруткой/темами/изображениями и полноценными взаимодействиями; затем shared backend/HUD и итоговые alpha проверки.

## Коллекции 3.5c и динамический список AlloyTest — предыдущий запуск 2026-10-07

**Реализация.** [CollectionBinding](../../TrueMoon.Argentis/CollectionBinding.cs) и [ElementExtensions](../../TrueMoon.Argentis/ElementExtensions.cs) предоставляют BindItems к mutable/read-only ObservableCollection и RefreshItems без новых dependencies. [ElementCollection](../../TrueMoon.Argentis/ElementList.cs) резервирует Items и выполняет внутренний batch commit до notifications; прежние публичные mutations unbound коллекций сохраняют ownership policy. Сопоставление по ReferenceEquals, null/duplicate rejection, snapshot coalescing, deferred initial sync, attachment guards, staged factory cleanup, committed mapping и best-effort cleanup реализованы. Передача single/AggregateException и recovery проверены отдельно от наличия кода. После review Request также сбрасывает pending marker при ошибке custom dispatcher, позволяя следующему Refresh снова поставить работу в очередь.

[SettingsModel](../../ManualTests/AlloyTest/SettingsModel.cs)/[SettingsRow](../../ManualTests/AlloyTest/SettingsRow.cs)/[View1](../../ManualTests/AlloyTest/View1.cs) сохраняют прежнюю форму и добавляют редактируемый список: Add/Remove/Replace/Move и один Reset с обратным порядком прежних model objects. Row factory смешивает constructors/factories и использует BindTwoWay. ScrollViewer ограничивает высоту списка, окно700×720. [SettingsFormSmoke](../../ManualTests/AlloyTest/SettingsFormSmoke.cs) проверяет form/list input/order/retained state/disposal; [CollectionFailureProbe](../../ManualTests/AlloyTest/CollectionFailureProbe.cs) проверяет ошибки factory в initial/update реального OpenGL/Silk window через неизменённый HostedUiWindow callback boundary. 3.5d/conditional regions не включены в эту реализацию.

Точное headless evidence из [CollectionBindingTests](../../TrueMoon.Alloy.Tests/CollectionBindingTests.cs), 31 methods /41 cases:

| Требование | Evidence |
| --- | --- |
| Initial empty/single/multiple и Update до layout | `InitialAttach_DefersFactoryUntilUpdateAndLayoutsEveryRow` (0/1/3) |
| Read-only source и Bind уже attached контейнера | `ReadOnlySource_AndRegistrationOnAttachedContainer_SynchronizeNextUpdate` |
| Add/Remove/Replace/Move/Reset и range events | `AddRemoveReplaceMoveReset_PreserveOnlySurvivingReferences`, `RangeNotifications_ReconcileFinalOrderForAllActions` |
| Singleton → empty, Reset с remove/retain/add | `RemoveLastItem_LeavesEmptyTreeAndReleasesFocusCaptureAndSubscription`, `ResetMixedItems_RemovesMissingRetainsSurvivorAndCreatesOnlyNewRows` |
| Reference identity/Equals trap/новый объект с прежним Id | `Identity_UsesReferencesEvenWhenEqualsAndIdsMatch` |
| Null/duplicate rejection до factory и coalescing | `DuplicateOrNullItems_RejectSnapshotBeforeFactoryAndPreserveTree` (2 cases), `CoalescedEvents_SkipTransientItemsAndRetainRemovedThenReaddedRow` |
| Retained focus/selection/capture/scroll/subscriptions | `MoveAndReset_KeepFocusSelectionCaptureScrollAndPropertySubscriptions` |
| Caller-owned models, removed subtree и once-only Dispose | `Removal_DisposesGeneratedSubtreeOnceAndDisconnectsModel` |
| Invalid factory results, ancestors, foreign ownership, throw и drafts | `FactoryFailure_PreservesOldTreeCleansDraftsAndNeverDisposesForeignNodes` (8 cases), `FactoryThrowsBeforeReturning_FactoryOwnsItsUnreturnedResources`, `FailedPreparation_CleanupErrorsStillDisposeAllDrafts` |
| Snapshot/source observer/subscribe failure и recovery | `SnapshotFailure_PreservesTreeAndRefreshRecoversWithoutNewFactory`, `EarlierSourceSubscriberThrows_RefreshRecoversCommittedSourceChange`, `SubscribeFailure_ReleasesReservationAndAllowsRetry`, `InitialSubscribeFailure_SessionAttachRollsBackAndReattachesOnceOnRetry` |
| Coherent postcommit observers/mapping/attachment failures | `CommitObservers_SeeFinalOrderParentsAndAttachmentsDespiteEarlierFailure`, `NewRowAttachmentFailure_IsPostcommitAndRefreshDoesNotRecreateOrDisposeIt` |
| Cleanup errors после commit не пропускают siblings/не повторяют factory | `RemovalCleanupErrors_CommitMappingAndReleaseAllRowsWithoutRetryingFactory` |
| Factory source mutation и observer queued changes | `FactoryChangesSource_AbortPreparedSnapshotAndNextUpdateUsesLatestItems`, `SourceMutationDuringPreparation_PreservesExistingFocusAndOwnershipThenRecovers`, `ObserverChangesSource_QueuesAnotherCoherentReconciliation` |
| Detach/reattach/transfer и stale queued/in-flight/gap events | `DetachReattach_RetainsRowsReconnectsAndDropsOldQueuedAndInflightEvents`, `TransferToWorker_EnumeratesAndCreatesOnlyOnNewOwnerAndIgnoresOldWork`, `DetachBeforeUnsubscribe_BackgroundInflightEventCannotEnumerateOrQueueStaleWork` |
| Background events/read/factory только на UI owner | `BackgroundEvents_QueueEnumerationAndFactoryOnUiOwner` |
| Items exclusive/registration validation/custom dispatcher recovery | `BoundItems_RejectEveryManualMutationAndDuplicateRegistrationWithoutChangingRows`, `RegistrationValidation_RejectsNullNonemptyDisposedUnboundRefreshAndForeignThread`, `DispatchFailure_RefreshCanQueueAgainWithoutLosingBinding` |

**Фактические финальные команды и результаты**, все exit0:

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --verbosity quiet --filter 'FullyQualifiedName~CollectionBindingTests' --logger 'trx;LogFileName=collection-bindings.trx' --results-directory TestResults/AlloyCollections
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --verbosity quiet --logger 'trx;LogFileName=ui-suite.trx' --results-directory TestResults/AlloyCollections
dotnet build TrueMoon.slnx --no-restore --no-incremental --verbosity quiet
dotnet publish ManualTests/AlloyTest/AlloyTest.csproj -c Release --no-restore --verbosity quiet -o TestResults/AlloyCollections/publish
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --smoke
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --collection-failure
./TestResults/AlloyCollections/publish/AlloyTest.exe --smoke
./TestResults/AlloyCollections/publish/AlloyTest.exe --collection-failure
```

Focused **41 passed/0 failed/0 skipped**, UI suite **229 passed/0 failed/0 skipped** (прежние188 +41), build **0 errors/1339 warnings**, publish exit0. Каждый form/list smoke выполнен **3 раза последовательно**: Debug31/30/30 frames, published Release31/31/31. Проверены form input/reset/summary, row input/model/order, retained row focus/grapheme selection при Add/Move/Reset/замене соседа, Remove cleanup/cancel focus, actual Add/Remove buttons, static no-redraw и disposal всех замеченных generated nodes. Каждый `--collection-failure` выполнил **2 сценария**: ошибка в initial Update до первого presentation и ошибка после одного кадра. Completion/StopAsync сохранили точную factory error, registry/subscriptions0, drafts/committed tree disposed once; это bounded managed failure probe через native callback boundary, не графическая device-loss проверка. Логи/TRX/publish — ignored `TestResults/AlloyCollections`; первый предварительный smoke и demo build также прошли, их результаты не подменяют финальный набор.

**Review и ограничения.** Assertion-quality и статический pseudo-mutation review: нет assertion-free/skipped/self-referential тестов, factory error assertions используют точные типы. Mutation runner/coverage metrics не запускались. Internal research/plan/status находятся во временном non-stageable каталоге, не среди commit changes. Статический Roslyn pairing analyzer выполнен один раз, exit0; его repository-wide output включает ignored vendor/TestResults и не является coverage или основанием тестировать vendor. Для целевого binding evidence приведён выше. Блокеров3.5c не выявлено. Не запускались полный solution test suite, новые Vulkan/HUD/backend comparisons, DPI/OS clipboard/font golden, long soak/native debugger heap runs, device-loss, NuGet/отдельный consumer и profiling. Нет нового rollback обещания для notification failures; arbitrary throwing event accessors/бесконечно самоменяющиеся callbacks остаются вне полного stress покрытия. Packages/native DLL/backend/Hosting/Platform и Git index не менялись.

Финальная статическая проверка: полный `git diff --check` — exit0; все14 changed/new files прошли whitespace/conflict-marker/final-newline проверки; fences четырёх Markdown документов парны, все91 локальная ссылка/heading anchor существуют; все31 точное имя test evidence текущего раздела сопоставлено со свежим focused TRX. Первое перечисление changed paths в check script ошибочно продублировало `src` и остановилось до проверок; повторный check использует `git diff --name-only --relative` и прошёл. Это ошибка проверки путей, не build/test failure. Промежуточные internal state файлы отсутствуют в git status. Последние изменения после исполняемых проверок — только документация.

**Точка продолжения того запуска:** 3.5d — загрузка/ошибка/содержимое через SetContent. Этот шаг выполнен и проверен в новом разделе выше; теперь очередь5.1c.

## Предложение контракта коллекций 3.5c — предыдущая документационная работа 2026-10-07, после 93a5cae

История подготовленного до реализации предложения. В следующем сообщении пользователь разрешил выполнение; контракт перенесён в PLAN, реализован и проверен в текущем подэтапе выше. Следующие записи о неначатых проверках относятся только к предыдущей документационной работе.

- **API и границы.** `container.BindItems(source, factory)` в Argentis возвращает конкретный тип `ElementList`; начальные перегрузки принимают `ObservableCollection<TItem>` и `ReadOnlyObservableCollection<TItem>`, `TItem : class`, `Func<TItem, Element>`. Источник фиксирован до Dispose контейнера; замена свойства модели с коллекцией автоматически не наблюдается. Один binding на первоначально пустой `Items`; структура этой коллекции после регистрации управляется только binding, включая detached состояние. Прямые Add/Remove/Clear/Move/index replacement отклоняются до mutation. Статические соседние controls размещаются во внешнем контейнере. `RefreshItems()` запрашивает новую синхронизацию через dispatcher; detached registration прочитает источник при следующем attach. Публичный Unbind пока не предлагается.
- **Соответствие и Reset.** Один уникальный ненулевой объект модели соответствует одному generated subtree; сравнение только ReferenceEquals. Повтор одной ссылки и null в source snapshot — ошибки до изменения дерева; одинаковые по Equals разные объекты допустимы. Изменения свойств item обрабатываются существующими Bind/BindTwoWay на строке. Reset/reattach/Refresh сохраняют nodes для оставшихся тех же объектов, переставляют их без detach, создают только новые и удаляют отсутствующие. Новый объект с прежним Id — новая строка; keys, retargeting и повторяющиеся occurrences отложены. Add/Remove/Replace/Move/Reset запрашивают сверку с актуальным составом, без replay всех промежуточных состояний: Add+Remove до Update может не вызвать фабрику; Remove+Add той же ссылки до Update может сохранить строку. Replace той же ссылкой — no-op, Replace новым объектом создаёт строку. Range events используют ту же сверку.
- **Ownership.** Source и items остаются caller-owned. Фабрика возвращает новый live unowned/unattached subtree; уже принадлежащие дереву, disposed, повторно возвращённые nodes и циклы отклоняются. Принятые результаты до commit принадлежат binding, после commit — контейнеру. При удалении/замене binding выступает caller существующего Remove и обязан Dispose отсоединённой строки; передачи generated nodes внешнему caller нет. Detach всего контейнера сохраняет строки и отключает connection; Dispose контейнера освобождает текущих детей обычным путём, binding не освобождает их повторно. При rollback подготовки binding освобождает все принятые новые subtrees best-effort; чужой owned node не освобождается. Если фабрика выделила ресурсы и бросила до возврата, очистить их обязана фабрика.
- **Подготовка и commit.** Сначала получить/проверить snapshot и полностью подготовить новые строки, затем одним внутренним TreeChange согласованно изменить child order, parent/attachment связи и item/node mapping до внешних notifications. Сохранённые строки не отсоединяются, их focus/selection/subscriptions сохраняются; scroll offset сохраняется в пределах обычного layout clamp. Ошибка snapshot/фабрики/предварительной проверки сохраняет прежнее дерево и освобождает только drafts; исходная модель не откатывается. Source mutation из factory считается ошибкой подготовки, без вложенной синхронизации; source version проверяется до commit. Изменения source из observers откладываются до следующей синхронизации. Ошибки notifications/attachment callbacks/cleanup после commit не откатывают дерево: продолжить остальных observers и cleanup всех удалённых строк, затем передать одну исходную ошибку либо AggregateException. Mapping уже соответствует committed дереву, повторное применение mutation не допускается. Следующий корректный event или RefreshItems допускает recovery.
- **Attachment и потоки.** Registration живёт через Own, connection через OwnAttachment с captured dispatcher/active guard. До attach нет source subscription/чтения/вызовов фабрики. Attach/Bind уже attached контейнера подписывает source и ставит initial sync в очередь; заполнение выполняется в следующем Update до layout, поскольку структурные mutations внутри AttachmentChanged сейчас запрещены. Detach/Dispose отключают subscription; старые queued/in-flight события после reattach/transfer ничего не читают и не меняют. Фоновые notifications только запрашивают sync; перечисление source, factory и tree mutations выполняются на owner thread. ObservableCollection не становится потокобезопасной: владелец source обязан сериализовать mutation/notifications и исключить конкурентное перечисление (например, завершить worker batch до owner Update); для AlloyTest mutations идут на UI-потоке, фоновые результаты доставляются через dispatcher. Это тот же принцип caller responsibility за безопасное чтение source, что у property bindings.
- **Ошибки и Hosting.** Factory failure при initial Update оставляет живую session с прежним/пустым списком для direct caller; регистрация сохраняется для recovery. Ошибка getter/subscription при самом attach следует текущему UiSession attachment rollback. В HostedUiWindow исключение из Update остаётся в существующей callback boundary и приводит к закрытию и ошибке Completion/StopAsync; inline error row автоматически не создаётся. Демонстрация состояний загрузка/ошибка/содержимое остаётся отдельным 3.5d через SetContent.

Предстоящие проверки на net10.0/xUnit: empty/single/multiple и все пять actions/range events; ReferenceEquals/duplicates/null/новый объект с прежним Id; сохранение экземпляров/focus/selection/subscriptions при Move/Reset и нормальный scroll clamp; snapshot/factory validation и partial draft cleanup; throw-before-return responsibility; notification/cleanup errors после batch commit; recovery/Refresh; detach/reattach/transfer и stale queued/in-flight/background events; отсутствие повторных subscriptions/double Dispose; запрет ручных Items mutations и factory reentrancy. Затем расширить прежнюю SettingsModel/View1 динамическим списком с добавлением, удалением, перемещением и редактированием, сохранив существующую форму; оконный smoke выполнять последовательно в Debug/published Release.

**Фактически выполнено:** прочитаны AGENTS/PLAN/STATUS, Element/ElementCollection/TreeChange/PropertyBinding, UiSession/HostedUiWindow и View1/SettingsModel; подтверждены `main`, HEAD 93a5cae и первоначально чистое рабочее дерево. Найдено ограничение tree mutations в AttachmentChanged, учтено отложенным initial sync. Изменён только STATUS; исполняемый код, PLAN, packages/native assets и Git index не менялись. Сборка/tests/GPU/publish не запускались; прежние188/188 — исторический результат. Блокеров чтения после обхода ошибки sandbox launcher не выявлено; реализация и проверки 3.5c ещё предстоят. Следующий конкретный шаг — согласовать предложенные identity/duplicates/Reset/ownership/failure/lifetime правила, перенести принятый контракт в PLAN и реализовать binding до расширения AlloyTest.

Проверки документационной правки: `git diff --check -- docs/alloy/STATUS.md` — exit0; Markdown fences парны, conflict markers отсутствуют; `git status --short` показывает только STATUS. Git сообщает обычную нормализацию LF → CRLF. Проверки поведения коллекций не выполнялись.

## Исправление window lifetime — 2026-10-07, после фабрик/формы

**Причина и реализация.** Явный цикл SilkWindowHost.Run сохраняет окно до Dispose вместо вызова parameterless Silk Run с автоматическим Reset. Dispose сначала освобождает input, затем NativeWindow в finally; Run после Dispose отклоняется. Renderer/session успевают освободиться до окна; OpenGL Closing handler сохраняется. [SilkWindowHost](../../TrueMoon.Alloy.Platform.Silk/SilkWindowHost.cs), [WindowLifetimeProbe](../../ManualTests/AlloyTest/WindowLifetimeProbe.cs), [запуск](../../ManualTests/AlloyTest/README.md).

Runtime evidence до правки: временный Win32 debugger (DEBUG_ONLY_THIS_PROCESS, DbgHelp/MiniDumpWriteDump, timeout45s) дважды воспроизвёл second-chance0xE0434352 / System.OutOfMemoryException. Анализ dotnet-dump: Marshal.GetDelegateForFunctionPointer → Glfw.SetKeyCallback → GlfwEvents.Dispose → GlfwInputContext.Dispose → SilkWindowHost.Dispose → HostedUiWindow.Run. Памяти системы было достаточно. В Silk2.23.0 [Run extension](https://github.com/dotnet/Silk.NET/blob/v2.23.0/src/Windowing/Silk.NET.Windowing.Common/WindowExtensions.cs) вызывает Reset, а [GlfwEvents.Dispose](https://github.com/dotnet/Silk.NET/blob/v2.23.0/src/Input/Silk.NET.Input.Glfw/GlfwEvents.cs) снимает callbacks с сохранённого handle. Это доказывает неверный порядок использования GLFW window. До изменения новый --window-lifetime также завершился ошибкой «Run destroyed the window before its input context could be released».

Исходный0xC0000374 не перехвачен, поэтому совпадение причины с ним не заявляется как доказанный native stack. До правки5 запусков debugger с process-only _NO_DEBUG_HEAP=1 и10 обычных прямых запусков проходили; это были контрольные запуски, а не исправление. Rider attach дал managed UI stack и live handle во время работы, но значение handle в Dispose не получено. Наши два breakpoint удалены,21 исходный breakpoint сохранён; собственный процесс закрыт. Run configuration, системные настройки и Git index не менялись. dotnet-dump установлен только во временный tool-path; helper находится во временном каталоге, не является поставляемым диагностическим инструментом.

**Фактические проверки после правки:**

- `dotnet build TrueMoon.slnx --no-restore --no-incremental --verbosity quiet`: exit0, **0 errors/1339 warnings**.
- `dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --verbosity quiet --logger 'trx;LogFileName=ui-suite.trx' --results-directory TestResults/AlloyNativeCrash`: **Passed:188, Failed:0, Skipped:0**, exit0.
- Publish AlloyTest и AlloyVulkanTest в Release с --no-restore: оба exit0, каталоги `TestResults/AlloyNativeCrash/publish-form` и `publish-gpu`.
- Win32 debugger с обычной отладочной кучей (_NO_DEBUG_HEAP не задан): Debug **10/10**, published Release **10/10** последовательных `--smoke`, каждый30–31 frames, exit0/captured0. По одному `--window-lifetime` в обоих вариантах: каждый **6 OpenGL/Vulkan сценариев**, exit0/captured0. Проверяются закрытие после render, закрытие до Run, перехваченная render failure, Closing/live handle, доступность input после Run, уничтожение окна и повторный Dispose.
- Исходная форма запуска `dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj --no-build --no-restore -- --smoke`: **3/3**,30/31/31 frames. Прямой published Release:32 frames, exit0.
- AlloyVulkanTest Debug и published Release: `--opengl-window`, `--opengl-failure`, `--window --validation`, все **6 запусков exit0**. OpenGL resize/minimize/restore30/31 frames и GL NoError; draw failure передана в Completion/StopAsync, registry0/tree disposed. Vulkan30 frames/2 swapchains и validation **0 errors/0 warnings**, включая disposal. VK_LAYER_PATH задавался только процессу на прежний manifest и восстановлен.

Логи/TRX/дампы: `TestResults/AlloyNativeCrash` (ignored). Финальный Release набор — `fixed-release-serial-heap-*.log`; ранний параллельный Release запуск failed с managed assertion о потерянном focus, пока работало другое оконное probe. Assertions не ослаблялись; оконные проверки теперь запускаются последовательно. Первоначальный probe с неперехваченным render throw также failed через GLFW boundary; финальный сценарий перехватывает callback error как Hosting. Ни один из этих ранних запусков не засчитан успешным.

После финального переноса timeout/Closing assertions за native boundary probe пересобран и опубликован (оба exit0); Debug/published Release --window-lifetime с отладочной кучей повторно прошли все6 сценариев, exit0/captured0 (`final-*-lifetime-heap.log`). Последние production изменения после основного набора — XML remarks/комментарий и final newline, без изменения исполняемого кода. Проверка8 изменённых файлов: whitespace/conflict markers/final newline, Markdown fences и77 локальных ссылок/anchors прошли; scoped tracked git diff --check exit0.20 финальных native smoke логов отдельно сверены на exit0/captured0. Rider breakpoint list полностью совпал с baseline:21 total/18 enabled, agent breakpoints отсутствуют.

**Ограничения и продолжение.** Найденный lifetime defect исправлен и проверен воспроизводимым до/после probe и bounded native runs. Связь исходного heap corruption остаётся гипотезой; новые нативные сбои требуют нового дампа. Это не long soak или итоговый alpha. Не повторялись полный solution test suite, HUD/backend comparison, fonts/OS clipboard/golden, DPI matrix, device-loss, NuGet consumer и retirement soak. Native DLL/packages/renderer/runtime/Argentis bindings не менялись; в Hosting изменён только устаревший комментарий. Следующий конкретный шаг — контракт коллекций3.5c (correspondence/duplicates/Reset/ownership/failures), затем реализация и список/условные области5.1b.

## Фабрики 4.8 и первая форма 5.1a — 2026-10-07, предыдущий запуск

Реализация:

- Button.Simple(string text = "") и Text.Plain(string value = ""); собственные Create у TextBox, CheckBox(string text = ""), ProgressBar, Slider, Image, Panel, ContentControl, Border, ScrollViewer, VStack, HStack, Line и Rectagle. Методы в Argentis вызывают публичный конструктор и возвращают новый конкретный тип. Slider/Border/ScrollViewer явно скрывают базовый Create. Constructors и Shapes helpers сохраняются; Line остаётся прежним пустым примитивом. Создание не требует App/DI/backend и не подключает элемент к сессии.
- [View1](../../ManualTests/AlloyTest/View1.cs) получает [SettingsModel](../../ManualTests/AlloyTest/SettingsModel.cs) через DI. Name/Enabled/Volume редактируются TextBox/CheckBox/Slider BindTwoWay, Summary наблюдается через BindText. LoadExample/Reset меняют модель без перестройки дерева. Форма смешивает factories/constructors; старый View2 в DI/дереве не участвует, файл сохранён.
- [SettingsFormSmoke](../../ManualTests/AlloyTest/SettingsFormSmoke.cs) работает в FramePresented на UI-потоке реального OpenGL/Silk окна: initial model → UI, input → model, queued model → UI/Summary, сохранение экземпляров/focus/выделения графемы при изменении соседнего свойства, preset/reset, отсутствие постоянной invalidation и Dispose/detach дерева после Completion/StopAsync. Ввод синтетический через UiSession.HandleInput; OS input/clipboard этим не проверяются.

Проверки:

| Требование | Точное evidence |
| --- | --- |
| Фабрика объявлена на типе и возвращает конкретный тип | `Factories_DeclareConcreteReturnTypes` — 15 cases |
| Constructor defaults/layout, независимые экземпляры, attach/theme/Dispose и создание после Dispose | `Factories_MatchConstructorsAndKeepIndependentLifetime` — 15 cases |
| Empty/unicode/literal markup, compile-time Fluent chain | `TextFactories_PreserveLiteralContent` — 2 cases |
| Constructor validation string arguments | `TextFactories_NullLabels_KeepConstructorValidation` |
| Mixed tree, ввод и container ownership | `FactoriesAndConstructors_MixedTreeRoutesInputAndOwnsChildren` |
| Form editing/model notifications/retained focus/disposal | Отдельные Debug/direct Debug/published Release `--smoke`; SettingsFormSmoke.Frame/VerifyValues/VerifyDisposed |

Команды, завершившиеся exit 0:

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --verbosity quiet --filter 'FullyQualifiedName~ControlFactoryTests' --logger 'trx;LogFileName=control-factories.trx' --results-directory TestResults/AlloyFactories
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --verbosity quiet --logger 'trx;LogFileName=ui-suite.trx' --results-directory TestResults/AlloyFactories
dotnet build TrueMoon.slnx --no-restore --no-incremental --verbosity quiet
dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj --no-restore -- --smoke
./ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.exe --smoke
dotnet publish ManualTests/AlloyTest/AlloyTest.csproj -c Release --no-restore --verbosity quiet -o TestResults/AlloyFactories/publish
./TestResults/AlloyFactories/publish/AlloyTest.exe --smoke
```

Focused **34 passed/0 failed/0 skipped**, UI suite **188 passed/0 failed/0 skipped** (прежние154 +34); solution build **0 errors/1339 warnings**. Первый Debug smoke — **30 frames** до финального усиления Summary assertion; прямой Debug exe после финальной сборки — **31**, published Release — **30**, включая освобождение формы; publish exit 0. TRX/логи — `TestResults/AlloyFactories` (ignored).

Финальная статическая проверка: whitespace/conflict markers 18 изменённых файлов, Markdown fences и все72 локальные ссылки/heading anchors четырёх документов прошли; scoped tracked git diff --check exit0. Review нового ControlFactoryTests:5 test methods /54 assertion call sites /0 assertion-free methods; это не coverage score. Последние изменения после code runs — документация и удаление прежних trailing spaces в VStack/Line, без изменения исполняемого кода.

**Blocker первоначального запуска window 5.1a:** повторный `dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj --no-build --no-restore -- --smoke` после окончательной сборки завершился exit **-1073740940 / 0xC0000374 (heap corruption)**. `window-debug.log` содержит Starting/Started без managed exception, stack trace или successful smoke. На момент этого запуска сбой не был исправлен; успешные отдельные запуски не доказывали устойчивость. Последующий прямой exe (`window-debug-direct.log`) и published Release (`window-release.log`) прошли. В Application log соответствующего Event1000 не найдено; cdb/windbg/procdump через PATH недоступны. Конфигурация AlloyTest сообщает supportsDynamicLaunchOverrides=false. В том запуске настройки/breakpoints не менялись, debugger session не запускалась; диагностика и исправление lifetime последующего запуска описаны выше.

Broad review по code-testing-agent/test-gap-analysis/assertion-quality проверил defaults, type, aliasing/cache/disposed reuse, text forwarding и independent ownership; добавлен null validation case. Assertions проверяют значения, типы, identities, layout и side effects. Это статический review без coverage percentages/actual mutation runner. Research/plan/status находятся вне repository. Core binding/runtime/renderer/platform/native assets/packages/Git index не менялись. Не выполнялись полный solution test suite, Vulkan/HUD/backend comparison/validation, OS clipboard/fonts golden, device-loss, package consumer, DPI/resize/long soak. Factory API 4.8 готова в этих границах; весь5.1/alpha не завершены.

## Двусторонние property bindings 3.5b — предыдущий подэтап 2026-10-06

- [ElementExtensions](../../TrueMoon.Argentis/ElementExtensions.cs) предоставляет `BindTwoWay(uiProperty, source, x => x.Property)` с concrete Fluent return type. [PropertyBinding](../../TrueMoon.Argentis/PropertyBinding.cs) теперь общий для обоих направлений: direct public readable/writable property точно того же TValue, без conversion/private-set/init-only. One-way контракт сохранён; один binding любого направления на UiProperty. Initial attach/reattach читает source без write-back, detached local edits не пишут модель.
- UI edits записываются немедленно на owner thread при отличии от source, затем читается canonical value, даже без Notify. Только same-thread echo внутри source setter подавляется; остальные события остаются queued. Connection захватывает dispatcher/generation текущего attach, чтобы race с detach не мог переключить source callback на immediate unattached Dispatch. Busy guard запрещает вложенные setter вызовы; active/generation guards и Own/OwnAttachment освобождают source и UI handlers при detach/Dispose и пропускают stale queued/in-flight события после transfer.
- Setter failure пытается восстановить UI из фактического source value, включая partial commit. Setter/readback/validation/observer errors передаются после попытки восстановления: одиночный сохраняет тип, несколько AggregateException. Недоступное/невалидное source value не объявляется откатом модели; последнее committed UI value сохраняется, новое корректное изменение допускает recovery. Direct Set/HandleInput/Update caller получает ошибку. Hosting callback catch прочитан и не менялся; native/window failure probe здесь не выполнялся.
- [Element](../../TrueMoon.Argentis/Element.cs) после property commit вызывает protected OnPropertyChanged invariant hook, invalidation и всех observers с best-effort error collection. Это явное уточнение alpha semantics: ранний callback exception больше не подавляет binding/later observers. [TextBox](../../TrueMoon.Argentis/TextBox.cs) clamps generic Set и normalized editing selection/caret на границы графем до observers; [Slider](../../TrueMoon.Argentis/Widgets.cs) освобождает capture при failed drag. Renderer/platform/native/packages/index не менялись.
- [TwoWayBindingTests](../../TrueMoon.Alloy.Tests/TwoWayBindingTests.cs) — **41 cases**, [PropertyNotificationTests](../../TrueMoon.Alloy.Tests/PropertyNotificationTests.cs) — **3 cases**, все passed. Проверены actual routed input/clipboard, canonical/equality/feedback/reentrancy/failures, selectors, attach/detach/Move/transfer, worker notifications и stale events. Broad research/plan/test-gap/assertion review выполнены inline во внешнем OS scratch (не в рабочем дереве); Roslyn static pairing запущен один раз, это heuristic, не coverage metric.

| Требование 3.5b | Точные test evidence текущего clean run |
| --- | --- |
| Initial attach без записи; concrete Fluent API | `BindTwoWay_InitialAttach_SourceWinsWithoutWriteBack` (2 cases) |
| TextBox input, canonical normalization, caret | `TextInput_Normalization_ImmediatelyWritesCanonicalTextAndClampsCaret` |
| Clipboard/латиница/кириллица/emoji selection | `ClipboardAndGraphemeEditing_WritesModelAndKeepsValidSelection` |
| CheckBox pointer/keyboard без feedback | `CheckBox_PointerOrKeyboardActivation_WritesOnceWithoutSourceFeedback` (2 cases) |
| Slider drag/keys и normalization без Notify | `Slider_DragAndArrowKey_WriteModelAndReleaseCapture`, `Slider_NormalizingModelWithoutNotify_AppliesCanonicalValueImmediately` |
| Equality, reentrant setter и независимые события | `UiEdit_AlreadyEqualToSource_DoesNotInvokeSetterOrRedrawAgain`, `ReentrantUiChange_InSourceSetter_DoesNotReenterSetterAndEndsCanonical`, `SourceChange_FromUiObserverDuringSync_IsQueuedWithoutWriteBack`, `BackgroundEvent_DuringUiWrite_IsStillQueuedForOwnerThread` |
| Setter rejection/partial commit/recovery; readback failure | `SetterFailure_RestoresActualModelStateAndLaterInputRecovers` (2 cases), `SetterAndReadbackFailure_AggregatesBothErrorsWithoutLosingConnection`, `CanonicalValue_FailsUiValidation_ReportsErrorAndRecoversOnValidSourceEvent` |
| Validation до write-back и ошибочный source getter | `LocalUiValidation_RejectsBeforeWriteBackAndAcceptsEndpoints` (4 cases), `SourceGetterFailure_QueuedSync_DoesNotWriteAndNextEventRecovers` |
| Property errors после commit не пропускают observers | `SetOrClear_ThrowingInvalidationAndObserver_StillNotifiesLaterObserver` (2 cases), `InvariantHookFailure_StillInvalidatesAndNotifiesAfterCommit`, `EarlierThrowingObservers_StillRunBindingAndLaterPropertyObservers` |
| Generic Set TextBox invariant / failed Slider capture | `GenericSet_TextBoxSelection_ClampsToGraphemeBeforeObservers`, `Slider_FailedDrag_ReleasesCaptureAndNextDragRecovers` (2 cases, bound/unbound) |
| Detach/reattach/Move и foreign-thread lifetime | `DetachReattachAndMove_KeepExactlyOneBidirectionalConnection`, `Transfer_ToNewUiThread_SkipsOldQueuedAndInFlightSourceHandlers`, `BackgroundSourceEvent_OnlyReadsAndMutatesDuringOwnerUpdate` |
| Stale queue/in-flight и detach-before-unsubscribe gap | `QueuedOldSourceEvent_ReattachSameSession_CannotOverwriteCurrentUi`, `Notification_DuringDetachBeforeUnsubscribe_CannotReadOrWriteSource` |
| Failed construction rollback/slot retry | `InitialGetterFailure_SessionRollsBackBothDirectionsAndRetryWorks`, `InitialGetterFailure_AttachedBind_ReleasesSlotAndUiHandlerBeforeRetry` |
| Selector/null/duplicate rejection до подключения | `BindTwoWay_UnsupportedSelector_IsRejectedBeforeSubscription` (6 cases), `BindTwoWay_ConvertedOrNullSelectorsAndArguments_AreRejected`, `Bind_DuplicateDirection_IsRejectedWithoutReplacingOriginal` (2 cases) |

Фактические команды текущего запуска, все exit 0:

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --verbosity quiet --filter 'FullyQualifiedName~TwoWayBindingTests|FullyQualifiedName~PropertyNotificationTests' --logger 'trx;LogFileName=two-way-bindings.trx' --results-directory TestResults/AlloyTwoWay
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --verbosity quiet --logger 'trx;LogFileName=ui-suite.trx' --results-directory TestResults/AlloyTwoWay
dotnet build TrueMoon.slnx --no-restore --no-incremental --verbosity quiet
```

Focused **44 passed/0 failed/0 skipped**, UI suite **154 passed/0 failed/0 skipped** (прежние 110 +44), build **0 errors/1341 warnings**. TRX/build log — `TestResults/AlloyTwoWay` (ignored). PLAN/Hosting README фиксируют API и error/invariant semantics. Блокеров этого подэтапа не выявлено; 3.5b завершён в этих границах, весь 3.5/3.6 и alpha — нет.

После финального усиления captured attachment dispatcher focused 44/44, UI suite 154/154 и non-incremental solution build повторно прошли на окончательном коде с теми же результатами. Выполнены scoped tracked git diff --check, проверки whitespace/conflict markers 10 изменённых файлов, fences трёх Markdown документов, 60 локальных ссылок/heading anchors и всех имён current test evidence по свежему TRX — успешно. Broad review: 30 test methods, 199 статически найденных assertions, ни одного assertion-free метода; это review, не coverage/mutation score. Внутренние research/plan/status находятся вне repository и не входят в commit changes; Git index не менялся.

Не запускались GPU/manual/window/publish, полный solution suite, coverage metrics/actual mutation runner, новые external/package consumers и long concurrent stress; arbitrary throwing event accessors и бесконечно самоменяющиеся user callbacks не покрыты. Полные interactions остальных контролов, collections/dynamic regions и новая demo остаются по PLAN. Следующий конкретный шаг — фабрики 4.8 + первая форма AlloyTest 5.1a.

## Согласованная корректировка плана — предыдущая документационная работа 2026-10-06

- Архитектура, retained tree, net10.0/xUnit, standalone/HUD и K1–K9 сохраняются. Номера исходных пунктов сохранены; добавлены 3.5a (one-way), 3.5b (two-way), 3.5c (collections), 3.5d (условные области), 5.1a/b/c (постепенное развитие одного AlloyTest). Функциональные подэтапы отделены от итогового alpha прохода. Это согласованный план, не наличие будущих API или новых проверок поведения.
- Для 3.5b приняты общий механизм TextBox.Value/CheckBox.IsChecked/Slider.Value и запись при каждом изменении редактируемого значения. До реализации уточнить typed API, setter normalization/rejection, состояние контрола после ошибки и feedback/reentrancy guards. Сохраняются owner-thread dispatch, attachment lifetime и инварианты контролов. Commit по потере фокуса — дальнейшее расширение.
- Фабрики 4.8 и форма 5.1a выполняются сразу после 3.5b, до collections: model editing → dynamic list/условные ветви → scrolling/themes/все control interactions. Ближайший сквозной результат — небольшое приложение с редактируемой моделью, динамическим списком и сохранением состояния незатронутых элементов. Сценарий затем используется на общих backend и адаптируется к HUD.
- Для 3.5c заранее определить item/node correspondence, Reset/duplicates и владение generated nodes, включая factory/notification failure. 3.5d сначала использует существующий SetContent для загрузки/ошибки/содержимого; BindContent только по фактической потребности, не обязательная API alpha. Это не изменение caller ownership при Remove/SetContent.
- Итоговый alpha проход привязан к фиксированной Windows x64/GPU/driver/native конфигурации: реальные interactions, fonts/backend comparisons, resize/DPI, lifetime/validation, графическая ошибка и новая сессия из сохраняемой модели, measurements baseline и отдельный consumer/publish/package. K8 не обещает бесшовное восстановление; K9 не обещает dirty-subtree layout до измерений. Текущий layout при соответствующей invalidation начинается от root.
- Исправлены устаревшие summary о 86 тестах и отсутствующем binding lifetime: one-way уже проверен 2026-10-06, 86/86 остаются датированным ownership результатом 2026-10-05. Исторические записи и команды сохранены. Блокеров документационной корректировки не выявлено; реализация следующих подэтапов и итоговые alpha checks остаются впереди.
- Фактически выполнены чтение PLAN/STATUS/AGENTS, сверка новых подэтапов и порядка, scoped git diff --check, проверки whitespace/conflict markers и парности Markdown fences в обоих документах, всех 38 локальных ссылок и их heading anchors — успешно. Изменены только PLAN/STATUS; build/tests/GPU/publish не выполнялись, код/packages/native assets/Git index не менялись. Следующий конкретный шаг — контракт и реализация 3.5b, описанные ниже.

## Односторонние property bindings — предыдущий подэтап кода 2026-10-06

- [ElementExtensions](../../TrueMoon.Argentis/ElementExtensions.cs) предоставляет `Bind(uiProperty, source, selector)` и `BindText(source, selector)`, возвращающие конкретный тип контрола. [PropertyBinding](../../TrueMoon.Argentis/PropertyBinding.cs) реализует lifetime без новых package/project dependencies. Source — reference type с INotifyPropertyChanged; прямое instance property со стандартным приведением типа. Nested/computed/field selectors отклоняются. Одно UiProperty — одна registration до Dispose; повторный Bind отклоняется. Unbind, DataContext auto-binding и замена регистрации не добавлены.
- [Element](../../TrueMoon.Argentis/Element.cs) резервирует slot регистрации, хранит её через Own и возвращает slot при неуспешном создании. Source connection хранится через OwnAttachment: initial sync только на attach или при Bind уже attached элемента, отключение на detach и новая subscription на reattach; Move её сохраняет. Connection имеет отдельный active guard для in-flight source handlers, включая промежуток между отсоединением дерева и unsubscribe. После transfer старые callbacks не читают source и не меняют контрол.
- Notify выбранного property/null/empty name отправляет чтение в attachment-scoped Dispatch. Чтение и Element.Set происходят на owner thread во время следующего Update; getter читает последнее состояние и должен быть безопасен на UI-потоке. Прямой записи UI → source нет. Equality/validation/layout invalidation сохраняют обычную семантику Element.Set. Initial/queued getter failure и invalid destination value проверены с cleanup/retry. Ошибки queued sync передаются из Update, восстановление — после нового корректного notification. Не обещается rollback committed UI property при observer failure.
- Focused scope — один новый binding mechanism, его Fluent entry points и существующий UiSession lifecycle. [PropertyBindingTests](../../TrueMoon.Alloy.Tests/PropertyBindingTests.cs): **24 passed/0 failed/0 skipped**. Основные требования сопоставлены с точными именами ниже; остальные cases проверяют selectors/null/duplicate/validation/getter failure/numeric conversion/owner-thread guard/equality.

| Требование подэтапа 3.5/3.6 | Проверка |
| --- | --- |
| Первичная синхронизация при attach и layout invalidation | `Bind_BeforeAttach_SynchronizesOnAttachAndInvalidatesLayout` |
| Выбранное свойство, null/empty и игнорирование соседнего | `Notification_MatchingOrAllProperties_OnlyReadsExpectedSource` (4 cases) |
| Односторонность, без feedback из TextBox | `Bind_AttachedTextBox_SynchronizesImmediatelyWithoutWritingBack` |
| Detach/reattach/Move без дублирования подписки; Dispose отключает source | `Detach_ReattachAndMove_RestoresExactlyOneSubscription` |
| Фоновый getter и запись выполняются на owner thread | `BackgroundNotification_ReadsAndMutatesOnlyDuringOwnerUpdate` |
| Прежняя очередь после reattach в ту же сессию | `QueuedUpdate_ReattachSameSession_SkipsOldAttachment` |
| Transfer на другой UI-поток, прежняя очередь и in-flight handler | `Transfer_ToDifferentOwnerThread_DropsOldWorkAndInFlightHandlers` |
| Уведомление до unsubscribe при detach не читает модель на worker | `Notification_DuringDetachBeforeUnsubscribe_DoesNotReadOnWorker` |
| Failed attach откатывает connection, registration работает при retry | `InitialGetterFailure_SessionAttach_RollsBackAndReconnectsOnRetry` |

**Команды предыдущего binding code run 2026-10-06, все exit 0:**

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --verbosity quiet --filter 'FullyQualifiedName~PropertyBindingTests' --logger 'trx;LogFileName=property-bindings.trx' --results-directory TestResults/AlloyBindings
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --verbosity quiet --logger 'trx;LogFileName=ui-suite.trx' --results-directory TestResults/AlloyBindings
dotnet build TrueMoon.slnx --no-restore --no-incremental --verbosity quiet
```

Focused **24/24**, вся UI suite **110/110** (прежние 86 +24), build **0 errors/1341 warnings**. TRX и полный build log — `TestResults/AlloyBindings` (игнорируемые артефакты). xUnit/net10.0 и существующие packages сохранены. PLAN и Hosting README фиксируют API, lifecycle и ограничения; решение о фабриках 4.8 сохранено. Git index, renderer/platform/native assets и пакеты не менялись.

Дополнительно успешно выполнены scoped tracked git diff --check, проверки whitespace/conflict markers семи изменённых файлов, парности Markdown fences и сверка всех имён test evidence текущего подэтапа со свежим focused TRX. Assertions перечитаны по lifecycle/dispatch/error веткам; отдельные промежуточные research/plan/status файлы для focused scope не создавались.

Блокеров одностороннего подэтапа не выявлено. **Не выполнены в code run:** двусторонние/collection bindings, nested/computed dependency tracking, dynamic AlloyTest demo, GPU/manual/publish, полный solution suite, coverage metrics, arbitrary throwing/reentrant source event accessors и долгий concurrent stress. Это не завершение всего 3.5/3.6 или alpha. Последовательность дальнейшей работы уточнена в согласованной корректировке выше и в разделе «Следующий конкретный шаг».

## Согласованное решение о фабриках — предыдущая документационная правка 2026-10-06

- Конструкторы и фабрики поддерживаются вместе и могут смешиваться в одном дереве. Начальные имена — Button.Simple и Text.Plain; Create предусмотрен для обычного создания без отдельного варианта.
- Фабрика объявляется на типе контрола Argentis, создаёт новый независимый экземпляр и возвращает конкретный тип для дальнейшей Fluent-цепочки. Публичные конструкторы сохраняются. Ownership, lifetime, динамическое обновление и независимость от backend не меняются.
- В PLAN 4.8 зафиксированы требования и будущие проверки; в README — целевой пример со смешанным созданием и явная отметка отсутствующей реализации. Блокеров фиксации решения нет. Код контролов, packages/native assets и Git index не изменялись; исторические build/tests/GPU результаты сохранены отдельно.
- Фактически выполнены чтение PLAN/STATUS/README, scoped tracked git diff --check, проверки whitespace/conflict markers и парности Markdown code fences в трёх изменённых документах — успешно. Поиск в Button.cs/Text.cs подтвердил отсутствие реализации Simple/Plain; build/tests/GPU/publish не выполнялись.

## Где остановилась работа

Актуально после сверки **2026-10-09**: реализованы общая Settings-сцена raster/OpenGL/Vulkan/HUD, controlled failure/recreate2.4/K8, K9 baseline/profile/reuse и Windows transparency/custom chrome/native Snap4.9b/c/d. Последняя запись запуска2026-10-08 — UI451/451 и bounded Debug/published Release window/input/alpha regression. **Следующий шаг — native DPI → UiViewport.Scale/input/client min-max contract**, затем реальные100/150/200% и переходы между мониторами. Clipboard positive fixture checks и shared scene уже выполнены в датированных предыдущих запусках; это больше не следующие задачи. Полнота4.9 и итоговый alpha открыты.

Ownership foundation сохраняется: [Element](../../TrueMoon.Argentis/Element.cs), [ElementCollection](../../TrueMoon.Argentis/ElementList.cs) и [ContentControl](../../TrueMoon.Argentis/Containers.cs) возвращают старое поддерево вызывающему коду без Dispose. Move сохраняет attachment и ввод; Remove/замена/перенос отменяют transient input. UiSession освобождает ссылки focus/capture/hover до передачи другой сессии, в том числе на другой поток. OwnAttachment, generation guard queued Dispatch и общий TreeChange обеспечивают notifications/cleanup после commit; [публичные правила](../../TrueMoon.Alloy.Hosting/README.md#владение-динамическим-деревом).

Предыдущий **2.1b** сохраняется: VisualTree/Visual, IGraphicsPlatform, ViewHandle/ViewManager, presentation initializer/configuration, presenters/factories и архивные прототипы перенесены в [Hosting/Compatibility](../../TrueMoon.Alloy.Hosting/Compatibility/README.md), namespace `TrueMoon.Alloy.Hosting.Compatibility` и `.Compatibility.Presenters`. No-options UsePresentation остался только opt-in compatibility API. Alloy лишён PackageReference Silk/Skia/Topten и ProjectReference Core/Contracts; Hosting объявляет необходимые зависимости явно. [AlloyTest](../../ManualTests/AlloyTest/README.md) использует explicit OpenGL/Silk и ограниченный `--smoke`; его отдельный smoke в текущем ownership подэтапе не повторялся.

Перенос 2.1b — **breaking alpha assembly/namespace migration без type forwarders**: legacy consumers обновляют references/imports/callers и пересобираются. Во время того переноса новый explicit Hosting API, Runtime и GPU алгоритмы не менялись; текущий ownership подэтап меняет retained runtime lifetime. `Visual.GetEnumerator` и старый compatibility window/lifetime/disposal порядок не исправлены. Проверка регистрации compatibility не является проверкой старого оконного пути.

Предыдущий 2.1a сохраняется: [VulkanWindowPresenter](../../TrueMoon.Alloy.Hosting/VulkanWindowPresenter.cs)/counters и [SilkVulkanHost](../../TrueMoon.Alloy.Hosting/SilkVulkanHost.cs) находятся в Hosting. Renderer принимает только `VulkanHostContext`; Silk overloads/Device удалены. Прямой renderer→Platform reference и транзитивные legacy dependencies через Alloy теперь отсутствуют.

Предыдущий OpenGL шаг сохраняется: [SkiaOpenGLRenderBackend/OpenGLUiTarget/SkiaOpenGLSurface](../../TrueMoon.Alloy.Rendering.Skia/SkiaOpenGLRenderBackend.cs), `UseSkiaOpenGL` и OpenGL standalone ветка HostedUiWindow/SilkWindowHost используют тот же UiSession/Argentis/Skia drawing/text adapter, что raster/Vulkan. UI owns offscreen RGBA/Premul GPU frame; host owns native GL context, destination RGBA8 FBO/attachments, window и frame loop. `PresentOpenGL(...clear: false)` композитит UI поверх сцены без CPU transfer, `ReadbackOpenGLImage` — caller-owned независимая CPU копия для diagnostics/export.

OpenGL host передаёт procedure resolver и current-context guard, сохраняет context/callbacks до закрытия UI; операции сериализованы на creator thread. Skia changes GL state, host заново binds свой state перед дальнейшей работой. Zero viewport suspended; stale/unrendered export/presentation запрещены. Resize/scale/input/invalidation выполняет общий runtime. [Примеры и владение](../../TrueMoon.Alloy.Hosting/README.md).

Standalone создаёт GL window на UI thread и явно swaps buffers. После исправления2026-10-07 SilkWindowHost.Run сохраняет window/context/input до host.Dispose; session закрывается в Closing, renderer и input освобождаются до уничтожения окна. Render/input/posted window operation exceptions перехватываются внутри GLFW callbacks, после безопасного закрытия становятся Completion/StopAsync failure. В текущем запуске это проверено также для collection factory failure. Legacy SkiaGlViewPresenter очищен от круга/time text/CreateVulkan/empty catch, Visual API сохранён для совместимости.

**Функциональные подэтапы отделены от готовности alpha.** Графический прототип/пути, границы2.1a/b, ownership retained дерева, bindings3.5a/b/c, условные области3.5d и форма/список5.1a/b имеют реализацию и проверки в своих датированных запусках. Общие этапы не объявлены завершёнными: итоговые alpha проверки, полные control interactions, font golden fixtures, legacy2.3/structured diagnostics2.4 и failure/performance сценарии остаются. Alloy→Argentis/BCL; Rendering.Skia→Alloy + Skia/Vulkan bindings; Platform.Silk→Alloy + Silk, без Skia; Hosting связывает adapters/App/compatibility.

## Выполненные проверки ownership — предыдущий подэтап 2026-10-05

Windows x64, SDK **10.0.401**, прежние packages/native DLL/renderer/platform алгоритмы. Реализация проверена отдельно от результатов выполнения:

- Remove/Clear/SetContent/замена коллекции отсоединяют subtree без Dispose. Прямые Detach/Dispose ребёнка с Parent отклоняются до изменения; автоматический virtual child disposal контейнера сохранён. Own действует до Dispose; OwnAttachment освобождается при detach и создаётся заново при reattach. Root живой UiSession вручную не отсоединяют/не передают.
- TreeChange проверяет входные ownership/thread условия до изменения, фиксирует все связи до notifications и блокирует reentrant tree mutations. Callback/cleanup exception не откатывает commit: остальные observers/cleanup продолжаются, затем одиночная ошибка или AggregateException передаётся caller. Invalidation доходит до session/ancestors даже после observer error. Не повторять Add/SetContent автоматически после такой ошибки. Failed UiSession construction откатывает attachment всех узлов; root остаётся caller.
- UiSession очищает focus/capture/hover references при detach; input state не восстанавливается при reattach. Queued Element.Dispatch пропускается после конца attachment. Неизменённые editor selection/focus/capture и ScrollViewer offset сохраняются. Session disposal продолжает tree/surface/registry cleanup после ошибок подписок; повторный Dispose не повторяет освобождение.
- `dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --verbosity quiet --filter 'FullyQualifiedName~OwnershipTests' --logger 'trx;LogFileName=ownership.trx' --results-directory TestResults/AlloyOwnership`: **Passed: 27, Failed: 0, Skipped: 0**, exit 0. Headless surface, без native/GPU calls. [OwnershipTests](../../TrueMoon.Alloy.Tests/OwnershipTests.cs): actual pointer activation/reinsert, focused/captured nested replacement, sibling editor/scroll state, transfer между сессиями и разными UI-потоками, Move, реальный publisher subscribe/unsubscribe/reattach, Clear/null/identity/index/disposed/thread validation, stale queued work, reentrancy, rollback и cleanup failures.
- `dotnet build TrueMoon.slnx --no-restore --no-incremental --verbosity quiet`: **0 errors/1341 warnings**, exit 0. Прежние nullable/XML/NU1507 warnings остаются. Финальная `dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --verbosity quiet --logger 'trx;LogFileName=ui-suite.trx' --results-directory TestResults/AlloyOwnership`: **Passed: 86, Failed: 0, Skipped: 0**, exit 0: прежние 59 cases +27 ownership. Assembly boundary assertions по-прежнему проходят.
- Девять manual probes: `--raster`, `--runtime --validation`, `--raw-hud-window --validation`, `--backend-compare --validation`, `--opengl-window`, `--opengl-failure`, `--window --validation`, `--retirement-soak --soak-cycles 5 --validation` и тот же soak с `--legacy-presentation`: каждый **exit 0** в Debug и published Release. VK_LAYER_PATH задавался только процессу на существующий validation manifest.
- Raw HUD каждый раз: **90 presentations/90 scene compositions/12 UI redraws/2 sessions/3 swapchains/6 readbacks**; unchanged view на raster/OpenGL/Vulkan, scale 1/1.5/2, layout/input/selected RGBA passed. Vulkan standalone **30 frames/2 swapchains**; OpenGL standalone **31 Debug/30 published frames**, GL checks passed. Intentional OpenGL draw failure дошёл до Completion/StopAsync, registry/tree освобождены.
- Retirement KHR/forced legacy каждый в Debug/published: **5 sessions/50 frames/10 created = destroyed swapchains/1 minimize**, peak 2 generations, owned Vulkan objects/subscriptions/retained UI references **0**. Vulkan core/synchronization validation во всех validation probes **0 errors/0 warnings**, включая teardown. Это короткий soak, не повтор длительного 200-session результата и не VRAM/driver profile.
- `dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --no-restore --configuration Release --verbosity quiet --output TestResults/AlloyOwnership/publish`: exit 0. Published probes загружают bundled native DLL из нового output.
- Scoped tracked diff --check, whitespace/conflict checks 10 затронутых source/docs файлов и существование новых documentation link targets проверены; финальный TRX подтвердил 86 passed/0 failed/0 skipped. TestResults/AlloyOwnership/RunProbes.ps1 игнорируется Git. Предыдущие изменения ElementList.Add1 и остальные пользовательские правки сохранены; Git index не изменялся.

Логи/TRX/RunProbes.ps1/publish — `TestResults/AlloyOwnership` (игнорируемые артефакты). Блокеров текущей policy не выявлено. **Не выполнялись в этом подэтапе:** полный solution test suite, отдельные core-only/renderer-only/NuGet/compatibility consumers, AlloyTest smoke, raw-host и standalone interop-compose probes, legacy window, long soak, EXT-only/другие GPU, monitor DPI migration, device/context loss, fixed-font golden и timing/allocation/driver profiling. Их прежние результаты не засчитываются текущими. Bindings и полные lifetime/race сценарии будущих bindings остаются следующим объёмом; legacy 2.3 и alpha не объявлены завершёнными.

## Выполненные проверки 2.1b — предыдущий подэтап 2026-10-05

Рабочий каталог — `src`, Windows x64, SDK **10.0.401**, bundled Skia **4.153.1**, desktop OpenGL3.3/NVIDIA RTX5070Ti, driver616.92. Реализация переноса найдена в исходниках; следующие результаты — фактически новые запуски **после 2.1b**, не перенос результатов 2.1a.

- `dotnet restore TrueMoon.slnx --ignore-failed-sources --verbosity quiet` и `dotnet build TrueMoon.slnx --no-restore --no-incremental --verbosity quiet`: exit0, **0 errors/1341 warnings**. Версии существующих пакетов не менялись; Hosting явно подключает Silk.NET/SkiaSharp/Topten/Core/Contracts. Прежние nullable/XML/NU1507 warnings сохраняются; local consumer restore также показал прежние NU1506 дубли CodeAnalysis. Sources/дубли не исправлялись.
- `dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --verbosity quiet --logger trx --results-directory TestResults/AlloyCoreSplit`: exit0, **Passed:59, Failed:0, Skipped:0**. Все прежние56cases +3новых `CoreBoundaryTests`: Alloy references только Argentis/BCL, Argentis references только BCL, legacy exports отсутствуют в Alloy. Runtime/GPU production-код не менялся.
- Отдельный свежий **CoreOnly** consumer с единственным reference на Alloy прошёл direct UiSession с headless surface: retained layout/update/focus/Post/disposal. В output только consumer/Alloy/Argentis DLL, без App/Skia/Silk/native assets. Это проверка независимости ядра, не pixel rendering.
- Отдельный свежий **RendererOnly** consumer с единственным reference на Rendering.Skia прошёл direct raster и raw Vulkan session/texture lease с самостоятельно созданным host device. В output нет Platform.Silk/Hosting/Core/Contracts/Topten/Windowing DLL. Device после UI disposal остаётся рабочим. Этот дополнительный raw smoke — без validation; validation отдельно ниже.
- Отдельный **CompatibilityConsumer** с opt-in no-options UsePresentation прошёл build/App/DI: graphics factory, tree builder и startup IView разрешаются, legacy типы принадлежат Hosting; новый HostedUiWindow не регистрируется. Старое окно **не запускалось**, lifetime/GetEnumerator bugs этим не закрыты.
- Debug и published Release: `--raster`, `--raw-host --validation`, `--runtime --validation`, `--raw-hud-window --validation`, `--backend-compare --validation`, `--opengl`, `--opengl-window`, `--opengl-failure`, `--window --validation`, `--retirement-soak --soak-cycles 5 --validation`, тот же soak с `--legacy-presentation`, `--interop-compose --validation`: **все12сценариев в каждом варианте — exit0**. Process-only VK_LAYER_PATH указывает на существующий validation manifest в TestResults/VulkanInterop; runtime/SDK не менялись.
- Raw host: **3engine-owned devices/6hosted +3direct sessions/54readbacks**; raw HUD: **90presentations/90scene compositions/12UI redraws/2sessions/3swapchains/6readbacks**, каждый Debug/published. Один unchanged view на raster/OpenGL/Vulkan совпал по layout/input/selected pixels при scales1/1.5/2. Vulkan core/synchronization validation **0errors/0warnings**, включая teardown.
- OpenGL external: **3GPU compositions**, alpha/ownership/guards/retry/independent readback; standalone **30Debug/31published frames**, GL checks passed; intentional draw failure доходит до Completion/StopAsync, registry/tree освобождены. Vulkan standalone **30frames/2swapchains**, validation0/0.
- Короткий retirement, каждый KHR/forced legacy в Debug/published: **5sessions/50frames/10created = destroyed swapchains/1minimize**, peak2generations, owned objects/subscriptions/retained roots0, validation0/0. External composition: **36compositions/18leases/2contexts**, validation0/0. Длительный200-session soak/VRAM profile не повторялся.
- `dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --no-restore --configuration Release --verbosity quiet --output TestResults/AlloyCoreSplit/publish` и соответствующий publish AlloyTest в `publish-alloytest`: exit0. Все published probes загружают bundled native DLL из нового output. Native asset/ABI, lease/retirement алгоритмы не пересобирались/не менялись.
- Новый **AlloyTest --smoke** через actual View1/View2/App/DI/explicit OpenGL/Silk прошёл: **31frames Debug/32frames published**, автоматическое закрытие и App.Stop/Dispose. Это минимальный оконный smoke, не демонстрация dynamic controls5.1 и не screenshot golden.

Логи/TRX, RunProbes.ps1, три локальных consumer проекта и publish — `TestResults/AlloyCoreSplit` (игнорируемые артефакты). Дополнительный read-only review не выявил блокирующих замечаний; 30legacy-файлов сопоставлены с прежним tracked содержимым после нормализации namespace, предыдущие пользовательские изменения SkiaGlViewPresenter сохранены. Scoped tracked diff --check и whitespace/conflict checks10новых/редактированных source/docs файлов прошли;30legacy-файлов совпали после нормализации namespace/BOM/EOL, без массового whitespace cleanup. ProjectReference граф7достижимых проектов — без циклов. Свежие project.assets.json подтвердили Alloy→толькоArgentis/zero packages, renderer безCore/Contracts/Hosting/Windowing и platform безSkia. Проверка сравнения сначала остановилась из-за неверного декодирования UTF-8 BOM вывода git в PowerShell; process-only OutputEncoding исправлен, повторная проверка прошла. Production-код ради проверки не менялся.

**Не запускались:** полный test suite решения, NuGet-consumer, старый compatibility window, long soak, EXT-only/другие GPU, real monitor DPI migration, device/context loss, fixed-font golden и allocation/driver profiling. Исторические AppCreate/IAppLifetime и пустой Mithril.Tests остаются отдельными вопросами. Блокеров2.1b не выявлено; ownership/bindings/controls и оставшиеся K1–K9 — следующий объём, не объявление alpha готовой.

## Выполненные проверки 2.1a — предыдущий подэтап 2026-10-05

Рабочий каталог — `src`, Windows x64, SDK **10.0.401**, bundled Skia **4.153.1**, desktop OpenGL 3.3/NVIDIA RTX 5070 Ti, driver 616.92. Результаты ниже относятся к предыдущему подэтапу2.1a; они не засчитываются вместо новых запусков2.1b.

- `dotnet restore TrueMoon.slnx --ignore-failed-sources --verbosity quiet`: exit 0. Добавлена явная backend-specific `Silk.NET.Vulkan` dependency **2.23.0** вместо renderer→platform; версия уже используемых bindings не менялась. Существующие NU1507 сохраняются; renderer-only restore также показал прежние NU1506 дубли CodeAnalysis. Настройки sources и дубли не исправлялись.
- `dotnet build TrueMoon.slnx --no-restore --no-incremental --verbosity quiet`: exit 0, **0 errors/1341 warnings**. Hosting разрешает unsafe для перенесённого presenter; прямой renderer ProjectReference — только Alloy, прямой Platform.Silk ProjectReference — только Alloy. Обратной ссылки platform→renderer нет, циклов сборки не возникло.
- `dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --verbosity quiet --logger trx --results-directory TestResults/AlloyDependencySplit`: exit 0, **Passed:56, Failed:0, Skipped:0**. Все прежние 53 cases и 3 новых `RendererBoundaryTests`: фактические assembly references без Platform.Silk/Hosting/Windowing, raw-only Vulkan public constructors и отсутствие presenter/counters в renderer. Они не утверждают отсутствие транзитивных legacy dependencies через Alloy.
- Отдельный локальный `RendererOnly.csproj` с единственным ProjectReference на Rendering.Skia собран и запущен: **direct CPU raster и raw Vulkan session/texture lease прошли**, Platform.Silk/Hosting DLL отсутствуют в output. Сам consumer создаёт instance/device/queue через Vulkan bindings, передаёт raw descriptor, закрывает UI и проверяет живое host device после disposal. App, Silk device wrapper и окно не используются. Этот дополнительный smoke запускался без validation и не заменяет отдельные validation probes.
- Debug и published Release: `--raster`, `--raw-host --validation`, `--runtime --validation`, `--raw-hud-window --validation`, `--backend-compare --validation`, `--opengl`, `--opengl-window`, `--opengl-failure`, `--window --validation`, `--retirement-soak --soak-cycles 5 --validation`, тот же soak с `--legacy-presentation`, `--interop-compose --validation`: **все 12 сценариев в каждом варианте — exit 0**.
- Raw host: **3 engine-owned devices/6 hosted +3 direct sessions/54 readbacks**. Raw HUD: **90 presentations/90 scene compositions/12 UI redraws/2 sessions/3 swapchains/6 scene readbacks** в каждом варианте. Triple backend: один unchanged view, scales **1/1.5/2**, matching layout/input/selected pixels. Vulkan core/synchronization validation **0 errors/0 warnings**, включая teardown.
- OpenGL external: **3 GPU compositions**, premultiplied alpha, borrowed FBO/context ownership и guards/retry/readback. Standalone: **30 Debug/31 published frames**, resize/minimize/restore, GL checks passed. Intentional draw failure дошёл до Completion/StopAsync, registry/tree освобождены. Vulkan standalone: **30 frames/2 swapchains**, validation **0/0**.
- Каждый короткий retirement KHR и forced legacy: **5 sessions/50 frames/10 created = destroyed swapchains/1 minimize**, peak 2 generations, owned objects/subscriptions/retained roots 0; validation **0/0**, Debug и published. Это smoke, не повторение исторического 200-session soak и не VRAM profile.
- External interop composition: **36 compositions/18 leases/2 Skia contexts**, validation **0/0** в обоих вариантах; проверена и перенесённая Silk surface factory.
- `dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --no-restore --configuration Release --verbosity quiet --output TestResults/AlloyDependencySplit/publish`: exit 0; все published сценарии выше использовали bundled DLL из нового output. Native DLL/ABI не пересобирались.

Первый raw-host validation запуск без настройки окружения остановился на отсутствии `VK_LAYER_KHRONOS_validation`; это не успешная GPU-проверка. Затем process-only `VK_LAYER_PATH` установлен на существующий `TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin`, и весь Debug/Release набор выше прошёл. Системный runtime/SDK не устанавливался и не менялся.

Логи/TRX, повторяемый локальный `RunProbes.ps1`, renderer-only consumer и publish — `TestResults/AlloyDependencySplit` (игнорируемые локальные артефакты). Статический дополнительный review не выявил блокирующих замечаний. Scoped tracked `git diff --check`, whitespace/conflict check всех22изменённых UI файлов и обход ProjectReference графа (7 достижимых проектов, без циклов) прошли. Полный test suite решения, NuGet-consumer, long soak, EXT-only/другие GPU, device/context loss, fixed-font golden и allocation/driver profiling **не выполнялись**. Блокеров завершения 2.1a не выявлено; эти ограничения и legacy Alloy migration остаются отдельными задачами.

## Проверки предыдущего OpenGL шага — исторические результаты

- `dotnet build TrueMoon.slnx --no-restore --no-incremental --verbosity quiet`: exit0, **0 errors/1341 warnings**. Scoped manual build: exit0, 0errors/14warnings. NU1507/XML и прежний VulkanDevice nullable warning остаются; пакеты не менялись.
- `dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --verbosity quiet --logger trx --results-directory TestResults/AlloyOpenGL`: exit0, **Passed:53, Failed:0, Skipped:0**, включая существующие24raster cases. Новые GL проверки — manual GPU probes, не новые xUnit tests.
- `--opengl`: Debug и published Release прошли App/DI/external host, retained input/redraw/Post, guards target/viewport/loader/thread/stale/reentrant, draw failure/retry, suspend/restore, неизменный CPU readback после redraw/resize/disposal, registry/tree disposal и borrowed context reuse. 3 shared-view GPU FBO compositions подтвердили premultiplied alpha, scene/background/button pixels, bottom-left origin и host texture/FBO ownership. GL GetError — NoError.
- `--opengl-window`: Debug и published Release **30 frames**, actual resize/minimize/restore/dynamic update/App disposal. GL error checks прошли; GL не имеет Vulkan swapchain generations.
- `--opengl-failure`: Debug/Release intentional DrawCore error дошёл до Completion/StopAsync после закрытия; registry0, process exit0. Это проверка одной render failure, не всех возможных cleanup failures.
- `--backend-compare --validation`: Debug/Release один неизменный BackendHudView на **raster/OpenGL/Vulkan**, scales1/1.5/2; matching bounds/input/selected RGBA pixels (допуск1), retained redraw. Vulkan core/synchronization validation **0 errors/0 warnings**, включая teardown; GL composition GetError NoError. Полного glyph/screenshot golden сравнения нет.
- Debug regressions: `--raster` App/DI/input/snapshot/lifecycle; `--raw-hud-window --validation` **90 presented frames/90 compositions/12 UI draws/2 sessions/3 swapchains/6 readbacks**; `--window --validation` **30 frames/2 swapchains**, resize/minimize/restore. Vulkan validation0/0 включая teardown.
- Release publish exit0; bundled native DLL загружена из publish/truemoon-native/win-x64. Native asset/ABI не пересобирались. Scoped tracked git diff --check и whitespace check всех изменённых untracked UI files прошли; полный diff не используется из-за прежнего missing blob индекса.

Логи/TRX: `TestResults/AlloyOpenGL/{build,solution-build,all-ui-tests,opengl,opengl-window,opengl-failure,backend-compare-validation,raw-hud-validation,raster,vulkan-window-validation,publish,published-opengl,published-opengl-window,published-opengl-failure,published-backend-compare-validation}.log`. Артефакты локальные/игнорируются Git. Первые оконные probes выявили release-after-context-destruction и managed exception unwind через GLFW refresh callback; обе причины исправлены и финальные сценарии выше прошли. Текущие успешные результаты отделены от первых неудачных запусков.

Проверено Windows x64 desktop OpenGL3.3 NVIDIA RTX5070Ti/driver616.92 и bundled Skia4.153.1. GLES/ANGLE/другие GL drivers, cross-context texture handoff, async GPU exchange/context loss не добавлены. GL GetError не является полноценным GL debug/driver memory profiling. Raw Vulkan остаётся последовательным WaitIdle на одной graphics queue; maintenance1 legacy shutdown/EXT-only/hardware/failure/VRAM ограничения сохраняются.

Предыдущие проверки: raster filtered24/0/0, PNG inspection и discovery24 — TestResults/AlloyRaster; raw host3 devices/54readbacks — AlloyRawHost; retirement KHR/forced legacy200sessions/2000frames/400swapchains/10minimize — AlloyRetirement. Эти full-load/discovery/PNG сценарии не повторялись в текущем OpenGL шаге. Full solution tests, NuGet-consumer, длительный soak и device-loss не выполнялись.

В историческом полном test run 2026-09-27 `TrueMoon.Tests.AppTests.AppCreate` падал из-за IAppLifetime/Cobalt; общий DI не исправлялся. Пустой Mithril.Tests остаётся отдельным вопросом. Старый missing blob/Enerite AD устранён при подготовке общего коммита2026-10-07 обычным согласованием индекса с текущими файлами; подробности ниже.

## Как читать статусы

- **Написано** — реализация найдена и прочитана; успешная текущая проверка поведения этим не утверждается.
- **Проверено** — указанный сценарий фактически прошёл; это не распространяется на остальные требования этапа.
- **Частично** — есть часть реализации, но не весь требуемый сценарий.
- **Не реализовано** — соответствующей рабочей реализации в проверенных UI-модулях нет.
- **Заблокировано** — текущая проверка не может дойти до выполнения из-за указанной ошибки.
- **Исторический результат** — сообщение из предоставленного лога, без подтверждения повторным запуском в этой сессии.

## Этап 1: графический стек и встраивание — частично

Функциональный графический прототип реализован; датированные проверки2026-10-04–08 приведены выше. Общая Settings-сцена на трёх backend/HUD проверялась2026-10-07 и в regression2026-10-08. Таблицы ниже — сводка по этим записям после статической сверки2026-10-09, а не новый успешный запуск. Последняя записанная UI suite —451/451 от2026-10-08.

| Пункт плана | Состояние и доказательства | Что остаётся |
| --- | --- | --- |
| 1.1. Версии пакетов | Silk.NET 2.23.0, SkiaSharp/NativeAssets.Win32 4.153.1: build/publish/runtime/оконный HUD прошли в подэтапах 2026-10-04/05. Bundled DLL имеет pinned revisions/hash; SkiaNativeLibrary публичный. | CI/release automation и итоговый NuGet-consumer; native поставка Windows x64. |
| 1.2. Жизненный цикл | Vulkan window/lease/retirement реализованы; raw HUD90, standalone30 и backend comparison3 прошли в подэтапах 2026-10-04/05. Raw-host 3 devices/54 readbacks и 200-session soak — их отдельные предыдущие запуски. CPU resize/suspend/restore/snapshot lifetime проверены 24 raster cases, повторно в UI suite 110/110 от 2026-10-06. | Итоговый lifetime/failure/device-loss проход; EXT-only/другие GPU и driver memory profiling по рискам. |
| 1.3. Сцена с текстом и панелью | Retained Argentis HUD и независимая Vulkan-сцена теперь композитятся и выводятся в окно; 6 scene readbacks подтверждают фон, alpha, кнопку и текст. | Расширение демонстрации и длительные сценарии. |
| 1.4. GPU-текстура HUD | UiSession отдаёт lease; scene/HUD pipelines используют sampling и premultiplied blending, после completion возвращают ShaderReadOnlyOptimal. Scene Output передаётся PresentImage без CPU-копии; readback только для assertions. | Async handoff/frames-in-flight за пределами текущего последовательного пути. |
| 1.5. Владение | UI owns tree/Skia; raw descriptor не owns handles/feature memory. CPU snapshot после App/session Dispose и registry count 0 проверялись raster tests; raw HUD сохраняет host device по GPU probes 2026-10-04/05. Raw devices/soak — отдельные предыдущие проверки. | Итоговый внешний consumer и lifetime проход; конкретный сторонний engine и allocations вне counters по рискам. |
| 1.6. Синхронизация | GPU sequential lease/layout callback; comparison/raw HUD/standalone прошли validation 0/0 в подэтапах 2026-10-04/05. CPU snapshot независим и не требует Return/borrow блокировки. Maintenance1/legacy retirement проверены в тех отдельных запусках. | Итоговый supported-path validation и device loss; legacy shutdown limitation сохраняется, EXT-only hardware по рискам, async handoff/разные queues вне последовательного режима. |

Файл [Compatibility/SkiaVulkanViewPresenter.cs](../../TrueMoon.Alloy.Hosting/Compatibility/SkiaVulkanViewPresenter.cs) целиком закомментирован и не является рабочим Vulkan-рендерером.

## Этап 2: независимые слои — частично

| Пункт плана | Состояние и доказательства | Что остаётся |
| --- | --- | --- |
| 2.1. Зависимости | **2.1a/b реализованы и проверены 2026-10-05:** Alloy→Argentis/BCL; renderer→Alloy + Skia/Vulkan bindings, без Platform.Silk/Hosting/Core/Contracts/Windowing; Platform→Alloy + Silk, без Skia; Hosting→Alloy/renderer/platform/Core/Contracts + Silk/Skia/Topten.31legacy-файл в Hosting.Compatibility. Build, boundary tests и core-only/renderer-only consumers прошли тогда; boundary tests повторно прошли в UI suite 110/110 от 2026-10-06. | Функциональный 2.1 закрыт; финальный внешний/package consumer ещё впереди. Compatibility — переходный API, не исправление legacy2.3/2.4. |
| 2.2. Контракты | Общие UiSession/IRenderBackend/IWindowHost используются Vulkan, OpenGL и raster. Explicit UseSkiaOpenGL работает для внешнего host и standalone Silk; один view прошёл все3 backend. | Legacy IGraphicsPlatform/no-options UsePresentation изолированы в opt-in Hosting.Compatibility; async GPU handoff отдельным контрактом. |
| 2.3. Дерево и время жизни | Retained ownership Remove/SetContent/transfer и property/collection/conditional attachment lifetime реализованы; rollback/reattach/transfer/stale/cleanup cases имеют датированные проверки2026-10-05–08. Последняя записанная UI suite451/451 —2026-10-08. | Long lifecycle stress; Legacy Visual.GetEnumerator в Hosting.Compatibility остаётся отдельной проверкой/исправлением. |
| 2.4. Диагностика | **Контракт реализован:** UiRenderingException/RenderingFailed/ReportRenderingFailure, terminal stop, cleanup и новая сессия из прежней модели. Controlled failure probe raster/GL/Vulkan и hosting прошёл2026-10-07; regression после reuse2026-10-08. | Реальная потеря device/context и teardown после неё не индуцировались; controlled BackendFailure не закрывает полный K8. |
| 2.5. OpenGL и raster | Миграция реализована: SkiaOpenGLRenderBackend/OpenGLUiTarget, shared drawing/text adapter, external/standalone Hosting и CPU raster. Shared Settings3scales×3backend×40steps проверялся2026-10-07, regression2026-10-08. | Fixed-font/full golden и финальная поддержанная конфигурация. Другие драйверы/GLES/ANGLE — по рискам; старый compatibility window/lifetime отдельно. |

В [TrueMoon.slnx](../../TrueMoon.slnx) зарегистрированы UI-проекты, Hosting и manual probes. Последняя записанная non-incremental solution build2026-10-08:0errors/1341warnings; UI451/451. Последний полный запуск тестов решения —2026-09-27, его результат не заменяется UI suite. Новый build/test run в сверке2026-10-09 не выполнялся.

## Этап 3: дерево, layout и динамический контент — частично

| Пункт плана | Состояние и доказательства | Что остаётся |
| --- | --- | --- |
| 3.1. Свойства | UiProperty/invalidation, property/collection bindings и conditional replacement реализованы. Shared Settings script и static no-redraw проверены2026-10-07; baseline/profile2026-10-07/08 подтверждают static0 layout/draw/Update allocations. | Сохранить эти инварианты в итоговом alpha проходе. |
| 3.2. Layout | Measure/Arrange/min/max/margin/padding реализованы; shared Settings сравнивал все logical bounds при scale1/1.5/2 на raster/GL/Vulkan2026-10-07. K9 baseline/profile выполнены2026-10-07/08. Layout invalidation обходит root. | Реальная native DPI-модель и monitor transitions, fixed-font fixtures. Оптимизация ветвей — после нового profile по необходимости. |
| 3.3. Контейнеры | Panel/Border/VStack/HStack/ScrollViewer/ContentControl используются общей Settings-сценой; scrolling, clipping, replacement и retained state проверены interaction suite/shared script2026-10-07. | Итоговые font/DPI/long lifecycle проверки. |
| 3.4. Динамическое дерево | Ownership Remove/SetContent/transfer реализован2026-10-05; generated list/conditional regions и failure/lifetime semantics реализованы2026-10-07. Общая сцена сохраняет неизменённые controls/focus/selection/scroll, HUD пересоздаёт UI из прежней модели. | Long lifecycle/concurrent stress и итоговая поставка. |
| 3.5a. Односторонние свойства | **Завершён в заявленных границах API:** Bind/BindText к INotifyPropertyChanged; initial sync, detach/reattach/Move/transfer, unsubscribe, stale callbacks и failure/retry проверены 24 cases 2026-10-06. Прямое instance property; один binding на UiProperty. | Nested/computed tracking и Unbind вне этого подэтапа; весь 3.5 не завершён. |
| 3.5b. Двустороннее редактирование | **Реализовано в контрактных границах2026-10-06:** BindTwoWay TextBox/CheckBox/Slider. Editing/normalization/recovery используются общей Settings-сценой и проверялись2026-10-07/08. | Long concurrent stress и итоговые alpha проверки; conversions/commit-on-focus-loss вне подэтапа. |
| 3.5c. Наблюдаемые коллекции | **Реализовано и проверено2026-10-07:** BindItems/RefreshItems, mutable/read-only source, unique reference identity, snapshot coalescing и Reset reuse, attachment dispatch, batch commit/ownership/factory/notification cleanup. Focused41/41, UI229/229, Debug/published form/list и factory-failure probes прошли. | Keys/повторяющиеся occurrences/Unbind вне подэтапа; long concurrent stress и итоговые alpha interactions ещё предстоят. |
| 3.5d. Условные области | **Реализовано и проверено2026-10-07:** LoadStatusRegion на Bind/SetContent, loading/error/ready/retry, staged factory/old cleanup/postcommit errors, coalescing/equality и attachment lifetime. Focused35/35, UI264/264, Debug/published regions smoke прошли. | Реальные async loading/cancellation вне подэтапа; BindContent не понадобился. Итоговые alpha interactions остаются. |
| 3.6. Потоки | UiSession/Post, hosted UI thread, binding getter/write-back, collection snapshot/factory и conditional preparation работают на owner thread. Background/stale/reattach/transfer/cleanup guards проверялись в датированных UI suites2026-10-06–08. | Long concurrent/cancellation/race stress; source serialization и прямой Post остаются ответственностью caller. |

## Этап 4: ввод, контролы и оформление — частично

| Пункт плана | Состояние и доказательства | Что остаётся |
| --- | --- | --- |
| 4.1. Набор контролов | Заявленные controls, packaged Image и VolumeMeter используются общей Settings-сценой. Headless interactions и script на raster/GL/Vulkan/HUD проверялись2026-10-07/08. | Итоговые fixed-font/DPI/поставка проверки. |
| 4.2. Ввод и фокус | Gestures/cancel/disabled/Tab/scrolling и сохранение состояния реализованы. Shared script2026-10-07 и real window input/Snap2026-10-08 имеют отдельные датированные результаты. | Native DPI/monitor transitions, settings off/Aero Shake, physical release-outside/cancel прозрачных областей. |
| 4.3. TextBox | Win32 clipboard Get/Set с owner-thread/lifetime/error recovery реализован; **positive OS fixture smoke Debug/Release прошёл2026-10-07**, блокер positive checks снят. Unicode/graphemes, bindings и controlled failures проверялись отдельно. | Причина прежнего OpenClipboard/error5 не установлена; real OS отказ записи/recovery не индуцирован. IME/font/итоговые OS проверки отдельно. |
| 4.4. HUD-ввод | UiInputResult сообщает Handled/PointerCaptured/KeyboardFocused. Общая Settings-сцена HUD проверила outside passthrough, scene capture, focus, динамическое дерево и routing после пересоздания UI2026-10-07; regression2026-10-08. | Дополнительные edge cases и game-specific policy; итоговый alpha проход. |
| 4.5. Тема и состояния | Typed spacing/palette/density/disabled и theme precedence реализованы. Общий Settings script сравнил model/UI state и selected pixels на трёх backend/HUD2026-10-07; regression2026-10-08. | Fixed-font/full golden и итоговые DPI/OS проверки. |
| 4.6. Приоритеты | **Реализовано/проверено:** local → style → theme → default для typography/foreground и typed Padding/Spacing, Clear/layout/equality/validation. Button/editor theme roles и checkbox indicator allowance определены в PLAN. | Дополнительные будущие style properties вводить явно; текущий spacing набор проверен. |
| 4.7. Пользовательские виджеты | LoadStatusRegion и VolumeMeter входят в shared View1 standalone/raster/GL/Vulkan/HUD; dynamic state/drawing/lifecycle проверялись2026-10-07/08. | Итоговые golden/DPI/поставка проверки. |
| 4.8. Статические фабрики | **Реализовано и проверено 2026-10-07:**15 собственных фабрик; focused34/34, mixed input tree и форма. Constructors сохраняются. | Дальнейшие проверки самих контролов; фабрики не добавляют отсутствующее поведение Line. |
| 4.9. Прозрачность/custom frame/Snap | Production4.9b/c/d и native Windows DPI contract реализованы. Текущий2026-10-09:HWND DPI144/UiScale1.5, Debug/Release DPI/chrome/Snap/desktop alpha/real input150% прошли, hooks0/validation0. | Physical100/200% и разные DPI мониторов, settings off/Aero Shake, physical cancel/release outside, callback fault injection, long lifecycle и отдельный повтор старых opacity/legacy capture failures. Полный4.9 не закрыт. |

## Этап 5: демонстрации и alpha — не завершён

| Пункт плана | Состояние |
| --- | --- |
| 5.1. Базовый запуск | AlloyTest запускает shared Settings View1 через explicit Hosting/OpenGL или Vulkan/Silk. Форма/список/regions/themes/image/widget проверялись в Debug/published Release2026-10-07/08. |
| 5.1a. Форма и фабрики | **Реализовано и проверено в bounded наборе 2026-10-07:** editing/summary/retained focus/selection/disposal; исправлен window lifetime, UI188/188, по10 Debug/published Release form smoke с отладочной кучей и по6 lifecycle сценариев. Исходный0xC0000374 отдельно не перехвачен; long soak/alpha впереди. |
| 5.1b. Динамический список и области | **Завершено в заявленных границах2026-10-07:** editable rows Add/Remove/Replace/Move/Reset, loading/error/ready/retry, retained neighbor focus/selection/scroll и generated-node cleanup. Headless41 collection +35 conditional cases, UI264/264 и по3 Debug/published smoke прошли. Real async loading не входит в сцену. |
| 5.1c. Полные взаимодействия | Функциональная сцена реализована: controls/input/scroll/themes/image/widget, native clipboard fixture и shared script raster/GL/Vulkan/HUD проверялись2026-10-07. Regression2026-10-08 пройден в ограниченном наборе. Fixed fonts/full golden и итоговые OS/DPI проверки открыты. |
| 5.2. Vulkan HUD | `--settings-hud` использует ту же Settings View1/model/40-step script:2026-10-07 Debug/Release90 compositions/36UI draws/2sessions/5readbacks. Passthrough/alpha/resize/borrowed host/registry0 проверены, regression2026-10-08. |
| 5.3. Обновление по необходимости | UiSession.Update пропускает неизменённый UI. Общий HUD2026-10-07 —36UI draws/90compositions; static baseline/profile2026-10-07/08 —0Measure/Arrange/Draw/Update allocations и stable resources. Presentation/composition учитываются отдельно, GPU путь последовательный. |
| 5.4. Документация | Есть raster/Vulkan runtime/Hosting ownership examples, CPU PNG export, shared-view comparison, native rebuild и shader/window инструкции. |
| 5.5. Выпуск | Критерии alpha не пройдены. Общая версия `0.2.2.7-alpha` в Directory.Build.props не доказывает готовность этой UI-версии. |

## Проверки готовности

| Критерий | Состояние на дату сверки |
| --- | --- |
| K1. Ядро без GPU | Текущая UI suite **2026-10-09:463passed/0failed/0skipped**, focused DPI12/12. Реализация/тесты включают clipboard/rendering failure/drawing resources/window/DPI contracts. CPU raster/PNG требуют native Skia; это не полный solution test suite. |
| K2. Raster и GPU | Shared View1 повторно проверен2026-10-09 Debug/Release на raster/GL/Vulkan:3scales×40steps, logical bounds/states и selected RGBA samples с допуском1. **Fixed-font assets/full golden fixtures отсутствуют**; smoke их не заменяет. |
| K3. Windows и DPI | Native DPI contract реализован2026-10-09:HWND DPI144/UiScale1.5, logical resize/input/min-max, minimize/zero-size/restore и Debug/Release alpha/Snap/physical input проверены. Pure/directed100/150/200% и headless Scale-only state tests прошли. Реальные100/200%/разные DPI мониторов недоступны в текущем окружении, settings off/Aero Shake и часть physical cancel проверок впереди. |
| K4. Одинаковое поведение бекендов | Общие runtime/drawing/text и тот же Settings script raster/GL/Vulkan сравнили bounds/model/input/selected pixels в Debug/Release2026-10-09. Полное сравнение текста с доставляемыми fixed fonts ещё не выполнено. |
| K5. HUD-ввод и ресурсы | Shared Settings HUD2026-10-09 Debug/Release:90compositions/36draws/2sessions/5readbacks, outside passthrough/capture/focus/alpha/dynamic content, resize/minimize/restore, новая UI-сессия из модели и host-owned device проверены при native Scale1.5. Итоговый supported configuration проход остаётся. |
| K6. Vulkan validation | Текущие DPI/window/Snap/alpha/shared Settings/HUD/controlled failure/standalone/physical input probes2026-10-09 Debug/Release сообщили0errors/0warnings, включая teardown. Другие physical DPI/OS/GPU configurations и итоговый supported configuration проход остаются. |
| K7. Повторные сессии | Последний записанный short retirement после reuse2026-10-08:KHR/legacy каждый5sessions/50frames/10swapchains, counters/subscriptions/roots0. Текущие window/DPI probes2026-10-09 проверили новые hooks0. Long200sessions2026-10-04 — исторический run; новые window hooks требуют длительного lifecycle/resize soak, VRAM/driver profiling отдельно. |
| K8. Потеря устройства | **Контракт и controlled failure/recreate повторно проверены2026-10-09 Debug/Release**: error→host, terminal stop, cleanup, новая session/tree из прежней модели на здоровом device. **Physical device/context loss**, accepted DeviceLost abandonment и реальный lost-device teardown не индуцировались; полный K8 не закрыт. |
| K9. Производительность | **Baseline, allocation/CPU profile и одна подтверждённая reuse оптимизация выполнены2026-10-07/08.** Shared Settings static/property/list; cached paint/FIFO16fonts, до/после24000rawframes. Static0layout/draw/Update allocations. Managed Update allocations уменьшились~120→46KB/property и~140→66KB/list. Native/VRAM/GPU timestamps не измерены; performance budget не согласован. |

Итоговый alpha проход **не выполнен**: fixed-font/full golden, native DPI/monitor transitions и оставшаяся4.9matrix, long lifecycle/VRAM, real device/context loss, final correctness/measurements, отдельный/package consumer/native delivery. Требования — [PLAN](PLAN.md#воспроизводимый-итоговый-проход-alpha). Сверка2026-10-09 обновляет сводку, не превращает предыдущие results в новый успешный запуск.

## Выполненная проверка 2026-09-27

Рабочий каталог команд — `src`. Windows, фактический SDK `10.0.401`; [global.json](../../global.json) задаёт `10.0.100` с `rollForward: latestMinor`. Проект тестов использует xUnit `2.9.3`, Microsoft.NET.Test.Sdk `18.10.1`, xunit.runner.visualstudio `4.0.0`; режим запуска — VSTest.

Первичная сверка до исправления остановилась на `CS0030` в `ElementList.Add1<TElement>`. В продолжении исправлено преобразование generic-параметра через интерфейс `IElement` к фактическому базовому классу `Element`; публичные сигнатуры не менялись. Затем выполнены следующие проверки.

Отдельный проект Alloy:

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --verbosity quiet /clp:ErrorsOnly --logger 'trx;LogFileName=alloy-resume.trx' --results-directory TestResults/AlloyResume
```

Код **0**, **Passed: 29, Failed: 0, Skipped: 0**. TRX: `src/TestResults/AlloyResume/alloy-resume.trx` (локальный артефакт, игнорируется Git).

Регистрация и проверка точного решения:

```powershell
dotnet sln E:\source\my\TrueMoon\src\TrueMoon.slnx add TrueMoon.Alloy.Platform.Silk/TrueMoon.Alloy.Platform.Silk.csproj TrueMoon.Alloy.Rendering.Skia/TrueMoon.Alloy.Rendering.Skia.csproj TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj
dotnet sln E:\source\my\TrueMoon\src\TrueMoon.slnx list
dotnet test E:\source\my\TrueMoon\src\TrueMoon.slnx --verbosity quiet /clp:ErrorsOnly --logger trx --results-directory TestResults/AlloyResume/Solution
```

Все четыре записи присутствуют в листинге. Запуск решения обнаружил Alloy.Tests: **Passed: 29, Failed: 0, Skipped: 0**. По всему решению суммарно **Passed: 51, Failed: 1, Skipped: 0**; в Mithril.Tests тесты не обнаружены. Общий код завершения **1**, поэтому решение целиком не отмечено зелёным.

Единственное падение — `TrueMoon.Tests.AppTests.AppCreate`: `TrueMoon.Cobalt.ServiceResolvingException<IAppLifetime>`, сообщение `Could not resolve service for type: TrueMoon.IAppLifetime`; стек ведёт через сгенерированный `LifeTimeExecutorResolver` к `DefaultApp.StartAsync`. Эта проблема находится в общем DI/запуске приложения и требует отдельного разбора. TRX-файлы сохранены в `src/TestResults/AlloyResume/Solution`.

GPU-прототип из текущих исходников, после его расширения:

```powershell
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --no-restore --verbosity quiet
```

Код **0**. Вывод: `Vulkan / Skia smoke passed: 12 GPU compositions, premultiplied alpha, text, surface recreation and 2 Skia sessions on one host-owned device verified.`

На первом запуске расширенного smoke сравнение полностью прозрачного пикселя учитывало RGB, что некорректно для premultiplied alpha (`SKColors.Transparent` — прозрачный белый, readback дал прозрачный чёрный). Проверка исправлена на alpha == 0; alpha blending цветной панели проверяется отдельно с погрешностью один уровень канала. Финальный запуск выше прошёл.

Инспекция фактической managed-сборки из `bin/Debug/net10.0` также завершилась с кодом 0:

```powershell
pwsh -NoProfile -File ManualTests/AlloyVulkanTest/InspectVulkanApi.ps1
```

Assembly version: `4.152.0.0`, NuGet version: `4.152.1`. Нативная PE-таблица exports также прочитана; результат и ограничения проверок записаны в [VULKAN-INTEROP.md](VULKAN-INTEROP.md).

Дополнительно замечены предупреждения restore:

- `NU1506`: повторяющиеся версии Microsoft.CodeAnalysis.Analyzers (`5.9.0`/`3.3.2`) и Microsoft.CodeAnalysis.CSharp (`5.9.0`/`4.0.1`) в Directory.Packages.props.
- `NU1507`: несколько источников NuGet без package source mapping в текущем окружении.

Они не помешали текущему запуску UI-тестов и GPU smoke; настройки пакетов не менялись.

## Следующий конкретный шаг

Актуализировано **2026-10-09** после реализации native Windows DPI и текущих build/tests/GPU/publish. Native DPI144 теперь соответствует UiScale1.5, текущая регрессия32/32 и physical input Debug/Release150% прошли; alpha не завершён.

1. **4.9d — физические100/200% и разные DPI мониторов:** native DPI/Scale/input/min-max contract реализован и проверен на доступном150%. Следующий проход — подготовленное окружение100/150/200%, туда/обратно между разными DPI, оба backend ×3modes ×standard/custom, logical size/focus/selection/capture/regions/input/desktop alpha/minimize/restore/cleanup. Directed96/144/192 не заменяет physical переход. Затем settings off/Aero Shake и физические release-outside/cancel для прозрачных областей; пользовательские настройки не менять. Alt+Space без PowerToys interception остаётся отдельной physical проверкой. Старые4.9b opacity/legacy capture failures требуют отдельного повторения и root-cause анализа; текущий успех не закрывает их. Прежний CPU layered-window prototype заменён production GPU/DWM adapter в4.9a.1/b; полнота4.9 ещё открыта.
2. **Нерегулярный standalone Vulkan smoke:** при повторе Name binding mismatch фиксировать actual model/editor/stage, call order и native input; retry/final success не является root-cause fix. До итогового alpha учесть воспроизводимость сценария. Текущую ограниченную reuse оптимизацию не расширять text blobs/dirty-subtree без нового profile.
3. **K2/K3/K7/реальный K8:** fixed fonts/full golden, физические DPI transitions, long lifecycle/resize soak и отдельный реальный device/context loss. Controlled failure/recreation и short5soak не закрывают эти требования; counters не заменяют driver/VRAM profiling.
4. **Итоговый K1–K9 /5.4–5.5:** финальная конфигурация/correctness/publish, отдельный/package consumer/native assets, документация/alpha migration; сохранять binding/collection/tree ownership. Legacy Visual.GetEnumerator и historical AppCreate/Mithril.Tests учитывать отдельно; текущие UI463/463 не объявлять результатом полного solution suite.

## Фактическая сверка планов 2026-10-04

Прочитаны PLAN/STATUS, четыре UI csproj, Runtime контракты, места Silk Vulkan overloads и старый запуск AlloyTest. Сопоставлены найденные callers в Hosting/manual probes; source search excludes bin/obj/TestResults. План 2.5 соответствует реализации; цель2.1 ещё не достигнута. Уточнено размещение renderer/platform bridge в Hosting, чтобы сохранить независимость обеих интеграций и избежать cycle; новые проекты для этого шага не запланированы. Основные цели, Windows/net10.0/xUnit и K1–K9 не изменены.

Текущие проверки ограничены чтением исходников/ссылок и scoped diff --check документации. Код, пакеты, native DLL и Git index не менялись; build, unit/GPU/publish не запускались. Их предыдущие результаты сохранены отдельно выше. Блокер начала 2.1a не выявлен; legacy API migration и dependency cleanup являются предстоящей работой, device-loss/полный suite/driver profiling — отдельными оставшимися ограничениями.

## Состояние рабочего дерева, не относящееся к UI-плану

До сверки в Git уже присутствовали четыре записи `AD` для проектов с именем `TrueMoon.Enerite*`; рабочее решение использует `TrueMoon.Enerit*`. Ранее чтение staged diff завершилось сообщением `unable to read 4cbcfe0b22416771b0356eb9e669c7252ad0bcee`. Эти записи не доказывают наличие задачи переименования и не изменялись в этой сессии. Диагностику индекса при необходимости вести отдельно от UI-работы.

Подготовка общего коммита2026-10-07 по прямому запросу пользователя включить все изменения/untracked: проверены157 status paths, все относятся к документированной UI-работе; у31 legacy deletion есть Compatibility destination. Четыре Enerite AD отсутствуют и в HEAD, и на диске: при staging явно проверенных путей их незавершённые добавления убраны из индекса, существующие Enerit проекты не менялись. Полный staged diff теперь читается, `git fsck --connectivity-only --no-dangling` прошёл exit0. Снимки первоначальных status/index сохранены в ignored TestResults/AlloyCommit. HostedUiWindow, native asset со совпадающим SHA256 и shaders включены; ignored build/log/dump файлы не добавлялись. Убран один лишний пустой EOF в NativeInterop README для полного diff --check; новые build/tests в рамках подготовки Git не запускались, результаты реализации выше остаются датированными.

## Журнал обновлений

- **2026-10-07, сверка по запросу пользователя:** работа соответствует согласованному порядку PLAN:3.5a/b/c/d,4.8,5.1a/b завершены в границах подэтапов;5.1c functional standalone реализован/проверен, nativeOSclipboard остаётся блокером. Следом sharedscene5.2–5.3, error/recreate2.4/K8, затем финальныеK1–K9/5.4–5.5. Старые BackendHudView/Vulkan/HUD results не перенесены на новую View1; новые цели/архитектура/net10/xUnit не менялись.

- **2026-10-07, функциональная часть5.1c /4.1–4.7:** typed Padding/Spacing и Theme tokens/roles, local/style/theme priority, disabled visuals; ChildClipBounds+BringIntoView/nested focus, primary-only Slider gesture/focus clearing. AlloyTest light/dark/compact/disabled,image,VolumeMeter, keyboard/wheel/static lifecycle smoke. Focused78/78, UI342/342, solutionbuild0errors/1339warnings, Releasepublish0; по3 Debug/published smoke40–41 и collection failure probes passed. **FinalOSclipboard probes failed в обеих сборках:** GLFW AccessDenied, host PowerShell read succeeded; root cause не установлен,5.1c не закрыт полностью. Review/static pseudo-mutation и narrow pairing выполнены; coverage/mutationrunner/fullsolution/GPUmatrix/longsoak не запускались. Next — clipboarddiagnostics, затем sharedscene/HUD.

- **2026-10-07, условные области3.5d / завершение5.1b:** LoadStatusRegion на Bind/SetContent, immutable phase/message, loading/error/ready/retry, staged replacement/cleanup, displayed/desired recovery, coalescing/equality/attachment lifetime. Focused35/35, UI264/264, solution build0errors/1339warnings, Release publish exit0; по3 последовательных Debug/published form/list/regions smoke и оба collection failure probes прошли. Factory errors условной области проверены headless на Compile-linked sources; новый library API/пакеты/native/Hosting code/index не менялись. Реальный async loader и итоговая alpha вне подэтапа. Next —5.1c полные interactions/scroll/themes в той же сцене.

- **2026-10-07, коллекции3.5c после93a5cae:** пользователь согласовал proposal, контракт перенесён в PLAN. BindItems/RefreshItems, mutable/read-only sources, unique ReferenceEquals/null/duplicate policy, Reset reuse/coalescing, deferred initial sync, attachment-scoped dispatcher, batch commit и factory/notification cleanup реализованы. AlloyTest дополнен editable rows/Add/Remove/Replace/Move/Reset. Focused41/41, UI229/229, solution build0errors/1339warnings, Release publish exit0; по3 последовательных Debug/published form/list smoke (30–31 frames) и по2 initial/update factory failures прошли, registry/subscriptions0/drafts+tree disposed once. Assertion/pseudo-mutation review выполнен, actual mutation/coverage/full solution/GPU matrix/long soak не запускались. Packages/native/backend/Hosting/Platform/index не менялись. Next —3.5d условные области в той же demo; полный5.1b/alpha не завершены.

- **2026-10-07, диагностика и window lifetime:** два pre-fix дампа показали GLFW input cleanup после window Reset; новый --window-lifetime failed до правки. SilkWindowHost теперь сохраняет окно после Run, Dispose освобождает input до окна через try/finally. UI188/188, solution build0errors/1339warnings, оба Release publish exit0; по10 Debug/published Release form smoke с отладочной кучей, по6 lifecycle cases,3 dotnet-run и direct Release прошли. По3 OpenGL/window/failure/Vulkan сценария Debug/Release прошли, validation0/0. Ранние callback-unwind/focus-interference probe failures не засчитаны. Исходный0xC0000374 отдельно не перехвачен; native DLL/packages/runtime/bindings/index/config/user breakpoints не менялись. Next —3.5c контракт/реализация, затем dynamic demo5.1b; не alpha готовность.

- **2026-10-07, фабрики4.8 / форма5.1a:**15 static factories с конкретными типами, public constructors, SettingsModel/View1 с BindTwoWay/Summary и preset/reset, mixed fluent markup. Focused34/34, UI188/188, non-incremental solution build0errors/1339warnings, publish exit0. Первый Debug30/direct Debug31/published Release30 smoke passed; повторный Debug native0xC0000374 без стека, причина не установлена. Настройки/debugger/native/packages/index не менялись. PLAN/README/STATUS различают API и частичную оконную проверку. Next — native stack/reproduction, затем collections/условные области; не alpha готовность.

- **2026-10-06, двусторонние bindings 3.5b:** BindTwoWay в общем PropertyBinding, exact readable/writable selector, immediate UI write/canonical readback, error recovery и echo/reentrancy guards реализованы для TextBox/CheckBox/Slider. Element best-effort property notifications + invariant hook, TextBox grapheme clamp и Slider failed drag capture cleanup. Fresh focused 44/44, UI suite 154/154, non-incremental solution build 0 errors/1341 warnings. Broad test review выявил избыточное подавление source events во время sync; guard сужен до собственного setter, два race/reentrancy cases подтвердили независимые события. PLAN/Hosting README обновлены; GPU/manual/window/publish/full solution suite/coverage не запускались, native/packages/index не менялись. Next — factories 4.8 и первая форма AlloyTest 5.1a.

- **2026-10-06, согласованная корректировка плана:** применены принятые пользователем замечания: раздельные 3.5a/b/c/d и 5.1a/b/c, общий two-way TextBox/CheckBox/Slider с immediate write-back, ранние фабрики/форма до collections, условные области через SetContent, конкретный итоговый alpha проход и measurements до layout оптимизаций. Архитектура/net10/xUnit/ownership/K1–K9 сохранены. Исправлены устаревшие summary; результаты code runs 2026-10-05/06 остались датированными. Только PLAN/STATUS: code/build/tests/GPU/publish/packages/native/index не менялись/не запускались. Следующий шаг кода — контракт и реализация 3.5b; будущие API и alpha готовность не объявлены.

- **2026-10-06, односторонние bindings:** Argentis Bind/BindText + lifetime registration/attachment connection, UI-thread getter/Set, direct selector validation и duplicate guard реализованы. Focused 24/24, вся UI suite 110/110, non-incremental solution build 0 errors/1341 warnings. Проверены detach/reattach/Move, different-thread transfer, stale queued/in-flight notifications, detach-before-unsubscribe gap, one-way TextBox без feedback и getter/validation retry. PLAN/Hosting README фиксируют ограничения; фабрики 4.8 остаются будущими. GPU/manual/publish, полный solution suite и coverage не запускались; packages/native/index не менялись. Next — two-way TextBox, затем observable collections.

- **2026-10-06, согласование фабрик контролов:** по запросу пользователя в PLAN добавлены решение о равноправии конструкторов/статических фабрик и пункт 4.8 с требованиями и будущими проверками; Hosting README получил целевой пример Button.Simple/Text.Plain рядом с new Button. Factory возвращает новый конкретный control; публичные constructors, ownership и backend boundaries сохраняются. STATUS отделяет согласование от реализации и прошлые проверки 2026-10-05 от текущей документационной правки. Код, packages/native DLL и Git index не менялись; build/tests/GPU/publish не запускались. Следующий шаг реализации остаётся typed bindings 3.5/3.6, фабрики включены в controls/demo этап.

- **2026-10-05, ownership после 2.1b:** Element/collections/ContentControl возвращают старое subtree caller без Dispose; прямой child Detach/Dispose требует Remove. OwnAttachment, attachment generation queued Dispatch, UiSession input reference cleanup, TreeChange commit/notifications/reentrancy и best-effort element/session cleanup реализованы. Button сохраняет Click при отклонённом Dispose и очищает при реальном освобождении. 27 новых headless cases passed, вся UI suite 86/86; solution build 0 errors/1341 warnings. Девять probes каждый Debug/published Release passed, validation 0/0; raw HUD 90/90/12/2/3/6, GL window 31/30, Vulkan 30/2; KHR/legacy soak каждый 5 sessions/50 frames/10 swapchains, subscriptions/resources/retained roots 0. PLAN/Hosting README явно фиксируют caller ownership, attachment vs lifetime resources и notification errors после commit. Native/renderer/platform/packages/index не менялись. Полный solution test suite/AlloyTest/NuGet/legacy window/long soak/golden/device-loss/profiling не повторялись. Следующий шаг — typed property binding с attach/detach/reattach, затем two-way и collection bindings; legacy 2.3/alpha не объявлены завершёнными.

- **2026-10-05, разделение2.1b:**31legacy-файл перенесён в Hosting.Compatibility[.Presenters], no-options UsePresentation стал opt-in; Alloy оставлен только Runtime/Argentis/BCL, необходимые Silk/Skia/Topten/Core/Contracts references явно объявлены в Hosting. AlloyTest обновлён на explicitOpenGL/Silk, View1/View2не менялись, добавлен ограниченный --smoke. Breaking alpha migration без typeforwarders описана. Solution build0errors/1341warnings, UI59/59(+3coreboundary), core-only/renderer-only и compatibility-registration consumers прошли. Все12manualGPUсценариев повторены Debug/published, Vulkanvalidation0/0; AlloyTest31/32frames. Retirement только5sessions/50frames/10swapchains каждый KHR/legacy, не long200. Native/runtime/GPU/legacy algorithms и Gitindex не менялись; прежние SkiaGlViewPresenter изменения сохранены. Fullsolutiontests/legacywindow/NuGet/device-loss/golden/profiling не запускались. Следующий шаг —ownership Remove/SetContent/transfer, затемbindings; этапы2.3/2.4/alpha не объявлены завершёнными.

- **2026-10-05, разделение2.1a:** VulkanWindowPresenter/counters перенесены в Hosting, SilkVulkanHost factories заменили renderer Silk overloads/FromSilkDevice/Device. Прямой renderer→Platform reference удалён; explicit Silk.NET.Vulkan2.23.0 сохраняет Vulkan bindings. HostedUiWindow/manual callers/README обновлены, alpha assembly/API migration описана. Solution build0errors/1341warnings, UI56/56 (3 новых boundary tests), отдельный renderer-only raster/raw consumer прошли; все12GPU/manual сценариев прошли Debug и published Release, Vulkan validation0/0. KHR и forced legacy retirement — только5sessions/50frames/10swapchains каждый; long200 не повторялся. Первичный validation запуск без VK_LAYER_PATH был неуспешен, после process-only настройки финальные сценарии прошли. Native/retirement/lifetime алгоритмы и Git index не менялись. Scoped tracked diff --check и whitespace check изменённых UI файлов прошли; полный diff не использовался из-за прежнего missing blob. Следующий шаг —2.1b, не объявление всех этапов alpha завершёнными.

- **2026-10-04, сверка после OpenGL:** статически подтверждены dependency graph и единственный найденный no-options consumer AlloyTest; план2.1 разбит на2.1a (renderer/platform bridge и VulkanWindowPresenter в Hosting, без обратной ссылки) и2.1b (legacy compatibility/Alloy cleanup). Уточнены assembly/API migration и проверки каждого переноса; исправлена устаревшая raster Vulkan-only формулировка. Цели/net10/xUnit/K1–K9 сохранены. Код/native/packages/index не менялись; сборка/tests/GPU/publish не выполнялись. Следующий конкретный шаг — реализовать2.1a.

- **2026-10-04, OpenGL migration:** добавлены borrowed OpenGLUiTarget, SkiaOpenGLRenderBackend/Surface, GPU retained RGBA/Premul framebuffer composition и независимый diagnostic CPU readback; общий UiSession/Skia drawing/text adapter. UseSkiaOpenGL external/standalone, explicit swap, Skia disposal в Closing до уничтожения context. Managed render/input/window command errors перехватываются внутри callbacks и становятся Completion/StopAsync failure после закрытия. Legacy presenter очищен без удаления compatibility API. Debug/Release OpenGL App/GPU3, standalone30 frames resize/minimize/restore, draw failure registry0 и triple backend3scales прошли; GL checks NoError/Vulkan validation0/0. Debug raster/raw HUD90/90/12/2/3/6/Vulkan standalone30/2 прошли, UI xUnit53/53. Solution build --no-incremental0errors/1341warnings; full solution tests не выполнялись. Native/packages/index не менялись. Первые оконные probes выявили lifetime-after-Run и native callback unwind, обе причины исправлены и успешно перепроверены. Next — dependency split2.1, не объявление всех этапов alpha завершёнными.

- **2026-10-04, CPU raster:** добавлены SkiaRasterRenderBackend/RasterUiTarget/SkiaRasterSurface, immutable caller-owned SnapshotRasterImage и UseSkiaRaster. Thread affinity, stale/unrendered export guards, zero viewport, resize/scale change и balanced draw failure/retry реализованы. 24 новых focused raster xUnit cases прошли; вся UI suite 53/53 и solution discovery24. Manual --raster прошёл App/DI/input/invalidation/PNG/lifecycle без window/device; --backend-compare подтвердил unchanged view при scale1/1.5/2, layout/input/selected pixels, validation0/0 в Debug и published Release. Raw HUD90/90/12/2/3/6 и standalone30/2 прошли0/0. Full solution build --no-incremental: 0 errors/1351 warnings; full solution tests не выполнялись. PNG просмотрен; native asset/ABI/packages/index не менялись. Fixed-font golden/OpenGL/driver profiling остаются; следующий шаг — migration legacy OpenGL к общим контрактам.

- **2026-10-04, raw Vulkan host:** добавлен borrowed VulkanHostContext с plain handles, immutable enabled extensions, API version, enabled feature pointers/pNext и host loader/DeviceWaitIdle callbacks. Renderer/interop surface работают через descriptor, Silk overloads сохранены; Dispose устройства хоста не вызывается. Native boundary защищён от callback exceptions; extension enumeration pointers preflight/cache; disposed VulkanDevice guarded. Новые --raw-host и --raw-hud-window прошли Debug и published Release: 3 engine-owned devices/6 hosted +3 direct sessions/54 readbacks и HUD90/90/12/2/3/6, validation 0/0. Existing xUnit29 passed, convenience HUD/runtime/standalone/composition и retirement smoke5 в обоих режимах прошли. Build 0 errors/14 warnings; native ABI/asset/index не менялись. Первый probe был исправлен: Resize идентичного viewport является допустимым no-op, lease guard теперь проверяет изменённый размер. Длительный 200-cycle soak этого шага не запускался. Next — raster backend/детерминированные UI checks, далее миграция legacy OpenGL.

- **2026-10-04, retirement и ресурсный soak:** включение KHR/EXT maintenance1/dependencies/feature в VulkanDevice, present fences per image и retirement всех generation resources после completion proof; fallback по replacement-image reacquisition. Добавлены ownership Resources/ActiveSessionCount и --retirement-soak с publisher Own/WeakReference. KHR и forced legacy: каждый 200 sessions/2000 frames/400 swapchains/10 minimize; peak 2, steady 1/4/3 и 1/4/0, after-dispose counters/subscriptions/roots 0, validation 0/0. HUD90, standalone30, runtime, composition36/18, xUnit29 passed; publish и published KHR smoke5/50/10 passed. Native ABI не менялся; raw handles следующий шаг. Legacy shutdown и driver profiling limitations явно сохранены в PLAN/README.

- **2026-10-04, сквозной оконный HUD и сверка плана:** проверена сборка после переименования Hosting. PresentImage принимает host-owned scene texture и сообщает фактический layout после completion; UiSession Present сохранён. Добавлены --hud-window/--hud-demo, общий retained HUD + scene + swapchain pipeline без CPU-копии для presentation. Текущий HUD: 90 frames/compositions, 12 UI draws, 2 sessions, 3 swapchains, 6 GPU assertions; validation 0/0. Runtime, standalone 30/2, offscreen 36/18 и 29 xUnit прошли; Release publish и published HUD повторили 90/90/12/2/3/6 и validation 0/0. Таблицы статуса актуализированы вместо устаревших отметок отсутствующего runtime/hosting. Цели/архитектура/K1–K9 плана сохранены. Следующий конкретный шаг — retirement swapchain semaphores и ресурсный soak.

- **2026-10-04, имя hosting-модуля:** удалён повторный суффикс TrueMoon из имени проекта, каталога, namespace и ссылок. Новое имя — TrueMoon.Alloy.Hosting. Semantic refactoring IDE успешен; листинг решения подтверждает новый путь. Сборка и тесты после переименования не запускались; предыдущие результаты не засчитываются как проверки этого изменения.

- **2026-10-04, единое подключение UI:** введён UiSession runtime для Argentis, публичный Skia Vulkan renderer/loader/lease и Hosting с UseAlloy/UsePresentation. Добавлены оконный host/presenter, resize/minimize/restore, UI thread и App lifecycle; Hosting включён в решение. Существующие 29 xUnit прошли повторно; новые manual probes проверили external App registration, retained GPU pixels/input/жизненный цикл и standalone 30 кадров/2 swapchains. Vulkan validation 0/0, включая disposal. Повторная внешняя композиция: 36/18, validation 0/0. Publish и запуск опубликованного runtime прошли. Архитектурные уточнения переходного API и ограничение VulkanDevice зафиксированы в PLAN; примеры и native loader описаны в README. Следующий шаг — retained HUD + сцена + presentation в одном сценарии, затем raw handles/soak и завершение разделения legacy/backend.

- **2026-10-04, сверка плана и внешняя композиция:** подтверждена последовательность этапа 1: handoff/native assets уже есть, следующий незакрытый пункт — GPU sampling/композиция 1.4. Добавлен VulkanHostCompositor с host-owned target/render pass/framebuffer, scene/HUD graphics pipelines, image view/sampler/descriptor и повторным использованием command buffer. UI-текстура переводится в ShaderReadOnlyOptimal, premultiplied alpha смешивается на GPU, фактическое состояние возвращается Skia; native DLL не менялась. Shaders GLSL/SPIR-V хранятся в ManualTests/Shaders и встроены в assembly, добавлена инструкция пересборки и --interop-compose. Текущие проверки: build exit 0 (7 NU1507 warnings/0 errors), 3 shaders прошли spirv-val; 36 внешних композиций/18 leases, raw 18 чтений и Skia 12 композиций прошли validation 0 errors/0 warnings после teardown. Publish и published GPU-композиция прошли (36, validation 0/0). Логи: TestResults/VulkanInterop/{composition-build,interop-compose-current-validation,interop-current-validation,smoke-current-validation,composition-publish,composition-published-validation}.log. UI unit tests/всё решение не запускались. Этап 1 ещё частичный: swapchain/оконный resize/presentation, DPI и длительные проверки не реализованы. Архитектура/критерии PLAN.md сохраняются; следующий конкретный шаг — оконный хост.

- **2026-10-04, хранение native asset:** проверены существующие проекты; выбран TrueMoon.Alloy.Rendering.Skia без нового проекта. Проверенная DLL (около 9 MB) и build.json с revisions/ABI/SHA-256 помещены в NativeAssets/win-x64 для хранения в Git. MSBuild копирует DLL в truemoon-native/win-x64 через ProjectReference при build/publish и включает contentFiles с copyToOutput в пакет; штатный asset не перезаписывается. AlloyVulkanTest автоматически загружает bundled DLL на Windows x64 для обоих bindings, сохраняя явный --native-skia. Добавлены [README renderer](../../TrueMoon.Alloy.Rendering.Skia/README.md) и -InstallToRenderer в native build script; реальная повторная сборка/установка прошла (exit 0). Managed build: 0 ошибок/7 предупреждений. ABI и 18 handoffs без явного пути прошли; validation: 0 ошибок/0 предупреждений. Publish прошёл, hash DLL совпал, ABI published-примера прошёл. Pack прошёл, hash внутри nupkg и copyToOutput/flatten metadata проверены; есть NU1507 и NU5100 (native DLL content не является managed reference). Восстановление пакета в отдельном потребителе не проверялось. Логи: TestResults/VulkanInterop/{managed-build-bundled,interop-bundled-validation,publish-bundled,pack-renderer,native-install-renderer}.log. UI-тесты/всё решение не запускались. Решение поставки явно записано в PLAN.md; следующий графический шаг остаётся внешней GPU-композицией.

- **2026-10-04:** установлен MSVC Build Tools/Windows SDK/Spectre toolchain; native extension собран для pinned 4.152.1 и текущей 4.153.1. Добавлены таблица source pins, локальный Ninja и process-only Git longpaths; preflight проверяет официальную совместимость managed/native перед ABI. В VulkanDevice добавлены opt-in validation layer, rooted debug callback, synchronization validation и счётчики результатов после teardown. ABI preflight и 18 внешних чтений прошли; исходный smoke прошёл 12 композиций со штатной DLL и с patched DLL. Оба patched GPU-сценария с validation: 0 ошибок/0 предупреждений. Отрицательные случаи несовместимой DLL и отсутствующего слоя: код 2. PowerShell parser для Apply/Build/SourcePins, повторный native build preflight и scoped git diff --check прошли. Managed build: 0 ошибок, финальный incremental — 7 предупреждений. Логи и команды: [README](../../ManualTests/AlloyVulkanTest/NativeInterop/README.md). Validation DLL/manifest извлечены из подписанного LunarG SDK 1.4.363.0 в локальный каталог; системный Vulkan runtime не менялся. UI-тесты и всё решение не запускались. GPU-композиция внешней сцены, оконный swapchain и длительные проверки остаются. Архитектура/критерии плана не изменены.


- **2026-10-02:** подготовлены pinned C ABI extension, применение/source-build scripts, managed `LibraryImport`/SafeHandle lease и внешний raw Vulkan readback probe. Managed build прошёл (код 0, ошибок 0); исходный GPU smoke повторно прошёл 12 композиций. Проверены PowerShell syntax, применение extension и отсутствие exports в штатной DLL. Native build preflight остановился на отсутствии MSVC; внешний handoff и validation не запускались. UI-тесты/всё решение не запускались. Общий `git diff --check`/`--stat` остановился на прежнем missing blob `4cbcfe0b22416771b0356eb9e669c7252ad0bcee`; scoped `git diff --check` для изменённых Program/STATUS/VULKAN-INTEROP и native insertion прошёл с кодом 0. Индекс не исправлялся. Архитектура/критерии плана не изменены.

- **2026-09-27:** восстановлен и сохранён полный план; прочитаны UI-модули, примеры и существующие тесты; зафиксированы частично написанные этапы и отсутствующие интеграции; запуск тестов остановлен ошибкой CS0030; добавлен указатель в `src/AGENTS.md` для следующих сессий.
- **2026-09-27, продолжение:** исправлена CS0030; подключены четыре UI-проекта к решению; 29 тестов Alloy прошли напрямую и через решение. GPU-пример расширен до 12 композиций с alpha/text/пересозданием поверхностей и повторным использованием устройства хоста; финальный запуск прошёл. Добавлены скрипт инвентаризации API и отчёт об ограничении внешнего Vulkan interop, проверены native exports. Зафиксировано отдельное падение общего AppCreate-теста.

## 4.9 / K7 callback fault boundary и cleanup, 2026-10-10

Параллельная работа отдельно от Enerit: `E:/source/my/TrueMoon-worktrees/alloy/src`, branch `codex/alloy-next`, base origin/main `b15d85f095b6fb1cbe00c4439d11d7bac71e6dc8`. Общие contracts/build/solution/plans не менялись, merge/push не выполнялись. Проход ограничен callback fault handling и bounded teardown/recreation; alpha и физические DPI требования сохраняются.

### Реализация и найденный дефект

Win32 DPI/Chrome/transparent framebuffer subclasses уже сохраняли managed errors в ExceptionDispatchInfo. Однако GLFW mouse handler вызывал `_dpi.PixelScale` до входа в защищённый Hosting Input handler. После native DPI fault этот accessor мог бросить через native callback boundary. Direct SilkWindowHost subscribers Render/Input также не имели собственной защиты.

Добавлен internal WindowCallbackBoundary: Execute возвращает нормально, сохраняет первый exception/stack и подавляет последующие operations. Внутри защищённого Execute перед user callback проверяется VerifyWindowAccess, включая pending Win32 hook fault; поэтому следующий keyboard/text/focus callback после DPI fault тоже не вызывает user code. Все native-origin mouse/wheel/keyboard/text/focus/render handlers подключены через boundary. Render delegate создаётся один раз; input closures остаются, их allocations не измерялись этим проходом. VerifyWindowAccess передаёт ошибку на owner thread вне dispatch; Run выполняет также финальную Verify после завершающего DoEvents. Existing Hosting Completion/StopAsync и UiCleanup best-effort teardown сохраняют исходную причину и cleanup errors. Public API, thread/window/device ownership, renderer/Argentis, net10.0/xUnit/native ABI не изменены.

### Новые проверки

WindowCallbackBoundaryTests — focused xUnit proof, **Passed:6, Failed:0, Skipped:0**. Требования:

| Требование | Evidence |
| --- | --- |
| Successful callbacks и Verify без повторного dispatch | SuccessfulCallbacksContinueAndVerificationDoesNotDispatch |
| Deferred failure без native unwind, original identity/stack | FailureReturnsNormallyThenVerificationPreservesCauseAndStack |
| Последующие user handlers не выполняются и не заменяют причину | LaterCallbacksCannotRunUserCodeOrReplaceFirstCause |
| Reentrant first captured cause | ReentrantFailurePreservesFirstCapturedCause |
| Независимая здоровая новая boundary | FreshBoundaryIsIndependentOfFaultedWindow |
| Pending hook fault до user handler | PendingHookFailureIsCapturedBeforeUserHandlerAndLaterVerification |

Новый `--window-callback-failure` probe посылает directed сообщения только собственным HWND. DPI injection — WM_GETDPISCALEDSIZE с dpi0 и валидным SIZE: настоящий Win32WindowDpi.Dispatch вызывает SetScaledSize→WindowPixelScale error, возвращает normally и сохраняет исключение. Это не физический DPI переход. Mouse/key/text/focus failures — реальные directed Win32→GLFW→Silk handlers; Render case — managed DoRender dispatch, не выдаётся за native WM_PAINT. После первого fault повторяются key/text/focus/render/mouse/DPI callbacks; subscriber counter не меняется, первая причина сохраняется. Dispose дважды освобождает HWND и DPI/chrome/transparency hooks.

Hosted cases — OpenGL/Vulkan×Opaque/Opacity/PerPixel×subscriber/DPI =12. Перед fault сохраняются editor focus/capture, закрытие намеренно бросает cleanup error. Проверены ordered original+cleanup causes, Completion/StopAsync exception identity, root/editor disposal, focus reset, registry0, HWND0 и hooks0. Затем на том же HostedUiSessionFactory запускается новая здоровая window/session с3frames, после закрытия registry/hooks0. Каждый финальный Debug и published Release run: **6direct +12hosted failures +12healthy recreations**, validation **0errors/0warnings** включая teardown. Оба выполнены после последнего semantic change с pending-hook guard.

### Фактически выполненные команды

PowerShell cwd всегда этот worktree src; SDK10.0.401, Windows x64, net10.0, xUnit2.9.3/VSTest. No full solution baseline.

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --filter FullyQualifiedName~WindowCallbackBoundaryTests --no-restore --logger 'trx;LogFileName=boundary.trx' --results-directory TestResults/AlloyCallbacks --verbosity minimal
# Passed:6, Failed:0, Skipped:0, exit0

dotnet build ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --verbosity minimal
# Initial consumer build exit0, 728warnings/0errors (включая один исправленный CS8778 probe warning).
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -c Release --no-restore -o TestResults/AlloyCallbacks/publish --verbosity minimal
# exit0, native/assets доставлены в publish.

$env:VK_LAYER_PATH=(Resolve-Path TestResults/AlloyCallbacks/tools/validation-1.4.363.0/Bin).Path
dotnet run --no-restore --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --window-callback-failure --validation --callback-output TestResults/AlloyCallbacks/callbacks-debug.json
dotnet TestResults/AlloyCallbacks/publish/AlloyVulkanTest.dll --window-callback-failure --validation --callback-output TestResults/AlloyCallbacks/callbacks-release.json
# Each exit0;6direct +12hosted failures +12healthy recreations; registry/HWND/hooks0; validation0/0.

# Existing regression, each Debug and published Release:
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-chrome --validation
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --settings-failure --validation
dotnet TestResults/AlloyCallbacks/publish/AlloyVulkanTest.dll --window-chrome --validation
dotnet TestResults/AlloyCallbacks/publish/AlloyVulkanTest.dll --settings-failure --validation
#4probe runs exit0, actual scenario summaries in logs, validation0/0 including teardown.

dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --logger 'trx;LogFileName=ui-debug.trx' --results-directory TestResults/AlloyCallbacks --verbosity minimal
# Final UI run: Passed:469, Failed:0, Skipped:0, exit0.
```

Первый callback probe attempt без VK_LAYER_PATH завершился exit-532462766 на unavailable VK_LAYER_KHRONOS_validation при первом Vulkan window startup после6direct+6OpenGL successes. Это не passing полный запуск. `callbacks-debug.log` сохраняет попытку; финальные полные logs — callbacks-debug-final.log и callbacks-release.log. Validation tooling1.4.363.0 скопирован из существующей ignored tool dependency в локальный TestResults этого worktree; VK_LAYER_PATH задан только процессу shell/probe, глобальная среда и Windows settings не менялись. SDK/native/binary/tooling SHA256 — provenance.json.

Артефакты ignored `TestResults/AlloyCallbacks`: boundary/ui-debug TRX/logs, build-debug.log, publish-release.log, callback JSON/logs,4existing-regression logs, provenance.json, published consumer, tools/validation copy. Intermediate artifacts не включены в commit; git diff --check проверен отдельно.

### Границы результата и продолжение

Этот проход доказывает bounded error/teardown/recreate на указанной машине. Native window hooks сняты после каждой сессии, но длительный lifecycle/resize soak, WeakReference retention/VRAM profiling не выполнялись. Natural DPI callback fault не заменяет отдельную fault injection Chrome/GDI/RemoveWindowSubclass failure. Synthetic/direct messages не доказывают physical input/settings/Shell Snap, physical100/200%/mixed-monitor DPI или device/context loss. Font/full golden, package/independent consumer и final supported alpha matrix остаются. Приоритет следующего шага4.9d/K3 — подготовленное физическое DPI окружение; доступный ограниченный независимый шаг — долгий lifecycle новых hooks без глобальных settings изменений.

## Интеграция в main — 2026-10-10

Коммит агента `cbae35403fb09487acf1ffed87d34974fd0121dd` объединён через `d56f217a0e918623c8dd4eb2036c813d85c2a5b8`, после merge Enerit `0a65e2e`. Проверяемые исходники: main `d56f217`, cwd `E:/source/my/TrueMoon/src`. Windows x64, Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. При интеграции не менялись UI source/public API, contracts/build/package настройки.

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Debug --logger 'trx;LogFileName=alloy-ui.trx' --results-directory TestResults/AgentMerge --verbosity minimal
dotnet build ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --configuration Debug --verbosity minimal
$env:VK_LAYER_PATH=(Resolve-Path TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin).Path
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-callback-failure --validation --callback-output TestResults/AgentMerge/alloy-callbacks-debug.json
```

Все три команды exit0. UI suite **469passed/0failed/0skipped**, TRX counters прочитаны отдельно. Consumer build **0errors/227warnings**; это локальная incremental Debug сборка, не повтор full solution baseline. Callback probe: **6direct cases +12hosted failures +12healthy recreations**, OpenGL/Vulkan×3appearance×subscriber/DPI. Проверены original cause/stack, ordered cleanup error, Completion/StopAsync identity, disposed tree/focus, registry/HWND и DPI/chrome/transparency hooks0; Vulkan validation **0errors/0warnings**. JSON содержит12 hosted results и zero hook counters. VK_LAYER_PATH задан только для процесса shell/probe и восстановлен в finally; глобальные настройки не менялись.

Ignored evidence в основном checkout: `TestResults/AgentMerge/alloy-ui.log/.trx`, `alloy-consumer-build.log`, `alloy-callbacks-debug.log/.json`. Published Release,4previous chrome/rendering-failure probes, physical DPI/settings/device loss, long soak/VRAM и consumer packaging при интеграции не повторялись. Их агентные/исторические результаты сохраняют прежний scope. Не подтверждается readiness остальных модулей или full alpha.

## TextBox control-text и Name smoke — 2026-10-10

Работа в основном checkout `E:/source/my/TrueMoon/src` поверх `9d7090a`, без commit/push этого прохода. Цель — расследовать исторический нерегулярный Release/Vulkan SettingsFormSmoke.Frame stage2 Name binding mismatch из K9 profile run. Старый `TestResults/AlloyPerformanceProfile/release-standalone-vulkan-first-failure.log` содержит exception/stack, но не actual model/editor или native input. Поэтому историческая причина остаётся неизвестной.

### Детерминированный дефект и исправление

TextBox.HandleInput фильтровал char.IsControl, затем вызывал ReplaceSelection даже при пустом результате. На выделенном emoji это удаляло grapheme и немедленно записывало изменённое значение через two-way binding. Минимальное исправление: после фильтра вернуть unhandled при отсутствии видимого текста. Явные Backspace/Delete/Cut и clipboard policy не менялись; mixed visible/control text сохраняет обычное замещение после фильтра.

Focused red regression:11cases, **7failed/4passed**, command exit1, expected «Ирина 🧑‍💻», actual «Ирина ». Null/empty input и mixed text уже проходили. После fix **11passed/0failed/0skipped**, exit0. Полная UI suite **480passed/0failed/0skipped**, exit0. SDK10.0.401/net10.0, Windows, xUnit2.9.3/VSTest; run-tests и focused code-testing guidance применены.

| Проверяемое поведение | Exact test evidence |
| --- | --- |
| Control-only/empty text не удаляет bound grapheme, не создаёт notifications/redraw и сохраняет focus/selection после unrelated Enabled update | DemoInteractionTests.ControlOnlyText_PreservesBoundNameGraphemeSelectionAndUnrelatedModelUpdate (9cases) |
| Mixed visible/control text заменяет выбранный grapheme, обновляет модель/summary и завершает selection | DemoInteractionTests.MixedControlAndVisibleText_ReplacesSelectedGraphemeAndWritesFilteredName (2cases) |

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~ControlOnlyText_PreservesBoundNameGraphemeSelectionAndUnrelatedModelUpdate|FullyQualifiedName~MixedControlAndVisibleText_ReplacesSelectedGraphemeAndWritesFilteredName' --logger 'trx;LogFileName=control-text-before.trx' --results-directory TestResults/AlloyNameBinding --verbosity minimal
# Before fix: exit1,7failed/4passed.
# After fix: same scope with --no-restore and LogFileName=control-text-after.trx; exit0,11passed.
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=ui-final.trx' --results-directory TestResults/AlloyNameBinding --verbosity minimal
# exit0,480passed/0failed/0skipped.
```

### Bounded диагностика и фактический native scope

`--smoke-trace <path.json>` сохраняет последние128 frame/native-input snapshots: stage/frame, model/editor, enabled/focus/caret/selection и нормализованный UiInput после Hosting routing. Input routing не меняется. При Name failure error включает expected/model/editor и reason=unexpected-value либо binding-out-of-sync. Если trace не удалось записать после primary failure, ошибка логируется и не маскирует первую причину. Наблюдатель включается до запуска оконного цикла, snapshots записываются на UI owner thread, JSON сохраняется после завершения/отказа. Без trace snapshots не создаются.

Сборки: Debug standalone build и Release publish exit0, initial Debug build728warnings/0errors; final incremental build/publish также exit0. Compile-linked AlloyVulkanTest build exit0.20repeat runs (5×Debug/Release×OpenGL/Vulkan) прошли до финального уточнения reason и защиты trace-write failure; TextBox fix, frame/native snapshots и основной interaction script в них уже присутствовали. Каждый40–41frames и completed stage40–41, stage2 model/editor «Ирина 🧑‍💻», snapshots80–82≤128. **Native input в этих20runs не наблюдался.** Vulkan successful runs сообщали validation0errors/0warnings включая teardown. Повторная успешность не закрывает historical failure.

Финальный диагностический код отдельно проверен ignored `verify-native-trace.ps1`. Script запускает только собственный published Release/Vulkan child, сверяет PID через GetWindowThreadProcessId перед каждым PostMessageW и посылает WM_CHAR исключительно его HWND. P/Invoke declarations используют pointer-sized HWND/WPARAM/LPARAM и Win32 BOOL/int. Нет global input injection/hooks/settings. Child ограничен10s, освобождается только собственный process tree; VK_LAYER_PATH process-local и восстановлен в finally.

- Character88/X: child ожидаемо exit−532462766,18recorded native text events, completed=false/stage1, model/editor оба изменились одинаково, reason=unexpected-value. Это воспроизводит похожий smoke failure из обычного изменения значения, **не доказывает historical cause**.
- Character13/CR: child exit0, completed=true/stage40,80snapshots, **0native text events**. Текущий GLFW отфильтровал control WM_CHAR до UiInput. Поэтому найденный control-only TextBox defect не объявляется root-cause исторического native Vulkan сбоя.
- Character88 плюс trace path существующей directory: child ожидаемо exit−532462766, stderr содержит trace-write error и исходный Name InvalidOperationException. Первичная причина сохранена. Эти2negative children не считаются passing smoke; все3outer proof commands exit0.

```powershell
dotnet build ManualTests/AlloyTest/AlloyTest.csproj --configuration Debug --no-restore --verbosity minimal
dotnet publish ManualTests/AlloyTest/AlloyTest.csproj --configuration Release --no-restore --output TestResults/AlloyNameBinding/publish --verbosity minimal
$env:VK_LAYER_PATH=(Resolve-Path TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin).Path
# Repeat each Debug/Release and OpenGL/Vulkan combination five times; every run used a distinct --smoke-trace path.
dotnet ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.dll --smoke --smoke-trace TestResults/AlloyNameBinding/Debug-OpenGL-1.json
dotnet TestResults/AlloyNameBinding/publish/AlloyTest.dll --smoke --vulkan --validation --smoke-trace TestResults/AlloyNameBinding/Release-Vulkan-1.json
pwsh -NoProfile -File TestResults/AlloyNameBinding/verify-native-trace.ps1 -Character 88
pwsh -NoProfile -File TestResults/AlloyNameBinding/verify-native-trace.ps1 -Character 13 -ExpectSuccess
pwsh -NoProfile -File TestResults/AlloyNameBinding/verify-native-trace.ps1 -Character 88 -RejectTraceWrite
dotnet build ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --configuration Debug --no-restore --verbosity minimal
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --settings-compare --validation
```

Последний comparison exit0:3logical scales1/1.5/2×40interaction steps, raster/OpenGL/Vulkan bounds/model/input/selected RGBA согласованы,29draws/backend/scale, static/suspend/restore/disposal passed; Vulkan validation0/0. Это synthetic scale и selected-pixel functional proof, не physical DPI/full golden. Shared sample constructors/compile links остаются совместимыми.

Артефакты ignored `TestResults/AlloyNameBinding`: before/after/final TRX/logs,20run JSON/logs, standalone-runs.json, native proof script/logs/trace, build/publish/compare logs, published consumer, provenance.json. Provenance хранит base HEAD и hashes окончательных source files; в20repeat runs финальный reason/catch refinement ещё отсутствовал, final native proofs выполнялись после него. Полный solution, Enerit/другие модули, physical DPI/device loss, long lifecycle/VRAM, independent package consumer и performance rebaseline не запускались. При естественном повторе Name failure сохранять trace и actual scope; retry не подменяет расследование.

## Lifecycle resize soak и native resource growth — 2026-10-10

Рабочие изменения в основном checkout поверх9d7090a после TextBox/Name шага; commit/push не выполнялись. Scope: Platform.Silk callback lifetime и ManualTests/AlloyVulkanTest probes, без изменений общих contracts/build/package/TFM. net10.0, SDK10.0.401, Windows10.0.26300 x64, native window scale1.5. Inventory adapters: NVIDIA GeForce RTX5070Ti driver32.0.16.1742 и AMD Radeon Graphics32.0.21043.5001; inventory не означает, что конкретный GL/native allocation уже атрибутирован одному из них. Validation1.4.363.0 через process-local VK_LAYER_PATH; native Skia из собственных build/publish artifacts. OS settings не менялись.

### Воспроизведение и исправления callback roots

Первый12-window probe (`short-debug.json/.log`) failed после GC: все6Vulkan host/window/session/tree/controls оставались rooted; GL references освободились. VulkanDevice создавал PfnDebugUtilsMessengerCallbackEXT и терял IDisposable wrapper. Silk.NET2.23.0 закрепляет delegate через GCHandle, освобождаемый wrapper.Dispose; добавлено сохранение wrapper и Dispose после DestroyInstance, включая cleanup failed initialization. KeepAlive сохраняет callback до последнего native teardown вызова. Источники: [Pfn wrapper](https://raw.githubusercontent.com/dotnet/Silk.NET/v2.23.0/src/Vulkan/Silk.NET.Vulkan/PfnDebugUtilsMessengerCallbackEXT.cs), [SilkMarshal](https://raw.githubusercontent.com/dotnet/Silk.NET/v2.23.0/src/Core/Silk.NET.Core/Native/SilkMarshal.cs).

После первого fix освободились5Vulkan graphs; последний оставался и без validation. Это не оказалось async/JIT retention: NoInlining synchronous caller не изменил результат. CLRMD3.1.512801 из уже установленного package, read-only snapshot собственного child, выявил root chain (`roots-detail.log`): StrongHandle/Stack Object[] →WindowPlatforms →GlfwPlatform →последнийGlfwWindow →Update delegate probe →HostedUiWindow. После снятия owned probe Update subscription через root.Own остался только `11:host`. SilkWindowHost дополнительно держался через свои anonymous Render/Focus handlers. Они теперь сохранены в fields и снимаются при constructor failure/Dispose до освобождения input/hooks/HWND. Короткая matrix после обоих production fixes освободила все references.

Automatic heap dump попытка по временным DOTNET_Dbg* process env vars не удалась: createdump сообщил MiniDumpWriteDump/ReadProcessMemory failure. `retention_43252.dmp` — неполный, не считается usable dump/evidence. Env восстановлены. Временные diagnostic wait/resource logging branches в probe удалены; ignored inspect-roots.ps1 относится к исторической instrumented binary, не к final CLI. Root-chain log остаётся свидетельством snapshot, не анализом повреждённого dump.

### Длительный window resource result — FAILED

Новый `--window-lifecycle-soak` задаёт240windows:20повторов каждой GL/Vulkan×Opaque/Opacity/PerPixel×standard/custom комбинации. Каждый цикл:3logical client resize, проверка editor value/emoji grapheme selection/focus, native minimize/restore, explicit zero-size UI suspension и active pointer capture при close/cancel. Publisher и registry живут через все циклы. После каждого HWND: IsWindow0, registry/subscriptions/3hooks0; каждые12окон/в конце GC/WeakReference host/window/session/root/content/editor/button. Собственная Update subscription probe имеет явного owner. JSON также сохраняется при ошибке без маскировки первичной причины.

До запуска приняты warm checkpoint12 и USER/GDI tolerances+2/+4. Первый240-window attempt остановился на24:USER17→23,GDI15. Threshold не менялся. Final bounded run продолжает заранее заданные240cycles ради полного trend, но превышение любого checkpoint сохраняет failure; retry-success вместо failure не использован.

| Configuration | Windows / frames / resize / minimize | Retained refs / hooks / registry / subscriptions | Warm12 →240 USER / GDI | Handles | Private memory | Result |
| --- | --- | --- | --- | --- | --- | --- |
| Debug | 240 /4782 /720 /240 | 0 из1680 tracked; остальные0 после каждого окна | 16→128 /15→15 | 551→896 | 200.9→638.8MiB | failed, exit−532462766 |
| Published Release | 240 /4779 /720 /240 | 0 из1680 tracked; остальные0 после каждого окна | 16→130 /15→15 | 551→894 | 196.7→638.0MiB | failed, exit−532462766 |

Elapsed108.30s/107.45s. Vulkan validation messages0errors/0warnings including teardown проверены в каждом из240cycle отдельно. Report.Completed=false/GuiLimitExceeded=true в обоих files; выполнение всех windows не называется passing resource soak. Managed bytes~0.28→0.94MiB(Debug)/~0.27→0.88MiB(Release) включают растущие metadata lists/WeakReferences самого probe; целевые UI объекты собраны. Process private/working-set/handles не являются VRAM counters, а причину их роста только USER controls не устанавливают.

Full reports получены до последних изменений bounded failure-cleanup и уточнения сообщения validation-not-requested; production cleanup, cycle operations и thresholds те же. Финальные short/callback и actual legacy checks выполнены после этих refinements. provenance.json hashes описывают final source, а не приписываются всем intermediate artifacts.

### Native controls — локализация USER growth

`--native-window-resource-control` выполняет24windows без загрузки native Skia/UiSession/VulkanDevice. Это observational diagnostic control, его exit0 не означает passing UI/resource proof. Native wrapper и direct Silk варианты используют3native render iterations/window; каждый child имеет собственный process.

| Scope | Debug USER/GDI first→last | Published Release USER/GDI first→last |
| --- | --- | --- |
| TrueMoon Silk host, GL, new thread/window | 9→32 /15→15 | отдельный repeat не запускался |
| Direct Silk Window, GL, new thread/window; без TrueMoon host/adapters/input | 9→32 /15→15 | 9→32 /15→15 |
| TrueMoon Silk host, GL, один thread | 24→24 /17→17 | 24→24 /17→17 |
| TrueMoon Silk host, Vulkan window без GL context, new thread/window | 3→3 /12→12 | 3→3 /12→12 |

USER рост воспроизводится на native OpenGL/new-thread пути до UI/Skia/TrueMoon adapters. Точный native allocator/owner (WGL/GLFW/driver/другая native integration) пока не установлен; driver виновником не объявлен. Dedicated UI thread reuse — возможное отдельное архитектурное решение, сейчас не внедрялось; требуется concurrent windows/owner-thread/shutdown proof. Глобальная GLFW termination и замена USER threshold не приняты. Для Win32 counters сверены primary signatures: [GetGuiResources](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguiresources), [IsWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-iswindow).

### Passing regression и точный retirement scope

- Final Debug build exit0,0errors/15warnings (incremental); published Release exit0 с существующими warning families. Ни build/publish, ни UI suite не являются full solution baseline.
- UI suite после production fixes:480passed/0failed/0skipped, xUnit2.9.3/VSTest; ui-lifecycle.trx/log.
- Final short matrix:12Debug windows/235frames и12published Release/239frames; в каждом36resize/12minimize,84tracked refs/retained0, hooks/registry/subscriptions0, validation0/0. Это короткий warm checkpoint, он не заменяет long failure.
- Final callback regression в каждом configuration:6direct+12hosted failures+12healthy recreations; original cause/cleanup errors, Completion/StopAsync identity, session/tree/HWND/hooks/registry cleanup, validation0/0. Native faults expected; outer probes exit0.
- Actual retirement matrix:Debug/published Release×KHR present fences/forced legacy, каждый200sessions/2000frames/400created=destroyed swapchains/10minimize, peak2generations. Fence proofs2000/case, legacy reacquisition399/case. Owned swapchains/semaphores/fences/command pools/retired generations0, registry/subscriptions/400tracked UI refs0, validation0/0 включая device teardown; новая post-window проверка всех3hooks0.
- First runner ошибочно передал scalar flag: файлы retirement-debug-legacy.log и retirement-release-legacy.log сообщаютKHR, а неlegacy. Они были extra fence runs, не засчитаны в forced-legacy coverage. Literal --legacy-presentation повторил оба configurations; evidence только retirement-*-legacy-actual.log. Проверялся log negotiation/actual counters, не название case.
- Legacy WaitIdle shutdown limitation сохраняется; поддержанный KHR и forced legacy на текущем устройстве не проверяют EXT-only hardware, physical device loss или полный driver/VRAM lifecycle.

Команды (VK_LAYER_PATH установлен только для собственного process и восстановлен):

```powershell
dotnet build ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --configuration Debug --no-restore --verbosity minimal
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --configuration Release --no-restore --output TestResults/AlloyLifecycle/publish --verbosity minimal
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=ui-lifecycle.trx' --results-directory TestResults/AlloyLifecycle --verbosity minimal
# Both following commands intentionally return a failed resource result; retain their JSON/logs.
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-lifecycle-soak --soak-cycles 240 --soak-output TestResults/AlloyLifecycle/lifecycle-debug-full.json --validation
dotnet TestResults/AlloyLifecycle/publish/AlloyVulkanTest.dll --window-lifecycle-soak --soak-cycles 240 --soak-output TestResults/AlloyLifecycle/lifecycle-release-full.json --validation
# For each of the two DLLs above; keep configuration-specific paths.
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-lifecycle-soak --soak-cycles 12 --soak-output TestResults/AlloyLifecycle/short-final-debug.json --validation
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-callback-failure --callback-output TestResults/AlloyLifecycle/callback-debug.json --validation
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --retirement-soak --soak-cycles 200 --validation
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --retirement-soak --soak-cycles 200 --legacy-presentation --validation
dotnet TestResults/AlloyLifecycle/publish/AlloyVulkanTest.dll --retirement-soak --soak-cycles 200 --legacy-presentation --validation
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --soak-output TestResults/AlloyLifecycle/native-gl-new-thread.json
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-direct-silk --soak-output TestResults/AlloyLifecycle/native-direct-gl.json
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-single-thread --soak-output TestResults/AlloyLifecycle/native-gl-single-thread.json
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-vulkan-window --soak-output TestResults/AlloyLifecycle/native-vk-new-thread.json
```

Артефакты ignored TestResults/AlloyLifecycle содержат logs/TRX/JSON, partial dump, snapshot root chain, published consumer и final source/native provenance. Общий solution/другие модули, physical DPI/монитор transitions, Settings pixel comparison, fixed fonts/full golden, actual device loss, independent package consumer, native VRAM/driver profiling и K9 rebaseline не запускались. Исторический Name failure не объявлен исправленным. K7/alpha остаются открыты; следующий доступный шаг — owner/native allocation/thread lifetime исследование с неизменным resource check, independent delivery остаётся отдельным шагом.

## WGL thread lifetime и shared owner loop — 2026-10-10

Работа в основном checkout `main`, base HEAD `9d7090a`, поверх предыдущих незакоммиченных TextBox/lifecycle fixes. Изменены только manual diagnostics, CLI и UI planning docs; production Hosting/thread/session contracts и общие build/package настройки этим шагом не менялись. Commit/push не выполнялись.

### Реализация и область доказательства

- `NativeWindowDiagnostics` добавляет own top-level/message-only HWND census с class/native owner ID и active GL vendor/renderer/version. Handles census borrowed; оно не перечисляет все USER objects либо child HWND и не устанавливает exact allocator.
- Raw WGL control: OS STATIC hidden HWND, owned DC, PIXELFORMATDESCRIPTOR40bytes, legacy context,3swaps, unbind→delete context→release DC→destroy HWND на том же owner thread; все cleanup actions выполняются с сохранением original failure. Raw window control не создаёт DC/context. Нет GLFW initialization, Silk window, Skia или UI. Legacy WGL profile отличается от GLFW, оба активных renderer записаны в JSON.
- Native control:1–10000fixed cycles, default24; fresh Thread/window либо current thread либо1–8serial round-robin workers. После каждого окна GC/GUI/private/managed/handles/thread IDs; до/после window disposal HWND census. После worker join дополнительная fixed1s quiescence observation. Exit0 означает завершённую диагностику, а не соблюдение resource budget. Worker reuse здесь не выполняет concurrent windows.
- Optional UI control создаёт прямой `HostedUiSessionFactory`/SkiaOpenGL session с editor, не менее12frames/3logical resizes, focus/selection/value verification; disposal при alive context, registry и4WeakReferences/window. Финальный source дополнительно проверяет zero native hooks после каждого окна. Это не `HostedUiWindow` scheduling и не полная appearance/chrome/minimize/Vulkan matrix.
- Shared-loop prototype работает на вызывающем owner thread:20rounds; два live независимых HWND/session, закрытие первого на12frames и replacement при продолжающемся sibling до36frames. Replacement12frames; context/current/owner guard при input/update/render/dispose. Registry/DPI hooks2→1→2→0, HWND destruction, text/selection,5WeakReferences/window, USER/GDI warm round4+2/+4. Нет одновременно двух native focus promises, cross-platform/fault/cancellation/VRAM proof.

### Изолированные24-cycle controls

Debug, каждый отдельным процессом; `*.json/log` в ignored `TestResults/AlloyNativeThreads`. Fresh-thread случаи полностью join каждый Thread перед sample.

| Control | USER after first window → quiescence | GDI | Private MiB first → quiescence | Итоговый top/message-only HWND census |
| --- | --- | --- | --- | --- |
| Direct Silk GL/new-thread |9→32|15→15|58.5→67.4|2|
| Direct Silk GL/current thread |24→23|17→17|58.5→59.0|9; caller остаётся живым|
| Direct Silk GL/2reused workers |25→10|17→15|59.8→90.2|2; before join USER32|
| Raw WGL/new-thread |7→30|7→7|53.9→63.4|2|
| Raw WGL/current thread |11→11|8→8|53.8→55.6|4; caller остаётся живым|
| Raw Win32/no GL/new-thread |1→1|0→0|8.5→8.6|0|
| Direct Silk Vulkan-window/new-thread |3→3|12→12|26.0→27.4|0; no VulkanDevice|

Фактический GL renderer raw/GLFW — `NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2`; JSON содержит version/vendor. Среди оставшихся top-level classes — NVOpenGLPbuffer/IME. Их count не растёт пропорционально USER, поэтому рост не объяснён retained top-level HWND census. Exact USER object type/allocator остаётся неизвестным: это WGL/Windows/active ICD path correlation, не доказанная конкретная driver bug. Private bytes этого control не объясняют целиком прежний mixed GL/Vulkan~640MiB результат.

### Фиксированная240-cycle матрица

Все10процессов завершились exit0/Completed=true; **это observational completion**, не passing full K7. Warm sample — после12window/context, end —240, last — после join/fixed quiescence. Raw new-thread USER growth сохраняется после quiescence; reused owners ограничивают наблюдаемый рост.

| Configuration/control | USER warm→end→quiescence | GDI warm→end | Private MiB warm→end→quiescence | Handles warm→end |
| --- | --- | --- | --- | --- |
| Debug raw WGL/new |18→246→246|7→7|83.2→113.1→113.0|381→382|
| Release raw WGL/new |18→246→246|7→7|84.9→107.5→107.5|386→387|
| Debug raw WGL/2workers |18→18→8|9→9|87.5→87.2→87.1|460→461|
| Release raw WGL/2workers |18→18→8|9→9|87.8→88.4→89.3|461→462|
| Debug UI/new |21→246→246|15→15|102.5→151.5→150.1|489→489|
| Release UI/new |21→234→234|15→15|99.6→147.6→147.1|487→487|
| Debug UI/1worker |26→26→10|17→17|98.1→103.4→79.0|555→555|
| Release UI/1worker |26→26→11|17→17|99.9→104.9→75.5|556→559|
| Debug UI/2workers |32→32→11|18→18|133.9→142.0→113.8|585→585|
| Release UI/2workers |32→33→12|18→18|134.2→140.8→115.6|586→588|

Raw/new240distinct native owner IDs; UI/new Debug237distinct, Release225distinct из240новых Thread. Native OS IDs могут переиспользоваться; один новый managed Thread не равен одному новому native ID. Рост USER отwarm кend в этих runs совпадает с ростом числа distinct owner IDs — корреляция с thread identity, exact allocator этим не установлен. UI cases по960WeakReferences retained0; process memory includes growing diagnostic metadata/native allocator caches, не VRAM measurement. Serial reused2workers не объявляются GLFW-supported multi-threaded scheduler.

`runs.json` содержит exact arguments/exit/time; `matrix-provenance.json` сохраняет source/debug/published DLL hashes **до** дополнительного hook assertion/error-report refinement, whitespace format и подключения shared-loop CLI. Основные window/session/resize операции матрицы после этого не менялись. Первые24-cycle controls и UI-short12 относятся к промежуточной diagnostic версии. Современные passing controls ниже выполнялись на финальном source.

### Финальная проверка source

- Debug consumer build exit0,15warnings/0errors; Release publish exit0. Existing warnings — NU1507/CS8602/CS1591; общие настройки не менялись.
- Shared loop Debug/published Release: каждый20rounds/60HWNDs/1200frames/180resizes,300WeakReferences retained0, registry/hooks0; sibling продолжал рендериться после teardown/replacement. Warm round4→20 USER25→25/24→24, GDI17→17; private120.2→125.0MiB/119.2→120.9MiB, handles526→528/524→526. Fixed warm+2/+4 assertions passed. Глобальная GLFW termination не выполнялась.
- Финальный serial UI/2workers повторён Debug/published Release по240windows,3resizes/window,960WeakReferences retained0, registry/hooks0. Warm12→240→quiescence USER32→31→11/32→32→11, GDI18→18. Private133.2→137.3→137.5MiB/133.3→138.8→139.9MiB, handles585→582/586→583. Counts/memory не объявляются абсолютным zero native resources или новым memory budget.
- UI unit suite, original full mixed lifecycle/chrome/alpha/minimize matrix, callback/retirement validation, другие modules/solution, physical DPI, fonts/golden, real loss/package consumer и VRAM profile **не повторялись**. Предыдущие480UI tests и failed mixed lifecycle results сохраняют прежний scope; K7/alpha открыты.

Воспроизведение из `src` (configuration/binary выбирается явно):

```powershell
dotnet build ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --configuration Debug --no-restore --verbosity minimal
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --configuration Release --no-restore --output TestResults/AlloyNativeThreads/publish --verbosity minimal
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-raw-wgl --soak-cycles 240 --soak-output TestResults/AlloyNativeThreads/raw-new.json
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-ui-control --native-workers 2 --soak-cycles 240 --soak-output TestResults/AlloyNativeThreads/ui-reuse.json
dotnet TestResults/AlloyNativeThreads/publish/AlloyVulkanTest.dll --window-shared-loop-control --soak-output TestResults/AlloyNativeThreads/shared-release.json
```

SDK10.0.401/Windows10.0.26300.0, active renderer NVIDIA RTX5070Ti; установленный driver inventory предыдущего provenance32.0.16.1742 (это inventory, не allocator attribution). Никакие user OS settings не менялись. Артефакты включают reports/logs/publish, intermediate matrix и final source/binary provenance.

### Решение для следующего этапа

GLFW документирует [window/context creation/destruction и event processing на main thread](https://www.glfw.org/docs/latest/intro.html#thread_safety). Поэтому worker reuse принят только как diagnostic comparison. Предлагаемый следующий шаг — app-lifetime owner dispatcher/event loop для standalone Hosting: несколько live окон, отдельные window commands/completion/first-fault, current-context switch и session cleanup-before-context/HWND. Shared loop prototype проверяет только Windows/OpenGL normal path; production API/lifetime/cancellation/shutdown/failure policy и Vulkan integration предстоит реализовать и проверить. Dedicated-thread-per-window контракт ещё не заменён. После замены повторить исходный full240-window matrix и callback/shutdown regressions в Debug/Release с неизменными thresholds. Exact WGL/OS/ICD allocation и mixed private/handles growth продолжают учитываться; доказанной общей resource stability/VRAM readiness нет.

## Hosting owner dispatcher и Vulkan WSI lifetime — 2026-10-10

Работа начата по запросу «давай приступим к задачам» с ближайшего доступного UI шага: production dispatcher вместо отдельного owner thread на каждое HostedUiWindow. Checkout `69223dd`, Windows, .NET SDK10.0.401/net10.0/xUnit2.9.3/VSTest. Изменены Hosting scheduling, manual pump/refresh в Platform.Silk, focused tests и manual diagnostics. HUD ownership и существующий четырёхпараметрический constructor ABI сохранены. Архитектурное уточнение явно записано в PLAN: окна/devices/sessions принадлежат app scope, единый background owner loop — process lifetime. Scope shutdown не завершает global GLFW; Windows-only контракт не обещает portable main-thread scheduling, concurrent direct Silk hosts на другом потоке не поддержаны.

### Реализация и lifecycle

- UiOwnerLoop запускается лениво, обрабатывает не более32startup works за iteration, pump/commands продолжают обслуживать siblings; idle ждёт signal. HostedUiWindow выполняет до64FIFO commands за tick. Startup, commands, input/render и cleanup проходят на одном owner thread с выбором нужного GL context; VulkanDevice/presenter/session остаются отдельными для каждого окна.
- HostedUiDispatcher регистрируется singleton в AppConfiguration и IStoppable. Shutdown app scope закрывает его окна, ждёт cleanup и отклоняет новые starts; отдельный scope продолжает работу. Cancellation отменяет только ожидание Start/Stop, принятое окно/shutdown продолжаются. Start идемпотентен; Close-before-Start не создаёт HWND/session, readiness cancelled, Completion successful. Posts после close rejected.
- First fault принадлежит отдельному окну; cleanup сохраняет primary cause и добавляет cleanup failures. Completion публикуется после очистки, callback `_closed` вызывается один раз, здоровые siblings продолжаются. Await незавершённого shutdown на owner thread отклоняется после инициирования Close, чтобы не блокировать loop.
- Pump заменяет блокирующий Run только для hosted path. Native WM_PAINT refresh на существующем DPI subclass вызывает guarded RenderRequested после default handler; current context выбирается и для sibling callbacks. Reentrant render/native refresh подавляется. Window Closing notification сохранён; session→presenter→device уничтожаются до HWND/context. Существующие borrowed-frame/fence teardown guards не ослаблены.
- Первый короткий native attempt выявил retention последнего окна/UI graph в idle owner frame. Исправлены lifetime stack boundary (bounded no-inline processing) и очистка callbacks/finished callback/Ready/Completion task references после cleanup. Тест проверяет последний work, без подмены новым. Ошибочное первоначальное ожидание8thread samples исправлено на фактические7 callbacks; это test expectation correction, не доказательство изменения scheduling.
- Финальная ревизия воспроизвела ещё один defect: sibling WM_PAINT меняет current GL context, следующая FIFO command в том же batch получала чужой context. Native regression сравнивает actual `wglGetCurrentContext` с context своего окна. До fix `command-context-red.log` failed с `Next FIFO command inherited a sibling GL context`; после выбора context перед каждой command итоговые Debug/Release dispatcher probes passed. Это red/green proof, не скрытый retry.

### CPU/build evidence

| Требование | Фактическая проверка |
| --- | --- |
| Один owner, sibling работает после close/recreate | `UiOwnerLoopTests.SiblingContinuesAcrossCloseAndRecreationOnSameOwner` |
| Startup/tick primary cause и cleanup fault isolation | `ParticipantFaultPreservesCauseAndCleanupWithoutStoppingSibling`:4theory cases, startup/tick × cleanup success/failure |
| Close до startup без allocation | `CloseBeforeStartupSkipsAllocationAndCancelsReadiness` |
| Shutdown нескольких startup batches; новые работы rejected | `StopDrainsAcceptedWorkAcrossStartupBatchesAndRejectsNewWork` |
| Idle owner не держит последний target | `IdleLoopDoesNotRetainClosedCallbackTargets` |

Focused8cases passed; итоговая UI suite **488passed/0failed/0skipped**, повторена после constructor compatibility shim и per-command context fix. `TrueMoon.slnx` non-incremental Debug build **exit0/errors0/warnings1271**; Release AlloyVulkanTest publish exit0. Tests Core/Cobalt/Enerit/Mithril не повторялись, full solution test pass не заявлен. CPU loop Compile-linked в UI tests; native dispatcher proof отдельно ниже.

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --no-restore --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Debug --no-build --no-restore --logger 'trx;LogFileName=ui-debug.trx' --results-directory TestResults/AlloyDispatcher
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --configuration Release --no-restore --output TestResults/AlloyDispatcher/publish --verbosity quiet
```

Артефакты: `solution-build.log`, `ui-debug.trx`, `owner-loop.trx`, `publish.log` в ignored `TestResults/AlloyDispatcher`. Финальные source/binary hashes отдельно в `provenance.json`; их нельзя ретроспективно приписывать более ранним probe stages.

### Native functional proof

Последовательные Debug и published Release `--hosted-dispatcher --validation` прошли для GL/GL, Vulkan/Vulkan и GL/Vulkan. Проверены distinct HWND/same owner, resize/editor selection, own и sibling directed WM_PAINT refresh, GL context следующей FIFO command, close/recreate при продолжающемся sibling, command/startup fault identity, независимые app scopes. Cancellation проверяет отмену startup wait при продолжающемся accepted window, repeated Start без второго root, canceled shutdown wait при продолжающемся shutdown, rejected starts/posts и Close-before-Start. Native focus не может принадлежать обоим окнам одновременно и такого proof не заявлено. После scope cleanup registry/hooks0, Vulkan validation0errors/0warnings. После constructor shim оба configurations повторены (`abi-final-debug/release.log`), после per-command context fix ещё раз (`final-debug/release---hosted-dispatcher.log`).

После финального per-command context fix повторены Debug/published Release:

- Callback regression — в каждом6direct+12hosted failures+12healthy recreations; first cause/stack/cleanup/Completion identity, HWND/tree/registry/hooks0; validation0/0. Итоговые reports — `callback-final-debug/release.json`, logs `final-debug/release---window-callback-failure.log`. В предыдущем run default JSON был перезаписан Release, его отдельный Debug JSON не заявлен.
- `--window-chrome --validation` и `--settings-failure --validation`:4runs exit0/validation0/0. Failure/recreate — controlled на здоровом device, не physical device loss.
- Short lifecycle12windows в каждом:220frames Debug/219Release, по36resize/12minimize,84tracked WeakReferences/retained0, hooks/registry/subscriptions0, validation0/0 (`short-debug/release.json` и logs). Предыдущая короткая matrix до per-command fix давала218frames в каждом; текущие reports перезаписаны финальным run. Один warm checkpoint не проверяет длительную resource stability.
- App/DI AlloyTest Debug `--smoke` OpenGL и `--vulkan --validation`:по40frames, model/editor smoke passed, успешный App shutdown; Vulkan validation0/0. `app-smoke-gl/vk.json` и logs.

Воспроизведение native checks (выбирать один binary/configuration; не запускать окна параллельно из разных processes):

```powershell
$env:VK_LAYER_PATH=(Resolve-Path TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin).Path
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --hosted-dispatcher --validation
dotnet TestResults/AlloyDispatcher/publish/AlloyVulkanTest.dll --hosted-dispatcher --validation
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-callback-failure --validation --callback-output TestResults/AlloyDispatcher/callback-debug.json
dotnet TestResults/AlloyDispatcher/publish/AlloyVulkanTest.dll --window-callback-failure --validation --callback-output TestResults/AlloyDispatcher/callback-release.json
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-chrome --validation
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --settings-failure --validation
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-lifecycle-soak --validation --soak-cycles 12 --soak-output TestResults/AlloyDispatcher/short-debug.json
dotnet ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.dll --smoke --smoke-trace TestResults/AlloyDispatcher/app-smoke-gl.json
dotnet ManualTests/AlloyTest/bin/Debug/net10.0/AlloyTest.dll --smoke --vulkan --validation --smoke-trace TestResults/AlloyDispatcher/app-smoke-vk.json
```

VK_LAYER_PATH задан только в shell процесса проверки; OS settings не менялись. Итоговый callback run использует explicit output `callback-final-debug/release.json`; команды выше воспроизводят тот же scope с новыми раздельными filenames.

### Full240-window resource check — failed

Full runs включают native refresh/Closing cleanup, но **предшествуют финальным Close-before-Start, rendering reentrancy, completed-task reference guards, constructor ABI shim и per-command GL context fix**. Суффикс `-final` в имени report относится к тому stage, не означает итоговый passing K7 и не доказывает запуск на последних hashes. Первоначальный `lifecycle-debug.json` до refresh также failed на том же GDI trend. Последние guards подтверждены unit/short/functional runs; полный финальный K7 остаётся required.

| Configuration | Scope | USER warm12→240 | GDI warm12→240 | Handles | Private bytes |
| --- | --- | --- | --- | --- | --- |
| Debug |240HWND/4332frames/720resize/240minimize |26→26 |23→137 |601→942 |227491840→635473920 |
| Published Release |240HWND/4342frames/720resize/240minimize |26→26 |23→137 |601→944 |224841728→640741376 |

Оба failed на неизменных warm USER+2/GDI+4 limits, `Completed=false`, `GuiLimitExceeded=true`. По1680tracked WeakReferences/retained0; после каждого окна registry/subscriptions/3hooks0; Vulkan validation0errors/0warnings включая teardown. Managed/private/handles observations не являются VRAM измерением. USER thread-per-window trend отсутствует в текущих shared-owner full runs, но общая native resource stability не достигнута.

```powershell
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-lifecycle-soak --validation --soak-cycles 240 --soak-output TestResults/AlloyDispatcher/lifecycle-debug-final.json
dotnet TestResults/AlloyDispatcher/publish/AlloyVulkanTest.dll --window-lifecycle-soak --validation --soak-cycles 240 --soak-output TestResults/AlloyDispatcher/lifecycle-release-final.json
```

### Native Vulkan isolation

24-cycle Debug controls на одном owner без validation, cleanup и fixed quiescence между windows. Mixed per-window trace показывает GDI+1 после Vulkan, unchanged после GL; warm12→24 GDI23→29/USER26→26. Retained loader handle не изменил результат (`retained-loader.json`).

| Control | GDI after1→quiescent24 | USER | Handles | Scope |
| --- | --- | --- | --- | --- |
| VulkanDevice |14→14 |15→15 |492→608 |Без Skia/presenter/swapchain |
| Vulkan Skia session |14→14 |15→15 |499→615 |Без export/presentation |
| Raw presentation |15→38 |15→15 |616→778 |Existing presenter, clear raw GPU image; без Skia/UI |
| Native swapchain control |15→38 |15→15 |505→667 |Capabilities/formats queries, swapchain create/destroy; без acquire/submit/present/Skia/TrueMoon presenter |

Native reports `Completed=true` означают завершение диагностической последовательности, **не resource acceptance pass**. Device-only handles/private observations также растут; отсутствие GDI trend не объявляет эти controls полностью стабильными. WSI GDI trend воспроизводится без Skia/TrueMoon presenter, но query vs create ещё не разделены. Не доказаны exact allocator, конкретный driver defect или принадлежность неизвестного GDI handle; произвольное удаление native handles не выполнялось. Loader retention не является решением.

```powershell
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-lifecycle-soak --soak-cycles 24 --soak-resource-trace --soak-output TestResults/AlloyDispatcher/resource-trace.json
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-lifecycle-soak --soak-cycles 24 --retain-vulkan-loader-control --soak-resource-trace --soak-output TestResults/AlloyDispatcher/retained-loader.json
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-vulkan-window --native-single-thread --native-vulkan-device --soak-cycles 24 --soak-output TestResults/AlloyDispatcher/vulkan-device.json
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-vulkan-window --native-single-thread --native-vulkan-device --native-vulkan-skia --soak-cycles 24 --soak-output TestResults/AlloyDispatcher/vulkan-skia.json
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-vulkan-window --native-single-thread --native-vulkan-device --native-vulkan-presenter --soak-cycles 24 --soak-output TestResults/AlloyDispatcher/vulkan-presenter.json
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-vulkan-window --native-single-thread --native-vulkan-device --native-vulkan-swapchain --soak-cycles 24 --soak-output TestResults/AlloyDispatcher/vulkan-swapchain.json
```

Следующий доступный шаг: разделить WSI queries/create controls, установить allocation/lifetime/owner, проверить безопасное освобождение; затем полный240-window Debug/published Release на финальном source с прежними limits и callback/shutdown regression. Device cache/global GLFW termination/ослабление thresholds не приняты как workaround. Physical100/200%/mixed-DPI, fixed fonts/full golden, real device loss, long VRAM и independent package consumer этим шагом не проверялись. K7/alpha остаются открытыми; исторический Name mismatch не объявлен исправленным.

## WSI stages, raw Win32 и driver screen DC — 2026-10-10

По запросу «приступай к следующей задаче» выполнен следующий K7 investigation: разделение surface queries и swapchain create/destroy, независимый raw Win32/Vulkan repro, сравнение lifetime orders/повторных generations/thread exit, native allocation stacks и второй установленный GPU. Production Hosting/renderer/ownership в этом шаге не менялись. Предыдущие dispatcher изменения остаются в рабочем дереве; текущий code diff добавляет только manual diagnostics и документы.

### Реализация diagnostic

- `--native-vulkan-wsi-stage capabilities|formats-count|formats|present-modes|swapchain` с `--native-vulkan-window --native-vulkan-device`: queries разделены, samples после каждого stage и device/host disposal. `--native-wsi-repeat 1..32` повторяет stage на одном HWND/surface, default1. Старый `--native-vulkan-swapchain` сохранён через overload без новых checkpoints.
- `--native-raw-vulkan`: hidden Win32 STATIC HWND с valid HINSTANCE, raw Vk instance с KHR_surface/Win32_surface, device с KHR_swapchain. Нет GLFW/Silk Window/production VulkanDevice/Skia или acquisition/GPU submit/presentation. Silk raw Vulkan bindings и общий `VulkanDevice.Check` используются только для ABI/result handling. Native handles принадлежат probe; HWND уничтожается на создавшем потоке после Vulkan cleanup.
- `--native-surface-first` переключает только raw teardown; default device→surface, alternative surface→device. Swapchain всегда уничтожен заранее; затем instance и HWND. При ошибке primary cause сохраняется, cleanup выполняется best-effort. Vulkan [требует уничтожить связанные swapchains до surface](https://docs.vulkan.org/refpages/latest/refpages/source/vkDestroySurfaceKHR.html); surface lifetime не является child logical device. No acquisition/submission/presentation означает отсутствие work/completion, требующего drain в этом control.
- `--native-vulkan-vendor 0x10DE|0x1002` выбирает matching graphics/present GPU только в raw control, без изменений system preferences/driver. Requested vendor/repeat/stage/host/order отражены в JSON; actual Vulkan GPU name/vendor/device/driver property — в log. Malformed/incompatible flags отвергаются до native allocation.
- Scope report явно говорит: `Completed=true` — diagnostic завершён, **не USER/GDI acceptance или K7 pass**. Queries controls не включают validation, graphics frames, full alpha/chrome/DPI или VRAM. Неизвестные/native ICD-owned HDC не удаляются приложением.

### Build и проверенная конфигурация

Windows, .NET SDK10.0.401/net10.0. Текущий non-incremental Debug `AlloyVulkanTest.csproj` consumer build **exit0/errors0/warnings780**; Release publish exit0. Это consumer+transitive dependencies, не полный solution build. Unit tests/другие модульные tests/full solution/full240-window check этим diagnostic шагом не запускались. Предыдущие UI488/488 и solution0errors/1271warnings остаются результатом dispatcher stage.

```powershell
dotnet build ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --configuration Debug --no-incremental --no-restore --verbosity quiet
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --configuration Release --no-restore --output TestResults/AlloyWsi/publish --verbosity quiet
```

Первый raw build failed на ambiguous target-typed constructor overload; исправлено explicit `WsiContext`. Nullable extension guards добавлены, новые raw warnings устранены. Этот failed build не засчитан как passing run.

### Stage/raw/repeat matrix

18основных controls (9Debug+9published Release), по198windows/configuration:8×24cycles +6windows с4create/destroy repeats. Дополнительно raw new-thread control по24windows/configuration. Все diagnostic sequences завершены, но растущие resource counters не объявлены исправленными. После окна sample выполняет bounded GC observation; после worker join фиксированная1s quiescence, без retry-until-pass.

| Control | Cycles/configuration | GDI after1→quiescent last, Debug/Release | USER | Interpretation |
| --- | --- | --- | --- | --- |
| Capabilities |24 |14→14 /14→14 |15→15 |Нет GDI trend |
| Formats count |24 |14→14 /14→14 |15→15 |Нет GDI trend |
| Formats count+data |24 |14→14 /14→14 |15→15 |Нет GDI trend |
| Present modes count+data |24 |14→14 /14→14 |15→15 |Нет GDI trend |
| Native swapchain stage |24 |15→38 /15→38 |15→15 |Trend появляется при create |
| Raw Win32 formats |24 |5→5 /5→5 |2→2 |Без GLFW/production VulkanDevice |
| Raw Win32 swapchain device-first |24 |6→29 /6→29 |2→2 |Trend остаётся после device/surface/instance/HWND cleanup |
| Raw Win32 swapchain surface-first |24 |6→29 /6→29 |2→2 |Перестановка не освобождает retained resource |
| Raw4generations/surface |6 |6→11 /6→11 |2→2 |Растёт только при первом create на каждой новой surface/window |
| Raw new-thread-per-window |24 |5→5 /5→5 |2→2 |Thread exit очищает GDI trend; production scheduling не меняется |

Raw repeat phases подтверждают: cycle1 GDI5→6 при первом create,2/3/4create/destroy остаются6; cycle2 first create6→7, repeats остаются7. New-thread control использует24distinct recorded owner thread IDs, join до after-window sample. Это serial raw control, не concurrent HWND/GLFW scheduling и не общее исправление WGL/new-thread USER issue предыдущего этапа.

Handles/private также меняются в queries/raw controls: например queries с production VulkanDevice492→609handles Debug (Release493→610), raw formats387→504 (Release388→505); raw swapchain400→563 (Release401→564). Эти kernel/private trends **не объяснены** только найденным HDC; отсутствие GDI trend не равно общей resource/VRAM stability.

Артефакты `final-debug/release-<case>.json/.log`, `final-matrix.json`, отдельные `final-debug/release-raw-new-thread.json/.log`. Их source/binary hashes сохранены **до** добавления optional vendor selector в `pre-vendor-provenance.json`; они не переименовываются в runs на последнем source. Начальные exploratory stage/raw reports также сохранены, без переноса их passing completion на K7.

Воспроизведение (один configuration/binary за раз):

```powershell
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-single-thread --native-vulkan-window --native-vulkan-device --native-vulkan-wsi-stage capabilities --soak-cycles 24 --soak-output TestResults/AlloyWsi/capabilities.json
# Повторить stage formats-count / formats / present-modes / swapchain.
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-single-thread --native-vulkan-window --native-vulkan-device --native-vulkan-wsi-stage swapchain --native-raw-vulkan --soak-cycles 24 --soak-output TestResults/AlloyWsi/raw-device-first.json
# Добавить --native-surface-first для alternative order.
# Добавить --native-wsi-repeat 4 --soak-cycles 6 для4generations/surface (оставить только один --soak-cycles).
# Убрать --native-single-thread для serial new-thread control; это не production workaround.
# Для Release заменить binary на TestResults/AlloyWsi/publish/AlloyVulkanTest.dll.
```

### Native allocator/lifetime evidence

Локально скачан [официальный Microsoft WinDbg bundle](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/)1.2610.1001.0. Извлечён только amd64 toolset в ignored `TestResults/AlloyWsi/debugger`, CDB10.0.29661.1004, Authenticode signer Microsoft Corporation/Valid. Системная установка, driver changes, OS settings, global symbol/environment configuration не выполнялись. Compressed archives после hash capture удалены; manifest/URI/hashes/toolset сохранены.

Broad2-cycle trace на GetDC/GetDCEx/GetWindowDC/ReleaseDC/DeleteDC и GDI allocation exports выявил конкретный callsite во время raw vkCreateSwapchain. Targeted return trace на3cycles фиксирует три HDC из `USER32!GetDC(NULL)`, caller `nvoglv64.dll+0xDE1BCA`, затем driver вызывает `wglDescribePixelFormat`. Стек включает raw KhrSwapchain.CreateSwapchain, Vulkan loader и текущий NVIDIA ICD. Private NVIDIA PDB не использовался; имена nearby exports в stack не выдаются за точные имена внутренних функций, module+RVA/version/hash — точное основание.

ICD: RTX5070Ti, vendor0x10DE/device0x2C05; `nvoglv64.dll` file version32.0.16.1742, SHA256 `ADDA6DC8054888D5C52E3F97CF33B742952D16B3F145CED74CFB1012556BD696`. RVA относится только к этому binary. Для HDC `750116d6`, `74011370`, `4f011384` в bounded trace **после их return** не наблюдается ReleaseDC/DeleteDC; GDI6→8, after HWND cleanup census0. Проверка order существенна: handle values могут повторно использоваться, ранний ReleaseDC того же numeric value не является release поздней allocation. Temporal match summary записан в provenance.

Formats-only3cycles negative control: target driver callsite0hits, GDI5→5. Анализ связывает retained screen HDC с первым swapchain create на новом HWND/surface в **текущем ICD**, а не с TrueMoon session ownership. [GetDC(NULL) получает screen DC](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdc); [common DC освобождает вызывающий через ReleaseDC на том же потоке](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-releasedc). Vulkan surface/swapchain API не передаёт этот HDC приложению; освобождение driver-held handles через hooks/scanning/debugger не принято как production fix.

Артефакты: `gdi-breakpoints.txt`, `gdi-allocation.log`, `cdb-console.log` (combined child stdout+debugger); `gdi-return-breakpoints.txt`, `final-cdb-swapchain/formats-console.log`, debugger logs и reports. Initial broad trace имел warning отсутствующего local symbol directory; директория создана, targeted runs выполнены без этой ошибки. Warning checksum managed assembly не считается private native symbol proof. Decoder/driver module paths и hashes — в provenance.

```powershell
$dotnetPath=(Get-Command dotnet).Source
& TestResults/AlloyWsi/debugger/amd64/cdb.exe -G -cf TestResults/AlloyWsi/gdi-return-breakpoints.txt -logo TestResults/AlloyWsi/repro-cdb.log $dotnetPath ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-single-thread --native-vulkan-window --native-vulkan-device --native-raw-vulkan --native-vulkan-wsi-stage swapchain --soak-cycles 3 --soak-output TestResults/AlloyWsi/repro-cdb.json
```

### Второй GPU и итоговая verification

Read-only inventory обнаружил AMD Radeon(TM) Graphics, установленный driver32.0.21043.5001; raw Vulkan properties vendor0x1002/device0x13C0. Добавлен optional selector и тот же raw24-cycle swapchain control повторён на обоих GPUs в Debug/published Release: **NVIDIA GDI6→29, AMD GDI5→5**, USER2→2 в обоих. Source/binary hashes этого stage отдельно в `provenance.json`, exact reports `gpu-debug/release-nvidia/amd.json/.log`, compact `gpu-matrix.json`. GPU selection меняет только созданный probe device, system setting не изменялся.

```powershell
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-single-thread --native-vulkan-window --native-vulkan-device --native-raw-vulkan --native-vulkan-wsi-stage swapchain --native-vulkan-vendor 0x10DE --soak-cycles 24 --soak-output TestResults/AlloyWsi/gpu-debug-nvidia.json
dotnet TestResults/AlloyWsi/publish/AlloyVulkanTest.dll --native-window-resource-control --native-single-thread --native-vulkan-window --native-vulkan-device --native-raw-vulkan --native-vulkan-wsi-stage swapchain --native-vulkan-vendor 0x1002 --soak-cycles 24 --soak-output TestResults/AlloyWsi/gpu-release-amd.json
```

Это подтверждает зависимость **GDI trend** от выбранного текущего ICD; не доказывает исправление всех NVIDIA versions, всех drivers, kernel/private/VRAM stability или complete UI rendering на AMD. AMD raw control не содержит Skia/Present/alpha/resize/minimize и не считается full consumer/K7 pass. Default production GPU policy не менялась.

Production `--hosted-dispatcher --validation` повторён в Debug/published Release: GL/GL, Vulkan/Vulkan, mixed pairs, WM_PAINT/следующий command context, cancellation/fault/scope shutdown прошли, registry/hooks0 и validation0errors/0warnings (`hosted-debug/release.log`). Этот functional run предшествует raw-only optional GPU selector, production binary/code не менялся. Callback/chrome/controlled rendering failure и UI unit suite не повторялись.

Этап локализации query/create/native owner выполнен; **safe app-side fix не получен**. K7 GDI acceptance на текущем default NVIDIA остаётся blocker. Для его снятия нужен supported driver/ICD fix или отдельно согласованная и полностью проверенная alternate GPU configuration; другая NVIDIA версия не установлена и не проверена. Private/handles growth расследовать отдельно. После решения — full240-window Debug/published Release на финальном source с прежними warm+2/+4 limits, callback/shutdown regressions и VRAM evidence. Independent package consumer5.4–5.5 остаётся ближайшим независимым UI code deliverable; physical DPI/fixed fonts/real loss/итоговый alpha не сокращены.

## K2/K4 fixed-font golden — 2026-10-11

Scope: explicit font snapshots in Rendering.Skia, backend/Hosting opt-in overloads,
shared Settings scene symbol alias, full screenshot manual comparison and xUnit
regressions. net10.0/xUnit preserved; native ABI/device/WSI ownership unchanged.
No commits/push/package publication in this task.

SkiaFontCatalog.Load validates and snapshots managed bytes. Ordinal aliases and
explicit default face avoid system lookup; surface owns native fonts/cache16.
Noto Sans/Symbols/Mono at ffebf8c1ee449e544955a7e813c54f9b73848eac with complete OFL
are delivered to test/manual outputs; Mono is the independent metrics test face.
Only Noto Sans/Symbols participate in golden fonts fingerprint.
30CPU raster PNG:10frames×scales1/1.5/2, view720×1000/viewport900×1100logical,
text/clipping/alpha/images/themes/disabled/rows/selection/scroll. All pixels tested,
no masks/outlier percentage. [Method and exact tolerance](GOLDEN_TESTS.md).

Preliminary strict2 failed on thousands of AA edge pixels, max channel156. Symmetric
radius1 envelope+8/+12 still rejected glyph coverage extrema (e.g. CPU R50 against
GPU neighborhood minimumR70). Final edge allowance24 is only for local contrast≥8;
flat/low contrast remains2. Raster/golden and full GL/Vulkan remain strict2. This
allows bounded edge coverage/position differences, not pixel-identical CPU/GPU.
Default API/system-font behavior preserved, emoji shaping/fallback not introduced.

After the user-reported OS failure, two new source files had incomplete tails;
they were restored and rebuilt. No complete result was claimed for the interrupted
edge attempt (its log/manifest were absent). Font SHA256 stayed intact. Symbols2
candidate did not contain U+2191: explicit glyph test failed; replaced with Noto
Sans Symbols and reran glyph/full UI/golden checks. Failed intermediate artifacts
are retained separately and never counted as final passing runs.

### Final checks

Windows x64/.NET10/SDK10.0.401, NVIDIA GeForce RTX5070Ti, OpenGL3.3/NVIDIA617.42;
Vulkan vendor4318/device11269/DriverVersion2588573696/API4211039. Native Skia
SHA25696525E8EFC1E3BB4B9C64000307D4D11C378FEDAEEB7C1E62D518EF84CDD1ABE.

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --filter "FullyQualifiedName~SkiaFontCatalogTests|FullyQualifiedName~PixelDifferenceTests" --logger "trx;LogFileName=focused-reviewed.trx" --results-directory TestResults/AlloyGolden/tests --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-build --no-restore --logger "trx;LogFileName=ui-reviewed.trx" --results-directory TestResults/AlloyGolden/tests --verbosity quiet
pwsh -NoProfile -File ManualTests/AlloyVulkanTest/RunSettingsGolden.ps1 -OutputDirectory E:/source/my/TrueMoon/src/TestResults/AlloyGolden/final-normal -ValidationLayerPath E:/source/my/TrueMoon/src/TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin
```

- Final focused suite **44 passed / 0 failed / 0 skipped**; final UI suite
  **532 passed / 0 failed / 0 skipped**, discovery532, including44new cases.
  Review added4Hosting cases for all3renderer selectors and null-catalog atomicity;
  actual AlloyOptions is compile-linked into tests without new host dependencies.
  Earlier focused33/39, failed528-symbol and passing528 `ui-final-verified.trx`
  runs are separate records; `ui-reviewed.trx` is the final full green evidence.
- Final candidate record90/90 cross-backend comparisons, validation0/0.30PNG
  (2269518bytes) visually reviewed via contact sheet plus full-size samples,
  including confirmed arrow glyph; copied into source fixtures explicitly.
- **Normal Debug180/180 +published Release180/180**, exit0/0:90golden,
  60current-raster/GPU and30strict GL/Vulkan comparisons/configuration.
 40script steps×3backend×3scales, selected RGBA tolerance1, static/no redraw,
 zero-size suspend, restore/disposal/model unsubscribe pass. Full GL/Vulkan max
 channel delta2, raster/golden0; GPU/raster raw max156 fits documented AA envelope.
 All comparisons OutsideTolerancePixels0. Vulkan validation0errors/0warnings
 including device teardown. Source fixtures SHA256 unchanged by runner.
- Incremental Debug build exit0/0warnings/0errors. Release publish exit0 with
 780warning entries (existing warnings retained); not a zero-warning fresh build.
 Fonts/manifest/reference PNG native assets present and read from build/publish.
- Original `--settings-compare --validation` repeated Debug/published Release:
 3scales×40steps with system font path, selected pixels/layout/state/disposal pass;
 validation0/0. This confirms the arrow alias change did not break the old scene.
- `VerifyNegativeControls.ps1` uses ignored copies only: one baseline flat pixel
 changed by20 at600,900, with updated PNG SHA. Release normal matrix produces
 3expected golden failures (one perbackend), each exactly1outside pixel at600,900
 and3diff PNG. Bad font fingerprint and attempted recording into source baseline
 are also rejected.3expected child failures/overall script exit0; source fixtures
  unchanged. This is negative proof, not3passing native golden runs.
- Final static verification:29changed/new text files contain no NUL/conflict
  markers; source files have a final newline, project XML is valid. All30reference
  PNG hashes match the manifest.28documents/358local links/79heading links have
  no errors; `git diff --check` passed with repository line-ending settings.
  Original endings of unchanged lines restored without changing normalized
  content. After normal native verification, only Hosting tests, documentation
  and line-ending restoration changed; focused44/full532 reruns cover the tests.

Artifacts ignored TestResults/AlloyGolden: candidate/failed/final PNG/report/logs,
final-normal summary/build/publish/debug/release reports, tests/TRX, contact sheet,
negative summary and controls. Reports preserve source/binary/native/font/asset
fingerprints and hardware/runtime/configuration. Internal test matrix/review under
.git/testagent/alloy-golden, never stageable.

| Outcome | Concrete regression evidence |
| --- | --- |
| Snapshots, aliases, no system lookup | SkiaFontCatalogTests.Catalog_SnapshotsFilesAndAliasDictionaryBeforeLaterMutation; Catalog_UsesRegisteredFacesAndExplicitDefaultForUnknownOrdinalAliases |
| Native font lifetime/thread ownership | SkiaFontCatalogTests.FontCache_EvictionAndDisposalReleaseNativeFontsWithoutInvalidatingCatalog; Catalog_SharedAcrossThreadsCreatesIndependentOwnedNativeFonts; RasterBackend_UsesCatalogForMeasureAcrossResizeAndIndependentSessions |
| Real arrow glyph | SkiaFontCatalogTests.Catalog_ExplicitSymbolAliasContainsSharedSceneArrowGlyph |
| Hosting renderer selection and failed-call state | SkiaFontCatalogTests.HostingSelectors_KeepRendererAndStandaloneHostSelectionWithCatalog; HostingSelectors_NullCatalogCannotReplacePreviouslySelectedRenderer |
| RGBA/alpha and tolerance bounds | PixelDifferenceTests.Compare_ChecksEveryChannelIncludingAlphaAndLastPixel; Compare_ChannelToleranceIncludesBoundaryButRejectsNextValue; Compare_EdgeCoverageToleranceHasAnExactBound |
| No dropped edge/flat failure | PixelDifferenceTests.Compare_SymmetricEnvelopeRejectsDisappearedEdgeAndShiftBeyondOnePixel; Compare_LowContrastNeighborhoodKeepsStrictTolerance; Compare_EdgeEnvelopeAcceptsAntialiasCoverageButRejectsChangedFlatRegion |
| Diagnostic diff pixels | PixelDifferenceTests.Diff_MagnifiesMaximumDifferenceIncludingAlphaAndClampsWithoutWrapping; negative3PNG |

Full solution, other modules, NuGet consumer/new overload package delivery,
standalone physical input/HUD/WSI soak, physical DPI100/200%, real device loss,
private/handles/VRAM were not rerun. UI K2/K4 checked on the stated configuration;
NVIDIA K7/final alpha acceptance stay open. Next independent general task is
CORE5/COB4 headless PackageReference consumer with both providers/analyzer delivery;
UI remaining4.9physical/K8/VRAM/final matrix keep their existing requirements.

## Сводка качества и следующий scope — 2026-10-15

Документационная сверка локального UI PLAN/STATUS, общего roadmap и текущих
AlloyPackageConsumer Program/csproj/runner показала раздельные прежние package
и font golden proofs. Consumer выбирает только UseDI и backend setup без explicit
SkiaFontCatalog; runner сохраняет source SHA без итогового сравнения. Новый runtime
failure не воспроизводился. [Общая сводка](../QUALITY.md),
[следующий bounded scope/acceptance](PLAN.md#проход-качества-ui-после-headless-модулей--2026-10-15).

Следом fresh package/Hosting proof на Cobalt/DI и explicit fonts, с existing direct
renderer boundary, analyzer/native/font provenance и bounded cleanup. K1–K9,
PA1–PA10, net10.0/xUnit, driver/Windows settings и остановка NVIDIA workaround
search сохранены. Full alpha/physical DPI/real loss/VRAM не объявлены закрытыми.
Code/build/tests/pack/process/GPU/performance в этой сверке не запускались;
проверки документов описаны в общей HISTORY, новые passing module results отсутствуют.

## ENG2 owned pixel image — 2026-10-17

Для engine CPU viewport добавлен `SkiaImageSource.CopyBgraPixels(width,height,pixels)`:
checked positive shape/exact tight length, BGRA8 premultiplied/sRGB/top-left,
SKImage.FromPixelCopy владеет собственной копией. Caller может сразу переиспользовать
buffer, UI owner освобождает image после drawing use. Argentis.Image продолжает
заимствовать source. Новых renderer-neutral/GPU borrowing contracts не вводилось.

UI570/570 Debug/Release, failed/skipped0;7новых cases в SkiaPixelImageTests:
ownership после source clear, channels/origin/premultiplied alpha и invalid/overflow shape.
Full Debug solution1368/1368; два native engine viewport runs на финальном source,
OpenGL readback corners/enlarged frame+tick counters, resize/first-frame/cleanup.
[Commands/reports/failures](../engine/HISTORY.md#eng2-cpu-frame-viewport--2026-10-17).
Это не новый packaged image API proof: pack/package consumer/golden и RichTextEditor
native matrix в этом шаге не запускались. K3 physical DPI/K7/K8/VRAM/final alpha
сохраняются. RTE improvements остаются отложенными, следующий общий шаг ENG3.

## SkiaPixelImageTests: отдельный запуск — 2026-10-17

По запросу пользователя класс запущен отдельно после commit/push26045ae.
VSTest/xUnit2.9.3, Windows x64/.NET10.0.13; Debug/net10.0.
Passed:7, Failed:0, Skipped:0; TRX подтверждает ровно7cases запрошенного класса:
CopyBgraPixels_OwnsPixelsPreservesChannelsOriginAndPremultipliedAlpha и6rows
CopyBgraPixels_RejectsInvalidShapeBeforeNativeAllocation.

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Debug --filter "FullyQualifiedName~TrueMoon.Alloy.Tests.SkiaPixelImageTests" --logger "console;verbosity=normal" --logger "trx;LogFileName=SkiaPixelImageTests.Debug.trx" --results-directory TestResults/SkiaPixelImageTests/isolated-Debug
```

Exit0; report:TestResults/SkiaPixelImageTests/isolated-Debug/SkiaPixelImageTests.Debug.trx.
Исходники/тесты не редактировались. Compile warnings existing Argentis CS1591/CS9113
не являются test failures. Полный UI suite/Release/native/packages/golden в этом
точечном запуске не повторялись; прежний ENG2 scope и следующие задачи сохраняются.

## Аудит инфраструктуры Alloy — 2026-10-17

По запросу пользователя просмотрены project boundaries Argentis/Alloy/Rendering.Skia/
Platform.Silk/Hosting, UiSession/owner loop, registry cleanup, backend options,
property/collection dispatch, image lifetime, Compatibility и test wiring.
Production APIs/код в рамках аудита не менялись; предложения не утверждают новую
очередность вместо ENG3 и не закрывают UI alpha/K3/K7/K8/VRAM.

### Подтверждённые наблюдения

1. **Cleanup retry, высокий приоритет.** HostedUiSessionFactory.Dispose устанавливает
   _stopped=true до session.Dispose. Вызов из posted action во время Update проходит
   factory prevalidation, но UiSession.Dispose запрещает disposal во время Update.
   Первый вызов сообщает InvalidOperationException, registry содержит1session;
   повторный factory.Dispose возвращается по _stopped и оставляет1live session.
   Требуется direct session.Dispose для освобождения. Это дефект восстановления
   после запрещённого shutdown, а не предложение разрешить disposal внутри Update.
   Основание: HostedUiSessionFactory.cs:39–41, UiSession.cs:239–249.
2. **Нет бюджета posted work.** UiSession.Update drains queue до полного опустошения.
   Bounded harness из20000self-posted actions выполнил все20000за один Update.
   Неограниченный producer может задержать layout/render/input и sibling windows.
   Это proof отсутствия batch limit, не измерение CPU/GPU performance.
   Основание: UiSession.cs:62–65/121. Native command batch уже ограничен64, startup
   batch32; CollectionBinding уже coalesces, PropertyBinding Changed — каждый dispatch.
3. **Frame scheduling.** UiOwnerLoop ждёт1ms при active windows; Pump вызывает
   DoUpdate/DoRender, HostedUiWindow.PresentFrame представляет frame после
   SynchronizeViewport независимо от того, изменилось ли дерево. UiSession.Update
   уже пропускает clean redraw, но это не приостанавливает presentation/polling.
   Реальные idle CPU/GPU/power costs в этом аудите не измерялись.
4. **Application-level close.** ENG2 probe вручную перехватывает NativeWindow.Closing,
   откладывает close, ждёт session/controller shutdown и затем закрывает окно.
   Это полезный lifecycle contract для Hosting: unsaved document cancel/save и async
   drain должны иметь owner-safe API; closing и already-closed notification различны.
5. **Extensibility/testing.** Standalone options жёстко выбирают GL/Vulkan bool flags;
   UseRenderer сбрасывает flags и не допускается standalone Validate. UsePresentation
   регистрирует generic TView, а Cobalt consumer требует явной concrete registration;
   root factory overload убрал бы эту ловушку. Часть Hosting/Platform checks сейчас
   Compile-links production source в UI test assembly; existing native/package proofs
   сохраняют свою ценность, но assembly-level tests полезны для новых lifecycle APIs.

### Приоритеты предлагаемых улучшений

| Приоритет | Направление | Ограниченный первый результат |
| --- | --- | --- |
|1 | Retry-safe session registry | Запрет новых Create после начала Stop; повторный cleanup оставшихся sessions после failed attempt; stopped только при отсутствии owned sessions, исходная ошибка наблюдаема |
|2 | Dispatcher/posted work | Бюджет actions или time slice на Update, явная policy переполнения, atomic accept/close, InvokeAsync/cancellation, optional keyed coalescing без silent drop произвольных commands |
|3 | Frame scheduling | RequestFrame/wake на invalidation и pending work, idle/continuous режимы, отдельные Update/Render/Present cadence; сохранить expose/resize/animations/external HUD и существующие FramePresented consumers |
|4 | Hosting close/root factories | RequestCloseAsync с cancel/save/drain, safe owner callbacks, explicit root factory без скрытой Cobalt-specialization зависимости |
|5 | Unified diagnostics | Session/window IDs, pending depth/high-water/processed work, measure/arrange/render/present timings и lifecycle state; bounded opt-in snapshots; existing Vulkan counters не дублировать |
|6 | Реальные assembly tests | Hosting/Platform project references для новых contract checks, portable fake surfaces/window seams, отдельные native/package suites; не считать linked source единственным delivery proof |
|Позже | Backend/resources/legacy | Explicit presenter/target compatibility, scoped image/font resources и dynamic image reuse после baseline; обозначить границу старой PropertiesBase/IView/Compatibility API без удаления совместимости в этом аудите |

Dirty-subtree layout/virtualization рассматривать после diagnostics и реального
large-tree workload: сейчас Layout invalidation делает Root.Measure/Arrange;
выигрыш и корректность partial layout не измерялись. Boundaries проектов удачны:
Argentis не зависит от graphics/platform, Alloy зависит только от Argentis;
перестройка этих пяти проектов не требуется для первых двух исправлений.

### Проверка и границы

Временный ignored harness TestResults/AlloyInfrastructureAudit/Audit.csproj с
ProjectReference на реальную TrueMoon.Alloy.Hosting; fake backend/surface, без native
окна/Skia/GPU. Команда:

```powershell
dotnet run --project TestResults/AlloyInfrastructureAudit/Audit.csproj --verbosity quiet
```

Final exit0; log TestResults/AlloyInfrastructureAudit/factory-retry.log:
Reproduced=true, AfterFailure1/AfterRetry1/SessionDisposedAfterRetry=false,
AfterManualCleanup0; PostedActionsInOneUpdate20000/NoBatchLimitObserved=true.
После наблюдения session освобождена напрямую. Initial harness ошибочно ожидал
AggregateException; UiCleanup при единственной ошибке сохраняет исходный
InvalidOperationException — assertion harness уточнён, production не менялся.

Это два targeted probes, не запуск xUnit suite или новый UI native/performance pass.
Предыдущая отдельная SkiaPixelImageTests Debug7/7 и ENG2 evidence сохраняют свои scopes.
Предлагаемый следующий UI fix — factory cleanup retry, затем dispatcher budget;
общая очередность engine/RTE backlog остаётся отдельным решением пользователя.

## План инфраструктурных улучшений INF0–INF7 — 2026-10-18

По прямому запросу пользователя [аудит2026-10-17](#аудит-инфраструктуры-alloy--2026-10-17)
переведён из предложений в сохранённый дополнительный
[UI roadmap](PLAN.md#дополнительный-план-улучшения-инфраструктуры-alloy--2026-10-18).
Детальные задачи/acceptance ведутся в существующем docs/alloy/PLAN.md, отдельный
дублирующий план не создавался. INF0 — свежая сверка, INF1 — retry-safe registry,
INF2 — bounded dispatch, INF3 — frame scheduling, INF4 — async close, INF5 —
bounded diagnostics, INF6 — root factories/integration compatibility,
INF7 — real assembly/package/native checks по затронутому contract.

Первый UI порядок INF0→INF1→INF2. INF7 сопровождает изменения с первого этапа,
минимальный INF5 нужен до INF3. Queue limits, API names и compatibility policy
уточняются перед реализацией, не объявлены уже согласованным public API.
Поздние image/font reuse, dirty-subtree/virtualization и legacy boundary записаны
как исследования с измерениями/consumer checks, без обещания performance gain
или удаления Compatibility. Слои/net10.0/xUnit/owner-thread/borrowed GPU contracts,
этапы1–5/K1–K9/PA1–PA10 сохранены. ENG3 остаётся общим следующим engine этапом,
RTE backlog отдельно отложен; NVIDIA workaround search не возобновляется.

Обновлены UI STATUS и одна ссылка в общем PLAN; исправлены устаревшие текущие
ENG2 pointers в PA7a вводной UI плана на реализованный ENG2/следующий ENG3.
Production/test code в этом шаге не редактировался. Предыдущая независимая правка
RichTextEditor.cs и незакоммиченные audit/test записи сохранены.
Все INF этапы имеют статус запланировано; новых build/xUnit/native/performance
запусков нет. Проверки этого шага: local links/heading anchors, fences,
whitespace/conflicts/NUL, INF identifiers/acceptance и scoped git diff --check.
Artifact ignored TestResults/AlloyInfrastructurePlanning/verification.json.

## INF0/INF1: повторяемый cleanup registry — 2026-10-18

По запросу пользователя начат рекомендуемый UI этап INF0→INF1.
[Contract/acceptance](PLAN.md#inf1-registry-shutdown-после-отказа), [статус](STATUS.md).
Первоначальные audit/planning records выше сохранены с исходными датами.

### INF0 и изменение поведения

До правки повторён ignored TestResults/AlloyInfrastructureAudit/Audit.csproj:
cleanup Reproduced=true/AfterFailure1/AfterRetry1, manual release0;
self-posting20000actions прошли за один Update. Собственный baseline scope — эти
два targeted fake-surface probes и сверка contracts, не performance baseline.
Log TestResults/AlloyInfrastructureAudit/inf0-baseline.log, exit0.

HostedUiSessionFactory теперь разделяет shutdown requested и активную попытку
cleanup. Первый принятый Stop/Dispose закрывает новые Create даже при failed
preflight; owner/borrow guards проверяются до destruction sessions. После Update/
tree mutation/возврата lease или на owner thread retry пытается закрыть оставшихся.
Ошибки отдельных Dispose не отменяют остальных попыток; UiCleanup сохраняет
исходную единственную ошибку либо aggregates causes. Завершённые sessions удаляются
через existing Disposed registry hook и повторно не освобождаются. Реентрантный
cleanup возвращается к внешней попытке; finally всегда снимает attempt guard.

Shutdown во время backend/attachment callback создания session отклоняется до
принятия request. Это сохраняет IUiSessionFactory contract root ownership on success:
failed Create не уничтожает caller root; constructor rollback очищает только
созданные renderer resources. StopAsync остаётся synchronous owner cleanup,
не прерываемым cancellation token посередине; async close — будущий INF4.
Новых public signatures/native ABI/package dependencies нет.

### Real assembly regression project

Создан TrueMoon.Alloy.Hosting.Tests: net10.0/xUnit2.9.3/VSTest, central package versions,
один ProjectReference на production Hosting. Зарегистрирован в точном TrueMoon.slnx
через dotnet sln add; dotnet sln TrueMoon.slnx list подтвердил регистрацию.
Existing UI tests намеренно сохраняют neutral/source-linked Hosting helpers;
их проекты/исходники не мигрировались массово. Новый project проверяет реальный
factory/UiSession с portable fake renderer, без HWND/Skia/GPU allocation.
Это частичное выполнение INF7 для INF1, не завершение всего INF7.

IDE findTests через execute_tool вернул Tool not found; fallback сверил existing
UI/engine tests и соседние ownership conventions. Focused code-testing workflow —
один factory class, без sub-agents. Перед fix новый
Dispose_DuringUpdateCanBeRetriedAfterUpdateEnds упал:1failed/0passed,
artifact TestResults/AlloyInfrastructure/INF1/red. На финальном source14cases passed.

| Requirement | Exact evidence: HostedUiSessionFactoryTests |
| --- | --- |
| Guarded Update failure и owner retry, Create fence | Dispose_DuringUpdateCanBeRetriedAfterUpdateEnds |
| Продолжение cleanup других sessions/без double-dispose | Dispose_PartialFailureCleansOtherSessionsAndRetryOnlyCleansRemaining (2rows) |
| Несколько live sessions, aggregate errors и progress2→1→0 | Dispose_MultipleBusySessionsPreserveErrorsAndMakeProgressAcrossRetries |
| Borrowed preflight не уничтожает ни одну session | StopAsync_BorrowedFramePreflightRetainsAllSessionsUntilOwnerRetries |
| Wrong-thread отказ и последующий owner retry | Dispose_ForeignThreadFailureRetainsOwnershipAndOwnerCanRetry |
| Tree mutation guard и retry после завершения изменения | Dispose_TreeMutationGuardRetainsSessionUntilMutationCompletes |
| Реентрантный cleanup не повторяет disposal | Dispose_ReentrantCleanupReturnsToOuterAttemptWithoutRepeatingDisposal |
| Первичные root/renderer error objects наблюдаемы | Dispose_PreservesCleanupCausesAndDoesNotRepeatAlreadyDisposedSessions |
| Preflight cause сохраняется, guard recovery допускает retry | Dispose_PreflightExceptionIsPreservedAndRetryAfterGuardRecoveryWorks |
| Empty/repeated Stop закрывает создание без allocations | StopAsync_EmptyRegistryRejectsNewCreationAndRemainsIdempotent |
| Независимый session.Dispose не останавливает factory | SessionDisposal_RemovesRegistryEntryWithoutStoppingFactory |
| Failed Create сохраняет root caller ownership | Create_ReentrantStopIsRejectedWithoutTakingRootOwnership (2rows) |

Exact commands Windows x64/SDK10.0.402/.NET10.0.13:

```powershell
dotnet test TrueMoon.Alloy.Hosting.Tests/TrueMoon.Alloy.Hosting.Tests.csproj --configuration Debug --logger trx --results-directory TestResults/AlloyInfrastructure/INF1/focused-final --verbosity quiet
dotnet test TrueMoon.Alloy.Hosting.Tests/TrueMoon.Alloy.Hosting.Tests.csproj --configuration Release --logger trx --results-directory TestResults/AlloyInfrastructure/INF1/final-checks/hosting-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/AlloyInfrastructure/INF1/final-checks/solution-Debug --verbosity quiet
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
& ./ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory ./TestResults/EngineSessions/inf1-final-native -IncludeViewport -Scenarios @()
```

Focused Hosting Debug14/14 и Release14/14, failed/skipped0. Exact solution entry
point discovered новый project: full Debug1382/1382,9TRX/failed0/skipped0;
Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI570/Hosting14/engine87.
Nonincremental Debug build0errors/1211warnings, warnings не подавлены.
Final helper TestResults/AlloyInfrastructure/INF1/RunFinal.ps1:3dotnet commands exit0
и2native viewport cases; summary final-checks/summary.json Completed=true,
10TRX/1396passed суммарно (full Debug +Hosting Release, не1396unique tests).

### Bounded native и исходный detector после fix

TestResults/EngineSessions/inf1-final-native/summary.json Completed=true:
2/2 Debug apphost/published framework-dependent Release viewport runs,14scenarios
и25child launches каждый, по20Start/Stop, resize/slow UI/actual GL corners+counter
pixels, live-child EOF, deadline/cancel/native close. PixelChecks48Debug/47Release,
children released, UI sessions0/mappings0, Editor state сохранён. Source323/binary76
fingerprints сохранены. Это только два viewport runs:32ENG1 headless scenarios,
Vulkan/physical DPI/K7/loss/VRAM/full alpha/golden/packages в этом шаге не повторялись.

После fix исходный audit detector сообщает Reproduced=false/AfterRetry0/
SessionDisposedAfterRetry=true. Его exit1 ожидаем: программа возвращает0 только
при наличии старого дефекта. Postfix log TestResults/AlloyInfrastructure/INF1/postfix-audit.log.
Его второй record остаётся PostedActionsInOneUpdate20000: INF2 ещё не реализован.
Новый code/testing acceptance — отдельные passing xUnit/native reports выше.

Следующий инфраструктурный UI этап INF2; общий engine шаг ENG3, отложенный RTE
backlog и hardware blockers сохраняются. Существующая пользовательская style-правка
RichTextEditor.cs не изменялась. Commit/push не выполнялись.
Final documentation/provenance/wiring guard — ignored
TestResults/AlloyInfrastructure/INF1/completion.json.

## ENG4b: borrowed GL owner и live import cleanup — 2026-10-20

Engine one-frame probe добавлен в Aether, UI production APIs не менялись.
App owns GPU resources; GL callbacks/context заимствованы из существующего окна.
Native WM_CLOSE с actual live imports ждёт GPU cleanup до view/window disposal.
Обычный CPU viewport и20Replace Editor proof повторены в Debug/published Release:
classic17cases/27children/145и141pixel checks; Editor20cycles/20failure cases/
53children/777и783pixel checks, MaxLive1. Новый import run:14checks/8children,
3whole-image roundtrips/923904pixels,7cooperative producer teardown reports,
core/synchronization validation errors0/warnings0, local NT/GPU/UI resources0.

Full Debug: UI613/Hosting79 passing (всего1769,9TRX). Это engine regression;
own UI Release/golden/RTE/UI packages/performance/hardware acceptance не повторялись.
K3/K7/K8/VRAM/final alpha и отложенный RTE backlog прежние. Validation layer
извлечён локально, drivers/Windows settings не менялись, K7 workaround search
не возобновлялся. Следующий общий code шаг ENG4c slots/surface lifecycle;
ordinary GPU viewport/compositing остаётся ENG4d. Точные commands/raw evidence:
[engine HISTORY](../engine/HISTORY.md#eng4b-one-frame-import-и-retirement--2026-10-20).

## ENG4c: GPU slots/surface lifecycle regression — 2026-10-20

UI production API не менялся. Aether App owns3persistent slots/shared device и
borrowed GL owner;21checks/80frames/12children в Debug и published Release
проверяют reuse/resize/queued resize/suspend/Stop/crash/restart/retry и WM_CLOSE с
accepted pending Produce/live import.9cooperative teardown reports/configuration:
15devices/45slots created/released, core/sync validation errors0/warnings0,
parent GPU/NT/UI resources0.3deliberate crashes имеют owned reap без native
cooperative teardown claim. Ordinary viewport остаётся CPU; далее ENG4d actual
GPU presentation/compositing/first-present.

Full Debug UI613/Hosting79 passing; всего1847/9TRX/failed0/skipped0.
Old classic viewport17cases/27children/140и145pixel checks, Editor20cycles/
20failure cases/53children/779и788pixel checks, MaxLive1. Existing Editor deadline
helper публиковал PID marker до закрытия writer; sharing violation воспроизведена,
helper исправлен на write-temp/close/atomic Move, вся native matrix повторена.
Final50aggregate cases/52commands/exit0,394source/76binary fingerprints.
Own UI Release/golden/RTE/UI packages/performance/hardware не повторялись;
K3/K7/K8/VRAM/final alpha и RTE backlog прежние. Drivers/Windows settings прежние,
K7 workaround search не возобновлялся. [Exact evidence](../engine/HISTORY.md#eng4c-reusable-slots-и-surface-lifecycle--2026-10-20).

## Сводки до PA8a — 2026-10-22

Последняя интеграционная работа — **2026-10-22, ENG5c.2 object interaction**:
UI production код не менялся. RoomGame меняет цвет только выбранного объекта,
App/Editor/IPC/GPU/retained UI pipeline и прежний input sender сохранены.
Full solution Debug/Release2186/configuration, UI615/Hosting79, failed0/skipped0.
[Scope/evidence](../engine/HISTORY.md#eng5c2-reachraycast-и-interaction--2026-10-22).
Новый interaction proof Debug/published Release: по10cases/9PNG, visible target
toggle/Replace/off/occlusion/disabled; core/sync0/0, zero ownership/imported readback.
Local pack только Aether; UI package drawing consumer не запускался.
Fresh full engine native60/60cases прошёл, включая прежние CPU/GPU/Editor/input/
Replace/failure regressions после исправления diagnostic JSON state deserialization.
Собственная full UI/golden/RTE/hardware/performance matrix не повторялась;
PA8a/RTE/K3/K7/K8/VRAM/final alpha прежние. Далее engine collisions, затем HUD.

Предыдущая интеграционная работа — **2026-10-22, ENG5c.1 authoring-комната**:
UI production код не менялся. App/ViewportView/Skia retained GPU image/input sender
используются новым `--room` consumer с несколькими примитивами из отдельного
runtime. Full solution Debug/Release2134/2134/configuration, UI615/Hosting79,
failed0/skipped0. [Scope/evidence](../engine/HISTORY.md#eng5c1-scene-consumers-и-несколько-примитивов--2026-10-22).
Engine native58/58cases Debug/published Release: room по5PNG, прежние CPU/GPU/
input/Editor Replace/failure regressions, core/sync0/0, zero resources/handles/
mappings/UI sessions/imported ReadPixels. Local pack только Aether; отдельный
UI package drawing consumer не запускался.
Own full UI/golden/RTE/hardware/performance matrix этим не повторяется;
PA8a/RTE/K3/K7/K8/VRAM/final alpha прежние. Далее engine ENG5c.2 reach/interaction,
затем нужные collisions/Alloy HUD.

Предыдущая интеграционная работа — **2026-10-21, ENG5b gameplay input**:
Argentis UiKey дополнен W/S/D/E с сохранением старых enum values; Silk mapping
дополнен. Sample viewport реализует focus/pointer capture, coalescing и30Hz sender,
Escape/FocusLost/capture transfer/resize/suspend/Replace сбрасывают input до teardown.
Full Debug/Release1968/1968 в каждой configuration, UI615/Hosting79.
Engine native regression56/56; отдельный Debug/published Release proof по15cases/
5PNG проходит через Silk key callback и реальный UiSession/GPU output; validation0/0,
zero ownership/imported ReadPixels0. Local Argentis/Platform.Silk pack payload
совпал с published proof; package rendering consumer не запускался.
Это не OS keyboard/mouse injection/physical focus acceptance и не raw cursor lock.
Own full UI/golden/RTE/performance/hardware matrix не повторена.
[Подробный scope и evidence](../engine/HISTORY.md#eng5b-gameplay-input-и-камера--2026-10-21).
Далее ENG5c комната/HUD; PA8a/RTE и K3/K7/K8/VRAM/final alpha прежние.

Предыдущая интеграционная работа — **2026-10-21, ENG5a Vulkan3D viewport**:
UI production код не менялся. Новый scene proof использует существующие
App/ViewportView/GPU transport/retained Skia copy: Debug/published Release,
по5PNG с depth/material/camera, resize/suspend/resume и checkpoint Replace.
Core/sync validation0/0; owned resources/handles/mappings/UI sessions0,
imported ReadPixels0. Full solution Debug/Release1926/1926 в каждой конфигурации,
UI615/Hosting79. Own full UI package/native/golden/RTE/hardware acceptance
этим не повторён. [Evidence и scope](../engine/HISTORY.md#eng5a-первый-vulkan-3d-frame--2026-10-21).
Отдельная engine native matrix12/12: scene и прежние GPU import/surface/viewport/
ordinary Editor/repeat, Debug/published Release; UI production код прежний.
Далее ENG5b input/camera; PA8a/RTE/K3/K7/K8/VRAM/final alpha сохраняются.

Предыдущая интеграционная работа — **2026-10-21, ENG4e viewport baseline**:
UI production код не менялся. Новый native consumer использует существующий
ViewportView/Skia retained images:12CPU/GPU cases/36intervals,3pixel checks на case,
zero owned resources после teardown. UI615/Hosting79 Debug/Release в full
solution1907/1907. Отдельный6-case GPU correctness run с validation0/0 прошёл;
собственный full UI package/native/golden/RTE/hardware acceptance этим не повторён.
[Метрики/ограничения и Mithril fix](../engine/HISTORY.md#eng4e-cpugpu-baseline--2026-10-21).
Далее общий ENG5; PA8a/RTE запланированы, K3/K7/K8/VRAM/final alpha открыты.

Документационная работа — **2026-10-21, планирование оформления UI**: в
[PA8a](PLAN.md#pa8a-дефолтный-стиль-контролов-и-оформление-окна--2026-10-21)
сохранены минималистичный цветной стиль с прямыми углами, тема/состояния контролов,
бордер и заголовок borderless-окна, кнопки minimize/maximize/restore/close и нижний
resize grip. Состояние — запланировано; при выборе направления начать с PA8a0
(визуальная спецификация и сверка API). На момент планирования приоритетом был ENG4e.
Изменены только планы/статус; code/build/test/native проверки не выполнялись,
прежние результаты ниже сохраняют свои даты и scope.
Проверки документации:3файла,150локальных ссылок/86якорей, ошибок0;
`git diff --check` для этих документов прошёл.

Исправление структуры — **2026-10-21**:12Runtime files используют namespace
TrueMoon.Alloy.Runtime. Imports, qualified references, package templates, README
и manual consumer namespaces обновлены. UI615/615 и Hosting79/79 Debug/Release;
UI source16/package8 native runs, дополнительная package matrix36+4cases прошли.
Native engine CPU/GPU editor consumers проверены в Debug/published Release.
Public namespace изменён; внешним consumers нужны новые using и пересборка.
K3/K7/K8/VRAM/final alpha остаются открытыми. [Команды, промежуточный отказ и границы](../HISTORY.md#исправление-структуры-проектов--2026-10-21).

Предыдущая работа над реализацией — **2026-10-20, ENG4d2 GPU GameEditor integration**. UI production
APIs прежние; обычный ManualTests Editor View использует explicit GPU transport,
retained Skia image и neutral first-present ticket. CPU mode/default сохранён.
Start ждёт borrowed GL binding ready, availability использует neutral surface;
background transport errors доступны в статусе. Engine lifecycle drains до UI.
UI615/615 Debug/Release, Hosting79 Debug; full Debug1898/1898. Свежие package
consumers проверяют managed transport API/App ownership и Skia native delivery.
Debug/published Release: per GPU Editor20Replace/20failures/53children; validation0/0, imported readback0,47devices/141slots balanced. GPU copies815/800, pixel checks791/783.
[Engine evidence/границы](../engine/HISTORY.md#eng4d2-ordinary-gpu-gameeditor--2026-10-20).
Далее ENG4e CPU/GPU baseline до 3D. Golden/RTE/full own UI package/native/performance/
hardware matrix не повторялись; K3/K7/K8/VRAM/final alpha прежние.

Предыдущая работа — **2026-10-20, ENG4d retained GPU image bridge**:
`SkiaOpenGLSurface.CopyTexture` копирует borrowed RGBA8 premul/stored-sRGB/top-left
texture в независимое GPU image и submits source use до return/throw. Image draw/
dispose требует исходный surface/context owner; raster/foreign draw отвергается.
Actual reference viewport Debug/published Release:17checks/14captures/
3,051,552independently checked interior-canvas pixels при150% DPI, imported
readback0; validation0errors/0warnings, resources/handles/UI sessions0.
UI615/615 Debug/Release; full Debug1858/1858 повторён после draw-path test review,
Hosting79 Debug. Fresh Rendering.Skia package API/native delivery проверен в4App
consumers вместе с Aether. Это не полный собственный UI package/native matrix.
[Команды и границы](../engine/HISTORY.md#eng4d-gpu-viewport-bridge--2026-10-20),
[UI запись](HISTORY.md#eng4d-retained-gpu-image-bridge--2026-10-20).
Далее ENG4d2 ordinary GameEditor GPU transport; CPU default сохранён. Golden/RTE/
performance/hardware/final alpha не повторялись; K3/K7/K8/VRAM прежние.

Предыдущая engine regression — **2026-10-20, ENG4c**: reusable GPU slots/surface lifecycle,
resize/queued resize/suspend/Stop/crash/restart/retry проверены в Debug/published
Release.21checks/80frames/12children в surface run; core/sync validation0/0,
GPU resources/NT handles/UI sessions0. Native WM_CLOSE drains pending Produce и
live imports до borrowed UI owner disposal. UI production APIs прежние, viewport
CPU default. Old Editor proof получил atomic worker marker fix; final run passing.
Full Debug повторил UI613/Hosting79. Далее ENG4d. Own Release UI/golden/RTE/UI package/performance/
hardware не повторялись; K3/K7/K8/VRAM/final alpha и отложенный RTE backlog прежние.
[Подробные engine команды/evidence](../engine/HISTORY.md#eng4c-reusable-slots-и-surface-lifecycle--2026-10-20).
Предыдущие regressions сохранены в [HISTORY](HISTORY.md).

App entry-point audit — **2026-10-19**: UseAlloy/UsePresentation находятся в
Alloy.Hosting; Argentis/Alloy/renderer/platform остаются самостоятельными слоями.
Focused Debug AlloyIntegrationTests/HostedUiSessionFactoryTests:38passed/failed0/
skipped0. UI code не менялся; full UI/Release/native GPU/package/golden/RTE/
performance/hardware matrix не повторялись. K3/K7/K8/VRAM/final alpha прежние.
[Общая таблица API и команды](../HISTORY.md#аудит-точек-подключения-модулей-к-app--2026-10-19).

Integration regression — **2026-10-19, Aether App integration**: viewport example
получает Supervisor/Frames из AetherHost, сохраняет UI close/drain порядок.
Полный Debug повторил UI613/Hosting79; Debug apphost/published Release viewport
прошли с actual GL pixels, по20Start/Stop, native-close-before-UI-disposal и
mappings/sessions0/childrenreleased. UI production code не менялся. Own Release
UI/golden/RTE/package/performance/hardware matrix не повторялись; K3/K7/K8/VRAM
и final alpha остаются открыты.
[Единственные подробные engine команды/evidence](../engine/HISTORY.md#app-integration-реализация--2026-10-19).

Package metadata check — **2026-10-19**: Argentis/Alloy/Silk получили descriptions
и собственные PACKAGE.md; все5UI packages — тематические tags и общие repository/
project metadata. Release pack/nuspec/README/license/icon/XML и Skia native/targets
payloads проверены. UI runtime code, own unit/native/golden/RTE/hardware matrix
в этом metadata шаге не менялись/не повторялись.
[Общие команды/evidence](../HISTORY.md#nuget-package-metadata--2026-10-19).

Последняя интеграционная проверка — **2026-10-19, выделение TrueMoon.Aether**:
пример EngineSessionProbe теперь использует engine library. Full Debug solution
повторил UI613/Hosting79; два native OpenGL viewport runs Debug/published Release
прошли с actual pixels, по20Start/Stop, native-close drain и cleanup. UI production
code не менялся; own Release UI/golden/RTE/package/hardware matrix не повторялись.
[Единственные подробные engine команды/evidence](../engine/HISTORY.md#truemoonaether--2026-10-19).

Предыдущая работа — **2026-10-19, документальная сверка roadmap**: исправлена
устаревшая ссылка на ENG1 в отложенном RTE плане. Bounded INF завершён;
следующий общий шаг ENG3a в [engine плане](../engine/PLAN.md#eng3-уточнение-плана-перед-реализацией--2026-10-19).
RTE/full INF5 остаются отложенными по потребности UI; K3/K7/K8/VRAM/final alpha
не закрыты. Новых code/build/tests/native/performance runs в этой сверке нет.

Последняя реализация — **2026-10-19, INF7**: tracked integration runner с профилями
Assembly/Source/Package/All, bounded owned processes, fresh output directories,
partial manifest и отдельный read-only verifier. Runner больше не зависит от
временных scripts в TestResults. Production API и test-project wiring не менялись.
[Запуск/профили](../../ManualTests/AlloyFactoryConsumer/README.md#inf7-воспроизводимый-integration-runner),
[контракт](PLAN.md#inf7-общая-проверка-изменений),
[команды/guards/evidence](HISTORY.md#inf7-воспроизводимый-integration-runner--2026-10-19).

Текущий All: Hosting79/UI613 Debug/Release,4TRX/1384passed/failed0/skipped0;
8source consumers/48App cases,8native scheduling/close/dispatcher/HUD processes,
10fresh nupkgs/8independent native consumers без analyzer/view registration.
Всего24native processes,46commands,4required analyzer/native payloads; validation,
ownership/registry и cleanup checks прошли в своих scopes.21runner guard checks,
standalone Assembly и повторная read-only verification прошли.394source inputs/
840artifact fingerprints сверены; directory/timeout/discovery/drift failures не
превращаются в passing proof. Existing linked UI checks сохранены; Hosting guard
требует actual ProjectReference. Нового full-solution/nonincremental build/golden/
native RTE/engine viewport/performance запуска нет; full Debug1490 относится к INF6.
Bounded INF0–INF4/INF6/INF7 и минимальный INF5 завершены. Далее общий ENG3;
full INF5/RTE/K3/K7/K8/VRAM/final alpha сохраняются.

Предыдущий [INF6 root factory/compatibility и full Debug1490](HISTORY.md#inf6-явные-factories-и-совместимость--2026-10-19)
проверен2026-10-19 в указанном там scope; API/migration contract сохранён.

Предыдущий [INF4 async close и engine drain](HISTORY.md#inf4-асинхронное-закрытие--2026-10-18)
реализован и проверен2026-10-18 в своём scope; его native engine evidence исторический.

Предыдущая работа — **2026-10-18, INF3**: Automatic/OnDemand/Continuous frame policy,
coalesced thread-safe RequestFrame, monotonic cadence, work/close wake и bounded
shared wait. Native expose использует cached output; resize/minimize/zero/resume,
pending commands и requests во время draw сохраняются. NeedsRender отделён от
NeedsUpdate для output под bounded queue. [Контракт](PLAN.md#inf3inf6-условия-интеграции),
[матрица/команды/failures/evidence](HISTORY.md#inf3-запросы-кадров-и-scheduling--2026-10-18).

22новых cases; UI613/613 и real Hosting39/39 Debug/Release. Повторный full Debug
solution1450/1450,9TRX/failed0/skipped0; nonincremental build0errors/1211warnings.
Первый full run1449/1450: независимая Mithril readiness-file sharing race; targeted20
и повторный full94Mithril прошли, race не исправлялась и не скрыта.
Fresh7packages/2independent consumers прошли. Debug apphost/published Release:
новый scheduling2pairs, legacy dispatcher3pairs, external HUD90frames/36draws/
2sessions/5readbacks, read-only golden180/config с validation включая teardown;
два ENG2 viewport smoke с явным Continuous/RequestFrame прошли. Все10native
processes завершились, registry/hooks/mappings0; source/binary fingerprints сверены.
Это functional proof; idle CPU/GPU и новый BDN overhead не измерялись. Full INF5
частичен; далее INF4. Full Release solution,32headless ENG1, прежняя full10package/
40consumer и native RTE matrix не повторялись. ENG3/RTE/K3/K7/K8/VRAM сохраняются.

Предыдущая работа — **2026-10-18, документация**: результаты аудита включены в
[дополнительный план инфраструктуры INF0–INF7](PLAN.md#дополнительный-план-улучшения-инфраструктуры-alloy--2026-10-18).
Зафиксированы приоритеты, зависимости, границы и acceptance registry/dispatcher/
scheduling/close/diagnostics/factories/assembly tests. Все этапы запланированы;
реализация не начиналась, build/tests/native/performance runs в этом шаге не было.
Проверены Markdown links/anchors, структура и whitespace;
[запись изменения](HISTORY.md#план-инфраструктурных-улучшений-inf0inf7--2026-10-18).

Предыдущая работа — **2026-10-17, аудит инфраструктуры Alloy**.
Воспроизведены unretryable factory cleanup после Dispose во время Update и
выполнение20000posted actions за один Update. Предложены приоритеты registry/
dispatcher/scheduling/close/diagnostics; production код в аудите не менялся.
[Findings, proof и границы](HISTORY.md#аудит-инфраструктуры-alloy--2026-10-17).
Это targeted fake-surface probes, не новый full UI/native/performance run;
ENG3 очередность, hardware blockers и отложенный RTE backlog сохраняются.

Точечная перепроверка — **2026-10-17, SkiaPixelImageTests отдельно**:
Debug/net10.0, Passed7/Failed0/Skipped0; проверены ownership/BGRA/origin/alpha
и6invalid shape cases. Исходники не менялись; полный UI suite/Release/native
в этом запуске не повторялись. [Команда и TRX](HISTORY.md#skiapixelimagetests-отдельный-запуск--2026-10-17).

Предыдущая реализация — **2026-10-17, ENG2 viewport image adapter**.
Добавлен owned `SkiaImageSource.CopyBgraPixels`: tight BGRA8 premultiplied/sRGB,
checked shape/length и независимая копия pixels. Owner-thread upload/disposal
проверены новым engine Alloy/OpenGL окном. [UI запись](HISTORY.md#eng2-owned-pixel-image--2026-10-17),
[точный engine scope/evidence](../engine/HISTORY.md#eng2-cpu-frame-viewport--2026-10-17).
UI570/570 Debug/Release; два native engine viewport runs (Debug apphost/published
Release) с actual GL pixels и cleanup. Full Debug solution1368/1368. Это отдельный
bounded engine proof; package/golden/native RichTextEditor matrix в этом шаге
не повторялись, K3/K7/K8/VRAM/final alpha не закрыты. Следующий общий этап ENG3.

Отдельный [план улучшений RichTextEditor](RICH_TEXT_EDITOR_PLAN.md) по решению
пользователя остаётся отложенным; implementation улучшений не начиналась.

Предыдущая UI реализация и запуски — **2026-10-17, RichTextEditor перед ENG1**.
Реализованы immutable document/style/options, editing/selection/clipboard/history
в Argentis и Topten.RichTextKit0.6.170 adapter в Rendering.Skia; raster/OpenGL/Vulkan
используют один layout/painting path. [Контракт PA7a](PLAN.md#pa7a-richtexteditor-перед-eng1--2026-10-17),
[API/options/fonts/binding](RICH_TEXT_EDITOR.md),
[команды/failures/evidence](HISTORY.md#richtexteditor--2026-10-17).

Предыдущие проверки реализации2026-10-17, Windows x64/net10.0/SDK10.0.402: UI suite **563/563 отдельно
Debug и Release**, failed/skipped0;31новый case. Demo: **6/6** Debug/published
Release ×raster/OpenGL/Vulkan с formatting/edit/history/options/cleanup.
Fresh **10nupkgs,36App +4direct processes** проверили rich editor и transitive
Topten/HarfBuzz delivery, fonts/native/analyzer provenance и final fingerprints;
negative control без analyzer — ожидаемый CS0246. Read-only прежний Settings golden
**360/360**, validation включая teardown0errors/0warnings, fixtures unchanged.
Nonincremental Debug solution build: **0errors/1200warnings**; это не full-solution
test pass. Own Core/Cobalt/Enerit/Mithril suites в этом шаге не повторялись.

RichTextEditor: plain clipboard и logical grapheme navigation; IME/accessibility,
full bidi caret affinity, HTML/RTF, lists/tables и long native-memory trend вне scope.
RichTextKit process-wide shaper caches не объявляются очищенными при Dispose.

Предыдущие проверки2026-10-11: fonts/golden/package — в
[истории](HISTORY.md#k2k4-fixed-font-golden--2026-10-11).
K3 physical DPI, NVIDIA K7, K8 real loss, VRAM и final matrix сохраняются.
Driver/Windows settings не менялись; workaround search не возобновлялся.

Область: Argentis, Alloy, Rendering.Skia, Platform.Silk, Alloy.Hosting,
UI tests и consumers. [План](PLAN.md), [история](HISTORY.md), [общий статус](../STATUS.md).

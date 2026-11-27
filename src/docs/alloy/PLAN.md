# TrueMoon.Alloy: план первой рабочей версии

<a id="current-ui-queue"></a>
<a id="текущая-очередь--актуализация-2026-11-09"></a>
## Текущая очередь — 26.11.2026

Рабочий срез `1.2611.26.17-alpha`: для консоли Editor добавлены
`ScrollViewer.ScrollToEnd()` и `UiSession.ViewportChanged`. Поведение проверено
в исходниках на layout, маршрутизируемом вводе и CPU Skia; пакетный и нативный
прогоны этого среза не выполнялись.
[Контракты и результаты](HISTORY.md#scroll-to-end-and-viewport-2026-11-26).
Адресная [пакетная приёмка композиции](#uia1-view-package-acceptance) относится
к прежнему набору `1.2611.25.20-alpha.ui.*`, а не к текущей миграции Editor.

| Очерёдность | Задача | Когда шаг закончен |
|---|---|---|
| Закрыто 27.11.2026 — [UIA4.1](#uia4-view-layer-audit) | Декларативная привязка списков не доходила до прикладного кода: строки инспектора велись словарями адресов и ручными `Items.Move`/`Dispose`. | Ключи и инкрементальный reconcile приняты на измеренном сценарии; все динамические списки инспектора Editor (секции компонентов, строки свойств, добавление и скрытые вхождения) ведутся привязками без потери фокуса, ввода и Undo. |
| На подходящем стенде — [PA8c.1a](#pa8c1a-positioning-rollout) | Перенести окно Editor между двумя мониторами с разным DPI туда и обратно. | В обеих темах сохранены состояние поля и точность ввода. Решение о режиме по умолчанию — отдельно, с учётом стоимости на Vulkan. |
| По подтверждённому сценарию — [динамические строки инспектора](#ui-reuse-study) | Перестановка и замена свойства по тому же адресу уже проверены. Не прототипировать удержание старого редактора, пока нет частой совместимой замены в рабочем сценарии. | Если сценарий найден, сравнить аллокации, число редакторов и время кадра. Общий API — только при измеримом выигрыше. |
| Отдельная приёмка выпуска — [alpha](#воспроизводимый-итоговый-проход-alpha) | Закрыть открытые профили, в том числе физический ввод, K7/K8, VRAM и поставку на актуальном артефакте. | K1–K9 и 5.4/5.5 подтверждены в своих конфигурациях; адресные smoke не заменяют матрицу. |

**Закрыто в проверенном объёме:** [UIQA.3 на Transform](HISTORY.md#uiqa3-visual-states-2026-11-26),
[пакетная приёмка Editor на обоих хостах](../engine/HISTORY.md#editor-package-view-acceptance-2026-11-26),
[пакетная композиция View](#uia1-view-package-acceptance) и
[UIA1f.12](#uia1f12-fluent-generation-study): пять кнопок иерархии используют
типизированный метод, без новой генерации и изменений динамических списков.

**Вне ближайшей очереди:**
- [INF5](#этапы-inf-и-критерии-завершения) — при появлении потребителя диагностики. [SplitView](#uia3e-split-view) реализован; интеграция остаётся у приложения.
- Legacy-сценарий `Game.Logic.csproj --smoke` остановился до viewport на `EditorCommand.Build`. Успешный `--smoke-camera` не закрывает этот маршрут.

<a id="uia4-view-layer-audit"></a>
### UIA4. Аудит View-слоя — 27.11.2026 (выполняется)

Основание — чтение исходников `TrueMoon.Argentis` (Element, Views, Bindings, Layout,
Styling), `TrueMoon.Alloy/Runtime`, `TrueMoon.Alloy.Hosting`, `TrueMoon.Argentis.Generator`
и потребителя `TrueMoon.Aether.Editor.UI`. Это статический разбор: сборка, тесты и нативные
прогоны этого среза не выполнялись, код и версия не менялись. Строки соответствуют рабочей
копии 27.11.2026. Ни один пункт не вводит публичный API до согласования контракта; стоимость
подтверждается прогоном по [PERFORMANCE_BASELINE](PERFORMANCE_BASELINE.md), а не оценкой.

| ID | Подтверждённое ограничение (исходники) | Задача | Когда задача закончена |
|---|---|---|---|
| UIA4.1 | `BindItems` не вызывается ни в одном прикладном экране (`TrueMoon.Aether.Editor.UI`, `TrueMoon.Aether.Player`); `Editing/EditorInspectorView.cs:283-346,587-593` ведёт строки 12 словарями адресов, ручными `Items.Move` и `Dispose`. Причины в движке: одна привязка на контейнер (`Layout/ElementCollection.cs:10-16`), идентичность строки = ссылка на item (`Bindings/CollectionBinding.cs:12,74-82`), `Changed` игнорирует аргументы и делает полный снимок с пятью коллекциями на каждое изменение (`Bindings/CollectionBinding.cs:67-116,148-153`), источник только `ObservableCollection`/`ReadOnlyObservableCollection` (`Views/UiBuildExtensions.cs:238-251`) | Привязка детей со стабильным ключом, источником `IReadOnlyList` и явным refresh, инкрементальный reconcile с переупорядочиванием и несколько привязок на контейнер. Совместить с пунктом 1 [повторного использования UI](#ui-reuse-study); вывод «адресного обновления достаточно» пересматривается явно | Один экран Editor переведён с ручных словарей; при изменении одной строки создаётся одна строка, фокус, ввод и прокрутка сохранены; аллокации и время кадра сравнены с той же сценой до правки |
| UIA4.2 | Точечной стоимости View-слоя нет: есть только K9 Settings (195 элементов, property-edit `MeasureCore` 278–483 µs, 120 КБ/кадр в Update, «dirty-subtree layout не объявляется») и ни одного измерения `Get`/`Set`/`Invalidate`, привязок и пересборки списков; `Benchmarks/` содержит только Cobalt и Enerit | Измерить фазы `Get`/`Set`/`Invalidate`, `PropertyBinding` (Expression и `Compile` на каждую привязку, `Bindings/PropertyBinding.cs:32`), `CollectionBinding.Synchronize` и аллокации на узел (`Element.cs:13-16`) на масштабируемой сцене; закрепить бюджеты в PERFORMANCE_BASELINE | Сцены и команды воспроизведения опубликованы, пара до/после имеет совпадающие scene/harness fingerprints; по каждой цифре назван следующий кандидат на оптимизацию |
| UIA4.3 | Layout всегда полный обход. Снято 27.11.2026 в части кэша: `UiSession.cs:375-379` вызывает `Root.Measure`/`Root.Arrange`, `PrepareInputLayout` повторяет обход на вводе; кэша валидности measure у `Element` не было — теперь `_measureValid`/`_arrangeValid` по ограничению размера и экземпляру `ITextLayoutService` (`Element.cs:33-37`, `:549-576`); `Text.ValueProperty` помечен `Invalidation.Layout` (`Text.cs:9`); `Invalidate` поднимается к корню, но снимает кэш только на этом пути (`Element.cs:268-270`); `ScrollViewer` вторично меряет контент только когда ему нужны снятые под скроллбар единицы (`Layout/ScrollViewer.cs:86-97`) | Адресная инвалидация поддерева и кэш measure/arrange — сделано 27.11.2026; viewport-виртуализация `ElementList` вынесена в UIA4.3.1 (пункт 2 [повторного использования UI](#ui-reuse-study), PA10) | Выполнено 27.11.2026: при изменении одного свойства `MeasureCore`/`ArrangeCore` неизменённые поддеревья не обходятся (`TrueMoon.Alloy.Tests/LayoutInvalidationTests.cs`), `Update` без изменений сохраняет 0 measure/arrange, CPU и аллокации зафиксированы парой до/после |
| UIA4.3.1 | Снято 27.11.2026. Виртуализации списков не было: `ElementList` (`Layout/ElementList.cs`) меряет и раскладывает всех детей, `ScrollViewer` строит контент целиком (`Layout/ScrollViewer.cs:91-119`). Теперь `Layout/VirtualizingStack.cs` реализует только окно видимости плюс `Buffer` с каждой стороны (`BufferProperty`, `EstimatedRowHeightProperty`, `WindowStart`/`WindowLength`); узлов вне окна не существует, а extent строится из измеренных высот строк и среднего по ним для нереализованных. Ключевая привязка окна — `Bindings/VirtualizingCollectionBinding.cs` поверх `IReadOnlyList` (`ElementExtensions.BindVirtualizedItems`), уведомление источника только помечает окно устаревшим; вышедшая из окна строка уходит в пул и переиспользуется, фокус и hover с неё снимаются (`Recycle`). Без предка `ScrollViewer` окно не определено — реализуются все строки; строки не используют `StackBase.FillProperty` | Viewport-виртуализация `ElementList` по образцу отрисованных строк `TreeView`: узлы за пределами окна не участвуют в measure/arrange и отрисовке, прокрутка и клавиатурный маршрут не меняются | Выполнено 27.11.2026: сборка сцены 2496 строк — 746/729 µs и 996 400 байт против 19 589/17 831 µs и 20 209 624 байт у того же списка без виртуализации; шаг прокрутки — 56/54 µs и 106 856 байт против 401/391 µs и 199 792 байт; вставка и удаление строки вне окна — 8 µs и 448 байт против 1 730/1 669 µs и 668 080 байт. Окно с буфером, переиспользование пула, extent, `ScrollToEnd` и фокус закреплены десятью тестами `VirtualizingListTests`; пара до/после — [PERFORMANCE_BASELINE](PERFORMANCE_BASELINE.md#uia431-viewport-virtualization-2026-11-27) |
| UIA4.4 | Снято 27.11.2026 в части состава узла, пакетной вставки и освобождения: четыре коллекции на узел аллоцировались eagerly (`Element.cs:14-17`) — первые два локальных значения живут в inline-слотах `_localKey`/`_localValue` и `_localKey2`/`_localValue2` (`:257-277`), словарь и остальные коллекции заведены по необходимости, отложен и словарь базы (`PropertiesBase.cs:5`); пакетная вставка не раскрывает детей в `[owner, .. children]` — блокировка поверх массива вызывающего (`Layout/ElementCollection.cs:55-80`, `TreeChange.cs:52-69`), набор проверки дубликатов переиспользуется (`ElementCollection.cs:62`); `DetachCore` больше не собирает поддерево LINQ-ом, обход индексный в `[ThreadStatic]`-буфер (`Element.cs:494-504`, `:539-578`), очередь уведомлений и список инвалидаций `TreeChange` переиспользуемые (`TreeChange.cs:19-21`, `:98-112`, `:146-158`). Освобождение закрыто 28.11.2026 полностью: сам объект `TreeChange` арендуется из `[ThreadStatic]`-стека и возвращается в пул разблокированным и пустым (`TreeChange.cs:22-27`, `:31-105`, `:189-214`), освобождение ребёнка идёт без делегата `Action` — `RunDispose` (`TreeChange.cs:128-133`, `Element.cs:721`). Закрыто числом без правки: `Get<T>` при промахе — две стилевые цепочки и примерно 14 `ReferenceEquals` с виртуальными `Theme*` (`Element.cs:212-237`) стоят 0 µs и 0 байт на чтение. Остаётся: `ElementScope` на каждый View (`Views/View.cs:17`) и обязательный слой `ContentControl` | После UIA4.2 сократить постоянные затраты узла и цену освобождения: отложенные коллекции, inline-слоты значений и переиспользуемые буферы — сделано 27.11.2026; компактное хранилище измерено и сокращено, кэш вычисленного значения в пределах кадра не вводился. Приоритет local → Style → стиль View → Theme → default и контракт `Get/Set/Clear` не меняются | Часть закрыта: 469 байт на узел вместо 550 и 160 байт на отсоединение с освобождением вместо 395 — [пара до/после](PERFORMANCE_BASELINE.md#uia44-detach-and-store-2026-11-27); второе значение стоит 24 байта, третье — 240 (словарь), `Get` закрыт нулём стенда. Третья часть 28.11.2026: освобождение subtree — 0 байт на узел вместо 160, постоянные затраты узла 469 → 427 байта — [замер](PERFORMANCE_BASELINE.md#uia44-change-pool-and-dispose-2026-11-28); строка остаётся открытой по `ElementScope` и `ContentControl` |
| UIA4.5 | Снято 27.11.2026 в части одной условной ветки. Контент объявляется один раз навсегда (`Views/View.cs:53-60` и флаг `_contentPreparationStarted`), поэтому условные поддеревья делались через `IsVisible` и ручной `SetContent(null)`+`Dispose` (`Editing/EditorInspectorView.cs:560-571` до переноса, `EditorView.cs:287`). Теперь есть `Layout/ConditionalContent.cs`: typed `State` (и `StateProperty` для привязки), фабрика `TState → Element?`, равный запросу состояние оставляет установленную ветвь, `null` снимает её, снятая ветвь освобождается самим слотом, пересборка идёт через очередь диспетчера текущего attach (`ConditionalContent.cs:13-102`) | Декларативный условный слот (состояние → фабрика или снятие) с сохранением экземпляра при совместимом состоянии; пересечься с пунктом 1 [повторного использования UI](#ui-reuse-study) | Выполнено для одной ветки: поповер мешей, материалов и слотов инспектора и область состояний AlloyTest обходятся без ручного `SetContent(null)`/`Dispose`, снятая ветвь освобождается слотом, повторное открытие после закрытия пересобирает строки — равный запрос этой проводкой не достигается ([замер](PERFORMANCE_BASELINE.md#uia45-conditional-slot-2026-11-27), 26 методов / 33 случая в `ConditionalContentTests`, проводка поповера — `InspectorRenderChoicesWiringTests`). Остаток: `Editing/EditorView.cs:287` меняет контент вручную — слоту нужен токен идентичности набора свойств из модели; несколько именованных секций в одном слоте и условные части генератора (UIA4.7) не вводились |
| UIA4.6 | Преобразования значений в привязках нет: converter и формат в Argentis отсутствуют, поэтому ViewModel отдаёт готовые строки (`SelectionText`, `SourceText`, `DiagnosticsText` в `Editing/IEditorInspectorViewModel.cs`), то есть знает о представлении — вопреки разделу «View, данные и оформление» в [`../../AGENTS.md`](../../AGENTS.md) | Типизированное преобразование значения привязки с двусторонним направлением; доступ к зависимостям — только через конструктор, без service locator | Одна presentation-строка инспектора убрана из ViewModel и заменена привязкой с преобразованием; `BindTwoWay` с преобразователем проверен на вводе и ошибках |
| UIA4.7 | Генератор частей видит только вызов с именем `Content` (`ViewPartsGenerator.cs:45-51`) и только в конструкторе (`:42-43,76-109`): `DeclareContent(...)`, фабрика-метод и `SetContent` в `OnContentPrepared` не дают ни частей, ни диагностики. Дочерние View-части исключены (`:176`), вложенные, generic и file-local View вне поддержки (`:121-125`). Следствие в приложении — дубль доступа: `NamedControls _parts = null!` и 13 пробросов (`Editing/EditorInspectorView.cs:58,95-119,183-184`), что прямо запрещено правилами View | Признать все точки объявления контента, генерировать типизированные дочерние View-части, выдавать диагностику когда `Content` найден, но частей нет, и объяснять причину для неподдерживаемой формы | Тесты генератора покрывают `DeclareContent`, фабрику-метод, дочернюю View-часть и случай «есть контент, нет ни одной именованной части»; в `EditorInspectorView` ручной `NamedControls` с `null!` заменён сгенерированным доступом |
| UIA4.8 | DI-конструктор выбирается эвристикой «самый длинный публичный» с ошибкой при ничьей (`ViewRegistrationGenerator.cs:63-66`); вызов дочерней фабрики генерируется (`:116-122`), но отсутствие `.View<TChild>()` видно только при создании окна | Явный выбор конструктора атрибутом и проверка на сборке, что каждый параметр-View и сервис имеет регистрацию | Неполный граф View даёт диагностику сборки вместо рантайм-исключения; лишняя публичная перегрузка не меняет активацию незаметно |
| UIA4.9 | `ViewActivation<T>` — изменяемое статическое поле на закрытый generic с first-writer-wins (`Alloy.Hosting/Generation/ViewActivation.cs:11-20`): вторая регистрация в процессе (другая сборка, второй App, тестовый контейнер) игнорируется без предупреждения, переопределения нет | Перенести делегаты активации в реестр контейнера или App; переходный вариант — ошибка при повторной регистрации и явное переопределение | Два независимых App или контейнера в одном процессе регистрируют и создают один и тот же View без молчаливой подмены; тест проверяет обе регистрации |
| UIA4.10 | `VerifyTreeAccess` запрещает структурные правки во время уведомлений (`Element.cs:331-337`, `TreeChange.cs:12-26`), поэтому каждый динамический экран строит свою очередь: `_contextVersion`, `_pendingUpdate`, `Dispatch`, `BlockDynamicInput` (`Editing/EditorInspectorView.cs:190-240`) | Отложенная очередь структурных изменений в движке, привязанная к текущему attach (по образцу `UiSynchronization.AfterTreeChange`), с коалесингом и блокировкой ввода на время синхронизации | Ручная очередь и `BlockDynamicInput` убраны минимум в одном экране; detach или смена контекста во время отложенной синхронизации не создают узлов |
| UIA4.11 | `TreeView` — нарисованный контрол со строковыми ключами вне модели View: `TreeItem(Id, ParentId, Text, Icon)` (`TreeItem.cs:8`), `SetItems` с лимитом 4096 и обходом предков на каждый вызов (`TreeView.cs:71-108`), фиксированный `RowHeight`; состояние сущности кодируется текстом подписи | Отдельно после UIA4.1 и UIA4.3: типизированное содержимое строки дерева без потери текущего маршрута ввода и Undo иерархии | Иерархия Editor показывает хотя бы одно поле помимо текста и иконки; лимит 4096 снят или задокументирован как контракт с измеренной причиной |

**Порядок.** UIA4.2 закрыта 27.11.2026: пара до/после есть в
[PERFORMANCE_BASELINE](PERFORMANCE_BASELINE.md#uia42-view-cost-2026-11-27) — 1394–1410 Б на узел при
сборке, 96 нс и 24 Б на узел при measure, 802 Б и 3.1 мкс на каждую строку списка за одну вставку,
3.4 мкс и 4.2 КБ на объявление привязки. UIA4.1 закрыта со стороны движка 27.11.2026:
[привязка детей со стабильным ключом, источником `IReadOnlyList`, явным `refresh` и инкрементальным
reconcile](PERFORMANCE_BASELINE.md#uia41-collection-reconcile-2026-11-27) даёт 466 Б на строку вместо
802 за одну вставку, 8 Б вместо 164 на `RefreshItems` и 125 µs/290 КБ вместо 27.2 ms/26.3 МБ на
перезагрузку списка с сохранением строк; все динамические списки инспектора Editor переведены на неё.
Закрыт и остаток UIA4.1: секции компонентов и строки свойств инспектора ведутся ключевыми привязками
(секция — `(ComponentId, поколение контекста)`, строка — `(Key, запись свойства, поколение контекста)`
внутри тела секции), восемь словарей адресов убраны, перепривязка обработчиков не нужна — строка
читает свою запись на момент нажатия, поэтому `ElementExtensions.OnClick` не накапливает подписку.
Первая половина UIA4.3 закрыта 27.11.2026: [кэш валидности measure/arrange, адресная инвалидация и
отказ от лишнего второго мерящего вызова `ScrollViewer`](PERFORMANCE_BASELINE.md#uia43-addressable-layout-2026-11-27)
— повторный обход 12 481 узла стоит 0 µs и 0 байт вместо 1.8 мс и 499 КБ, правка одного листа —
120 µs и 400 байт вместо 1.8 мс и 499 КБ, уведомление одного свойства в списке 2496 строк — 132–214 µs
и 872 байта вместо ~10 мс и 499 КБ. Закрыта и viewport-виртуализация `ElementList` (UIA4.3.1): `VirtualizingStack`
держит только окно видимости плюс буфер — сборка списка 2496 строк стоит 746/729 µs и 996 400 байт вместо
19 589/17 831 µs и 20 209 624 байт, шаг прокрутки — 56/54 µs вместо 401/391 µs, мутация источника вне окна —
8 µs и 448 байт вместо 1 730/1 669 µs и 668 080 байт
([замер](PERFORMANCE_BASELINE.md#uia44-node-cost-2026-11-27)). Вторая часть UIA4.4 закрыта 27.11.2026 тем же
стендом ([замер](PERFORMANCE_BASELINE.md#uia44-detach-and-store-2026-11-27)): постоянные затраты узла
550 → 469 байт, отсоединение и освобождение — 160 байт на узел вместо 395 (−59 %), второе локальное значение
стоит 24 байта, шаг прокрутки окна — 66 088 байта вместо 87 376, хранилище одного значения — 472 байта вместо
576. Третья часть закрыта 28.11.2026: объект `TreeChange` арендуется из `[ThreadStatic]`-стека, а
освобождение ребёнка идёт без делегата — освобождение subtree стоит 0 байт на узел вместо 160,
постоянные затраты узла 469 → 427 байта
([замер](PERFORMANCE_BASELINE.md#uia44-change-pool-and-dispose-2026-11-28)). Остаток UIA4.4 —
`ElementScope` на каждый View и обязательный слой `ContentControl`; пункт `Get<T>` при промахе закрыт
числом (0 µs и 0 байт на чтение, код не менялся); UIA4.5–UIA4.7 убирают обходные пути новых экранов;
UIA4.8–UIA4.10 — контракты регистрации и очерёдности; UIA4.11 вне ближайшей очереди.

UIA4.5 закрыта 27.11.2026 в части одной условной ветки: `Layout/ConditionalContent.cs` принимает typed
`State`, оставляет установленную ветвь при равном запросе и сам освобождает снятую. Поповер выбора мешей,
материалов и слотов инспектора и область состояний AlloyTest переведены на него; ручной
`SetContent(null)`+`Dispose` остался только в `Editing/EditorView.cs:287`. В поповере равный запрос не
возникает: повторное нажатие на то же поле закрывает список и сбрасывает состояние, как и новый снимок
ассетов, поэтому повторное открытие собирает строки заново — экономия примитива достижима только там,
где состояние переустанавливают равным.
[Замер](PERFORMANCE_BASELINE.md#uia45-conditional-slot-2026-11-27): повторный запрос равного состояния —
0 µs и 24 байта вместо 39 152 / 301 584 / 2 524 608 байт и 77–3 310 µs ручной замены ветки при
39 / 312 / 2496 строках; пересборка новой ветки через слот стоит на 432 байта дороже ручной замены
(+80 304 байта при 2496 строках).

**Вне области UIA4:** reference identity действующего `BindItems` как совместимый путь, отложенный
`Content` при первом attach (принято в [UIA1f.11](#uia1f11-contextual-content)), сам reentrancy-замок,
accessibility/IME и multi-monitor DPI — у них свои маршруты плана.

<a id="editor-chrome-ui-findings"></a>
### Улучшения из миграции статусной области

- [x] **Активация с контекстом ввода.** `Activate(IInputContext)` получает
  контекст напрямую; `.OnActivated(...)` не требует поля во View. Меню и
  статус-бар больше не хранят временный контекст. `IUiCommand` не зависит от сессии.
- [x] **Фабрики внутренних контролов View.** `ui.Create(factory[, arguments])`
  переиспользует проверки и очистку `Content` без регистрации в хосте. Пилот —
  статус-бар и `EditorHeaderView`; обе панели не требуют регистрации закрытых деталей в хосте.
  [Изменения и проверки](HISTORY.md#ui-routed-activation-and-local-factories-2026-11-26).
- [x] **Внутренние части и общая подгонка текста.** `Named(...)` генерирует
  `internal`-доступ к внутренним типам контролов без раскрытия их из сборки.
  Заголовок и статусные сегменты используют один `Text.FitSingleLine`.
  [Контракты и проверки](HISTORY.md#ui-internal-parts-and-text-fit-2026-11-26).
- [x] **Контракты заголовка.** `SetHeader` заменяет слот одной операцией;
  `ObserveState` объединяет сигналы нескольких источников; общая проверка
  подсказок ждёт актуального layout. [Проверки](HISTORY.md#ui-title-slot-state-and-hints-2026-11-26).
- **Списки из снимков с устойчивыми ключами.** Каталог popup и инспектор уже
  могут переставлять готовые строки без detach через `Items.Move`. Не расширять
  `BindItems`, пока не сравним повторяемый сценарий двух consumers: сохранение
  той же строки, фокуса и цели команды при обновлении снимка.

<a id="skiasharp-41570-completed"></a>
Выполнено 18.11.2026. SkiaSharp обновлён до **4.157.0**, расширенная
Windows x64 DLL пересобрана из соответствующих исходников с Vulkan GENERAL/HOST
и ABI v1. В Debug и Release пройдены ABI, Vulkan и сравнение raster/OpenGL/Vulkan.
Свежие NuGet-пакеты прошли проверку независимых потребителей.
[Приёмка и границы](HISTORY.md#skiasharp-41570-2026-11-18).

<a id="ui-testing-guidance"></a>
## UIQA-DOC — методика проверок UI

Документация выполнена 16.11.2026: [UI_TESTING.md](UI_TESTING.md) связывает
существующие UiSession/input, Window host contracts, Skia raster и native scopes.
Навигация добавлена в AGENTS/docs README; GOLDEN_TESTS сохраняет подробные правила
шрифтов, pixel tolerance и baseline. Методика применима к consumers без Computer Use.
Проверка документации: сигнатуры/ownership сверены с public API и существующими
tests; относительные ссылки проверены. Новых build/test/native результатов нет.
Public API, пакеты и функциональная очередь этим этапом не меняются; новый общий
host/driver не реализован. Документация не требует повышения общей Version.

<a id="uiqa-agent-checks"></a>
## UIQA. Инструменты проверки UI/UX для агентов — 2026-11-22

Запланировано как часть [ENG9b](../engine/PLAN.md#eng9b-scenario-testing).
Здесь ведутся диагностика и управление UI через Alloy. Общий CLI, сценарий,
отчёт и игровой профиль ведутся в Aether. Существующая
[методика](UI_TESTING.md) задаёт уровни доказательства.

1. **UIQA.1 — семантический снимок после layout.** Реализован и проверен на
   обозревателе и инспекторе. `UiSession.CaptureSemantics()` копирует дерево с
   устойчивыми ID заявленных целей, эффективным состоянием, логической геометрией,
   клипом и порядком отрисовки. Снимок фиксирует фокус, захват, popup `OverlayHost`,
   viewport и поколения дерева и layout. Доступные имена включаются явным флагом.
   Превышение лимита узлов отмечается `IsTruncated`.
   [Контракт](UI_TESTING.md#ui-semantic-snapshot) · [Проверки](HISTORY.md#uiqa1-semantic-snapshot-2026-11-24).
2. **UIQA.2 — поиск цели и воспроизведение ввода.** Поиск цели по ID и
   защищённая маршрутизация событий реализованы. На обозревателе и инспекторе
   проверены устаревание снимка, перекрытие, отключение, текст, клавиши,
   прокрутка, закрытие popup и отмена захвата при потере фокуса.
   [Контракт](UI_TESTING.md#ui-semantic-input) · [Проверки](HISTORY.md#uiqa2-semantic-input-2026-11-24).
   [Сценарий Editor](../engine/SCENARIO_TESTING.md#editor-scenario) использует
   маршрутизацию по ID и ожидание завершения операций с дедлайном. Поиск цели
   по роли остаётся отдельным расширением UIQA.
3. **UIQA.3 — проверка визуальных состояний.** Первый профиль проверен
   26.11.2026 на инспекторе Transform. Raster в Light/Dark на 1×, 1,25× и
   1,75× подтверждает клип текста, отсутствие наложения на соседнее поле,
   мягкий перенос ошибки, фокус и Tab, отключение Apply и отмену правки.
   Нативное окно Editor/OpenGL повторяет фокус, Tab, ошибку, Apply и отмену
   в обеих темах. [Воспроизведение](../engine/SCENARIO_TESTING.md#editor-visual-states) ·
   [Результаты](HISTORY.md#uiqa3-visual-states-2026-11-26).
   Снимки помечаются backend, масштабом и темой. Для эталонов действует
   [GOLDEN_TESTS](GOLDEN_TESTS.md); этот профиль не меняет эталоны.
   Физический ввод, IME, OS accessibility и multi-monitor DPI проверяются
   в отдельных профилях. UX-оценка читаемости и удобства использует кадры
   и результаты взаимодействия.

Выполнено2026-11-15: **SkiaSharp/native4.156.1 +RichTextKit0.6.173**, exact parent/gitlink,
GENERAL/HOST patch/ABI1/48bytes сохранены. Native source/published GPU/typography и
fresh10package40case delivery прошли; полные UI/Hosting/Editor suites D/R прошли.
Единственная детальная dependency задача — [Core DEP1](../core/PLAN.md#dep1-dependency-refresh),
[результаты/SHA](../core/HISTORY.md#dep1-2026-11-15). Functional UI/release очередь прежняя.

<a id="skiasharp-41560-completed"></a>
Выполнено2026-11-12: **SkiaSharp/native4.156.0** — exact source pins, extended
Windows x64 DLL и GENERAL/HOST patch, ABI1/48bytes. Native build recipe учитывает
M156 default MSVC/Spectre, installed Windows SDK и explicit upstream NuGet config.
Source/published5-mode native profiles validation0/0; Alloy/Hosting/Editor D/R,
solution Debug и fresh10-package/40-process delivery проверены.
[Manifest/команды/SHA](SKIASHARP41560_RESULTS.json),
[история/ограничения](HISTORY.md#skiasharp-41560-2026-11-12).
Функциональная UI очередь и physical/K7/K8/release gates не меняются.

Текущий build/test профиль CORE8 (2026-11-11): net11.0, SDK 11.0.100, runtime-async=on, xUnit 2/VSTest. Он заменяет исходный net10 профиль для новых запусков, сохраняя исторические результаты и функциональные критерии. [Переход и границы](../core/DOTNET11_MIGRATION.md).

Конфигурация2026-11-10: [CORE7.5](../core/CONFIGURATION_MODULES.md) добавил
Alloy:Window scalar schema/UseConfiguredPresentation и Editor:Window defaults.
Renderer/fonts/chrome factories остаются explicit; UI/native очередь не меняется.
Детальная задача и configuration evidence принадлежат Core.

<a id="skiasharp-41550-completed"></a>
Выполнено2026-11-07: **SkiaSharp/native4.155.0** — pinned source build extended DLL,
ABI v1/48bytes, source Debug/published Release Vulkan readback/composition и backend
comparison, fresh10-package/40-process delivery, Alloy/Hosting/Editor D/R regression.
Solution Debug build0errors, новых warnings0; [результаты и команды](HISTORY.md#skiasharp-41550-2026-11-07).
На дату этой проверки ближайшей задачей был PA8c.0.


### Основания прежней очереди (ноябрь 2026)

UIA2 Window и UIA3 compact/numeric editors реализованы в проверенных scopes.
ED4g.3 group reset и ED4g.4 rad/deg завершены без новых generic controls;
ED5a camera/picking завершён в bounded scope, далее ED5b translation contract.
Общие capabilities добавлять по этому consumer, не переносить document/history/
angle-unit policy в Argentis и не ставить выполненный Window повторно как feature.

Закрыто в текущем UI потоке: [PA8c.3a](#pa8c3a-system-fonts) и
[PA8c.3b](#pa8c3b-default-fonts) — системный каталог, веса и default Variable Text
во фреймворке/Editor; повторное подключение шрифтов не является следующей задачей.
[Контракт ED5a.0](../engine/EDITOR_CAMERA_SELECTION.md#ed5a-view-contract-v1) зафиксирован;
ED5a.1 values и ED5a.2 versioned transport/actual receipt реализованы в Aether/Editor;
ED5a.3/4 navigation и bounded acceptance завершены; далее ED5b translation contract.
UIA3d реализовал validation/shadows; UIA3d.1 исправил Vulkan frosted barrier и проверен отдельно.
Детальные Editor задачи и acceptance — в Editor плане.

Последующая работа по типографике и архитектуре изменила порядок задач;
актуальные приоритеты и незакрытые условия приведены в [текущей очереди](#current-ui-queue).

<a id="uia3e-split-view"></a>
## UIA3e. Разделение панелей — реализовано и проверено 2026-11-09

Владелец реализации — Alloy. [Результаты проверок](HISTORY.md#split-view-2026-11-09).
Детальный план ведётся только здесь. Две retained ContentControl-панели и focusable
divider; horizontal/vertical axis, preferred first-pane logical size (NaN = half).
Axis Min/Max на pane hosts; при дефиците места minima сжимаются пропорционально,
конечные maxima оставляют trailing space. Resize не перезаписывает preference.

- [x] Реализовать ownership/Fluent/typed properties, layout и themed divider без
  file IO, collapse, новых DI сервисов или пересоздания детей.
- [x] Pointer capture/release outside, Escape/focus/capture/visibility/enable/
  detach/dispose cancellation; внешняя preference прерывает gesture без rollback.
- [x] Tab, axis arrows, Shift x10, Home/End и accessible name; unrelated input
  остаётся consumer. Контролы/поля сохраняют draft/selection/scroll/theme state.
- [x] Адресные layout/input/error/ownership тесты и UI/Hosting/Editor D/R regressions.
- [x] Cobalt App/IFactory demo, source/published native consumer и independent fresh
  package consumer; actual scope/results в STATUS/HISTORY. Routed input не доказывает
  physical input/OS capture/DPI. Studio consumer обновляется его владельцем.

<a id="uia3d-validation-shadows"></a>
## UIA3d. Значок валидации и тени поверхностей — 2026-11-08

Реализовано и проверено перед ED5b в указанном scope. Детальная задача
ведётся здесь; Editor — consumer существующих моделей validation/draft.

- [x] TextBox/RichTextEditor и внутренний TextBox NumberBox: красный геометрический
  значок справа у границы при HasError, отдельное ErrorMessage от валидатора;
  текст/каретка не заходят под значок. Промежуточный ввод не нормализуется.
- [x] Наведение именно на значок показывает delayed tooltip с полным сообщением,
  переносом строк и границами viewport. Без смены focus/capture/selection; закрыть
  при уходе/input/focus loss, снятии/смене ошибки, hide/detach/dispose/layout change.
  Общая копируемая диагностика инспектора сохранена; inline error rows заменены.
- [x] Общий immutable shadow value и свойство оформления; Theme.PanelShadow и
  MenuShadow. Skia рисует Gaussian outer shadow с offset/radius до содержимого,
  вне собственной границы, внутри ancestor clip; opacity применяется ко всему.
  Панели Editor и MenuPopup получают themed shadows; local null отключает эффект.
- [x] Не расширять layout/hit area, не затенять прозрачную середину/backdrop,
  не создавать per-frame retained GPU resources или idle animation. Legacy drawing
  contexts без optional shadow capability продолжают работать без тени.
- [x] Проверки: icon/text geometry и retained editing, exact tooltip/error lifecycle,
  theme/style precedence, clipping/opacity/DPI1/1.5/2 raster, D/R UI/Hosting/Editor,
  source/published visual proof. Фактический scope отдельно в STATUS/HISTORY.

UI1170/Hosting154/Editor1412 каждый D/R, source Debug/published Release3backend
matrix и fresh4package raster consumer прошли. Первоначальный native gallery menu
был opaque; последующий UIA3d.1 frosted gate ниже закрыт. [Команды/границы](HISTORY.md#validation-shadows-2026-11-08).

<a id="uia3d3-textbox-line-fit"></a>
### UIA3d.3. Полная строка в компактном TextBox — 2026-11-08

Реализовано и проверено по screenshot пользователя: fixed Height32 с default Padding8 обрезал
низ строки default Segoe UI Variable Text16/600 (line box больше16).

- [x] При недостатке внутренней высоты уменьшаются эффективные вертикальные отступы,
  сохраняя полный измеренный line box, центровку, горизонтальный clip и error badge.
  Не менять font size, fixed Height, draft, caret/selection или обрезку за Bounds.
- [x] Exact raster regression против просторного поля с тем же текстом/шрифтом: default,
  error/selection, NumberBox, Dark/Light и synthetic1/1.5/2. Auto-height и asymmetric
  padding сохранить; физически слишком низкое поле всё равно ограничено Bounds.
- [x] Обновлённые source Debug/published Release3backend previews, D/R UI/Hosting/
  Editor проверки: UI1184/Hosting154/Editor1412 каждый D/R,18PNG/12comparisons и
  Vulkan0/0/profile. [Причина/проверки/границы](HISTORY.md#textbox-line-fit-2026-11-08).

<a id="uia3d2-validation-badge"></a>
### UIA3d.2. Квадратный блок валидации и аудит полей — 2026-11-08

Реализовано и проверено в bounded scope; прежний значок UIA3d заменён
сплошным блоком цвета ошибки справа, стороной в высоту поля. В узком поле блок
ограничен границами контрола. Контрастный символ — Theme.OnError; tooltip работает
на всём блоке, focus и текст остаются в редактируемой части.

- [x] TextBox/RichTextEditor, auto-height/wrapping и NumberBox; local/style priority,
  draft, selection, focus и tooltip lifecycle сохранены.
- [x] Аудит потребителей: исправлены пропущенные сообщения/привязки;
  отклонённый ввод отделён от общей диагностики команд и read-only текста.
- [x] Геометрия/контраст/input regression, D/R suites, source/published raster/GL/Vulkan
  previews; UI1176/Hosting154/Editor1412 каждый D/R, fresh5package Vulkan6cases D/R.
  [Аудит/tests/границы](HISTORY.md#validation-badge-2026-11-08).

<a id="uia3d4-control-glass"></a>
### UIA3d.4. Стеклянный стиль и скруглённые поля — 2026-11-21

Общий стиль распространяет матовую поверхность на оформленные контролы.
Ранее размытый фон панели используется и её дочерними контролами, которые
рисуют собственную тонировку. Явные материалы, цвета и непрозрачное
оформление поддерживаются через существующий приоритет. Ввод получает радиус
5 и отдельную скруглённую плашку ошибки внутри рамки.

Критерии среза — сохранение области наведения, подсказки и черновика, точная
геометрия и защита углов от заливки на Light/Dark, raster/OpenGL/Vulkan при
1×, 1,25× и 1,75×. Отдельно проверить повторные слои размытия и непрозрачный
fallback. [Правила оформления](DESIGN.md#матовое-стекло).

<a id="uia3d5-window-underlay"></a>
### UIA3d.5. Цветная подложка окна — 2026-11-21

Градиент рисуется в стандартной рамке и пустом viewport Editor до матовых
панелей, текста и значков. Light и Dark используют разные палитры. Тема может
выбрать свои три цвета или сплошную заливку. Отрисовка игрового кадра сохраняет
свой исходный фон. Статичный градиент не планирует дополнительные кадры.

Критерии среза: градиент виден в открытой области и даёт разные цвета после
тонировки в двух точках панели, значок ошибки сохраняет свой цвет, явный фон
рамки и отключение градиента работают. Матрица — raster/OpenGL/Vulkan, Light/Dark,
1×, 1,25×, 1,75×. [Правила оформления](DESIGN.md#матовое-стекло).
[Проверки и кадры](HISTORY.md#uia3d5-window-underlay-2026-11-21).

<a id="uia3d1-vulkan-backdrop"></a>
### UIA3d.1. Vulkan frosted backdrop — исправлено и проверено 2026-11-08

- [x] Native cause установлен: pinned GrVkImage GENERAL access включает HOST_WRITE,
  source stages были ALL_COMMANDS без HOST. Добавлен HOST к GENERAL source stage,
  access/остальные layouts/ABI/ownership сохранены. Reproducible patch включён в
  native apply/build scripts только для pinned4.155.0; DLL+metadata установлены.
- [x] Actual blur witness на чёрно-белой подложке и frosted default gallery;
  source Debug/published Release3backend Dark/Light1/1.5/2 прошли18PNG/12comparisons
  и6Vulkan validation0/0 reports каждый. Opaque/no-shadow controls также прошли;
  старая DLL дала прежний VUID, disabled-blur control отклонён pixel witness.
- [x] ABI/readback/return18 и composition36/leases18 с validation0/0; UI1170,
  Hosting154, Editor1412 каждый D/R; fresh5package Vulkan consumer D/R по6cases,
  native SHA совпал. [Команды/хеши/ограничения](HISTORY.md#vulkan-backdrop-2026-11-08).

Default backdrop/blur/shadow сохранён. Нет suppression validation/CPU fallback.
Wider K7/device-loss/VRAM/physical/swapchain и полный release остаются отдельно.

<a id="pa8c3a-system-fonts"></a>
## PA8c.3a. Opt-in системные шрифты и веса — 2026-11-07

**Статус: завершён в проверенном scope.** Прежняя opt-in граница этого этапа
сохранена ниже как решение07ноября; текущий default определён PA8c.3b.

Принято и реализовано по прямому запросу пользователя: отдельные family/weight/
italic в UI, общий measure/draw/cache путь; явная policy requested family →
ordered system families → file catalog/system default. Файловые каталоги остаются
детерминированными; default typography/hinting/AA не меняются. Изменение ограничено
Argentis/Skia и existing Hosting catalog overloads, без зависимости от Nom.

До правок сохранены и просмотрены исходные raster font samples/layout1/1.5/2;
прежние pixels/layout после изменения совпадают. Acceptance этого bounded среза:
style/theme/local invalidation,400/600/italic, fallback order/immutable config,
same-font metrics/pixels/caret, Rich/Bold/Undo, bounded ownership, source Debug и
published Release3backends, independent package-only consumer. Проверено в этих
scopes; [точные результаты и команды](HISTORY.md#system-fonts-2026-11-07).

Это не закрытие всего PA8c.0/3/5/6: arbitrary variation axes/automatic opsz,
per-glyph fallback/shaping для plain text, physical DPI, performance/VRAM и wider
package GPU/Hosting acceptance остаются отдельными задачами. Выбор default отдельным opt-in стилем был границей PA8c.3a;
текущий framework default принят и проверен в PA8c.3b.

<a id="pa8c3b-default-fonts"></a>
## PA8c.3b. Общая default typography и Editor — 2026-11-08

**Статус: завершён в описанном source/publish/package scope.** Более широкая
PA8c/physical/release acceptance остаётся у своих этапов.

По прямому запросу пользователя принято продолжение PA8c.3a: FontDefaults.Family=
Segoe UI Variable Text для Theme/UiProperties/новых controls и Rich fallback;
SystemDefault: requested → preferred → Segoe UI → system default. Явный file
catalog сохраняет своё назначение. В Editor host/пример добавлен Noto/OFL final
fallback; --font примера остаётся явным file-only override. Это меняет прежнее
решение PA8c.3a оставить смену default отдельному consumer; baseline сохранён.

Custom caption/hint/status/viewport согласуют measure/draw weight/italic, palette
switch сохраняет все4typography параметра. Semibold600 — scoped title styles,
основной текст regular400. Default policy не владеет native resources; прежние
App/Cobalt/factory/surface lifetimes, file catalogs и native ABI сохранены.

Реализовано и проверено в [указанных source/publish/package scopes](HISTORY.md#default-fonts-2026-11-08):
Alloy1126/Hosting154/Editor1300 каждый D/R,4native Editor profiles и default
3backend gallery. Automatic opsz, plain glyph shaping, full physical/release/
performance acceptance не закрыты этим срезом. Детальный task ведётся здесь;
[Editor plan](../engine/EDITOR_PLAN.md#editor-default-typography) ссылается на него.

<a id="uia3c-numeric-gestures"></a>
## UIA3c. Numeric step/drag — 2026-11-05

NumberBox получает opt-in checked Int64/finite Real step и pointer capture gesture,
явные Completed/Cancelled/Interrupted outcomes, точный string rollback, consumer
admission/identity gates и cleanup при interrupt/callback failure. Общие styles/
types/ownership без engine dependency; прикладной proportional Scale и history —
[ED4g.2](../engine/EDITOR_PLAN.md#ed4g2-numeric-gestures). Контракт ввода и цветовые
состояния — в [дизайн-коде](DESIGN.md#numeric-gestures). Готовность подтверждается
arithmetic/routed input/capture/dispose tests и actual Editor transaction proof;
native/GPU/package/physical gates сохраняют свой scope.

Native follow-up [ED4g.2a](../engine/EDITOR_PLAN.md#ed4g2a-native-numeric)
проверен в source Debug/published Release standard/Cobalt и embedded/MS DI.
Platform.Silk сохраняет ordered cursor position для button/wheel input и передаёт
Shift/Ctrl в pointer events. Windows drag/release/Esc/Ctrl+Enter/Undo подтверждены
отдельно от routed arrows/captured cancel. Physical arrows, Esc во время удержания
кнопки и pointer modifiers остаются отдельными gates; ED4g.3/4 group reset/angle units завершены, далее ED5a.

## UIA3. Компактные редакторы и семантические цвета — 2026-11-05

Продолжение UIA3b: общий PropertyEditorRow с adaptive label/value/accessory,
ограниченная автоматическая высота RichTextEditor, реальный CheckBox indicator
для общих color/state roles и read-only activation contract. Прикладные generic
Number/Integer/Boolean/String/nullable consumers принадлежат
[ED4g.1](../engine/EDITOR_PLAN.md#ed4g1-generic-editors). Типы схем/engine не расширяются.


По согласованному дизайну инспектора принимается [дизайн-код](DESIGN.md).
Цветовые роли редактора и состояния границы независимы; значения принадлежат Theme.
Общие `NumberBox`/`Vector3Editor`, pending-edit state и palette tokens развиваются
здесь; Transform/history/schema integration — в [ED4g](../engine/EDITOR_PLAN.md#ed4g-compact-transform).
Критерии: равные адаптивные колонки, центрированный текст, retained draft/focus/
selection/theme, явный state priority/override, error marker и отсутствие idle work.
Drag/step/linked-scale, success/busy animation и hardware/release gates отдельны.


Уточнение INF5,2026-10-31: optional `IUiRenderDiagnostics`/`UiRenderDeviceInfo`
передаёт cached value-only device evidence из host в surface. OpenGL/Vulkan host
снимает данные при initialization; getter не делает native calls, IO или waits и
не является проверкой текущего GPU health. External raw hosts могут оставить
DeviceInfo=null; raster не получает фиктивный device. Editor копирует строки,
не сохраняет renderer/handles в модели. [Scope и evidence](HISTORY.md#inf5-cached-device-observation-для-editor--2026-10-31).
INF5 и общие K1–K9 остаются частичными; новый report consumer не закрывает release.

План восстановлен 2026-09-27 из предоставленного пользователем текста предыдущей сессии. Сохранены цели, архитектурные решения, последовательность работ, проверки и границы первой версии. Идентификаторы пунктов добавлены для отслеживания выполнения.

## Область плана — уточнение формата 2026-10-09

Это локальный план UI направления **Alloy / Argentis**: TrueMoon.Argentis, TrueMoon.Alloy, Rendering.Skia, Platform.Silk, Alloy.Hosting, их compatibility path, UI tests и manual consumers. Общие задачи bootstrap/DI/IPC/units, build всего solution и общий выпуск относятся к [общему плану TrueMoon](../PLAN.md) и планам Core/Cobalt/Enerit/Mithril.

Разделение документов не меняет UI цели, архитектуру, этапы1–5, K1–K9, PA1–PA10, net10.0/xUnit или требования alpha. Общий build и ошибки других модулей учитываются в [общем статусе](../STATUS.md); UI хранит собственные проверки и влияние общих зависимостей на интеграцию. Подробные прежние записи сохранены в [HISTORY.md](HISTORY.md), текущая сводка — в STATUS. Правила ведения — [PLANNING.md](../PLANNING.md).

Текущее состояние, доказательства и точка продолжения находятся в [STATUS.md](STATUS.md). Наличие пункта в этом документе означает требование к результату; статус реализации определяется отдельно.

## Дополнительный план улучшения инфраструктуры Alloy — 2026-10-18

### Runtime HUD Aether — 2026-10-29

По запросу пользователя HUD игрового Runtime строится на Argentis/Alloy.
Launch roles и игровой consumer/acceptance имеют одного владельца:
[ENG7b](../engine/PLAN.md#eng7b-режимы-запуска-editor-и-player--2026-10-29) и
[ENG5c.4](../engine/PLAN.md#eng5c4-runtime-hud-поверх-alloyargentis--2026-10-29).
Это планирование, не новая реализация UI/API или закрытая HUD acceptance.

Общие UI задачи этого consumer:

- Подключение через App/UseAlloy и existing external-host/factory contracts;
  retained View<TData>, отдельные Data/VM/IViewStyle/Theme по UIA1. HUD owner —
  runtime session; window/device/loop принадлежат host. UI не создаёт второе окно
  или вложенный App, не удерживает borrowed dependencies через disposable aliases.
- Native texture handoff/composition через существующие raw Vulkan targets и
  sequential acquire/return/completion, с alpha/resize/zero-size/teardown proof.
  Конкретный engine compositor hook определяется в ENG5c.4.1; Alloy/Argentis
  не получают зависимости на Aether World, game commands или Editor.
- HUD/game routing: handled/capture/focus/modal semantics, logical scale mapping,
  cancellation/reset transient input и owner-thread dispatch. UI продолжает
  обрабатывать ввод/обновления при paused simulation; игру UI clock не двигает.
  Runtime adapter и bounded game snapshots/commands остаются у engine consumer.
- Смена session/scene, failed creation/draw/dispose и retry проверяются на real
  assembly; theme/style/bindings обновляют существующее дерево. Реализовать лишь
  выявленные consumer gaps, сохранив regression external host/обычного окна.

HUD/game composition едина для Player и Editor Play; runtime child экспортирует
составной кадр, Editor не строит вторую копию игрового HUD. Aether core остаётся
без UI dependency, подключение выполняет optional runtime integration/host.
Первый HUD не требует весь UI beta или async PA9/multiple frames-in-flight.
K1–K9/PA/INF и net10.0/xUnit сохраняют отдельные критерии; низкоуровневый Skia
interop proof не заменяет integrated HUD/input/lifecycle proof.

### Поддержка рабочего приложения Aether.Editor — 2026-10-22

Решение2026-10-27: целевой TrueMoon.Aether.Editor — library/package, executable
host подключает reusable View и выбирает поддержанный window/backend/resources.
ARCH2b перевёл Editor в library/App module; bounded package/native
[ARCH2](../engine/PLAN.md#arch2-aether-и-editor-как-пакеты--2026-10-27) проверен2026-10-29:
standard/Cobalt и embedded/MS DI, source/package build/publish, native24/24.
Hosting cleanup callback захватывает borrowed lifetime до teardown; no late Resolve.
Engine ARCH2e.4 scoped contract проверен; далее ED6a/ED8a consumer; детали доказательств — в STATUS/HISTORY владельцев.
Editor panels/styles/commands остаются в Editor; общие controls/input/layout —
здесь. Изменение формы поставки не закрывает и не расширяет K/PA/INF критерии.

Уточнение2026-10-23: редактируемые inspector/tools подключаются после
[минимального authoring API, headless proof и Edit preview Aether](../engine/PLAN.md#основа-api-перед-редактируемым-ui--2026-10-23).
ED1a project/session идёт параллельно; IPC Editor↔Runtime использует Enerit,
не становится новым UI transport. Полный движок и весь UI beta не являются
предварительным условием первого среза.

Согласован [прикладной план ED1–ED8](../engine/EDITOR_PLAN.md). Здесь остаются
общие UI capabilities и их acceptance; прикладной checklist не дублируется.
ED4 использует overlays/dialogs, command/menu/shortcut, tree/selection и forms
conversion/validation/commit/cancel. ED5 нуждается в input/focus/capture и viewport
tool integration. ED7 проверяет custom widgets через Element и backend-independent
drawing/input contracts, а также конкретные capability interfaces по потребителям.

IEditorCommand/panel/property-editor/tool contracts принадлежат пакету Editor;
пользовательские UI controls — Argentis/Alloy. Сам IButton не заменяет Element:
переход runtime на полностью интерфейсное дерево не назначен. Начать с минимальных
capabilities, необходимых первому Transform/preview/Undo/save срезу, сохраняя UIA1,
net10.0/xUnit/ownership и существующие PA/K критерии. Это документационное
планирование без новых code/API или текущих passing результатов.

<a id="uia2-window"></a>

### UIA2: Window как компонент Argentis — 2026-11-03

По запросу пользователя реализованы Window/Window<TData> как provider-neutral root
поверх существующих View/content/style и HostedUiWindow. Window<TData> наследует
Window и реализует IView<TData>; отдельное владение моделью не вводится. Argentis
не получает зависимости от Alloy/Silk/Skia/App. WindowFrame/WindowTitleBar и native
ownership остаются прежними; вложенный Window не создаёт самостоятельное окно.

- Title и ClientSize — typed properties с обычным приоритетом local/binding →
  element style → scoped view style → default. ClientSize: whole logical pixels
  [1,32768], не обещание предела конкретного GPU; Element.Width/Height — layout.
- WindowPresentationState/IsActive/ActualClientSize — наблюдение, не команды и не
  перезапись requested size. Actual size допускает fractional DPI и zero/minimize.
- IWindowController заимствуется через одну WindowHostConnection; root должен быть
  fresh/detached. UI owner/lifetime fences предшествуют native side effects; stale
  connection не может публиковать state в новый host. Controller/VM не dispose-ятся.
- Minimize/Maximize/Restore/RequestCloseAsync и Closing используют существующую
  host policy. Component close callbacks перед host callbacks, snapshot на attempt;
  Application/Native veto/error оставляют окно, Shutdown drains all/ignores vetoes.
  Caller cancellation отменяет wait, а не accepted attempt/actual drain.
- HostedUiWindow создаёт root до native/graphics allocation. Доступные до attach
  Title/ClientSize, включая defaults, имеют приоритет над host settings; chrome
  limits проверяются до HWND. Стартовые значения задавайте в конструкторе или
  фабрике, не через отложенный стиль, `Content(...)` или attach-привязки.
  Фабрика не должна требовать current graphics context.
  Native/platform/backend настройки остаются в Hosting options.
  Запрет вложенных `Window` проверяется и до HWND, и после подготовки отложенного
  дерева перед attach. `Window` в content-слоте рамки хоста допустим только
  как исходный корень презентации; в заголовке он недопустим.
- Полная fresh root ownership при failed startup; foreign trees не освобождаются.
  Disconnect предшествует tree/native dispose. Host добавляет одну existing frame,
  синхронизирует native/default painted title и effective style/binding properties
  без пересоздания content. Existing ordinary View path проверять отдельно.

Acceptance: direct Argentis component/connection checks, Hosting property/ownership/
close integration, Cobalt factory compatibility, native OpenGL/Vulkan source Debug/
published Release, затронутые packages и actual cleanup. Modal/dialog/window-manager,
новые renderer/platform APIs и hardware/release gates не входят в этот профиль.
Фактические результаты и текущие ограничения — в STATUS/HISTORY.

Состояние2026-11-04; исходный component run2026-11-03 и новый Editor consumer
различаются по native/backend scope:

| Подэтап | Состояние и граница вывода |
| --- | --- |
| UIA2a: component/connection/Fluent | Реализовано;57новых direct cases входят в full Alloy976/976 Debug/Release |
| UIA2b: Hosting properties/startup/close | Реализовано;37новых cases входят в full Hosting154/154 Debug/Release; сохранён ordinary View путь |
| UIA2c: source App/native regression | Проверено в Windows x64/net10/SDK10.0.403: source Debug/published Release, OpenGL/Vulkan и App Cobalt/MS DI, по4/4cases; Editor1062/1062 отдельно Debug/Release — regression старого consumer |
| UIA2d: package delivery | Independent fresh NuGet Window App consumer проверен через standard Editor/Cobalt; embedded/MS DI regression отдельно. Fresh15packages/headless8/8, published Release2/2 WindowRoot workflows. Это закрывает targeted delivery profile, не все Window backends |
| UIA2e: validation/остальные режимы | В новом Editor OpenGL parent/Vulkan child profile временный official validation layer доступен:78graceful reports0errors/0warnings. Старый plain Window Vulkan UI-presenter run не переобъявляется validated; physical/hardware/K7/release gates остаются отдельными |

[Команды и результаты исходного component run](HISTORY.md#uia2-window-component--2026-11-03),
[новый Editor consumer](HISTORY.md#uia2-editor-window-consumer-2026-11-04).
Текущие full Alloy976/Hosting154 отдельно Debug/Release прошли; это собственные
suite results, не вывод из Editor tests. Следующие wider Window backend/validation
checks и PA8c.0/physical/hardware/K7/release gates сохраняют отдельный scope.

Прикладной consumer стандартного окна Aether.Editor реализован:
[ED4f.3](../engine/EDITOR_PLAN.md#ed4f3-window-root). Его задачи и acceptance ведутся
только в Editor плане. EditorView сохраняет reusable View contract для embedded
hosts; headless Aether и offscreen runtime/HUD не получают Window dependency.
Standalone Player рассматривает Window в своём host composition/ENG7b.2, сохраняя
одного владельца native window/device. Editor consumer не меняет общего Window API
и не является доказательством готовности HUD/Player.

### UIA1: View, Data/ViewModel и scoped styles — 2026-10-22

По запросу пользователя реализована штатная модель авторинга: `View<TData>` с
Fluent-композицией, отдельные optional Data/ViewModel и `IViewStyle<TView>`.
Argentis предоставляет provider-neutral style contracts/builders; Alloy сохраняет
retained tree, attachment/binding/thread ownership. Первый production consumer —
Aether.Editor: EditorViewModel/EditorViewStyle, DI-created owner-thread View.
Правило закреплено в AGENTS; новые public типы находятся в корне Argentis согласно
назначению и namespace. Общий детальный UIA1 план ведётся здесь, engine ссылается.

Контракты: Root и typed Part выбирают стабильные части текущего View; typed Role
покрывает существующие/будущие элементы, совпадает по identity token. Ближайший View
является строгой границей, включая корень вложенного View. Последнее совпавшее
правило выигрывает в пределах view layer; local/binding → direct Style → view style
→ Theme → default. WithViewStyle заменяет/снимает слой; старые значения не остаются.
Тематические значения вычисляются до commit. Declaration/value failures сохраняют
предыдущий слой/Theme, observer failures наблюдаемы после commit. Immutable
declarations переиспользуются, per-view builders замораживаются после Configure.

ViewModel хранит UI-independent state/actions/validation; обычным данным не нужен
обязательный base class. View подключает bindings и визуальное представление
состояния, владеет подписками и элементами. DI factories/styles/модели заимствуются
окном/деревом по lifetime. Style не удерживает live View и не запускает команды.
Theme/style обновляются без rebuild; dynamic roles пересчитываются при adopt/orphan.

| Этап | Критерий |
| --- | --- |
| UIA1a | Typed contracts/root/part/roles, precedence/order/replace/remove/scope/guards с direct tests |
| UIA1b | Theme switch и dynamic transfer без утраты tree/input/selection/scroll/bindings; failed value atomicity |
| UIA1c | EditorViewModel/Style и Fluent View через DI; notifications/commands/ownership tested без renderer |
| UIA1d | Debug/Release suites, native Editor/published proof, fresh packaged API consumer и docs/agent rules |
| UIA1e | Общие Bindable → ViewModel и переиспользование property notifications в Editor/sample; direct и binding regression tests |

UIA1a–d выполнены2026-10-22 в указанном scope: UI661/Editor49, full2321/2321
Debug/Release, nonincremental Debug build, native Debug/published Release и fresh
14packages/4consumer proof. Датированные команды/ограничения — в HISTORY/STATUS.

Полный CSS cascade, selector language, implicit DataContext rebinding, templates,
animations и массовая migration consumers сохраняют собственные будущие задачи.
Фактические passing результаты отдельно в STATUS/HISTORY; UIA1 не закрывает
physical DPI/K3/K7/K8/VRAM/final alpha или ENG7 property editing.

### UIA1e: Bindable и ViewModel — 2026-10-31

По запросу пользователя общая иерархия observable presentation — `Bindable` →
`ViewModel` → конкретная модель. Оба abstract public класса находятся в корне
TrueMoon.Argentis, без новых assembly/provider/App dependencies. Обычные данные
по-прежнему могут использоваться без base class; observable data без роли ViewModel
могут наследовать непосредственно Bindable. Binding API принимает прежний
INotifyPropertyChanged и не требует этой иерархии от внешнего consumer.

- [x] Bindable реализует event и protected SetProperty(ref field, value, propertyName):
  EqualityComparer<T>.Default, bool change result, CallerMemberName, commit перед
  синхронным event; одинаковое значение не заменяет stored instance и не уведомляет.
- [x] Protected virtual OnPropertyChanged позволяет сообщать computed/dependent
  properties; explicit null/empty означает все properties. Observer failure
  распространяется после commit. UI dispatch/synchronization остаются у владельца.
- [x] ViewModel наследует Bindable без дополнительного service/lifecycle API.
  Protected ClearPropertyChangedSubscribers освобождает текущие observers при
  cleanup derived owner; зависимости/повторные подписки/Dispose не управляются базой.
- [x] Шесть Editor ViewModels используют общую базу, InspectorProperty — Bindable;
  Transform наследует её через Inspector. Partial notifications, dependent HasError,
  owner-thread guards и прежний cleanup сохраняются. SettingsModel/SettingsRow sample
  демонстрируют оба уровня. Diagnostic test doubles/custom subscription counters
  сохраняют самостоятельную реализацию интерфейса по назначению.
- [x] Direct tests проверяют hierarchy, caller name, committed value, equality/null,
  observer failure/unsubscribe, virtual hook, clearing observers и retained two-way
  binding; полные UI/Editor suites выполнены отдельно Debug/Release.

Команды/результаты/границы — [HISTORY](HISTORY.md#uia1e-bindable-и-viewmodel--2026-10-31).
Package/native/physical проверки этого изменения отдельным прогоном не выполнялись.

<a id="uia1f-minimal-view-authoring"></a>
### UIA1f. Минималистичная композиция View — завершено 2026-11-23

Из одного метода Fluent-композиции должны читаться порядок и вложенность
блоков, подписи, привязки, действия и условные состояния.
Минималистичность относится к исходному коду. Визуальную плотность, типографику
и состояния определяет [дизайн-код](DESIGN.md). Композиция остаётся C# поверх
retained Argentis. Контролы объявляются там, где они стоят в дереве. Ссылка на
конкретный экземпляр нужна, когда к нему обращается поведение или стиль.
Повторяющиеся элементы оформления получают семантические роли. Согласовано
23.11.2026: композиция и поведение нового View по умолчанию в одном C# файле.
В пилоте разметка была в `BuildContent()`. По [UIA1f.11](#uia1f11-contextual-content)
разметка находится в фабрике `Content(...)` в конструкторе, обработчики ниже. Простые действия
в одну-две строки можно объявить лямбдой в дереве. Крупная повторяемая часть может
стать отдельным компонентом. Отдельный `*.Design.cs` и `partial` для генерации
не требуются. ViewModel хранит состояние и действия, `IViewStyle<TView>`
и Theme — оформление. Подписки и ввод остаются ответственностью UI-владельца.

**Правило композиции, 25.11.2026.** Все зависимости прикладного UI и их фабрики
предоставляет DI приложения. View объявляет, какие компоненты нужны, но не
выбирает конкретный сервис, реализацию фабрики или презентер. Для повторяемого
элемента внедряется фабрика, а данные строки передаются ей при создании.
[Инструкция для новых и перерабатываемых экранов](../../AGENTS.md#ui-composition-di).

**Результаты чтения исходников 22.11.2026**

- [EditorDocumentView](../../TrueMoon.Aether.Editor.UI/Documents/EditorDocumentView.cs)
  уже задаёт порядок блоков через `Content(...)` и `WithChildren(...)`.
  В Argentis есть типизированные `Bind`/`BindTwoWay`, scoped Part/Role и
  `BindItems`, который сохраняет строки по ссылочной идентичности объектов.
  [Fluent API](../../TrueMoon.Argentis/ElementExtensions.cs) ·
  [Style builder](../../TrueMoon.Argentis/Styling/ViewStyleBuilder.cs).
- [EditorHierarchyView](../../TrueMoon.Aether.Editor.UI/Hierarchy/EditorHierarchyView.cs)
  собирает панель в двух partial-файлах. `HierarchyLayout` использует индексы
  детей, прижимает кнопку вправо и выделяет прокрутке остаток высоты.
  `RenameLayer` привязывает поле ввода к геометрии строки. В
  [EditorDiagnosticsView](../../TrueMoon.Aether.Editor.UI/Diagnostics/EditorDiagnosticsView.cs)
  `ConsoleLayout` выделяет остаток высоты центральной области с двумя колонками.
  Обычный [StackBase](../../TrueMoon.Argentis/Layout/StackBase.cs) измеряет детей
  вдоль своей оси с неограниченным размером. Одной Fluent-обёртки над
  существующим стеком для такого layout недостаточно.
- [BindCommand в обозревателе](../../TrueMoon.Aether.Editor.UI/Hierarchy/EditorHierarchyView.Entities.cs)
  уже подключает исполнение, `CanExecuteChanged`, UI dispatch и отписку.
  Кнопки, меню и клавиатура используют общие команды. `Search`, `Tree` и
  `NameEditor` — кандидаты на именованные части для фокуса, поведения или стиля.
  `AccessibleName` описывает контрол пользователю. Отдельная таблица
  технических имён в View и генерация свойств по этим именам пока отсутствуют.
- У [Cobalt](../../TrueMoon.Cobalt.Generator/CobaltGenerator.cs) и
  [Enerit](../../TrueMoon.Enerit.Generator/ServicesGenerator.cs) есть примеры
  `IIncrementalGenerator` с Roslyn-диагностикой или генерацией C#. Roslyn
  добавляет исходники к компиляции и допускает входные `AdditionalFiles`.
  Это основание для исследования, а результат генерации View или стилей
  в текущем дереве ещё не проверен.
  [Документация Roslyn](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md).

**Этапы и критерии**

1. **UIA1f.1 — композиция на существующем API. Выполнено 22.11.2026.**
   Контролы объявлены в одном Fluent-дереве конструктора обозревателя.
   Типизированные части сохраняют ссылки для поведения и стилей.
   Layout принимает заголовок, кнопку, строки и прокрутку по их назначению.
   Геометрия inline rename остаётся внутри `RenameLayer`. По 84 теста
   иерархии прошли в Debug и Release. [Пилот Editor](../engine/EDITOR_PLAN.md#ed4c-view-authoring-pilot).
2. **UIA1f.2 — прототип `Fill()` и `WithCommand()`. Проверен 22.11.2026.**
   При конечном размере `HStack` и `VStack` делят остаток поровну между
   видимыми детьми с `Fill()` после обычных размеров и промежутков. При
   неограниченном измерении используется естественный размер. Обычные стеки
   сохраняют прежний режим. `WithCommand(IUiCommand)` занимает привязку
   `Enabled`, перепроверяет допуск перед активацией и наблюдает команду
   в рамках attachment кнопки. Отложенные уведомления прежнего attachment
   отбрасываются. Проверено в [обоих представлениях Editor](../engine/EDITOR_PLAN.md#ed4c-view-authoring-pilot).
   [Прогоны и границы](../engine/HISTORY.md#uia1f2-fill-command-2026-11-22).

   **UIA1f.2a — Fluent-подписки на события контрола. Проверено 22.11.2026.**
   `TreeView` поддерживает `OnSelectionChanged`, `OnMoveRequested` и
   `OnPlacementRequested`. Вызов возвращает тот же контрол и добавляет
   обычный типизированный обработчик синхронного CLR-события. Порядок
   регистрации и поведение при переносе сохраняются. Контрол очищает
   свои события при `Dispose`. Обозреватель и консоль подключают действия
   рядом с объявлением дерева. Дальнейшие методы добавлять по реальным
   потребителям, по образцу `OnClick` и этого пилота.
   [Приёмка](../engine/HISTORY.md#uia1f2a-fluent-events-2026-11-22).

   **UIA1f.2b — Fluent-подписки на внешние источники. Прототип проверен 22.11.2026.**
   `ObservePropertyChanged(source, handler, synchronize)` подключает
   `INotifyPropertyChanged` на срок текущего attachment. Начальная
   синхронизация выполняется на UI owner thread после подключения.
   Уведомления доставляются в порядке очереди через захваченный UI dispatcher.
   `OwnAttachment` освобождает подписку при detach и Dispose. Уведомления из
   очереди и из уже скопированного списка обработчиков старого подключения
   отбрасываются. Для события CLR выбран явный add/remove внутри
   реализации. Вариант с токеном `IDisposable` остаётся для внешних
   источников с собственным API подписки. Штатные `Bind` и
   `WithCommand` сохраняют свои контракты.
   Проверены `EditorDiagnosticsView`, повторное подключение и cleanup
   при ошибке синхронизации. [Приёмка](../engine/HISTORY.md#uia1f2b-viewmodel-observer-2026-11-22).
3. **UIA1f.3 — именованные части View. Прототип проверен 23.11.2026.**
   `.Named("Search")` задаёт техническое имя до передачи элемента владельцу.
   Каждое View ведёт реестр своих частей. Вложенный View образует собственную
   область, включая имя своего корня. Сравнение имён регистрозависимое.
   Дубликаты отклоняются до изменения дерева. Замена, перенос и удаление
   обновляют реестр до уведомлений. `GetRequiredPart<T>()` диагностирует
   отсутствие и несовпадение типа, `TryGetPart<T>()` возвращает false в обоих
   случаях. Обозреватель получает `Search`, `Tree` и `NameEditor` один раз
   перед подключением действий и стиля. Селекторы `Part` по-прежнему выбирают
   конкретные экземпляры при применении стиля. Устойчивая цель внешней
   автоматизации следует отдельному контракту сценарных проверок.
   [Приёмка](../engine/HISTORY.md#uia1f3-named-parts-2026-11-23).
4. **UIA1f.4 — исследование Source Generator для View и стилей. Завершено
   23.11.2026.** Генератор выпускает типизированные свойства частей и
   Fluent-обёртки над сеттерами, событиями, привязками и правилами стиля.
   Реестр частей формируется при принятии живого дерева. Привязки, события
   и стили используют штатные контракты UI. Полная генерация стиля проверена
   отдельным прототипом в [UIA1f.4f](#uia1f4f-style-generation-study).
   Фактические ограничения навигации в Rider сохранены в отчёте.

   <a id="uia1f4a-fluent-setters"></a>
   **UIA1f.4a — Fluent-имена и opt-in генерация сеттеров.** Соглашение для
   нового и целенаправленно обновляемого Fluent API:

   - `Property(value)` задаёт одно значение через существующий setter.
     Имя совпадает с CLR-свойством, вызов возвращает тот же экземпляр
     с типом получателя. Примеры `.AccessibleName("Поиск")`, `.MinWidth(0)`.
   - `On<Event>(handler)` подписывает на событие контрола. Имя события
     берётся из API получателя. Для `Button.Click` это `.OnClick(handler)`.
     Контракт подписки следует жизненному циклу источника.
   - `With<X>(...)` выражает подключение или композицию. Примеры
     `.WithCommand(command)`, `.WithChildren(...)`, `.WithViewStyle(style)`.
     Владение, замена и снятие определяются контрактом метода.
   - `Bind...` и `Observe...` выражают привязку и наблюдение за внешним
     источником. `Named()` и `Fill()` остаются отдельными операциями дерева
     и layout.

   Прототип `[FluentSetter]` подключается при сборке Argentis. Он создаёт
   методы в `ElementExtensions` через C# 14 extension blocks для
   `AccessibleName`, `MinWidth` и `ReorderEnabled`. Атрибут применяется
   к поддерживаемым публичным свойствам с публичным сеттером, доступным после
   создания контрола. Подпись сохраняет nullable-аннотацию типа свойства.
   Для расширяемого контрола сохраняется тип наследника через ограничение
   получателя, для sealed-контрола используется его конкретный тип.
   Совпадение сигнатур в `ElementExtensions` проверяет компилятор.
   Для расширений в других классах и методов экземпляра проверять разрешение
   вызовов на реальном View. Генерацию подписок и команд оценивать отдельно
   с учётом их жизненного цикла.

   Прототип проверен 23.11.2026 на SDK 11.0.100 и `EditorHierarchyView`.
   Клиент со старым синтаксисом C# 13 вызывает методы из готового Argentis.
   Компилятор подтвердил opt-in, nullable-тип, код `ARG001` для недоступного
   сеттера и конфликт `CS0111` при дубликате сигнатуры. Rider в открытой
   solution пометил новые цепочки как ошибочные. Подхват сгенерированных
   методов, навигацию и переименование проверить после обновления проекта.
   [Приёмка](../engine/HISTORY.md#uia1f4a-fluent-setters-2026-11-23).

   <a id="uia1f4b-fluent-events-factory"></a>
   **UIA1f.4b — C# 14, события и фабрики контролов.** В `1.2611.23.3-alpha`
   `[FluentEvent]` генерирует `OnSelectionChanged`, `OnMoveRequested`
   и `OnPlacementRequested` для `TreeView`. Атрибут ставится на
   события, обработчики которых принадлежат контролу и очищаются им при
   `Dispose`. Сгенерированный метод сохраняет проверки доступа и
   аргументов, порядок добавления и исходную сигнатуру. Подписки на
   заимствованные источники используют attachment-контракт UIA1f.2b.
   Статическое расширение `TreeView.Create()` создаёт обычный экземпляр
   с настройками конструктора. Синтаксис вызова статического расширения
   требует C# 14. Вызовы сгенерированных `On<Event>` подтверждены из
   нового C# 13 клиента и из клиента, собранного до замены реализаций.
   [Приёмка](../engine/HISTORY.md#uia1f4b-fluent-events-factory-2026-11-23).

   <a id="uia1f4c-typed-bindings"></a>
   **UIA1f.4c — opt-in типизированные двусторонние привязки.** Проверено
   23.11.2026 в `1.2611.23.4-alpha`. Маркер `[FluentTwoWayBinding]` на
   публичном `static readonly UiProperty<T>` с именем `XProperty`
   генерирует `BindXTwoWay(source, selector)` для контрола-владельца.
   Сигнатура сохраняет тип элемента и тип значения с nullable-аннотацией.
   Расширение передаёт конкретный дескриптор в штатный `BindTwoWay`,
   который управляет проверкой селектора, записью и подписками.
   Неподдерживаемая декларация получает `ARG003` при сборке.
   `TextBox.ValueProperty` использует этот путь для `Search` и `NameEditor`
   в обозревателе. Поиск и переименование проверены через ввод в `UiSession`.
   [Приёмка](../engine/HISTORY.md#uia1f4c-typed-bindings-2026-11-23).

   <a id="uia1f4d-typed-styles"></a>
   **UIA1f.4d — opt-in генерация свойств стиля.** Проверено 23.11.2026 в
   `1.2611.23.5-alpha`. `TypedPart(selector)` вызывает штатный `Part`
   и сохраняет тип цели в `ViewStyleRule<TElement>`. Маркер
   `[FluentStyleProperty]` на `UiProperty<T>` генерирует перегрузки для
   прямого значения и выражения от `Theme`. В стиле обозревателя
   `RowHeight(24f)` и `Indent(16f)` объявляются через эти методы.
   Они делегируют штатному `Set` и участвуют в существующем порядке правил,
   проверке области View и пересчёте темы. Компилятор отклоняет вызов
   `RowHeight()` для `ViewStyleRule<TextBox>`.
   [Приёмка](../engine/HISTORY.md#uia1f4d-typed-styles-2026-11-23).

   <a id="uia1f4e-generated-parts"></a>
   **UIA1f.4e — генерация именованных частей.** Проверено 23.11.2026 в
   `1.2611.23.6-alpha`. `[GenerateViewParts]` включает генерацию отдельного
   статического класса с C# extension properties. Для прототипа используются
   прямые Fluent-вызовы `Named("Search")` в конструкторах с константным
   именем и выведенным типом контрола. Допустимы верхнеуровневые View без
   параметров типа и публичные верхнеуровневые типы контролов без параметров
   типа. Вложенные View объявляют части в собственных конструкторах.
   Начиная с `1.2611.26.9-alpha`, поддерживаются доступные на уровне сборки
   типы контролов, в том числе вложенные: внутренний тип даёт `internal`
   свойство, а публично доступный сохраняет `public` для публичного View.
   Сгенерированное свойство читает текущий элемент из реестра живого View.
   Генератор проверен на обычных классах без `partial`.

   `EditorHierarchyView` использует `Search`, `Tree` и `NameEditor`
   через расширения. Проверены фокус, поиск, переименование, селекторы
   стилей, границы вложенных View и очистка при `Dispose`. Потребителям
   прежних публичных свойств нужны актуальная сборка и импорт пространства
   имён расширений. В пилоте анализатор подключён к `Editor.UI` при сборке.
   В `1.2611.25.9-alpha` на тот же механизм переведены остальные ручные
   ссылки обозревателя: кнопки, подсказка, диагностика, прокрутка, корневая
   панель и хост меню. [Переход потребителя](../../TrueMoon.Aether.Editor.UI/PACKAGE.md).
   Остальные View мигрировать по одному; удержание динамических редакторов
   [исследуется отдельно](#ui-reuse-study).
   [Приёмка](../engine/HISTORY.md#uia1f4e-generated-parts-2026-11-23).

   <a id="uia1f4f-style-generation-study"></a>
   **UIA1f.4f — сравнение полной генерации стилей. Исследование закрыто
   23.11.2026.** Отдельный прототип читает `.viewstyle` и генерирует
   `IViewStyle<TView>` на штатном builder. Стили обозревателя и диагностики
   дали то же оформление, геометрию и CPU raster в Light и Dark.
   Ошибочные ссылки и значения отклоняются при компиляции с указанием
   строки декларации. Уменьшение авторского текста приходится на запись
   селекторов и обвязку класса. Сами значения и выражения Theme остаются C#.

   Основной способ описания стилей — типизированный C# `IViewStyle<TView>`.
   Генератор используется для Fluent-методов и именованных частей.
   `.viewstyle` остаётся исследовательским прототипом. Рассматривать полный
   генератор повторно имеет смысл при сценарии, где декларации должен
   выпускать внешний инструмент. [Сравнение и проверки](HISTORY.md#uia1f4f-style-generation-study-2026-11-23).

   <a id="uia1f4g-generated-roles"></a>
   **UIA1f.4g — генерация токенов ролей.** `[ViewRole<TElement>("Name")]`
   на верхнеуровневом необобщённом `partial`-классе `IViewStyle<TView>`
   объявляет типизированный токен; View применяет `.WithRole(Style.Name)`.
   Правила остаются C#, токены сопоставляются по экземпляру, вложенные View
   сохраняют свою область. Анализатор диагностирует неверные и конфликтующие
   имена. Первая миграция — кнопки панели Editor.

   В `1.2611.25.15-alpha` эксперимент `.WithRole(ViewNameRoles.Name)` выводит
   токен из типа контрола в inline `Content(...)`. Одно имя с разными типами
   диагностируется. Динамика вне inline-дерева может повторно использовать
   такой токен или объявить его через атрибут.

   <a id="uia1f5-ui-skill"></a>
5. **UIA1f.5 — инструкции и скилл. Выполнено 23.11.2026.** Раздел UI в
   [AGENTS.md](../../AGENTS.md) описывает Fluent-композицию и именованные части.
   Проектный скилл [truemoon-ui](../../../.agents/skills/truemoon-ui/SKILL.md)
   ведёт от сценария экрана до проверок и использует [DESIGN](DESIGN.md)
   и [UI_TESTING](UI_TESTING.md).
   [Пример на основе EditorHierarchyView](../../../.agents/skills/truemoon-ui/references/editor-hierarchy.md)
   показывает layout, действие, привязки, тему и типизированные селекторы.
   Код из документа скомпилирован с текущим генератором Argentis на SDK
   `11.0.100`. Генератор выпустил отдельный класс для обычного View с
   тремя extension properties. Исходник проверки и вывод компилятора
   находятся в `TestResults/UIA1f5-20261123-100349`.

   <a id="uia1f6-build-content"></a>
6. **UIA1f.6 — создание дерева через `View.BuildContent()`. Выполнено 23.11.2026.**
   `View<TData>` наследует виртуальный `BuildContent()` от `View`. Фабрика
   передаёт полностью сконструированный экземпляр в `ViewCreation.Prepare`.
   Подготовка однократно устанавливает корень, регистрирует части, вызывает
   `OnContentPrepared()` и применяет стиль. Явно установленный корень
   сохраняется. При сбое фабричная подготовка освобождает черновик.
   Генератор распознаёт override по символу базового метода. Для прямых
   `.Named(...)` в поддерживаемых типах View он выпускает свойства-расширения
   без `[GenerateViewParts]` и `partial`. Атрибут остаётся для конструкторной
   композиции. `EditorHierarchyView` переведён на этот путь через DI-фабрику.
   [Проверки](../engine/HISTORY.md#uia1f6-build-content-2026-11-23).

   <a id="uia1f7-context-bindings"></a>
7. **UIA1f.7 — привязки к текущему контексту. Выполнено 23.11.2026.**
   `Bind`, `BindTwoWay`, `BindText` и генерируемые адаптеры вроде
   `BindValueTwoWay` принимают `IDataContext<TData>` и типизированный
   `Expression`. Перегрузки с явно переданным источником сохранены.
   `IDataContext<TData>.DataContextChanged` обязателен для реализаций
   интерфейса и вызывается после замены модели на UI owner thread.
   На detach подписки снимаются, при reattach читается актуальная модель.
   При null привязка приостанавливается, контрол сохраняет последнее значение.
   Отложенные уведомления предыдущего источника отбрасываются, в том числе
   при A → B → A. В обозревателе переиспользуются `Search` и `NameEditor`.
   Действия, данные и размещение редактора имени следуют за текущей моделью.
   [Проверки](../engine/HISTORY.md#uia1f7-context-bindings-2026-11-23).

   <a id="uia1f8-layout-reduction"></a>
8. **UIA1f.8 — сократить объявления контролов и контейнеров. Выполнено
   23.11.2026.** `HStack.Create(children...)` и `VStack.Create(children...)`
   принимают дочерние элементы в порядке объявления. Доступны также фабрики
   без параметров и явный `WithChildren`. В `EditorHierarchyView` заголовок,
   панель действий и строка поиска объявлены через `Named`. Селекторы стилей
   получают `Title`, `Actions` и `SearchRow` через сгенерированные свойства.
   Повторяющиеся кнопки используют `ActionRole`. Проверены порядок детей,
   геометрия при сжатии, фокус и повторное применение стиля.
   [Проверки](../engine/HISTORY.md#uia1f8-layout-reduction-2026-11-23).

   <a id="uia1f9-context-actions"></a>
9. **UIA1f.9 — команды и наблюдение за текущим контекстом. Выполнено 23.11.2026.**
   `WithCommand(context, selector)` выбирает готовую команду при attach и смене
   `IDataContext<TData>`. Доступность обновляется через `CanExecuteChanged`.
   При отсутствии модели или команды кнопка недоступна.
   `ObservePropertyChanged(context, handler, synchronize)` подключает уведомления
   текущей модели и даёт прочитать её состояние при attach и замене контекста.
   Callback уведомления исполняется в очереди UI. Для обеих перегрузок detach
   снимает подписки. Отложенные уведомления прежнего подключения отбрасываются.
   Обозреватель применяет контекстный наблюдатель. Кнопки, клавиатура и меню
   используют общие команды View. Обновлены скилл и пример `truemoon-ui`.
   [Проверки](../engine/HISTORY.md#uia1f9-context-actions-2026-11-23).

   <a id="uia1f10-diagnostics-view"></a>
10. **UIA1f.10 — второй пилот Fluent-композиции. Выполнено 23.11.2026.**
    `EditorDiagnosticsView` собирает консоль в `BuildContent()`. Привязки,
    действия и наблюдение следуют за текущим `DataContext`.
    `Named` обеспечивает типизированный доступ к фильтрам, колонкам и кнопкам.
    Фабрика готовит дерево до применения стиля. Проверены равные колонки,
    фокус и выбор события, пауза прокрутки и очистка текущего журнала после
    повторного подключения. [Проверки](../engine/HISTORY.md#uia1f10-diagnostics-view-2026-11-23).

Итоговая проверка версии `1.2611.23.11-alpha` описана в
[истории Editor](../engine/HISTORY.md#uia1f-delivery-2026-11-23).

Подход применён к [динамическому инспектору Editor](../engine/HISTORY.md#ed4d-inspector-fluent-2026-11-24).
Каркас выражен через Fluent-дерево. Каждая динамическая секция использует
фабрику редакторов и получает оформление через роли.

Продолжение по динамическим поддеревьям — [исследование повторного
использования контролов и ресурсов рендера](#ui-reuse-study). В нём проверим,
можно ли выбирать живой узел по ключу и типу до вызова фабрики, сохранив
читаемость Fluent-дерева и текущий контекст привязок.

Приёмка пилота — дерево читается в порядке экрана, создание и обновление
сохраняют прежние экземпляры там, где от них зависят фокус, выбор, раскрытие
или прокрутка. Проверить layout на узкой/короткой панели, смену темы и стиля,
клавиатуру и cleanup в `UiSession`. Для оценки внешнего вида использовать
raster по [UI_TESTING](UI_TESTING.md). Сверка исходников выше описывает состояние
до пилота. Результаты запуска UIA1f.1 указаны в его шаге и в истории Editor.

<a id="uia1f11-contextual-content"></a>
### UIA1f.11. Отложенный Content и типизированное создание контролов

**Локальная реализация и приёмка завершены 24.11.2026.** Штатный формат View —
Fluent-дерево в фабрике, переданной из конструктора в `Content(data, ...)`.
Типизированный контекст позволяет объявлять привязки в цепочке контрола.

```csharp
public sealed class SearchView : View<ISearchViewModel>
{
    public SearchView(ISearchViewModel model)
        => Content(model, static ui => ui.HStack(
            ui.Text("Поиск"),
            ui.Create<TextBox>()
                .Named("Search")
                .Bind(UiProperties.Enabled, data => data.CanSearch)
                .BindValueTwoWay(data => data.SearchText)));
}
```

**Первый пилот проверен 24.11.2026 в `1.2611.24.7-alpha`.** Перегрузка
`Content(data, factories, factory)` включает отложенную подготовку. Реестр
`UiControlFactories` находится в Alloy и принимает `IFactory<TControl>`.
Argentis получает `IUiControlFactories` и сохраняет границу зависимостей BCL.
В `SearchView` проверены первый attach, поздняя вставка, reattach, стиль,
отзыв подписки и запись в новый `DataContext` через `.BindValueTwoWay()`.
[Результаты пилота](HISTORY.md#uia1f11-deferred-content-pilot-2026-11-24).

**Генерация частей проверена 24.11.2026 в `1.2611.24.8-alpha`.** Генератор
читает прямые вызовы `.Named()` внутри inline-фабрики штатного `Content(...)`.
Именованный контрол внутри `UiControl<TControl, TData>` экспортируется как
`TControl`. `Search` и `NameEditor` обозревателя используют сгенерированные
`TextBox` и двусторонние привязки без `this`. Проверены отложенная подготовка
и `PrepareImmediately` для вызывающего кода, который обращается к части до
attach. [Проверки](HISTORY.md#uia1f11-content-parts-2026-11-24).

**Контракты фреймворка переключены 24.11.2026 в `1.2611.24.9-alpha`.**
`Content(data, factory)` на `View<TData>` и `Window<TData>` получает
контекст от сессии при первом attach. Для View без данных используется
`Content(factory)`. Registry Alloy принимает `IFactory<TControl>` и
`IFactory<TControl, TArguments>`. App передаёт реестр через DI, прямая
сессия имеет встроенные фабрики. Генераторы поддерживают конфигурацию
и привязки через `UiControl<TControl, TData>` и находят части из `Content`.
Дерево проверяет имена через общую операцию `Element`, а слот `ContentControl`
имеет единое представление. Удалены прежние способы объявления и
синхронной подготовки View и статические фабрики контролов.

**View-потребители переведены 24.11.2026 в `1.2611.24.10-alpha`.**
Обозреватель, инспектор и консоль диагностики описывают дерево через
`Content(model, ui => ...)`. Оболочка Editor разрешает `Caption` после
подготовки обозревателя. Контейнеры с уже созданными дочерними View
принимают их через `SetContent`. Генератор выводит части и для фабрик с
inline-обработчиками, обращающимися к этим частям.
[Сборки](HISTORY.md#uia1f11-view-consumers-2026-11-24).

**Локальная регрессия пройдена 24.11.2026 в `1.2611.24.11-alpha`.** Тесты
используют новый жизненный цикл отложенного дерева. Fluent-композиция
проверяет всех детей контейнера до изменения их владельца. Проверены
attach/reattach, смена модели и стиля, фокус, диагностика и ENG9b. Скилл
`truemoon-ui` содержит собранный пример на актуальном API.
[Результаты и границы](HISTORY.md#uia1f11-regression-2026-11-24).

**Действующий контракт**

1. **Отложенная подготовка.** `Content` задаёт начальный `DataContext` и
   сохраняет `Func<IUiBuildContext<TData>, Element>`. Фабрика выполняется
   на UI owner thread при первом attach корня или поздней вставке View в
   подключённый контейнер. Подготовка регистрирует части, вызывает
   `OnContentPrepared()` и применяет отложенный `IViewStyle<TView>` до
   attach-уведомлений и первого layout. После успеха ссылка на фабрику
   освобождается. Detach сохраняет контролы, reattach возобновляет подписки.
2. **Фабрики контролов.** Реестр использует общий
   [`IFactory<TControl>`](../../TrueMoon.Contracts/IFactory.cs) и
   `IFactory<TControl, TArguments>`. App передаёт реестр через DI.
   Прямая `UiSession` предоставляет встроенные фабрики. Шорткаты используют
   тот же реестр. Корень создаётся текущим `ui` или передаётся ему через
   `ui.Adopt(control)`. Все полученные этим контекстом контролы входят в
   возвращаемое дерево и освобождаются при ошибке. `Adopt` нужен для
   неприсоединённого контрола, который хост читает до attach. `View.OwnDraft`
   удерживает такой контрол до передачи своему сборщику; другой View не может
   его принять. Уже подключённое дерево сохраняется.
3. **Типизированный Fluent API и генерация.** `Create<TControl>()` возвращает
   `UiControl<TControl, TData>`, небольшую `readonly struct` с конкретным
   контролом и живым `IDataContext<TData>`. Преобразование в `TControl`
   передаёт обычный элемент в дерево. `Bind`, `BindTwoWay` и контекстные команды
   делегируют существующим методам, сохраняя `Expression` для привязок.
   Включены контекстные перегрузки и opt-in генерация из `UiProperty<T>`.
   Генератор частей распознаёт фабрику штатного `Content(...)` по символу
   и выводит тип части из `TControl`. Сгенерированные свойства-расширения
   доступны без `partial` и ручной аннотации View. Начиная с `1.2611.25.16-alpha`
   пакет Argentis подключает анализатор автоматически, в том числе транзитивно.
   `NamedOptional` явно выбирает nullable-свойство для части одного режима;
   `Named` сохраняет обязательный getter.
4. **Синхронизация дерева и именованных частей.** `ContentControl` владеет
   слотом `Child` и предоставляет его через `Children`. `_namedParts` —
   производный индекс области ближайшего View. Проверка имён и обновление
   индекса выполняются в общих операциях `Element` и `TreeChange`, которыми
   пользуются `ContentControl`, `ElementList` и `OverlayHost`. Имена проверяются
   до фиксации, индекс обновляется вместе с деревом до вызова наблюдателей.
   Вложенные View изолируют свои имена. Для ручной замены готового поддерева
   используется `SetContent`.
   Наблюдатели `ObservePropertyChanged` и `ObserveState` с
   `UiSynchronization.AfterTreeChange` синхронизируют состояние в очереди
   текущего attach и могут менять детей; inline-режим остаётся по умолчанию.

Следующий открытый срез композиции — [сохранение динамических контролов по
ключу](#ui-reuse-study). Выпуск пакетов и приёмка внешних приложений ведутся
отдельно от локальной приёмки UIA1f.11.

<a id="uia1f12-fluent-generation-study"></a>
### UIA1f.12. Исследование генераторов для проектирования и использования Fluent UI

**Исследовано 26.11.2026.** Цель — убрать повторяющуюся авторскую обвязку из
Fluent-разметки и подключения View, оставив в C# читаемое дерево экрана,
важные привязки и действия. Основа — текущие `Content(...)`, генерация Fluent
методов, частей и ролей, а также [фабрики View](#uia1-view-registration).
Исследование не меняло публичный API. Выполнено после
[пакетной приёмки](#uia1-view-package-acceptance).

1. Найти повторения на двух примерах: `EditorHierarchyView` и динамический
   список `BindItems` с `ViewWithArgs`. Отдельно отметить шаблонный код
   создания контролов, привязок, команд, частей, ролей и регистрации фабрик.
   Отделить обвязку от кода, который объясняет интерфейс и его поведение.
2. Сравнить три варианта на тех же примерах: текущий Fluent, упрощение
   обычными типизированными C#-методами, генерацию поверх существующего
   Argentis. Проверить, что можно надёжно вывести по символам, а где
   нужна явная декларация. Не вводить отдельный DSL и не скрывать
   бизнес-логику, порядок дерева, владение или runtime-аргументы.
3. Сделать ограниченный прототип для наиболее повторяемого шаблона. Сравнить
   общий объём авторского кода с учётом вынесенной обвязки, читаемость дерева,
   навигацию IDE, диагностику неоднозначностей и стоимость инкрементальной
   сборки. Проверить, что сохранились DI и стили, семантические ID,
   привязки при смене контекста и освобождение динамических строк.

**Решение:** для повторов в панели действий предпочесть типизированный C#-метод,
оставив имена частей, команды и callback в дереве. Для динамических строк
оставить `BindItems(items, rows.Create)` и `ViewWithArgs`; привязки не выводить
из имён полей. Генерацию действий не добавлять: она укорачивает строки дерева,
но увеличивает авторский код и разносит метаданные по файлу.
[Сравнение и протокол](HISTORY.md#uia1f12-fluent-generation-2026-11-26).
В `1.2611.26.1-alpha` типизированный метод применён к пяти кнопкам
`EditorHierarchyView`; остальные экраны, API и динамические списки не менялись.

<a id="uia1g-element-view-boundary"></a>
### UIA1g. Отделить Element от View

**Реализовано в `1.2611.25.6-alpha`.** `View` готовит отложенное содержимое и
владеет `ElementScope`; дерево не распознаёт конкретный `View`. Проверки
attach, наблюдателей, вложенных имён, фокуса и привязок — в [статусе](STATUS.md#uia1-architecture).

**Запланировано 24.11.2026.** Сейчас `Element` распознаёт конкретный `View`
для подготовки содержимого и ищет ближайший `View` при проверке имён,
изменении дерева и применении стилей.
[Подготовка и области стилей](../../TrueMoon.Argentis/Element.cs),
[реестр частей](../../TrueMoon.Argentis/Views/View.cs).

- Оставить у дерева общий обход подготовки к attach. `View` выполняет
  отложенный `Content(...)` в собственной реализации подготовки узла.
  Кандидат для внутреннего контракта — `private protected` hook.
- Область имён и стилей создаёт `View`. Дерево использует её через внутренний
  объект области. Сохранять проверку имён до принятия поддерева, обновление
  индекса до уведомлений и изоляцию вложенных областей. Свести
  межклассовые вызовы к операциям подготовки, проверки и фиксации изменения.
  Внутренние операции владения и доставки уведомлений сохраняют общую
  границу `TreeChange`.
- Критерий завершения — `Element` без ссылок на тип `View`. Проверить первый
  attach, позднюю вставку, reattach, совпадающие имена во вложенных View,
  отклонение дубля с сохранением прежнего дерева и смену темы и стиля
  с сохранением фокуса и привязок.

<a id="uia1h5-external-presenters"></a>
### UIA1h.5. Убрать выбор презентера из контрола

Этот контракт заменяет штатный дескриптор на элементе из UIA1h.1–4.

1. Контролы Argentis хранят дерево, свойства, состояние ввода и геометрию layout.
   У `Element` нет `Presentation`; ни `TextBox`, ни другой контрол не создаёт и
   не выбирает презентер. Рисование использует подготовленную геометрию.
2. Штатные фабрики и рисование находятся рядом с `UiTreeRenderer` в Alloy,
   а не в каждом backend. Для наследника используется ближайшая штатная графика;
   регистрация приложения действует только на точный тип. Структурный узел не
   требует пустого презентера.
3. Hosting и прямой рендер имеют один штатный каталог. В App фабрику прикладного
   презентера получают из DI при сборке фабрики сессий, не через `Resolve` на
   элемент или кадр. Каждый реестр освобождает созданные им экземпляры.
4. Рисование прикладного типа регистрирует потребитель через внешний каталог.
   Приёмка: независимый пакетный экран, подмена и наследование, изоляция двух
   сессий, освобождение ресурсов, отсутствие изменений ввода и изображения.

Публичный API несовместим с `1.2611.25.6-alpha`; [миграция](../../TrueMoon.Argentis/PACKAGE.md)
и [текущий статус](STATUS.md#external-presenters).

<a id="uia1h-control-presenters"></a>
### UIA1h. Вынести отрисовку из Element и сделать презентеры сменными

**Реализовано в `1.2611.25.6-alpha`.** UIA1h.1–4 используют один обход,
точные регистрации `UiPresenters`, подготовленную геометрию и отдельное
подтверждение показа. [Пакетный пример миграции](../../ManualTests/AlloyPackageConsumer/PresenterScreen.cs)
и [профили проверок](STATUS.md#uia1-architecture). Ниже сохранены исходные критерии.

**Запланировано 24.11.2026.** Приложение может заменить алгоритм отрисовки
штатного контрола в своей `UiSession`. Смена презентера сохраняет контракт
layout, ввода, команд, фокуса и семантических ID. Основание — Raster, OpenGL
и Vulkan сейчас вызывают общий `Element.Draw()`, а сам `Element` управляет
обходом детей и вызывает `DrawCore()` контролов.
[Текущий обход](../../TrueMoon.Argentis/Element.cs),
[пример render surface](../../TrueMoon.Alloy.Rendering.Skia/SkiaRasterRenderBackend.cs).

**Решение 25.11.2026.** Единственное дерево — `Element.Children`; презентер
не владеет детьми и не переставляет их. Фазы ввода/обновления, layout и
рисования выполняются на UI-потоке владельца. По умолчанию окна делят поток,
но не фокус, viewport, поверхность или признаки необходимости layout и кадра.
Поток на окно, отдельный render-поток, постоянный второй граф и обязательный
буфер команд не вводим. Возвращаться к выносу исполнения команд только по
замерам задержки ввода, layout, обхода, исполнения и present по отдельности.

- `Element` и контролы владеют деревом, состоянием, свойствами, layout,
  обработкой ввода и инвалидированием. Рендерер и маршрутизация ввода
  в `UiSession` читают одну геометрию, включая границы клипа детей.
- Общий рендерер дерева в Alloy заменяет обход из `Element.Draw()`.
  Он отвечает за порядок отрисовки, видимость, clipping, групповую
  прозрачность и баланс состояния контекста при ошибке. Презентер рисует
  элемент по подготовленному состоянию через `IDrawingContext`.
  Значения стилей остаются входами отрисовки. Штатные операции фона,
  рамки и эффектов доступны для композиции. Презентер может нарисовать
  собственную поверхность в тех же границах. Презентер рисует до детей
  (подложка, содержимое) и после них (рамка, индикация); детей посещает
  только рендерер. `Element` не получает `IDrawingContext`. Layout и
  hit-test используют один результат; формулы измерения пока остаются у
  контролов. Изменяемое состояние ввода готово до рисования. Допустима
  подготовка данных только для изображения без обязательного второго обхода.
- Реестр на уровне `UiSession` выбирает штатный или зарегистрированный
  приложением презентер для типа контрола. Реестр хранит типизированные
  фабрики из [`TrueMoon.Contracts`](../../TrueMoon.Contracts/IFactory.cs).
  Новый экземпляр создаётся через `IFactory<TPresenter>`. Если для
  создания нужны параметры времени выполнения, используется
  `IFactory<TPresenter, TArguments>`. Фабрики задаются при настройке
  App или при прямом создании сессии. Для повторных кадров используется
  один лениво созданный экземпляр на регистрацию в сессии; сессия освобождает
  его при закрытии. Пользовательская регистрация действует только для точного
  типа, без поиска по базовым классам. Презентер читает подготовленное состояние,
  не удерживает контрол и не хранит данные узла между входом и выходом из него.
  У каждого контрола с собственной графикой должен быть свой типизированный
  презентер. Это относится и к производному контролу, если он добавляет своё
  рисование. Структурному узлу без собственной графики пустой класс презентера
  не нужен: его детей рисует общий обход.
  Форму передачи данных проверить на `IconButton` и `TextBox`.
  Рабочий вариант типизированного контракта — `IElementPresenter<TState>`.
  Выбор для отдельного экземпляра рассмотреть при появлении потребителя.
  Прежний `IContentPresenter` относился к отдельному пути `VisualTree`, который
  [удалён](#uia1j-remove-hosting-compatibility).
- Backend управляет поверхностью, `IDrawingContext` и ресурсами Skia/GPU.
  Stateless-презентер можно использовать для нескольких экземпляров.
  Пул типизированных презентеров и переиспользование ресурсов backend
  проверяются в [исследовании PA10](#ui-reuse-study).

**DI и владение (решение 25.11.2026).** При проектировании нового UI API сначала
определять, кто регистрирует зависимость, кто создаёт экземпляр и кто его
освобождает. Действуют [правила DI TrueMoon](../../AGENTS.md#di-и-создание-систем-и-сервисов).

- В штатном приложении граф сервисов собирает Cobalt в точке подключения Alloy
  к App. Сервисы получают зависимости через конструкторы; для окон, сессий и
  презентеров с отдельным временем жизни внедряются типизированные `IFactory`.
  Контролы, презентеры и обход дерева не получают контейнер и не вызывают `Resolve`
  на узел или кадр. Значения свойств, геометрию и снимки состояния регистрировать не нужно.
- Реестр заимствует фабрики; созданный для сессии презентер принадлежит сессии,
  а не одновременно ей и контейнеру. При закрытии и откате частично созданного
  графа владелец освобождает каждый созданный ресурс ровно один раз.
- Для прямого рендера и тестов без App вызывающая сторона явно собирает те же
  фабрики и задаёт владельца их результатов. Отдельный контейнер не обязателен;
  глобальный экземпляр презентера вместо фабрики недопустим. Это не требует
  зависимости Argentis от Cobalt.
- Приёмка UIA1h.2: проверить штатную и подменённую фабрики через Hosting и при
  прямом рисовании; повторный кадр не создаёт новый экземпляр у того же владельца.
  Отдельно проверить освобождение при закрытии и при ошибке создания. Пользовательская
  подмена для `IconButton` не распространяется на наследников.

**Аудит путей создания UI-объектов (25.11.2026).** До миграции очередного
контрола проверять не только регистрацию в App, но и прямой конструктор,
прямое рисование, наследников, совместимые хосты и тестовые поверхности.
Для каждого сервисного объекта фиксировать путь создания, владельца, время
жизни и поведение при ошибке. Статический поиск `new`, `Default`, `Resolve`
— список кандидатов, не основание для механической замены.

| Очередь | Действие и критерий закрытия |
|---|---|
| 1. Презентеры (`IconButton` первым) | Исправлено для `IconButton` на `1.2611.25.3-alpha`: глобальный экземпляр удалён, Hosted и прямой рендер используют фабрики и явного владельца. При переносе каждого следующего контрола проверять те же пути. [Результат](STATUS.md). |
| 2. Реестры и фабрики контролов | Проверено на `1.2611.25.4-alpha`: общий каталог штатных фабрик неизменяем, регистрации App изолированы от других App и прямой сессии. Тесты прямого и Hosted-путей фиксируют владение при ошибке attach. [Результат](STATUS.md). |
| 3. Окна, backend и ресурсы | Проследить создание, заимствование и остановку от App до окна/поверхности, включая общий `HostedUiDispatcher` / `UiOwnerLoop`. Сохранить общий UI-поток; проверить, что закрытие одного окна не освобождает ресурс другого, а ошибка создания не оставляет ресурс без владельца. |

<a id="ui-di-window-cleanup"></a>
**Партия 3: исправления и приёмка по исходникам Hosting (25.11.2026).**

1. **Сначала зафиксировать отказ закрытия окна с занятым кадром.** В
   [`HostedUiWindow.Cleanup`](../../TrueMoon.Alloy.Hosting/HostedUiWindow.cs)
   защита оставляет presenter/device живыми, если сессия не освобождена.
   Однако `UiCleanup.Complete` пытается выполнить и последующие действия
   (`window.Dispose`, уведомление `_closed`), а `finally` безусловно очищает
   ссылки владельца. Это расхождение в коде; фактическая утечка на GPU не
   подтверждена. В [BorrowAfterPresentation_BlocksOwnerCleanupButNotSiblingInputAndFrame](../../TrueMoon.Alloy.Hosting.Tests/HostedUiSessionFactoryTests.cs)
   воспроизведён отказ `UiSession.Dispose` на общем owner loop: поверхность
   имитирует удержание кадра подписчиком после успешного present. Ссылка,
   сохранённая **тестом**, позволяет вернуть кадр и закончить освобождение.
   Тест не вызывает native `HostedUiWindow.Cleanup`; следующий критерий —
   перенести ответственность за сохранение владельца из стенда в путь закрытия окна.
2. **Исправить передачу владения при незавершённом cleanup.** Снимать ссылки
   на зависимые ресурсы только после их освобождения либо явной передачи
   владельцу, который завершит cleanup на UI-потоке. Не уничтожать native-окно,
   пока оно нужно живой поверхности; не выдавать неполное освобождение за
   успешное закрытие. Приёмка: вернуть заимствованный кадр после первого
   отказа, завершить освобождение в порядке сессия → presenter → устройство
   → окно; уже освобождённые ресурсы не трогать повторно, исходную ошибку
   сохранить. Если заимствование невозможно на данном пути, доказать это
   тестом до изменения кода.
   В `1.2611.25.5-alpha` реализована явная повторная попытка на потоке окна:
   `RetryCleanupAsync` принимает действие возврата кадра. Результат попытки
   отделён от первоначального `Completion`. См. [статус и профиль](STATUS.md).
3. **Проверить изоляцию окон и откат запуска.** На общем
   [`UiOwnerLoop`](../../TrueMoon.Alloy.Hosting/UiOwnerLoop.cs) оставить окно B
   работающим, пока A закрывается или не завершает инициализацию. Проверить,
   что B принимает ввод и рисует следующий запрошенный кадр, а для A очищены
   только приобретённые ресурсы. Исправлять регистрацию, подписки и остановку
   цикла только при воспроизводимом нарушении; сам факт общего потока не дефект.
   Начать с управляемого теста; native/GPU-проверку проводить отдельно, если
   изменение затронет фактическое освобождение контекста или устройства.
   25.11.2026 проверен частичный старт с отказом при построении `Content`:
   две нативные пары окон сохранили ввод и показ следующего кадра соседа.
   Изменения регистрации или общего цикла не потребовались. [Профили и границы](STATUS.md).

Исправлять подтверждённые расхождения отдельными проверяемыми изменениями;
для оставленных путей фиксировать основание. Описатели `UiProperty`, значения
геометрии и неизменяемые данные символов — не сервисы и не требуют DI.

**Исходная сверка 25.11.2026 (чтение кода, не результат прогона)**

| Что обнаружено | Что предусмотреть до массового переноса |
|---|---|
| [Raster](../../TrueMoon.Alloy.Rendering.Skia/SkiaRasterRenderBackend.cs), [OpenGL](../../TrueMoon.Alloy.Rendering.Skia/SkiaOpenGLRenderBackend.cs) и [Vulkan](../../TrueMoon.Alloy.Rendering.Skia/SkiaVulkanRenderBackend.cs) масштабируют canvas и вызывают `root.Draw`. Тесты также рисуют изолированные элементы напрямую. | Определить синхронный вход рендерера с контекстом текущего backend; масштаб применяет backend ровно один раз. Один и тот же обход использовать для окна и теста без окна; не создавать вторую реализацию обхода в fake. |
| В [Element](../../TrueMoon.Argentis/Element.cs) результат фона (`overBackdrop`) передаётся детям, clip детей вложен в групповую opacity, рамка рисуется после детей. | До удаления снять составной CPU-эталон. Новый обход должен восстановить состояние при исключении и не переносить backdrop или состояние презентера между соседями. Проверить вложенные панели одного типа, подложку родителя и рамку поверх ребёнка. |
| [UiSession](../../TrueMoon.Alloy/Runtime/UiSession.cs) выполняет layout внутри `Update()` перед `Draw`; `RouteInput` сам не выполняет отложенный layout. В [RichTextEditor](../../TrueMoon.Argentis/Editors/RichTextEditor.cs) `DrawCore` вызывает `EnsureLayout` и `RevealCaret`. | Отделить актуализацию геометрии от выдачи кадра. После resize или изменения текста направить ввод до рисования; проверить hit-test, каретку, прокрутку и инвалидность старого semantic snapshot. Не вызывать полный `Update()` из обработчика ввода. |
| [EditorViewport](../../TrueMoon.Aether.Editor.UI/EditorViewport.cs) записывает `ImageBounds`, `LayoutRevision` и `DrawnFrame` при рисовании; [тесты квитанций](../../TrueMoon.Aether.Editor.Tests/EditorViewportReceiptTests.cs) читают результат. | Отделить расчёт геометрии от результата успешного рисования и подтверждения показа хостом. Передавать результат рендера явно, не мутируя контрол из презентера. Проверить пропущенный, ошибочный и устаревший кадр. |
| [HostedUiDispatcher](../../TrueMoon.Alloy.Hosting/HostedUiDispatcher.cs) уже использует общий `UiOwnerLoop`; Vulkan проверяет поток создателя. | Переиспользовать цикл, а не заводить новый scheduler. Две сессии: hover и закрытие в первой не должны вызывать layout/рисование или освобождение ресурсов второй. Пул вводить только для измеренной дорогой операции с понятным сбросом состояния. |
| В [EditorStatusBarView](../../TrueMoon.Aether.Editor.UI/EditorStatusBarView.cs) есть закрытые производные `IconButton`, `Button` и `Text`. | До фиксации публичного API проверить регистрацию прикладного закрытого типа из его сборки. Различить структурный узел без собственной графики и забытый презентер рисующего контрола; не скрывать пропажу графики подбором по базовому типу. |

**Этапы и критерии**

1. **UIA1h.1 — обход дерева.** Зафиксировать CPU raster эталон и вынести
   общий обход `Element.Draw()` в Alloy. Подключить его в штатном маршруте
   сессии. После фиксации исходного поведения удалить публичный `Draw()` и
   виртуальный `DrawCore()` сразу, без переходного API. Ошибки сборки
   использовать как список потребителей для миграции, а не менять ожидания
   тестов ради сборки. Проверить порядок оверлеев, clipping, тени, backdrop,
   групповую прозрачность и восстановление состояния контекста при ошибке.
   Перевести Raster, OpenGL, Vulkan и тесты без окна на один вход обхода.
   При масштабе viewport, отличном от 1, логические границы должны перейти
   в ожидаемые пиксели ровно один раз. Для CPU-эталона взять две вложенные
   панели одного типа и соседний узел: рамка родителя видна поверх ребёнка,
   clip и backdrop ребёнка не переносятся на соседа.
2. **UIA1h.2 — заменяемый IconButton.** Подключить штатный и альтернативный
   презентеры через `IFactory` для [`IconButton`](../../TrueMoon.Argentis/IconButton.cs).
   Штатный сохраняет CPU-кадр. Альтернативный меняет алгоритм рисования
   в выбранной сессии и сохраняет размер, границы hit target, фокус,
   состояние кнопки, одну активацию по клику и семантический ID. Во второй
   сессии проверяется применение штатного презентера. При нескольких кадрах
   и смене темы проверить число вызовов фабрики и освобождение её результата
   при закрытии сессии. На общем `UiOwnerLoop` проверить два окна после
   первого кадра: наведение и закрытие первого не запускают layout/render
   второго и не освобождают его презентер; второе окно продолжает принимать ввод.
   На уже разложенном экране проверить границу фаз через
   [`UiDiagnostics`](../../TrueMoon.Alloy/Runtime/UiDiagnostics.cs).
   Наведение на `IconButton` должно вызвать отрисовку без нового
   `Measure` и `Arrange`. Изменение размера шрифта текстового элемента
   должно запустить layout перед отрисовкой.
   Для прототипа по семантическому ID сохранять в тестовом отчёте тип
   контрола, тип выбранного презентера и основание выбора: регистрация
   для точного типа или штатный вариант. Замена `Button` не заменяет `IconButton`.
   Публичную диагностику по запросу рассматривать при появлении
   конкретного сценария отладки.
3. **UIA1h.3 — геометрия TextBox и RichTextEditor до рисования.** Перенести подготовку раскладки, корректировку
   выделения и смещение к каретке из рисования в обновление состояния
   контрола. На [`TextBox`](../../TrueMoon.Argentis/Editors/TextBox.cs) проверить,
   что клик, выделение, каретка и прокрутка используют ту же геометрию,
   которую рисует презентер. Сравнить CPU raster до и после, включая
   сжатое поле и смену фокуса. При повторной отрисовке одного состояния
   проверить сохранение позиции каретки, выделения и смещения прокрутки.
   Для обоих редакторов проверить последовательность «изменение текста или
   resize → клик → кадр»: клик использует новую геометрию, старый semantic
   snapshot отклоняется после изменения layout, а счётчик render до кадра
   не растёт. В `RichTextEditor` перенести `EnsureLayout` и `RevealCaret`
   из рисования; обработчик ввода не должен вызывать полный `Update()`.
   До массового переноса собрать независимого NuGet-потребителя: на одном
   экране заменить штатный `IconButton` и зарегистрировать презентер для
   закрытого типа из сборки потребителя; проверить рисование и ввод обоих.
   Отдельным тестом отличить допустимый узел без собственной графики от
   пропущенной регистрации рисующего контрола: ошибка должна называть тип,
   а не маскироваться отрисовкой базового класса.
4. **UIA1h.4 — миграция контролов и потребителей.** Перевести оставшиеся
   фреймворковые и прикладные реализации `DrawCore()`. Для
   [EditorViewport](../../TrueMoon.Aether.Editor.UI/EditorViewport.cs) и
   [PlayerCanvas](../../TrueMoon.Aether.Player/PlayerView.cs) сохранить
   различие между фактически нарисованным кадром и подтверждением показа
   хостом. Вынести `ImageBounds` в расчёт геометрии; результат успешного
   рисования передавать явно, без записи в контрол из презентера. Проверить,
   что пропуск, исключение и устаревший результат не подтверждают новый кадр,
   а успешное рисование без подтверждения хоста не считается показом.
   Проверить отсутствие вызовов старого пути. Прогнать UI-регрессию, ENG9b Editor на CPU Skia и адресные
   сценарии на OpenGL и Vulkan, включая показ и смену размера viewport.
   На одном экране независимого NuGet-потребителя через публичный API
   заменить рисование штатного `IconButton` и добавить собственный
   контрол с его презентером. Проверить отрисовку и ввод для обоих
   элементов. Использовать этот пример в инструкции по миграции
   с `DrawCore()` и обновить примеры по принятому API.

Исходный порядок приёмки был UIA1h.1–2, затем UIA1h.3–4; UIA1g не был
их предпосылкой. [Результат всего блока](HISTORY.md#uia1-architecture-2026-11-25).

<a id="uia1i-argentis-structure"></a>
### UIA1i. Организация исходников Argentis по подсистемам

**Запланировано 24.11.2026.** На момент сверки все 109 C#-файлов
`TrueMoon.Argentis` находятся в корне проекта. Цель — сделать устройство
модуля видимым по дереву файлов и собирать связанные типы рядом.

1. Составить карту текущих типов и зависимостей. Начать с layout,
   ввода, привязок, стилей, View и окон. Для контролов с общей логикой,
   например меню и редакторов текста, определить функциональные группы
   по фактическим связям. Базовые типы без отдельной подсистемы оставить
   в корне. Контракты и реализации размещать в одной функциональной
   группе по [правилам репозитория](../../AGENTS.md#пространства-имён-и-структура-файлов).
2. Переносить исходники по подсистемам. Приводить namespace к фактическому
   пути, переносить связанные partial-файлы и обновлять ссылки, импорты
   и имена типов в генераторах. Для каждого перенесённого публичного типа
   зафиксировать старый и новый namespace и учесть его потребителей.
   Согласовать границы переносимых подсистем с [UIA1g](#uia1g-element-view-boundary)
   и [UIA1h](#uia1h-control-presenters), чтобы сократить повторные переносы.
3. Проверить соответствие путей и namespace, сборку Argentis, Alloy,
   Hosting, Editor, Player и затронутых демопроектов. Прогнать UI-тесты
   и проверить сгенерированные Fluent-вызовы и именованные части.
   Собрать независимого потребителя из локальных пакетов с обновлёнными
   импортами. Когда доступен пример со штатным и пользовательским контролами
   из [UIA1h.4](#uia1h-control-presenters), сверить его с новыми namespace.
   Обновить примеры и ссылки на перемещённые файлы.

**Реализовано в `1.2611.25.6-alpha`.** Пути 83 исходников соответствуют
девяти пространствам имён; обновлены анализаторы, встроенные C#-примеры
в PowerShell и прикладные потребители. [Проверки](STATUS.md#uia1-architecture).

**Карта миграции UIA1i, 25.11.2026.** Сборка и пакет по-прежнему называются
`TrueMoon.Argentis`. Меняется полное CLR-имя перенесённого типа: это **breaking
change** для исходных и ранее собранных потребителей. Импорт корня не импортирует
подпространства. Обновите `using`, псевдонимы (`Window`, `IView`, `IInputContext`),
reflection/assembly-qualified имена и шаблоны кода, затем пересоберите приложение
и его библиотеки на одной версии пакетов. Старые полные имена не являются API.

В каждой строке старое пространство имён — `TrueMoon.Argentis`; новое указано
полностью. Середина — имена файлов без `.cs`; если файл содержит другие типы,
они перечислены справа. Папка внутри проекта совпадает с последним сегментом
нового имени. Это карта `1.2611.25.6-alpha`; с `.7-alpha` штатные презентеры
перенесены в `TrueMoon.Alloy.Runtime` по [UIA1h.5](#uia1h5-external-presenters).

| Новое пространство имён | Файлы | Типы с другими именами или арностью |
| --- | --- | --- |
| `TrueMoon.Argentis.Layout` | `Border`, `ContentControl`, `ElementList`, `HStack`, `Panel`, `ScrollViewer`, `SplitOrientation`, `SplitView`, `StackBase`, `VStack` | `ElementList.cs`: `ElementCollection`, внутренний `IItemsBinding` |
| `TrueMoon.Argentis.Input` | `UiInput`, `UiKey`, `IInputContext`, `InputKind`, `IUiCommand`, `DelegateUiCommand`, `ButtonCommandBinding` | — |
| `TrueMoon.Argentis.Bindings` | `AttachmentDataContext`, `AttachmentPropertyObserver`, `Bindable`, `CollectionBinding`, `DataContextState`, `IDataContext`, `PropertyBinding`, `ViewModel` | В том числе публичный `IDataContext<TData>` |
| `TrueMoon.Argentis.Styling` | `Style`, `ViewStyleBuilder`, `ViewStyleExtensions`, `ViewStyleRole`, `ViewStyleRule`, `ViewStyleRule.Typed`, `ViewStyleScope`, `IViewStyle`, `IViewStyleBuilder`, `EditorColorRole`, `ControlTone`, `FontDefaults` | `Style.cs`: `Theme`; `ViewStyleRule.Typed.cs`: `ViewStyleRule<TElement>`; `ViewStyleRole<TElement>`, `IViewStyle<TView>`, `IViewStyleBuilder<TView>` |
| `TrueMoon.Argentis.Views` | `View`, `ViewCreation`, `IView`, `UiBuildContext`, `UiBuildExtensions`, `UiControl`, `IUiBuildContext`, `IUiControlFactories` | `View` и `View<TData>`, `IView` и `IView<TData>`, `IUiBuildContext<TData>`, `UiControl<TControl,TData>` |
| `TrueMoon.Argentis.Windows` | `Window`, `WindowCloseReason`, `WindowExtensions`, `WindowFrame`, `WindowGradient`, `WindowHostConnection`, `WindowPresentationState`, `WindowRegionRole`, `WindowTitleBar`, `IWindowController`, `IWindowHeaderActivity` | `Window<TData>`, `WindowResizeGrip` (в `WindowFrame`), `IWindowGradientDrawingContext` (в `WindowGradient`), `WindowFrameState` (в `WindowTitleBar`) |
| `TrueMoon.Argentis.Menus` | `MenuBar`, `MenuItem`, `MenuPopup`, `OverlayHost`, `OverlayHost.Validation` | `IMenu` (в `MenuBar`), `MenuAction` и `MenuGroup` (в `MenuItem`); обе декларации `OverlayHost` сохраняют одну область |
| `TrueMoon.Argentis.Editors` | `NumberBox`, `NumericAdjustment`, `NumericValueKind`, `PropertyEditorRow`, `RichText`, `RichTextEditor`, `TextBox`, `Vector3Editor` | `NumericGestureResult`; в `RichText.cs`: `RichTextStyle`, `RichTextRun`, `RichTextDocument`, `RichTextAlignment`, `RichTextEditorOptions`, `IRichTextLayoutService`, `IRichTextLayout`, `IRichTextDrawingContext` |
| `TrueMoon.Argentis.Painting` | `ElementPresentation`, `IElementPresenter`, `IAfterChildrenPresenter`, `IconButtonPresenter`, `ButtonPresenter`, `TextPresenter`, `ProgressBarPresenter`, `Drawing`, `BackdropMaterial`, `BoxShadow`, `IOpacityDrawingContext`, `UiSymbol`, `VectorIcon`, `TrueMoonMark` | `IElementPresenter<TState>`, `IAfterChildrenPresenter<TState>`, `IconButtonPaintState`, внутренний `BoundPresenter`; в `Drawing.cs`: `ITextLayoutService`, `IImageSource`, `IDrawingContext`; рядом с соответствующими типами: `IBackdropDrawingContext`, `IShadowDrawingContext`, `UiSymbols`, `IVectorIconDrawingContext` |

В корне остаются, в частности, `Element`, `ElementExtensions`, `UiProperty<T>`,
`UiProperties`, `Size`, `Rect`, `Thickness`, `Color`, `LayoutAlignment`, `Text`,
`Button`, `IconButton` и `TreeView`. Например, для `ui.TextBox().BindValueTwoWay(...)`
важны два разных импорта: `Views` предоставляет фабрику, а корень — сгенерированное
расширение привязки; если в исходнике указан сам тип `TextBox`, нужен `Editors`.
Именованные свойства генерируются в пространстве имён прикладного View, не в
`TrueMoon.Argentis.Views`. Генерацию на стороне приложения подключайте отдельно;
для доступа без анализатора используйте `GetRequiredPart<T>()` с новым типом.

<a id="uia1j-unused-ui-code-audit"></a>
### UIA1j. Аудит устаревшего и неиспользуемого кода UI

**Начат 24.11.2026.** Область аудита: Argentis, его генераторы, Alloy,
Hosting (на момент начала — с `Compatibility`), Rendering.Skia и Platform.Silk.
Тесты, демопроекты, Editor, Player и пакетные примеры учитываются как потребители.

1. Составить список устаревших входов, дублирующих путей и кандидатов без
   вызовов. Сверить прямые ссылки, регистрации DI и фабрик, генерируемые
   исходники, рефлексию, условную компиляцию и различия между хостами
   и backend. Для публичного API проверить, что входит в пакет и какие
   потребители и режимы совместимости от него зависят.
2. Для каждого кандидата записать область проверки, найденные точки
   входа и решение: сохранить, перевести на замену, пометить устаревшим
   или удалить. При недостаточных данных о внешнем использовании
   пометить кандидата как требующего сверки. Старый `IContentPresenter`
   и `VisualTree` оценить по вызовам из `Hosting.Compatibility`.
3. До [UIA1i](#uia1i-argentis-structure) полезно выделить кандидатов,
   чтобы не переносить код, который решено удалить. После
   [UIA1h.4](#uia1h-control-presenters) повторно сверить переходные пути
   отрисовки. Подтверждённую очистку выполнять отдельными порциями, для
   изменений публичного API фиксировать путь миграции.
4. Для каждой порции очистки собрать затронутые проекты и прогнать их
   тесты. Изменения пакетного API проверить на независимом NuGet-потребителе.
   Изменения путей Hosting или рендера дополнить проверкой затронутого
   хоста и backend. Результат аудита — список решений с основаниями
   и результатами проверок.

**Первичная сверка до переноса namespace, 25.11.2026.** Ссылки на символы
сверены с кодом фабрик, регистрациями, шаблонами Roslyn, строковыми именами,
условиями сборки, пакетными проектами и скриптами потребителей в `src`.
Отсутствие вызова в этой копии не доказывает отсутствие внешнего клиента NuGet.
Ниже решения по кандидатам, а не результаты запуска Editor, окна или пакета.

| Кандидат | Фактическая точка входа или основание | Решение до переноса |
| --- | --- | --- |
| `IContentPresenter` и `IContentPresenter<T>`, `IVisual`, `VisualTree` | Opt-in `UsePresentation<TView>()` регистрировал `PresentationInitializer`, `IVisualTreeBuilder` и обе фабрики презентеров. Инициализатор получал `StartupViewType` через `IServiceProvider.GetService(Type)`. `ViewHandle.RunCore` строил дерево; builder обходил `IContent.Child` и коллекции; GL presenter вызывал `visual.Presenter.Present` для каждого узла. | Сохранить отдельный путь совместимости на дату первичной сверки. Для новых окон использовать явный `UsePresentation(...AlloyOptions...)`. |
| `Rectagle`, `Line` | `UiControlFactories.CreateBuiltIns` регистрировал оба типа для `ui.Create<T>()`; старая фабрика также выбирала `RectangleContentPresenter` по `data is Rectagle`. | Сохранить контракты на дату первичной сверки. |
| `ITrigger`, `ITriggerSubscription`, `ElementMarginExtensions`, `ElementForegroundExtensions` (Argentis); `HelloWorldSample`, `SkiaViewPresenter` (Hosting.Compatibility) | У первых пяти не найдены внешние для объявления ссылки в исходниках этой копии. У двух классов расширений закомментированы методы, но сами классы компилируются. В `SkiaGlViewPresenterFactory.Create()` вызов `new SkiaViewPresenter()` — **комментарий**; фактически создаётся `SkiaGlViewPresenter`. Регистрация этих кандидатов по имени не обнаружена. | **Требуют сверки с внешними потребителями.** Все типы публичны в поставляемых сборках. Не удалять и не объявлять устаревшими на основании одного поиска. |
| `Compatibility/{RenderObject,SkiaVulkanViewPresenter,TextureData,ViewRenderContext}.cs` | Только комментарии и пробелы; явных ссылок из проектов или скриптов нет. Это не условный код и не экспортируемые CLR-типы. | **Можно удалить отдельной порцией**; путь миграции API не нужен. Не путать архивный `SkiaVulkanViewPresenter` с действующим `SkiaVulkanRenderBackend`. |
| Четыре атрибута `Fluent*Attribute`, генераторы Fluent и именованных частей, `PropertyBinding` | `ForAttributeWithMetadataName` находит внутренние атрибуты; вывод дополняет `ElementExtensions` и `ViewStyleExtensions`. [ViewPartsGenerator](../../TrueMoon.Argentis.Generator/ViewPartsGenerator.cs) сравнивает символы `View`, `Content`, `Named` и обёртки. [Обозреватель Editor](../../TrueMoon.Aether.Editor.UI/Hierarchy/EditorHierarchyView.cs) использует `BindValueTwoWay` и `Named("Tree")`, стиль — `TypedPart(view => view.Tree)`. `PropertyBinding` берёт `PropertyInfo` из выражения-селектора и проверяет setter, а не ищет UI-типы сканированием сборки. | Сохранить, при переносе обновить metadata names и шаблоны partial вместе с ручными объявлениями. [Проект Argentis](../../TrueMoon.Argentis/TrueMoon.Argentis.csproj) подключает анализатор с `PrivateAssets="all"`; это не доставка анализатора приложению. Сгенерированные методы самой библиотеки уже входят в её DLL. |
| `IUiSessionFactory.Create` и `IFactory<UiSession, UiSessionCreation>`; фабрика презентера `IconButton` | [Регистрация Hosting](../../TrueMoon.Alloy.Hosting/AppConfigurationExtensions.cs) предоставляет интерфейс сессии и типизированную фабрику; [HostedUiSessionFactoryTests](../../TrueMoon.Alloy.Hosting.Tests/HostedUiSessionFactoryTests.cs) используют типизированный вход. `HostedIconButtonPresenterTests` регистрируют подмену из приложения. [Пакетный пример](../../ManualTests/AlloyPackageConsumer/Program.cs) выбирает внешний raster или окно GL/Vulkan, различает Cobalt с `COBALT_GENERATION` и Microsoft DI. | Сохранить обе формы создания сессии, регистрацию и время жизни. Результат только одного DI-профиля не покрывает второй. |
| Три `Skia*RenderBackend`, необязательные интерфейсы рисования, нативная DLL | Фабрики `AlloyOptions.UseSkiaRaster/OpenGL/Vulkan` выбирают разные поверхности; все три вызывают `UiTreeRenderer.Draw`. `SkiaDrawingContext` реализует, в частности, контракты тени, групповой opacity и rich text. [SkiaNativeLibrary](../../TrueMoon.Alloy.Rendering.Skia/SkiaNativeLibrary.cs) назначает одну DLL двум сборкам; `VulkanInteropNative` вызывает экспорты `tm_skia_vk_*` через `LibraryImport`. [Правила пакета](../../TrueMoon.Alloy.Rendering.Skia/TrueMoon.Alloy.Rendering.Skia.csproj) поставляют win-x64 DLL и `build`/`buildTransitive` targets. | Сохранить. Общий обход не дублирует backend, а успешный CPU raster не проверяет нативную доставку, вызов ABI или GPU present. |
| `Win32WindowDpi`, `Win32WindowChrome`, `Win32AccentColors`, `Win32TransparentFramebuffer`, `Win32Clipboard` | [SilkWindowHost](../../TrueMoon.Alloy.Platform.Silk/SilkWindowHost.cs) выбирает реализации по ОС, наличию HWND, custom chrome и `PerPixel`. Сообщения окна приходят через native callbacks; Win32-методы вызываются через `LibraryImport`. Оконный Player выбирает `UseSkiaOpenGL` и `UseSilkWindow`; headless их не создаёт. | Сохранить. Не считать неактивную в конкретном режиме ветку или обратный вызов от ОС неиспользуемым кодом. |

Отдельная граница UIA1h.4: до миграции `DrawCore`/`DrawAfterChildren` обслуживали
контролы и прикладные viewport. Это работающие вызовы, подлежащие **замене**,
а не кандидаты на безусловное удаление. После миграции сверить overrides и
вызовы во всех поставляемых проектах, тестовом хосте, демо и встроенных
C#-примерах PowerShell. Результаты новой сборки, пакетной и нативной проверки
в эту первичную сверку не входят.

**Первый срез UIA1j в `1.2611.25.6-alpha`.** Четыре закомментированных файла
удалены; скомпилированные типы Compatibility ещё сохранялись. Проверка `.cs`
и `.ps1` в `src` не нашла вызовов или объявлений старого `DrawCore`. Штатный
общий путь — [`UiTreeRenderer`](../../TrueMoon.Alloy/Runtime/UiTreeRenderer.cs).
[Приёмка пакета и backend](STATUS.md#uia1-architecture).

<a id="uia1j-remove-hosting-compatibility"></a>
**Вывод Compatibility, `1.2611.25.13-alpha`.** Удалены отдельный оконный стек
и контракты Argentis `IViewManager`, `IVisual`, `IContentPresenter` обеих
арностей и `IContentPresenterContext`. `View`/`IView`, `IContent` и текущие
презентеры сохранены. Убраны прямые зависимости Hosting от Core и RichTextKit
(RichTextKit по-прежнему поставляется через Rendering.Skia). В текущих исходниках
TrueMoon, включая пакетные и ручные примеры, нет вызовов удалённой регистрации.
[Миграция](../../TrueMoon.Alloy.Hosting/README.md#миграция-с-hostingcompatibility).

Решение2026-10-22: [Aether DI1–DI4](../engine/PLAN.md#приведение-к-правилу-cobaltifactory)
добавляет Alloy→Contracts для IUiSessionFactory : IFactory<UiSession,UiSessionCreation>.
Существующий четырёхаргументный Create сохранён через default interface bridge.
<a id="uia1-view-registration"></a>
В `1.2611.25.15-alpha` `UsePresentation<TView>` заменяет
`UsePresentationFactory<TView>`: injected root factory вызывается
на UI owner thread, окно владеет root. `.View<TView>()` и `.View<TView,TViewStyle>()`
регистрируют `IFactory<TView>` через сгенерированные вызовы конструкторов, без
регистрации самого View. В обеих формах стиль `IViewStyle<TView>` опционален.
Явный callback и явная типизированная фабрика остаются. Borrowed session factory/lifecycle facades
не дублируют provider disposal owned window/dispatcher/session registry. Renderer,
targets и UI thread affinity прежние; конкретного DI provider в Alloy нет.

С `1.2611.25.16-alpha` генератор поставляется в NuGet-пакете `TrueMoon.Argentis`.
`buildTransitive` выбирает ровно один анализатор той же версии для прямой зависимости
и для Hosting. `EnableArgentisGenerator=false` отключает пакетный анализатор;
при сборке из исходников остаётся analyzer `ProjectReference`.

В `1.2611.25.17-alpha` сгенерированная фабрика родителя получает конкретные
View-параметры из `IFactory<TChild>`. Контейнер не хранит экземпляры. До
принятия в дерево продукты фабрик принадлежат активации или родительскому
View; сбой освобождает только неприсоединённых детей. Интерфейсные зависимости
не меняются. Явные `IFactory<T>` остаются для динамической композиции.

В `1.2611.25.18-alpha` динамические View регистрируются через
`.ViewWithArgs<TView, TArguments>()`; коллекция передаёт `rows.Create` в
`BindItems`. Генератор подставляет runtime-значение в один параметр точного
типа, остальные зависимости и опциональный стиль остаются в DI.
[Пример](../../TrueMoon.Alloy.Hosting/README.md#динамические-строки-из-фабрики).

[Миграция всех прикладных View Aether](../engine/PLAN.md#aether-view-authoring-migration)
ведётся в плане движка. Адресная приёмка механизма пройдена в Alloy.

<a id="uia1-view-package-acceptance"></a>
#### Приёмка пакетной композиции View

**Адресная приёмка пройдена 26.11.2026.** Один свежий набор из девяти пакетов,
два независимых Debug-потребителя по конфигурации Cobalt: шесть прогонов
Raster/OpenGL на Cobalt и Microsoft DI. [Версия и отчёт](HISTORY.md#uia1-view-packages-2026-11-26).
Пункты ниже — покрытие адресного профиля.

1. Сверены [два прежних расхождения](STATUS.md#ui-package-open-checks):
   allowlist CLR-ссылок Argentis сохранён без требования неиспользуемого
   `AssemblyRef`; ожидание дефолтного радиуса TextBox приведено к теме.
2. [Независимый потребитель](../../ManualTests/AlloyPackageConsumer/README.md)
   собран с одним анализатором Argentis без ручного `Analyzer` и
   `ProjectReference`. Конкретный ребёнок получен из `IFactory<TChild>` со
   стилем, а строка `BindItems` — из `IFactory<TRowView, TItem>` с аргументом
   коллекции, остальными зависимостями и отдельно зарегистрированным стилем.
3. Пройдены первый кадр и закрытие OpenGL на обоих DI-провайдерах, а также
   перемещение строк, освобождение дочернего дерева и снятие подписок.
   Это не заменяет полную матрицу 5.4/5.5 и K1–K9.

По запросу пользователя результаты [аудита2026-10-17](HISTORY.md#аудит-инфраструктуры-alloy--2026-10-17)
включены в модульный roadmap как **INF0–INF7**. Первичное добавление было
документационным. По следующему запросу пользователя2026-10-18 выполнен bounded
INF0 и реализованы INF1/INF2; после минимального INF5 выполнены INF3/INF4.
INF7 сопровождает их assembly-level checks и independent native/package consumers.
INF6 и bounded integration runner INF7 выполнены2026-10-19; расширение INF5
остаётся запланированным. [Реализация/собственные проверки INF1](HISTORY.md#inf0inf1-повторяемый-cleanup-registry--2026-10-18)
отделены от исходных audit evidence; общая готовность UI этим не объявляется.

Цель: UI сохраняет отзывчивость под потоком обновлений, завершает свои ресурсы
после отказа cleanup и предоставляет приложению явное управление кадрами/закрытием.
Scope: Argentis/Alloy runtime, Hosting, renderer/platform adapters и их проверки.
Существующие пять слоёв, thread affinity, borrowed GPU ownership, net10.0 и xUnit
сохраняются. Core/Cobalt/process lifecycle не переносятся в UI.

Это дополнительный UI backlog. Общая engine точка продолжения — в [его STATUS](../engine/STATUS.md);
[улучшения RichTextEditor](RICH_TEXT_EDITOR_PLAN.md) ведутся отдельно и отложены.
INF не заменяет этапы1–5/K1–K9/PA1–PA10, не снимает physical DPI/K7/K8/VRAM
и не возобновляет остановленный поиск NVIDIA workaround. Конкретные новые API,
лимиты очередей и compatibility policy уточнять перед соответствующим этапом.

### Этапы INF и критерии завершения

| ID | Результат | Состояние | Зависимость и критерий завершения |
| --- | --- | --- | --- |
| INF0 | Сверка исходного состояния и границ | Выполнен2026-10-18 в bounded audit scope | Повторить два audit repro на выбранном source; описать registry/queue/frame/close contracts и зафиксировать собственный baseline. Existing tests/native results сохраняют свои даты и scope |
| INF1 | Повторяемый cleanup registry | Реализован2026-10-18; проверки в STATUS/HISTORY | После начала Stop новые Create запрещены; failed cleanup не скрывает оставшиеся owned sessions; повторный Stop/Dispose пытается завершить их cleanup, состояние завершения означает отсутствие owned sessions. Первичная ошибка и ошибки cleanup наблюдаемы |
| INF2 | Ограниченный dispatcher/posted work | Реализован2026-10-18; проверки в STATUS/HISTORY | После INF1: ограничить work за один Update, определить capacity/overflow policy и atomic accept/close; предоставить awaitable dispatch с cancellation/error semantics. При нагрузке очередь не растёт бесконтрольно, layout/render/input и соседние окна получают выполнение |
| INF3 | Управление запросами кадров | Реализован2026-10-18; scope/checks в STATUS/HISTORY | После INF2 и минимальных INF5 metrics: wake/request на pending work и invalidation, idle/continuous режимы и явные update/render/present phases. Clean UI не требует постоянной presentation; expose/resize/DPI/animations/external HUD корректно возобновляют работу |
| INF4 | Согласованное закрытие окна | Реализован2026-10-18; собственные проверки в STATUS/HISTORY | Поверх INF1/INF2: owner-safe асинхронный close request с cancel/save/drain, различение запроса закрытия и факта закрытия. Native close, API close и App Stop согласованы; фоновые работы завершаются до освобождения зависимых UI/native ресурсов |
| INF5 | Общая диагностика UI | Минимум реализован2026-10-18; расширение запланировано | Session/window IDs, lifecycle state, queue depth/high-water/processed work, measure/arrange/render/present counters/timings и bounded snapshots. Existing Vulkan resource counters переиспользовать; diagnostics opt-in, без unbounded history и зависимости core UI от конкретного telemetry provider |
| INF6 | Явные factories и совместимость интеграций | Реализован2026-10-19; собственные проверки в STATUS/HISTORY | Root factory overload устраняет необходимость скрытой specialization generic TView для Cobalt; Cobalt/DI сохраняются. Renderer/target/presenter compatibility описана явно вместо неявного выбора private bool flags; unsupported pair отвергается до native allocation |
| INF7 | Проверки реальных сборок и поставки | Реализован2026-10-19 в bounded runner scope; проверки в STATUS/HISTORY | Сопровождает INF1–INF6, не откладывается до конца: новые contract checks обращаются к Hosting/Platform через ProjectReference и portable fake seams; native/package consumers отдельно проверяют compiled assemblies, analyzer/native delivery и cleanup |

### INF1: registry shutdown после отказа

- Воспроизвести вызов factory.Dispose из posted action во время UiSession.Update:
  запрет disposal внутри Update сохраняется, но subsequent cleanup должен оставаться
  возможным. Проверить partial failure с несколькими sessions и повторные Stop/Dispose.
- Разделить запрет новых sessions, незавершённый cleanup и окончательное завершение;
  конкретная форма состояний определяется в реализации. Не откатывать запрет Create
  после начала shutdown и не объявлять живую session освобождённой.
- Сохранить проверки owner thread, tree mutation/reentrancy и borrowed frame.
  После возврата lease/выхода из Update явный retry освобождает оставшиеся sessions;
  ошибки одного объекта не отменяют попытки cleanup остальных.

Уточнение реализации2026-10-18: принятый Stop/Dispose закрывает Create до любых
preflight checks; preflight failure сохраняет sessions для owner retry. Все
session guards проверяются до cleanup, затем ошибки отдельных Dispose собираются
без прекращения остальных попыток. Реентрантный cleanup возвращается к внешней
попытке. Stop из backend/attachment callback во время создания session отклоняется
до принятия shutdown request: неуспешный Create не забирает ownership root.
StopAsync остаётся синхронным owner-thread cleanup без прерывания посередине;
новый async close относится к INF4. TrueMoon.Alloy.Hosting.Tests использует реальный
ProjectReference на Hosting, без Compile-linked production copies, и включён в
TrueMoon.slnx. Новых native ABI/dependencies/public signatures не введено.

### INF2: queue и dispatch contract

Решение2026-10-18: `UiDispatchOptions` задаёт immutable `Capacity=1024` и
`ActionsPerUpdate=64`, оба строго положительные. Прежние constructor/Create
signatures сохранены; новые overloads принимают options после clipboard.
Hosted factories используют defaults; отдельная Hosting configuration здесь не
добавляется. Action budget ограничивает число начатых queued callbacks, включая
новые self-posts в этом Update, но не время одного callback. Свободное место
появляется при извлечении, отмене или закрытии; уже выполняемый callback не pending.

`Post(Action)` принимает FIFO work или синхронно бросает InvalidOperationException
при overflow; callback error выходит из Update, оставшиеся commands доступны
следующему Update. `InvokeAsync(Action, CancellationToken)` с worker ставит ту же
FIFO работу; task сообщает rejection/exception/completion. На owner callback
выполняется сразу, обходя очередь/capacity/budget, в том числе из Update: ожидание
своего вызова не требует следующего кадра. Это не async delegate API, task
continuations не привязываются к owner; блокировать owner на task, ранее поставленном
worker, нельзя. Pending task continuations выполняются асинхронно.

Cancellation до извлечения удаляет команду и освобождает capacity, после начала
callback не прерывает его и не меняет результат. Отменённый token на живой сессии
не занимает место даже при полной очереди. Accept/cancel/close сериализованы gate.
После успешных Dispose preflight checks очередь закрыта до owned cleanup;
ожидающие tasks fault с ObjectDisposedException. Failed guard сохраняет очередь.
Terminal rendering failure закрывает её с InvalidOperationException и первым
UiRenderingException в InnerException; сам callback, бросивший UiRenderingException,
faults свой task этой ошибкой и переводит session в terminal state. Обычная callback
ошибка InvokeAsync fault только task, не останавливая следующие commands.

Coalescing применяется явно только к latest-value PropertyBinding per attachment:
одна pending synchronization читает актуальный source, rejected enqueue сбрасывает
pending guard. CollectionBinding и attachment-generation guards сохранены.
Произвольные Action не объединяются. [Проверки INF2](HISTORY.md#inf2-ограниченный-dispatcher--2026-10-18).

Исходные acceptance требования сохраняются:

- Выбрать измеримый action budget или time slice и отдельно предел pending work.
  Self-posting/continuous producer не должен удерживать один Update бесконечно.
  Для очереди задать явную реакцию на overflow; произвольные commands/input cleanup
  не отбрасывать молча. Сохранить FIFO для принятых обычных commands.
- Awaitable dispatch должен сообщать completion/exception/cancellation, не допускать
  synchronous wait deadlock на UI owner и иметь определённое поведение при shutdown.
  Post versus Dispose/fault проверять как race принятия работы и закрытия очереди.
- Coalescing — явная политика для подходящих обновлений, например latest property
  synchronization; не применять её ко всем Action. CollectionBinding уже coalesces:
  сохранить его semantics и attachment-generation guards, учесть PropertyBinding.
- Проверить flood, очередь на границе capacity, отмену до/после принятия, callback
  error, detach/dispose и fairness нескольких окон. Это behavioral acceptance,
  а не обещание конкретного CPU/GPU ускорения.

### INF3–INF6: условия интеграции

INF3 — принято2026-10-18: runtime `UiFrameMode` нейтрален к renderer/platform.
`SilkUiWindowOptions.FrameMode=Automatic` сохраняет continuous polling при наличии
FramePresented observers и работает on-demand без них. Явный OnDemand не включает
polling из-за observers; Continuous задаёт его независимо от observers. Existing
consumers не теряют heartbeat. Engine viewport явно выбирает Continuous для
receiver polling и RequestFrame после dynamic pixel upload; event-based transport
notifications в engine этим шагом не добавляются.

`FramesPerSecond=60`, допустим1–240: лимит scheduled frame attempts на монотонном
TimeProvider. Native refresh и input geometry update могут выполняться сразу.
`UiSession.RequestFrame()` и `HostedUiWindow.RequestFrame()` thread-safe/coalesced,
не занимают command capacity. Session request означает redraw на следующем Update;
window request допускается до startup, отклоняется после Close. Request во время
draw сохраняется до следующего кадра. Invalidation/accepted Post/InvokeAsync
сигнализируют private host wake; произвольные user callbacks под queue gate не
выполняются. Close/fault закрывает запросы и снимает signal, failed Dispose guards
сохраняют live session. External host по-прежнему владеет своим loop/Update/output.

OnDemand разделяет Update, draw и output: clean state не вызывает Update/present,
native expose показывает last output без нового draw. Native resize/DPI/restore
применяются до output. Minimized viewport считается empty; posted work при
suspension обрабатывается, visual dirty/redraw сохраняются до resume. Skipped
presentation повторяется с frame deadline, ошибка сохраняется без замены причины.
Private UiFramePump имеет injectable TimeProvider для deterministic net10/xUnit
tests; real Hosting assembly используется через ProjectReference.

Shared owner выбирает minimum participant wait и будится на работу/close. Live
windows имеют bounded OS-event polling до16ms; idle CPU/GPU reduction без замеров
не объявляется. Startup batch32 и native commands64/iteration сохраняют fairness,
session callbacks ограничены INF2 budget. `SilkWindowHost.Pump()`/Run сохраняют
periodic semantics; overload `Pump(render:false)` выключает periodic DoRender,
сохраняя native refresh. Новые resize/state callbacks активны только в manual loop.

Уточнение output contract по native finding: `NeedsUpdate` включает pending FIFO/
future frame requests, `NeedsRender` — unapplied visual dirty текущего дерева/
viewport. Raster/OpenGL/Vulkan output guards используют NeedsRender. Пока queue
ещё ожидает следующего bounded Update, last completed image допустим для output;
прямое изменение tree/viewport по-прежнему запрещает export до Update. Export не
выполняет очередь и не обещает применение ещё ожидающих model commands. Это явное
расширение прежнего guard, необходимое для presentation при bounded queue/flood.
[Собственные regressions/native/package/golden evidence](HISTORY.md#inf3-запросы-кадров-и-scheduling--2026-10-18).

Уточнение реализации2026-10-29 по ED6a.2 native finding: `Update()` может оставить
`NeedsRender=true`, если selection reveal/Arrange или Draw снова invalidated tree.
UiFramePump не экспортирует такой кадр, сохраняет request и выполняет следующий
bounded update на очередном deadline. Повторного unbounded Update-loop нет;
OpenGL/Raster/Vulkan output guards сохраняются. Pending FIFO без visual dirty
по-прежнему допускает last completed image. [RED/GREEN](HISTORY.md#inf3-dirty-frame-после-update--2026-10-29).

INF4 — решение2026-10-18: `HostedUiWindow.RequestCloseAsync(token)` — cancellable
user request (`Application`), native close — тот же путь с reason `Native`.
`Closing` содержит sequential `Func<UiCloseReason, Task<bool>>` handlers: snapshot
на попытку, начало каждого на owner, async continuation без UI affinity. False
прекращает user attempt; error/canceled handler оставляет окно открытым, faults
request, сохраняется в `CloseError`, включая native request. Retry сбрасывает error.
Concurrent requests делят попытку. Caller cancellation отменяет только его wait;
pre-canceled request не принимается. Never-started close не вызывает handlers.

`Close`/StopAsync/dispatcher App Stop остаются forced: upgrade pending attempt
не повторяет начатые handlers, игнорирует veto, остальные получают `Shutdown`.
Save/drain errors собираются до cleanup; одна ошибка не пропускает остальные
handlers. Owner продолжает обслуживать UI/другие окна, bounded handler batch64.
Native, handler и cleanup errors сохраняются вместе. Terminal native/graphics
failure может лишить drain возможности обращаться к UI, но не разрешает освобождать
зависимые ресурсы до уже начатого drain. Таймаут/abort чужих tasks не вводится.

`IsCloseRequested` — pending attempt; true result RequestCloseAsync и `IsClosed`
означают успешное завершение `Completion` после owner-thread native/GPU/UI cleanup.
Faulted cleanup не объявляется successful closed. Legacy constructor `closed`
сохраняет lifetime callback после попытки cleanup, включая failure, и явно не
является success notification. Внутри Closing нельзя ждать собственный close/
Completion/window или dispatcher StopAsync: cleanup зависит от возврата handler.
После await UI mutations возвращаются через InvokeAsync/PostWindow. Producers
останавливаются до ожидания принятых controller/compiler/game tasks. External HUD
сохраняет собственный owner lifecycle; engine viewport использует этот close API.

INF6 — решение2026-10-19: новый non-generic
`UsePresentation(Func<IServiceResolver, Element>, Action<AlloyOptions>)` получает
root на owner thread после готовности target, не регистрирует/не resolve-ит view.
Cobalt и Microsoft DI сохраняются. Полностью factory-based dependency graph не
требует generated view resolver. Исторический generic overload с concrete
consumer graph заменён [в `1.2611.25.15-alpha`](#uia1-view-registration).
Factory возвращает fresh detached root без Parent.
Null/disposed/parented/attached root отвергается; чужое дерево не disposing/detaching.
Допустимый returned root принадлежит окну, в том числе при subsequent startup failure;
partial objects, которые factory не вернула, остаются ответственностью factory.
Startup/Completion сохраняют причину ошибки. Dependencies живут до UI cleanup.

`UiRendererKind` вместо двух private selection flags описывает None/Custom/Raster/
OpenGL/Vulkan. `SilkUiWindowOptions.GraphicsApi` явно выбирает target/presenter;
null выводится из выбранного GPU renderer при UsePresentation, direct window
сохраняет default Vulkan. Explicit mismatch отвергается до регистрации services.
Настройки renderer/window копируются при регистрации до Build/service resolution;
appearance/chrome immutable, callback delegates/captures не deep-clone.

Известная matrix: Raster→RasterUiTarget только external; OpenGL→OpenGLUiTarget +
OpenGL presenter; Vulkan→VulkanUiTarget + Vulkan presenter. Target guards built-in
backends отвергают mismatch до native surface/attachment. `IUiWindowSessionFactory`
декларирует pure SupportsWindowGraphics до native window allocation; real
HostedUiSessionFactory предоставляет metadata известных Skia GPU pairs. Raster и
unknown custom backend standalone не заявляются поддержанными; UseRenderer остаётся
external-only. Для custom standalone factory одного IUiSessionFactory теперь
недостаточно: реализовать новый explicit capability contract. Это намеренное
ужесточение alpha preflight, existing signatures/generic path не удаляются.
Custom declaration не доказывается пробным native allocation. External factories,
five layers и borrowed GPU ownership сохраняются. Публичный Cobalt/DI consumer —
[AlloyFactoryConsumer](../../ManualTests/AlloyFactoryConsumer/README.md).

Минимальный INF5 — решение2026-10-18: `UiSession.EnableDiagnostics(long? windowId)`
включает observation с текущего момента. `Id` session/window стабилен в пределах
процесса, одинаковые числовые IDs разных domains не сравниваются без типа.
`SilkUiWindowOptions.EnableDiagnostics` включает session collector до первого
Update; `HostedUiWindow.Diagnostics` сохраняет его после cleanup. Existing
constructor/Create signatures и dependency graph не меняются.

`UiDiagnostics.Snapshot` — согласованная value copy под collector lock, доступная
с любого потока и после disposal. Lifecycle Active/Suspended/Faulted/Disposing/
Disposed, accepted viewport и nullable classification первого rendering failure
не требуют хранения exception/tree/device/callback references. Suspended означает
zero-size viewport; Idle отдельным lifecycle состоянием здесь не вводится.

Queue имеет InitialPending/Pending/HighWater и Accepted/Started/Completed/Failed/
Canceled/Rejected/Discarded с согласованным accept/cancel/close gate. InitialPending
отдельно от новых Accepted; owner-inline InvokeAsync и pre-canceled requests в FIFO
не входят. Capacity/closed rejections наблюдаемы. Команда, уже выполнявшаяся при
Enable, и её partial Update не записываются как новые операции.

Update/root measure/root arrange/surface render/host present учитываются раздельно:
attempts/completed/failed и total/last/max CPU wall time; present имеет skipped.
Clean/suspended Update — completed Update без фиктивных layout/render/present.
Root invocations не означают обход всех cached descendants. Update включает
вложенные фазы, totals не складываются. Present измеряется вокруг output, после
Update и до FramePresented observers; Vulkan false result — skipped. External
host может вызвать `RecordPresentation(elapsed, outcome)` на collector. Это не
presentation command и не GPU completion/timestamps. Vulkan resource counters
сохраняют существующий `VulkanWindowPresenter.Resources`, не дублируются.

Хранятся фиксированные скаляры и пять агрегатов фаз; ring/history/samples,
telemetry provider/events и reset/disable API не добавляются. Выключенный путь
не читает Stopwatch и не создаёт collector; остаются process IDs, cached host
delegate и null checks. Включённый путь добавляет collector locks, timestamps и
копирование фиксированных value counters; overhead проверяется отдельно на
headless idle/posted workload. CPU/GPU ускорение и idle policy не обещаются.
[Собственные проверки/измерения](HISTORY.md#inf5-минимальная-диагностика--2026-10-18).

Остаток полного INF5: window lifecycle до session creation/после failed startup,
Hosting command-queue metrics, общая resource/diagnostic projection и exporters/
нагрузочные contention measurements при конкретной потребности. Это не блокирует
bounded INF3 с уже доступными session/phase/queue observations. Performance budgets,
GPU timing/VRAM и аппаратная alpha matrix остаются самостоятельными acceptance.

- Frame scheduling вводить после baseline/минимальных diagnostics. Нужны explicit
  frame requests для dynamic image/engine viewport и отдельная возможность polling
  или continuous rendering: текущие FramePresented consumers не должны потерять
  источник следующих кадров. Показ неизменённого содержимого после expose/restore,
  resize/DPI и suspended zero-size проверять отдельно от dirty redraw.
- Close API не блокирует owner thread ожиданием controller/compiler/game tasks.
  Проверить отказ пользователя от закрытия, failed save/drain, повторные/concurrent
  close requests и cleanup ошибки; ошибку не маскировать успешным Closed событием.
  Семантику native close и принудительного App Stop зафиксировать раздельно.
- Metrics должны различать update/layout/render/present и suspended/failed state.
  Зафиксировать overhead diagnostics и bounded storage; idle CPU/GPU выводы делать
  только по замерам на указанной конфигурации, не по уменьшению числа callbacks.
- Factories/capabilities не требуют смены DI provider или удаления нынешних явных
  renderer overloads. Для нового root factory нужны Cobalt/DI source и independent
  package checks; для presenter/target — positive/negative compatibility scenarios.

### INF7: общая проверка изменений

Для каждого этапа — собственные xUnit regressions на net10.0 с assertions поведения,
отказа и cleanup; при изменении platform/Hosting контракта тестировать реальную
production assembly, не только Compile-linked copy исходника. Portable fake surface/
window seam не выдаётся за native proof. Existing linked tests не удалять массово:
перенос обосновывать конкретным contract и сохранением проверяемого поведения.

Relevant native сценарии выполнять bounded, с cleanup только owned процессов и
фактическими resource counters. Изменение scheduling/close требует standalone и
external HUD, нескольких окон, resize/suspend/resume, Debug apphost/published Release
в supported scope. Изменение public/package API требует independent consumer;
rendering/text изменения — existing read-only golden checks без подгонки эталонов.
Полный UI/native/performance pass и закрытие hardware blockers остаются отдельными
acceptance, historical results не переносить на новый source автоматически.

### Последующие исследования

INF7 — решение2026-10-19: tracked RunIntegration.ps1 и Integration helpers в
[AlloyFactoryConsumer](../../ManualTests/AlloyFactoryConsumer/README.md#inf7-воспроизводимый-integration-runner)
предоставляют Assembly/Source/Package/All profiles. Assembly — real Hosting и UI
suites Debug/Release; Source — factory/generic/failure consumers и native
scheduling/close/dispatcher/HUD; Package — fresh graph/isolated cache,8factory
consumers без analyzer/view registration и проверка обязательных analyzer/native
payloads. Все selected phases/report matrices должны завершиться; zero/failed/
skipped discovery не passing. Native validation обязательна и process-local.
Timeout завершает только owned process tree; logs/partial manifest сохраняются.

Fresh run directory не перезаписывается. Manifest фиксирует scope, SDK, commands,
reports, input set/source/binary/package hashes. Отдельный read-only verifier
повторяет completeness/provenance checks, не обновляя evidence. Tracked guards
проверяют quoting/UTF-8, failure/timeout/ownership, prerequisite rejection, TRX,
partial matrix, source/artifact drift, source package dependency и missing payload.
Временные scripts в TestResults не являются зависимостью runner. Producer
bin/obj общие: один runner на checkout, source/binary drift отвергается.
Existing linked checks не удаляются массово; при следующих contract changes
переносить targeted проверки на actual assembly с сохранением поведения.
Runner не объявляет новый full-solution/golden/RTE/engine/performance/hardware pass.

После diagnostics и конкретного workload рассмотреть scoped image/font resources,
асинхронную загрузку и reuse dynamic image storage с явным ownership. Выигрыш против
нынешней owned pixel copy подтвердить замерами; shared mapped/GPU pointers не
вводить как неявное ускорение. Dirty-subtree layout/virtualization — после baseline
large-tree сценария и проверки measure/arrange invalidation.

Для `PropertiesBase` и `IView` граница и путь миграции требуют отдельного
решения и consumer checks. Старый оконный стек удалён в
[UIA1j](#uia1j-remove-hosting-compatibility).

INF0–INF4/INF6/INF7 и минимальный INF5 выполнены в указанном scope. Bounded
инфраструктурная последовательность завершена; следующий общий шаг — **ENG3**.
Полный INF5 остаётся частичным и расширяется при конкретном consumer/workload;
linked checks переносить по конкретному contract, без массового удаления.
номера INF не задают новую глобальную очередь вместо engine/RTE планов.

## PA7a: RichTextEditor перед ENG1 — 2026-10-17

Дальнейшие улучшения вынесены в отдельный
[план RichTextEditor](RICH_TEXT_EDITOR_PLAN.md). По решению пользователя2026-10-17
они отложены до дальнейшей работы над функциями UI-модуля. Подробные задачи и
acceptance ведутся только там. Назначенный после PA7a ENG1 выполнен2026-10-17;
актуальный общий шаг — ENG3, [engine статус](../engine/STATUS.md).

По прямому запросу пользователя перед ENG1 реализуется RichTextEditor поверх
Topten.RichTextKit. Это отдельное ограниченное расширение текста, а не начало
всей beta/PA1–PA10 или закрытие PA7/alpha. net10.0/xUnit и независимость Argentis
от Skia/Alloy сохраняются. [API/options/границы](RICH_TEXT_EDITOR.md).

Контракт: neutral immutable rich document/styles/options и retained control в
Argentis; RichTextKit0.6.170 shaping/layout/hit testing/painting только в
Rendering.Skia. Common adapter используется raster/OpenGL/Vulkan. Поддержать
multiline input, UTF-16/grapheme selection/navigation, plain clipboard,
formatting selection/insertion, bounded undo/redo, read-only/word-wrap/alignment,
общие Element font/color/padding и независимый toolbar example.

Acceptance: behavioral xUnit regressions editing/binding/format/history/input/
cleanup и реальные raster pixels; Debug/published Release native smoke3backends;
fresh independent package consumer с rich editor и direct renderer; прежние
K2/K4 golden fixtures/tolerances не перезаписывать. Code/test/native/package
results фиксировать отдельно. IME/accessibility, bidi caret affinity, rich clipboard,
RTF/HTML import/export, lists/tables и long native resource trend вне этого scope.
RichTextKit process shaper caches не объявлять очищенными при session Dispose.

Результат2026-10-17: компонент реализован; UI563/563 Debug/Release,6native demo
cases,40fresh package processes и прежний read-only Settings golden360 прошли.
[Точные команды, failures и границы вывода](HISTORY.md#richtexteditor--2026-10-17).

После этого UI запроса выполнены headless ENG1 и ENG2 CPU viewport;
актуальная точка продолжения — ENG3. Само расширение PA7a engine code не создавало.
NVIDIA K7/physical DPI/loss/VRAM/final alpha сохраняются.

## Проход качества UI после headless модулей — 2026-10-15

По [общей сводке качества](../QUALITY.md) следующий ограниченный scope — обновить
5.4/5.5 independent package consumer после bundled Cobalt и explicit font catalog.
Это уточнение очередности quality работы, не изменение архитектуры, alpha criteria,
net10.0/xUnit, K1–K9/PA1–PA10 или решения пользователя остановить NVIDIA workaround search.

Исходная сверка: существующий AlloyPackageConsumer использует только UseDI и
backend overloads без explicit fonts. Предыдущие package16 и font golden360
имеют отдельные runs2026-10-11; совместного нового proof нет. Runtime failure
в этом quality шаге ещё не воспроизводился.

Acceptance следующего code этапа:

- Переиспользовать existing consumer: Cobalt и Microsoft DI как независимые варианты,
  общий View/model и собственный TrueMoon registration API. Не переводить остальные
  UI applications массово на Cobalt; adapter path и provider selectors сохранить.
- Fresh unique local packages/feed/cache, независимые consumer build/CPM settings,
  только PackageReference, Debug/Release ×build/publish, Windows x64/net10.0,
  framework-dependent без RID. Producer graph учитывает IsPackable=false generator;
  matching bundled analyzer Cobalt реально участвует в compilation, его DLL не
  попадает в runtime output. Вариант Microsoft DI с отключённым Cobalt analyzer
  подтверждает, что consumer не зависит от generated Cobalt graph.
- Сохранить Raster/OpenGL/Vulkan Hosting smoke и direct renderer без Hosting/App.
  Подключить существующий SkiaFontCatalog с лицензированными фиксированными fonts;
  убедиться в фактической доставке fonts/license/native DLL, matching source/package/
  compiler/generated provenance и достижимости новых overloads из пакетов.
- Проверять реальный render/update и bounded lifecycle: startup, model/binding
  updates, отсутствие owned sessions/subscriptions после Stop/Dispose, правильный
  native LoadedPath. Контракт владения каждого provider сохраняется, alias disposal
  counts не объявляются одинаковыми без проверки. Не заменять checks одним build.
- Сохранить failure logs, case deadlines, cleanup только own launched processes,
  fresh output directories и итоговую проверку source/nupkg/generated/native/font
  fingerprints. Точные commands и actual case counts записать после выполнения;
  Completed требует всей заявленной matrix, negative control учитывается отдельно.
- Выполнить текущую UI xUnit suite и relevant Core/Cobalt regressions по фактически
  затронутому коду. При изменении fonts/native/renderer read-only K2/K4 golden
  повторяется с прежними fixtures/tolerances; эталоны не перезаписывать ради passing.

Runtime fixes вносить только при воспроизводимом defect. Этот пакетный proof не
объявляет снятым K7, physical DPI/loss/VRAM или final alpha, не меняет driver/GPU/
Windows settings и не начинает engine. После него обновить сводку качества и
выбрать следующий доступный UI/ENG0 scope с явно сохранёнными ограничениями.

**Результат2026-10-15:** scope выполнен:10fresh nupkgs,36App +4direct processes,
Cobalt/DI и DI без analyzer, explicit fonts/native delivery, fingerprints и
negative analyzer control. Воспроизведён missing resolver HostedUiDispatcher;
регистрация заменена явной factory без зависимости Hosting от Cobalt.
Consumer явно регистрирует concrete View/model graph для source generator.
UI532/Core107/Cobalt179/generator91 отдельно Debug/Release; read-only golden360
с validation teardown прошли. [Команды, failures и границы](HISTORY.md#ui-packagehosting-quality--2026-10-15).
Архитектура и критерии alpha сохранены. Следующим решением пользователя2026-10-15
выбран [ограниченный ENG0](../engine/PLAN.md#eng0-принятый-scope-первого-прототипа--2026-10-15)
с открытыми UI рисками; тогда назначен headless ENG1 (выполнен2026-10-17). Это допуск
прототипа, не K3/K7/K8/VRAM/final alpha pass и не автоматическое начало PA1–PA10.

## Цель и основные решения

Создать гибкий UI-фреймворк с Fluent API на C# и сменяемыми бекендами, пригодный для самостоятельных Windows-приложений и встраивания в качестве GUI/HUD в Vulkan-приложение. Представления и пользовательские элементы описываются в C# классах.

Первая версия должна демонстрировать два сценария:

1. Небольшое самостоятельное приложение с динамическим содержимым.
2. Интерактивный HUD поверх графической сцены.

В обоих сценариях используются одинаковые контролы, layout, состояние и механизм обновления UI. Различаются владение окном и графическими ресурсами, а также организация цикла приложения. HUD получает размер поверхности, ввод и запрос на отрисовку от игры; собственное окно и цикл приложения ему не требуются.

Основная модель — постоянное дерево элементов с изменяемыми свойствами, явными привязками данных и заменяемыми поддеревьями. Точечные изменения включают обновление свойств, замену содержимого контейнера, добавление и удаление элементов коллекции. Неизменённые экземпляры сохраняют фокус, выделение и прокрутку.

Fluent-вызовы создают и настраивают элементы. Для динамических областей используются наблюдаемые коллекции и явная замена содержимого. Полное сопоставление заново построенных деревьев откладывается до появления такой потребности.

Согласовано с пользователем 2026-10-06: контролы можно создавать как публичным конструктором (`new Button(...)`, `new Text(...)`), так и статической фабрикой на самом типе (`Button.Simple(...)`, `Text.Plain(...)`). Оба способа равноправны и могут смешиваться в одной Fluent-разметке. Фабрика возвращает новый экземпляр конкретного контрола для дальнейшей настройки существующими Fluent-методами. Фабрики **4.8** реализованы для существующих типов 2026-10-07; исходное решение сохраняется. [Синтаксис и пример](../../TrueMoon.Alloy.Hosting/README.md#создание-контролов-и-fluent-разметка).

Основной графический стек — SkiaSharp поверх Vulkan. Возможность создания контекста через `GRContext.CreateVulkan` уже исследовалась в прототипе. Пригодность конкретных пакетов, работа поверх внешнего GPU-хоста, создание устройства, swapchain и синхронизация должны быть проверены первым графическим этапом.

## Архитектура и публичные границы

Сохранить Argentis и Alloy; конкретные интеграции вынести в отдельные сборки.

| Модуль | Ответственность |
| --- | --- |
| `TrueMoon.Argentis` | Контролы, композиция, свойства, стили, Fluent API и независимые контракты layout/рисования |
| `TrueMoon.Alloy` | Выполнение UI: обход дерева, layout, обновления, маршрутизация ввода, фокус и жизненный цикл |
| `TrueMoon.Alloy.Rendering.Skia` | Отрисовка и измерение текста через Skia; поверхности Vulkan, OpenGL и raster |
| `TrueMoon.Alloy.Platform.Silk` | Окна, события ОС и ввод через Silk.NET |
| `TrueMoon.Alloy.Hosting` | Интеграция с `App.Builder`, DI, жизненным циклом TrueMoon и связывание выбранного renderer с оконной платформой |

Уточнение имени по запросу пользователя, 2026-10-04: hosting-проект, каталог, сборка и namespace называются `TrueMoon.Alloy.Hosting`. Повторный суффикс `TrueMoon` удалён; ответственность модуля и API подключения сохраняются.

Уточнение поставки native Skia, 2026-10-04: проверенная DLL с Vulkan interop хранится в существующем `TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64` вместе с revisions/hash. Renderer копирует её при build/publish в отдельный `truemoon-native/win-x64`, чтобы избежать конфликта со штатными native assets. Публичный SkiaNativeLibrary загружает один экземпляр DLL для обоих bindings. Отдельного native-проекта не создаётся. Инструкция пересборки: [README renderer](../../TrueMoon.Alloy.Rendering.Skia/README.md).

Обновление2026-10-28 по запросу пользователя: managed/native пара **4.154.0**
собрана и проверена в source Debug/published Release Vulkan interop/backend scope.
ABI v1/48-byte descriptor и sole ownership сохраняются; official version gate
и extension exports обязательны. Новые pins и условный depot_tools учитывают
изменения upstream build recipe. [Текущие результаты и границы](HISTORY.md#skiasharp-41540-и-vulkan-interop--2026-10-28).
Этот quality проход не закрывает independent NuGet ARCH2e, GUI/hardware/K7/VRAM.

Argentis не зависит от Alloy, SkiaSharp или Silk.NET. Контрол владеет поведением,
состоянием и layout; внешний презентер отвечает за рисование в общем
[рендерере Alloy](#uia1h5-external-presenters).

Основные точки расширения:

- `UiSession` — корневое представление, обработка ввода, обновление, layout, рендеринг и освобождение ресурсов.
- `IWindowHost` — окно, размер framebuffer, DPI, платформенные события и сервисы.
- `IRenderBackend` — создание графической сессии и вывод UI на поверхность.
- `IDrawingContext` и `ITextLayoutService` — геометрия, clipping, трансформации, изображения и текст без типов Skia.
- Отдельный Vulkan-контракт подключения внешнего устройства и обмена GPU-ресурсами. Vulkan-типы остаются внутри соответствующей интеграции.

Оконный путь — `UsePresentation<TView>(configure)` с `IFactory<TView>` либо
`UsePresentation(rootFactory, configure)` с явной фабрикой. Выбор renderer и
хоста обязателен; старой перегрузки без настроек нет.
[Регистрация и миграция](#uia1-view-registration).

Внешний App-хост использует `UseAlloy(options => options.UseSkiaVulkan().UseExternalHost())`:
он регистрирует renderer и `IUiSessionFactory` без окна или устройства. Для HUD с
собственным lifecycle доступен `UiSession.Create` без App и DI-контейнера.

Vulkan surface/lease и публичный `SkiaNativeLibrary` перенесены в Rendering.Skia. `VulkanUiTarget` принимает raw-handle `VulkanHostContext`; после разделения 2.1a Silk convenience target создаётся через `Hosting.SilkVulkanHost.CreateTarget(device)`. GPU обмен пока последовательный с ожиданием completion; контракта нескольких frames-in-flight ещё нет. [Примеры подключения и владение](../../TrueMoon.Alloy.Hosting/README.md).

Уточнение raw-handle подключения, 2026-10-04: host передаёт instance/physical device/device/graphics queue/family, запрошенную API version >=1.1, enabled extensions и указатели enabled features/features2/pNext вместе с собственным loader и проверяемым DeviceWaitIdle callback. Descriptor не владеет native handles; features memory/loader/callback targets живут до закрытия всех UI-сессий. Skia owns только свои context/surfaces/textures. Protected content, другие queues и frames-in-flight остаются за пределами последовательного режима. Первоначальные convenience overloads и Silk window presenter были сохранены в renderer; в 2.1a (2026-10-05) они заменены factories в Hosting, presenter/counters перенесены туда же, а dependency Rendering.Skia → Platform.Silk удалена. Это реализация предусмотренного внешнего Vulkan-контракта 1.5/2.2, цели и критерии alpha сохраняются.

Уточнение presentation, 2026-10-04: игровой хост композитит retained HUD со сценой на GPU, возвращает UI lease после sampling и передаёт готовую scene texture в `VulkanWindowPresenter.PresentImage`. Presenter заимствует источник и сообщает его фактический layout после завершённой GPU-работы; источник остаётся собственностью игры. `Present(UiSession)` сохраняется для standalone UI. Это реализация исходных целей 1.2–1.6/5.2–5.3; цели, архитектурные границы и критерии K1–K9 не меняются. [Оконный пример и автоматический сценарий](../../ManualTests/AlloyVulkanTest/README.md).

Уточнение retirement, 2026-10-04: window device согласует KHR/EXT swapchain_maintenance1 и feature с необходимыми instance dependencies. Presenter использует present fences для reuse и уничтожения swapchain generations. Legacy fallback сохраняет поколение до подтверждённой reacquisition показанного image нового swapchain; shutdown без extension сохраняет ограничение WaitIdle базового Vulkan. Это не основание объявлять WaitIdle доказательством завершения presentation. K7 проверяется отдельным --retirement-soak с ownership counters, registry/subscriptions и WeakReference; счётчики handles не заменяют VRAM/driver profiling. Цели и критерии alpha не меняются. [Алгоритм, ограничения и команды](../../ManualTests/AlloyVulkanTest/README.md).

Уточнение raster, 2026-10-04: `SkiaRasterRenderBackend`/`RasterUiTarget` используют общий UiSession и Skia drawing/text adapter на CPU без window/device. RGBA premultiplied snapshot экспортируется как caller-owned immutable SKImage, который живёт независимо от session/resize и не требует lease Return. Native Skia Windows x64 сохраняется; ABI Vulkan не требуется на raster-пути. Выбор — `UseAlloy(...UseSkiaRaster().UseExternalHost())` или direct Create. На момент raster шага standalone был Vulkan-only, comparison охватывал raster/Vulkan; после OpenGL шага standalone поддерживает оба GPU backend, а comparison — все три. Selected-pixel/layout/input проверки не заменяют полное font golden сравнение K2/K4.

Уточнение OpenGL, 2026-10-04: второй явно выбираемый GPU renderer — `SkiaOpenGLRenderBackend` с borrowed `OpenGLUiTarget` (host loader/current-context guard). Общие UiSession/Argentis/Skia drawing/text сохраняются; GPU retained frame композитится в host-owned RGBA8 FBO через PresentOpenGL. Host owns context/FBO/window, сериализует работу и восстанавливает своё GL state после Skia. Cross-context texture lease/async handoff не включаются в эту миграцию. `UseSkiaOpenGL` работает с external host и standalone Silk presentation; последний освобождает Skia в Closing до уничтожения GL context. Managed callback errors переходят в Completion/StopAsync после закрытия, без unwind через GLFW. Одинаковый view проверяется на raster/OpenGL/Vulkan; legacy no-options Visual API остаётся compatibility path без экспериментального drawing/CreateVulkan/empty catch. Это завершает миграцию пути 2.5, но не разделение зависимостей 2.1; цели и K1–K9 сохраняются. Предыдущее ограничение standalone Vulkan-only отменено.

## Последовательность реализации

<a id="актуальная-точка-продолжения--реализация-dpi-2026-10-09"></a>
### Историческая точка продолжения — реализация DPI 2026-10-09

Реализованы функциональные подэтапы3.5a/b/c/d,4.8,5.1a/b/c и общая Settings-сцена raster/OpenGL/Vulkan/HUD, контракт2.4/K8 с controlled failure/recreate, K9 baseline/profile/reuse и Windows appearance/custom chrome/native Snap4.9b/c/d. 2026-10-09 добавлен native Windows DPI contract: Scale, логические размеры и нормализованный ввод. Фактически выполненные текущие build/tests/GPU/publish и границы проверки находятся в STATUS. Реализация не означает итоговую готовность alpha.

Физический следующий шаг остаётся **4.9d / K3: 100/200% и переходы между разными DPI**, с сохранением logical client size, focus/selection и повтором chrome/input/alpha. K7 WSI investigation2026-10-10 разделила queries/create и локализовала GDI trend до driver-owned screen DC внутри первого vkCreateSwapchain на новой surface/window (NVIDIA ICD32.0.16.1742); тот же raw control на AMD сохраняет GDI. Safe app-side fix не установлен; нужен supported NVIDIA fix либо отдельно согласованная и проверенная alternate GPU configuration, затем полный240-window check с прежними limits. Ближайший независимый code deliverable — **5.4–5.5 package/independent UI consumer**. Fixed-font/full goldenK2/K4, длительныйK7/VRAM, real lossK8 и оставшаяся4.9matrix сохраняются перед итоговым alpha проходом. Directed DPI не заменяет physical transitions; Name mismatch и прежние opacity/legacy capture failures сохраняются как отдельные риски.

Этот маршрут актуализирует более ранние датированные формулировки «следующий шаг» ниже; цели, архитектура, net10.0/xUnit и критерии готовности сохраняются. Точная область прежних проверок и открытые ограничения — [STATUS.md](STATUS.md).

**Точка продолжения после K2/K4 — 2026-10-11:** explicit fonts/full golden проверены на текущем Windows x64/NVIDIA в Debug/published Release,360полных сравнений, validation0/0 и UI532/532. Ближайшая независимая общая задача — CORE5/COB4 package/analyzer consumer по планам владельцев. UI сохраняет physical4.9/K3, внешний K7, real loss/K8/VRAM и полный итоговый alpha проход. [Контракт сравнения](GOLDEN_TESTS.md).

**Предыдущая точка продолжения2026-10-11 (package шаг):** подготовка package/independent consumer5.4–5.5 проверена на Windows x64/net10.0/framework-dependent Debug/Release build/publish без RID. Исправлена native transitive delivery,16consumer процессов прошли, package README есть. Следующий независимый этап — fixed-font goldenK2/K4. Внешний NVIDIA K7 блокер, physical DPI/4.9matrix, real loss/VRAM и итоговая alpha acceptance сохраняются; публикации alpha не было. [Точные результаты и ограничения](HISTORY.md#package-и-независимый-ui-consumer--2026-10-11).

### Контракт Windows DPI 4.9d / K3 — уточнение 2026-10-09

По запросу начать следующий этап alpha реализовано уточнение координат существующего Silk host. Размеры UI, параметры создания/Resize и custom-frame MinimumSize/MaximumSize задаются в логических 96-DPI единицах. На Windows UiViewport.Width/Height — физические framebuffer pixels, Scale — GetDpiForWindow(HWND)/96 независимо от framebuffer/client ratio. NativeWindow.Size/Position и raw Win32 messages остаются физическими координатами. Silk mouse position и native caption input преобразуются в логические координаты ровно один раз; wheel delta не масштабируется. Hosting перед input синхронизирует изменённый viewport и layout/region map.

Логический client resize округляется до ближайшего физического пикселя (середина от нуля); native minimum округляется вверх, maximum вниз по текущему HWND DPI, в том числе до первой публикации regions. Custom chrome отвергает snapshot с устаревшими размером или Scale и сбрасывает его при WM_DPICHANGED. WM_GETDPISCALEDSIZE вычисляет новый physical client из прежнего logical client, для обычного окна добавляет рамку через AdjustWindowRectExForDpi. Предложенный Windows rectangle при WM_DPICHANGED применяет GLFW; второго SetWindowPos в DPI hook нет.

Окно создаётся в scoped PER_MONITOR_AWARE_V2 context потока; прежний context восстанавливается. Viewport/Resize и оконный loop используют такой же scope для callers с другим DPI awareness. Новый адаптер не меняет process-wide DPI settings приложения. DPI hook владеет только subclass/GC root и borrowed HWND, снимается до уничтожения окна; managed ошибки не выходят через native callback. HUD с внешним host сохраняет прежний контракт: host задаёт UiViewport/Scale и нормализует ввод сам. Public API, renderer ownership, native Skia ABI, пакеты и net10.0/xUnit не меняются.

Это исправляет Windows поведение на high DPI: клиент600×400 логических единиц при150% получает900×600 физических пикселей вместо прежнего physical600×400/Scale1. K3 по-прежнему требует реальные100/150/200% и переходы; арифметические/headless/directed проверки записываются отдельно от физической matrix.

Основания реализации: [GetDpiForWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdpiforwindow), [WM_DPICHANGED](https://learn.microsoft.com/en-us/windows/win32/hidpi/wm-dpichanged), [thread DPI context](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setthreaddpiawarenesscontext), [GLFW Windows DPI handling](https://github.com/glfw/glfw/blob/3.4/src/win32_window.c).


### Контракт отказа графики 2.4/K8 — уточнение 2026-10-07

`UiRenderingException` обозначает подтверждённый terminal failure с backend/operation, original cause и причиной DeviceLost, ContextLost либо BackendFailure. Обычные ошибки DrawCore сохраняют dirty state и допускают повторный Update; они не объявляются потерей устройства. Vulkan ErrorDeviceLost классифицируется явно, abandoned Skia context сообщает ContextLost. Для внешнего presentation/engine host сообщает подтверждённый отказ через UiSession.ReportRenderingFailure.

Первый отказ сохраняется в RenderingFailure, уведомляет RenderingFailed один раз на owner thread, отменяет focus/capture/hover и pending posts. Update/Resize/input/Post/theme/export/presentation больше не допускаются. Повторный report не заменяет первый cause. VerifyAccess и VerifyAvailable проверяют lifetime/thread/lease, допускают cleanup faulted context; проверка GPU health выполняется отдельно. Dispose из notification/Update и с borrowed frame отвергается до разрушения дерева. Ошибки cancellation/observers/cleanup сохраняются вместе с исходным отказом, остальные безопасные cleanup действия выполняются. Cleanup errors могут образовывать вложенные AggregateException.

Host владеет model/device/window/queues, UI — своим деревом, подписками и Skia wrappers. Model сохраняется независимо от Dispose; после освобождения старой сессии создаются новые controls/session из той же модели. Transient focus/capture/selection/scroll автоматически не переносятся. При настоящей потере устройства host сначала освобождает старый UI, затем сам пересоздаёт device/context/target. Автоматическое бесшовное восстановление не включается.

Vulkan lease обычно возвращается после завершённой host GPU работы с actual layout. Только подтверждённый DeviceLost разрешает AbandonAfterDeviceLoss без GPU ожидания и state-return; Skia context abandon выполняется без backend calls. Borrowed Vulkan handles и loader остаются живы до окончания cleanup даже после abandon. При неизвестном отказе completion нельзя разрушать потенциально in-flight host ресурсы: standalone сохраняет device, если UI/presenter cleanup не завершился. Best-effort cleanup не является обещанием полного освобождения driver allocations при неизвестном состоянии графики. [Подробный контракт и пример](../../TrueMoon.Alloy.Hosting/README.md#отказ-графики-и-явное-пересоздание-24k8).

Воспроизводимый `--settings-failure --validation` проверяет controlled BackendFailure на реальных raster/OpenGL/Vulkan surfaces, borrowed-frame guard/normal Return, renderer result classification, hosting draw/external-report/creation failures и explicit recreation с прежней SettingsModel. Контролируемые сценарии учитываются отдельно от физического device/context loss; последний этим probe не индуцируется. Критерии K1–K9 и требования итогового alpha прохода сохраняются.

### Согласованная корректировка порядка работ — 2026-10-06

После оценки текущей реализации пользователь согласовал уточнение плана. Архитектура модулей, retained дерево, net10.0/xUnit, два сценария запуска и критерии K1–K9 сохраняются. Крупные пункты разделяются на проверяемые подэтапы; графический прототип и функциональные механизмы учитываются отдельно от итоговой готовности alpha. Исходные номера пунктов сохраняются для ссылок и исторических записей. Это изменение плана, не реализация перечисленных возможностей.

Ближайший результат — одно небольшое AlloyTest-приложение с редактируемой моделью, динамическим списком и сохранением состояния неизменённых элементов при изменении дерева. Пример развивается вместе с API и затем используется для проверки общего runtime и графических адаптеров.

| Порядок | Пункты плана | Проверяемый результат |
| --- | --- | --- |
| 1 | 3.5b / 3.6 | Общие двусторонние привязки: TextBox.Value, CheckBox.IsChecked и Slider.Value; пользовательское редактирование, нормализация модели, отсутствие feedback и корректный attachment lifetime. |
| 2 | 4.8 / 5.1a | Фабрики рядом с публичными конструкторами и первая форма AlloyTest с моделью; примеры обоих способов создания контролов. |
| 3 | 3.5c / 3.5d / 5.1b | Наблюдаемый список и условные области загрузка/ошибка/содержимое; корректное владение generated nodes и сохранение неизменённых экземпляров. |
| 4 | 4.1–4.7 / 5.1c / 5.2–5.3 | Полные взаимодействия заявленных контролов, прокрутка, темы, пользовательский виджет и динамическая демонстрация в standalone/HUD. |
| 5 | 2.4 / K8 | Передача ошибок хосту, безопасное закрытие и создание новой сессии после графической ошибки. |
| 6 | K1–K9 / 5.4–5.5 | Воспроизводимый итоговый проход на заявленной Windows-конфигурации, measurements baseline, документация и проверка поставки отдельному consumer. |

При завершении каждого подэтапа STATUS должен указывать реализацию, конкретные прошедшие проверки, их дату и оставшийся объём. Завершённый подэтап не объявляет завершённым весь родительский пункт или alpha.

### Этап 1. Проверить графический стек и встраивание

Создать минимальный Vulkan-хост: простая сцена, текст и полупрозрачная панель Skia поверх неё.

- **1.1. Совместимость пакетов.** Проверить совместимость зафиксированных версий Silk.NET, SkiaSharp и нативных библиотек.
- **1.2. Жизненный цикл графики.** Реализовать создание GPU-контекста, поверхности, вывод кадра, изменение размера и освобождение ресурсов.
- **1.3. Графический пример.** Показать сцену, текст и полупрозрачную панель Skia в минимальном Vulkan-хосте.
- **1.4. UI-текстура HUD.** Использовать прозрачную GPU-текстуру с premultiplied alpha; хост накладывает её на сцену без копирования через CPU.
- **1.5. Владение ресурсами.** Хост владеет устройством, очередью и swapchain; Alloy владеет собственным Skia-контекстом и UI-текстурами.
- **1.6. Синхронизация.** Для первой версии использовать одно устройство и согласованное использование графической очереди. Явно передавать зависимости GPU-работы и сигнал завершения использования текстуры хостом. Проверить синхронизацию и переходы состояния изображений при обмене ресурсами со Skia.

Результат этапа — работающий пример и зафиксированный контракт взаимодействия. Если возможности пакета ограничивают интеграцию, оформить конкретное техническое препятствие до разработки основного рендерера.

### Этап 2. Разделить прототип на независимые слои

- **2.1. Сборки и зависимости.** Вынести зависимости Skia и Silk из общего API; переместить интеграцию с TrueMoon в hosting-сборку.
- **2.2. Контракты запуска.** Заменить привязанный к Silk.NET `IGraphicsPlatform` отдельными контрактами окна и рендерера. Реализовать `UiSession`, оконный и встраиваемый сценарии; сохранить `UsePresentation<TView>()` с явным выбором бекендов.
- **2.3. Дерево и время жизни.** Исправить хранение и обход дочерних элементов, родительские связи и освобождение подписок.
- **2.4. Диагностика.** Убрать экспериментальный вывод из основного пути рендеринга; обеспечить передачу ошибок в диагностику.
- **2.5. Другие графические пути.** Перенести существующий OpenGL-прототип на новые контракты как второй явно выбираемый GPU-путь. Raster использовать для проверок без окна.

Результат — одно представление запускается с разными графическими адаптерами без изменения кода контролов.

### Ближайшие подэтапы 2.1: разделение зависимостей

Сверка исходников 2026-10-04 показала: графический путь 2.5 уже был реализован, однако общий Alloy ещё содержал legacy presentation, а Rendering.Skia ссылался на Platform.Silk. Переносить связку renderer/window в Hosting: этот модуль уже зависит от обоих адаптеров и организует standalone запуск. Перенос 2.1a выполнен и проверен 2026-10-05: presenter/counters и `SilkVulkanHost` находятся в Hosting, renderer принимает только raw descriptor и больше не ссылается на Platform.Silk. Перенос 2.1b также выполнен и проверен 2026-10-05: 31 legacy-файл вынесен в Hosting.Compatibility, Alloy оставлен только с Runtime/Argentis, AlloyTest использует explicit Hosting API. Требуемые зависимости compatibility явно подключены в Hosting; транзитивные legacy dependencies через Alloy удалены. Фактические проверки и ограничения записаны в STATUS. Пять основных модулей, net10.0, xUnit и критерии K1–K9 сохраняются.

Целевые направления ProjectReference:

| Модуль | Зависимости UI после разделения |
| --- | --- |
| Argentis | Без зависимости от других UI-модулей, Skia и Silk |
| Alloy | Argentis; runtime и независимые контракты |
| Rendering.Skia | Alloy/Argentis, SkiaSharp; Vulkan bindings допустимы внутри Vulkan-интеграции, без Platform.Silk и оконного API |
| Platform.Silk | Alloy/Argentis, Silk; без Rendering.Skia и SkiaSharp |
| Hosting | Alloy, Rendering.Skia, Platform.Silk, TrueMoon lifecycle/DI contracts; интеграционные adapters и standalone presentation |

**2.1a — выполнен и проверен 2026-10-05: убрать Rendering.Skia → Platform.Silk.**

1. Перенести VulkanWindowPresenter и его presentation resource counters в Hosting; сохранить алгоритм retirement, Present/PresentImage, ownership и синхронизацию.
2. В Hosting вынести Silk→VulkanHostContext adapter. Заменить renderer API `FromSilkDevice`, `VulkanUiTarget(VulkanDevice)`/`Device` и `VulkanInteropSurface(VulkanDevice, ...)` на raw descriptor плюс явные convenience factories в Hosting. Raw host descriptor, enabled capabilities, lifetime callbacks и native ABI сохраняются.
3. Обновить HostedUiWindow, GPU manual probes и README на новые namespace/factories; удалить ProjectReference renderer→platform. Vulkan types, нужные renderer для lease/layout, оставить backend-specific package dependency; не переносить их в общий Alloy.
4. Проверить прямой raw/raster/OpenGL renderer без Hosting/Platform: исходники, ProjectReference и фактические сборочные зависимости. Наличие Silk.NET.Vulkan package внутри Vulkan renderer не равнозначно зависимости от оконного Platform.Silk. До 2.1b отдельно учитывать transitive Skia/Silk/TrueMoon зависимости через legacy Alloy: удаление renderer→platform не доказывает полного очищения всего графа.

Готовность 2.1a: нет циклов между сборками и ссылки renderer→Platform.Silk; публичный raw-host путь работает без Silk window/device wrapper; общие3backend/input/layout, external App и обе standalone ветки сохранены. Проверки после переноса: сборка решения и UI xUnit; raw-host, raw HUD, backend comparison, OpenGL external/window/error, Vulkan window, короткий retirement smoke в maintenance1/legacy режимах; publish и запуск опубликованного примера. Новые результаты записать в STATUS, прошлые логи не засчитывать автоматически. Это завершает только 2.1a, Alloy в этот момент ещё может содержать legacy зависимости.

**2.1b — выполнен и проверен 2026-10-05: оставить общий Alloy независимым.**

1. Вынести старую связку VisualTree/IGraphicsPlatform/ViewHandle/ViewManager, presenters и no-options UsePresentation из Alloy в явно выделенную compatibility область Hosting. Она переходная, не часть нового runtime. Не возвращать зависимости Hosting/Skia/Silk в Alloy ради совместимости.
2. Перевести найденный consumer `ManualTests/AlloyTest` на Hosting.UsePresentation с явным backend/host; View1/View2 уже наследуют Argentis.View/Element. Проверить оставшиеся callers, imports и сборочную принадлежность типов. Сохранение namespace само по себе не обеспечивает совместимость уже собранных приложений; миграция alpha API допускается планом и документируется с обновлением примеров.
3. Удалить из Alloy PackageReference Silk.NET/SkiaSharp/Topten.RichTextKit и ProjectReference Core/Contracts после переноса использующего их кода; проверить фактическую assembly dependency. Выделение compatibility области не означает исправление старого Visual.GetEnumerator: его проверка/вывод из использования учитывается отдельно в 2.3.

Готовность 2.1b: Alloy содержит runtime/независимые контракты; обычный UiSession не требует App, Skia или Silk. 05.10.2026 legacy API перенесён в `TrueMoon.Alloy.Hosting.Compatibility` без type forwarders. Проверки assembly references и отдельные core-only/renderer-only consumers подтверждали границы; compatibility consumer тогда проверял только регистрацию. С `1.2611.25.13-alpha` legacy API [удалён](#uia1j-remove-hosting-compatibility). Завершение 2.1 не означает завершения 2.3/2.4 или alpha.

После разделения: ownership Remove/SetContent/transfer (3.4/2.3) уточнён и проверен 2026-10-05; one-way3.5a и BindTwoWay3.5b реализованы и проверены 2026-10-06 в своих контрактных границах. Фабрики4.8, первая форма AlloyTest5.1a, коллекции3.5c и условные области3.5d/демонстрация5.1b реализованы и проверены 2026-10-07; window lifetime defect исправлен и повторно проверен в ограниченном Debug/published Release наборе. Следующий шаг — полные взаимодействия5.1c в той же сцене. Итоговые K1–K9 идут по согласованному порядку; реализация отдельных подэтапов не завершает весь3.6 или alpha. Дополнительные GPU/EXT-only/VRAM исследования вести по выявленным рискам; async frames-in-flight остаются за пределами текущего последовательного режима.

### Уточнение ownership и failure semantics — 2026-10-05

Это уточнение lifetime постоянного дерева для 2.3/3.4 перед bindings. Модули, net10.0/xUnit и критерии K1–K9 сохраняются.

- Контейнер владеет текущими детьми; UiSession владеет корнем и surface после успешного создания. Remove/Clear/замена элемента коллекции/SetContent отсоединяют старое поддерево без Dispose и возвращают владение вызывающему коду. Оно обязано либо повторно добавить его, либо Dispose.
- Перенос — явные Remove и Add/SetContent. Между вызовами поддерево принадлежит вызывающему коду. После detach очищаются focus/hover/capture/pressed state и ссылки прежней сессии; повторное подключение не восстанавливает ввод. Сохранённые данные, selection/scroll и экземпляры остаются. Move внутри одной коллекции не отсоединяет элемент и сохраняет ввод/подписки; замена соседней ветви сохраняет состояние неизменённых контролов.
- `Own` освобождает ресурс при Dispose и сохраняет его при Remove/переносе. Новый `OwnAttachment` требует live attached element, освобождает ресурс при текущем detach и не переносит subscription в новую attachment. Повторное подключение может создать новую подписку через AttachmentChanged. Это lifetime foundation, не реализация bindings.
- Прямые Detach/Dispose принадлежащего контейнеру ребёнка отклоняются до изменения. Сначала Remove/SetContent(null). Dispose контейнера автоматически освобождает оставшихся детей, в том числе virtual cleanup контролов. Корнем управляет UiSession; вручную отсоединять или передавать корень живой сессии нельзя.
- Invalid owner/cycle/disposed subtree/index/thread проверки выполняются до изменения дерева. Такие ошибки сохраняют прежнее владение и ввод. Внешние уведомления выполняются после согласованного изменения всех parent/attachment связей. Ошибка observer/cleanup не откатывает уже видимый commit: продолжаются остальные notifications/cleanup, затем исходная одиночная ошибка или AggregateException передаётся вызывающему коду. После такой ошибки Add/SetContent может уже передать владение контейнеру; автоматически повторять изменение нельзя.
- Структурные изменения и Dispose сессии внутри tree/lifetime notifications отклоняются; отложенную работу следует отправлять через UiSession.Post. Queued Element.Dispatch привязан к текущей attachment и пропускается после удаления/переноса. Прямой UiSession.Post остаётся явно управляемой работой вызывающего кода.
- Ошибка attachment при создании UiSession откатывает подключение всех узлов и attachment subscriptions; корень остаётся вызывающему коду. При Dispose ошибки отдельной подписки не прерывают освобождение siblings, surface и уведомление registry; повторный Dispose не повторяет cleanup.

Реализация и проверки описаны в STATUS и [Hosting README](../../TrueMoon.Alloy.Hosting/README.md#владение-динамическим-деревом). Это закрывает policy текущего retained дерева; legacy Visual.GetEnumerator и остальные требования 2.3/2.4 остаются отдельно.

### Этап 3. Реализовать дерево, layout и динамический контент

- **3.1. Свойства.** Ввести типизированные свойства с уведомлениями и признаками влияния на layout и отрисовку.
- **3.2. Layout.** Реализовать Measure/Arrange, логические координаты независимо от DPI, размеры, ограничения, margin, padding и выравнивание.
- **3.3. Контейнеры.** Добавить вертикальный и горизонтальный контейнеры, наложение элементов, clipping и прокрутку.
- **3.4. Динамическое дерево.** Поддержать замену содержимого контейнера и изменения коллекций. Неизменённые экземпляры сохраняют фокус и состояние; удалённые освобождают подписки и захват ввода.
- **3.5. Привязки.** Добавить явные типизированные привязки к `INotifyPropertyChanged` и наблюдаемым коллекциям, включая двустороннее обновление редактируемых значений.
- **3.6. Потоки.** Выполнять изменения UI последовательно на потоке-владельце. Фоновые обновления поступают через диспетчер; в HUD поток и последовательность вызовов задаёт хост.

Результат — постоянное дерево с корректным layout и точечными обновлениями, сохраняющими состояние оставшихся элементов.

### Проверяемые подэтапы привязок и динамической разметки

- **3.5a. Односторонние свойства.** Bind/BindText, direct selector, initial sync, owner-thread Dispatch и отключение/reconnect при detach/reattach. Контракт ниже; фактическая готовность отдельно в STATUS.
- **3.5b. Двустороннее редактирование.** Общий typed read/write механизм для TextBox.Value, CheckBox.IsChecked и Slider.Value. Для первой версии запись в модель выполняется при каждом изменении редактируемого значения; режим commit по потере фокуса/команде откладывается. UI setter и source getter/setter работают в согласованном owner-thread контексте; фоновые source events проходят через dispatcher.
- **3.5c. Наблюдаемые коллекции.** Initial population и INotifyCollectionChanged Add/Remove/Replace/Move/Reset. Добавление/удаление/перестановка сохраняют экземпляры и состояние незатронутых элементов; Move не отсоединяет nodes. До реализации определить соответствие model item → generated node, обработку повторяющихся items и Reset. Явно описать, какие generated nodes освобождает binding, а какие передаются caller; исходные Remove/SetContent ownership правила сохраняются.
- **3.5d. Условные области.** В той же демонстрации показать загрузку/ошибку/содержимое через ContentControl.SetContent. Неизменённые соседние ветви сохраняют состояние. Новую ветвь создавать и проверять до замены; старую переиспользовать либо Dispose по существующим правилам. BindContent — возможный следующий helper по выявленной потребности демонстрации, не обязательная новая API alpha и не автоматическое наблюдение за произвольными factory lambdas.

До реализации 3.5b зафиксировать публичную typed API и полный контракт: equality/feedback/reentrancy guard, чтение канонического значения после setter normalization, отображение отказа setter и состояние контрола после отказа. Ошибка не должна молча теряться или нарушать native callback boundary; нельзя считать её доказательством отката уже committed изменений. Обе стороны обязаны соблюдать инварианты контрола, включая selection/caret TextBox. Новое корректное изменение после ошибки должно допускать восстановление. Проверки: реальный ввод, переключение и drag/keys; отсутствие обратной записи при source → UI; normalization/equality/validation failure; detach/reattach/Move/transfer; stale queued/in-flight события и cleanup.

Для 3.5c отдельно проверить empty/single/multiple items, все пять действий коллекции, source changes во время detach, reconnect без повторных subscriptions, фоновые события и ошибки node factory/notifications. Неуспешное создание нового subtree должно иметь определённое владение и cleanup; ошибка notification после commit не повод автоматически повторять mutation. Для 3.5d проверить ошибку создания новой ветви, освобождение заменённой ветви и сохранение focus/selection/scroll соседей.

### Контракт коллекционных привязок 3.5c — согласован 2026-10-07 после 93a5cae

Пользователь разрешил реализацию предложенного контракта. Цели retained дерева, ownership обычных Remove/SetContent, границы модулей, net10.0/xUnit и K1–K9 сохраняются. Первая версия намеренно использует уникальные reference items; keys и повторяющиеся occurrences отложены.

- Публичная API Argentis: `container.BindItems(source, factory)` для ObservableCollection<TItem>/ReadOnlyObservableCollection<TItem>, `TItem : class`, factory возвращает Element; конкретный тип контейнера сохраняется. Источник фиксирован до Dispose, автоматическое наблюдение за заменой model property не добавляется. Один binding на первоначально пустой Items; после регистрации структура Items управляется исключительно binding, даже detached. Статические controls располагаются в соседних контейнерах. `RefreshItems()` запрашивает повторную синхронизацию; Unbind не вводится.
- Сопоставление только ReferenceEquals: null и повтор одной ссылки отклоняются до изменения дерева; равные по Equals разные объекты допустимы. Свойства item обслуживают существующие property bindings. Reset/reattach/Refresh сохраняют nodes для оставшихся объектов, переставляют без detach, создают новые и освобождают отсутствующие. Новый объект с прежним Id создаёт новую строку. Add/Remove/Replace/Move/Reset, включая range events, запрашивают сверку актуального snapshot; промежуточные состояния могут coalesce. Add+Remove до Update может не вызвать factory, Remove+Add той же ссылки — сохранить node. Replace той же ссылкой — no-op.
- Source и items принадлежат вызывающему коду. Factory возвращает новый live unowned/unattached subtree; disposed/owned/repeated nodes и ancestors/cycles отклоняются. До commit принятые drafts принадлежат binding, после — контейнеру. При удалении binding получает ownership отсоединённой строки и обязан Dispose. Detach всего контейнера сохраняет строки и отключает subscription; Dispose контейнера освобождает детей обычным путём без повторного cleanup binding. Factory отвечает за ресурсы, выделенные до throw и не возвращённые binding; чужой owned node binding не освобождает.
- Snapshot проверяется, все новые строки полностью создаются и проверяются до batch commit. Один внутренний TreeChange фиксирует конечный порядок детей, parent/attachment связи и mapping до notifications. Сохранённые строки не отсоединяются: focus/selection/subscriptions сохраняются, scroll offset ограничивается обычным layout clamp. Ошибка snapshot/factory/validation сохраняет прежнее дерево, cleanup drafts продолжается после отдельных ошибок. Source mutation из factory отклоняет подготовку по version guard; исходная модель не откатывается. Source changes из observers ставят следующую синхронизацию в очередь, без вложенного commit.
- Notification/attachment callback/cleanup failure после commit не откатывает дерево и mapping. Остальные observers и cleanup удалённых строк выполняются best-effort; одна ошибка сохраняет исходный тип, несколько передаются AggregateException. Повторное применение committed mutation не допускается. Следующий корректный event/RefreshItems допускает recovery.
- Registration живёт через Own, connection через OwnAttachment. До attach source не читается и factory не вызывается; attach/Bind уже attached контейнера подписывает source и ставит initial sync в очередь следующего Update до layout. Структурные mutations внутри AttachmentChanged по-прежнему запрещены. Detach/Dispose отключают subscription; captured attachment dispatcher и active guard отбрасывают старые queued/in-flight callbacks после reattach/transfer.
- Background notifications только запрашивают sync; snapshot/factory/tree mutations выполняются на owner thread. Владелец source обязан сериализовать changes/notifications и исключить конкурентное перечисление; ObservableCollection не становится потокобезопасной. AlloyTest меняет коллекцию на UI-потоке; фоновые результаты доставляются через dispatcher. Factory failure в initial Update оставляет direct session живой с прежним списком для recovery; standalone HostedUiWindow использует существующий callback boundary и передаёт ошибку через Completion/StopAsync после закрытия. Inline error rows — отдельный 3.5d через SetContent.

Проверки: initial empty/single/multiple, все пять actions/ranges, identity/duplicates/null/coalescing, retained input/selection/scroll/subscriptions, factory drafts/validation/errors, coherent postcommit notifications/cleanup/recovery, detach/reattach/transfer/background/stale events и эксклюзивность Items. Затем динамический редактируемый список в существующей SettingsModel/View1, sequential Debug/published Release window smoke. Фактические результаты реализации и пределы проверок — в STATUS; контракт не объявляет завершёнными 3.5d, 5.1b целиком или alpha.

### Реализация условных областей 3.5d / завершение 5.1b — 2026-10-07

В AlloyTest отдельная область переключается между загрузкой, ошибкой и готовым содержимым через существующий ContentControl.SetContent; форма и наблюдаемый список остаются соседними retained ветвями. Состояния демонстрации контролируются явными действиями модели, без реального сетевого запроса или обещания async loading/cancellation. Ошибка содержит действие «Повторить», возвращающее состояние загрузки. BindContent и новые library API/dependencies не вводятся.

Sample-specific LoadStatusRegion использует обычную typed property binding immutable LoadStatus snapshot (phase/message). Property hook ставит замену в очередь owner thread, включая initial attach; attachment generations отбрасывают stale work, reattach повторно сверяет desired/displayed состояние. Одинаковый snapshot сохраняет текущую ветвь без redraw/rebuild. Новая ветвь полностью строится до SetContent validation/commit. При factory/validation failure прежний child остаётся; принятый unowned draft очищается, foreign owned node/ancestor не освобождается. Factory отвечает за ресурсы, выделенные до throw без возврата.

После commit, в том числе когда SetContent бросил notification error, принятая ветвь остаётся собственностью контейнера, старая освобождается caller-кодом виджета. Cleanup error не останавливает оставшийся cleanup; single error сохраняет тип, несколько передаются AggregateException. Desired model/property snapshot не откатывается при ошибке подготовки; displayed child/status остаются прежними до успешного event/RefreshContent. Postcommit ошибка не запускает повторную замену автоматически. Общие Remove/SetContent ownership правила не меняются.

Проверки используют точные исходники sample widget через Compile links в существующем xUnit проекте, без ProjectReference на manual executable/App/Silk. Проверять все три ветви/messages, фабрику и validation before commit, old/draft cleanup и notification failures, equality/coalescing, owner-thread/background/attachment lifetime, input cleanup заменённой ветви и focus/selection/scroll соседей. Реальный View1/window smoke дополняет headless проверки transitions/retry/Dispose, отдельно от fonts/OS input/long soak/alpha. Результаты текущего запуска — в STATUS.

Уточнение 27.11.2026 (UIA4.5): виджет `LoadStatusRegion` из примера удалён, область состояний AlloyTest
собрана общим `TrueMoon.Argentis.Layout.ConditionalContent<LoadStatus>` (`ManualTests/AlloyTest/View1.cs:15,52-59`),
проверки перенесены в `TrueMoon.Alloy.Tests/ConditionalContentTests.cs`. Правила выше — подготовить ветвь
до замены, сохранить прежнюю при ошибке фабрики, очистить принятый draft, `AggregateException` на
несколько ошибок и неизменный snapshot без пересборки — теперь принадлежат движковому слоту, а не примеру:
[контракт и стоимость](PERFORMANCE_BASELINE.md#uia45-conditional-slot-2026-11-27).

### Контракт двусторонних property bindings 3.5b — 2026-10-06

Для реализации принят `element.BindTwoWay(uiProperty, source, x => x.Property)`, сохраняющий конкретный тип Fluent-цепочки. Выбирается прямое readable/writable public instance property точно того же TValue; computed/nested/field/conversion/read-only/private-set/init-only selectors отклоняются до подключения. One-way Bind сохраняет свой контракт, включая стандартное приведение типа. На одно UiProperty остаётся одна registration любого направления до Dispose.

- Начальное значение и reattach берутся из source, без обратной записи. Пока элемент detached, обе стороны отключены. UI-изменение attached свойства записывает значение немедленно на owner thread, только если оно отличается от source. Локальное программное изменение также считается UI-изменением; source → UI и изменения во время синхронизации не вызывают повторный setter.
- После setter значение source перечитывается и применяется к контролу, включая normalization и setter без PropertyChanged. Same-thread echo выбранного свойства во время source setter подавляется; остальные source events, в том числе фоновые и изменения модели из UI observers при source → UI sync, остаются queued. Reentrant UI property events не запускают вложенный setter; по завершении записи применяется актуальное canonical source value. Это не транзакция модели.
- При setter/getter ошибке write-back пытается перечитать и применить актуальное source value, затем передаёт ошибку из Set/HandleInput. Если source уже частично изменён setter, применяется его фактическое состояние, а не предполагаемый rollback. Если readback/validation/observer cleanup также неудачны, одиночная ошибка сохраняет тип, несколько передаются AggregateException; последнее committed UI значение сохраняется там, где восстановление невозможно. Новое корректное изменение допускает recovery.
- Property change после commit выполняет invariant hook контрола, invalidation и всех подписчиков, даже когда отдельный observer бросает исключение; ошибки передаются после попыток уведомления. TextBox selection/caret остаются в пределах canonical текста и на границах графем при generic Set и normalized editing. Failed slider drag освобождает capture. Native callback boundary использует существующую передачу ошибки хосту; direct UiSession caller обрабатывает исключение.
- Registration/connection используют прежние Own/OwnAttachment, active guard и attachment-scoped Dispatch. Все source/UI subscriptions удаляются при detach/Dispose; stale queued/in-flight callbacks не читают source и не пишут в новую attachment. Потокобезопасность модели остаётся обязанностью её владельца.

Контракт реализован и проверен в подэтапе 2026-10-06: общий binding, property notification/invariant hooks, TextBox grapheme selection/caret и failed Slider capture cleanup. Конкретные команды, результаты и ограничения отдельно в STATUS; этот результат не заменяет полные control/window/alpha проверки.

### Уточнение односторонних property bindings — 2026-10-06

Первый подэтап 3.5/3.6 реализуется в Argentis без зависимостей от App, renderer или платформы. Общая цель привязок, net10.0/xUnit и критерии K1–K9 сохраняются; двусторонние и collection bindings остаются отдельными следующими подэтапами.

- Публичная Fluent API: `element.Bind(uiProperty, source, x => x.Property)`; для Text — `text.BindText(source, x => x.Name)`. Возвращается тот же конкретный тип контрола. Source — reference type с INotifyPropertyChanged; selector выбирает прямое instance property, включая стандартное приведение типа. Nested paths, поля и вычисляемые выражения явно отклоняются, автоматического наблюдения за ними нет.
- На одно UiProperty допускается одна binding registration до Dispose. Повторный Bind отклоняется без замены прежней привязки. Для этого подэтапа Unbind/замена регистрации не вводятся. Разные свойства можно привязывать независимо; DataContext не подключается автоматически.
- Регистрация живёт через Own до Dispose; connection живёт через OwnAttachment. До attach модель не читается и source subscription отсутствует. Attach/reattach синхронно считывает актуальное значение на UI-потоке и создаёт одну подписку. Detach отключает subscription, не меняя последнего значения; Move сохраняет connection.
- PropertyChanged выбранного свойства, null или пустое имя отправляет чтение через Element.Dispatch в следующий host Update, в том числе при уведомлении с UI-потока. Читается последнее состояние модели, не snapshot на момент события; getter должен быть безопасен на потоке-владельце. Прежние queued/in-flight notifications после detach/reattach/transfer не читают модель и не изменяют элемент.
- Направление только source → UI. Локальное изменение контрола не записывается в source. Validation и invalidation выполняются обычным Element.Set; одинаковое значение не вызывает redraw/property event.
- Ошибка первичной регистрации на attached element отключает её subscription и освобождает слот для повторного Bind. При неуспешном UiSession attach действуют существующие rollback/ownership правила: регистрация сохраняется для следующего attach. Ошибка getter/validation при queued update передаётся из Update; новое корректное уведомление позволяет повторить синхронизацию. Уже committed property/tree changes не откатываются из-за ошибки внешнего observer.
- Критерии этого подэтапа: initial attach, выбор уведомлений, layout invalidation, отсутствие обратной записи, повторные detach/reattach и Move, разные UI-потоки, stale queued/in-flight events, failure/retry и отсутствие повторных subscriptions. Фактические результаты и границы проверок — в STATUS.

### Этап 4. Добавить ввод, контролы и оформление

Уточнение реализации 5.1c /4.1–4.7, 2026-10-07: typed `UiProperties.Padding` и `Spacing` участвуют в local → style → theme → default. Тема содержит валидируемые immutable `ButtonPadding`, `EditorPadding`, `StackSpacing`; исходные constructor defaults сохраняются (button12/8, editor8, stack0). CheckBox добавляет24px слева для индикатора. Произвольные контейнеры по умолчанию имеют padding0; theme button/editor tokens применяются только к соответствующим controls. Width/Height/local padding/spacing и типографика продолжают перекрывать тему. Palette/density меняются в существующем дереве AlloyTest, без повторного создания формы/строк.

Для scrolling один `Element.ChildClipBounds` задаёт child viewport одновременно drawing/hit testing; ScrollViewer исключает padding из него. Wheel на границе не потребляется и может всплыть во внешний ScrollViewer. `BringIntoView(Rect)` раскрывает arranged root-coordinate bounds с clamp, oversized content выравнивается сверху; UiSession.Focus (включая Tab/Shift+Tab) применяет его от внутреннего viewport к внешнему с поправкой на фактическую смену Offset. Это не горизонтальная прокрутка и не виртуализация. Secondary pointer down/up не меняет primary Slider gesture или focus; FocusLost/disabled/detach сохраняют existing input cancellation policy.

AlloyTest показывает все заявленные control types, PNG32×32 через borrowed Image source, owned decoded source освобождается View1 после детей. Asset поставляется build/publish через PreserveNewest. VolumeMeter — sample-specific widget с собственным IDrawingContext drawing и существующим normalized ProgressBar property. Новых backend/native ABI/packages не вводится. Headless tests Compile-link точные demo sources, raster проверяет image/custom drawing и clipping; native OpenGL smoke дополняет keyboard/pointer/theme/density/scroll/disposal. OS clipboard и full shared raster/OpenGL/Vulkan/HUD/font/DPI/soak результаты учитываются отдельно в STATUS; этот функциональный этап не подменяет итоговый alpha проход.

- **4.1. Контролы первой поставки.** `Text`, `Image`, `Button`, `CheckBox`, однострочный `TextBox`, `Slider`, `ProgressBar`, `Border`, контейнеры и `ScrollViewer`.
- **4.2. Маршрутизация ввода.** Реализовать hit testing, порядок наложения, pointer capture, фокус, Tab-навигацию и активацию с клавиатуры.
- **4.3. Редактирование текста.** Разделить клавиши и текстовый ввод; поддержать латиницу, кириллицу, выделение и clipboard в `TextBox`. Согласованное уточнение 2026-10-07: Windows Get/Set используют единый Win32 путь с borrowed HWND. Get возвращает null только при отсутствии текстового формата; borrowed HGLOBAL не освобождается, lock/clipboard закрываются в finally. Set готовит UTF-16 GMEM_MOVEABLE до Open, владеет памятью до успешного SetClipboardData, затем передаёт её Windows; при отказе до передачи освобождает draft. Close failure после публикации не возвращает владение. Set заменяет содержимое clipboard текстом, откат прежних форматов после EmptyClipboard не обещается. Open допускает десять retry waits по10ms только при error5, operation/cleanup errors сохраняются. Operational errors оборачиваются в UiClipboardException с Read/Write и original cause; UiSession.HandleInput сообщает ClipboardFailed и сохраняет сессию. Прямой Get/Set, programming errors и ошибки notification handlers продолжают бросать исключения. TextBox изменяет value при Cut только после успешной записи. Clipboard доступен на owner thread до Dispose; null/embedded NUL отвергаются до native записи. GLFW error callback не меняется; другие платформы сохраняют Silk путь. Native copy/cut/paste smoke запускается только при точном заменяемом fixture `Alloy clipboard smoke fixture v1`; сохраняет/восстанавливает текст, не произвольные форматы. Controlled clipboard smoke проверяет окно/редактор отдельно от обмена с ОС. Фактические результаты и блокеры — в STATUS. Новых packages или изменения renderer ABI нет.
- **4.4. Совместная работа HUD и игры.** Возвращать хосту результат обработки события и состояние захвата мыши/клавиатуры.
- **4.5. Тема и состояния.** Добавить тему с цветами, типографикой и отступами, а также состояния hover, pressed, focused и disabled.
- **4.6. Приоритеты значений.** С уточнением UIA1 от2026-10-22: local/binding → direct Style элемента → scoped стиль View → Theme → значение по умолчанию. Снятие слоя возвращает значение следующего уровня без перестройки дерева.
- **4.7. Пользовательские виджеты.** Поддержать создание виджета композицией элементов либо собственным рисованием через `IDrawingContext`.
- **4.8. Статические фабрики контролов.** Добавить фабрики на типах контролов Argentis для удобной Fluent-разметки, сохранив публичные конструкторы и возможность смешивать оба способа создания. Начальные согласованные имена — `Button.Simple(string text = "")` и `Text.Plain(string value = "")`. Фабрика обычного контрола создаёт экземпляр с теми же значениями по умолчанию и текущим оформлением темы, что соответствующий конструктор; `Plain` обозначает обычный текст без обработки разметки. Для остальных контролов подобрать понятные имена при их реализации; если отдельного варианта нет, использовать `Create(...)` (например, `TextBox.Create()`, `VStack.Create()`). Имена дополнительных вариантов связывать с явно определённым внешним видом или поведением.

Приоритет 4.8 уточнён 2026-10-06: реализовать после 3.5b вместе с первой формой 5.1a, до collection bindings и завершения всего набора контролов. Принадлежность к этапу 4 и требования фабрик сохраняются.

Правила и готовность 4.8:

- Каждая фабрика объявляется на собственном типе и возвращает его конкретный тип, чтобы последующая Fluent-цепочка сохраняла все методы контрола. Для производных контролов нельзя подменять фабрику унаследованным методом, возвращающим базовый тип.
- Каждый вызов создаёт новый независимый элемент. Фабрика находится в Argentis, принимает параметры создания и не требует окна, UiSession, App/DI или графического backend. Attach/detach, ownership, свойства и динамическое дерево сохраняют принятые правила.
- Публичные конструкторы продолжают поддерживаться для обычного создания, наследования и пользовательских контролов. Фабрики и конструкторы можно использовать рядом в одном дереве; отдельного промежуточного builder не требуется.
- При реализации проверить конкретный возвращаемый тип и доступность Fluent-цепочки, независимость создаваемых экземпляров и эквивалентность обычной фабрики конструктору по исходным свойствам/поведению. Примеры и демонстрации должны показывать оба способа создания. Фактические проверки 2026-10-07 перечислены отдельно в STATUS.

Реализация 4.8 / 5.1a — 2026-10-07: доступны Button.Simple, Text.Plain и собственные Create на TextBox, CheckBox, ProgressBar, Slider, Image, Panel, ContentControl, Border, ScrollViewer, VStack, HStack, Line и Rectagle. Производные Slider/Border/ScrollViewer явно скрывают базовый Create, возвращая свой тип; это не virtual factory и не способ создания пользовательских наследников. Shapes и public constructors сохраняются; Line.Create не добавляет рисование к прежнему пустому Line. AlloyTest получает SettingsModel через DI, редактирует три свойства через BindTwoWay и показывает Summary через BindText; две кнопки меняют модель без перестройки дерева. После первоначального native0xC0000374 диагностика выявила input cleanup после уничтожения GLFW window. Этот lifetime defect исправлен; UI188/188, по10 form smoke с отладочной кучей и по6 lifecycle сценариев в Debug/published Release прошли. Связь исходного неперехваченного0xC0000374 с найденным дефектом — гипотеза, не отдельный полученный стек; long soak/alpha остаются впереди. Цели, зависимости, ownership и K1–K9 не меняются; следующий ближайший шаг — 3.5c/d / 5.1b.

Уточнение window lifetime, 2026-10-07: SilkWindowHost.Run завершает цикл без Reset, сохраняет native window/context и input до host.Dispose на том же потоке. Hosting освобождает renderer/session, input снимает GLFW callbacks с ещё живого окна, затем host уничтожает окно. OpenGL session по-прежнему освобождается в Closing; Render/Input callback errors перехватываются до выхода через native boundary. Прямой NativeWindow.Reset/Dispose до host.Dispose нарушает ownership. Это исправление teardown порядка внутри Platform.Silk, без новых зависимостей или изменений backend/Argentis tree contracts.
Уточнение callback boundary, 2026-10-10: Platform.Silk guards защищают native-origin mouse/wheel/key/text/focus/render handlers, включая DPI normalization до входа в Hosting. Pending Win32 hook fault проверяется внутри boundary до user handler; первый exception/stack сохраняется, поздние callbacks подавляются. Direct callers получают ошибку через VerifyWindowAccess после manual dispatch либо Run (включая финальный dispatch); Hosting сохраняет Completion/StopAsync и best-effort cleanup. Bounded fault/recreate proof — HISTORY/STATUS. Public API/ownership и требования physical DPI, долгого lifecycle/VRAM и full alpha matrix не сокращены.

Уточнение cleanup/K7, 2026-10-10: own Render/Focus subscriptions снимаются при constructor failure и Dispose SilkWindowHost; Pfn Vulkan validation wrapper сохраняется до DestroyInstance и освобождается после последнего native callback. Это исправления callback ownership без изменения public API. Window recreation/resize и native-only controls добавлены как воспроизводимые probes; текущий длительный resource result **failed** на USER/native growth GL/new-thread. Нулевые hooks/registry/WeakReference и presenter counters не заменяют общую resource stability/VRAM evidence. Причина native allocation и новая thread lifetime стратегия ещё не согласованы; текущий dedicated UI thread контракт сохраняется. Подробности — [статус](STATUS.md) и HISTORY. K7 и alpha требования не сокращены.

Уточнение следующего шага K7,2026-10-10: raw Win32/WGL controls воспроизводят USER growth без GLFW/Silk/UI/Skia; serial thread reuse и manual shared owner loop с двумя live окнами проверены отдельно. **Предлагаемое направление Hosting — app-lifetime dispatcher с единым owner event loop**, поскольку GLFW ограничивает window creation/event processing main thread. Worker pool не принимается как portable window scheduler. Production dedicated-thread-per-window контракт этим diagnostic шагом ещё не изменён. Перед его заменой нужны явные startup/shutdown/cancellation/command/fault contracts, concurrent-window isolation, context switching, Vulkan integration и повтор исходного full lifecycle/resource check без ослабления лимитов. Точный native allocator и mixed GL/Vulkan private/handles growth не объявляются устранёнными. [Основание GLFW](https://www.glfw.org/docs/latest/intro.html#thread_safety), [результаты](HISTORY.md#wgl-thread-lifetime-и-shared-owner-loop--2026-10-10). Alpha/K1–K9 сохраняются.

**Реализованный контракт Hosting dispatcher — 2026-10-10.** По запросу приступить к задачам заменён новый Thread на каждое окно: app-scoped HostedUiDispatcher владеет окнами приложения, все scopes используют один Windows process owner loop. Это явное уточнение lifetime предложенного app dispatcher: HWND/device/session имеют app/window lifetime, idle background owner thread сохраняется до выхода процесса, чтобы новый App/scope не возобновлял WGL thread growth. Scoped shutdown не выполняет global GLFW termination. Native ownership, последовательный GPU handoff и отдельный device/session каждого окна сохраняются. Linux/macOS portable main-thread scheduling не заявлен; direct Silk hosts не запускаются конкурентно на другом потоке.

- Start идемпотентен; cancellation отменяет wait, не принятую работу. Close-before-start завершает Completion без allocation и отменяет readiness; после accepted startup stop ждёт owner cleanup.
- Commands FIFO, finite64 batch; startup batch32; отказ одного окна не прекращает siblings. Readiness после window/device/session init; Completion после cleanup и close callback, original cause перед cleanup errors. Закрытые task/delegate targets удаляются из постоянно живущего loop.
- Scope Stop отклоняет новые starts и закрывает активные окна; canceled wait не отменяет shutdown. Завершённые faults наблюдаются через соответствующее окно. Owner callbacks используют Close; ожидание shutdown на owner thread отвергается.
- GL context явно выбирается перед commands/input/render/cleanup, включая sibling native callbacks. SilkWindowHost.Pump сохраняет DPI scope; WM_PAINT refresh через существующий DPI hook имеет callback/reentrancy guards, без нового HWND hook и без unwind в native code.
- App UsePresentation регистрирует dispatcher; прежний четырёхпараметровый HostedUiWindow constructor сохранён, дополнительная перегрузка принимает scope. HUD и renderer/native ABI не меняются.

Функциональная реализация, unit tests, concurrent/native callback/short/chrome/controlled-failure checks и App smoke записаны в [STATUS](STATUS.md)/[HISTORY](HISTORY.md#hosting-owner-dispatcher-и-vulkan-wsi-lifetime--2026-10-10). Full resource check остаётся **failed**: USER стабилен, GDI растёт. Isolated device/Skia controls не показывают GDI trend, но их handles/private также растут; native WSI queries/create/destroy и raw presenter воспроизводят GDI рост. Удержание loader не устраняет его. Это не доказательство конкретного driver allocator. Следующий доступный K7 шаг — изоляция native WSI lifetime и безопасного решения без скрытого shared-device cache, удаления чужих GDI objects или ослабления лимитов. Alpha/net10.0/xUnit/K1–K9 сохраняются.

**Результат следующего K7 investigation — 2026-10-10.** WSI stages/raw Win32/repeat и debugger controls отделили queries от первого swapchain create и зафиксировали screen HDC allocation внутри текущего NVIDIA ICD, без последующего ReleaseDC в bounded lifecycle. Raw surface-first/device-first teardown не меняет GDI trend; завершение raw owner thread очищает его, но не является принятым production scheduling fix: прежний WGL/new-thread USER growth и GLFW/concurrent-window contracts сохраняются. Raw vendor selector проверил установленный AMD: GDI5→5 против NVIDIA6→29 в Debug/Release; full UI на AMD этим не подтверждён, production GPU policy не меняется. Device/session/presenter ownership сохраняется, ICD-owned HDC не освобождаются приложением, thresholds не ослабляются. Нужен supported NVIDIA fix либо полностью проверенная alternate GPU configuration; private/handles growth остаётся отдельным вопросом. Independent package consumer5.4–5.5 можно выполнять до устранения environment-specific K7 blocker, однако итоговый alpha проход по-прежнему требует полного K7. [Evidence и воспроизведение](HISTORY.md#wsi-stages-raw-win32-и-driver-screen-dc--2026-10-10).

Результат — интерактивный базовый набор контролов, работающий через общую сессию UI в обоих сценариях запуска.

### Подзадача 4.9. Прозрачные окна и собственное оформление — добавлена 2026-10-08

По запросу пользователя расширить первую рабочую версию поддержкой прозрачных/полупрозрачных standalone-окон, отключения стандартной рамки и заголовка, а также опционального системного поведения при перетаскивании собственного заголовка. Отсутствие рамки, способ прозрачности и системное поведение настраиваются раздельно. Обычное окно по умолчанию сохраняет стандартное оформление и непрозрачность.

- **4.9a. Проверка оконной композиции.** До реализации публичного API сделать минимальный Windows x64 prototype на текущих OpenGL/Vulkan presentation путях: непрозрачное окно, общая opacity, per-pixel alpha0/0.5/1 поверх видимого фона другого окна. Проверить фактическую поддержку desktop composition, alpha format/blending и для Vulkan supported composite alpha; прозрачная offscreen UI-текстура/HUD не доказывает прозрачность standalone-окна. Зафиксировать capability matrix и ограничения Silk/GLFW/driver. Если текущий presenter не поддерживает требуемый режим, явно спроектировать отдельный Windows composition adapter; не переключать backend и не объявлять непрозрачный fallback успехом молча.
- **4.9b. Прозрачность.** Предусмотреть явные режимы opaque, общая непрозрачность окна (opacity0–1) и попиксельная прозрачность с полупрозрачным UI/непрозрачными контролами. Валидация и reporting неподдерживаемых сочетаний обязательны. Для текущего GLFW пути общую opacity и transparent framebuffer не включать одновременно: документация объявляет результат неопределённым. Поведение ввода в прозрачных областях определить отдельно и проверить нативно; визуальная alpha сама по себе не является контрактом click-through. Управляемый click-through, blur/Acrylic/Mica и тени DWM — отдельные возможные расширения, в обязательный объём4.9 не входят.
- **4.9c. Окно без системного оформления.** Отключаемая системная рамка/заголовок, собственный заголовок из Argentis-элементов и команды minimize/maximize/restore/close через оконный контракт. Явно определяемые drag regions и исключённые интерактивные области: кнопки, TextBox и другие controls не начинают перемещение окна. Для resizable окон определить невидимые границы/углы resize; учитывать min/max размеры, maximized work area и DPI. Визуально окно остаётся без системного оформления; необходимые Win32 styles выбираются с сохранением системного поведения.
- **4.9d. Опциональное системное перемещение и Snap.** Для собственного заголовка предоставить opt-in native move/resize: системная drag loop, Snap к краям/углам и Snap bar/layouts на поддерживаемой Windows11 при включённых пользователем настройках. Восстановление maximized окна перетаскиванием, double-click maximize/restore и системное меню должны сохранять ожидаемое поведение. Если есть собственная кнопка maximize/restore, поддержать Windows11 Snap Layouts при наведении и Win+Z. Направление реализации — Win32 non-client hit testing (`HTCAPTION`, resize codes, `HTMAXBUTTON`) и системная обработка сообщений; точный механизм согласовать после prototype, не имитировать системные previews рисованием в UI. Уважать Windows settings, не менять их; Aero Shake проверять отдельно при включённой настройке. Peek обозначает просмотр рабочего стола и не является названием Snap при перетаскивании.

**Границы архитектуры.** Конфигурация и capability/команды доступны через Hosting и независимый оконный контракт Alloy; HWND/DWM/message hooks и их owner-thread lifetime остаются в Platform.Silk/Windows adapter, выбор alpha/presentation — в Rendering/Hosting. Argentis описывает собственный заголовок через независимые роли/геометрию, без Win32/Skia/Silk типов. Конкретные имена API пока не согласованы. Внешний HUD не меняет оформление окна игры. Native callbacks/hooks снимаются до уничтожения HWND; исключения не выходят через native boundary. net10.0, xUnit, retained дерево и ownership сохраняются.

**Порядок и готовность.** Ближайший performance profile/подтверждённая оптимизация K9 сохраняются; затем4.9a,4.9b/c/d и оконная демонстрация до итогового alpha прохода5.5. Это явное расширение объёма первой версии: к K3 добавить прозрачность/собственный заголовок/native Snap, к K6/K7 — validation и lifecycle новых presentation/hooks. Проверять OpenGL/Vulkan на заявленной конфигурации, реальные desktop alpha, drag/excluded controls/resize/buttons/Snap, включённые и выключенные системные настройки, DPI100/150/200% и переход между мониторами, minimize/zero-size/restore, повторные create/close без retained callbacks/resources; выполнить Debug/published Release smoke и регрессию обычного окна/HUD. Прототип, реализация и прошедшие проверки фиксируются отдельно; неподдержанный обязательный режим остаётся блокером4.9, изменение требований фиксируется явно.

Источники для реализации: [GLFW: прозрачность окна](https://www.glfw.org/docs/latest/window_guide.html#window_transparency), [Win32 custom frame/DWM](https://learn.microsoft.com/en-us/windows/win32/dwm/customframe), [WM_NCHITTEST](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest), [Snap Layouts для custom title bar](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-snap-layout-menu), [Snap/Snap bar и настройки Windows](https://support.microsoft.com/en-us/windows/experience/snap-your-windows).

**Исторический результат 4.9a, 2026-10-08.** В Debug/published Release проверена реальная desktop-композиция поверх другого окна: OpenGL поддержал opaque/общую opacity/per-pixel alpha; Vulkan поддержал opaque/opacity, но не per-pixel ни с обычным Opaque, ни с internal PreMultiplied experiment. Driver объявляет Opaque|PreMultiplied, однако это не доказало прозрачность HWND. Публичный default presenter сохраняет Opaque-first; эксперимент не является публичным API или принятым режимом поставки. Подробные raw RGB, конфигурация и регрессия — [STATUS](STATUS.md).

**Пересмотр направления 4.9a.1 после изоляции, 2026-10-08.** Вывод о необходимости отдельного composition adapter был преждевременным: отсутствие desktop alpha в двух проверенных конфигурациях не исключало проблему HWND/DWM. По согласованной проверке сначала выполнен Vulkan proof без native Skia: `vkCmdClearColorImage` → существующий GPU blit/presenter → GLFW swapchain. В Debug/published Release transparent framebuffer + PreMultiplied в decorated окне повторил смешивание с белым; `PatBlt(BLACKNESS)` клиентского DC после show/resize устранил его. Alpha 0/128/255 над двумя фонами прошла initial/resize/restore; тот же эксперимент с настоящим Skia UI также прошёл. Borderless raw Vulkan прошёл на этой машине и без очистки; это наблюдение не отменяет явной инициализации redirection surface. Источники гипотезы: [GLFW PR #2815](https://github.com/glfw/glfw/pull/2815), [PatBlt](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-patblt). Фактические измерения и ограничения — STATUS.

1. Следующий шаг — **4.9b: оконный configuration/capability contract и интеграция подтверждённого GLFW/Windows пути**. Для per-pixel явно запросить TransparentFramebuffer до создания окна, проверить supported composite alpha и выбрать PreMultiplied для premultiplied output. Windows adapter должен обеспечивать инициализацию redirection surface в нужные моменты lifecycle; выбирать механизм WM_PAINT/resize после проверки, а не копировать диагностическую очистку перед каждым frame. HWND/DC/hooks и owner-thread cleanup остаются в Platform/Hosting, Argentis/HUD не получают Windows типов.
2. Сохранить обычный opaque путь и отдельную whole-window opacity; неподдержанный per-pixel сообщает причину, без скрытого fallback. Обычный VkSwapchainKHR используется для подтверждённого режима, SkiaVulkan renderer и UI image ownership сохраняются. Readback в prototype проверяет источник и не участвует в desktop presentation.
3. Повторить desktop proof с публичной конфигурацией: оба backend, decorations on/off, resize/minimize/restore, repeated create/close, DPI/monitor transitions и регрессия обычного окна/HUD. Диагностический успех ещё не означает готовность lifecycle/configuration/input контракта. Borderless title bar/native Snap 4.9c/d следуют по прежнему порядку.
4. Layered HWND/UpdateLayeredWindow с явным CPU transfer и GPU DirectComposition/DXGI interop остаются резервными направлениями только для конфигураций, где существующий GLFW/Vulkan путь не проходит проверку. Их реализация сейчас не требуется; для любого будущего transport отдельно подтвердить capabilities, стоимость, ownership и cross-API completion.

Это явное изменение порядка технической реализации после нового desktop proof. Цели Vulkan/per-pixel, net10.0/xUnit, retained UI, HUD ownership и критерии готовности 4.9 сохраняются.

**Принятый и реализованный контракт 4.9b, 2026-10-08.** Alloy содержит immutable `WindowAppearance` (`Transparency`: Opaque/Opacity/PerPixel, initial `Opacity`, `Decorated`) и необязательный `IWindowAppearanceHost`, не меняющий существующий IWindowHost. Параметры валидируются до native allocation; finite opacity0–1 допускается только в Opacity режиме, другие режимы требуют initial opacity1. Native `WindowAppearanceCapabilities` отдельно сообщает transparent framebuffer и uniform opacity; Vulkan presenter отдельно проверяет supported composite alpha. Hosting выбирает premultiplied для PerPixel и opaque для остальных явных режимов, без скрытого fallback. No-argument Vulkan presenter сохраняет прежнюю alpha precedence для существующих callers/HUD. HostedUiWindow фиксирует snapshot options, сообщает combined per-pixel support после startup и ставит SetOpacity в очередь owner thread. Прямой SilkWindowHost принимает те же appearance settings и предоставляет thread-bound SetOpacity.

Windows per-pixel adapter использует SetWindowSubclass, сохраняет GLFW WndProc и обрабатывает creation/WM_PAINT/WM_SIZE/show/DPI/composition события. BLACKNESS очистка выполняется до GLFW damage callback, а не на каждом presentation frame. HWND заимствован, client DC имеет SafeHandle lifetime; static callback и GC root живут до RemoveWindowSubclass/WM_NCDESTROY. Ошибки native callback сохраняются для последующего managed reporting; hooks снимаются на owner thread до HWND destruction. Политика per-pixel input — обычная client-area hit testing, alpha не включает managed click-through. На проверенной Windows alpha0/128/255 области по WindowFromPoint принадлежат окну; полный native mouse interaction и другие configurations остаются отдельными проверками. Uniform opacity пока заявлена в adapter только для Windows.

`Decorated=false` в4.9b реализовал создание без системной рамки. Принятый ниже4.9c добавляет собственный Argentis title bar/команды/resize regions; полная native DPI/monitor/Snap matrix остаётся открыта. Реализация4.9b отделена от её полной переносимой верификации: фактические Debug/published Release, source/desktop/input/lifecycle checks и промежуточные failures — STATUS. net10.0/xUnit и исходные критерии готовности сохраняются.

**Принятый контракт4.9c, 2026-10-08.** Необязательные независимые `IWindowCommands` и `IWindowChromeHost` предоставляют state/minimize/maximize/restore/close, capabilities и публикацию immutable `WindowRegionMap`. `WindowChromeOptions` задаёт NativeDrag (defaultfalse), Resizable, DPI-aware resize border и min/max client sizes. Chrome opt-in требует Windows x64 и Decorated=false; null сохраняет исходный GLFW behavior. У Argentis появился WindowRegionRole Inherit/Caption/Client и WindowFrame с отдельной высотой заголовка; TitleBarFactory Hosting создаёт Argentis content на owner thread с независимыми commands. UI session владеет title/content, HUD не меняется.

Геометрия копируется с учётом paint order/clipping/visibility после update. Focusable controls (включая disabled) и explicit Client subtree исключены из перемещения; custom non-focusable input controls должны явно задавать Client. Native callback не вызывает tree/пользовательские delegates; stale caption map после resize не применяется. Windows adapter сохраняет SYSMENU/minimize/maximize/THICKFRAME semantics по настройкам, снимает видимую рамку через WM_NCCALCSIZE и возвращает native HTCAPTION/resize codes. WM_GETMINMAXINFO использует work area текущего монитора и application limits; resize через оконный контракт задаёт точный client size, обходя GLFW стандартный frame adjustment. Resize border масштабируется по GetDpiForWindow, limits/regions — по существующему UI viewport scale; общая DPI-модель не меняется. Hook/GC root lifetime и reporting callback errors сохраняют правила4.9b.

Это уточняет границу4.9c/d: базовый opt-in native move/resize нужен уже для4.9c;4.9d завершает Snap Layouts/Win+Z/maximize-button hit testing, menu/double-click/maximized drag и реальные interaction/settings/DPI/monitor проверки. Native previews не рисуются вручную. Наличие API и directed-message/desktop proofs не означает прохождение всего4.9 или alpha; фактические результаты — STATUS.

**Реализованный контракт4.9d, 2026-10-08; bounded physical/desktop proof выполнен, полная matrix остаётся открытой.** `WindowChromeOptions.NativeSnapLayouts` — отдельный opt-in (default false), требует Resizable и Windows11; platform capability `SnapLayouts` не означает включённые пользовательские настройки. Явная Argentis роль `Maximize` на enabled кнопке даёт `HTMAXBUTTON`, disabled/Client ancestor сохраняют Client. Non-client hover/leave передаётся DWM; press/release/cancel копируются в очередь UiInput и доставляются вне native callback. Командой владеет Argentis OnClick, native default maximize на press подавляется; Space/Enter сохраняются. Direct Silk hosts должны вызвать DispatchPendingWindowInput после DoEvents, Hosting делает это автоматически.

`SystemMenu` default true: Alt+Space, caption right-click и optional `IWindowSystemMenu.ShowSystemMenu(x,y)` с logical client point, queued equivalent в Hosting. Disabled menu не открывается; native menu учитывает state/Resizable и отправляет выбранный system command самому окну. Caption double-click делегируется системе. Пример использует adaptive header и minimum330×300 вместо760×400 для узких Snap zones. Это уточнение API/демонстрации не сокращает критерии готовности4.9d: real system move/resize, maximized drag, edge/corner/bar/hover/Win+Z, settings on/off, Aero Shake, physical DPI/monitor и lifetime требуют отдельного подтверждения. Исторический LockApp foreground/desktop blocker и новые physical результаты — STATUS; не считать направленные сообщения доказательством Shell Snap UI.

**Уточнение native styles после physical prototype4.9d, 2026-10-08.** Для custom frame снимается GLFW `WS_POPUP`: окно использует overlapped semantics с SYSMENU/minimize/THICKFRAME/maximize по настройкам, без `WS_CAPTION`. Caption regions задаются `HTCAPTION`; полноразмерный client сохраняется через `WM_NCCALCSIZE` с wParam=true. Добавление WS_CAPTION оказалось ненужным для Snap и добавляло невидимые maximize margins; этот вариант отвергнут. Рабочая область/max-size контракт4.9c сохраняется. Проверки видимого оформления используют actual HWND/client geometry, а не один style bit. Alt+Space обрабатывается также на WM_SYSKEYDOWN с Alt context и без autorepeat, до стандартного GLFW keyboard-menu/WM_SYSCHAR suppression; глобальные hotkeys ОС/других приложений имеют приоритет. Native Snap previews остаются ответственностью Windows. Фактические physical/desktop proofs и оставшиеся settings/DPI/monitor критерии — STATUS; прежний LockApp blocker относится к историческому запуску.

### Этап 5. Собрать демонстрации и подготовить alpha

- **5.1. Самостоятельное приложение.** Развивать один AlloyTest вместе с API: **5.1a** — форма с редактируемой моделью, TextBox/CheckBox/Slider и смешанным созданием через фабрики/конструкторы; **5.1b** — динамический список и условные области; **5.1c** — прокрутка, смена темы и полноценные взаимодействия всех заявленных контролов. Добавление/удаление, редактирование и переключатели должны сохранять состояние незатронутых элементов. Пример становится общей контрольной сценой для standalone, backend comparison и адаптируемого HUD.
- **5.2. Vulkan HUD.** Добавить показатели, динамический список и интерактивную панель поверх сцены.
- **5.3. Обновление по необходимости.** Самостоятельное приложение перерисовывается при изменениях и анимациях; HUD обновляет UI-текстуру по необходимости, а композицию выполняет хост.

Уточнение реализации общей сцены5.1c/5.2–5.3, 2026-10-07: AlloyTest сохраняет View1/SettingsModel и выбирает OpenGL по умолчанию или Vulkan через `--vulkan`. AlloyVulkanTest compile-link исходники той же формы, модели, generated rows/условной области/VolumeMeter и SettingsFormSmoke, доставляет тот же PNG; новой shared assembly или зависимости на manual executable нет. Один interaction script используется на raster/OpenGL/Vulkan при одинаковых logical viewport/scale1/1.5/2 и в Vulkan HUD. HUD оборачивает ту же форму прозрачным Panel, добавляет показатели/кнопку сцены, использует borrowed raw Vulkan handles и host-owned composition/presentation; вне панели ввод проходит в сцену. Хост композитит retained UI независимо от UiSession.Update, модель сохраняется при явном пересоздании UI. Нового runtime/backend/native API не вводится. Сравнение всех logical bounds/model/input steps и выбранных RGBA pixels с допуском1 — функциональный smoke, не fixed-font/full screenshot golden. Фактические проверки и K2/K3/K7–K9 ограничения находятся в STATUS. Следующий этап по согласованному порядку —2.4/K8: контракт ошибки backend и явного пересоздания из модели.

- **5.4. Документация.** Описать запуск, создание виджета, привязки, подключение бекенда, потоковую модель и владение GPU-ресурсами.
- **5.5. Выпуск.** Выпустить alpha после прохождения критериев готовности ниже.

**Порядок независимых работ — 2026-10-10.** По решению пользователя текущий NVIDIA GDI defect учитывается как внешний блокер: безопасное решение со стороны TrueMoon не найдено, дальнейший поиск app-side workaround пока прекращён. Это не отменяет K7 и не разрешает выпуск alpha с failed lifecycle check. Private/handles growth остаётся отдельной неопределённостью. Ближайшая доступная задача — подготовка поставки5.4/5.5: локальные NuGet artifacts и отдельный net10.0 consumer без ProjectReference/compile-link исходников TrueMoon, проверка transitive dependencies и bundled native Skia в build/publish output, запуск raster/OpenGL/Vulkan и App startup/shutdown в Debug/Release на Windows x64, документация подключения и ownership. Подготовка пакетов не является публикацией или выпуском alpha. Затем — K2/K4: доставляемые фиксированные шрифты с лицензией и полноценные golden fixtures общей сцены. Physical DPI/4.9 matrix, real device loss и VRAM evidence сохраняются; PA1/PA2 остаются после alpha. Возврат к NVIDIA K7 — при supported driver/ICD решении либо отдельно согласованной alternate GPU configuration и полном повторном проходе с прежними limits.

## Проверки и критерии готовности

**Уточнение K2/K4 — 2026-10-11.** Opt-in immutable `SkiaFontCatalog` в Rendering.Skia загружает managed snapshots лицензированных font files; backend и Hosting принимают каталог явно, surface сохраняет creator-thread/FIFO16/native font ownership. Unknown alias использует explicit default face, без системного fallback. Стандартный system-font путь, native ABI, net10.0 и xUnit сохраняются. Settings full golden:30CPU PNG (10script frames×3logical scales), точное layout/model/input сравнение и все premultiplied RGBA pixels; raster/golden и OpenGL/Vulkan channel tolerance2, GPU/raster symmetric radius1 envelope с contrast≥8/+24 и strict2 для flat/low-contrast. Нет masked/outlier pixels. Правило учитывает измеренные AA coverage differences, не обещает pixel-identical CPU/GPU или emoji shaping. Normal run read-only, baseline recording только в новый explicit directory после визуальной проверки. [Контракт, assets/license и команды](GOLDEN_TESTS.md). Проверка этого scope не отменяет K3/K7/K8/VRAM/final alpha matrix; следующий независимый общий шаг — CORE5/COB4.

- **K1. Автоматические проверки ядра.** Тесты дерева, layout, свойств, привязок, коллекций, фокуса и маршрутизации ввода без GPU.
- **K2. Рендеринг.** Проверки на raster-поверхности с фиксированными шрифтами; сравнение GPU-результатов с допустимой погрешностью.
- **K3. Windows.** Проверки resize, сворачивания, восстановления и DPI 100/150/200%; отсутствие рисования в поверхность нулевого размера.
- **K4. Сменяемость бекендов.** Одинаковое поведение демонстрационного UI через Vulkan и OpenGL.
- **K5. HUD.** Необработанный ввод проходит в сцену; сохраняется прозрачность; ресурсы хоста не уничтожаются библиотекой.
- **K6. Vulkan validation.** В демонстрационных сценариях нет ошибок синхронизации и времени жизни ресурсов.
- **K7. Повторное создание сессий.** Создание и закрытие сессий не вызывает накопления ресурсов или подписок.
- **K8. Потеря устройства.** Сессия сообщает ошибку хосту и допускает пересоздание. Автоматическое бесшовное восстановление не входит в первую версию.
- **K9. Производительность.** Зафиксировать время layout/отрисовки и аллокации на контрольной сцене. Статичный интерфейс не должен непрерывно перестраивать дерево и создавать графические ресурсы.

### Воспроизводимый итоговый проход alpha

Метод baseline K9 уточнён и реализован 2026-10-07: shared Settings View1,32 строки/195 исходных элементов (до200 при append), view720×1000/viewport900×1100/scale1, static/property-edit/list-edit на raster/OpenGL/Vulkan и Vulkan HUD. Два независимых Release-процесса, warmup≥100frames и≥500ms на case,500 samples; validation выключен при timing и включён в отдельных correctness checks. Owner-thread managed allocations и CPU wall time Update/MeasureCore/ArrangeCore/Render измеряет manual profiling root/surface proxy; вложенные фазы не суммируются повторно. GL completion, Vulkan export/return, HUD composition/presentation учитываются отдельно; GPU timestamps/native/VRAM не измеряются. Raw samples и source/binary/font/native/config fingerprints сохраняются в JSON, итоги — [PERFORMANCE_BASELINE.md](PERFORMANCE_BASELINE.md). Статичный UI проверяет сохранение tree/surface/image,0 layout/draw/Update allocations. Архитектура/runtime API и критерии K1–K9 сохраняются; performance budget не назначен. Следующая оптимизация выбирается после CPU/allocation profile выявленной фазы и проверяется повтором baseline.

Критерии K1–K9 сохраняются; функциональные результаты отдельных подэтапов не заменяют итоговый проход с актуальной демонстрацией. Зафиксировать поддержанную конфигурацию Windows x64, GPU/driver, SDK/packages/native revisions, команды и версии артефактов. Изменение конфигурации требует явно ограничить область вывода. Другие OS/GPU/EXT-only конфигурации — отдельные исследования по выявленным рискам.

- **K1 / controls.** На финальном коде выполнить UI suite, включающий two-way/collections/dynamic regions и реальные interactions всех заявленных контролов. Указать summary и ограничения; failures полного solution suite разбирать отдельно, не скрывая их результатами UI suite.
- **K2/K4.** Один view/model сценарий на raster/OpenGL/Vulkan: фиксированные доставляемые шрифты, одинаковые logical bounds и input results, прозрачность/clipping/text/images и документированный допуск сравнения pixels. Selected-pixel smoke отдельно от полного набора golden fixtures.
- **K3/K5/K6.** Resize/minimize/zero-size/restore и DPI 100/150/200%, включая переход DPI окна; HUD event handling/passthrough и сохранение ресурсов игры. На поддержанном Vulkan пути validation без ошибок синхронизации/lifetime, включая teardown.
- **K7.** Повторяемое создание/закрытие/resize с заранее фиксированным числом циклов; после завершения отсутствие накопления owned ресурсов, source subscriptions и retained roots. Counters не объявлять полным VRAM/driver профилем.
- **K8 / 2.4.** Определить контракт передачи backend/device/context ошибки хосту; прекратить недопустимую работу, выполнить безопасный cleanup и явно создать новую сессию/дерево из сохраняемой модели. Ошибка и recreate должны иметь воспроизводимый failure scenario с указанием, что проверялось реальной потерей устройства, а что контролируемой инъекцией. Автоматическое бесшовное восстановление не включается.
- **K9.** Зафиксировать сцену и размеры, число элементов, static/property-edit/list-edit режимы, warmup и число samples; измерить layout/draw time и allocations. Static UI не выполняет повторный layout/draw и не создаёт новые UI graphics resources; HUD composition и оконный presentation учитываются отдельно. Текущая invalidation приводит к layout от корня, когда layout требуется; dirty-subtree layout/partial redraw не обещаются. Оптимизацию выбирать после baseline и повторного измерения выявленной проблемы.
- **5.4/5.5 / поставка.** Debug/Release build и publish на заявленной конфигурации. Независимый потребитель восстанавливает только пакеты, проверяет bundled analyzer и native assets, создаёт View и проходит нужные оконные/внешние профили. Описать ограничения и миграцию; [адресная проверка композиции](#uia1-view-package-acceptance) и исторические прогоны не заменяют приёмку итогового артефакта.

Уточнение K9,2026-10-08: после sampled managed-stack/allocation profile реализован surface-owned paint/FIFO16fonts reuse для общего Skia drawing/text adapter, с owner-thread/lifetime guards и cleanup при eviction/disposal. Свежие baseline до/после и UI/native regression — [PERFORMANCE_BASELINE](PERFORMANCE_BASELINE.md#профиль-и-оптимизация-2026-10-08)/STATUS; это одна ограниченная оптимизация, без dirty-subtree/partial redraw/text-blob cache, public API/native ABI/package изменений. Следующий этап по согласованному порядку —4.9a. Alpha/K1–K9 сохраняются; первый нерегулярный standalone smoke failure учитывается отдельно от повторных успешных checks.

## Допущения и границы первой версии

- Использовать `net11.0`, SDK 11.0.100 и xUnit 2/VSTest по [профилю CORE8](../core/DOTNET11_MIGRATION.md).
- Допускаются изменения API прототипа с обновлением примеров.
- Переключение бекенда происходит при создании сессии.
- Поддерживаемая ОС первой версии — Windows. Vulkan HUD проверяется небольшим самостоятельным хостом.
- Linux/macOS, нативные системные контролы, IME, полноценная accessibility-интеграция, сложный редактор текста, виртуализация, docking и дизайнер относятся к следующим этапам.
- Сроки оценивать после графического прототипа, снимающего основную неопределённость интеграции Skia/Vulkan.

## Развитие UI после alpha

Согласовано с пользователем **2026-10-09**. Этот раздел фиксирует будущие работы над TrueMoon.Alloy и TrueMoon.Argentis; статус всех новых пунктов — **запланировано**. Требования alpha, её критерии K1–K9 и текущая точка продолжения по DPI сохраняются. Конкретные публичные API и сроки уточняются при начале соответствующего подэтапа.

### Цель beta и контрольное приложение

Развивать UI на одном регулярно используемом приложении: редакторе настроек и ресурсов с деревом навигации, списком и фильтрацией, формой свойств и журналом операций. Приложение развивается вместе с библиотекой и показывает, где создание UI требует лишней инфраструктуры. Его повторяемые сценарии становятся проверками поведения, диагностики и производительности.

Приоритет первой beta — контролы, формы и инструменты диагностики. Целевой результат: приложение с меню, диалогами, редактируемыми формами и большим списком, которое удобно писать на Alloy и диагностировать. Виртуализация сначала вводится для одного списка контрольного приложения; развитие деревьев и таблиц идёт следующими подэтапами. Сроки и объём остальных направлений определяются по потребностям приложения.

### Направления и порядок работ

| Пункт | Приоритет | Планируемый результат |
| --- | --- | --- |
| PA1. Контрольное приложение | Начало beta | Навигация, фильтрация, редактирование и журнал операций на общей модели; воспроизводимые пользовательские сценарии и перечень неудобств API. |
| PA2. Инструменты разработчика | Первая beta, начиная с PA1 | Инспектор дерева и выбранного элемента: bounds, clipping, фокус, capture, источники свойств, причины invalidation и timings layout/draw. |
| PA3. Общие overlays и команды | Основа контролов первой beta | Единые правила popup/menu/tooltip/dialog для порядка рисования, clipping, размещения, маршрутизации ввода и возврата фокуса. Команды доступны кнопкам, меню и shortcuts, сообщают возможность и состояние выполнения. |
| PA4. Контролы и навигация | Первая beta после PA3 | Menu, context menu, ComboBox, tabs, dialogs, popup/tooltip; согласованная навигация с клавиатуры и shortcuts. Набор вводится по сценариям PA1. |
| PA5. Формы и модель | Первая beta | Валидация, преобразования значений, режимы commit; async loading/cancellation и состояния выполнения без ручного повторения инфраструктуры в каждой форме. |
| PA6. Большие данные | Список в первой beta; дерево и таблицы далее | Виртуализированные списки и деревья, затем таблицы; стабильные ключи, сохранение выделения, фокуса и прокрутки. [Исследование пула строк](#ui-reuse-study). |
| PA7. Текст и доступность | Клавиатура вместе с PA3–PA5; отдельные следующие подэтапы | IME/composition, развитие текстового ввода, semantic tree и platform accessibility adapter для повседневной работы и автоматизации. |
| PA8. Оформление | PA8a — оформление существующих контролов; PA8c — отдельный плановый проход качества рендера и текста | Дефолтный минималистичный цветной стиль и оформление окна — PA8a; качество текста, DPI/геометрии и композиции — PA8c ниже. Системные theme tokens, templates, состояния, transitions и animations развиваются поверх общего поведения. |
| PA9. Vulkan/HUD | Отдельная ветка по реальной интеграции с движком | Асинхронный обмен кадрами, несколько frames-in-flight и явные completion tokens с новым контрактом владения ресурсами. |
| PA10. Производительность | По измерениям PA1 и последующих сценариев | Виртуализация, кеширование текста, уменьшение повторного layout и рисования; для каждого изменения профиль, baseline до/после и проверки корректности. [Исследование ресурсов рендера](#ui-reuse-study). |

<a id="ui-reuse-study"></a>
### UIA1f / UIA1h / PA6 / PA10. Повторное использование UI и ресурсов рендера

**Запланировано 24.11.2026.** Исследование определит, где повторное использование
даёт измеримый выигрыш и какой контракт нужен для каждого случая.

1. **Динамические поддеревья (UIA1f).** Сравнить текущее адресное обновление
   и конфигурацию с ключом, конкретным типом контрола и отложенной фабрикой.
   Проверить, что для совместимого узла фабрика вызывается только при его
   первом появлении, а вставка, перемещение и обновление соседей сохраняют
   его экземпляр, фокус и черновик. Испытать на динамической секции инспектора.
   Оценить читаемость рядом с фабрикой `Content(...)` и работу привязок с текущим
   `IDataContext`, `.Named()` и стилями. Правила совместимости узлов и форма
   конфигурации определяются по итогам прототипа.
   Первый срез завершён на `1.2611.25.10-alpha`: при перестановках секций того
   же источника достаточно адресного поиска и `Items.Move`. [Сценарий и измерения](HISTORY.md#inspector-section-move).
   При замене модели по тому же адресу редактор пересоздаётся даже при прежнем
   типе. Привязки и обработчики получают новый источник; соседние поля не
   пересоздаются. [Базовая проверка замены](HISTORY.md#inspector-property-replacement).
   Обычное обновление совместимой схемы уже сохраняет объект свойства в
   `EditorInspectorViewModel`. Сравнивать альтернативу с перепривязкой только
   после подтверждения частой замены совместимых объектов в реальном сценарии.
2. **Пул строк виртуализированного списка (PA6).** Взять длинный список
   контрольного приложения с интерактивными контролами в строках. Сравнить
   виртуализацию с созданием строки при входе в viewport и с ограниченным
   пулом по шаблону. Уточнить лимит по видимому диапазону и запасу, владельца
   пула и очистку при закрытии. Проверить смену записи, снятие подписок и
   отбрасывание старых уведомлений, сброс локального состояния и захвата
   указателя. Выделение и семантический ID относятся к записи. Проверить
   фокус, прокрутку и ввод в правильную запись после повторной выдачи строки.
3. **Ресурсы рендера (PA10).** Снять профиль того же сценария с действующим
   переиспользованием paint, шрифтов и геометрии иконок в Skia. По профилю
   выбрать один кандидат для прототипа, например кеш раскладки текста по её
   входам или пул временных буферов. Для кандидата определить владельца,
   лимит, условия инвалидирования и освобождения. Повторную выдачу GPU-ресурсов
   оценивать с учётом завершения работы GPU и возврата ресурса внешним
   потребителем. [Текущий baseline](PERFORMANCE_BASELINE.md).
4. **Типизированные презентеры (PA10).** Исходный вариант уже реализован в
   [UIA1h](#uia1h-control-presenters): сессия лениво создаёт презентер через
   `IFactory` и переиспользует его. Только если профиль выявит затраты,
   сравнить его с общим stateless-презентером и ограниченным пулом для презентеров
   с собственным состоянием или затратным созданием. Фабрика создаёт новые
   экземпляры при пополнении пула. Для выдачи и возврата определить
   владельца, момент окончания использования, сброс состояния и очистку
   при ошибке и закрытии сессии. Разделять регистрации одного типа контрола
   со штатным и пользовательским презентерами. Проверить, что повторная
   выдача сохраняет корректное рисование и очищает ссылки на прежний
   контрол и его состояние. Сравнить число вызовов фабрики и
   пиковое число экземпляров, аллокации и время отрисовки.

Для сравнения зафиксировать одинаковые данные, viewport и последовательность
прокрутки и правок. Записать созданные и живые экземпляры, managed allocations,
p95 времени обновления с отдельными фазами layout и draw, а для рендера также
релевантные счётчики ресурсов. Корректность сверить через `UiSession` и
семантический снимок после layout. Для изменения в рендере сравнить CPU raster,
а для GPU-ресурсов добавить проверку соответствующего backend и завершения
их использования. Результат исследования — baseline до/после и решение по
каждому прототипу на основании замеров и проверок жизненного цикла.

### PA3a/PA4a/PA8b. Меню и компактный анимированный заголовок — 2026-10-23

Минимальный профиль ниже реализован2026-10-23 и проверен Editor consumer;
уточнение disclosure и знак TrueMoon реализованы2026-10-24.
[Текущие проверки](HISTORY.md#ed4a-знак-truemoon-и-анимированное-раскрытие--2026-10-24).
[Evidence и границы](HISTORY.md#ed4a-компактный-заголовок-и-меню--2026-10-23);
полные PA3/PA4/PA8, modal stack и hardware acceptance остаются открытыми.

Согласованный consumer — [ED4a Editor](../engine/EDITOR_PLAN.md#ed4a-компактная-верхняя-панель--2026-10-23).
Общие controls/contracts принадлежат UI, прикладные commands/layout — Editor.

1. PA3a: nonmodal OverlayHost с одним активным popup, размещением в пределах
   viewport/переворотом у края, paint/hit testing поверх content, light dismiss,
   Esc/FocusLost и возвратом живого focus target. Popup принадлежит host до Close;
   Close/dispose снимает bindings/capture и не удерживает owner. Detach самого host
   скрывает popup, снимает borrowed owner/focus references; retained popup/tree
   принадлежит caller до Close/reattach/dispose. Detach owner при живом host закрывает
   popup на следующем owner frame. Tooltip
   не получает фокус и не перехватывает clicks. Full dialogs/modal stack — отдельно.
2. PA4a: MenuBar/MenuItem и интерфейс меню, shared command contract с availability,
   icon/text/shortcut, disabled и keyboard navigation (F10, arrows, Enter, Esc).
   Click heading открывает dropdown; hover переключает headings уже открытого меню.
   More остаётся в дереве: hover раскрывает headings; click/Enter/Space раскрывает
   их и открывает первую группу после transition. Повторный click закрывает,
   Esc/Tab/outside/FocusLost/detach отменяют pending open. Первый доступный пункт
   получает фокус без выполнения команды; F10 сохраняет выбор heading.
   Кнопки/меню/shortcuts вызывают одну операцию; async lifetime остаётся у command owner.
3. PA8b: owner-thread frame participation с monotonic time и небольшой числовой
   transition. Reveal180ms, close delay240ms, reversible easing без скачка;
   duration0 — immediate/reduced-motion route. Нет фонового Task/timer на control,
   idle frame requests прекращаются, detach/dispose прекращает работу. Те же reveal
   progress и owner frame поворачивают три точки ⋮→⋯ и открывают первую группу;
   отложенная операция не удерживает IInputContext. Hit bounds More неподвижны;
   обычного бордера нет, hover/pressed/focus состояния сохраняются.
4. Дополнить геометрические UiSymbols (More/Play/Pause/Stop/file/edit/build/theme/TrueMoon),
   сохранить accessible names, tooltip и disabled/hover/pressed/focus состояния.
5. WindowTitleBar получает owned custom header content внутри caption area;
   штатные minimize/maximize/close и native drag/double-click/Snap сохраняются.
   Interactive subtrees — Client, caption — Caption, overlaps исключены на small widths.

Проверки: deterministic frame time/reversal/idle/dispose; overlays bounds/keyboard/
outside click/focus restoration, disabled commands, client/caption region maps,
both DI providers и failed creation cleanup. Standalone raster/native и Editor
source/published proof фиксируются отдельно. Этот bounded slice не закрывает все
PA3/PA4/PA8 или physical DPI/real loss/K7/VRAM acceptance.

#### PA8b: матовые поверхности — 2026-10-24

По запросу пользователя общий механизм реализован в Argentis/Skia; первый consumer —
dropdown меню Editor. BackdropMaterial — immutable value (Gaussian sigma0..64 logical
pixels, tint с alpha, opaque fallback), Element.Backdrop/UiProperties.Backdrop —
typed property с обычным style precedence. Доступны WithBackdrop и ViewStyleRule.Backdrop.
Фон рисуется до обычного Background, foreground и children; непрозрачный Background
может закрыть эффект. Материал не меняет layout, focus, input или ownership.

IBackdropDrawingContext — optional capability без изменения IDrawingContext.
Unsupported backend рисует opaque fallback; null отключает material. Skia берёт
только уже нарисованное в текущем surface/frame, включая earlier materials;
capture ограничен видимой областью с halo3sigma и размерами поверхности.
Scale/translation/clip/rounded bounds учитываются. Snapshot/filter/paint живут
только во время draw; CPU ReadPixels для эффекта не используется. Sigma0/opaque
tint обходятся без snapshot. Дополнительных frame requests/timers нет; изменение
фона обновляет материал при обычном render invalidation без кеша прошлого кадра.

Theme.MenuBackdrop задаёт Dark/Light tint; null возвращает opaque Surface. Обычные
и disabled menu rows прозрачны, hover/pressed и focus indicator остаются различимы.
Проверены raster pixels при scale1/1.5/2 и Editor OpenGL native source/published.
Vulkan adapter подключён к той же реализации и компилируется; отдельная native
Vulkan/HUD effect matrix и performance/VRAM acceptance этим не заявлены. Desktop
за окном, OS acrylic/Mica, arbitrary canvas transforms/layers, saturation/noise,
общая effect graph и blur cache не входят в этот срез.
[Проверки и ограничения](HISTORY.md#pa8b-матовые-поверхности--2026-10-24).

#### PA8b.1: group opacity и panel material — 2026-10-24

Consumer ED4e Editor использует отдельный `Theme.PanelBackdrop` для закреплённых
островков поверх сцены; MenuBackdrop остаётся независимым. Null отключает эффект
и consumer рисует opaque Surface. `UiProperties.Opacity`/`Element.Opacity` — конечное
значение0..1 с render invalidation, без изменения layout/input eligibility.
Нулевая opacity пропускает draw; единица сохраняет обычный путь. Дробная opacity
применяется один раз к composited subtree через optional `IOpacityDrawingContext`;
каждый успешный SaveOpacity балансируется Restore, включая ошибки и nested groups.
Skia реализует transient SaveLayer/paint без retained ресурсов. Backend без
capability рисует дробную opacity непрозрачно; нулевая остаётся скрытой.

Input/visibility admission при закрытии принадлежит consumer: opacity сама по себе
не отключает hit testing, focus или frame participation. Editor отключает child,
оставляя wrapper участником160мс перехода. Acceptance включает raster overlap,
nested groups/images/backdrop, invalid bounds/alpha, style/theme precedence,
balanced state и native OpenGL Editor. Общая effect graph, render cache и
performance/VRAM/physical matrix остаются за рамками этого среза.

#### PA8b.2: оконный заголовок поверх content — 2026-10-24

Consumer ED4f использует opt-in `WindowFrame.TitleBarOverContent` и одноимённую
snapshot/validated Hosting option. Stable Content/TitleBar properties не зависят
от Children paint order. Default frame продолжает резервировать caption height;
overlay frame сначала рисует полное content, затем title и resize grip. Сохраняются
Caption/Client/Maximize hit regions и host-owned close/minimize/maximize actions.

Windows NC caption move/leave должен доходить до logical UiInput даже при
выключенном NativeSnapLayouts. Hook сохраняет default native dispatch для
drag/doubleclick/system commands; owned-HWND probe дополняет UiSession proof,
physical input/Snap matrix остаётся отдельной приёмкой.

WindowTitleBar.OverlayContent использует frame-driven160мс ScalarTransition только
для backdrop: idle tint alpha≤72/sigma0, hover до theme PanelBackdrop. Foreground/
bounds не меняются. `IWindowHeaderActivity.KeepsTitleBarActive` удерживает фон при
custom menu/focus; inactive hover поддерживается, native FocusLost очищает pointer.
Повторные события без смены target не инвалидируют render. Detach/dispose завершают
переход без timers. Null material — opaque Surface; unsupported backdrop — fallback.

OverlayHost.PopupInsets ограничивает measure/place popup внутри safe area,
оставляя основное content полным; значения finite/nonnegative, tiny bounds clamped.
Editor хранит document prompts как contextual retained View в своём layout;
общий OverlayHost остаётся владельцем transient menus/Tooltip выше островков.
Критерии: direct frame/title/placement/hosting tests плюс consumer native OpenGL.
Это не desktop transparency, новый scene transport, general modal stack или
physical/performance/VRAM acceptance. Детальные document tasks — в Editor плане.

#### PA8b.3: статус-бар поверх content — 2026-10-24

Реализовано; [актуальные проверки и границы](HISTORY.md#pa8b3-статус-бар-поверх-content--2026-10-24).

Consumer ED4f.1 включает StatusBar.OverlayContent: idle tint alpha≤72/sigma0,
hover/keyboard focus → Theme.PanelBackdrop за160мс. KeepBackdropVisible — явный
сигнал consumer для важных состояний или popup с focus вне самого бара. Общий
контрол не знает document/runtime semantics. Foreground/Opacity/layout/hit targets
не меняются; тонкий верхний separator проявляется вместе с подложкой. Null material
даёт Surface, unsupported renderer использует opaque fallback.

Frame-driven ScalarTransition без timers, unchanged target без invalidate, detach/
dispose отменяют переход. Анимационные frames не запускают overflow focus return:
он выполняется только по собственному pending flag. Default OverlayContent=false
сохраняет прежний контрол и overflow/input/ownership. Высоту/положение над полным
viewport задаёт consumer. Проверки: deterministic transitions/focus/idle, raster
backdrop/sharp foreground, theme/null и Editor native actual-scene idle/hover.

#### PA8b.4: системный акцент границы окна — 2026-10-24

Реализовано25октября; [проверки и границы](HISTORY.md#pa8b4-системный-акцент-границы--2026-10-25).

По согласованному дизайну opt-in WindowFrame.UseSystemAccentBorder и одноимённая
Hosting option выбирают cached IWindowSystemColors.AccentColor; null возвращает
Theme.Border. По потере native focus RGB умножается на0.6 с reversible переходом
160мс; alpha/толщина/geometry/foreground прежние. Local/binding/element/view styles
для BorderColor сохраняют штатный приоритет. Default opt-out других consumers
сохраняет прежнюю рамку; unsupported host использует theme fallback.

Windows adapter читает именно UISettings.GetColorValue(UIColorType.Accent),
используя документированный IUISettings3 ABI. HSTRING/COM refs/apartment временные,
освобождаются на читающем owner thread. Ошибки активации/HRESULT/API availability
дают null и не блокируют startup. WM_SETTINGCHANGE/WM_THEMECHANGED/
WM_DWMCOLORIZATIONCOLORCHANGED помечают dirty cache; refresh и render request
выполняются после event pump вне WndProc. Polling/native read на frame отсутствует.
Опциональная capability остаётся в Alloy, platform code не входит в Argentis.
Существующий App/Cobalt/IFactory путь создания окна/ownership сохраняется.

Acceptance: accent/update/failure/fallback/recovery, active/inactive160мс/reversal/
idle/detach/opt-out, explicit style precedence, immutable options/early validation,
native Windows source Debug/published Release с actual border pixels и owned-HWND
color/focus notifications. Personalization OS не менять для автоматического proof;
реальная пользовательская смена акцента и physical input остаются отдельными checks.

#### PA4b: StatusBar — 2026-10-24

Согласованный consumer — ED4b Editor. Общий StatusBar и IStatusBar capability
размещают primary/action, ordered collapsible secondary items и details trigger.
Приоритет essential controls, secondary скрываются целыми с exclusion из input/
Tab; overflow доступен потребителю. Child visibility/bindings не переписывать,
скрывать owned slots. Сужение/расширение сохраняет элементы и состояние, возвращает
фокус на details при скрытии focused slot. Bounds конечны и не перекрываются.
Нет registry/plugins/docking framework; список частей фиксируется при создании.
Иконки backend-independent, popup behaviour остаётся общим OverlayHost contract.

Реализовано2026-10-24.20новых layout cases; related UI759/Hosting98/Editor527
Debug/Release =1384/configuration,6native Editor cases/68PNG/12reaped children.
[Точные проверки и границы](HISTORY.md#pa4b-statusbar-и-ed4b-editor--2026-10-24).

Уточнение разметки2026-10-24: стандартный WindowFrame не резервирует отдельную
нижнюю строку под resize grip. Grip16×16 рисуется поверх правого нижнего угла
content; контент и StatusBar доходят до внутренней нижней границы рамки.
Показ/скрытие grip при maximize/fixed-size не меняет доступную высоту content.
Интерактивные controls оставляют место в углу; Editor уже резервирует справа24.
Это заменяет прежнее решение об отдельной нижней строке в default frame.

#### PA4c: TreeView — начат 2026-10-24

Consumer — [ED4c.1 обозреватель сущностей](../engine/EDITOR_PLAN.md#ed4c-обозреватель-сущностей--начат-2026-10-24).
Общий TreeView/ITreeView принимает frozen forest с stable string IDs, parent links,
labels и геометрическими icons. Один выбранный узел, retained expansion, keyboard
navigation, clipped row drawing и ScrollViewer reveal; parent move requests через
drag/drop и keyboard. Контрол не меняет предметные данные и не владеет моделями.
Проверки graph/duplicate/missing/cycle предшествуют замене; максимум4096items в
первом bounded profile. Поддерживаются rejection self/descendant/same-parent и
cancel capture при Esc/FocusLost/disable/detach/new snapshot. Нет фоновых timers,
resolution на кадр или полного пересоздания row-controls при обновлении.
Порядок row siblings следует входным данным. Уточнение2026-10-31 по ED4c.2:
optional ReorderEnabled (default false) добавляет consumer-owned PlacementRequested,
Ctrl+↑/↓ и Before/After в первой/последней четверти строки. Центр строки и пустая
область сохраняют parent-move semantics. Контрол отвергает self/descendant targets;
destination/anchor/revision/history валидирует consumer. Смена reorder capability
отменяет capture, как смена snapshot. UI не переставляет source items самостоятельно.
Inline rename остаётся композицией retained TextBox в Editor. Общий Element.FocusChanged
сообщает transfer/FocusLost/detach; UiSession отменяет input до закрытия dispatcher,
затем отменяет queued work и освобождает дерево. Это позволяет focus-loss callbacks
обновить bound model без отправки уведомлений в уже закрытый dispatcher.
Multiselect/typeahead, drag autoexpand/autoscroll и полная virtualization остаются
следующими consumer-driven расширениями.

Первый bounded TreeView реализован2026-10-24; editor использует общий контрол,
свой стиль и DI-created View. Прямые TreeView tests и native consumer проверяют
иконки/глубину/clip/выбор/раскрытие/перенос/capture/сохранение дерева при theme.
[Текущие проверки](STATUS.md), [точная область и команды](HISTORY.md#pa4c-treeview-и-ed4c1--2026-10-24).
Rename/reorder consumer ED4c.2 использует persistent engine metadata и общую history;
текущие проверки — [STATUS](STATUS.md).
Уточнение ED4c.3 от2026-10-31: MenuPopup принимает1..64frozen shared MenuAction,
пропускает disabled rows при Up/Down/Home/End/Tab и закрывается перед выполнением
команды. OverlayHost владеет popup; MenuItem заимствует command и снимает подписку
при Dispose. Escape focused popup закрывает его раньше expanded menu bar, возвращая
focus trigger. Shift+F10 оставлен context consumers, plain F10 сохраняет menu-bar path.
Editor использует общий popup для right-click/keyboard actions; document/history,
search policy и coalesced items/selection projection принадлежат consumer.
Расширения physical menu input, nested/scrolling menu и accessibility остаются отдельно.

### PA8a: дефолтный стиль контролов и оформление окна — 2026-10-21

По запросу пользователя запланировать единое оформление Alloy/Argentis:
**минималистичные цветные контролы с прямыми углами**, включая рамку окна без
системного оформления и виджеты управления им. Это развитие существующих тем
4.5/4.6 и custom chrome 4.9c/d в рамках PA8. По запросу пользователя **2026-10-22
PA8a0–2 реализованы**, автоматическая часть PA8a3 проверена; physical DPI/OS
acceptance остаётся отдельным открытым пунктом. Отдельный отложенный план
RichTextEditor сохраняется. Этап не требует предварительной
реализации всего набора будущих beta-контролов.

**Визуальное направление.** Плоские цветовые поверхности, прямоугольные контролы
и квадратные кнопки с иконками, радиус углов — 0, тонкие чёткие бордеры, компактные
согласованные отступы и простая типографика. Цвет выделяет действие, выбор,
фокус и состояние ошибки; у текста и значимых границ достаточный контраст.
Фокус, недоступность и ошибки имеют также различимые форму/маркер/состояние.
Точные палитры, наличие светлого/тёмного вариантов, размеры и набор иконок
определены в [спецификации стиля](DEFAULT_STYLE.md) по общей контрольной сцене:
Dark/Light, radius0, border1, focus2, error marker3, title36/buttons32/grip16.
Style Gallery демонстрирует публичные API и смену темы. `UseDefaultWindowFrame`
по умолчанию true только для явно включённого Chrome; обычное системное окно
и приоритет TitleBarFactory сохранены. Новый golden baseline SettingsSquare
сосуществует с неизменённым Settings. Результаты и открытые ограничения —
[в текущем статусе](STATUS.md), подробные проверки —
[в истории](HISTORY.md#pa8a-дефолтный-квадратный-стиль--2026-10-22).

| ID | Результат | Критерий завершения |
| --- | --- | --- |
| PA8a0 | Визуальная спецификация и сверка существующего API | Инвентаризация текущих контролов, тем и WindowFrame; контрольная сцена с вариантами оформления и состояниями. Зафиксированы палитра, типографика, размеры, иконки, focus indicator и границы первой реализации; определены необходимые расширения существующих contracts |
| PA8a1 | Дефолтная тема существующих контролов | Общие semantic tokens для фона/поверхностей, текста, акцента, границ, ошибок и disabled; единые spacing, размеры, толщины линий и состояния. Обычные конструкторы и Fluent-фабрики используют одну тему; local → style → theme → default сохраняется, настройки приложения продолжают работать |
| PA8a2 | Готовое оформление окна без системной рамки | Композиция WindowFrame: видимый бордер, собственный заголовок, кнопки minimize/maximize/restore/close, активное/неактивное окно и видимый resize grip в нижнем правом углу. Команды и доступность отражают реальное состояние/capabilities окна; drag/resize/input согласованы с существующим custom chrome |
| PA8a3 | Общая демонстрация, проверки и подключение | Галерея контролов/состояний и borderless-окно показывают единый стиль; проверены ввод, смена темы и native window behavior на заявленных backend/DPI. Документированы tokens, overrides, подключение и ownership; фактические build/test/native/package результаты и оставшиеся ограничения внесены в STATUS/HISTORY |

**Контролы и состояния.** Охватить существующие Button, CheckBox, TextBox,
RichTextEditor, Slider, ProgressBar, ScrollViewer/scrollbars, Border, контейнеры,
текст и используемые панели инструментов. Описать normal/hover/pressed/focused/
disabled, checked/selected, read-only и error там, где они применимы. Оформление
RichTextEditor опирается на его текущий API; функциональные улучшения остаются в
[собственном плане](RICH_TEXT_EDITOR_PLAN.md). Для будущих menu/tab/popup/dialog
переиспользовать tokens после появления поведения соответствующих контролов.
Смена темы в существующем дереве сохраняет значения, выделение, фокус и прокрутку.

**Окно и виджеты.** Собственный заголовок содержит название и опциональную иконку
окна, drag region и исключённые интерактивные области. Кнопки имеют единые размеры,
иконки, hover/pressed/disabled/focus и доступные названия; close вызывает общий
close request с действующими cancel/save/drain semantics INF4. Maximize/restore
отражает state и capabilities, сохраняет предусмотренную поддержку Snap/системного
меню. Resize grip использует тот же оконный resize contract, что границы/углы:
корректный курсор, hit testing, min/max client size и DPI. У maximized/fixed-size
окон resize grip и соответствующие зоны недоступны. Видимая толщина бордера
задаётся отдельно от удобной области захвата и не перекрывает ввод контента.
Двойной клик по заголовку, восстановление/перемещение и состояния активного окна
согласовать с уже принятым 4.9c/d behavior.

**Архитектура и совместимость.** Визуальные tokens и композиция принадлежат
Argentis; использовать существующие темы, WindowFrame, независимые IWindowCommands/
IWindowChromeHost и WindowRegionMap. Native hit testing, курсоры и оконные операции
остаются в платформенном adapter, интеграция — в Alloy/Hosting. Готовое оформление
подключается через существующую App/Hosting-конфигурацию custom chrome; обычное
окно сохраняет системное оформление по умолчанию согласно 4.9. HUD использует общую
тему контролов, а декорациями окна управляет его host. Конкретные новые имена API
уточняются на PA8a0; готовый набор templates/animations не является предпосылкой
первого визуального этапа.

**Проверки.** Зафиксировать visual fixtures общей сцены и матрицу состояний;
проверить raster/OpenGL/Vulkan, клавиатуру/Tab/активацию, pointer capture,
drag regions, minimize/restore/maximize/close cancellation, resize grip/границы и
повторное открытие окна. Размеры/hit regions проверять при 100/150/200% и переходах
между DPI; физические проверки учитывать отдельно от synthetic. Проверить idle
invalidation и cleanup после смены темы/закрытия. Изменения существующих golden
эталонов допускаются только после явной сверки нового оформления; прежние fixtures
и их результаты сохраняются как отдельный baseline. Сначала behavioral/raster
checks, затем native Debug/published Release и fresh package consumer по
затронутым contracts. Этот этап не закрывает K3/K7/K8/VRAM/final alpha и не
возобновляет остановленный поиск NVIDIA workaround.

### PA8c: качество рендеринга и текста — 2026-10-31

По запросу пользователя запланирован отдельный проход качества UI-рендеринга:
читаемый текст небольших размеров, чёткие линии/иконки, предсказуемое масштабирование
и одинаковая композиция на raster/OpenGL/Vulkan. Контрольные consumers — Settings
и существующие панели Aether.Editor: меню, дерево, inspector, console и text editors.
Первый срез PA8c.0 воспроизвёл расхождение теней у нижнего RichText-поля и
сохранил кадры трёх backend. [Результат](HISTORY.md#pa8c0-shadow-overlap-2026-11-20).
[Раздельная проверка теней](HISTORY.md#pa8c0-shadow-contributions-2026-11-20)
подтвердила выход за допуск при их совместной отрисовке на двух пикселях.
Базовый профиль для мелкого текста, каретки, выделения и линий при 1,25× и 1,75×
прошёл в Light. На GPU выделение на один физический пиксель шире, чем на raster.
[Матрица и кадры](HISTORY.md#pa8c0-fractional-typography-2026-11-20).
Первый замер PA8c.1 покрыл непрозрачную отрисовку, полупрозрачную панель и
наложение промежуточной текстуры. Границы глифов совпали во всех трёх случаях.
[Результаты композиции](HISTORY.md#pa8c1-text-composition-2026-11-21).
На той же матрице измерены сглаживание, hinting и дробное позиционирование.
Явное grayscale-сглаживание совпало со штатным.
[Результаты режимов](HISTORY.md#pa8c1-text-rendering-modes-2026-11-21).
У отдельных глифов `Subpixel` меняет пиксельный отпечаток при дробном сдвиге
при тех же advances. Граница advance может отсекать выступающий контур.
[Замеры позиции и отсечения](HISTORY.md#pa8c1-isolated-glyphs-2026-11-21).
В узком `TextBox` при 1,25× и 1,75× проверены границы строки, каретка,
выделение и попадание по клику при прокрутке с шагом 0,25 физического пикселя.
Дробное позиционирование приблизило движение отпечатка последней буквы к
движению строки. В Dark на трёх строках этот эффект повторился в обоих
направлениях. Скорость движения отпечатка зависит от глифа и фазы.
Штатные поверхности подтвердили эффект с обычным текстом и shaped runs.
По умолчанию дробное позиционирование используется в RichTextKit. Замер PA8c.5
на 12 обычных полях показал рост байтов кэша шрифтов при дробной прокрутке
и разные изменения времени Render между повторами. Замер на штатном
инспекторе Editor дал тот же объём GPU-кэша контекста Skia в обоих режимах.
Для обычных полей область настройки уже проверена в [PA8c.1a](#pa8c1a-positioning-rollout).
Осталась проверка в окне Editor при смене физического DPI.
[Замер в TextBox](HISTORY.md#pa8c1-textbox-positioning-2026-11-21).
[Повтор в Dark](HISTORY.md#pa8c1-textbox-dark-bidirectional-2026-11-21).
[Штатные поверхности Skia](HISTORY.md#pa8c1-textbox-renderer-positioning-2026-11-21).
[Замер стоимости позиционирования](HISTORY.md#pa8c1-textbox-positioning-cost-2026-11-21).
[Замер в инспекторе Editor](HISTORY.md#pa8c1-inspector-positioning-cost-2026-11-21).

<a id="pa8c1a-positioning-rollout"></a>
#### PA8c.1a — включение дробного позиционирования в контролах

Локальная точка отсчёта — `1.2611.25.8-alpha`: обычные поля поддерживают
явный выбор режима, а штатное поведение не изменено. Профили ниже различают
переключение всей поверхности и переключение только полей; первый не заменяет
второй при оценке стоимости. Это не приёмка опубликованных пакетов.

- [x] Сопоставить штатный режим, `off` и `on` для обычного `TextBox` и
  `NumberBox.Editor` на зависимостях `1.2611.25.6-alpha`: Light/Dark, три
  логических масштаба и три backend. [Профиль и результат](HISTORY.md#pa8c1a-editable-fields).

- [x] Повторить на `1.2611.25.7-alpha` короткое сравнение времени и кэша
  инспектора на raster/OpenGL/Vulkan при переключении всей поверхности:
  [результат и ограничения](HISTORY.md#pa8c1a-inspector-cost-7). Длинный
  профиль не завершён; короткие серии с ним не объединять.
- [x] В Rendering.Skia добавлен явный `EditorSubpixelPositioning` для обычного
  `TextBox` и внутреннего редактора `NumberBox`. Измерение и рисование используют
  один режим, ключ кэша шрифта учитывает его. `null` сохраняет штатное поведение.
  У статического текста и RichTextKit свой путь; grayscale, hinting и LCD не менялись.
  Включение по умолчанию — отдельное решение после проверки DPI и стоимости.
- [x] Сравнены поля в Light/Dark при логических масштабах 1×/1,25×/1,75×,
  изоляция подписи и стоимость настройки только для полей в инспекторе на
  raster, OpenGL и Vulkan. [Два порядка замеров и пиксельная проверка](HISTORY.md#pa8c1a-editor-scope-8).
- [ ] Проверить обычный `TextBox` и редактор `NumberBox` в окне Editor на двух
  мониторах с разным эффективным DPI. В Light и Dark задать выделение в
  прокрученном поле; записать DPI окна до переноса, на втором мониторе и после
  возврата. На каждой остановке сверить фокус, каретку и диапазон выделения
  до следующего ввода, затем попадание клика в ожидаемый символ и отсечение
  строки. Изменение логического масштаба на одном мониторе не заменяет переход.
- [ ] По результату оконной проверки и [замера стоимости](HISTORY.md#pa8c1a-editor-scope-8)
  зафиксировать решение о режиме по умолчанию. Сохранение явного выбора —
  допустимый итог; успешный DPI-переход сам по себе не основание включать режим.
- [ ] Отдельно сравнить статические подписи, кнопки, меню, дерево, многоточия
  и шрифтовые иконки при 16 логических пикселях. По кадрам решить, где расширить
  область включения. Выравнивание векторных иконок относится к PA8c.2.

#### Остальные проверки PA8c

Для PA8c.2 профиль задаёт исходные размеры и пиксели для проверки выравнивания.
Остальные этапы выбирать по найденным дефектам и замерам с отдельной приёмкой
каждого среза.

**Границы.** Общий контракт принадлежит этому UI плану. Argentis сохраняет
нейтральные text/layout/drawing contracts; Skia/font policy и native ресурсы —
Rendering.Skia, DPI окна и presentation — Platform/Hosting. Новые options/API и
их default/compatibility определить перед реализацией, без типов Skia в ViewModel.
Изменение 3D-освещения/материалов/сглаживания игровой сцены остаётся в engine плане;
здесь проверяется качество показа готового изображения в UI viewport/HUD.
IME, bidi navigation и новые функции rich editor ведутся в
[RTE плане](RICH_TEXT_EDITOR_PLAN.md), без дублирования его checklist.

Критерии готовности по подэтапам

| ID | Результат | Критерий завершения |
| --- | --- | --- |
| PA8c.0 | Исходная сцена и baseline качества | Доставляемые fonts/licenses и hashes, размеры/веса, logical/framebuffer bounds, scale, backend/driver/native revisions; исходные PNG 1:1 и увеличенные фрагменты, список конкретных дефектов и timings до изменений |
| PA8c.1 | Чёткость и сглаживание текста | Сравнить и явно выбрать antialiasing, hinting и fractional/subpixel positioning для небольшого текста; отдельно opaque, translucent и offscreen targets. LCD/subpixel AA допускается только при подтверждённых условиях target/composition; portable default и fallback проверены на всех трёх backend |
| PA8c.2 | DPI и точность геометрии | Согласовать logical→device mapping, stroke alignment, baseline/caret/selection и clipping на целых/дробных координатах. Линии, границы, иконки и текст не получают повторное масштабирование; measure/hit testing сохраняют логические координаты и точность |
| [PA8c.2a](#pa8c2a-vector-icons) | Векторные иконки панели инструментов Editor | Первый набор читается при 16 логических пикселях в Light/Dark. Сравнены кадры raster/OpenGL/Vulkan при 1×, 1,25× и 1,75×. Кнопки сохраняют доступные имена и управление |
| PA8c.3 | Метрики и разрешение шрифтов | Согласовать measure/draw, ascent/descent/line height и bounds для обычного и rich text; исключить обрезание верхних/нижних выносных элементов. Проверить кириллицу/латиницу, combining marks, ligatures и mixed font runs; определить поддержанные shaping/fallback/missing-glyph сценарии и проверять glyph advances вместе с caret/hit testing |
| [PA8c.3c](#pa8c3c-emoji-fallback) | Цветные эмоджи в смешанной строке | На raster/OpenGL/Vulkan подтверждены цвет, выбранный шрифт для каждого участка строки, высота и шаг каретки через составной эмоджи |
| PA8c.4 | Изображения, цвет и композиция | Явная sampling policy для 1:1, увеличения/уменьшения и pixel-art; проверить цветовое пространство, premultiplied alpha, opacity/backdrop/clip и прозрачные края. UI viewport/HUD показывает кадр без случайного double scaling, ореолов и повторного alpha blend |
| PA8c.5 | Стоимость улучшений и кеширование | Сравнить layout/draw CPU time, allocations и owned native/GPU resources до/после. Оптимизировать только измеренные затраты; bounded text/font/layout caches учитывают font/style/scale/render policy и корректно инвалидируются, evict/dispose не нарушают ownership |
| PA8c.6 | Итоговая визуальная и consumer приёмка | Общая visual matrix raster/OpenGL/Vulkan, Debug/published Release, source/fresh package consumer по затронутым API. Снимки визуально сверены при 1:1 и увеличении; focus/selection/scroll/editing и cleanup сохранены, расхождения и ограничения документированы |

**Сцена и проверки.** Подготовить отдельные fixtures для текста 10–18 logical units,
обычных/bold/italic начертаний, длинных строк, многострочного текста, выносных элементов,
combining sequences и отсутствующих glyphs. Проверять light/dark, normal/disabled/
selected/error states, caret/selection, прозрачные панели, hairlines, скругления,
иконки и изображения. Emoji/color fonts включать только в явно выбранный supported
профиль с доставленными assets и описанным fallback.

Synthetic scale matrix — 1/1.25/1.5/1.75/2; отдельно реальные Windows DPI и перенос
между мониторами, если окружение доступно. Directed/synthetic cases не заменяют
physical proof. Для text layout проверять bounds/advances/cluster positions и input,
для изображения — PNG и численное сравнение; selected-pixel smoke недостаточен.
Различать нечёткость исходного рендера и масштабирование захвата/просмотрщика.

Прежние K2/K4 fixtures и tolerances сохранять read-only. Intentional visual changes
получают отдельный baseline с before/after и визуальной сверкой; пороги сравнения
зафиксировать по измерениям до финального прогона, не расширять ради passing.
Performance budgets согласовать после PA8c.0/5, без заранее выдуманного FPS target.
При изменении font resolution/cache policy сохранить explicit catalog/default face
и lifetime; новые fallback/shaping возможности вводить как явное изменение контракта.

Начинать с behavioral/layout и raster checks, затем GPU/native и поставки. Смена
theme/font/scale, resize/minimize/restore и закрытие должны сохранять retained tree,
фокус, ввод, selection/scroll и освобождать owned ресурсы. Применяются прежние
K2/K3/K4/K6/K7/K9 и PA10; этот проход не закрывает автоматически K8/VRAM/final alpha
и не возобновляет остановленный поиск NVIDIA workaround. Реализацию и фактические
результаты каждого этапа записывать в STATUS/HISTORY отдельно от этого плана.

<a id="pa8c2a-vector-icons"></a>
#### PA8c.2a — векторные иконки Editor

Первый набор реализован и проверен на трёх бэкендах. В отчёте зафиксированы
геометрия и расхождения пикселей Open, Save и Play.
[Кадры и результаты](HISTORY.md#pa8c2a-vector-icons-2026-11-21).

Первый набор для панели инструментов — Play, Pause, Stop, Open, Save, Undo
и Redo. Согласовать толщину штрихов, окончания линий и визуальный вес всего
набора. Проверить читаемость при 16 логических пикселях и упростить мелкие
детали там, где они сливаются. Размер значка задаётся отдельно от области
клика. Цвет и доступность действия отражают тему и состояние кнопки.

Геометрия в Argentis описывает контуры в общей системе координат, например
24×24. Заливку и обводку выполняет адаптер Rendering.Skia для raster, OpenGL
и Vulkan. SVG подходит как исходник для подготовки контуров на этапе сборки.
Сохранить семантику действий, клавиатурный фокус и доступные имена текущих
`IconButton`. Точный контракт геометрии определить на первом наборе.

Показать новый и текущий наборы рядом в панели Editor. Сохранить кадры 1:1
и увеличенные фрагменты при 1×, 1,25× и 1,75× в Light и Dark на трёх бэкендах.
На исходном размере оценить узнаваемость значков и читаемость обычного,
активного и отключённого состояний. По увеличениям сверить толщину штрихов,
центрирование и края контуров. Клавиатурой проверить фокус и активацию.

<a id="pa8c2b-vector-catalog"></a>
#### PA8c.2b — общий каталог векторных иконок

Штатные символы заголовка, меню, дерева, статуса, валидации и числовых
полей рисуются из контуров 24×24. Встроенные иконки не требуют шрифта.
Пользовательские бэкенды могут использовать геометрический fallback.
Фирменный знак TrueMoon.Tales изображает Аргентис. Сохранить доступные имена,
действия и логические размеры. Проверить каталог при 12, 16 и 24 логических
пикселях в Light/Dark на raster, OpenGL и Vulkan при 1×, 1,25× и 1,75×.
[Кадры и результаты](HISTORY.md#pa8c2b-vector-catalog-2026-11-21).

<a id="pa8c3c-emoji-fallback"></a>
#### PA8c.3c — цветные эмоджи в смешанной строке

Первый сценарий проверен на Windows в Debug и published Release.
`TextBox.UseShapedText` включает общий результат раскладки для отрисовки,
высоты и каретки. [Кадры и отчёт](HISTORY.md#pa8c3c-mixed-emoji-2026-11-21).

Контрольный сценарий — `Готово ✅ · Автор 👩‍💻` в `TextBox`. Для первого
Windows-прогона кандидатом на подстановку служит системный Segoe UI Emoji.
Зафиксировать имя, версию и SHA256 основного и подставляемого шрифта в отчёте.

- **Цвет и подстановка.** Подтвердить по диагностике раскладки, что обычный
  текст использует основной шрифт, а каждый эмоджи подбирается целым
  кластером. В кадре подтвердить цвет обоих эмоджи и цельное изображение `👩‍💻`.
- **Высота строки.** Записать высоту, базовую линию и видимые границы глифов
  и сопоставить их с контрольной строкой без эмоджи. Поле показывает глифы
  целиком, а высота и базовая линия совпадают между бэкендами.
- **Каретка.** Провести её клавишами в обе стороны через оба эмоджи.
  Каждый переход через `👩‍💻` занимает один шаг, а позиции до и после
  кластера совпадают с позициями вставки из раскладки.

Выполнить сценарий при 1,25× и 1,75× на raster, OpenGL и Vulkan. Сохранить
кадры 1:1, увеличенный фрагмент и отчёт с выбранными шрифтами, метриками
строки и последовательностью позиций каретки.

### Архитектурные ориентиры

Интеграция2026-10-22: [TrueMoon.Aether.Editor](../../TrueMoon.Aether.Editor/README.md) использует
PA8a default frame/style для основного экрана, central viewport и read-only grid.
Детальные задачи и acceptance приложения принадлежат
[ENG7a](../engine/PLAN.md#eng7a-основной-экран-редактора--2026-10-22);
новые generic Grid/Tree/Docking controls этим этапом не объявлены реализованными.

- **Постоянное дерево и состояние.** Сохранить текущую модель элементов, точечных обновлений и явного lifetime. Повторное использование визуальных элементов при виртуализации требует определённых правил смены модели, подписок, выделения, фокуса и capture. Стабильные ключи коллекций проектируются отдельно от существующего BindItems с reference identity.
- **Overlays.** Сначала определить общий механизм, затем строить меню, popup, tooltip и dialog. Уточнить размещение у границ поверхности, порядок рисования относительно обычного контента, modal/input policy, закрытие и восстановление фокуса. Общий runtime должен работать в standalone и HUD; оконные возможности запрашиваются через независимые контракты.
- **Команды.** Кнопка, меню и shortcut вызывают одну операцию и используют одно состояние доступности/выполнения. Async-команды определяют cancellation и lifetime; изменения UI доставляются на owner thread. Существующие обработчики событий и bindings сохраняют явные правила владения.
- **Forms.** Определить правила conversion, validation, commit/cancel, нормализации модели и показа ошибок до расширения binding API. Async loading задаёт поведение устаревших результатов, cancellation и detach/dispose.
- **Templates и оформление.** Отделить заменяемое визуальное представление контрола от его поведения. Template сохраняет ввод, фокус, доступность и состояния; Fluent API остаётся понятным при композиции и настройке. Для animations определить scheduler, invalidation и остановку при detach/dispose.
- **Текст и accessibility.** Argentis/Alloy содержат независимые input/semantic контракты; native IME и accessibility adapters остаются в платформенной интеграции. Навигация с клавиатуры и focus policy входят в разработку новых контролов с самого начала.
- **Диагностика.** Инспектор показывает актуальные данные runtime и происхождение свойств. Определить lifetime диагностических подписок, чтобы инспектор не удерживал закрытые сессии и элементы.
- **Vulkan/HUD.** PA9 проектирует версионируемое расширение последовательного GPU-контракта: ownership каждого кадра, completion, image layout, reuse/retirement, resize и failure cleanup. Потребность и приоритет проверяются на конкретном engine host. Простое подключение standalone и существующий последовательный путь сохраняются.
- **Производительность.** PA10 опирается на замеры контрольного приложения. Кеш текста и ограничение layout/redraw выбираются по подтверждённым затратам; managed allocations, native ресурсы, GPU completion и presentation учитываются раздельно.

Текущие границы модулей, net10.0/xUnit и владение деревом/ресурсами остаются исходной архитектурой. Перечисленные ориентиры требуют уточнения контрактов и проверок при реализации; добавление раздела не утверждает готовность этих механизмов.

### Планируемые критерии первой beta

- Контрольное приложение проходит повторяемые сценарии меню/диалогов, редактирования с validation/commit/cancel, async операции и большого виртуализированного списка.
- Новые controls/overlays/commands имеют согласованные keyboard/focus/capture/disabled правила; закрытие popup/dialog возвращает фокус, detach/dispose освобождает подписки и отменяет связанную работу.
- Основные общие сценарии подтверждены на raster/OpenGL/Vulkan; переносимый набор контролов и overlays проверен в HUD с явной политикой передачи ввода игре. Фиксированные шрифты и допустимая погрешность сравнения сохраняют требования alpha.
- Инспектор объясняет layout, источники свойств и invalidation; диагностические подписки освобождаются при закрытии UI.
- Для большого списка и обычных форм есть воспроизводимые timings/allocations/resource baseline; static UI сохраняет отсутствие лишнего layout/draw. Число элементов, workload и бюджеты уточняются при подготовке PA1/PA6.
- Документированы создание приложения, новые контракты, compatibility/migration и поставка отдельному consumer. Реализация, фактически прошедшие проверки и ограничения каждого подэтапа отражаются отдельно в STATUS.

IME/accessibility, развитие trees/tables, templates/animations и асинхронный GPU-контракт имеют собственные критерии при старте подэтапов. Их порядок и включение в конкретный выпуск после первой beta уточняются по результатам контрольного приложения.

## Решения и свидетельства из восстановленных логов

Эти записи сохраняют контекст предыдущей сессии. Проверенное состояние текущего дерева находится в [STATUS.md](STATUS.md).

1. Исходный Alloy собирался, но содержал заглушки коллекций и layout; началась переработка ядра.
2. Сообщалось о доступности Vulkan и двух GPU на машине.
3. Сообщалось об отсутствии публичного API обмена Vulkan-семафорами в установленной версии SkiaSharp. Для первой рабочей интеграции была выбрана последовательная передача ресурса с ожиданием завершения GPU. Ограничение по параллельности должно быть отражено в API и документации. Это решение не снимает требований к владению ресурсами и переходам состояния изображений; ограничение API нужно проверить для фактически используемой версии перед реализацией контракта.
4. Первый прототип успешно создал Vulkan-контекст Skia и проверил цвет и прозрачность пикселей на GPU.
5. Последняя заявленная точка работы: независимые контракты Argentis и постоянное дерево элементов; затем подключение layout, ввода и рендерера.

## Поддержание плана между сессиями

Перед работой читать этот план и [STATUS.md](STATUS.md); для межмодульных задач также [общий план](../PLAN.md). После завершённого изменения обновлять дату сверки, статусы затронутых пунктов, результаты фактически выполненных проверок и следующий конкретный шаг. При изменении архитектурного решения фиксировать причину и затронутые критерии готовности. Длинные записи запусков хранить в [HISTORY.md](HISTORY.md), текущий статус оставлять кратким. Общие build/test/release вопросы обновлять в общем STATUS по [правилам](../PLANNING.md).

## ENG2: owned pixel image adapter — 2026-10-17

Для [engine CPU viewport](../engine/PLAN.md#eng2-cpu-frame-viewport--2026-10-17)
Rendering.Skia предоставляет `SkiaImageSource.CopyBgraPixels`: positive checked
shape/exact tight BGRA8 premultiplied/sRGB/top-left length, независимая owned copy.
Borrowing IImageSource/Argentis.Image contract сохранён; caller владеет disposal
после owner-thread drawing use. UI не читает mapped ring slots. Transport/readers/
process ownership и подробные ENG2 задачи ведутся только в engine плане.
[Результаты UI adapter](HISTORY.md#eng2-owned-pixel-image--2026-10-17).
Это ограниченное расширение не меняет alpha blockers или отложенный RTE backlog.

## ENG4d: retained GPU image bridge — 2026-10-20

Rendering.Skia предоставляет `SkiaOpenGLSurface.CopyTexture`: borrowed RGBA8
premultiplied stored-sRGB/top-left GL texture копируется в независимый GPU image,
last source use submitted до return/throw. Caller владеет image на исходном
surface/context owner до UI teardown; raster/foreign drawing context отвергается.
Engine transport/process ownership остаётся в Aether, без UI dependency в нём.
Подробный контракт/acceptance ведётся только в
[engine плане](../engine/PLAN.md#eng4d-gpu-viewport-bridge--2026-10-20).
Reference viewport proof не закрывает ordinary GameEditor GPU integration,
zero-copy/performance или K3/K7/K8/VRAM/final UI acceptance.


## Входящие заявки приложений — 07.11.2026

Владелец UI проверяет локальную очередь модуля `alloy` по
[правилам заявок](../requests/README.md).
Регулируемые панели поставлены локально 09.11.2026 в [UIA3e](#uia3e-split-view).
Потребитель проверяет интеграцию в своём приложении.

# TrueMoon.Nom: история исследования и проверок

<a id="core76-2026-11-10"></a>
## CORE7.6: текущая итоговая сверка — 2026-11-10

[Единственная подробная запись и команды](../core/HISTORY.md#core76-2026-11-10),
[guide](../core/CONFIGURATION_GUIDE.md).
7suites6945/6945 каждый D/R; module-specific counts и exclusions указаны в текущем
STATUS. Source solution0errors в D/R;16fresh packages одной версии,5programs×3forms,
по230checks/4owned children на source/published/fresh profile, оба DI.
Window/GPU/game/native runtime не запускаются в новых configuration consumers.
Aether full3234/3234 D/R выполнен последовательно с test-only props copy; ordinary
nested restore и прежние diagnostics reliability причины не исправлены этим proof.
Current managed public API8assemblies unchanged; source C#/csproj1673/1673 сохранены.
Дальнейшая очередь модуля и hardware/native/release gates остаются самостоятельными.


<a id="core75-2026-11-10"></a>
## CORE7.5: configured Nom admission — 2026-11-10

NomConfigurationSchema.Default/UseConfiguredNom:8startup data fields, common
profile/callback/copies → существующий host/factory/lifecycle. Nom874/874 D/R,
12newcases; настоящие stores/quota/slot return и no startup IO проверены.
Source/published/fresh16package combined consumer76checks/profile на обоих DI,
actual Nom store open/close/reopen/drain. GpuRun/WindowRun/ChildProcessRun=false.
Format/roots/IDs/ownership не менялись; crash/disk-full/VM trials не повторялись.
[Команды/ограничения](../core/HISTORY.md#core75-2026-11-10).


<a id="nom-commit-2026-11-08"></a>
## Предкоммитная проверка Nom-only scope — 2026-11-08

По запросу пользователя подготовлен отдельный commit/push изменений Nom:библиотека,
тесты, NomAssetTool/Probe, docs/nom, старые docs/assets redirects и относящиеся к модулю
solution/navigation/общие Nom записи. UI/Editor/engine implementations и их результаты
не входят в этот commit; shared documents staged отдельно от их параллельных правок.
Общий request README включён как существующая ссылка Nom PLAN, сами app claims не включаются.
Directory.Packages.props имеет только newline-only diff и остаётся вне Nom scope.

Новые фактические проверки этой подготовки:Nom.Tests862/862 каждый Debug/Release,
0failed/0skipped; старые source/published/fresh-package/solution proofs относятся к
предыдущему source-organization срезу и здесь не повторялись. C# implementations,
test bodies и package dependencies в этой подготовке не менялись. Изменён только
один historical link target ARCH1g:он ведёт на существующий engine PLAN без ещё не
закоммиченного model-import anchor. Полная реализация import research остаётся
отдельной engine работой, Nom от неё не зависит.

```powershell
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug --no-restore --logger 'trx;LogFileName=Nom-Debug.trx' --results-directory TestResults/Nom-Commit-20261108/Debug '-bl:TestResults/Nom-Commit-20261108/nom-debug-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=Nom-Release.trx' --results-directory TestResults/Nom-Commit-20261108/Release '-bl:TestResults/Nom-Commit-20261108/nom-release-{}.binlog'
```

Evidence:TestResults/Nom-Commit-20261108, исходный HEAD6dae16af25186dc3d24c91d25d006ed87709fabf,
Windows x64/.NET10. Git staged tree и отсутствие чужих путей проверяются до commit;
push направлен в origin/main без force. Новых VM/physical/native/GPU запусков нет.
Actual VM acceptance по-прежнему отложена пользователем, hardware power-off исключён.

<a id="nom-structure-2026-11-08"></a>
## Nom: функциональные каталоги и namespace — 2026-11-08

Запрос: «размести исходники Nom по соответствующим каталогав».
Выполнена [структурная реорганизация](STRUCTURE.md#nom-source-layout-2026-11-08):
60из65прежних C# файлов перенесены в Catalog16/Resources15/Retention5/Backup5/
Hosting2/Storage9/Storage.Format8; в Hosting выделен NomLifecycle, итого66файлов.
Корень содержит5общих entry/facade/open файлов. Namespace каждого файла совпадает
с путём. Все partials CatalogOwner7/NomCatalog3/NomKvStore3 сохраняют общий namespace
и лежат рядом с основным классом; functional orchestration не разделялось на новые сервисы.

Перенос публичных типов меняет fully qualified API names. Обновлены imports
существующих tests/probes/NomAssetTool и всех public consumer templates. Alpha
clients требуют пересборки с Catalog/Resources/Retention/Backup/Hosting/Storage
using; compatibility aliases не введены. UseNom и общий IProjectAssetStore остаются
в корне, options принадлежат Hosting. Package README объясняет migration. Сам формат
Nom, codec bytes/digests/revisions, public member behavior и lifecycle не менялись.
Namespace не записывается как storage data; старые roots открываются тем же core.

Текстовый audit сопоставил все65прежних implementations плюс exact lifecycle
extraction и64consumer C#/template bodies после удаления using/file-scoped namespace
и whitespace.66namespace/path pairs и согласованность partial namespaces проверены.
Это текстовое сравнение, не semantic equivalence proof или mutation testing.
Существующие assertions/test bodies не менялись, новых тестовых случаев нет.
Source freeze Verify.ps1 сделан рекурсивным с исключением bin/obj; иначе новые
подкаталоги не входили бы в frozen input set. Package PE audit проверяет ожидаемые
public namespaces, а internal KV probe обновлён на Storage. Номера строк unmanaged
negative probe теперь вычисляются из actual template; каждая операция по-прежнему
обязана получить CS8377. Dependency graph, project references, global version и
target frameworks не менялись. .codex не изменялась.

### Фактические проверки текущего запуска

Evidence:TestResults/Nom-Structure-20261108, dirty HEAD6dae16af25186dc3d24c91d25d006ed87709fabf,
Windows x64/.NET SDK10.0.403/runtime10.0.14. Build каждого SDK invocation имеет
уникальный binlog/log; public runners фиксируют source и actual DLL SHA и reap
owned processes. Предшествующие Editor/UI/native изменения сохранены; этот запуск
не подтверждает их runtime/GPU acceptance. Full solution ниже — compilation proof.

| Проверка | Результат |
| --- | --- |
| Full Nom Debug и Release |862/862 каждый,0failed/0skipped, прежние тесты с обновлёнными imports |
| Default functional probes |120groups/profile source Debug/published Release:18Core/6Kv/41Fault/27Maintenance/12Retention/16Backup, actual killed/reaped children |
| Public source consumers |App34/Buffer28/Raw31checks каждый Debug/published Release, новые namespaces |
| Fresh NuGet |4packages одной local версии,4consumers:App34/Buffer28/Raw31/Tool211checks каждый portable Debug/published Release/win-x64 FDD Release; Tool также SourceDebug9successful+2rejected processes |
| Package guards |8fresh success graphs ровно4package libraries,0ProjectReference/fallback, matching analyzer/payload/SHA/license/icon/readme; CS0122/NU1101/CS8377 на каждой raw операции и8CLI controls |
| Durability template |Compile-only Debug/published Release из fresh packages/отдельного cache; consumer не исполнялся, disk/VM не создавались |
| Full nonincremental solution Debug |0errors/1560warnings; Nom/Nom.Tests0warning occurrences |
| Source layout/body audit |66namespace/path pairs,65original implementations+NomLifecycle extraction,64consumer bodies и все partial groups прошли |

Первый запуск dotnet test остановился на CS1061:два read test files использовали
только extension method names и не получили Resources using при initial type-name
scan. Добавлен точный import в эти2файла, затем FullDebugFinal862/862 прошёл.
Это compile failure до test execution, функциональных failures не было. Первичная
попытка запуска вспомогательного Python script не выполнилась из-за недоступного
WindowsApps alias; перенос выполнен native PowerShell, пакетный код от этого не менялся.

Команды из src (PowerShell7):

```powershell
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug --no-restore --logger 'trx;LogFileName=Nom-Debug-Final.trx' --results-directory TestResults/Nom-Structure-20261108/FullDebugFinal '-bl:TestResults/Nom-Structure-20261108/nom-debug-final-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=Nom-Release.trx' --results-directory TestResults/Nom-Structure-20261108/FullRelease '-bl:TestResults/Nom-Structure-20261108/nom-release-{}.binlog'
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -Profile All -EvidenceDirectory TestResults/Nom-Structure-20261108/CoreRegression
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -EvidenceDirectory TestResults/Nom-Structure-20261108/PublicApp
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -Consumer Buffer -EvidenceDirectory TestResults/Nom-Structure-20261108/PublicBuffers
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -Consumer Raw -EvidenceDirectory TestResults/Nom-Structure-20261108/PublicRaw
& ./ManualTests/TrueMoon.Nom.Probe/VerifyPackages.ps1 -EvidenceDirectory TestResults/Nom-Structure-20261108/Packages
dotnet build TrueMoon.slnx -c Debug --no-incremental '-bl:TestResults/Nom-Structure-20261108/solution-debug-{}.binlog'
```

Повтор runner требует новый evidence path. Fresh local version:
0.2.5.1-alpha.nomstructure.20261108170856.raeb3f712; Nom nupkg SHA256:
C36D60772D98C452F90A5B96C2E1416F682CC3A3277BD306E4285814F0BAC9F9.
Внешней публикации/commit/push не было.66исходников включаются штатным SDK recursive
Compile glob; дополнительных зависимостей/ручных Compile Include не добавлено.

Исторические source links в Nom HISTORY обновлены по точной карте переноса,
prose/counts/даты/прежние namespace и команды оставлены историческими. BeforeDocs
сохраняет исходные документы до адаптации ссылок; BeforeArchive — точный STATUS
непосредственно перед новым архивированием. Полный старый Nom/general HISTORY body
проверяется с той же явной source-link migration; остальные archive texts/EOL gates
проверяются отдельно. Новые два STATUS archives дословны после адаптации source links.

Heavy capacity/performance, actual physical disk-full/VM/power-off здесь не повторялись.
Actual VM verification отложена пользователем, hardware power-off исключён. Следующий
consumer bridge — engine g.2 после g.1 по согласованию; функциональная очередь не меняется.

<a id="nom-status-before-structure-2026-11-08"></a>
## Предыдущий Nom STATUS перед реорганизацией — дословно после адаптации source links

# TrueMoon.Nom: состояние модуля хранения

**2026-11-08 — NOM8b raw T:unmanaged реализован и проверен.**
[AssetStoreRawBufferExtensions](../../TrueMoon.Nom/Resources/AssetStoreRawBufferExtensions.cs)
добавляет borrowed Memory/readonly publication, bounded Span snapshot и whole-resource
typed read с возвратом числа элементов. [Контракт](BUFFER_API.md#nom8b-raw-api),
[команды/матрица/evidence](HISTORY.md#nom8b-2026-11-08).

Checked long elements×runtime sizeof(T) выполняется до projection/allocation;
exact byte length обязана быть кратна размеру T. Non-array/non-pinnable typed owners,
slices и unused tail поддержаны. Memory borrow длится до actual terminal Task;
Copy snapshot-ит каждый вызов до Task return. Caller owner не Pin/Dispose-ится.
Entered read failure/cancel может оставить часть bytes элемента. Raw layout,
endianness и padding задаёт consumer, они не нормализуются; CLR type в каталог не
записывается. Managed references отвергаются компилятором. Byte API/format/App/DI
и зависимости сохранены; единый journal/CAS/receipt/lifecycle pipeline используется.

| Проверка текущего запуска | Результат |
| --- | --- |
| Full Nom xUnit Debug/Release |862/862 каждый,0failed/0skipped;73новых raw cases/31test methods |
| Source/published functional regression |120groups/profile D/R, actual child kills/reaps |
| Public source consumers |Raw31/Buffer28/App34checks каждый Debug/published Release |
| Fresh NuGet delivery |4packages,4consumers,8fresh success graphs; Raw31/Buffer28/App34/Tool211checks каждый portable Debug/published Release/win-x64 FDD Release |
| Compiler/delivery negatives |CS8377 на raw publication/copy/read для managed T; CS0122/NU1101 и8CLI controls; графы/payload/SHA/analyzer/license/readme прошли |
| Full nonincremental solution Debug |0errors/1560warnings; Nom/Nom.Tests0warnings |
| Bounded raw IO observation |16MiB uint, warmup+3samples/path; expected full snapshot у Copy, без performance promises; samples/limits в HISTORY |

Согласованная очередь NOM0–NOM8 завершена в описанном local IO/process scope.
Следующий consumer bridge — engine ARCH1g.2 после g.1 importer adapter, по согласованию
владельца engine; f.3/f.4/render, Editor/UI и release очереди не менялись.
Typed batch/load/portable codecs не добавлены. Другие OS/RID/AOT/trimming/external
publish и повторный heavy NOM6 capacity/performance здесь не проверялись.

Actual VM crash verification остаётся отложенной пользователем, новых trials0.
Hardware power-off исключён из acceptance. UAC disk-full5cases/166assertions D/R
и VM harness98guards/10host protocol groups D/R — прежние исторические proofs,
здесь не повторялись. [Предыдущий STATUS — дословно](HISTORY.md#nom-status-before-raw-api-2026-11-08).


<a id="nom8b-2026-11-08"></a>
## NOM8b: explicit raw T:unmanaged — 2026-11-08

Запрос: «Тогда приступай к реализации NOM8b с raw T : unmanaged.»
[Контракт](BUFFER_API.md#nom8b-raw-api) реализован в AssetStoreRawBufferExtensions:
PublishRawResourceAsync<T> для ReadOnlyMemory/Memory, PublishRawResourceCopyAsync<T>
для ReadOnlySpan/Span и ReadRawResourceAsync<T> для writable Memory; все where T:unmanaged.
Forwarding обеспечивает generic inference, arrays поддерживаются с явным T или views.
Три новых production файла:extensions, ReadOnlyRawMemoryResourceStream<T> и
RawDestinationMemory<T>. Единственное изменение прежнего production source —
PublishCoreAsync private→internal для общего owned-adapter cleanup/single-receipt guard.
Остальные61Nom C# и все39прежних test/fixture files сохранены byte-for-byte относительно
Before snapshot этой задачи. Новых test/fixture files4, public class1; interfaces,
App/DI, KV/format/codec/CAS/journal и dependency graph не расширены.

### Реализация и границы контракта

Runtime Unsafe.SizeOf<T>(), checked long elements×size до byte projection/allocation,
source≤512MiB; explicit copy ceiling положительный, выражен в bytes. Copy synchronously
snapshot-ит каждый вызов до Task return, включая replay. Borrowed raw memory не
копируется полностью: readonly stream отдаёт chunks≤64KiB, в том числе разрезающие
элемент. Committed memory replay не вызывает backing Span; caller owner не Pin/Dispose.
Single helper следует existing whole-ticket policy и проверяет committed receipt.

Typed read отвергает negative/oversized/non-divisible exact length и short capacity
до Open/изменения destination. Доступная capacity вычисляется long; только требуемые
elements выбираются до MemoryMarshal.AsBytes, так что огромный unused tail не вызывает
int byte-span overflow. Owned MemoryManager<byte> projection делегирует byte reader,
который использует bounded owned scratch; non-pinnable typed destination поддерживается.
Read возвращает число **elements**, сохраняет tail и закрывает verified stream до Task
terminal, включая empty. Entered failure/cancel может оставить bytes части элемента,
successful count не выдаётся. Cancel request не завершает borrowed lease сам по себе.

Raw не является serialization:runtime endianness/layout/nonzero padding остаются как
есть и влияют на digest/revision. Тип T автоматически в каталог не записывается;
resource kind/metadata/schema/version и deterministic bytes задаёт consumer. Bool/char
используют managed runtime size, не Marshal.SizeOf interop representation. Managed
references отвергаются компилятором. Typed load/batch не добавлялись:byte detached load
с explicit divisible-length Cast и byte/Stream heterogeneous batches сохраняются.

### Новые фактические проверки

Windows x64 Microsoft Windows10.0.26340, SDK10.0.403/runtime10.0.14;
dirty HEAD6dae16af25186dc3d24c91d25d006ed87709fabf. Evidence:TestResults/Nom8b-20261108.
Каждый SDK invocation имеет уникальный binlog и logs; public runners сохраняют
frozen inputs и actual DLL SHA, bounded owned processes/reap. .codex не изменялась.
Editor/UI локальные изменения существовали до задачи; их runtime acceptance этот
Nom запуск не подтверждает. Новый full solution ниже означает компиляцию.

| Проверка | Фактический результат |
| --- | --- |
| Narrow raw suite |73/73 Debug,31Fact/Theory methods;0failed/0skipped. Предварительный64case run тоже passed, final73закрывает дополнительные boundaries |
| Full Nom |862/862 каждый Debug и Release,0failed/0skipped;789прежних+73новых |
| Default All regression |120functional groups/profile source Debug/published Release; Core18/Kv6/Fault41/Maintenance27/Retention12/Backup16, actual killed/reaped children |
| Public source/published |Raw31checks, ByteBuffer28checks, прежний App34checks каждый Debug/Release; независимые assembly names без friend access |
| Fresh NuGet |4согласованных packages; Raw31/Buffer28/App34/Tool211checks каждый portable Debug/published Release/win-x64 FDD Release; Tool также SourceDebug9successful+2rejected processes |
| Delivery audits |8fresh success graphs/caches ровно4package libraries,0ProjectReference/fallback; packaged Cobalt analyzer, managed payload/PE/XML/license/icon/readme/hash audits |
| Compiler negatives |CS8377 отдельно на canonical raw publication/copy/read для string/managed-field struct; CS0122 для internal API; NU1101 без Nom. Ещё8CLI controls, всего11expectednegative processes |
| Full solution nonincremental Debug |0errors/1560warnings; Nom/Nom.Tests0warning occurrences. Другие warnings в этой задаче не исправлялись |

Финальные команды из src, PowerShell7:

```powershell
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug --no-restore --filter 'FullyQualifiedName~NomRawBuffer' --logger 'trx;LogFileName=Raw-Debug-Final.trx' --results-directory TestResults/Nom8b-20261108/RawDebugFinal '-bl:TestResults/Nom8b-20261108/raw-debug-final-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug --no-restore --logger 'trx;LogFileName=Nom-Debug.trx' --results-directory TestResults/Nom8b-20261108/FullDebug '-bl:TestResults/Nom8b-20261108/nom-debug-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=Nom-Release.trx' --results-directory TestResults/Nom8b-20261108/FullRelease '-bl:TestResults/Nom8b-20261108/nom-release-{}.binlog'
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -Profile All -EvidenceDirectory TestResults/Nom8b-20261108/CoreRegression
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -Consumer Raw -EvidenceDirectory TestResults/Nom8b-20261108/PublicRaw
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -Consumer Buffer -EvidenceDirectory TestResults/Nom8b-20261108/PublicBufferRegression
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -EvidenceDirectory TestResults/Nom8b-20261108/PublicAppRegression
& ./ManualTests/TrueMoon.Nom.Probe/VerifyPackages.ps1 -EvidenceDirectory TestResults/Nom8b-20261108/Packages
dotnet build TrueMoon.slnx -c Debug --no-incremental '-bl:TestResults/Nom8b-20261108/solution-debug-{}.binlog'
```

Rerun требует новые evidence directories. Fresh local version:
0.2.5.1-alpha.nom8b.20261108164026.r689f979a. Nom nupkg SHA256:
2CC289DCA2E59A13027DF4D372BCFCF0B6D2A706549D9F39BD982BCFEEA9419E.
Все89delivery commands завершены/reaped; failed compile/runtime attempts вне expected
negative controls в этой задаче не было. Пакеты не публиковались во внешний feed.

### Матрица acceptance

| Требование | Точные новые тесты/проверки |
| --- | --- |
| Runtime bytes,15partitions, четыре input variants/inference, byte-equivalent revisions |RawVariants_ProduceSameRuntimeBytesAndExactVersionAsByteStream |
| Packed3 empty/single/chunk±element и разрезанное IO |RawPackedThreeByteElements_EmptySingletonAndCrossChunkReadsRemainExact; RawAdapter_CapsByteChunksAcrossElementBoundaries_AndDisposalNeverOwnsTypedManager |
| Nonzero padding и влияние только padding на identity |RawPaddedStruct_PreservesNonzeroPaddingAndChangingOnlyPaddingChangesDigestAndRevision |
| Checked sizing до projection/access,512MiB/+1byte/int overflow |RawMemory_RejectsByteCeilingAndIntSizedProjectionOverflowBeforeAccessOrPublication; RawMemory_AdmitsExact512MiBByteCeilingWithoutReadingCommittedReplacement; RawPackedMemory_RejectsExactlyOneByteOverObjectCeilingBeforeProjection; RawCopy_RejectsIntOverflowByteProjectionBeforeAsBytesOrPublication |
| Byte copy budgets,positive min/max/empty и invalid admission |RawCopy_BudgetIsBytes_NotElementCount_AndExactBudgetIsAccepted; RawEmptyCopy_AdmitsMinimumAndMaximumPositiveByteBudgets; RawCopy_RejectsInvalidByteBudgetBeforePublication; RawPublication_RejectsMissingStoreTicketOrIdentityBeforeAccessingCallerMemory |
| Snapshot до Task return, actual lifetime success/error/cancel |RawSpanSnapshot_IsCapturedBeforeTaskReturn_AndRetainedUntilActualPublicationCompletion |
| Single/multi/wrong-ID pending/committed и copy replay admission |RawSingleHelper_RejectsWrongIdOrMultiTicketWithoutPartialPublication_AlsoOnCommittedReplay; RawCopy_CommittedReplayReturnsOriginalVersion_AndStillEnforcesByteAdmission |
| Nonpinnable typed source/slice/released owner replay |RawManagerSourceSlice_IsBorrowedWithoutPinOrDisposal_AndCommittedReplaySkipsReleasedOwner |
| Typed element count, prefix/tail и nonpinnable destination |RawRead_NonPinnableDestinationSlice_ReturnsElementsAndPreservesNeighborBytes |
| Malformed/nondivisible/short destination и large unused capacity |RawRead_RejectsMalformedByteLengthBeforeOpenOrDestinationAccess; RawRead_RejectsNondivisibleByteLengthBeforeOpenOrDestinationAccess; RawRead_RejectsOneElementShortDestinationBeforeOpenOrMutation; RawRead_HugeUnusedLogicalCapacityIsSlicedBeforeByteProjection |
| Verified empty/corrupt resources, partial element IO failures/cancel/EOF |RawRead_VerifiesEmptyAndNonemptyExactResourcesBeforeChangingDestination; RawRead_EnteredFailureOrCancellationMayLeavePartialElementBytes_ButNeverSuccess; RawRead_TruncatedStreamThrowsAndClosesAfterPartialElementCopy |
| Actual close завершает typed destination lease |RawRead_BorrowedDestinationLeaseEndsOnlyAfterActualStreamClose_OnSuccessAndFailure; RawProjection_AdmissionDoesNotAccessCallerSpan_AndDisposeInvalidatesOnlyOwnedByteView |
| Cancel-before-entry и blocked cancel/Stop удерживают root/owner |RawHelpers_CancelBeforeEntryDoesNotAccessMemoryOrStartIo; RawBlockedSource_CancelOrAppStopRetainsBorrowedOwnerAndRootUntilActualCompletion |
| Unknown commit/reopen и CleanupPending без reads released owner |RawUnknownCommit_ReopenResolvesExactOutcomeAndReplaySkipsDisposedTypedOwner; RawCleanupPending_ReplayFinishesCleanupWithoutReadingReleasedTypedOwner |
| Managed T compiler rejection |VerifyPackages raw-managed-negative:CS8377 на Program.cs lines8/9/10; не произвольная build failure |

Assertion/gap review повторно сверил byte invariants и pseudo-mutations; оставшихся
существенных unit partitions без содержательных assertions не найдено. Это статический
review, без фактического mutation run или coverage percentage. Huge metadata views
проверяют admission без giant allocations/разыменования;512MiB actual typed transfer
и повторный heavy NOM6 capacity run здесь не заявлены.

Документационный аудит первоначально остановился на старом Alloy STATUS archive:
его полный текст сохранён после CRLF→LF normalization, но не byte-for-byte. Original
snapshot SHA2567601E37CD85431C6F6721E036778C10485443FEBC71F782652C668EDA2A4DE3B
совпадает с прежним manifest. Alloy HISTORY не изменялся этой задачей; локальный audit
явно учитывает этот третий pre-existing/concurrent EOL gate наряду с general/engine,
проверяя полный normalized text. Два **новых** Nom/general STATUS archives сохранены
строго дословно. Новые local links/anchors/fences и diff --check проверяются отдельно.

### Bounded IO observation

Тот же16MiB publication workload, raw uint input:по1warmup+3samples RawMemory/
RawSpanCopy, fixed order, небольшой каталог. Source published Release median
elapsed/allocated:35.246ms/240264bytes и37.479ms/17017656bytes. Fresh NuGet portable
Release:32.106ms/240632bytes и33.308ms/17018024bytes. Точные samples в PublicRaw/
Release.json и Packages/NomRawConsumer-NuGetRelease.json. Это наблюдение конкретного
запуска, не microbenchmark или portable performance gate. Дополнительная полная
allocation у Copy соответствует явному16MiB snapshot; memory adapter не делает такую
копию. Cache/order/flush/concurrency и3samples ограничивают latency выводы.

NOM8b завершает согласованный buffer API. Дальнейший Nom consumer bridge — engine
ARCH1g.2 после g.1 importer, по согласованию владельца engine. Actual VM acceptance
отложена пользователем, новых VM trials0; hardware power-off исключён. Прежние
UAC disk-full и VM protocol proofs — история, здесь не повторялись. Другие OS/RID,
NativeAOT/trimming/self-contained и external publish по-прежнему отдельно.

<a id="nom-status-before-raw-api-2026-11-08"></a>
## Предыдущий Nom STATUS перед raw API — дословный архив

# TrueMoon.Nom: состояние модуля хранения

**2026-11-08 — NOM8a byte API реализован и проверен.**
[AssetStoreBufferExtensions](../../TrueMoon.Nom/Resources/AssetStoreBufferExtensions.cs) добавляет
borrowed Memory single/batch publication, explicit bounded Span snapshot,
whole-resource read в caller Memory и detached read-only load.
[Контракт/пример](BUFFER_API.md#nom8-buffer-api), [evidence и команды](HISTORY.md#nom8a-2026-11-08).
IProjectAssetStore/App/format/codec/dependencies сохранены; generic raw T ещё не добавлен.

Memory нельзя изменять/освобождать до actual terminal Task; cancel request недостаточно.
Caller owner не Dispose-ится. Non-array/non-pinnable source/destination и slices
поддерживаются. Memory replay не читает backing bytes; Copy snapshot-ит span до
Task return при каждом вызове, включая replay. Read сохраняет хвост destination;
entered IO error/cancel может оставить prefix. Load не удерживает store/root lease.

| Проверка текущего запуска | Результат |
| --- | --- |
| Full Nom xUnit |789/789 каждый Debug/Release,0failed/0skipped;71новый byte case |
| Source/published functional regression |120groups/profile D/R, actual child kills/reaps |
| Public source consumers |Buffer28checks и прежний App34checks каждый Debug/published Release |
| Fresh NuGet delivery |4packages; Buffer28/App34/Tool211checks каждый portable Debug/published Release/win-x64 FDD Release; graph/payload/visibility/negative audits прошли |
| Full nonincremental solution Debug |0errors/1560warnings, Nom/Nom.Tests0warnings; другие модули не исправлялись |
| Bounded IO observation |16MiB warmup+3samples/path; memory без полной входной копии, explicit Copy добавляет16MiB snapshot; latency/allocations — в HISTORY, без performance promises |

Следующий Nom этап — **NOM8b explicit unmanaged raw helpers**:checked byte sizes,
layout/endianness/padding, divisibility/type inference и typed acceptance. Engine
importer g.1/bridge g.2 и render f.3/f.4 очередь не меняется. Heavy NOM6 capacity,
другие OS/RID/AOT/trimming/external publish в этой задаче не проверялись.

Actual VM crash verification отложена пользователем, новых VM trials0. Hardware
power-off исключён из текущего acceptance. Прежние UAC disk-full5cases/166assertions
D/R и VM harness98guards/10host protocol groups D/R — исторические proofs, здесь не
повторялись. [Предыдущий STATUS — дословно](HISTORY.md#nom-status-before-byte-api-2026-11-08).


<a id="nom8a-2026-11-08"></a>
## NOM8a: byte Memory/Span API — 2026-11-08

Запрос: «Тогда приступай к реализации byte API по этому плану.»
Реализован [byte contract](BUFFER_API.md#nom8-buffer-api) в двух новых production
файлах:AssetStoreBufferExtensions и внутренний ReadOnlyMemoryResourceStream.
Borrowed single/batch ReadOnlyMemory input, explicit bounded Span copy, whole-resource
read в caller Memory и bounded detached LoadResourceAsync. IProjectAssetStore,
App/DI registrations, format/codec/CAS/journal/package dependencies не менялись.
Существующие implementers совместимы; generic raw T остаётся NOM8b.

Capture dictionary выполняется синхронно до await; overlapping immutable views
допустимы. Источники проверяются до обращения к Span (256views/512MiB each/1GiB total).
Single helper требует matching single-ticket; pending mismatches следуют прежнему
abort policy publisher, committed receipt проверяется отдельно. Часть batch не публикуется.
Read допускает non-pinnable destination: IO использует64KiB rented scratch, затем
sync Span copy. Verified stream закрывается до terminal Task; empty также verified.
Load аллоцирует после verified open и не удерживает root/store после окончания.
Caller MemoryManager/IMemoryOwner никогда не Dispose-ится adapter; views очищаются
при освобождении owned adapters. Cancel request не завершает lease занятого owner.

Уточнение draft:Span Copy копирует при **каждом** вызове до возврата Task, включая
replay. Асинхронно узнать durable outcome до синхронной snapshot copy при таком
lifetime нельзя. Publisher не читает owned snapshot на replay; borrowed memory replay
не обращается к backing Span вообще. Budget/token admission остаётся обязательной.

### Фактические новые проверки

Dirty workspace HEAD6dae16af25186dc3d24c91d25d006ed87709fabf, Windows x64,
SDK10.0.403/runtime10.0.14. Evidence:TestResults/Nom8a-20261108. Каждый SDK command
имеет уникальный binlog, stdout/log; независимые runners фиксируют input SHA,
actual loaded DLL SHA и reap owned processes. Изменения Editor/UI существовали до
Nom задачи; их проверки не выводятся из этого запуска. .codex не изменялась.

| Проверка | Фактический результат |
| --- | --- |
| Narrow NomBuffer, окончательные test sources |71/71 Debug,0failed/0skipped;36test methods,4новых test/fixture files |
| Full Nom Debug и Release |789/789 каждый,0failed/0skipped (718прежних+71новый case) |
| Default functional regression |120groups каждый source Debug/published Release; Core18/Kv6/Fault41/Maintenance27/Retention12/Backup16 |
| Source public consumers |NomBufferConsumer28checks и прежний NomPublicConsumer34checks каждый Debug/published Release, без friend access |
| Fresh NuGet |4новых согласованных packages; Buffer28/App34/Tool211checks каждый portable Debug/published Release/win-x64 FDD Release; Tool также SourceDebug9successful+2rejected processes |
| Delivery audits |6independent fresh consumer caches/graphs, ровно4package libraries, no ProjectReference/fallback; packaged Cobalt analyzer/PE/XML/LICENSE/icon/readme/hashes. CS0122 и NU1101 expected negatives,8CLI rejection controls |
| Full solution nonincremental Debug |0errors/1560warnings; Nom и Nom.Tests0warning occurrences. Остальные warnings не исправлялись в этой задаче |

Финальные команды из src (PowerShell7):

```powershell
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug --no-restore --logger 'trx;LogFileName=Nom-Debug-Final.trx' --results-directory TestResults/Nom8a-20261108/FullDebugFinal '-bl:TestResults/Nom8a-20261108/nom-debug-final-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=Nom-Release.trx' --results-directory TestResults/Nom8a-20261108/FullRelease '-bl:TestResults/Nom8a-20261108/nom-release-{}.binlog'
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -Profile All -EvidenceDirectory TestResults/Nom8a-20261108/CoreRegression
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -Consumer Buffer -EvidenceDirectory TestResults/Nom8a-20261108/PublicBuffers
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -EvidenceDirectory TestResults/Nom8a-20261108/PublicAppRegression
& ./ManualTests/TrueMoon.Nom.Probe/VerifyPackages.ps1 -EvidenceDirectory TestResults/Nom8a-20261108/Packages
dotnet build TrueMoon.slnx -c Debug --no-incremental '-bl:TestResults/Nom8a-20261108/solution-debug-{}.binlog'
```

Повтор runners требует **новые** evidence paths. Fresh local package version:
0.2.5.1-alpha.nom8a.20261108153409.re9a7c727; внешней публикации не было.
Nom nupkg SHA256:CBF49990B15E146F1B09142F7870E0AED60E16BCFE92ACB280873582527009F6.
Промежуточные focused/full logs сохранены; clean narrow proof — ByteDebugReviewed.
Первый focused build остановился на obsolete xUnit Assert.Throws(Func<Task>) overload;
для намеренно синхронных admission guards использован Action block. После исправления
71case прошли; этот compile failure не был runtime failure и не потребовал production fix.
FullDebugFinal повторён после последнего изменения
точной границы1GiB+1; предыдущий full run не является финальным source proof.

### Матрица byte acceptance

| Требование плана | Точные новые тесты |
| --- | --- |
| Stream/Memory/readonly/Span эквивалентность, empty и64KiB±1 |StreamMemoryAndSpan_IdenticalBytesProduceIdenticalExactVersions |
| Non-array/non-pinnable source, slice, caller ownership и replay |ManagerSource_SliceIsBorrowedWithoutPinningOrDisposal_AndReplayDoesNotReadReleasedOwner |
| Snapshot до Task return, release при success/error/cancel |SpanCopy_CapturesSnapshotBeforeReturningTask_DespiteImmediateSourceMutation; SpanCopy_FailureOrCancellationReleasesSnapshotOnlyAfterUnderlyingPublicationFinishes |
| Dictionary capture, overlap, whole-batch atomic failure |Batch_CapturesDescriptorsBeforeAwait_AndAllowsOverlappingReadOnlyViews; BatchStageFailure_PreservesOldWholeContentAndReleasesBothMemoryOwners |
| Single helper не публикует часть ticket и проверяет replay receipt |SingleHelper_RejectsPendingWrongIdOrMultiResourceTicket_WithoutPartialPublication; SingleHelper_RejectsMismatchedCommittedReceipt_WithoutReadingSource |
| Exact object/batch/count ceilings и1GiB+1, malformed admission до IO |MemoryAndBatch_RejectObjectOrAggregateOverflowWithoutAccessingLogicalMemory; MemoryBatch_AdmitsExactObjectAndAggregateCeilingsWithoutReadingCommittedReplacement; Batch_AdmitsExactly256DescriptorsAndReleasesAdaptersAfterCompletion; Batch_RejectsMalformedDescriptorSetsBeforePublishingOrReadingMemory; Copy_RejectsInvalidBudgetBeforePublication; Copy_RejectsOneByteOverExplicitBudgetBeforePublication |
| Non-pinnable destination, complete read, prefix/tail и short/invalid admission |Read_WholeResourceIntoNonPinnableManagerSlice_PreservesPrefixAndTail; Read_ShortDestinationRejectedBeforeOpenOrMutation; ReadAndLoad_MalformedLengthRejectedBeforeOpenOrDestinationAccess; Load_InvalidCeilingRejectedBeforeOpen; Load_OneByteOverBudgetRejectedBeforeOpen |
| Exact identity, corruption, empty verification и detached copies |ReadAndLoad_RejectNonExactIdentityWithoutChangingDestination; ReadAndLoad_VerifyEvenEmptyObjectsAndLeaveDestinationUntouchedOnOpenFailure; ReadAndLoad_EmptyVersionStillOpensAndDisposesVerifiedStream; Load_DetachedBytesSurviveReimportGcStoreAndAppDisposal |
| Partial-copy errors/cancel, no result до actual stream close |Read_EnteredFailureOrCancellationLeavesOnlyCopiedPrefix_AndClosesStream; ReadAndLoad_TruncatedCustomStreamFailsWithoutSuccessfulPartialResult_AndClosesStream; Read_DestinationLeaseEndsOnlyAfterStreamCloseCompletes_OnSuccessAndFailure; Load_ResultIsNotPublishedUntilOwnedStreamActuallyCloses |
| Cancel-before-entry/mid-IO и App Stop с retained root |CancelBeforeEntry_AllByteHelpersRejectWithoutAccessingOwnersOrStore; CancelDuringBorrowedRead_LeaseAndRootRemainUntilActualTaskCompletion_ThenTicketIsAborted; AppStop_BlockedMemoryOwnerRetainsRootAndLeaseAfterDrainTimeout_ThenCleansUpWithoutOwnerDisposal |
| UnknownOutcome/reopen и CleanupPending replay |LostCommitAcknowledgement_ReopenFindsWholeBatch_AndMemoryReplayDoesNotReadReleasedOwners; CleanupPendingReplay_CompletesCleanupWithoutReadingReleasedMemoryOwner |
| Bounded adapter и explicit copy replay admission |Adapter_ReadsBoundedChunksAndSlices_WithoutSeekingPinningOrOwningManager; Adapter_CanceledReadDoesNotTouchSourceOrDestination; SpanCopy_CommittedReplayKeepsOriginalOutcomeAndStillEnforcesCopyAdmission |

Статический pseudo-mutation/assertion review новых тестов не выявил оставшихся
существенных byte partitions без assertions. Фактическое mutation testing/coverage
collection не выполнялись и не заявляются. Входящие engine/alloy заявки не изменялись.

### Bounded allocation/IO observation

NomBufferConsumer выполняет16MiB fixed-order IO workload на малом каталоге, по одному
warmup+3samples Stream/Memory/SpanCopy, same bytes через общий publisher. В source
published Release median elapsed/allocated:Stream33.028ms/233744bytes,
Memory36.067ms/234104bytes, SpanCopy35.764ms/17011504bytes. Fresh NuGet Release:
33.919ms/234080bytes,36.638ms/234440bytes,36.291ms/17011936bytes соответственно
(округление time; точные samples сохранены в JSON). Memory в этом workload добавляет
сотни bytes адаптера, Copy — ожидаемый полный16MiB snapshot. Это наблюдение конкретного
запуска, без обещания ускорения/OS zero-copy и без нового large-catalog baseline.
Concurrency/warm cache/order/flush и небольшой sample count ограничивают latency выводы.

Heavy NOM6 capacity/performance, VM protocol/actual VM и physical disk-full здесь не
повторялись. Actual VM acceptance отложена пользователем, hardware power-off исключён;
прежний UAC disk-full proof остаётся отдельным историческим результатом. NOM8b raw
helpers — следующий Nom этап; engine f.3/f.4/g.1/g.2 и release очереди не менялись.

<a id="nom-status-before-byte-api-2026-11-08"></a>
## Предыдущий Nom STATUS перед реализацией byte API — дословный архив

# TrueMoon.Nom: состояние модуля хранения

**2026-11-08 — спроектирован NOM8 buffer API по запросу пользователя.**
[Предложение контракта](BUFFER_API.md#nom8-buffer-api) покрывает Memory/Span и readonly
варианты:byte memory borrow, explicit Span copy, bounded detached load и отдельно
unmanaged raw helpers. **Методы ещё не реализованы**, public interface/format/API/deps
не менялись, новых build/tests нет. [Решения/история](HISTORY.md#nom8-design-2026-11-08).

Следующий предлагаемый Nom этап — NOM8a byte helpers/lifecycle, затем NOM8b raw typed
helpers с explicit layout contract. Детальные подписи пока draft, acceptance/source/
publish/package checks предусмотрены до объявления готовности. VM verification остаётся
отложенной пользователем; аппаратный power-off исключён. Engine очередь не меняется.

Ниже — **предыдущие проверки2026-11-08**, не новый запуск:

| Область | Реализация | Фактические проверки |
| --- | --- | --- |
| Controller | Новая owned Gen2 VM, read-only parent+OS diff+128MiB data; no network/checkpoints; exact VM/disk identity до TurnOff | Elevated inventory0VM/0ready images; runtime controller остановлен MissingImage до любых VM mutations |
| External witness | Host CreateNew+Flush ledger, exact trial/phase/IDs/boot/PID/module/golden digests; raw data image до reboot/recovery |98pure guard cases passed; foreign witness/no-overwrite/UTF8 budgets/save-resume/process-restart rejection |
| Guest cuts/recovery |7real IO holdpoints:commit ack/CAS finalize/partial journal/checkpoint/root switch/backup/restore publication |10host protocol groups каждый Debug/published Release,7started=killed=reaped children/profile; exact state/outcomes/bytes и append liveness |
| Regression | Optional VmProtocol сохраняет default All |120functional groups/profile source/published повторены; source hashes проверены |
| Solution | Full nonincremental Debug build |0errors/1560warnings; Nom/Probe0warnings. Новые EditorHost diagnostics вне Nom scope, не исправлялись |
| Actual guest VM | TurnOff→actual Off→raw snapshot→distinct boot→normal recovery |0trials:нет подготовленного Windows Gen2 VHDX/guest credential; final exit2/Blocked, VmCrashTested=false |
| Hardware power | По прямому решению пользователя исключён |HardwarePowerLossRequired=false/HardwarePowerLossTested=false; host/device power commands отсутствуют |

Текущего запроса на подготовку VM параметров нет. Возврат к actual VM trials — после
явного возобновления пользователем, когда будет доступен подготовленный image и
локальный guest credential. Реальное выполнение по-прежнему не подтверждено.
Независимые следующие кодовые этапы выбираются у владельца engine: importer g.1
или owned render snapshots f.3; storage consumer bridge g.2 следует после g.1.

Предыдущие [UAC disk-full5cases/166assertions D/R](HISTORY.md#nom7-uac-2026-11-08)
сохранены, здесь не повторялись.718unit D/R/NuGet proof/heavy capacity/performance —
предыдущие датированные запуски, новые результаты не заявлены. Production Nom C#/API/
codec/package graph не менялись; изменения ограничены probe/controller/test artifacts.
Engine g.2 bridge и f.3→f.4/g.1 очередь сохраняется независимо от этого стенда.

Предыдущий STATUS сохранён [дословно](HISTORY.md#nom-status-before-buffer-design-2026-11-08).


<a id="nom8-design-2026-11-08"></a>
## NOM8: предложение Memory/Span API — 2026-11-08

По вопросу пользователя предусмотрен [buffer API](BUFFER_API.md#nom8-buffer-api).
Проверено адресным чтением:ResourceReadStream уже реализует Read(Span<byte>) и
ReadAsync(Memory<byte>), publication пока принимает Streams. Предлагаются extension
helpers без изменения IProjectAssetStore/его implementers и без нового lifecycle.

NOM8a:canonical borrowed ReadOnlyMemory<byte> publication, writable Memory destination,
bounded detached ReadOnlyMemory load и explicit CopyAsync для short-lived Span input.
Memory caller не изменяет/не освобождает до actual terminal Task; cancellation request
не равен completion. Readonly view не делает underlying storage immutable. Span entry
валидирует budget и синхронно snapshot-ит bytes до Task return, не удерживает ref-like
state через await. No hidden full copy/zero-copy promise, input MemoryManager не disposed.

NOM8b:explicit raw T:unmanaged, checked bytes=length×sizeof(T), divisibility и element
return counts. Layout/endianness/padding — consumer contract, unmanaged не является
portable serialization. Canonical assets используют consumer codec→bytes. Managed
graphs не cast-ятся в raw bytes. Четыре variants/generic type inference и invariant
batch dictionaries входят в acceptance. Stream semantics/hash/generation/atomicity/
UnknownOutcome/replay/CleanupPending/verified-open сохраняются.

Это design/proposal, не implemented public API:подписи draft, production code/format/
package version/dependencies не менялись. Компиляция/new tests/benchmarks не запускались,
passing для proposed overloads не заявлен. VM pause сохранён, engine f.3/f.4/g.1 очередь
не переупорядочена. Сверены primary Microsoft memory/ref-struct/MemoryMarshal contracts;
ссылки в design doc. Проверки документационных links/archives выполнены отдельно.

<a id="nom-status-before-buffer-design-2026-11-08"></a>
## Предыдущий Nom STATUS перед buffer design — дословный архив

# TrueMoon.Nom: состояние модуля хранения

**2026-11-08 — документационная актуализация: actual VM crash verification отложена
по просьбе пользователя из-за сложности подготовки image/credential.** Код стенда
сохранён; новых запусков/build/tests здесь нет. [Решение](HISTORY.md#nom-vm-deferred-2026-11-08),
[сохранённый стенд](VM_CRASH.md), [очередь](PLAN.md#nom-own-queue).
Это открытая отложенная acceptance, не блокер дальнейшей разработки Nom/engine.
Аппаратное отключение питания остаётся исключённым из текущего scope.

Ниже — **предыдущие проверки2026-11-08**, не новый запуск:

| Область | Реализация | Фактические проверки |
| --- | --- | --- |
| Controller | Новая owned Gen2 VM, read-only parent+OS diff+128MiB data; no network/checkpoints; exact VM/disk identity до TurnOff | Elevated inventory0VM/0ready images; runtime controller остановлен MissingImage до любых VM mutations |
| External witness | Host CreateNew+Flush ledger, exact trial/phase/IDs/boot/PID/module/golden digests; raw data image до reboot/recovery |98pure guard cases passed; foreign witness/no-overwrite/UTF8 budgets/save-resume/process-restart rejection |
| Guest cuts/recovery |7real IO holdpoints:commit ack/CAS finalize/partial journal/checkpoint/root switch/backup/restore publication |10host protocol groups каждый Debug/published Release,7started=killed=reaped children/profile; exact state/outcomes/bytes и append liveness |
| Regression | Optional VmProtocol сохраняет default All |120functional groups/profile source/published повторены; source hashes проверены |
| Solution | Full nonincremental Debug build |0errors/1560warnings; Nom/Probe0warnings. Новые EditorHost diagnostics вне Nom scope, не исправлялись |
| Actual guest VM | TurnOff→actual Off→raw snapshot→distinct boot→normal recovery |0trials:нет подготовленного Windows Gen2 VHDX/guest credential; final exit2/Blocked, VmCrashTested=false |
| Hardware power | По прямому решению пользователя исключён |HardwarePowerLossRequired=false/HardwarePowerLossTested=false; host/device power commands отсутствуют |

Текущего запроса на подготовку VM параметров нет. Возврат к actual VM trials — после
явного возобновления пользователем, когда будет доступен подготовленный image и
локальный guest credential. Реальное выполнение по-прежнему не подтверждено.
Независимые следующие кодовые этапы выбираются у владельца engine: importer g.1
или owned render snapshots f.3; storage consumer bridge g.2 следует после g.1.

Предыдущие [UAC disk-full5cases/166assertions D/R](HISTORY.md#nom7-uac-2026-11-08)
сохранены, здесь не повторялись.718unit D/R/NuGet proof/heavy capacity/performance —
предыдущие датированные запуски, новые результаты не заявлены. Production Nom C#/API/
codec/package graph не менялись; изменения ограничены probe/controller/test artifacts.
Engine g.2 bridge и f.3→f.4/g.1 очередь сохраняется независимо от этого стенда.

Предыдущий STATUS сохранён [дословно](HISTORY.md#nom-status-before-vm-deferred-2026-11-08).


<a id="nom-vm-deferred-2026-11-08"></a>
## Actual VM verification отложена пользователем — 2026-11-08

Из-за сложности подготовки Windows image/credential пользователь попросил пока
отложить эту проверку и обсудить другие работы. Actual VM trials сняты с активного
следующего шага, prerequisite question больше не требует ответа. Harness/source и
прежние guard/process/core/disk-full proofs сохранены. VM acceptance не выполнена
и не считается закрытой; аппаратное отключение питания по-прежнему исключено.
Возобновление actual VM run — только по новому запросу пользователя.

Это документационный шаг: новых C#/SDK/build/test/VM/native запусков нет. Проверены
ссылки/архивы/diff документации отдельно. Независимая engine очередь не менялась:
g.1 production importer после research/format policy, затем g.2 storage delivery;
f.3 owned snapshots — отдельное возможное продолжение. Детальные tasks остаются
у engine, новая реализация автоматически не назначена. [Nom plan](PLAN.md#nom-own-queue).

<a id="nom-status-before-vm-deferred-2026-11-08"></a>
## Предыдущий Nom STATUS перед отсрочкой VM — дословный архив

# TrueMoon.Nom: состояние модуля хранения

**2026-11-08 — VM crash harness реализован; аппаратное отключение питания исключено
пользователем из текущего acceptance.** [Контракт/запуск](VM_CRASH.md),
[команды/evidence](HISTORY.md#nom-vm-2026-11-08), [очередь](PLAN.md#nom-own-queue).

| Область | Реализация | Фактические проверки |
| --- | --- | --- |
| Controller | Новая owned Gen2 VM, read-only parent+OS diff+128MiB data; no network/checkpoints; exact VM/disk identity до TurnOff | Elevated inventory0VM/0ready images; runtime controller остановлен MissingImage до любых VM mutations |
| External witness | Host CreateNew+Flush ledger, exact trial/phase/IDs/boot/PID/module/golden digests; raw data image до reboot/recovery |98pure guard cases passed; foreign witness/no-overwrite/UTF8 budgets/save-resume/process-restart rejection |
| Guest cuts/recovery |7real IO holdpoints:commit ack/CAS finalize/partial journal/checkpoint/root switch/backup/restore publication |10host protocol groups каждый Debug/published Release,7started=killed=reaped children/profile; exact state/outcomes/bytes и append liveness |
| Regression | Optional VmProtocol сохраняет default All |120functional groups/profile source/published повторены; source hashes проверены |
| Solution | Full nonincremental Debug build |0errors/1560warnings; Nom/Probe0warnings. Новые EditorHost diagnostics вне Nom scope, не исправлялись |
| Actual guest VM | TurnOff→actual Off→raw snapshot→distinct boot→normal recovery |0trials:нет подготовленного Windows Gen2 VHDX/guest credential; final exit2/Blocked, VmCrashTested=false |
| Hardware power | По прямому решению пользователя исключён |HardwarePowerLossRequired=false/HardwarePowerLossTested=false; host/device power commands отсутствуют |

Следующий конкретный шаг — предоставить путь к trusted prepared Windows Gen2 VHDX
с .NET10 runtime и локально передать GuestCredential через Get-Credential; затем
запустить VerifyVmCrash.ps1 в Administrator PowerShell. Existing user VM не принимается.
Guest VM acceptance пока не закрыта; host process cuts/guards не подменяют guest reset.

Предыдущие [UAC disk-full5cases/166assertions D/R](HISTORY.md#nom7-uac-2026-11-08)
сохранены, здесь не повторялись.718unit D/R/NuGet proof/heavy capacity/performance —
предыдущие датированные запуски, новые результаты не заявлены. Production Nom C#/API/
codec/package graph не менялись; изменения ограничены probe/controller/test artifacts.
Engine g.2 bridge и f.3→f.4/g.1 очередь сохраняется независимо от этого стенда.

Предыдущий STATUS сохранён [дословно](HISTORY.md#nom-status-before-vm-2026-11-08).


<a id="nom-vm-2026-11-08"></a>
## VM crash harness, аппаратное питание исключено — 2026-11-08

Прямое решение пользователя: «Начни со стенда аварийного выключения VM. но без
физического выключения питания - это протестировать не сможем.» Hardware power-off
снят с текущего acceptance, не объявлен проверенным. Новая цель — guest TurnOff с
внешним witness/raw image/distinct boot/exact recovery. [Полный контракт](VM_CRASH.md).

Добавлены VerifyVmCrash.ps1/VmCrashProtocol.ps1/TestVmCrashGuards.ps1 и NomVmCrashProof.cs;
Program получил strict vm-child/vm-verify/vm-protocol-proof routes. Verify.ps1 получил
optional VmProtocol, default All остаётся120groups. Production Nom source/API/deps
не менялись. Guest cuts используют existing friend probe/per-factory real IO seam,
а не public fault API или injected IOException. Source/published VM profile отделён
от прежнего independent public package consumer proof.

Controller создаёт только собственную random-name Gen2 VM, standalone immutable parent
held read-only и новый diff OS disk; новый identity-checked128MiB data VHDX. No network/
checkpoints/save/restore. Host witness с точными trial/phase/IDs/boot/PID/module/golden
digests flushed CreateNew до actual Stop-VM -TurnOff. Off наблюдается отдельно, raw
data image сохраняется до reboot/recovery, boot ID обязан измениться. Expected config/
witness восстанавливаются из host ledger, не из потенциально утраченного guest signal.
Cleanup останавливает только свою VM, disks/registration Off сохраняются evidence.
GuestCredential остаётся PSCredential, не сериализуется в reports; image/credential
предоставляются caller. Аппаратных host/device shutdown/restart команд нет.

Elevated UAC inventory в
TestResults/Nom-VM-Inventory-58f40b6bf6f6447a852b10f00bd4cae4/inventory.json:
Administrator=true,VMs=[],CandidateImages=[],Error=null. Проверены registered VM и
стандартные image directories, не весь host filesystem. Готовый image path и guest
credential не предоставлены; существующие VM/диски не менялись, OS не скачивалась.

Фактические команды из src, Windows x64/SDK10.0.403/runtime10.0.14, HEAD
6dae16af25186dc3d24c91d25d006ed87709fabf + frozen dirty working tree:

```powershell
& ./ManualTests/TrueMoon.Nom.Probe/VerifyVmCrash.ps1 -EvidenceDirectory TestResults/Nom-VM-Harness-Frozen
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/Nom-VM-Core-Regression
dotnet build TrueMoon.slnx -c Debug --no-incremental '-bl:TestResults/nom-vm-solution-{}.binlog'
```

VM harness exit2/Blocked/Completed=false,Trials0/VmCrashTested=false; reason prepared
Windows Gen2 VHDX required. Owner VmId empty, vm root/data/OS disks не созданы.
HardwarePowerLossRequired=false/HardwarePowerLossTested=false. Это runtime prerequisite
gate, не failed VM durability и не отказ auto-review. До gate98guards и protocol прошли.

Pure guard tests exit0:98distinct checks, включая ForeignVmIdRejected,
MissingDataDiskRejected,ExactCommitAcknowledgementWitnessAccepted,
ProcessRestartSameBootRejected,LedgerOverwriteRejected,
Utf8ByteBudgetCannotBecomeCharacterBudget. Golden SHA old32785bytes=
021119ba13bde34cae75892dfd696e0bf831dbf9480daad64018a7570173a42c,
candidate65553bytes=adc0c3e0553ab5351cc3b29d62e00b4b7d401d549b9d3c0b6b3039749a88670a.
Fixed upperhex admission:PowerShell -notmatch case-insensitive заменён на -cnotmatch;
tests изолированно проверяют token/digest/revision casing. CreateNew/ASCII+UTF8 exact
65536/65537bytes и SHA сохранности прежнего ledger при overwrite rejection проверены.

VmProtocol:10groups каждый source Debug/published Release exit0,7started=killed=reaped
children/profile. Phases:commit-ack/cas-finalized/journal-partial/checkpoint-ready/
root-switched/backup-published/restore-published. Ещё ForeignWitnessRejectedWithoutMutation,
RegularBackupPublicationRejected,InvalidProtocolRejected. Exact golden resource bytes/
EOF, old acknowledged outcome, committed либо aborted candidate, whole generation,
compaction/restore retry и append liveness прошли. Reports явно VmCrashTested=false:
это host process regression протокола, не actual VM/hardware acceptance. Исходники SHA
не менялись во время запуска, own empty core feed/cache имеет0external packages.

Standard Core regression exit0:120functional groups каждый source Debug/published
Release, source drift0, no removed backend. Full solution nonincremental Debug exit0:
0errors/1560warnings, Nom/Probe0warnings. Против предыдущего summary1555 рост5; четыре уникальных новых CS8602
находятся в параллельном ManualTests/AetherEditorHost/EditorViewportInputSmoke.cs;
чужие файлы не исправлялись. Full build выполнен до последнего private negative-test
refinement; final Probe D/R и120groups повторены после него. Unit/full other suites/
native/GPU/fresh NuGet/Capacity/Performance здесь не запускались.

Первый VmProtocol build остановился CS1061 на неверном предположении ProjectId у
AssetWriteTicket; исправлен probe по реальному StoreId contract, production API не
менялся. Предварительные Second/First/Final host proofs сохранены; только Frozen
с10groups/98guards используется как final source set. Ни один preflight не объявлен
actual VM crash. Missing-image question запрошен асинхронно; зависимая VM mutation не
начиналась без ответа/image/credential.

Raw evidence:TestResults/Nom-VM-Harness-Frozen/{vm-crash.json,guards.json,Protocol/},
Protocol/{verification.json,Debug-VmProtocol.json,Release-VmProtocol.json,logs,binlogs};
host worker scratch paths в Protocol.RunDirectory с case/witness/recovery reports.
Core-Regression/{verification.json,profile reports,logs,binlogs};nom-vm-solution.log и
unique binlog в TestResults. Полные actual VM raw images появятся только после actual
run. Ignored evidence/TEMP сохраняются локально без automatic cleanup/retention guarantee;
перед release архивировать их с exact source snapshot. Versioned запись сохраняет
scope/commands/outcomes/limitations даже при отсутствии старых ignored files.

<a id="nom-status-before-vm-2026-11-08"></a>
## Предыдущий Nom STATUS перед VM harness — дословный архив

# TrueMoon.Nom: состояние модуля хранения

**2026-11-08 — после разрешённого пользователем UAC запуска real-volume disk-full
и recovery прошли:5cases/166assertions каждый Debug/published Release consumer.**
[Контракт/границы](DURABILITY.md), [команды/evidence](HISTORY.md#nom7-uac-2026-11-08),
[очередь](PLAN.md#nom-own-queue). Power-loss gate остаётся открытым.

| Область | Реализация | Фактическая проверка этого запуска |
| --- | --- | --- |
| UAC/isolated volume | Новый owned fixed128MiB NTFS/VHDX, exact identity/size/role checks | Administrator=true; Created/Formatted/Dismounted=true, cleanup errors0 |
| Real disk-full | Staging/catalog append/checkpoint/export/restore | Каждый из5cases в обоих profiles:free=0, real filler/storage disk-full; filler Win32112 во всех10trials |
| Recovery | Exact old IDs/head/bytes/generation/acknowledged outcome; append liveness | Generation1 сохранена, last issued ticket aborted, новая acknowledged generation2; compaction и transfer retries прошли |
| Filler ownership | Exclusive WriteThrough/DeleteOnClose handle до конца fault phase; bounded release wait | Actual removal/≥16MiB свободного места до reopen, all owner registries0/actual App release |
| Harness guards |24pure cases/12consumer assertions профиль; до16metadata Reserve/Abort | Debug/published Release compile0warnings; catalog disk-full на3attempt в каждом,166physical assertions/profile |
| Package regression | Fresh4package closure, no ProjectReference/fallback/friend access | App34checks/3NuGet profiles, tool211checks/SourceDebug+3NuGet profiles;10negative controls passed |
| Remaining gate | Physical power-loss/independent witness/cut/reboot recovery | Не выполнялся, PowerLossTested=false; итоговый exit2/DiskFullPassed-PowerLossOpen и Completed=false ожидаемы |

Fresh version `0.2.5.1-alpha.nom7.20261108134624.r95aebebb`, external publish не выполнялся.
Исправлены только assumptions harness: первая metadata write при free=0 может пройти;
filler теперь held до actual DeleteOnClose с проверкой возврата места. Production API/
C#/codec/package metadata не менялись. Промежуточные failures сохранены, не объявлены passing.

Последние718unit D/R,120functional groups/profile и solution0errors/1555warnings —
предыдущий [NOM7 delivery](HISTORY.md#nom7-2026-11-08), здесь не повторялись. NOM6 heavy
maxima/workload также не запускались; default64MiB logical index budget и measured full
domain pass сохраняют [свои ограничения](HISTORY.md#nom6-2026-11-08).

Следующий physical шаг — supervised VM/hardware cut rig с independent acknowledgement
witness. NTFS/VHDX disk-full не доказывает power-loss или universal OS/filesystem/RID
durability. Engine g.2 bridge и f.3→f.4/g.1 очередь согласуются отдельно.

Предыдущий STATUS сохранён [дословно](HISTORY.md#nom-status-before-uac-2026-11-08).


<a id="nom7-uac-2026-11-08"></a>
## NOM7: разрешённый UAC и real disk-full/recovery — 2026-11-08

По прямому запросу пользователя запрошен elevated launch через UAC. Start-Process
pwsh -Verb RunAs -WindowStyle Hidden запустил owned launcher; внутри проверен Windows
Administrator=true. Обычный sandbox escalation прежних запусков не был Windows UAC.
Новый128MiB fixed VHDX создан/подключён/NTFS formatted после exact identity guards и
после проверок отключён; CleanupFailure=null. Existing disks и host power не менялись.

Исправлены две assumptions только в PublicDurabilityConsumer.cs.template:
catalog-append теперь делает bounded≤16Reserve/Abort до настоящего metadata IO отказа
(первая операция при free=0 могла завершиться успешно); filler FileShare.None/WriteThrough/
DeleteOnClose остаётся held через fault operation. Actual removal и возврат≥16MiB
проверяются перед cleanup/reopen, максимум15s wait. Это исключает зависимость следующих
шагов от преждевременно released filler; точная причина предыдущего delayed space
availability не устанавливалась. Production Nom/API/codec/зависимости не изменены.

Final elevated command из src:

```powershell
& ./ManualTests/TrueMoon.Nom.Probe/VerifyDurability.ps1 -EvidenceDirectory TestResults/Nom7-Uac-8ff3b72f49c942ef9c4afd7cd8a30817/Physical
```

Фактический UAC launcher/command/transcript и parent PID54500 —
TestResults/Nom7-Uac-8ff3b72f49c942ef9c4afd7cd8a30817/{RunElevated.ps1,uac-launch.json,
elevated.log,elevated-exit.json}. Windows x64, SDK10.0.403/runtime10.0.14, HEAD
6dae16af25186dc3d24c91d25d006ed87709fabf + frozen dirty source. Source SHA drift0.

**Результат:5physical cases/166assertions каждый Debug и framework-dependent published
Release consumer**, оба processes exit0/Completed=true/PhysicalDiskFullTested=true.
Parent7dotnet commands exit0/reaped (restore/build/guards/publish/guards/2physical).
24pure admission cases и12guard assertions/profile повторены, new consumer warnings0.

| Сценарий | Debug/Release | Filler bytes Debug / Release | Recovery |
| --- | --- | --- | --- |
| staging |free0/OS112 |121360384 /119107584 | Issued operation aborted, old bytes/head/outcome сохранены |
| catalog-append |free0/OS112,3attempt каждый |121069568 /118816768 | Whole old content, last issued operation aborted, sequence не reset |
| checkpoint |free0/OS112 |120729600 /118476800 | Authoritative old state читается, explicit compact retry successful |
| export |free0/OS112 |120532992 /118280192 | Failed destination не опубликован, retry export/restore exact |
| restore |free0/OS112 |119701504 /117448704 | Failed destination не опубликован, typed restore retry exact |

Во всех10trials WholeOldStateAfterReopen/AcknowledgedOutcomeSurvivesDiskFull,
ExactBytesAfterDiskFull/ExactLengthAndEofAfterDiskFull прошли; acknowledged generation1
и seed receipt сохранены. LastIssuedOperationResolvedAbortedWithoutHead проверен для
выданных staging/catalog tickets; AppendLivenessAfterDiskFullRecovery затем дал
generation2. Own filler actually removed/free≥16MiB, store registry0 и App actual release.
Checkpoints/transfers не подменялись injected IO. NTFS/VHDX — реальный ограниченный
filesystem disk-full, не заполнение host device и не power-loss proof.

Parent exit2 ожидаем: ExecutionStatus=DiskFullPassed-PowerLossOpen, DiskFullStatus=Passed,
Created/Formatted/Dismounted=true, PhysicalDiskFullTested=true; PowerLossTested=false,
AllPhysicalGatesClosed=false/Completed=false. Error/cleanup error отсутствуют. Закрыт
bounded isolated-volume disk-full/recovery gate, physical power-loss остаётся открытым.

Fresh VerifyPackages prerequisite снова прошёл71commands с10expected-negative exits:
App34checks/3NuGet profiles, NomAssetTool211checks/SourceDebug+3NuGet profiles;
matching generator,4exact cached packages,0ProjectReference/fallback, source/output SHA.
Version `0.2.5.1-alpha.nom7.20261108134624.r95aebebb`, SHA256 nupkg:

| Package | SHA256 |
| --- | --- |
| TrueMoon.Contracts |A2275D38C173329B9A98C6CEBE782D1A2F3716DB09656AED494381DA40C86150 |
| TrueMoon.Cobalt |2FF623B435FF26158114ED31C1D4986981BCDCA58ECA7C2AFE5B52322711642C |
| TrueMoon |B4E1BEED883B00AC3B7234288790D50339920B68027EA0EB10133771F079FDD1 |
| TrueMoon.Nom |916CFA606DCB029102BD91BB3BAAAEA706FCD8E1402E3915FDBA6B4BACD25F09 |

Actual loaded Nom DLL SHA256 обоих physical consumers:
2C3410088DD964EF23777495C52E4DAEF3EF5CBDED9A734BA32153CB56FFCE48.
Raw evidence в указанном root/Physical:durability.json,physical-{Debug,Release}.json,
guards.json,consumer-guards-*.json,stdout/stderr/binlogs,isolated-*.vhdx и Delivery/
{verification.json,packages,App-*.json,Tool-*.json,logs,binlogs}. Detached VHDX и failed
attempt images сохраняются локально, ignored, без automatic deletion/retention
guarantee. Для release архивировать evidence с exact source snapshot; эта versioned
запись сохраняет scope/outcome/commands/hashes независимо от наличия raw files.

Промежуточные попытки не green:
619908bafa9448539cc1c0cc568b7567 — staging прошёл, первый catalog reserve не дал ожидаемый
disk-full, тест ошибочно требовал первый отказ; VHDX отключён без cleanup error.
ef8532e599ea40f4a8c5656cb3777bc9 — launcher сослался на предыдущий evidence path;
fresh-directory guard остановил запуск до нового VHDX, existing evidence не изменён.
324e1c8ecf8347c9835825310ced7cd7 — staging прошёл, следующий seed получил disk-full после
filler release; введены exclusive DeleteOnClose ownership и actual free-space admission.
VHDX отключён/cleanup0. Только final8ff3... run является passing всех5cases/profile.

Unit/full solution/native/Capacity/Performance suites не повторялись. Engine/Editor
и другие физические/release acceptance не объявлены проверенными. [Контракт](DURABILITY.md).

<a id="nom-status-before-uac-2026-11-08"></a>
## Предыдущий Nom STATUS перед UAC proof — дословный архив

# TrueMoon.Nom: состояние модуля хранения

**2026-11-08 — NOM7 physical durability follow-up: harness добавлен, physical
acceptance заблокирована окружением.** [Контракт/запуск](DURABILITY.md),
[команды и ошибка](HISTORY.md#nom7-durability-2026-11-08), [очередь](PLAN.md#nom-own-queue).

| Область | Реализация | Фактическая проверка этого запуска |
| --- | --- | --- |
| Isolated disk-full | Новый owned fixed128MiB VHDX/NTFS; real filler writes, OS112/39; staging/catalog append/checkpoint/export/restore | New-VHD запрещён Windows authorization policy; Created/MountAttempted/Formatted=false,0physical cases выполнено |
| Safety guards | Exact VHD path+ID+number/stable disk ID/size/role до initialize/format; caller-owned filler, bounded/reaped processes и detach cleanup |24pure cases passed; existing/boot/system/large/wrong-identity disks/partitions отклоняются |
| Public physical consumer | PackageReference-only, App/typed factories; exact old bytes/head/acknowledged outcome, reopen и append liveness | Debug/published Release compile0warnings; по12guard assertions. Пять real-volume cases только реализованы, пока не выполнены |
| Fail-closed report | Separate guard/physical profiles, DiskFullStatus/PowerLossTested/AllPhysicalGatesClosed | Final exit2/Blocked, Completed=false; отсутствие доступа не становится passing/skip |
| Power-loss | Матрица independent witness/cut/reboot/raw image/recovery зафиксирована | VM/hardware cut rig не предоставлен; автоматического power-cut runner нет, PowerLossTested=false |
| NOM7 package regression | Fresh4package closure,2independent consumers,0ProjectReference/fallback | Повторно App34checks/3NuGet profiles, tool211checks/SourceDebug+3NuGet profiles; CS0122/NU1101 и8CLI negative controls passed |

Final local package version `0.2.5.1-alpha.nom7.20261108121639.r8d5cc07d`, external
publish не выполнялся. Последние unit718/718 D/R,120functional groups/profile и full
solution0errors/1555warnings относятся к предыдущему [NOM7 delivery run](HISTORY.md#nom7-2026-11-08),
здесь не повторялись. Production C#/API/codec/package metadata не менялись.
NOM6 heavy maxima/workload также не запускались; default64MiB logical index budget и
measured full domain pass сохраняют свои ограничения [NOM6](HISTORY.md#nom6-2026-11-08).

Следующий конкретный шаг — запустить тот же VerifyDurability.ps1 с разрешёнными
Hyper-V volume operations в isolated environment; затем подготовить supervised cut rig.
Ни process kill, ни real disk-full, ни guest TurnOff сами по себе не доказывают потерю
host/device power. Engine g.2 bridge и f.3→f.4/g.1 порядок согласуются отдельно.

Предыдущий STATUS сохранён [дословно](HISTORY.md#nom-status-before-durability-2026-11-08).


<a id="nom7-durability-2026-11-08"></a>
## NOM7 follow-up: physical durability gates — 2026-11-08

По прямому запросу пользователя реализованы VerifyDurability.ps1, DurabilityVolume.ps1,
TestDurabilityGuards.ps1 и PublicDurabilityConsumer.cs.template. Production API/source,
package metadata и зависимости не менялись. Public consumer copied в отдельный fresh
PackageReference-only OS TEMP project, без friend access/Aether/LiteDB, с matching
4package hashes/output Nom DLL. Сначала fresh package proof, затем compile/guard checks,
только потом создание нового owned fixed128MiB VHDX. Identifier/path/number/size/role
проверяются до initialize/format; cleanup не выбирает чужой disk и не удаляет evidence.

Пять реализованных real-volume cases: staging1MiB, catalog append с48KiB metadata,
checkpoint, export и typed restore. Filler — реальные non-sparse/uncompressed writes,
OS112/39 обязателен как у filler, так и у storage operation; finally освобождает только
этот filler до cleanup/reopen. Exact IDs/head/generation/old bytes/acknowledged seed
outcome и append liveness, compaction/transfer retry проверяются после reopen. Эти
physical branches ещё не исполнялись: source compilation не считается runtime proof.
Power-loss matrix выделена отдельно, автоматического VM/hardware power-cut runner нет.
Подробные критерии и ограничения — [DURABILITY](DURABILITY.md#nom7-physical-2026-11-08).

Фактически выполнено Windows x64, SDK10.0.403/runtime10.0.14, HEAD
6dae16af25186dc3d24c91d25d006ed87709fabf + dirty frozen source:

```powershell
& ./ManualTests/TrueMoon.Nom.Probe/CheckFaultVolume.ps1 -EvidenceDirectory TestResults/Nom7-Durability-20261108/Capability
& ./ManualTests/TrueMoon.Nom.Probe/TestDurabilityGuards.ps1 -Report TestResults/Nom7-Durability-20261108/guards-first.json
& ./ManualTests/TrueMoon.Nom.Probe/VerifyDurability.ps1 -EvidenceDirectory TestResults/Nom7-Durability-20261108/Run-Frozen
```

Capability exit0 означает только сохранённую проверку доступности:64MiB New-VHD
отклонён Windows authorization, Administrator=false. Guard script exit0:24pure cases,
включая OwnedRawDiskAcceptedWithoutMutation/BootDiskRejected/SystemDiskRejected/
UniqueIdChangedRejected/ExistingPartitionsRejected/PartitionWrongDiskRejected.
Final runner exit2/ExecutionStatus=Blocked/Completed=false, DiskFullStatus=Blocked,
AllPhysicalGatesClosed=false/PhysicalDiskFullTested=false/PowerLossTested=false.
Ошибка: «У вас нет разрешения на выполнение этой задачи. Обратитесь к администратору
политики авторизации для компьютера "PH-DEV".» ErrorId
Unspecified,Microsoft.Vhd.PowerShell.Cmdlets.NewVhd. Это отказ Windows, не auto-review.
128MiB VHDX не создан, MountAttempted=false/Formatted=false, cleanup failure отсутствует.
Ни host filesystem fill, ни existing-disk format, ни reboot/VM power-cut не выполнялись.

До OS gate завершились5consumer dotnet commands exit0/reaped:restore,Debug build,
Debug guards,Release publish,Release guards. Каждый MSBuild command имеет unique binlog.
По12consumer assertions/profile (не12physical cases): DiskFull112Classified,
NestedDiskFull39Classified, OtherErrorsCannotSatisfyDiskFullGate,4unsafe root inputs
UnownedOrHostVolumeRejectedBeforeWrite и admission preconditions. Classification-only
IOException fixtures не являются physical failures. Public consumer warnings0.

Вложенный VerifyPackages повторно прошёл71commands, включая10expected-negative exits:
App34checks/3NuGet profiles; NomAssetTool211checks/9successful+2rejected processes в
SourceDebug/NuGetDebug/NuGetRelease/WinX64Release.4fresh packages,0ProjectReference/
fallback, packaged generator, CS0122/NU1101 controls; источники и SHA сверены.
Version `0.2.5.1-alpha.nom7.20261108121639.r8d5cc07d`, nupkg SHA256:

| Package | SHA256 |
| --- | --- |
| TrueMoon.Contracts |F4675FAEB8EB7935EF6111D6942889094A893441C24A55DCA1DA6B498D25D03A |
| TrueMoon.Cobalt |9B9D632F3434892E59EBD5A4EA81699D9ADBC9F89C4FE081261C3FCBF6E8E146 |
| TrueMoon |4232DD28C237C213BE7DA50B0C9473A7214494D3668FF0830F45C9886E20329C |
| TrueMoon.Nom |E79DA565299E6C7C9E1597CB8279FCEA99EDA08A9AE25614EEC6022BCA13F710 |

Raw evidence:TestResults/Nom7-Durability-20261108/Capability/volume-capability.json;
Run-Frozen/{durability.json,guards.json,consumer-guards-{Debug,Release}.json,logs,binlogs},
Run-Frozen/Delivery/{verification.json,packages,App-*.json,Tool-*.json,logs,binlogs}.
OS TEMP physical consumer path указан в durability.json. Ignored raw files сохраняются
локально без automatic deletion/retention guarantee; для release архивировать их вместе
с exact source snapshot. Versioned запись сохраняет commands/outcome/error/scope/hashes
независимо от наличия ignored files. External publish не выполнялся.

Run-First exit1 остановился до volume creation из-за повторного workspace prefix на
absolute nested Delivery path; исправлен explicit relative argument. Run-Second exit2
дошёл до New-VHD denial и скомпилировал прежний guard template; после добавления3error
classification assertions и outcome checks повторён frozen Run-Frozen. Старые попытки
не выданы за финальный passing/physical proof. После Run-Final добавлен explicit namespace NomPhysicalConsumer; package/build/guards и OS gate повторены в Run-Frozen. Final docs audit:61documents/2469valid local links,0new failures;2status archives дословно,2pre-existing EOL gates сохранены. Unit suites/full solution/native/
NOM6 heavy profiles здесь не повторялись. Никакой общий release gate не закрыт.

<a id="nom-status-before-durability-2026-11-08"></a>
## Предыдущий Nom STATUS перед physical follow-up — дословный архив

# TrueMoon.Nom: состояние модуля хранения

**2026-11-08 — NOM7 package delivery и самостоятельный NomAssetTool реализованы
и проверены в local Windows x64 scope.** [Доставка/матрица](DELIVERY.md),
[команды/артефакты](HISTORY.md#nom7-2026-11-08), [очередь](PLAN.md#nom-own-queue).

| Область | Реализация | Фактические проверки NOM7 |
| --- | --- | --- |
| Package | IsPackable=true, standalone PACKAGE.md, managed net10.0; только Contracts dependency | Четыре согласованных fresh packages; nuspec/LICENSE/icon/readme/API/PE/output audit, exact SHA в feed/cache/runtime |
| Isolation | Два независимых consumer assembly names, private feed/cache вне checkout | Portable Debug/published Release и win-x64 framework-dependent Release; ровно4package libraries,0ProjectReference/fallback; matching packaged Cobalt analyzer |
| Public App | Public UseNom/typed factories/ownership |34checks в source Debug/published Release и каждом из3NuGet profiles |
| NomAssetTool | App/constructor DI, named receipt, opaque geometry/shader; exact dependencies/pins/backup/typed restore/explicit GC |211checks/9successful processes в source Debug и каждом из3NuGet profiles; повтор collect/reopen, source frozen bytes сохранены, owners0 |
| Negative controls | Ошибки admission до App, internal API закрыт, отсутствующий пакет не подменяется |2CLI exits2/profile с unchanged source SHA/no report; CS0122 и NU1101 exits1 |
| Regression | Существующие storage API/source/tests сохранены |718/718 каждый Debug/Release,0failed/skipped;120functional groups/profile source/published, source hash drift0 |
| Solution | NomAssetTool включён в TrueMoon.slnx | Nonincremental Debug0errors/1555warnings; Nom/Tests/Probe/Tool и isolated package consumers0warnings |

Собраны локальные пакеты версии `0.2.5.1-alpha.nom7.20261108112314.r00129b91`;
внешняя публикация не выполнялась, общая версия не менялась. Source packaging использует
Roslyn compiler cache; только fresh consumer graphs имеют ровно4packages/0fallback.
AnyCPU не доказывает Linux/macOS/win-arm64, self-contained, NativeAOT или trimming.
Installed .NET10 runtime нужен для обоих framework-dependent publish профилей.

NOM0–6 semantics повторно проверены functional regression; actual maxima/workload
NOM6 здесь не повторялись, сохраняются [датированным результатом](HISTORY.md#nom6-2026-11-08).
Default64MiB logical domain index budget сохранён, opt-in≤256MiB не является RSS/free disk
reserve. Full bounded domain index pass на content commit остаётся измеренной стоимостью;
incremental preparation — отдельный follow-up при необходимости consumer.

Physical NOM3/4 isolated-volume disk-full/power-loss gates открыты, VHD denial в этом
этапе не повторялся. Process kills не закрывают physical acceptance. Следующий конкретный
consumer этап — engine ARCH1g.2 bridge после согласования владельцем; независимая очередь
engine f.3→f.4/g.1 сохранена. Другие платформы и внешняя release delivery проверяются отдельно.

Предыдущий STATUS сохранён [дословно](HISTORY.md#nom-status-before-nom7-2026-11-08).


<a id="nom7-2026-11-08"></a>
## NOM7: package delivery и самостоятельный consumer — 2026-11-08

TrueMoon.Nom IsPackable=true, PACKAGE.md/description/tags; root framework README исключён
из payload, LICENSE/icon наследуются и byte-verified. Изменений production C# API/codec/
storage semantics и dependency graph модуля нет: только TrueMoon.Contracts. Старые
36test-project files и production .cs сверены с before snapshot без изменений. NomAssetTool
добавлен в solution; остальные pending файлы Editor/ECS/UI сохранены, не входят в NOM7 proof.

VerifyPackages.ps1 строит actual projects, пакует четыре библиотеки одной уникальной local
alpha версии и создаёт отдельные внешние consumer projects/cache/feed. Source build может
использовать существующий Roslyn compiler cache; consumers — только fresh feed, без fallback
и ProjectReference. Проверены package ID/version/nuspec/readme/license/icon/lib XML,
managed PE/public surface и BCL/Contracts references, exact cached package/runtime DLL SHA,
ровно4package libraries, единственный matching Cobalt packaged analyzer. Nom nupkg9entries,
без runtime/analyzer/build/provider/test payload. Разные имена assemblies не являются friends.
CS0122 на internal NomKvStore и NU1101 при исключении Nom из feed реально воспроизведены.

Независимый headless NomAssetTool использует App/UseNom/constructor-injected typed factories.
Четыре .cs копируются без изменений в PackageReference-only project.9successive processes:
create/verify/export/restore/collect/verify-current/repeat collect/repeat verify-current/
verify original source.211checks/profile: independently computed bytes/EOF, exact current/
old revisions, source/kind/tag continuation, dependency closure/reverse, expired outcomes,
durable pin, frozen manifest, typed restore и unpin/GC/compaction/reopen. Old operation
tickets истекли до проверки liveness, pin refusal проверяется без runtime lease; source
root не содержится в receipt и остаётся unchanged после restored GC. Registry0/actual
App release требуются. Ещё2CLI rejection processes/profile exit2 до App/report, hashes0drift.

Фактически выполнено на Windows x64, SDK10.0.403/runtime10.0.14, HEAD
6dae16af25186dc3d24c91d25d006ed87709fabf + dirty working tree с frozen input SHA:

```powershell
& ./ManualTests/TrueMoon.Nom.Probe/VerifyPackages.ps1 -EvidenceDirectory TestResults/Nom7-20261108/Packages-Final
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug --no-restore --results-directory TestResults/Nom7-20261108/Tests-Debug --logger 'trx;LogFileName=nom7.trx' '-bl:TestResults/Nom7-20261108/tests-debug-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Release --no-restore --results-directory TestResults/Nom7-20261108/Tests-Release --logger 'trx;LogFileName=nom7.trx' '-bl:TestResults/Nom7-20261108/tests-release-{}.binlog'
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/Nom7-20261108/Core
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -EvidenceDirectory TestResults/Nom7-20261108/App
dotnet build TrueMoon.slnx -c Debug --no-incremental '-bl:TestResults/Nom7-20261108/solution-debug-{}.binlog'
```

Все6final commands exit0. Unit tests Passed718/Failed0/Skipped0 каждый D/R; новый
искусственный unit-test слой для pack properties не создавался. Core120functional
groups/profile, public App34checks source/published. Packages-Final71dotnet commands,
включая10expected-negative exits; все owned processes reaped. NomAssetTool211checks/9+2
processes в SourceDebug/NuGetDebug/NuGetRelease/WinX64Release; NomPublicConsumer34checks
в каждом из3NuGet profiles. Source input hash drift0; consumers без новых warnings.
Solution nonincremental Debug0errors/1555existing warnings, Nom/Tests/Probe/Tool0warnings;
full other-module suites/native/GPU не запускались. Новая solution discovery запись
относится только к standalone console, существующий test project не перевязывался.
Final audit:60production .cs,36test-project files и11probe .cs unchanged относительно
before snapshot;6consumer files совпали с frozen agent SHA. Сверены3report source sets
и4delivered package hashes, drift0. Documentation audit60documents/2395valid local
links,0new issues,2new STATUS archives дословно;2прежних general/engine EOL gates
сохранены и не выданы за потерю archive content. git diff --check прошёл после
исправления единственного добавленного CR на Nom solution-folder line.

Final local version `0.2.5.1-alpha.nom7.20261108112314.r00129b91`. SHA256 nupkg:

| Package | SHA256 |
| --- | --- |
| TrueMoon.Contracts | A7AF1AEAB19920E06D569A27ED8A43927A9F4ABF413DFEC18079EC8DE69327BE |
| TrueMoon.Cobalt | A229E1D47D6B5AB4317D5D7857C9813C9AF49D3505C5BC4ADE3D46A6C6765B6F |
| TrueMoon | A183F23319B78667ABD9561C52D8F0AE0EB12C7D0CA191898DC45559224A5996 |
| TrueMoon.Nom | D581B7EDA16A03FF5CEB270912AC3D8DC359C20AEB592F9799119E0D13675C22 |

Raw artifacts: TestResults/Nom7-20261108/Packages-Final/{verification.json,packages,logs,
binlogs,App-*.json,Tool-*.json}, Tests-{Debug,Release}/nom7.trx, Core/App verification.json
и profile reports, solution-debug.log/binlog. TEMP package run
C:\Users\MEGA6\AppData\Local\Temp\Nom-PackageDelivery-f8e7ef73179c4aff8323016f5d413a77.
Ignored raw artifacts сохраняются локально без automatic cleanup/retention guarantee;
перед release архивировать evidence вместе с exact source snapshot. Эта versioned запись
содержит scope/commands/outcome/hashes независимо от будущего наличия ignored files.

Промежуточные попытки сохранены: Packages-First отклонил BOM-prefixed nuspec (audit
исправлен TrimStart FEFF); Packages-Second остановился на download unused win-x64 runtime
packs (для FDD отключены runtime-pack/transitive downloads, installed runtime сохранён).
Packages-Third прошёл7processes/166checks, но выявил2новых CS8602 в package consumer;
Resolve guards исправлены, final211check run повторён после fix. Только Packages-Final
является текущим frozen proof, failed attempts не выданы за clean delivery.

Проверены portable net10.0 Debug, portable published Release и win-x64 published Release;
publish framework-dependent/UseAppHost=false. External feed publish, Linux/macOS/win-arm64,
self-contained/AOT/trimming не выполнялись. Physical NOM3/4 disk-full/power-loss/VHD gate
не повторялся и остаётся открыт. Heavy Capacity/Performance — результаты NOM6, не этого
запуска. Следующее расширение consumer — engine g.2 bridge по отдельному согласованию.
[Полный delivery контракт](DELIVERY.md), [единый план](PLAN.md#nom-own-queue).

<a id="nom-status-before-nom7-2026-11-08"></a>
## Предыдущий STATUS перед NOM7 — дословный архив

# TrueMoon.Nom: состояние модуля хранения

**2026-11-08 — NOM6 backup/restore, format protocol, actual limits и performance
реализованы и проверены в Windows x64 IO/process scope.** [Backup API](BACKUP.md),
[workload/числа](PERFORMANCE.md), [команды/evidence](HISTORY.md#nom6-2026-11-08),
[единственная очередь](PLAN.md#nom-own-queue).

| Область | Реализация | Фактические проверки |
| --- | --- | --- |
| Backup/restore | Immutable NOMBK001, selected metadata/all stored revision bytes; typed App restore factory; prepared hash/domain verification до publication | Exact IDs/generation/revisions/outcomes/floor/snapshots/pins/tombstones; source unchanged, normal Open backup rejected |
| Failure/ownership | No overwrite/reset, cancel sides, retained staging/actual handles; source/root/host held through entered IO |14prepubIO+4lostpuback cases, actual close/Stop gates;16kill/reap cutpoints/profile export+restore |
| Format compatibility | Explicit current1→1 transport, unknown future versions rejected, offline converter/verify/new-root protocol |Independent288bytegolden/everyprefix/everybyte/authenticated structure+domain closure corruption; implicit migration нет |
| Limits | Copied transfer quotas и DomainIndexBytes (default64MiB, opt-in≤256MiB) |Exact quotas/639–640admission; actual2mkeys/256MiBindex+checkpoint/65536flush batches и adjacent refusals source/published |
| Performance | Sorted KV page cache, reused detached values, single validated-byte revision hash |Real1k/4k/16k +50k/100k/300k, fresh/reopen/compaction p50/p95/allocations/RSS/disk;16/128MiB streams,2×512MiB/1GiBbatch |
| Regression |718/718 каждый Debug/Release и solution-filter,0failed/skipped |90newcases/31methods,29прежних .cs tests unchanged; Nom/Tests0warnings |
| Source/published/App |120functional+4Capacity groups/profile; public no-friend App34checks/profile |Empty core feed/cache0packages, no LiteDB graphs/output; frozen source hashes drift0 |
| Solution | Nonincremental Debug build |0errors/1555warnings, Nom/Tests/Probe0warnings; full suites/engine/GPU не заявлены |

Следующий этап — **NOM7: fresh NuGet delivery, второй независимый consumer и supported
OS/RID scope**; IsPackable=false. Physical disk-full/power-loss NOM3/4 остаются открыты
(последний New-VHD denial в NOM4; в NOM6 не повторялся). Workload numbers не являются
latency/RSS SLA или LiteDB comparison; OS cache uncontrolled, empirical p95=max на малом
sample set. Full bounded domain index pass при content commit остаётся измеренной
стоимостью; incremental preparation — возможный отдельный follow-up по consumer.
Большой corpus требует explicit256MiB logical index budget; default64MiB сохранён.
Logical index/backup quotas не равны RSS/free disk reserve. Failed backup staging
сохраняется для диагностики, automatic recursive cleanup/format migration нет.

Предыдущий STATUS сохранён [дословно](HISTORY.md#nom-status-before-nom6-2026-11-08).

<a id="nom4-2026-11-08"></a>
## NOM4: checkpoint, explicit rotation и offline compaction — 2026-11-08

Реализован streaming NOMCKP01 codec: independent header/records/footer/whole-file
digest, strict IDs/epoch/counters/anchor, sorted unique opaque keys, bounded values/
index, no entire-checkpoint buffer. Authoritative Open использует root-selected
checkpoint + exact successor/sealed chain, missing/corrupt checkpoint отвергается
до repair/cleanup даже при наличии old file set. Tombstones не воскресают; empty
checkpoint при generation0 допустим. Старое ожидание NotSupported для отсутствующего
checkpoint заменено на IOException с сохранением всех no-mutation/handle assertions.

Explicit IProjectAssetStore.CompactAsync сохраняет content generation, resource
revisions/CAS/retained operations/control floor. KV physical tickets covered by cp
получают явный Expired. Prepared checkpoint/segment/root flush/close/read verified до
same-directory root rename. Owner lock удерживается через root-handle close/switch.
Old handles/files освобождаются только после selected root; cleanup failure возвращает
CleanupPending и повторяется без повышения epoch. Failed prep/switch требует reopen;
NomCompactionException сохраняет cause и RootPublicationAttempted. До switch cancel
не выбирает новый root, после switch успех не становится cancelled. Offline API
отклоняет активные reservations/read leases. DI/lifetime/публичный bootstrap прежние.
[Контракт, wire layout, API assumptions и ограничения](COMPACTION.md#nom4-compaction-2026-11-08).

Проверки Windows x64, SDK10.0.403/runtime10.0.14, xUnit2/VSTest:

- **536/536 каждый Debug/Release и solution-filter**, failed/skipped0.110new cases
  над NOM3 baseline426,30methods. Independent pinned193-byte golden/footer/wholeSHA,
  authenticated malformed fields/order/length/budgets,64KiB IO/128KiB value, checkpoint
  seeds/sealed→active replay, deletes/empty values,30prep/switch fault cases, three
  owned-file corruptions после flush до verify, byte/file-slot equality/one-short,
  cancelled prep/post-switch, actual close/delete retry, no-reset/no-fallback.
  Public test подтверждает два old/current CAS versions, journal/floor retention,
  active reader/reservation rejection и один App stop при вошедшей compaction IO.
- **Maintenance27groups source Debug/published Release**:24started=killed=reaped
  children/profile на checkpoint/segment/root writes/flush/move/close/delete и3semantic
  groups. Exact selected old/new epoch, root lock до kill, reopen/retry/cleanup,
  post-cp append/second epoch, deleted keys не воскресают, public journal/CAS сохраняются.
- Повторены **Core18+KV6+Fault41**, вместе **92groups/profile**. Fault48children/profile
  и прежние KV6started=reaped/4kills сохраняют свой scope; не выдаются за power loss.
- Independent public no-friend App consumer **16checks/profile** source/published:
  предыдущие10 плюс CompactAsync/read-lease refusal, retained public ticket, idempotent
  repeat, checkpoint reopen exact2MiB+17bytes и post-cp reserve/abort. Actual project
  references; не fresh NuGet proof. Core graph0external packages, LiteDB отсутствует.
- Nonincremental solution Debug **0errors/1555warnings**, Nom/Tests/Probe0warnings.
  Прежние UI/engine results не пересчитываются; full suite/GPU/performance не запускались.

```powershell
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug --logger 'trx;LogFileName=debug-frozen.trx' --results-directory TestResults/Nom4-20261108 '-bl:TestResults/Nom4-20261108/Debug-Frozen-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Release --logger 'trx;LogFileName=release-frozen.trx' --results-directory TestResults/Nom4-20261108 '-bl:TestResults/Nom4-20261108/Release-Frozen-{}.binlog'
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/Nom4-20261108/Probe-Final
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -EvidenceDirectory TestResults/Nom4-20261108/PublicApp-Final
dotnet build TrueMoon.slnx -c Debug --no-incremental -m '-bl:TestResults/Nom4-20261108/Solution-Frozen-{}.binlog'
dotnet test TrueMoon.slnx -c Debug --no-build --filter 'FullyQualifiedName~TrueMoon.Nom.Tests' --logger 'trx;LogFilePrefix=nom-discovery' --results-directory TestResults/Nom4-20261108/DiscoveryFrozen '-bl:TestResults/Nom4-20261108/Discovery-Frozen-{}.binlog'
```

Ignored raw evidence: TestResults/Nom4-20261108/{debug-frozen.trx,release-frozen.trx,
Probe-Final,PublicApp-Final,DiscoveryFrozen,VolumeGate}, matching logs/binlogs и
final-audit.json. Предварительные codec48, full529/535 runs предшествуют final536;
единственный новый xUnit2031 warning в first529 исправлен predicate Assert.Single.
Последний отдельный stop test добавлен до final536; original tests не ослаблены.
Research/plan/assertion-gap review вне checkout. Единственная parse-only static pairing
попытка заблокирована NuGet.Config ACL; это discovery limitation, не успешный scan,
не failure target tests и не основание выдумывать pairing counts.

Physical NOM3/NOM4 gate повторён: New-VHD64MiB снова отказан Windows authorization,
Administrator=false; file/volume не создан/не подключён/не форматирован. Это OS gate,
не auto-review rejection. Injected disk-full/process kill не закрывают physical
volume/power-loss/RID acceptance. Explicit compaction готова в проверенном scope;
automatic scheduling нет. Checkpoint≤256MiB/maintenance output budgets, metadata-dir
scan≤1024 и reserved2file slots enforced. Live data, превышающие checkpoint budget,
дают явный отказ до preparation, не скрытую переработку данных. Heap/RSS — NOM6.
Следующий implementation этап NOM5: domain indexes, snapshots/pins/offline GC;
physical gate остаётся открытым независимо. NuGet delivery — NOM7, IsPackable=false.

<a id="nom-status-before-nom4-2026-11-08"></a>
## Предыдущий STATUS перед NOM4 — дословно

# TrueMoon.Nom: состояние модуля хранения

**2026-11-08 — NOM3 проверен в deterministic IO/process scope; physical gate открыт.**
Собственный App/KV/CAS backend сохранён, LiteDB отсутствует.
[Матрица и исправления](RECOVERY_ACCEPTANCE.md#nom3-recovery-2026-11-08),
[команды/evidence](HISTORY.md#nom3-2026-11-08), [очередь](PLAN.md#nom-own-queue).

| Область | Реализация | Фактические проверки |
| --- | --- | --- |
| Fault/recovery | Allocation/pending/commit/abort/prune/recovery, zero/partial/full write, flush/lost ack, actual close/retry |426/426 каждый Debug/Release и solution-filter,0failed/skipped;44new cases над NOM2 baseline382 |
| Capacity | До allocation зарезервирован worst-case budget для3frames и index; per-batch payload/mutations/key/value ceilings |8one-short refusals до IO/Next, exact abort/commit; owner/root не удерживаются после admission отказа |
| Lifecycle fixes | Poisoned pending read → staging-only stop/reopen abort; stop-during-Reserving → control-slot handoff |Один stop завершает allocation/pending race; ambiguous owner не append-ит; combined causes и actual handles проверены |
| Process proof | Domain/tail/Create cuts с настоящими pending operations и no-reset checks |Fault41groups каждый source Debug/published Release,48started=killed=reaped children/profile |
| Regression/App | Core18+KV6 и public no-friend App consumer повторены |65console groups/profile вместе с Fault41; App10checks/profile, exact2MiB+17byte roundtrip; core graph0external packages |
| Общая сборка | Nonincremental solution Debug |0errors/1555warnings; Nom/Tests/Probe0warnings. Другие модули не объявлены протестированными |
| Physical disk-full | Safe64MiB VHDX creation capability runner |Windows authorization отказ, Administrator=false; file/volume не создан/не подключён/не форматирован. PhysicalDiskFullTested=false |

**Полный NOM3 остаётся частичным:** нужны реальные isolated-volume disk-full в
staging и catalog append, controlled filler cleanup, close/reopen/exact outcomes
в разрешённом Windows окружении. OS permission gate не является auto-review отказом.
Injected IOException/HRESULT и process kill не подменяют physical disk-full/power-loss.

Следующий открытый acceptance шаг — physical NOM3; следующий implementation этап —
NOM4 checkpoint/rotation/offline compaction. Segment64MiB/65536frames остаётся явным
capacity refusal, automatic maintenance ещё нет. Snapshots/GC — NOM5, performance —
NOM6, fresh NuGet/RID matrix — NOM7, IsPackable=false. Old roots/revisions не мигрируются.
ReadAsync≤256/1MiB; history≤4096, prune bounded/atomic; OpenResource полностью проверяет
hash того же handle до возврата. Full suite/GPU/performance/fresh packages не запускались.
Прежние Aether failures и physical/reparse gates не закрыты; engine очередь сохранена.

Предыдущий STATUS сохранён [дословно](HISTORY.md#nom-status-before-nom3-2026-11-08).

<a id="nom3-2026-11-08"></a>
## NOM3: expanded faults/recovery, physical gate открыт — 2026-11-08

Проверен собственный backend, не прежний vendor path. Три адресных исправления:
reservation заранее оставляет capacity для allocation/pending/publication-or-abort;
poisoned read больше не удерживает idle reservation/root бесконечно (stop чистит owned
staging без append, reopen abort-ит pending); stop во время Reserving передаётся в
reserved cleanup slot после entered IO и завершается с первой попытки.
Public API/DI/options/hash format не менялись. Internal StorageLimits и typed
NomKvAppendBudget позволяют проверять реальные малые quotas без public fault options.
[Контракт/матрица/границы](RECOVERY_ACCEPTANCE.md#nom3-recovery-2026-11-08).

Фактические результаты Windows x64, SDK10.0.403/runtime10.0.14, xUnit2/VSTest:

- **426/426 каждый Debug/Release и solution-filter**, failed/skipped0.44new cases:
 26allocation/pending/abort/prune/recovery/poisoned-read и18capacity/lifecycle.
  Zero/partial100byte/full writes, flush before/after, exact tickets/sequence gaps,
  old bytes/generations, cancellation/lost flush ack, both cleanup causes и actual
  handles/stop retry. Eight quotas exact/one-short; admitted reservation всегда может
  abort/commit, отказ не изменяет Next и не оставляет active resource user.
- Новый **Fault41groups каждый source Debug/published Release**:24domain cuts,
 3tail-repair и14Create cuts. **48started=killed=reaped children/profile**, включая
 7подготовительных настоящих pending operations. Live lock witness, bounded protocol,
  raw prefix до recovery, exact resource bytes/outcome/floor после reopen, no-reset.
- Прежние **Core18+KV6groups/profile** повторены на итоговом коде. Default All теперь
 65groups/profile. Public no-friend App consumer10checks/profile D/R повторён.
  Isolated core restore с пустым feed/cache:0external packages; loaded/runtime/output
  graphs без LiteDB. App graph включает свои Roslyn build packages.
- Nonincremental solution Debug build: **0errors/1555warnings**, Nom/Tests/Probe0warnings.
  Фильтр solution выполняет только Nom; остальные assemblies не объявлены проверенными.

```powershell
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug --logger 'trx;LogFileName=debug-final.trx' --results-directory TestResults/Nom3-20261108 '-bl:TestResults/Nom3-20261108/Debug-Final-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Release --logger 'trx;LogFileName=release-final.trx' --results-directory TestResults/Nom3-20261108 '-bl:TestResults/Nom3-20261108/Release-Final-{}.binlog'
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/Nom3-20261108/Probe-Final
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -EvidenceDirectory TestResults/Nom3-20261108/PublicApp-Final
& ./ManualTests/TrueMoon.Nom.Probe/CheckFaultVolume.ps1 -EvidenceDirectory TestResults/Nom3-20261108/PhysicalGate
dotnet build TrueMoon.slnx -c Debug --no-incremental -m '-bl:TestResults/Nom3-20261108/Solution-Final-{}.binlog'
dotnet test TrueMoon.slnx -c Debug --no-build --filter 'FullyQualifiedName~TrueMoon.Nom.Tests' --logger 'trx;LogFilePrefix=nom-discovery' --results-directory TestResults/Nom3-20261108/Discovery '-bl:TestResults/Nom3-20261108/Discovery-Final-{}.binlog'
```

Ignored evidence: TestResults/Nom3-20261108/{debug-final.trx,release-final.trx,
Discovery,Probe-Final,PublicApp-Final,PhysicalGate}, matching logs/binlogs,
final-audit.json. Baseline382passed после initial fixes; первый new-only run43/44
имел только слишком узкое Throws<OperationCanceledException> вместо разрешённого
TaskCanceledException subtype. Исправлено на ThrowsAny с проверкой cancelled token
и durable aborted outcome. Финальные426passed после исправления; skips нет.
Test research/plan/quality review вне checkout; исходные14test files побайтно сохранены,
из прежних файлов адресно расширен согласованный NomFaultFixtures.

**Physical gate:** свежий New-VHD -Dynamic -SizeBytes67108864 отклонён Windows
authorization policy, Administrator=false. File не создан, том не подключался и не
форматировался. Это OS permission gate, не auto-review rejection. Injected HRESULT
0x80070070 и killed children не подтверждают real volume disk-full/power loss.
CheckFaultVolume.ps1 воспроизводит только safe creation capability check; даже его
будущий success не считается physical fill/recovery acceptance.

NOM3 закрыт только в deterministic IO/process scope, полный этап остаётся частичным
до isolated-volume staging/catalog disk-full/reopen proof в разрешённом окружении.
Сохраняются power-loss/reparse/RID gates. Maintenance/rotation/checkpoint — NOM4,
snapshots/GC — NOM5, performance — NOM6, NuGet delivery — NOM7. Full suite/GPU/fresh
packages/performance не запускались; чужие Aether/UI результаты не изменены.

<a id="nom-status-before-nom3-2026-11-08"></a>
## Предыдущий STATUS перед NOM3 — дословно

# TrueMoon.Nom: состояние модуля хранения

**2026-11-08 — NOM2 реализован и проверен:** UseNom/App и resource catalog
переключены на собственный KV, LiteDB/BSON implementation/зависимости/vendor runners
удалены. [Контракты и границы](CATALOG.md#nom2-catalog-2026-11-08),
[команды/evidence](HISTORY.md#nom2-2026-11-08), [очередь](PLAN.md#nom-own-queue).

| Область | Реализация | Фактические проверки |
| --- | --- | --- |
| Каталог | Typed overlay → atomic KV batch; mandatory header/control, bindings/revisions/heads/journal, новые canonical records/hash domains |382/382 Nom.Tests каждый Debug/Release и solution-filter,0failed/skipped;64new cases +2net replacement cases |
| Recovery | Вся chain/domain validation до tail repair и staging cleanup |24authenticated logical corruptions с100byte tail и sentinel: zero mutation; future generation/declared total/request/closure/head integrity |
| Streams/CAS/App | Public API,64KiB buffers, borrowed inputs/owned leases, durable outcomes, stop/drain/actual close retry сохранены |18core groups каждый source Debug/published Release,32MiB payload,4resource kill cutpoints/profile; public no-friend App consumer10checks/profile, exact2MiB+17bytes |
| KV regression | NOM0/1 плюс bounded ReadPage и deferred CompleteRecovery |6KV groups/profile,6started=reaped children,4actual kills/profile; whole old/new state и exact outcome |
| Удаление зависимости | Package/CPM, BSON/engine wrappers, WAL/download runners удалены; legacy root только отклоняется | Actual isolated core graph0external packages, public App restored/runtime graphs и fresh outputs без LiteDB; source hashes drift0 |
| Общая сборка | Nonincremental solution Debug |0errors/1555warnings, Nom/Tests/Probe warnings0; другие модули не объявлены протестированными |

NOM2 готов в указанном scope. Старые LiteDB roots/AST revisions не мигрируются и не
сбрасываются. Existing public names/ownership сохранены, persisted metadata/hash format
новый. Publication учитывает4MiB whole-KV ceiling до admission; ReadAsync≤256/1MiB,
operation history≤4096. Prune может атомарно отказать по batch budget — caller уменьшает
range. Segment64MiB/65536frames, automatic rotation/checkpoint/compaction ещё нет.
OpenResourceAsync проверяет полный hash того же handle до возврата; progressive read
и hostile/reparse filesystem guarantees не добавлены.

**Следующий шаг NOM3:** integrated fault/recovery/lifecycle matrix для allocation,
pending/commit/abort/prune/recovery IO, capacity и cleanup retry; physical disk-full
отдельно. Maintenance — NOM4, snapshots/GC — NOM5, performance — NOM6, fresh NuGet/RID
delivery — NOM7. IsPackable=false. Physical disk-full/power-loss/symlink gates открыты.
Full suite/GPU/performance здесь не запускались; прежние Aether failures не закрыты,
vendor defect не объявлен исправленным. Engine f.3→f.4/importer g.1 не переупорядочены.

Прежний STATUS сохранён [дословно](HISTORY.md#nom-status-before-nom2-2026-11-08).

<a id="nom2-2026-11-08"></a>
## NOM2: App/resource catalog cutover и удаление LiteDB — 2026-11-08

UseNom/public factory/store сохраняют API и ownership, но теперь используют собственный
KV. NomCatalog — typed transaction overlay, один KV batch для logical publication.
Own binary header/control/asset/binding/revision/head/operation/item codecs заменили
BSON; NOM0 metadata/revision domains используются в streams. IDs/bytes отделены от
revision/ticket domains старого backend: implicit migration нет. Mandatory control,
key/value relationships, heads/current metadata, journal requests/closure/outputs,
generation и declared totals проверяются до tail truncate/flush и staging cleanup.
ReadPage bounded/detached; CompleteRecovery откладывает repair до domain validation.
High-water reservation отдельно durable; unknown write/flush требует reopen/lookup.

Удалены NomCatalogConnection/vendor stream wrappers, LiteDB reference/central version,
WalFaultProof/VerifyWalFaults и pinned downloads. NomFileOperations наследует собственный
KV IO seam. Dedicated worker, queue/control slot, typed DI/factory, App registry,
borrowed inputs/owned read leases и actual close/retry перенесены. Удалены2старых
LiteDB.dll только из проверенных принадлежащих модулю bin paths, с exact SHA; historical
evidence и global NuGet cache не удалялись. Новые graphs/outputs не содержат зависимости.

Финальные проверки Windows x64, SDK10.0.403/runtime10.0.14, xUnit2/VSTest:

- Nom.Tests **382/382 каждый Debug/Release**, failed/skipped0.64new catalog/cutover
  cases +2net cases при замене vendor close tests: baseline316→382. Перенесены прежние
  App/fault сценарии; skips нет.24authenticated logical corruptions сохраняют полный
  root hash,100byte incomplete tail и staged sentinel, не вызывают repair/cleanup IO.
- Solution-filter Debug обнаружил и выполнил те же382/382; остальные assemblies
  не совпали с фильтром и не считаются проверенными.
- Core **18groups каждый source Debug/published Release**: перенесены semantic identity,
  metadata/queue/ownership/CAS/streaming proofs,32MiB bounded stream и4resource kill
  cutpoints/profile. Separate KV **6groups/profile**,6started=reaped children/profile,
 4actual kills/profile. Partial17byte tail=old state, after-flush=new, exact outcomes.
- Independent NomPublicConsumer (не friend) **10checks каждый source Debug/published
  Release**: public App/UseNom/typed factory,2MiB+17byte exact resource roundtrip/reopen,
  journal, early product release и host fallback drain. Actual ProjectReferences;
  не fresh NuGet delivery. Core restore с новым empty feed/cache содержит0external
  packages; App graph содержит Roslyn build packages, LiteDB отсутствует в обоих.
- Nonincremental TrueMoon.slnx Debug build: **0errors/1555warnings**. Nom/Tests/Probe
  warnings0. Full suite, GPU, performance, fresh packages и physical gates не запускались.

Финальные команды:

```powershell
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug --logger 'trx;LogFileName=debug-frozen.trx' --results-directory TestResults/Nom2-20261108 '-bl:TestResults/Nom2-20261108/Debug-Frozen-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Release --logger 'trx;LogFileName=release-frozen.trx' --results-directory TestResults/Nom2-20261108 '-bl:TestResults/Nom2-20261108/Release-Frozen-{}.binlog'
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/Nom2-20261108/Probe-Final
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -EvidenceDirectory TestResults/Nom2-20261108/PublicApp-Frozen
dotnet build TrueMoon.slnx -c Debug --no-incremental -m '-bl:TestResults/Nom2-20261108/Solution-Frozen-{}.binlog'
dotnet test TrueMoon.slnx -c Debug --no-build --filter 'FullyQualifiedName~TrueMoon.Nom.Tests' --logger 'trx;LogFilePrefix=nom-discovery' --results-directory TestResults/Nom2-20261108/Discovery '-bl:TestResults/Nom2-20261108/Discovery-Frozen-{}.binlog'
```

Raw ignored evidence: TestResults/Nom2-20261108/{debug-frozen.trx,release-frozen.trx,
Discovery,Probe-Final,PublicApp-Frozen}, matching logs/binlogs и final-audit.json.
Probe-Final повторён после исправления старой Scope строки в KV report; source hashes
сверены. Предварительные379/380case runs, missing-root exception mismatch, public
consumer namespace/count/shared-root harness failures сохранены в evidence; финальные
профили прошли после исправлений. Старый vendor defect не исправлялся: backend заменён.

В [CATALOG](CATALOG.md#nom2-catalog-2026-11-08) закреплены schemas, hash compatibility,
preflight physical payload183+Σ(3L+567)≤4MiB, bounded prune и ограничения. Последняя
достижимая граница4194303 реально опубликована;4194306 отклоняется до admission/IO.
Segment64MiB/65536frames без automatic rotation; maintenance — NOM4. Следующий NOM3:
полная integrated IO/recovery/reservation/abort/prune/cleanup/capacity fault matrix,
physical disk-full отдельно. Snapshots/GC/performance/delivery — NOM5–7. Process kill
не является power-loss proof; engine f.3→f.4/importer g.1 и чужие UI changes сохранены.

<a id="nom-status-before-nom2-2026-11-08"></a>
## Предыдущий STATUS перед NOM2 — дословно

# TrueMoon.Nom: состояние модуля хранения

**2026-11-07 — NOM1: рабочий low-level KV core реализован и проверен.**
[API/ownership/команда](KV_CORE.md#nom1-kv-2026-11-07), [формат](FORMAT.md),
[история](HISTORY.md#nom1-2026-11-07), [очередь](PLAN.md#nom-own-queue).

| Область | Реализация | Фактические проверки |
| --- | --- | --- |
| Create/Open | Root authority последним, exact IDs, exclusive owner, root-listed chain и controlled tail repair | Create/new+empty, legacy/foreign/corrupt/no-reset snapshots, missing files/lock, checkpoint refusal, sealed validation до repair |
| Get/WriteBatch | Detached Get/GetMany view, canonical atomic batch, expected sequence/generation, bounded prepared index, explicit durable flush/Unknown ticket | Exact values/empty Put/Delete/metadata generation, inputs/results copies, quotas pre-IO, physical ticket lookup/reopen; GetMany IO failure без partial result |
| Владение | Factory registry≤4, data/root shares и root lock, close retry, stop всех до drain | Actual handles, raw OS write-denial root/active/sealed, pre/post-close faults, failed create+cleanup causes, entered writer/drain concurrency |
| Unit regression |316/316 каждый Debug/Release и solution-filter Debug, failed/skipped0 |83new KV cases +233existing; Nom warnings0 |
| Console/process proof |6groups каждый source Debug/published Release;6started=reaped children/profile,4kill cutpoints/profile | Old/new whole state, after-flush new, partial17byte tail repair, exact outcome и cross-process root contention/release;48input hashes drift0 |
| Общая сборка | Nonincremental solution Debug0errors/1555warnings | Nom warnings0;2Skia warnings из прошлого snapshot исчезли в чужой параллельной работе, не fix Nom |
| App/поставка | Собственный KV внутренний; UseNom/resource API всё ещё LiteDB | Public API/dependencies/solution не менялись этим срезом. Full App/catalog cutover и удаление LiteDB — следующий NOM2 |

NOM1 готов в своём scope. Синхронные операции сериализованы; cancellation ожидания
Monitor/зависшего filesystem call не обещает принудительного прерывания. GetMany даёт
consistent view; single diagnostic Sequence/Generation properties не atomic pair.
Create failures сохраняют uninitialized files, auto-reset/retry-create в nonempty root нет.
No automatic segment rotation/compaction: capacity refusal оставляет owner usable;
maintenance/root switch/checkpoint restore — NOM4. Heap/RSS/SLA измерения — NOM6.

Следующий этап NOM2: перевести resource catalog/journal/CAS adapter и UseNom lifecycle
на этот core, заменить BSON codecs и полностью удалить LiteDB из source/tests/probes/
packages/output. NOM3 затем проверяет full integrated fault matrix, NOM7 — fresh packages.
Physical disk-full/power-loss/symlink gates не сняты. Full suite/GPU не запускались,
исторические Aether failures и vendor defect не объявлены исправленными. Engine f.3→f.4
и importer g.1 не переупорядочены.


<a id="nom1-2026-11-07"></a>
## NOM1: рабочий KV writer и filesystem recovery — 2026-11-07

NomKvStoreFactory создаёт/открывает caller-disposable NomKvStore: exact Get/GetMany,
atomic WriteBatch, expected sequence/generation, prepared index capacity, durable
Flush(true) acknowledgement и exact physical ticket LookupOutcome. Write/flush error
poisons owner и сохраняет cause/ticket, reopen разрешает committed/not committed.
Create публикует nom.root последним; legacy/foreign/uninitialized roots не сбрасываются.
Open проверяет всю root-listed sealed chain до flush/truncate valid incomplete active tail.
Root/data handles исключают ordinary external writer; live owner registry/capacity и
root lock удерживаются при close failure до actual cleanup. Factory stop закрывает
admission всех owners до ожидания первого. Новые internal helpers не регистрируются
как отдельный public API; полный UseNom/resource adapter и удаление LiteDB — NOM2.

Финальные Nom.Tests316/316 каждый Debug/Release и solution-filter Debug, failed/skipped0:
83new KV cases +233existing. Exact roots/header goldens, key/value snapshots, metadata
generations, expected seq/conflicts, quotas, ticket match/mismatch, corruption/no-reset,
repair fault, partial/full write+flush faults, both startup/cleanup causes, close retry,
concurrent drain/entered writer и actual handles/OS write-denial witnesses проверены.
Partial GetMany IOException не возвращает partial view и требует reopen.

Actual Probe Profile Kv:6groups каждый source Debug/published Release,6started=reaped
children/profile.4killed cutpoints/profile: before-write old, partial17bytes old+tail
repair, after-write whole new, after-flush new; exact ticket outcome и both keys
после reopen.2remaining children/profile проверяют lock contention и open после release.
5commands0/3SDK binlogs,48frozen core/probe/config inputs, drift0. Evidence KvAttempt1
остаётся финальным: production source после него не менялся, добавлялись только tests/docs.

Общая nonincremental solution Debug0errors/1555warnings, Nom warnings0. От NOM0
snapshot1557 исчезли2CS8620 в параллельном Skia work; CS1591 presenter лишь сменил
номер строки, новых normalized warning messages нет. Эти UI изменения Nom не правил.
Full unit suite/GPU/fresh packages/physical disk-full/power-loss не запускались,
исторические Aether failures и8vendor crashes не закрывались/не повторялись.

Первый unit Attempt1:233passed/72failed из-за doubled separator в test temp cleanup
guard (падение Dispose маскировало body). Guard исправлен без ослабления containment;
Attempt2:305passed, затем11review gap/boundary cases, финальные316above. Ни skips,
ни восстановления исходников/чужих UI правок не было. Test-agent intermediate state
вне checkout; exact requirement/assertion matrix и frozen hashes сохранены там.

[API/контракт/команда](KV_CORE.md#nom1-kv-2026-11-07). Raw ignored evidence:
TestResults/Nom1-20261107 — final logs/TRX/binlogs, KvAttempt1 reports/verification,
warning-delta.json, scoped docs/final audit. NOM1 готов в low-level scope; no automatic
rotation/checkpoint/compaction (NOM4), no new App backend (NOM2), no NuGet readiness (NOM7).

<a id="nom-status-before-nom1-2026-11-07"></a>
## STATUS перед NOM1 — сохранён целиком

# TrueMoon.Nom: состояние модуля хранения

**2026-11-07 — NOM0: format/codecs и read-only recovery реализованы и проверены.**
[Точный format v1](FORMAT.md#nom0-format-2026-11-07), [история/команды](HISTORY.md#nom0-2026-11-07),
[очередь](PLAN.md#nom-own-queue).

| Область | Реализация | Фактические проверки |
| --- | --- | --- |
| Формат | Root/segment headers, atomic batch framing, canonical keys/metadata, SHA-256 и новый resource revision domain | Independent literal goldens для всех codecs, RFC GUID/UTF8, every-byte corruption, reserved/version/framing/overflow и exact budget boundaries |
| Recovery | Read-only replay, целые batches, active incomplete tail, sealed/sequence floor, consistent generation и bounded value-location index | Every final-frame truncation, full corruption/conflicts, short reads, limits, cancellation/IO и отсутствие Write/SetLength/Dispose |
| Segment chain | Sealed-file SHA, IDs/sequence/generation continuity; inherited index/locations и immutable previous view | Delete/overwrite через boundary, foreign/active/wrong-hash predecessor rejection и inherited budgets |
| Проверки Nom | 233/233 каждый Debug/Release и solution-filter Debug, failed/skipped0: 147new +86existing App/fault cases | Module Debug0warnings/0errors; solution nonincremental Debug0errors/1557warnings (1555baseline+2CS8620 в SkiaFontCatalog), new Nom warnings0 |
| Рабочий backend | UseNom/App/resource storage всё ещё LiteDB5.0.21; новые helpers internal, не подключены к store | Packages/solution/public API не менялись. Полный cutover/удаление LiteDB — NOM2 |
| Следующий шаг | NOM1: реальный KV writer/Create/Open/Get/WriteBatch, filesystem root authority/locks, flush/acknowledgement/Unknown и tail cleanup | Writer/checkpoint restore/compaction/pins/GC, published/fresh-package и physical disk/power proofs ещё не выполнены |

NOM0 готов в своём scope: контракт и executable parsing/replay foundation. Это не
готовое новое хранилище. Root descriptor умеет описать checkpoint authority, но payload
checkpoint/seed loading/restore — NOM4. Legacy filesystem-root rejection до записи
будет проверен на настоящем owner в NOM1/2; кодеки отклоняют чужие форматы/IDs.

Полная замена LiteDB остаётся принятой целью без optional provider/fallback/worker.
Исторические8fatal vendor children остаются подтверждённым результатом старого кода;
этот запуск их не повторял и не объявляет исправленными. Physical disk-full ранее
permission-gated, power-loss/symlink gates не сняты. Full unit suite/GPU не запускались,
прежние Aether failures не закрыты. Engine f.3→f.4/importer g.1 не переупорядочены.

<a id="nom0-2026-11-07"></a>
## NOM0: binary format/codecs и read-only recovery — 2026-11-07

Зафиксирован [format v1](FORMAT.md#nom0-format-2026-11-07): root256/segment160,
batch96+payload+footer48, unsigned LE counters/RFC GUIDs, strict canonical keys,
metadata codec и новый resource/1 revision hash domain. Header length/hash проверяются
до frame allocation, reserved/unknown/trailing/overflow data отклоняются.
Read-only replay возвращает bounded key→segment/offset/length index, sequence/view,
valid prefix и incomplete-tail flag; полный corruption всегда отказ. Sealed predecessor
SHA/IDs/base counters связывают segments, keys/locations переносятся без изменения
предыдущего view. Borrowed streams не закрываются/не изменяются; cancellation/IO
не превращаются в harmless EOF. Root/metadata/key codecs не используют BSON encoding.

Фактически: 233/233 Nom.Tests каждый Debug/Release и solution-filter Debug, failed/skipped0,
147new cases +86existing App/fault cases. Independent fixed goldens, каждый byte
complete frame и каждый cut финальной записи, authenticated semantic-invalid input,
index/record/replay budgets, cross-segment state и stream/cancel side effects проверены.
Solution nonincremental Debug0errors/1557warnings (1555baseline+2CS8620 в SkiaFontCatalog), new Nom warnings0.
Module initial Debug также0warnings/0errors. Full suite/published/fresh-package/IO
write/physical disk-full/power-loss не запускались; исторические Aether failures
не закрыты. Старые18core groups/8LiteDB crashes не повторялись этим запуском.

NOM0 завершён как contract +codec/read-only replay foundation. Следующий NOM1:
реальный single-writer KV Create/Open/Get/WriteBatch, filesystem authority/locks,
durable acknowledgement, Unknown и controlled tail cleanup. Checkpoint seed/restore/
compaction — NOM4, полный App/resource cutover и удаление LiteDB — NOM2.
Новые public API/packages/DI registrations не добавлены. Backend App пока LiteDB.

Raw ignored evidence: TestResults/Nom0-20261107 — final D/R logs/TRX/binlogs,
solution-debug.log, docs verification и final audit. Первичный Debug Attempt1:
228passed до последнего review; финальные counts выше относятся к замороженному source.
Research/plan/review state находится вне checkout, не входит в поставку.

<a id="nom-status-before-nom0-2026-11-07"></a>
## STATUS перед NOM0 — сохранён целиком

# TrueMoon.Nom: состояние модуля хранения

**2026-11-07 — принято: полностью заменить LiteDB собственным хранилищем Nom.**
[Решение и границы](STORAGE_DESIGN.md#nom-own-store-2026-11-07),
[этапы NOM0–NOM7](PLAN.md#nom-own-queue), [история](HISTORY.md#nom-own-store-2026-11-07).

| Область | Текущее состояние |
| --- | --- |
| Целевая архитектура | Transactional KV каталог, append-only batches, bounded key index, checkpoint/offline compaction; immutable CAS bytes через Stream |
| Полное удаление LiteDB | Запланировано в NOM2 вместе с работающей заменой: source/BSON codecs, PackageReference/CPM, tests/probes/downloads и dependency/output graph. Optional provider/fallback/worker не остаётся |
| Исходники сейчас | Ещё LiteDB5.0.21, public UseNom/store/factory и retained lifecycle. Этот документационный срез не менял C#, packages или solution |
| Совместимость | App API/logical IDs/stream ownership сохраняются как цель. Новый format/codec/hash domain уточняется в NOM0; legacy roots отклоняются без изменения файлов, auto-migration/legacy reader не включаются |
| Следующий этап | NOM0: binary framing/commit/recovery/OS durability contract, revision golden vectors и numeric index/segment/replay budgets; затем runnable core NOM1 и полный cutover NOM2 |
| Собственный backend | Ещё не реализован и не проверен. Новых build/unit/published/package/performance результатов нет |

Прежние проверки относятся только к LiteDB backend:86safe Nom.Tests каждый D/R и
solution filter,18core groups source Debug/published Release;8isolated fatal WAL
finalizer crashes. Это не passing proof новой реализации и не исправленный vendor
defect. Старый AST1b.f остановлен неполным по смене решения; vendor fix/upgrade/process
isolation сняты с активной очереди. [Прежний scope](FAULT_ACCEPTANCE.md).

Physical disk-full остаётся environment gate (Windows отклонила New-VHD); symlink
fixture privilege-gated. Будущая реализация требует собственных IO/kill/recovery/close
proofs; physical power-loss проверяется отдельно. Snapshots/pins/GC, backup/limits и
package delivery ещё впереди. Engine f.3→f.4 и importer g.1 не переупорядочены.

Проверки этого среза:42документа/1416локальных ссылок, новых link failures0; прежние
Nom PLAN/STATUS сохранены целиком,44source/test/probe/config inputs не изменились.
Build/tests не запускались; исторические passing counts не считаются новым запуском.

<a id="nom-own-store-2026-11-07"></a>
## Принята полная замена LiteDB собственной реализацией — 2026-11-07

Пользователь утвердил собственный Nom: transactional KV каталог, append-only журнал,
bounded in-memory key index и отдельные immutable CAS ресурсы через Stream.
LiteDB удаляется целиком в NOM2 вместе с рабочей заменой: source/BSON codec,
packages/CPM, test/probe vendor paths и dependency/output graph. Optional provider,
fallback, vendor fork и LiteDB worker не планируются. Прежний provider/package split
TrueMoon.Nom.LiteDb отменён; имя Nom, App API и standalone consumer boundary сохраняются.

[Решение и ограничения](STORAGE_DESIGN.md#nom-own-store-2026-11-07),
[единственная активная очередь NOM0–NOM7](PLAN.md#nom-own-queue).
Следующий шаг NOM0 фиксирует format/recovery/durability, canonical codec/hash domains
и numeric index/segment budgets; далее runnable core NOM1, full cutover NOM2,
faults NOM3, checkpoint/compaction NOM4, snapshots/indexes/GC NOM5, backup/perf NOM6,
delivery NOM7. Legacy roots отклоняются без изменения данных; автоматическая миграция
и LiteDB reader в первую поставку не входят. Существующие revision hashes нельзя
незаметно пересчитать при замене BSON.

Выполнено только планирование и проверка документов. C#/package/solution не менялись,
новых build/test/performance результатов нет. Старые86safe cases/18core groups/8fatal
children относятся к LiteDB и не подтверждают новый backend. AST1b.f остановлен
неполным по смене backend, vendor defect не объявлен исправленным. Generic disk-full/
power-loss requirements сохраняются; OS privilege gate не снят.
Engine f.3→f.4 и importer g.1 не переупорядочены, dependencies не добавлены.

<a id="nom-before-own-store-2026-11-07"></a>
## Прежние STATUS и PLAN — исторические снимки целиком

Ниже сохранено состояние до принятия собственной реализации. Формулировки
«текущий/следующий» внутри снимков относятся к тому моменту; активная очередь —
NOM0–NOM7 в PLAN.md. AST IDs и результаты не переименованы в новые этапы.

### Прежний STATUS

# TrueMoon.Nom: состояние модуля хранения

**2026-11-07 — AST1b.f частично выполнен; подтверждён blocker LiteDB5.0.21.**
[Fault acceptance: реализация, команды, результаты](FAULT_ACCEPTANCE.md#nom-faults-2026-11-07),
[очередь](PLAN.md#nom-fault-queue), [история](HISTORY.md#nom-faults-2026-11-07).

| Область | Текущее состояние | Фактические проверки2026-11-07 |
| --- | --- | --- |
| App/public API | UseNom, copied options, typed factory/store, sole App-owned host, lazy startup и retained async drain | Все57прежних App cases входят в86/86 каждый Debug/Release и solution-filter Debug; failed/skipped0 |
| Sink/cleanup | Write/flush/finalize и abort errors сохраняют causes; CleanupPending/Unknown удерживают reservation до cleanup; concurrent replay не завершает original writer |29новых safe fault cases: old/new outcome, journal/bytes/hash, borrowed inputs, реальные OS locks/handles, startup+cleanup causes |
| Native close | Собственные data/log/temp handles, non-owning vendor streams; swallowed checkpoint IO наблюдается, retry на том же owner thread завершает оставшийся close | Native before/after close, data write/flush/log truncation, actual file close/root unlock и concurrent drain retry |
| Native WAL | **Подтверждён fatal finalizer defect pinned5.0.21**; обвязка close его не устраняет |4сценария source Debug +4published Release:8crashes после atomic-outcome-verified; все reaped, AcceptancePassed=false |
| Physical disk-full | **Environment gate**: New-VHD64MiB отклонён Windows Hyper-V permission, Administrator=false | Том не подключался/не форматировался. Synthetic disk-full0x80070070 — отдельный safe test scope |
| Core regression | Schema/CAS/journal/hash domains сохранены, dependency не обновлена |18groups каждый source Debug/published Release;4actual killed/reaped cutpoints/profile;5commands0/3SDK binlogs/35frozen inputs, drift0 |
| Delivery | Source App API/solution registration есть; IsPackable=false | Production package/provider split, published App/fresh NuGet consumer и OS/RID acceptance — AST2b |

Solution nonincremental Debug:0errors/1555existing warnings, delta0; новые Nom warnings0.
Full unit suite/GPU не запускались, прежние Aether diagnostic failures не закрыты.
Symlink fixture privilege-gated0x80070522; process kill не является power-loss proof.

Следующий приоритет — устранить/изолировать подтверждённый native WAL blocker:
проверенный vendor fix/версия либо process boundary. Решение ещё не выбрано и не
реализовано; LiteDB5.0.21 не изменён. Затем physical isolated-volume proof.
AST1b.f, весь AST1 и AST2 остаются открыты; AST1c pins/GC и AST2b delivery в плане.
Старые AST1a stores без resource_control отклоняются, migration не реализована.
Engine f.3→f.4 и importer g.1 не переупорядочены.

### Прежний PLAN

# TrueMoon.Nom: план самостоятельного модуля хранения

<a id="current-assets-queue"></a>
<a id="current-nom-queue"></a>
## Текущая очередь — 2026-11-07

AST1b.f выполнен частично: IO/cleanup/transient close fixes проверены в86/86 safe
cases каждый Debug/Release и solution discovery; core18groups source/published.
**WAL fault probes дали8/8 fatal finalizer crashes LiteDB5.0.21**; physical disk-full
заблокирован OS permission. [Результат](FAULT_ACCEPTANCE.md#nom-faults-2026-11-07).
Приоритет — [native blocker и physical gate](#nom-fault-queue), AST1b.f не завершён.

Предыдущим запросом пользователя выполнен **AST2a — UseNom/App integration** до
оставшихся AST1 fault gates. [Public API и проверки](APP_INTEGRATION.md#nom-app-2026-11-07):
validated options, typed factory, sole App owner/lifecycle, public streams и retained
async drain;57/57 xUnit каждый D/R и через solution filter, core18groups каждый source/published.
Это явное уточнение порядка работ2026-11-07, не закрытие AST1 или всей AST2.
Переименование2026-11-07: библиотека [TrueMoon.Nom](../../TrueMoon.Nom/README.md)
и отдельный [TrueMoon.Nom.Probe](../../ManualTests/TrueMoon.Nom.Probe/README.md)
включены в TrueMoon.slnx. AST2a реализовал публичный App/storage API; пакетная поставка ещё впереди. [Повторные18groups D/R и solution build](NOM_RENAME.md#nom-rename-2026-11-07).
Прежний Assets — рабочее название того же направления; AST IDs сохраняются.
**AST1b streaming core проверен на Windows x64; AST1b.f частично выполнен и блокирован.** LiteDB выбран после
сравнения. Самостоятельный модуль остаётся вне движка. [Профиль LiteDB v1](LITEDB_DESIGN.md#litedb-v1-2026-11-07)
задаёт catalog authority/schema, Direct owner thread, staged objects, snapshots/GC,
cancel/commit outcomes и рабочие budgets. [Runnable AST1a](AST1A_PROTOTYPE.md#ast1a-2026-11-07) и [AST1b streaming/CAS/journal proof](AST1B_STREAMING.md#ast1b-2026-11-07) прошли source Debug/published Release; весь v1 ещё не реализован.
[STATUS](STATUS.md), [правила планирования](../PLANNING.md). Имя TrueMoon.Nom утверждено; публичный API AST2a реализован, provider/package split — AST2b.

Предыдущее [сравнение](COMPARISON.md#comparison-2026-11-07) сохраняет аргументы/ограничения;
оно не переопределяет последующий выбор пользователя. Working baseline — LiteDB5.0.21.
AST1a закрепил package/archive/DLL identity и owner/schema/transactions; AST1b добавил
streaming reads/writes, CAS, journal и process-kill recovery. Текущий runner:18групп
каждый Debug/published Release. SQLite не входит в implementation очередь.
Actual symlink fixture privilege-gated; target-write/cleanup safe proofs выполнены, native WAL и physical disk-full gates открыты;
snapshots/GC/migrations/performance ещё открыты. Numeric defaults design не объявлены
целиком проверенными. Уточнение2026-11-07: fault acceptance AST1b сохраняется отдельным
AST1b.f перед AST1c; core passing не означает завершение всего AST1b.

## Границы и потребители

- Модуль хранит persistent asset identity/metadata, immutable content revisions,
  dependency references и manifests, предоставляет bounded read/write/snapshot operations.
  Он применим к моделям, изображениям, аудио и другим пользовательским типам.
- Подключается к App независимо от Aether. Engine/editor/importer/audio — consumers;
  storage не зависит от World, MeshDefinition, renderer, GPU, UI или Assimp.
- Формат исходного файла, import/decode/CPU validation, resource preparation и
  GPU/voice lifetime принадлежат соответствующим consumers, а не storage backend.
  Persistence snapshot/pin и f.2 CPU/GPU lease имеют разные ownership contracts.
- Общие для хранения contracts принадлежат модулю Nom. App/IFactory integration
  использует TrueMoon.Contracts; implementations/providers изолированы. Перенос
  существующих публичных Aether типов не входит в эту работу и требует отдельного
  решения о совместимости API.

## Предлагаемая архитектура — ещё не утверждённый API

1. **Persistent catalog:** SourceId/AssetId, kinds/schema, names/locators/tombstones,
   exact revisions, dependency graph, importer recipe/settings и provenance.
2. **Immutable byte store:** streaming writes, length/hash verification, staged
   publication, deduplication по digest; content-addressed files — первый кандидат.
   Метаданные и бинарные bytes могут иметь разные providers.
3. **Read snapshots/export:** frozen manifest + exact bytes для build/runtime/обычных
   приложений. Пакет поставки может читаться без writable Editor database. Конкретный
   archive/container format ещё выбрать.

Первый provider — **LiteDB catalog + filesystem content-addressed objects**. Catalog
authoritative для IDs/ledger/revisions; Runtime/export — frozen manifests+bytes.
FileStorage остаётся возможным будущим byte provider после отдельного atomicity/
streaming proof, не заменяет staged publication v1. Производительность пока не измерена.

## Инварианты для согласования и прототипа

- Требование2026-11-07: потоковая запись и загрузка ресурсов (геометрия, изображения,
  шейдеры) без обязательного whole-payload массива. Borrowed non-seekable input/short
  reads, bounded buffer/length/hash/cancel; owned seekable read stream удерживает
  bytes до actual close. Full same-handle integrity scan до возврата read stream
  допустим в первом профиле; progressive/chunk verification — отдельное развитие.
  Storage не декодирует formats и не владеет GPU resource lifetime.
- AssetId обозначает logical asset и переживает edits/explicit rename; revision
  обозначает exact bytes/recipe. Одинаковый blob digest не объединяет logical IDs.
  Persistent identity ledger не считается удаляемым cache: authoritative records
  находятся в LiteDB, portable logical export не создаёт второй source of truth.
- Source originals, generated artifacts, thumbnails и temporary import data имеют
  отдельные retention/ownership policies. Метаданные поиска могут быть rebuildable,
  но удаление индекса не должно менять IDs или терять tombstones/import settings.
- Write batch фиксирует expected generation, complete dependencies и candidate ledger.
  Readers видят целиком прежнюю или новую generation. Blobs durable/verified до
  публикации catalog references. Database transaction не распространяется автоматически
  на filesystem objects; orphan recovery и проверка crash cut points обязательны.
- Отмена до commit не публикует новую generation. После commit возвращается committed
  outcome; спорный исход после сбоя разрешается через transaction/import operation ID
  и recovery. Отмена ожидания не освобождает незавершённую операцию/owner.
- Snapshot/pins удерживают exact revisions для reader/build/child process. GC не удаляет
  bytes активных snapshots/manifests/retained generations. Начать с offline GC либо
  доказанного единственного writer; multi-process writers не обещать по умолчанию.
- App регистрирует service и typed project-store factory; зависимости через constructor.
  Project/session владеет результатом фабрики, App — фабрикой/общими services; stop
  прекращает admission, drain завершает операции, затем закрываются readers/provider.
  Failed startup/cleanup и повторное подключение должны иметь явные outcomes.
- Read/write bytes, object size, metadata/query/page size, queue, concurrent operations,
  staging/orphan disk space и snapshot count bounded. Рабочие defaults/corpus — в
  LiteDB v1; model-import limits не становятся общими лимитами для аудио/изображений.

## Этапы и критерии

| Этап | Результат | Критерий завершения |
| --- | --- | --- |
| AST0 | Requirements, provider comparison и обсуждение решения | Выбраны authority/source-control policy, process/read/write model, catalog/blob/bundle границы, numeric workload/budgets, baseline и лицензии/поставка. Decision record фиксирует выбор после обсуждения, без объявления нового пакета готовым |
| AST1 | Bounded standalone LiteDB prototype по v1 | AST1a–d: owner/schema/codecs, staged publication/journal, snapshots/pins/offline GC, migrations/backup и limits/performance проходят на pinned5.0.21; source proof без Aether, затем production integration AST2 |
| AST2 | App/IFactory подключение и поставка | AST2a public App API проверен; AST2b provider/package split, standalone published/fresh-package consumer, migration/backup и OS/RID acceptance ещё нужны. AST2 целиком не завершена |
| AST3 | Consumer integration | Aether importer/build использует exact storage snapshots, identity ledger и atomic candidate publication; Runtime получает immutable manifest/bytes, existing f.1/2 validation/leases сохраняются. Другой consumer подтверждает независимость от движка |

<a id="litedb-ast1"></a>
## AST1: LiteDB implementation queue

| Шаг | Проверяемый результат |
| --- | --- |
| AST1a, проверено2026-11-07 | [Runnable proof](AST1A_PROTOTYPE.md): pinned5.0.21, Create/Open, IDs/schema/collation/index definitions, exclusive root/Direct, owner-thread transaction/rollback/reopen и serialized/query/queue/startup-stop boundaries;10groups каждый D/R, symlink fixture — environment gate |
| AST1b, core проверено2026-11-07 | [Streaming proof](AST1B_STREAMING.md): bounded geometry/image/shader Stream upload/read; staging/CAS, atomic batch/journal, cancel/commit, retry/expiry, missing/corrupt objects и4actual child-kill cutpoints;18groups каждый D/R вместе с AST1a |
| AST1b.f, частично/blocker2026-11-07 | Safe IO/cleanup/transient-close86/86 D/R;8/8 isolated native WAL finalizer crashes pinned5.0.21, physical volume permission gate. [Fault queue](#nom-fault-queue); весь AST1b не закрыт |
| AST1c | Indexed lookup/reverse closure, stable identities/tombstones, frozen manifests/pins, offline GC и recovery; old snapshot сохраняет bytes после нового import |
| AST1d | Migrations/backup/export, limits/retained failed cleanup; corpus1kassets/4krevisions/16kedges, затем50k/100k/300k; small payloads и16/128MiB blobs, cold/warm/reopen, p50/p95/allocations/disk amplification |

<a id="nom-fault-queue"></a>
## AST1b.f: оставшаяся fault acceptance

| Часть | Состояние и критерий |
| --- | --- |
| AST1b.f.software | Проверено2026-11-07: sink/finalize/flush, retained cleanup/Unknown, failed startup и transient close/retry;86safe cases D/R + solution filter. [Exact tests](FAULT_ACCEPTANCE.md#nom-faults-2026-11-07) |
| AST1b.f.native, blocker | Подтверждён5.0.21 WAL/finalizer defect:8/8 bounded child crashes, runner nonzero. Выбрать и проверить vendor fix/версию либо отдельный process boundary; четыре сценария source/published должны завершаться без fatal exception с целым old/new outcome, ticket resolution и confirmed cleanup. Решение пока не принято, dependency не менять молча |
| AST1b.f.volume, environment gate | На isolated disposable volume доказать настоящий disk-full для sink/WAL/recovery/cleanup, не на рабочем диске. New-VHD64MiB отклонён Windows permission; attach/format не было. Synthetic HRESULT не заменяет эту проверку |

AST1b.f остаётся открытым до native/volume acceptance. Full power-loss proof —
отдельный environment gate. AST1c не объявлять готовым по passing safe tests;
provider/package и production readiness по-прежнему требуют AST1/AST2b acceptance.
<a id="nom-app-queue"></a>
## AST2: App и поставка

| Шаг | Результат и граница |
| --- | --- |
| AST2a, проверено2026-11-07 | [UseNom](APP_INTEGRATION.md): copied options, IFactory<IProjectAssetStore, OpenAssetStoreArguments>, one App-owned host/non-owning aliases, lazy start, rollback/fallback cleanup, cancellation/timeout retention и public storage DTOs;57/57 D/R + solution discovery, source-only App proof |
| AST2b, запланировано | Provider/contracts/package split и pack metadata; независимый published/fresh NuGet App consumer без friend access/Aether, заявленные OS/RID, migrations/backup после AST1 readiness. IsPackable пока false |

## Связь с engine roadmap

Detailed storage work принадлежит AST1a–d ниже; engine PLAN хранит только consumer dependency.

Model import остаётся в [ARCH1g](../engine/PLAN.md), rendering owners
в ARCH1f, audio research в ARCH1h. Детальные storage задачи ведутся только здесь.
AST0 может идти независимо от f.3/f.4 и g.1 CPU import. Production persistent delivery
g.2 должно согласовать contract с Nom; это не причина переносить renderer/importer
в storage или заранее вводить зависимость Aether от конкретной базы.

Заявки потребителей принимаются по [общим правилам](../requests/README.md) с module `nom`.
Текущий пользовательский запрос не подменяет существующие engine/alloy claims.

<a id="nom-faults-2026-11-07"></a>
## AST1b.f: IO/cleanup fixes и подтверждённый WAL blocker — 2026-11-07

Добавлены внутренний per-factory IO seam, явное владение data/log/temp handles,
наблюдение swallowed native-close IO и retry close на том же живом owner thread.
Abort/Unknown+cleanup сохраняют обе причины; CleanupPending удерживает write reservation,
replay не читает replacement input и не завершает ещё работающий publisher. Recovery
освобождает owned staging даже при metadata abort failure после полной journal validation.
Failed startup/cleanup не освобождает registry/lock до фактического close.

Safe Nom.Tests86/86 каждый Debug/Release и solution-filter Debug, failed/skipped0
(57existing App +29new fault cases). CoreFinal18groups каждый source Debug/published
Release,5commands0/3SDK binlogs/35frozen inputs, drift0. Solution nonincremental Debug
0errors/1555existing warnings/delta0, new Nom warnings0. Full suite/GPU не запускались.

Отдельные bounded WAL children дали8/8 падений pinned LiteDB5.0.21 в
TransactionService.Finalize: discarded page must be writable. До падения все проверили
целый old state после reopen, но process continuity нарушена. RunnerCompleted=true,
AcceptancePassed=false, runner exit1, все children reaped. Это подтверждённый blocker,
не expected-success/Skip. Первичный in-process Attempt2 оборвался после45passed;
финальные86safe cases исключают опасный vendor path, который сохранён в child runner.
New-VHD64MiB отклонён OS Hyper-V permission (Administrator=false); attach/format не было.
Physical disk-full остаётся environment gate; synthetic HRESULT не заменяет этот proof.

[Изменения, exact cases, команды и evidence](FAULT_ACCEPTANCE.md#nom-faults-2026-11-07).
AST1b.f/AST1 не закрыты. Следующий приоритет — проверенный vendor fix/версия либо
process boundary; решение не выбрано, pinned dependency не изменена. Затем isolated
physical-volume proof; AST1c/AST2b остаются в плане, engine f.3→f.4 не меняется.

<a id="nom-status-before-faults-2026-11-07"></a>
## STATUS перед AST1b.f — сохранён целиком

# TrueMoon.Nom: состояние модуля хранения

**2026-11-07 — реализовано подключение через App (AST2a).**
[UseNom/API/команда/ownership](APP_INTEGRATION.md#nom-app-2026-11-07),
[план](PLAN.md#nom-app-queue), [история](HISTORY.md#nom-app-2026-11-07).

| Область | Реализация | Фактические проверки2026-11-07 |
| --- | --- | --- |
| App/DI | Public UseNom, validated copied NomOptions, typed store factory, sole NomStoreHost и shared non-disposable start/stop aliases; startup без IO | Nom.Tests57/57 каждый Debug/Release, failed/skipped0; exact solution-filter Debug57/57; App.Build/Builder.Setup/RunAsync, duplicate/conflict/snapshot и independent Apps |
| Public storage | IProjectAssetStore, OpenAssetStoreArguments/Create/Open, AssetRecord и resource/journal values, sync/async disposal; library зависит только от Contracts/LiteDB | Public bytes/hash/reopen/journal, capacity/null/invalid/cancel/failed open/native IOException и canonical UNC rejection до IO |
| Lifecycle | All owners stop before async drain, no new admission, borrower cancellation не освобождает resources; timeout сохраняет root/count для retry | Failed startup/build, no-start Dispose fallback, shared cleanup, actual read/ignored-cancel writer retained locks и release после actual finish; App.RunAsync normal/external-cancel |
| Core regression | Private owner/schema/CAS/journal и persistent hash domains сохранены; DTO names стали public | CoreVerified:18groups каждый source Debug/published Release,4actual kill/reap cutpoints/profile,5commands0/3SDK binlogs/31frozen core/probe/project/config inputs, drift0 |
| Delivery | Public App API доступен из source; Nom/Nom.Tests/Probe в solution, IsPackable=false | Production NuGet/provider split, published App consumer и full OS/RID acceptance — AST2b, этим срезом не проверены |

Общая nonincremental solution Debug:0errors/1555existing warnings, delta0; новые
Nom/Nom.Tests warnings0. Full unit suite не запускалась, старые Aether diagnostic
failures не закрыты. Symlink fixture privilege-gated0x80070522. Process-kill proof
не является physical power-loss proof. Native-close recovery не обещан; combined
creation+cleanup error aggregation проверен по source, без fault injection.

Следующий storage шаг — AST1b.f target-write/disk-full/native close/cleanup fault
gates, затем AST1c snapshots/pins/offline GC. AST1 и вся AST2 не закрыты. Старые AST1a
stores без resource_control по-прежнему отклоняются; migration не реализована.
Engine f.3→f.4 и importer g.1 не переупорядочены.

<a id="nom-app-2026-11-07"></a>
## AST2a: публичное App подключение, factory и drain — 2026-11-07

Реализован UseNom с immediate validation/copied options, one owned NomStoreHost,
non-owning typed factory и одним shared IStartable/IStoppable lifecycle adapter.
Startup не открывает stores; user получает caller-disposable IProjectAssetStore по
typed Root/IDs/Create/Open arguments. Public AssetRecord/resource/journal DTOs
не раскрывают LiteDB/BSON API. Persistent schemas/hash domains сохранены.
Factory/owner получили async drain: запрет admission, stop всех owners до await,
retention при read/writer timeout и cancel wait; success cached, drain timeout retryable.
Failed creation+cleanup сохраняет обе ошибки; native close-fault recovery не заявлен.
Canonical root guard закрыл //server/share UNC до filesystem I/O.

Новый TrueMoon.Nom.Tests в solution:57/57 xUnit каждый Debug/Release, failed/skipped0,
solution-filter Debug57/57. Проверены App.Build/Setup/RunAsync normal/cancel, options
boundaries/snapshot/conflicts, public stream/journal/reopen, capacity/failed open,
startup/build rollback/fallback disposal, idle read и ignored-cancel writer retained
root/count и confirmed cleanup. CoreVerified18groups каждый source Debug/published
Release (4actual killed/reaped children/profile);31frozen inputs,5commands0/3SDK binlogs.
Общая nonincremental solution Debug0errors/1555existing warnings/delta0; new warnings0.
Первичные44/56case runs предшествуют final UNC guard; финальные57case runs после него.

[Команда, exact tests и limitations](APP_INTEGRATION.md#nom-app-2026-11-07).
Raw ignored artifacts — TestResults/NomApp-20261107. Full suite/GPU/production NuGet
не запускались; Aether failures и symlink privilege gate не закрыты. Combined
startup/cleanup failure source-reviewed, не injected fault proof. Следующий AST1b.f,
provider/package delivery AST2b; user запрос явно разрешил App-срез раньше этих gates.

<a id="nom-rename-2026-11-07"></a>
## TrueMoon.Nom: переименование и включение в solution — 2026-11-07

По решению пользователя research Assets называется TrueMoon.Nom.6implementation
файлов выделены в class library TrueMoon.Nom (namespace совпадает),4proof/fixture
файла — в ManualTests/TrueMoon.Nom.Probe с ProjectReference и friend assembly.
Оба проекта зарегистрированы в TrueMoon.slnx; LiteDB5.0.21 — в общем CPM.
Implementation остаётся internal/non-packable, AST2 public App/package не закрыт.
docs/nom — canonical documentation; старые docs/assets — pointers для прежних ссылок.

Повторный runner собирает реальные csproj с isolated artifacts/feed/cache/exact CPM.
Attempt2:18groups каждый source Debug/published Release,5commands0,3SDK binlogs,
15frozen source/project/config hashes/drift0. Program проверяет отдельную output-local
TrueMoon.Nom.dll. Graph LiteDB5.0.21: прежний research System.Buffers direct reference
не перенесён, framework предоставляет его API. Actual LiteDB.dll SHA/version прежние.
Attempt1 старый двухпакетный graph guard остановил run после restore, исправлен;
Attempt2 прошёл целиком. [Scope/команда/ограничения](NOM_RENAME.md#nom-rename-2026-11-07).

Общий nonincremental solution Debug0errors/1555existing warnings, delta0; новые
проекты warnings0. Solution list содержит оба проекта; links/namespace/source-preservation
и whitespace checks выполнены. Full unit suite не запускалась; старые Aether failures
не закрыты. Symlink privilege, disk/write/cleanup faults, power-loss/GC/public App/GPU
остаются gates. Очередь AST1b.f и engine f.3→f.4 сохранена.

<a id="ast1b-2026-11-07"></a>
## AST1b: streaming ресурсов, CAS и журнал — 2026-11-07

Реализован запрос о потоковой загрузке геометрии, изображений и шейдеров в existing
standalone [LiteDbPrototype](../../ManualTests/TrueMoon.Nom.Probe/README.md).64KiB streaming
upload/read, non-seekable short reads, optional length/hash, borrowed sources/owned read
leases, staging/CAS/no-overwrite dedup и atomic batch publication. Journal поддерживает
reserved sequence/request, retry/abort/expiry/unknown outcome; recovery валидирует
cross-record output references до любых mutation. Mandatory control исключает sequence
reset после prune; старые AST1a stores без control явно отклоняются без migration.

FinalReviewed: source Debug/published Release по18groups (10AST1a+8streaming),5commands0,
3SDK binlogs,11frozen source/script hashes/drift0.4actual killed/reaped children/profile
на stage/finalize/before-commit/after-commit. Native110 partial-publication rollback,
tampered head/journal/control и retained active reader/writer drain проверены.
Для32MiB: upload491720/491456bytes и open+read537272/524648bytes allocations D/R;
maxrequest65536,513inputreads. Это bounded-memory observation, не heap/RSS/SLA.

Общий nonincremental solution Debug0errors/1555existing warnings, delta0. Prototype
own warnings0; full unit suite/production App/package/decoder/GPU не запускались.
Symlink fixture privilege-gated0x80070522. Disk-full/cleanup fault acceptance AST1b.f
и physical power-loss ещё открыты; AST1b core passing не закрывает весь шаг.
[Версионированный scope/API/команда/limits/hashes](AST1B_STREAMING.md#ast1b-2026-11-07).
Raw ignored artifacts: TestResults/LiteDbAst1b-20261107/FinalReviewed и solution-debug.log.
Attempt1 compilation failed на fixture accessor, исправлен; Attempt2 до новых регрессий;
Attempt3 passed, затем исправлен explicit abort/stop race и выполнен FinalReviewed.
Следующий шаг AST1b.f, затем AST1c; public module — AST2, engine queue без изменений.

<a id="ast1a-2026-11-07"></a>
## AST1a: runnable LiteDB5.0.21, owner/schema/transactions — 2026-11-07

Добавлен [ManualTests/LiteDbPrototype](../../ManualTests/TrueMoon.Nom.Probe/README.md):
isolated pinned feed/cache, source TrueMoon.Contracts/IFactory, no Aether/package graph
edits. [Exact command/inputs/limits/evidence](AST1A_PROTOTYPE.md#ast1a-2026-11-07).
FinalReviewed source Debug/published Release:2/2profiles,10groups каждый,5commands exit0,
3SDK binlogs,5source hashes/drift0; actual local DLL5.0.21.0/SHA matches archive.
Official missing-archive bootstrap проверен; tampered package отвергнут до restore.

Реализованы Create/Open с IDs/schema/binary collation/index preflight, exclusive root/
Direct и single-thread DB owner, bounded queue, immutable strict BSON codec, checked
metadata transactions/rollback/reopen. Seeded corrupt/mismatched catalog не reset;
actual raw Direct и root child contention, partial-batch failures/native duplicate,
size boundaries, cancel/stop/startup race/2live owner factory drain проверены.
Source-derived native AutoTransaction auto-rollback после duplicate-key110 объясняет
Rollback=false без active transaction; сохранены original exception и строгие
assertions unchanged set/generation/reopen. Symlink fixture не выполнен:0x80070522
privilege gate, source precheck не выдаётся за tested filesystem isolation.

Failed attempts сохранены: Attempt1 выявил общий CPM CLI-tool reference в source
Contracts restore (изолирован invocation-only DirectoryPackagesPropsPath, root settings
не менялись); Attempt2 — serializer name ambiguity в новом harness; Attempt3 — wrong
collation error mapping (read-only stored-collation preflight); Attempt4/5 — уточнение
native auto-rollback и именованного duplicate-key code110. Attempt6–8 passing получили
дополнительные review cases; FinalReviewed включает wrong index-expression preservation.
Независимые test-gap/assertion review помогли закрыть framing, startup-stop/failed-start,
factory drain, exact queue bytes/uppercase GUID и response-boundary gaps.

Общий nonincremental solution Debug0errors/1555warnings, warning delta0; новых prototype
warnings0,167existing source Contracts XML warnings. Full unit suite не запускалась,
прежние Aether diagnostics failures не закрыты. Restore context Contracts возвращён
штатным отдельным restore после final isolated run. AST1b–d CAS/journal/pins/GC/
migrations/fault/performance и AST2 App/production package остаются впереди.

<a id="litedb-v1-2026-11-07"></a>
## Выбран LiteDB и проработан v1 — 2026-11-07

Пользователь предпочёл LiteDB после сравнения; это меняет текущую очередь с provider
selection на [AST1a](PLAN.md#litedb-ast1). [Design v1](LITEDB_DESIGN.md) задаёт Direct/
exclusive project owner, owner-thread transactions, authoritative catalog/identity
ledger, immutable objects, staged publish/journal, snapshot pins/offline GC, recovery,
cancel/commit outcomes, budgets и workload. Package/API names остаются рабочими;
новые production зависимости не добавлены.

Сверены source5.0.21 transaction/SharedEngine/EngineSettings и Collation.Binary
(invariant/ordinal), docs FileStorage/pragmas. Default culture/global mapper, implicit
upgrade/rebuild и Shared mutex path не входят в первый профиль. Backend choice —
решение пользователя, не performance/crash superiority claim. Кодовые этапы AST1a–d
теперь LiteDB-specific; прежняя comparison сохранена с явной исторической пометкой.
Проведены только чтение и design/document changes; package SHA/runtime/restore/build/
unit/benchmark/crash/native tests не выполнялись.

<a id="comparison-2026-11-07"></a>
## LiteDB и SQLite: сравнение по критериям модуля — 2026-11-07

По запросу сравнения создана [матрица AST0](COMPARISON.md#comparison-2026-11-07):
identity/revisions/dependencies, query/traversal, flexible metadata, atomic generation,
snapshots/multi-process, bytes/streaming, cancel/drain, collation, migrations/backup,
native delivery/licenses и performance gates. Основание — официальный package metadata,
docs и versioned source LiteDB5.0.21/Microsoft.Data.Sqlite10.0.14; версия actual native
SQLite и resolved dependency graph пока не проверялись.

Подтверждённые отличия: LiteDB managed implementation, multiple readers/write locks
per collection, Direct/Shared connection policy, per-thread transaction; FileStorage
per-chunk transaction не равен atomic whole artifact. SQLite FK/recursive queries,
WAL snapshot/one writer и Online Backup API полезны для catalog, но external blob
commit/pins/GC остаются контрактом модуля. SqliteCommand.Cancel()10.0.14 — no-op,
ExecuteReaderAsync проверяет token до синхронного выполнения. Async/Task cancellation
не используются как доказательство завершения I/O у любого кандидата.

Предварительно SQLite ведёт как catalog для revision/dependency graph; LiteDB остаётся
сильной альтернативой при приоритете полностью managed delivery и одного project
owner. Dependency/query преимущества — архитектурная оценка, не benchmark result.
Backend не утверждён, AST0 не завершён: открыты authority/portable records, numeric
workload/budgets и окончательный process model. Сформулированы одинаковые AST1 gates:
identity, publication/cancel/commit races, snapshot/GC, faults/recovery, queries,
durability/performance и standalone package/lifecycle.

Только документационное исследование; NuGet install/restore/build/unit/native/database
benchmark/fault tests не выполнялись. Production code/packages не менялись, engine
очередь и прежние captured-import results не обновлены этим сравнением.

## Архив текущего STATUS после AST1a

Сохранён дословно перед обновлением AST1b; результаты ниже исторические.

# Assets: состояние модуля хранения

**2026-11-07 — AST1a runnable-прототип реализован и проверен.**
Граница пользователя: вне Aether/engine, пригоден другим приложениям/asset kinds.
[План AST0–AST3](PLAN.md), [матрица LiteDB/SQLite](COMPARISON.md#comparison-2026-11-07),
[LiteDB design](LITEDB_DESIGN.md#litedb-v1-2026-11-07), [результаты/команда](AST1A_PROTOTYPE.md#ast1a-2026-11-07), [история](HISTORY.md#ast1a-2026-11-07).

| Область | Текущее состояние | Проверки |
| --- | --- | --- |
| Границы/контракт | Предложены persistent catalog, immutable bytes и snapshots/export; package/API names ещё не утверждены | Прочитаны App/DI правила, engine f.1/2 и captured-import design; реализации нет |
| Backend | Pinned LiteDB5.0.21, research Direct/owner/schema/codec implemented; actual DLL5.0.21.0 | Source Debug/published Release:10groups каждый, pinned archives/DLL SHA и resolved graph проверены; symlink fixture privilege-gated |
| Identity/consistency | AST1a Create/Open/IDs/strict codec/unique index/atomic metadata batch, partial/native rollback, reopen и bounded owner lifecycle реализованы; CAS/journal/pins/GC ещё design |10named groups каждый D/R, source drift0; raw Direct/root child contention, exact BSON/query/queue bounds и deterministic startup-stop/drain прошли |
| App/consumer integration | Самостоятельный App module/typed factory и Aether consumer bridge запланированы | Production code/packages/references не добавлены |

FinalReviewed —2profiles/5commands exit0/3SDK binlogs/5frozen source hashes. Общий
nonincremental solution Debug отдельно:0errors/1555existing warnings, warning delta0;
full unit suite/benchmark/fault/power-loss и production Assets package не запускались.
Actual symlink rejection не выполнен из-за Windows privilege gate; prechecks не равны
handle-verified filesystem isolation. Прежние Aether diagnostic failures не закрыты.

Следующий storage шаг — [AST1b](PLAN.md#litedb-ast1): staged bytes/CAS, operation journal,
atomic candidate publication/cancel/commit и recovery. Public App/package — AST2.
Текущая engine очередь f.3→f.4 и model importer g.1 не переупорядочены.

## Архив STATUS перед переименованием Nom

Ниже дословная предыдущая сводка; результаты исторические.

# Assets: состояние модуля хранения

**2026-11-07 — AST1b streaming/CAS/journal core реализован и проверен.**
Самостоятельное хранение остаётся вне Aether; geometry/image/shader — opaque bytes.
[План](PLAN.md#litedb-ast1), [design](LITEDB_DESIGN.md#litedb-v1-2026-11-07),
[команда и результаты](AST1B_STREAMING.md#ast1b-2026-11-07), [история](HISTORY.md#ast1b-2026-11-07).

| Область | Реализация | Фактические проверки2026-11-07 |
| --- | --- | --- |
| Backend/owner | Pinned LiteDB5.0.21, strict codecs/Direct owner, source IFactory, resource drain/control lane | Source Debug/published Release:18groups каждый,5commands exit0,3SDK binlogs,11frozen source/script hashes, drift0; actual DLL5.0.21.0 |
| Streaming | Borrowed non-seekable/short-read upload,64KiB I/O, optional length/hash, owned read-only seekable stream; full same-handle verification при Open | Literal GEO1/BMP/GLSL readback;32MiB generated bytes и independent hash; upload allocations491720/491456bytes, open+read537272/524648bytes D/R |
| Publication/journal | Atomic multi-resource generation/heads/revisions, no-overwrite CAS/dedup, reserved ticket/replay/abort/expiry, strict cross-record validation | Failed second stream/native110 rollback, cancel before/after commit, unknown outcome/reopen, corrupt head/journal/control, missing/corrupt CAS;4killed/reaped children/profile |
| Ownership | Read leases и writer удерживают factory/root до actual finish/close, stop abort unused reservation, bounded users/queue | Full queue completion, ignored-cancel writer, idle/active reader drain,16/17leases и confirmed root release |
| App/consumer | Public App/Cobalt/module/package и Aether bridge пока запланированы | Production projects/dependencies/API не добавлены; decoder/GPU streaming не проверялись |

Общий nonincremental solution Debug:0errors/1555existing warnings, delta0; prototype
own warnings0. Full unit suite не запускалась, прежние Aether diagnostic failures
не закрыты. Symlink rejection fixture не выполнен: Windows privilege0x80070522.
Process-kill proof не равен power-loss durability. AST1a каталоги без нового mandatory
resource_control явно отклоняются; автоматической migration нет.

Следующий конкретный шаг — AST1b.f: target-write/disk-full и retained cleanup/retry
fault gates, затем AST1c snapshots/pins/offline GC. AST1b целиком ещё не закрыт.
Общие disk/log accounting и часть v1 budgets пока design; production App/package — AST2.
Engine f.3→f.4 и importer g.1 не переупорядочены.

## Архив STATUS перед App integration

Предыдущая сводка сохранена дословно; проверки ниже исторические.

# TrueMoon.Nom: состояние модуля хранения

**2026-11-07 — модуль переименован в TrueMoon.Nom и включён в solution.**
Существующее storage core выделено в отдельную библиотеку, console proofs — в
TrueMoon.Nom.Probe. [План](PLAN.md#current-nom-queue), [design](LITEDB_DESIGN.md),
[перенос/команда/проверки](NOM_RENAME.md#nom-rename-2026-11-07), [история](HISTORY.md#nom-rename-2026-11-07).

| Область | Реализация | Фактические проверки2026-11-07 |
| --- | --- | --- |
| Имя/solution | TrueMoon.Nom.csproj, namespace TrueMoon.Nom, Probe с ProjectReference; оба в TrueMoon.slnx | dotnet sln list подтверждает оба; source Debug/published Release проверяют отдельную output-local TrueMoon.Nom.dll |
| Storage core | AST1a/AST1b strict LiteDB catalog/owner, streamed resources/CAS/journal/cancel/replay/atomic batch; перенос с изменением namespace | Attempt2:18groups каждый D/R,5commands0,3SDK binlogs,15frozen source/project/config hashes, drift0; все4kill/reap cutpoints/profile выполнены заново |
| Dependencies | Nom не зависит от Aether; source TrueMoon.Contracts/IFactory, LiteDB5.0.21 в общем CPM | Isolated graph только LiteDB5.0.21, actual DLL5.0.21.0/SHA совпал с pinned archive; прежний лишний System.Buffers direct reference не перенесён |
| App/поставка | Internal research library, IsPackable=false; public App/Cobalt/provider split и package delivery — AST2 | Включение в solution не является production package/API proof |

Общий nonincremental solution Debug:0errors/1555existing warnings, warning delta0;
новые проекты warnings0. Full unit suite не запускалась; прежние Aether diagnostic
failures не закрыты. Symlink fixture privilege-gated0x80070522. Process-kill proof
не равен power-loss durability; decoder/GPU integration не проверялись.

Численные allocations и source hashes прежних AST1a/AST1b записей остаются историческими.
Persistent schemas/hash domains при переименовании сохранены. Старые AST1a stores без
mandatory resource_control по-прежнему отклоняются: migration этим шагом не реализована.

Следующий конкретный шаг — AST1b.f: target-write/disk-full и retained cleanup/retry
fault gates, затем AST1c snapshots/pins/offline GC. AST1b целиком ещё не закрыт;
общие disk/log accounting и часть v1 budgets пока design. Engine f.3→f.4 не переупорядочен.

<a id="nom5-2026-11-08"></a>
## NOM5: indexes, frozen manifests, pins и offline GC — 2026-11-08

Реализован [контракт NOM5](RETENTION.md#nom5-retention-2026-11-08). AssetRecord tags и
exact dependencies copied/canonical, NOMAST02 extension входит в revision hash.
Пустые links сохраняют прежние NOMAST01 bytes/hash; исходный5-argument constructor
сохранён. ID/source/kind/tag queries и forward/reverse dependency indexes bounded,
rebuildable; query cursor привязан к full metadata sequence/filter. Content commit
готовит новый index до durable write. Stable ID/source/kind и binding tombstones
сохраняются после DeleteResources; старые exact versions удерживаются retention roots.

Frozen manifests включают complete exact closure; durable pins не истекают по PID/
возрасту. Runtime snapshot handles/streams имеют отдельное ownership; snapshot Dispose
блокирует новые opens, stream сохраняет manifest/root до actual close. Entered read,
concurrent close/stop и reopen проверены. Metadata checkpoint сохраняет новые records.

GC dry-run marks heads/manifests/retained committed outputs и exact dependencies.
Порции≤2048revisions/2048objects с HasMore; parents-before-dependencies предотвращают
dangling metadata между порциями. Shared CAS защищён всеми оставшимися revisions.
Stale/foreign/inventory-changed plan не пишет; reparse/неизвестные имена не удаляются.
Atomic prune+durable deletion intents предшествуют CAS delete; ack снимает только
успешные intents. Delete/cancel/unknown сохраняют recoverable whole state. Open
проверяет cross-references до tail/staging mutation, CAS queue автоматически не
исполняет; explicit CollectGarbageAsync() продолжает её. Resource publication до
завершения queue запрещена. Prune/ack capacity проверяется до первого mutation/delete.

Фактические проверки Windows x64, SDK10.0.403/runtime10.0.14, xUnit2/VSTest:

- **628/628 каждый Debug/Release и solution-filter**, failed/skipped0;92newcases,
  38methods/293direct assertions +3fixture assertions.21прежний testfile byte-unchanged.
  Independent literal wire goldens, every-prefix truncation/noncanonical02,
  10authenticated domain corruptions с exact filesystem/no-repair/actual close,
  seeded page model,4096/4097members,32768/32769edges,1MiB/nextbyte response,
  64/65snapshot leases,2048+1objects и2050revision chain в двух GC slices.
  10unknown prune/ack write/flush cases; delete before/after actual IO/cancel sides.
  Exact GC append budget492vs491bytes и resume196vs195bytes подтверждены без mutation
  при отказе. Nom/Tests warnings0.
- **104console groups/profile** source Debug и published Release:18Core+6KV+41Fault+
  27Maintenance+12Retention. Новый Retention —12started=killed=reaped children/profile
  на prune write/partial/flush, CAS delete и ack write/partial/flush. Каждый reopen
  сохраняет pinned old dependency bytes/current heads, explicit resume завершается,
  unpin/remove разрешает только intended GC. Повторены прежние KV6started/reaped,
  4kills; Fault48kills/reaps; Maintenance24kills/reaps. Core4resource kills отдельно.
- **27public App checks/profile**, independent OS TEMP AssemblyName NomPublicConsumer,
  no friend access, actual App/Cobalt factory/UseNom, source Debug/published Release.
  Frozen dependencies, tags/reverse, reimport+prune+durable pin+reopen, exact old
  2MiB+17bytes, stream retention и GC/post-GC checkpoint доказаны public APIs.
- Probe Nom/Contracts graph восстановлен с empty feed/cache:0external packages.
  Public App graph имеет Roslyn build dependencies; no LiteDB restored/runtime/output/
  loaded module checks прошли. Проверены exact module hashes и source drift0.
- Nonincremental solution Debug build: **0errors/1555warnings**, Nom/Tests/Probe0warnings.
  Solution-filter обнаружил628Nom cases; прочие suites не исполнялись/full green не заявлен.
  Engine/UI/native/GPU/fresh NuGet/RSS/performance не запускались.

Повторяемые команды из src (каждому runner нужна новая evidence directory):

```powershell
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug --logger 'trx;LogFileName=debug-final.trx' --results-directory TestResults/Nom5-20261108 '-bl:TestResults/Nom5-20261108/Debug-Final-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Release --logger 'trx;LogFileName=release-final.trx' --results-directory TestResults/Nom5-20261108 '-bl:TestResults/Nom5-20261108/Release-Final-{}.binlog'
pwsh -NoProfile -File ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/Nom5-20261108/Probe-Final
pwsh -NoProfile -File ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -EvidenceDirectory TestResults/Nom5-20261108/PublicApp-Final
dotnet build TrueMoon.slnx -c Debug --no-incremental -m '-bl:TestResults/Nom5-20261108/Solution-Final-{}.binlog'
dotnet test TrueMoon.slnx -c Debug --no-build --filter 'FullyQualifiedName~TrueMoon.Nom.Tests' --logger 'trx;LogFilePrefix=nom-discovery' --results-directory TestResults/Nom5-20261108/Discovery '-bl:TestResults/Nom5-20261108/Discovery-{}.binlog'
```

Raw ignored artifacts: TestResults/Nom5-20261108 (TRX/logs/binlogs/JSON, frozen hashes,
Before/BeforeDocs snapshots). Они сохранены локально, не являются package deliverable
и не гарантированы в новом checkout; этот HISTORY и versioned runners сохраняют
scope/commands/outcome. Ранние debug-first/second failures относились к новым fixture
lock/header reads и исправлены; debug-slices имел xUnit2031, finalD/R warnings0.
Intermediate research/plan/status только OS TEMP, не входят в intended commit.

Physical isolated-volume disk-full/power-loss NOM3/4 остаются открыты: последнее
New-VHD Windows authorization denial записано в NOM4, **в NOM5 не повторялось**.
NOM5 не закрывает этот gate и не заявляет unsupported FS/RID guarantees. Полный bounded
metadata index rebuild на content commit остаётся измерить/оптимизировать в NOM6;
logical64MiB budget не равен RSS. Backup/export/format evolution — NOM6; IsPackable=false,
fresh NuGet/second consumer/RID delivery — NOM7. Engine f.3→f.4/importer g.1 и соседние
Editor изменения сохранены. Commit/push этим запросом не выполнялись.

<a id="nom-status-before-nom5-2026-11-08"></a>
## Предыдущий STATUS перед NOM5 — дословно
# TrueMoon.Nom: состояние модуля хранения

**2026-11-08 — NOM4 checkpoint/explicit rotation/offline compaction реализованы и
проверены в Windows x64 IO/process scope.** [Контракт](COMPACTION.md#nom4-compaction-2026-11-08),
[команды/evidence](HISTORY.md#nom4-2026-11-08), [очередь](PLAN.md#nom-own-queue).

| Область | Реализация | Фактические проверки |
| --- | --- | --- |
| Checkpoint/recovery | Bounded NOMCKP01, root-selected seed + exact successor chain, strict digests/schema/quotas |Independent golden + authenticated corruption/truncation/order/budget cases; missing/corrupt cp не вызывает repair/cleanup/fallback |
| Compaction | Prepare/flush/close/read verify → root rename → index switch → old handles/files cleanup |30injected prep/switch cases, prepared-file corruption witnesses, cancellation sides, actual close/delete retry, deleted/empty state и post-cp append |
| Public API/ownership | CompactAsync offline, retained resource tickets/CAS/floor, no-op repeat, CleanupPending; fault требует reopen |Read leases/reservation rejection, old/current CAS exact bytes, один App stop ждёт вошедшую IO; logical generation не меняется |
| Unit regression |536/536 каждый Debug/Release и solution-filter,0failed/skipped |110new cases над426NOM3; новые Nom/Tests warnings0 |
| Process proof |Maintenance27groups/profile,24actual killed/reaped children |Повторены Core18+KV6+Fault41; всего92groups source Debug/published Release; source hashes drift0 |
| Public consumer/dependencies |Independent App source/published16checks/profile, без friend access; own core graph0packages |Checkpoint reopen exact2MiB+17bytes, durable public outcomes, post-cp append; restored/runtime/fresh outputs без LiteDB |
| Solution |Nonincremental Debug build |0errors/1555warnings, Nom/Tests/Probe0warnings; full suite/engine/GPU не объявлены проверенными |
| Physical acceptance |NOM3/NOM4 isolated-volume disk-full и power-loss открыты |Свежий New-VHD64MiB denied Windows authorization, Administrator=false; file/volume не создан/не подключён/не форматирован |

Следующий implementation этап — **NOM5: domain indexes, snapshots/pins и offline GC**.
Physical disk-full требует разрешённого отдельного окружения; это environmental gate,
не функциональная зависимость всех новых features. Полная physical acceptance не закрыта.
Maintenance explicit, automatic scheduling нет. Checkpoint≤256MiB, file scan≤1024
с2reserved slots; oversized live checkpoint явно отказывается до записи. Physical KV
tickets≤checkpoint sequence Expired; public resource tickets сохраняются до explicit
prune. Retired handles/root остаются owned до actual release. Backup/export/performance
— NOM6, fresh NuGet/RID — NOM7, IsPackable=false. Hostile/reparse FS и device-loss
гарантии не расширены. Old LiteDB roots не мигрируются. Full suite/GPU/performance/
fresh packages не запускались; прежние engine failures и очередь engine сохранены.

Предыдущий STATUS сохранён [дословно](HISTORY.md#nom-status-before-nom4-2026-11-08).
<a id="nom6-2026-11-08"></a>
## NOM6: verified backup/restore, format protocol, limits и workload — 2026-11-08

Реализован [backup/current-format restore](BACKUP.md#nom6-backup-2026-11-08): explicit
immutable directory, NOMBK001 canonical manifest, selected KV authority/all stored
revision CAS, source unchanged. Prepared files Flush(true)/actual close/hash verified,
independent KV+domain/journal/closure verifier до same-parent directory publication.
Новая public typed restore factory зарегистрирована UseNom через существующий host;
retained owner cleanup/Stop и actual handle release сохранены. Unknown formats, existing
destinations, overlap/UNC/reparse и limits отказываются без reset. Lost publication ack
сохраняет whole prepared/published state; до publish cancel возвращает OCE/diagnostic
stage, после acknowledged move не relabel-ит успех. Future migration — explicit offline
converter/verify/new-root protocol, не фиктивный converter несуществующего KV-v2.

NOM6 измерил два [полных corpus и streaming](PERFORMANCE.md#nom6-performance-2026-11-08).
Исправлены repeated full-key top-K scan на каждой странице (sorted key cache/binary
range), повторное чтение уже detached page values и redundant metadata decode/re-encode
при revision validation. Schemas/hash domains/golden bytes неизменны. DomainIndexBytes
настраивается через copied UseNom options1byte..256MiB, default64MiB сохранён; большой
50k/100k/300k corpus реально проверен с explicit256MiB, default admission отказывает.
Полный bounded domain index pass при content commit остаётся; стоимость опубликована,
incremental update не объявлен реализованным. No RSS/latency SLA или LiteDB speedup claim.

Фактические проверки Windows x64, SDK10.0.403/runtime10.0.14, xUnit2/VSTest:

- **718/718 каждый Debug/Release и solution-filter**, failed/skipped0.90newcases,
  31methods в6newfiles;29прежних .cs testfiles byte-unchanged. Nom/Tests warnings0.
  Literal independent288-byte manifest golden/everyprefix/everybyte mutation,
  authenticated invalid header/entries/path/order/closure, count100256/100257,
  transfer exact bytes/file quotas, metadata admission639/640, source/pins/outcomes/
  floor/tombstone snapshots, source/prepared corruption и old→new source isolation.
  14prepubIO +4publication acknowledgement cases, cancellation sides, real enteredcopy
  Stop/retained actualclose failures; optimized pages prefix/after/bytes/update/cp/reopen.
- **120functional console groups/profile**, source Debug и published Release:
  18Core+6KV+41Fault+27Maintenance+12Retention+16Backup. Новый Backup profile:
  16started=killed=reaped children/profile на export/restore write/partial/flush/close/
  directory publish. Source exact hashes не меняются, final root появляется только
  после publication, recovery сохраняет pinned bytes/current heads/IDs/generation.
  Предыдущие Core/KV/Fault/Maintenance/Retention profiles повторены, no module/source drift.
- **4actual Capacity groups/profile**, source Debug/published Release:128KiBvalue/
  1024key/4MiBpayload/8192mutations,2000000keys+reopen, exact268435456index bytes и
  actual268435456-byte checkpoint+reopen,65536durable Flush(true) batches. Везде
  immediately adjacent refusal до IO/state change; compaction восстанавливает budget.
- **34public App checks/profile**, independent no-friend OS TEMP source/published
  consumer с actual App/Cobalt, двумя typed factories, early product disposal и host
  cleanup. Backup/restored source identities/exact versions/dependencies/pins/stream
  bytes сохранены; обычный Open immutable backup отвергнут. Это ProjectReference,
  не NuGet delivery. Core Nom/Contracts graph empty feed/cache:0external packages.
- **Published Release Performance completed**, exact1k/4k/16k и50k/100k/300k seeds,
  empirical p50/p95/allocations/RSS/private/disk amplification,5fresh-process opens
  на corpus (10started=reaped, no kill). Streaming16/128MiB; actual2×512MiBobjects
  в1GiBbatch и verified open; maxima/admission count/default refusal. [Все числа](PERFORMANCE.md).
  OS cache uncontrolled, не physical cold-disk; samples3/5/9, p95 nearest-rank=max.
- Nonincremental solution Debug **0errors/1555warnings**, Nom/Tests/Probe0warnings.
  Solution-filter выполнил718Nom tests; прочие suites не исполнялись. Full Aether/
  UI/native/GPU/fresh packages в этом срезе не запускались; соседний ED5a.2 сохранён.

Финальные команды из src (runner evidence directories должны быть новыми):

```powershell
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug --logger 'trx;LogFileName=debug-final.trx' --results-directory TestResults/Nom6-20261108 '-bl:TestResults/Nom6-20261108/Debug-Final-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Release --logger 'trx;LogFileName=release-final.trx' --results-directory TestResults/Nom6-20261108 '-bl:TestResults/Nom6-20261108/Release-Final-{}.binlog'
pwsh -NoProfile -File ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/Nom6-20261108/Probe-Final
pwsh -NoProfile -File ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -Profile Capacity -EvidenceDirectory TestResults/Nom6-20261108/Capacity-Final
pwsh -NoProfile -File ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -Profile Performance -EvidenceDirectory TestResults/Nom6-20261108/Performance-Final
pwsh -NoProfile -File ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -EvidenceDirectory TestResults/Nom6-20261108/PublicApp-Final
dotnet build TrueMoon.slnx -c Debug --no-incremental -m '-bl:TestResults/Nom6-20261108/Solution-Final-{}.binlog'
dotnet test TrueMoon.slnx -c Debug --no-build --filter 'FullyQualifiedName~TrueMoon.Nom.Tests' --logger 'trx;LogFilePrefix=nom-discovery' --results-directory TestResults/Nom6-20261108/Discovery '-bl:TestResults/Nom6-20261108/Discovery-{}.binlog'
```

Ignored raw artifacts сохранены локально в TestResults/Nom6-20261108:TRX/logs/binlogs/
JSON, source freezes/Before snapshots, measured baseline DLL. Их перенос в новый
checkout не гарантирован; versioned runners и этот HISTORY сохраняют scope/commands/
outcome. Research/plan/status только OS TEMP, не intended commit. Ранние compile issues
(default interface factory diamond/type inference) исправлены; quota assertion и
OCE diagnostic propagation исправлены до final passing runs. Исторические initial/
ordered/codec/first profiles не подменяют финальные freeze proofs.

Physical isolated-volume disk-full/power-loss NOM3/4 открыты; прежний Windows New-VHD
authorization denial здесь не повторялся. Checksums не являются hostile-input signature.
Backup staging не удаляется автоматически. IsPackable=false; следующий этап NOM7:
fresh isolated NuGet/second consumer/RID delivery. Engine f.3→f.4/importer g.1 и
соседние Editor планы/код сохранены. Commit/push запросом не выполнялись.

<a id="nom-status-before-nom6-2026-11-08"></a>
## Предыдущий STATUS перед NOM6 — дословно
# TrueMoon.Nom: состояние модуля хранения

**2026-11-08 — NOM5 indexes/snapshots/pins/offline GC реализованы и проверены в
Windows x64 IO/process scope.** [Контракт](RETENTION.md#nom5-retention-2026-11-08),
[команды/evidence](HISTORY.md#nom5-2026-11-08), [очередь](PLAN.md#nom-own-queue).

| Область | Реализация | Фактические проверки |
| --- | --- | --- |
| Metadata/indexes | NOMAST02 tags/exact deps, прежние01 bytes/hash/constructor сохранены; indexed ID/source/kind/tag pages и forward/reverse/closure | Golden/truncation/order/limits, seeded query model/reopen, cursor sequence/filter,4096/4097members и32768/32769edges |
| Identity/snapshots/pins | Tombstones retain ID/binding; frozen complete manifests, durable explicit pins, owned runtime handles/streams | Reimport+outcome prune+checkpoint+reopen сохраняют old exact bytes; actual entered-read close/Stop удерживает root до release |
| GC | Bounded dry-run/HasMore slices, parents-first prune, durable deletion queue и explicit resume |2050revision chain/2049objects, shared CAS, unknown names, stale plan, delete/cancel/10unknown commit cases; no dangling live refs |
| Recovery/limits | Новые schemas/cross-references до tail repair; queue не исполняется автоматически при Open |10authenticated corruptions: exact files/no repair/no delete; append492/491 и resume196/195byte boundaries |
| Unit regression |628/628 каждый Debug/Release и solution-filter,0failed/skipped |92newcases/38methods;21прежний testfile unchanged, Nom/Tests warnings0 |
| Source/published |104groups/profile =18Core+6KV+41Fault+27Maintenance+12Retention |12actual Retention kills/reaps/profile; прежние profiles повторены, source hashes drift0 |
| Public App/dependencies | Independent no-friend App27checks/profile; core graph0external packages | Source Debug/published Release old2MiB+17bytes/pins/reimport/GC/checkpoint; no LiteDB restored/runtime/output |
| Solution | Nonincremental Debug build |0errors/1555warnings, Nom/Tests/Probe0warnings; full suite/engine/GPU не заявлены проверенными |

Следующий implementation этап — **NOM6: backup/export, format evolution, limits и
performance**, включая стоимость полного metadata index rebuild на content commit.
Physical disk-full/power-loss NOM3/4 остаются открыты (предыдущий NOM4 New-VHD denied
Windows authorization; в NOM5 не повторялся). Env gate не закрыт IO/process proof.
Snapshots≤256, pins≤1024, runtime handles≤64, closure≤4096/32768edges; domain index
≤100kassets/200krevisions/400kedges/64MiB logical accounting, это не RSS bound.
GC explicit/offline,≤2048revisions/objects за slice, HasMore требует нового plan;
cleanup queue блокирует resource publication до explicit resume. Automatic scheduling нет.
Checkpoint≤256MiB, inventory≤100256entries; oversized operations явно отказываются
до mutation. Hostile/reparse FS/power loss/RID не расширены. IsPackable=false, delivery — NOM7.

Предыдущий STATUS сохранён [дословно](HISTORY.md#nom-status-before-nom5-2026-11-08).

<a id="status-before-core75-2026-11-10"></a>
## Полный STATUS перед CORE7.5 — 2026-11-10

# TrueMoon.Nom: состояние модуля хранения

**Предкоммитная сверка2026-11-08:** повторены862/862 Nom tests каждый Debug/Release,0failed/0skipped. Код не менялся; [scope/команды](HISTORY.md#nom-commit-2026-11-08). Остальная таблица ниже — предыдущие проверки source organization.

**2026-11-08 — исходники организованы по функциональным каталогам.**
[Карта namespace и совместимость](STRUCTURE.md#nom-source-layout-2026-11-08):
Catalog, Resources, Retention, Backup, Hosting, Storage и Storage/Format.
60файлов перенесены; NomLifecycle выделен в Hosting, итого66C# файлов.
UseNom/общий store facade/open arguments остаются в корне. Public subsystem types
получили новые namespace; clients требуют обновления imports и пересборки.
Implementations/tests/lifetime/format/dependencies сохранены; [история и команды](HISTORY.md#nom-structure-2026-11-08).

| Проверка текущего запуска | Результат |
| --- | --- |
| Source layout/body audit |66namespace/path pairs,65прежних implementations+extraction и64consumer C#/template bodies без using/namespace/whitespace совпадают; partial groups согласованы |
| Full Nom xUnit Debug/Release |862/862 каждый,0failed/0skipped; новых test cases нет |
| Functional regression |120groups/profile source Debug/published Release, actual killed/reaped children |
| Public source consumers |App34/Buffer28/Raw31checks каждый Debug/published Release |
| Fresh NuGet delivery |4packages/4consumers/8fresh success graphs; App34/Buffer28/Raw31/Tool211checks каждый portable Debug/published Release/win-x64 FDD Release; namespace/internal/managed-T/package guards прошли |
| Durability template |Только compile Debug/published Release из fresh packages; не исполнялся |
| Full solution nonincremental Debug |0errors/1560warnings; Nom/Nom.Tests0warnings |

NOM0–NOM8 реализованы в описанных local scope; byte/raw API работает из Resources,
snapshots/GC — Retention, backup/restore — Backup, host/options — Hosting. Формат и
bytes не зависят от CLR namespace. Следующий consumer bridge — engine ARCH1g.2 после
g.1 importer adapter, по согласованию; engine/UI/release очередь сохраняется.

Actual VM crash verification отложена пользователем; новых VM/disk-full trials0.
Hardware power-off исключён. Прежние UAC disk-full5cases/166assertions и VM protocol
proofs, heavy NOM6 workload/capacity — история, здесь не повторялись. Другие
OS/RID/AOT/trimming/self-contained/external publish не подтверждаются этой задачей.
[Предыдущий STATUS](HISTORY.md#nom-status-before-structure-2026-11-08) сохранён после
точной адаптации source-link targets; остальной текст и датированные результаты сохранены.

<a id="status-before-core76-2026-11-10"></a>
## Полный STATUS перед CORE7.6 — 2026-11-10

# TrueMoon.Nom: состояние модуля хранения

**2026-11-10 — CORE7.5: общая конфигурация admission settings.**
NomConfigurationSchema.Default/UseConfiguredNom готовят8startup scalar fields,
передавая проверенные копии существующим host/factory owners. Project/store
identity и roots остаются явными runtime arguments; startup не открывает store.
[Guide](../core/CONFIGURATION_MODULES.md).

Текущий запуск: **874/874 каждый Debug/Release, failed/skipped0;12newcases**.
Actual store quota/close/reopen, source/callback priority, independent copies,
invalid-before-IO, restart rejection и App independence проверены.
Combined source Debug/published Release/fresh16package module consumer:
по76checks на обоих DI, включая реальные Nom store admission/drain; window/GPU/
child runtime не запускались. [Команды и scope](HISTORY.md#core75-2026-11-10).

NOM0–NOM8 реализованы в прежнем local scope; source organization и namespace map
сохранены по [STRUCTURE](STRUCTURE.md#nom-source-layout-2026-11-08).
Формат/byte contracts/dependencies не менялись. [Предыдущий STATUS целиком](HISTORY.md#status-before-core75-2026-11-10).

Следующий consumer bridge — engine ARCH1g.2 после g.1 importer по согласованию;
общая engine/UI/release очередь сохраняется. Actual VM crash verification отложена
пользователем, hardware power-off исключён. Новых VM/disk-full/capacity trials нет;
прежние UAC disk-full/VM/heavy workload proofs остаются историческими.
Другие OS/RID/AOT/trimming/self-contained/external publish не подтверждаются.

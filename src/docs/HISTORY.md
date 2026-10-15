# TrueMoon: история общей документации

[Общий статус](STATUS.md), [план](PLAN.md), [индекс документов](README.md), [формат ведения](PLANNING.md). Подробные implementation/test/native записи принадлежат HISTORY соответствующего модуля; эта запись не заменяет их результаты.

## Общий README и навигация — 2026-10-11

По запросу пользователя сверены общий README, source projects/ProjectReference graph, solution, SDK/package/test настройки, общие PLAN/STATUS/PLANNING и локальные планы/статусы Core/Cobalt/Enerit/Mithril/Alloy. Корневой README сохраняет английский язык и расширен: назначение,14существующих module/runtime/generator проектов в8областях, зависимости, build/test команды из src, headless и standalone UI quickstart, local NuGet consumer, examples, license, readiness и deployment ограничения. Старые UseSignals/UseSignalService/ListenSignalService отсутствуют в текущем source API; README теперь указывает Enerit UseInvocationService/ListenInvocationService/PipesServiceTransport и builder-level Microsoft DI selection. Существующий MithrilTest приведён ссылкой как composition example с открытым own-process proof, не как подтверждённый запуск.

Добавлен `docs/README.md` с общей/модульной навигацией и практическими guides. В PLAN текущий порядок сведён в раздел развития; дублирующие UI записи убраны из общей release criteria section. Согласованные G1–G6/R1–R6, локальные критерии и architecture/dependencies не изменены. PLANNING уточняет роль root README/index как навигации, не второго roadmap. Общий и UI STATUS отделяют текущую документационную сверку от предыдущих package/native/test runs.

Проверки:

- Два **точных** fenced C# snippets из нового root README извлечены в isolated projects с собственными build/CPM settings. Headless имеет PackageReference на TrueMoon/Extensions.DependencyInjection, UI дополнительно Hosting. Использованы существующий local feed и version `0.2.2.7-alpha.consumer.20261011065747061` предыдущего UI package run, SDK10.0.401/net10.0/Windows/Debug. Оба `dotnet build` завершились exit0/0warnings/0errors. Это compile proof public API examples, не отдельный новый headless consumer lifecycle/COB4 proof.
- Проверка текущих Markdown документов: **26documents/339local links/76heading links**, problems0; существование локальных file/directory links, heading anchors, balanced code fences, conflict markers/NUL. Исторические модульные HISTORY записи не проверялись как текущие документы, но anchors ссылок на них разрешались. Результаты и exact scope сохранены в `links.json`; добавленные после первого прохода статусы/общая история включены в финальную проверку. `git diff --check` exit0.
- Артефакты ignored `TestResults/Documentation/Readme20261011`: подготовленный README, extracted source/projects, build logs, local settings, link-check script/report и diff-check log. Корневой README записан в явно запрошенный пользовательский файл; другие файлы вне src не менялись.

Приложения из README, unit/full solution tests, pack/publish и native/GPU checks в этом шаге не запускались. Исторические результаты остаются датированными; alpha/NVIDIA K7 blocker и module readiness не переоценивались. Команда `dotnet test` в README сверена с xUnit2/VSTest/global.json; пустая Mithril discovery не объявляется успешным suite. Общий root README включается в некоторые nupkg через Directory.Build.props, но артефакты предыдущего package run не пересобирались ради документационной правки; UI Hosting/renderer имеют самостоятельные PACKAGE.md.


## Сводка до quality matrix — 2026-10-15

Ниже сохранён прежний общий STATUS целиком; это архив результатов, а не новый запуск.

<details>
<summary>Прежняя сводка и датированный журнал</summary>

# TrueMoon: общий статус

Последняя работа — **2026-10-15, Mithril MIT5 operational diagnostics**.
Структурированная process trace через собственный IEventsSource: run/generation/PID,
effective policy/budget/delays, launch/exit/restart/terminal и cleanup outcomes.
Mithril94/94 и Enerit126/126 в каждой Debug/Release:4current TRX/440passed,
Failed0/Skipped0.40restart +18shutdown apphost/published cases и fresh package108/108
прошли, Completed=true;96package cases проверяют payload через App.UseDiagnostics,
12abrupt-parent-exit cases не наблюдают sink убитого parent.8nupkgs/9projects,
negative missing-analyzer CS0234;205package source fingerprints совпали.
Full nonincremental Debug build0errors/1193warnings. Core/Cobalt/generator suites,
UI/native и performance не повторены. [Scope/ограничения](mithril/STATUS.md),
[команды/evidence](mithril/HISTORY.md#mit5-operational-diagnostics--2026-10-15).
MIT5 закрыт в bounded Windows x64/net10.0 same-build/cooperative scope. Далее
сводная quality matrix модулей и оставшийся UI acceptance перед выбором ENG0;
новые engine/editor проекты этим шагом не создаются.

Предыдущая работа — **2026-10-15, Enerit ENE5 measured pipes baseline**.
10typed payload/concurrency cases,10verify +30fresh client/server pairs,
90samples/571944RPC;104source/10binary/4generated fingerprints совпали, power plan
не менялся. [Числа/ограничения](enerit/BASELINE.md),
[команды/scope](enerit/HISTORY.md#ene5-pipes-performance-baseline--2026-10-15).
Scalar/c1: median RTT P50183.9µs,4896calls/s,client2512B/call; c8:779.3µs,
9062calls/s,2214B/call. Межзапусковый spread сохранён, не SLA/speedup.
Runtime126/generator114 в каждой Debug/Release,4TRX/480passed/0failed/0skipped.
Full nonincremental Debug build0errors/1193warnings;10дополнительных CS8669 от
generated benchmark не подавлены. Runtime/generator API/logic не менялись;
fresh package108 и собственные Core/Cobalt/Mithril suites/UI/native не повторены.
ENE5 measurement часть закрыта в bounded same-build Windows x64/net10.0 scope;
package portion имеет отдельную прежнюю дату. Далее Mithril MIT5 diagnostics,
allocation/CPU profile Enerit — отдельный measured backlog, engine ещё не начинается.

Предыдущая работа — **2026-10-15, Core headless quality proof**.
Согласованный Core quality проход завершён в bounded Windows x64/net10.0 scope:
configuration/bootstrap, diagnostics и App/JSON/реальный FileStream через текущие
sources и свежие независимые пакеты. [Scope/ограничения](core/STATUS.md),
[команды/результаты](core/HISTORY.md#сквозной-headless-proof--2026-10-15).
52source +80package +3README processes прошли Debug/Release build/publish;
130/127source fingerprints совпали, negative opt-out expected CS0246. Cobalt7
scenarios включает startup+Dispose failure; Microsoft DI6compatibility scenarios
сохраняют native semantics. Core107/107 в каждой Debug/Release, Failed0/Skipped0,
current2TRX/214passed. Full nonincremental Debug build0errors/1183warnings.
Production/runtime API не менялись; другие module suites/IPC/units/UI/native и
performance не повторены. Далее Enerit quality/ENE5, затем Mithril MIT5 diagnostics;
engine ждёт сводки остальных модулей, общий выпуск не закрыт.

Предыдущая работа — **2026-10-15, Core diagnostics quality**.
Исправлены удержание completed DiagnosticListener и старые registrations при
повторном UseDiagnostics. Subscription completion/late handle cleanup, filters,
callback Dispose и независимое освобождение нескольких Apps проверены.
[Контракт](core/PLAN.md#проход-качества-до-engineeditor--2026-10-15),
[команды/scope](core/HISTORY.md#diagnostics-quality--2026-10-15).
Core107/107 в каждой Debug/Release, Failed0/Skipped0, current2TRX/214passed;
diagnostics18cases в каждой конфигурации входят в107. Full nonincremental Debug
solution build exit0/errors0/warnings1183. Windows x64/net10.0/SDK10.0.402.
Другие module suites/package/IPC/units/UI/native/performance не повторены.
Далее цельный headless proof и сводка Core перед Enerit; engine ещё не начинается.

Предыдущая работа — **2026-10-15, Core configuration/bootstrap quality**.
Исправлен GetOrCreate при маскирующем null в именованной секции; новый объект
остаётся доступен повторным чтениям и сохраняется в JSON при JSON source.
Проверены порядок configuration/registrations/factories на Cobalt/Microsoft DI,
validation failure, whole-refresh failure/recovery и JSON serialization/sharing
errors/retry. [Контракт и следующий scope](core/PLAN.md#проход-качества-до-engineeditor--2026-10-15),
[команды/границы](core/HISTORY.md#configuration-и-bootstrap-quality--2026-10-15).
Core97/97 в каждой Debug/Release, Failed0/Skipped0, current2TRX/194passed;
ConfigurationTests27/27 отдельно Debug. Full nonincremental Debug build
exit0/errors0/warnings1184. Windows x64/net10.0/SDK10.0.402. Другие module suites,
package/IPC/units/UI/native/performance не повторены. Core quality частично:
далее diagnostics, затем цельный headless proof; порядок модулей сохраняется.

Предыдущая работа — **2026-10-15, Cobalt COB7: provider comparison**.
Один unchanged App graph проверен на Cobalt/Microsoft DI:20functional scenarios,
20Dry/20paired default measurements и24fresh startup samples, Completed=true.
[Решение и числа](cobalt/COMPARISON.md), [scope/команды](cobalt/HISTORY.md#cob7-provider-comparison--2026-10-15).
Cobalt остаётся existing default; **массовая migration explicit DI consumers отложена**.
Регистрация одинакова, Cobalt даёт structured path/unique cleanup, MS дешевле в hot
resolution. Cobalt App lifecycle16.91us против19.32us MS; singleton52.10ns/32B против
14.91ns/0B.127source fingerprints/power plan совпали; Ryzen9 9900X/Windows11 x64/
SDK10.0.402/.NET10.0.13. General release не объявлен.
Cobalt179/generator91/Core81 Release:3TRX/351passed/0failed/0skipped. Full Release
build exit0/errors0/warnings1185. Fresh package64+3, identical-graph consumer
12processes/120scenarios иlibrary/app4/2diagnostic controls прошли. Runtime/Contracts/
generator/adapter logic и provider choices не менялись. Debug unit suites/IPC/units/
UI/native не повторены; UI migration по headless proof не выполняется.
COB6a–d/COB5/COB7 закрыты в bounded scope; далее Core quality.

Предыдущая работа — **2026-10-14, Cobalt COB6b: runtime errors и recovery**.
Cycle/closed type guard, structured missing/depth/stack errors, immutable dependency
path и original factory/constructor exceptions реализованы. Failed/null singleton
retry и completed dependency ownership проверены. [Контракт](cobalt/PLAN.md#cob6b-runtime-errors-и-recovery--2026-10-14),
[команды/артефакты/границы](cobalt/HISTORY.md#cob6b-runtime-errors-и-recovery--2026-10-14).
Cobalt84/generator62/Core81 в каждой Debug/Release:6TRX/454passed/0failed/0skipped.
Fresh main package64/64 +3README, library/app4/4 +legacy1/1 и2constructor negative
builds; opt-out expected CS0246. Full nonincremental Debug build exit0/errors0/
warnings1187. Source manifest127files/mismatches0; packages local/unpublished.
Windows x64/net10.0/SDK10.0.402. Other module suites, IPC/units, UI/native/performance
не повторены. Далее COB6c registration contracts; полный COB6 и migration открыты.

Предыдущая работа — **2026-10-14, Cobalt COB6a: конструкторы и diagnostics**.
Закреплены unique longest public constructor, closed/open nullable/default semantics
и COB001–COB006 на registration call; internal types/factories/required members
проверены. [Контракт](cobalt/PLAN.md#cob6a-конструкторы-и-diagnostics--2026-10-14),
[команды/артефакты/границы](cobalt/HISTORY.md#cob6a-конструкторы-и-diagnostics--2026-10-14).
Generator62/Cobalt37/Core81 в каждой Debug/Release:6TRX/360passed/0failed/0skipped.
Fresh main package64/64 +3README, library/app4/4 +legacy1/1,2expected negative
constructor builds; opt-out expected CS0246. Full nonincremental Debug build
exit0/errors0/warnings1191. Source manifest127files/mismatches0; packages local.
Windows x64/net10.0/SDK10.0.402. IPC/units, прочие suites, UI/native/performance
не повторены; далее COB6b runtime cycles/error paths, полный COB6 открыт.

Предыдущая работа — **2026-10-14, единый Cobalt package и принятый TrueMoon scope**.
Cobalt runtime/generator остаются двумя projects, поставляются одним nupkg;
analyzer delivery автоматический direct/transitive, opt-out EnableCobaltGenerator=false.
Microsoft DI ecosystem bridge/соглашения не являются целью; существующий adapter
сохраняется. [Контракт](cobalt/PLAN.md#принятый-scope-и-один-пакет--2026-10-14),
[команды/scope](cobalt/HISTORY.md#единый-пакет-и-truemoon-scope--2026-10-14).
Fresh headless64/64 +3README, library/app4/4 +legacy1/1, App/IPC/units108/108;
negative controls expected. Cobalt37/generator16/Core81 отдельно Debug/Release:
6TRX/268passed/0failed/0skipped. Full nonincremental Debug build exit0/errors0/
warnings1227. Packages local, не опубликованы; UI/native/performance и остальные
module suites не повторены. Далее COB6a constructor diagnostics/selection.

Предыдущая работа — **2026-10-14, начало качества Cobalt и Try helper generation**.
По решению пользователя до engine/editor идёт качество существующих модулей;
начинаем с Cobalt: COB6 → COB5 → COB7 и условная миграция Microsoft DI consumers.
[Критерии и порядок](PLAN.md#качество-модулей-до-engineeditor--2026-10-14),
[Cobalt contract](cobalt/PLAN.md#качество-cobalt-до-engineeditor--решение2026-10-14).
Исправлена генерация для TrySingleton/TryTransient helpers; generator16/16,
Cobalt37/37 и Core81/81 отдельно Debug/Release — **6TRX/268passed/0failed/0skipped**.
Windows x64/net10.0/SDK10.0.402. Full nonincremental Debug solution build exit0,
errors0/warnings1227. Package/IPC/Mithril/UI/native checks не повторены; прежние package fingerprints
не являются подтверждением изменённого generator. Далее COB6a constructor diagnostics.
[Команды и scope](cobalt/HISTORY.md#cobalt-quality-и-try-helpers--2026-10-14).

Предыдущая работа — **2026-10-14, MIT5/ENE5 independent package portion**.
9local packages и независимый PackageReference App/IPC/units consumer:
**108/108cases** Debug/Release ×build/publish, Cobalt/DI и DI без Cobalt analyzer;
Completed=true. Actual package provenance/version,compiler analyzer path/SHA и
runtime output проверены; omitted Enerit analyzer даёт ожидаемый CS0234.
Packages не опубликованы. Source fingerprints current/mismatches0.
Windows x64/net10.0/SDK10.0.402: Enerit126/generator114,Mithril89,Core81,
Cobalt37/generator6 в каждой Debug/Release configuration — **12TRX/906passed**,
Failed0/Skipped0. Full non-incremental Debug solution build exit0/errors0/warnings1227.
[Контракт](mithril/PLAN.md#mit5ene5-independent-package-contract--2026-10-14),
[команды и границы](mithril/HISTORY.md#mit5ene5-independent-package-consumer--2026-10-14),
[runner](../ManualTests/UnitsPackageConsumer/README.md).
Это package portion; **ENE5 performance и MIT5 operational diagnostics открыты**.
На момент package portion следующий функциональный шаг — фиксированный IPC latency/throughput/allocations
baseline до оптимизаций, затем process diagnostics. Same-build/cooperative scope;
UI/native/MMF/other OS/RID/NativeAOT/stress/uncooperative shutdown не подтверждены.

Предыдущая работа — **2026-10-14, Enerit strict response/decode ownership**.
Generated responses потребляют payload полностью; explicit decode scope возвращает
nested byte rentals при failure, включая DTO dictionary keys и более поздние
arguments. Successful caller/service ownership и DTO prefix API сохраняются.
Empty ack, canonical bool0/1, strict error UTF-8 и invalid dictionary keys проверены.
Windows x64/net10.0/SDK10.0.402: **Enerit126/126, generator114/114, Mithril89/89
отдельно Debug/Release**, Failed0/Skipped0; current6TRX/658passed.
App/IPC/restart **36/36** Cobalt/DI и isolated pipes **12/12** Debug/published
Release повторены, Completed=true; fingerprints current/mismatches0.
Final non-incremental Debug solution build exit0/errors0/warnings1227.
[Контракт](enerit/PLAN.md#ene1ene2-strict-response-и-decode-ownership--2026-10-14),
[команды и границы](enerit/HISTORY.md#strict-response-и-decode-ownership--2026-10-14).
Core/Cobalt suites, UI/native/independent packages/performance не повторены;
ранние результаты этого же дня принадлежат предыдущему MIT4 run ниже.
Same-build/cooperative scope, killed parent client cleanup не наблюдаем.
На момент decode hardening далее была **MIT5/ENE5 package composition**, затем отдельный
IPC baseline до оптимизаций; backpressure/schema/MMF/uncooperative shutdown открыты.

Предыдущая работа с кодом и проверками — **2026-10-14, MIT4/ENE4**.
Parent App/AddUnit child, generated typed pipes, same-client reconnect после
bounded OnCrash restart, budget exhaustion, pending Stop/rollback и child cleanup
при abrupt parent exit проверены на Cobalt и Microsoft DI.
Windows x64/net10.0/SDK10.0.402: **Enerit102/102, generator34/34, Mithril89/89,
Core81/81, Cobalt37/37/generator6/6 отдельно Debug/Release**, Failed0/Skipped0;
**App matrix36/36 и isolated pipes12/12**, Completed=true.
Final non-incremental Debug solution build exit0/errors0/warnings1226.
[Единый контракт](mithril/PLAN.md#mit4ene4-app-и-generated-pipes-контракт--2026-10-14),
[команды/границы](mithril/HISTORY.md#mit4ene4-app-и-generated-pipes--2026-10-14).
Production исправления Enerit: explicit typed factories/App-local instances,
scoped endpoints, transport Start/Stop rollback; Core test isolation исправлена.
Mithril/Cobalt runtime не изменялись. Same-build/cooperative/ProjectReference scope;
client cleanup killed parent не наблюдаем. UI/native/package/другие OS/stress не повторены.
На момент MIT4 следующим шагом были Enerit response validation/partial nested-buffer
cleanup, затем MIT5/ENE5 independent package composition.

Будущий engine/editor план2026-10-13 сохранён:
[архитектура и этапы](engine/PLAN.md), [статус](engine/STATUS.md).
ENG0–ENG8 запланированы, проектов/реализации и собственных code/process/GPU checks
пока нет. Новый MIT4/ENE4 proof не является реализацией frame transport/hot reload.

Предыдущая работа с кодом и проверками — **2026-10-12, Mithril MIT3**.
Bounded Never/Once/OnCrash/Always, fresh process/control endpoint, budget/delay,
terminal callback, cancel/drain monitor при Stop/Dispose. Windows x64/net10.0:
**Mithril89/89 Debug +89/89 Release**, restart manual40/40; fresh App Never28/28
Cobalt/DI и legacy16/16. Enerit runtime71/71 в обеих configurations.
Non-incremental Debug solution build exit0/errors0/warnings1220.
[Контракт](mithril/PLAN.md#mit3-bounded-restart-контракт--2026-10-12),
[команды/границы](mithril/HISTORY.md#mit3-bounded-restart--2026-10-12).
На момент MIT3 следующим шагом был MIT4/ENE4: App/AddUnit + generated Enerit IPC/restart.
External restart/package/другие OS/stress и остальные suites/UI не проверены.

Предыдущая работа с кодом и проверками — **2026-10-11, Mithril MIT1/MIT2**.
App-local configurations, main hosting в текущем graph/PID, owned external,
controller rollback/error propagation и cancellable parent/control listener.
Windows x64/net10.0/SDK10.0.401, App lifetime/Never, Cobalt и Microsoft DI:
**Mithril65/65 Debug +65/65 Release**, **App manual28/28**, fresh legacy16/16,
Completed=true; Enerit runtime regression71/71 в каждой configuration.
Non-incremental Debug solution build exit0/errors0/warnings1220.
[Контракт](mithril/PLAN.md#mit1mit2-app-composition-контракт--2026-10-11),
[команды и ограничения](mithril/HISTORY.md#mit1mit2-app-composition--2026-10-11).
На момент этого run следующим шагом был MIT3, затем MIT4/ENE4 App+IPC.
Другие module suites/UI, Enerit generator/manual и package consumers не повторены.

Предыдущая работа с кодом и проверками — **2026-10-11, Enerit ENE1/ENE2 pipes baseline**.
Runtime **71/71**, generator **34/34** отдельно Debug/Release, Failed0/Skipped0;
own-process apphost matrix **12/12**, Completed=true. Same-build typed payload,
32concurrent, local cancellation/late response, remote errors, disconnect/reconnect,
cooperative shutdown и owned child cleanup проверены на Windows x64/net10.0.
Non-incremental Debug solution build exit0/errors0/warnings1242;
другие модульные suites/UI этим запуском не проверялись.
[Контракт и ограничения](enerit/PLAN.md#ene1-ene2-контракт--2026-10-11),
[команды](enerit/HISTORY.md#ene1-ene2-pipes-baseline--2026-10-11).
App/AddUnit/controller/parent-exit в том pipes run не проверялись; restart/общий IPC и Enerit packages остаются открыты.

Предыдущая работа с кодом и проверками — **2026-10-11, Mithril MIT0 own-child baseline**.
Убрана очистка одинаково названных процессов; child start/stop/disposal теперь
освобождают owned Process и ждут exit после принудительного завершения.
**Mithril14/14 Debug и14/14 Release**, Failed0/Skipped0; **manual16/16**
Debug/published Release apphost, Completed=true на Windows x64/net10.0/SDK10.0.401.
Проверен `Never` child-handle scope, App/AddUnit/parent-crash/restart/IPC — открыты.
[Команды и границы](mithril/HISTORY.md#mit0-own-child-baseline--2026-10-11),
[runner](../ManualTests/MithrilLifecycleProbe/README.md).
Другие unit suites/full solution/UI этим запуском не проверялись.

Предыдущая работа с кодом и проверками — **2026-10-11, CORE5/COB4 headless package consumer**.
Проверены **64/64 matrix processes + 3/3 packaged README examples** на local NuGet
в Debug/Release build/publish: Cobalt, Microsoft DI и Microsoft DI без consumer
generator. Actual analyzer path/SHA256/generated sources и отрицательная сборка
без generator подтверждены; metadata и package README дополнены.
Регрессии: **Core81/81, Cobalt37/37, generator6/6**, failed0/skipped0.
[Воспроизведение](../ManualTests/CorePackageConsumer/README.md),
[команды и scope](core/HISTORY.md#core5--cob4-headless-package-consumer--2026-10-11).
Full solution, другие модули, UI/IPC/units этим запуском не проверялись.
На момент pipes baseline следующим шагом были Mithril MIT1/MIT2
contracts; Core/Cobalt — COB5 измерения.

Предыдущая UI работа **2026-10-11 — K2/K4 fixed-font golden**: font catalog,
30 reference PNG, UI532/532, golden360/360 Debug/published Release,
Vulkan validation0/0 и negative controls. [Метод](alloy/GOLDEN_TESTS.md),
[scope](alloy/HISTORY.md#k2k4-fixed-font-golden--2026-10-11).
K7/physical DPI/real loss/VRAM остаются открыты; UI проверки не повторены в CORE5/COB4.

Предыдущая документационная работа: **2026-10-11 — общий README и навигация документации**. Корневой README дополнен картой существующих модулей, source build/test командами, headless/UI quickstart, examples, local NuGet delivery и ограничениями. Старый Signals пример заменён текущими API; добавлен [индекс документации](README.md), актуальный порядок собран в PLAN без дублирования в release criteria. Проверены локальные ссылки/anchors и компиляция двух точных README C# snippets на ранее подготовленных локальных пакетах:2builds exit0/0warnings/0errors. Приложения, unit tests, полный solution и native проверки в этом документационном шаге не запускались. [Область сверки](HISTORY.md#общий-readme-и-навигация--2026-10-11).

Предыдущая code/package работа: **2026-10-11 — UI package/independent consumer5.4–5.5**. Исправлена transitive native Skia delivery, подготовлены локальные NuGet artifacts и packaged инструкции.12App/backend +4direct renderer smoke прошли Windows x64/net10.0/SDK10.0.401, Debug/Release ×build/publish, framework-dependent без RID;4consumer builds/4publishes exit0/0warnings/0errors. Это UI поставка на указанной конфигурации, не Core5/COB4 headless/analyzer proof и не готовность alpha. На тот момент следующий независимый UI шаг — fixed-font golden K2/K4; внешний NVIDIA K7 блокер сохраняется. Unit/full solution tests этим шагом не повторялись. [Результаты и ограничения](alloy/HISTORY.md#package-и-независимый-ui-consumer--2026-10-11).

Предыдущая диагностическая работа: **2026-10-10 — UI WSI stages/raw Win32 и driver screen DC**. [Общий план](PLAN.md), [правила ведения](PLANNING.md), [UI статус](alloy/STATUS.md), [команды, native stack и scope](alloy/HISTORY.md#wsi-stages-raw-win32-и-driver-screen-dc--2026-10-10). Non-incremental Debug UI consumer build exit0/errors0/warnings780, Release publish exit0; native stage/raw/repeat/Hosting controls повторены в Debug/published Release. Unit tests и full solution build этим diagnostic шагом не повторялись.

UI Hosting использует shared process owner loop; concurrent/cancellation/fault/shutdown checks повторно прошли Debug/published Release, validation0/0. GDI trend связан с первым vkCreateSwapchain на новом HWND/surface; raw repro и debugger фиксируют GetDC(NULL) внутри текущего NVIDIA ICD32.0.16.1742 и HDC без последующего ReleaseDC в bounded lifecycle. Raw24cycles NVIDIA GDI6→29, AMD5→5 в Debug/Release; full UI на AMD не проверен. Production ownership/GPU policy не изменены, safe app-side fix не установлен; G4/K7 остаются открыты. Предыдущий full240-window failed USER26→26/GDI23→137; текущий diagnostic его не повторяет.

Предыдущий dispatcher run: UI suite488/488, solution build0errors/1271warnings, Debug/Release functional/short evidence. [Область и ограничения](alloy/HISTORY.md#hosting-owner-dispatcher-и-vulkan-wsi-lifetime--2026-10-10); его passing results не считаются текущими unit/solution checks.

Предыдущий общий Core/DI/Enerit run2026-10-10: **643passed/0failed/0skipped**, Mithril discovery0 отдельно. AppCreate failure устранён, contracts зафиксированы в модульных PLAN; NU1506/NU1507 устранены локальными package/source настройками. [Результаты и ограничения](core/HISTORY.md#исправление-проблем-ядра--2026-10-10). Эти результаты остальных модулей не являются новым UI run.

## Состояние модулей

| Модуль | Реализация и область вывода | Проверки и продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) | Runner/lifecycle, configuration/diagnostics и package consumer | **2026-10-15, COB7 regression:** Core81/81 Release, App comparison на обоих providers. Далее собственный Core quality review |
| [Cobalt / DI](cobalt/STATUS.md) | Собственный API, bundled generator, contracts и paired App comparison | **2026-10-15, COB7:**20measurements/24cold/120package scenarios, Release179+91regressions. Migration отложена, default/adapter сохранены; hot-path backlog отдельно |
| [Enerit](enerit/STATUS.md) | Same-build generated payload contract, TME001/size limits, acknowledged errors/void, concurrent framing и local cancellation/reconnect | **2026-10-14, hardening:** Runtime126/126/generator114/114 Debug/Release, App36/36 и isolated12/12. Strict response/failed nested decode cleanup проверены; Package portion108case проверена; MMF/performance открыты |
| [Mithril](mithril/STATUS.md) | App-local units, owned processes, controller/parent lifecycle и bounded process restart | **2026-10-14:**89/89 Debug/Release regression; App/generated IPC/restart36/36 Cobalt/DI. MIT4 и MIT5 package portion108 проверены; operational diagnostics открыты |
| [Alloy / Argentis](alloy/STATUS.md) | Shared owner, native package delivery, independent consumer, explicit font catalog и30full golden fixtures. Alpha частична | **2026-10-11:** UI532/532, golden360/360 Debug/published Release, validation0/0. Package16processes — предыдущий отдельный run; K7 failed/NVIDIA external blocker, physical DPI/real loss/VRAM сохраняются |

Наличие source/test projects означает наличие реализации или сценариев, а не успешное прохождение. Подробности и датированные результаты принадлежат локальным STATUS/HISTORY.

Текущий COB7 run:3TRX/351passed/0failed/0skipped Release,20measurements/24cold,
package64+3/12comparison/4library processes/2controls, full Release build exit0.
Предыдущий COB5:3TRX/351passed,10cases/12cold. Предыдущий COB6d:6TRX/702passed,
package64+3+5/2controls, full Debug build exit0.
Предыдущий COB6c run:6TRX/644passed, package64+3+5/2controls.
Предыдущий COB6b run:6TRX/454passed, package64+3+5/2controls.
Предыдущий COB6a run:6TRX/360passed, package64+3+5 и2controls.
Предыдущий bundled run:6TRX/268passed, package64+3+5 и108.
Предыдущий Try helper run имеет отдельные6TRX/268passed.
Предыдущий package portion run:12TRX/906passed/0failed/0skipped, package108.
Предыдущий decode hardening run:6TRX/658passed, App36 и isolated12.
Предыдущий MIT4/ENE4 run того же дня:12TRX/698passed/0failed/0skipped.
MIT3 restart40/App28/legacy16, MIT0/MIT1/MIT2, headless package64+3, UI532/golden360
и UI package — отдельные предыдущие runs с собственными датами и scope.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Solution baseline2026-10-10 сохранён отдельно; тогда Mithril tests0. Текущий Mithril89+89; final full solution build успешен,12current module TRX отдельно |
| G2. Core и DI | Известные defects исправлены; component и bounded independent package lifecycle на обоих providers проверены. Дополнительные configuration/utility/platform критерии сохраняют свой scope |
| G3. IPC и units | MIT0–MIT3 и ENE1/ENE2 подтверждены ранее; MIT4/ENE4 same-build App/generated pipes/restart проверен2026-10-14. Strict response/nested failure cleanup и bounded independent package delivery проверены; расширенные transport criteria открыты |
| G4. UI alpha | Частично; UI критерии и собственный следующий шаг — в Alloy плане |
| G5. Сквозные examples | Headless package64+3 исторически проверен; source App/typed IPC/restart36case теперь подтверждён. IPC/units package108 теперь проверен; UI и дополнительные deployment/performance criteria отдельно |
| G6. Общая поставка | Общие R1–R6 не закрыты; версия0.2.2.7-alpha не является readiness свидетельством |

## Общие проверки — датированные сведения

**UI WSI stages2026-10-10:** current non-incremental AlloyVulkanTest Debug consumer build exit0/errors0/warnings780, Release publish exit0. Stage/raw/repeat matrix Debug/published Release: queries без GDI trend, первый swapchain на новой surface/window воспроизводит его; raw teardown orders и thread-exit control проверены. Debug native stacks — screen GetDC(NULL) внутри NVIDIA ICD32.0.16.1742; safe app-side fix не установлен. Hosted dispatcher Debug/Release regression passed/validation0/0. Unit/full solution/full240-window/physical/package checks не повторялись. [Команды и ограничения](alloy/HISTORY.md#wsi-stages-raw-win32-и-driver-screen-dc--2026-10-10).

**UI dispatcher2026-10-10:** Windows/Debug/net10.0/SDK10.0.401; non-incremental `TrueMoon.slnx` build exit0/errors0/warnings1271, UI tests488passed/failed0/skipped0. Release UI consumer publish exit0. Native concurrent/callback/chrome/controlled failure/short cleanup Debug и published Release прошли, validation0/0; два полных240-window прогона **failed** на прежнем GDI limit и предшествуют финальным lifecycle guards. Native24-cycle controls определяют WSI scope, не exact allocator и не VRAM. [Подробности и фактические команды](alloy/HISTORY.md#hosting-owner-dispatcher-и-vulkan-wsi-lifetime--2026-10-10). Другие модульные tests, physical DPI и package consumers не запускались.

**Итог исправлений Core/DI2026-10-10:** Windows/Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. `dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet`: exit0/0errors/1271warnings. Стандартный solution test с no-build/no-restore и отдельная list-tests discovery: exit0. Core81, Cobalt37, Cobalt.Generator6, Enerit30, Enerit.Generator9, Alloy480 — все Passed, Failed0/Skipped0. Mithril TRX total0 не считается passing suite. [Команды, requirement matrix, TRX и ограничения](core/HISTORY.md#исправление-проблем-ядра--2026-10-10); локальные детали — [Cobalt](cobalt/HISTORY.md), [Enerit](enerit/HISTORY.md#совместимость-pools-и-завершение-pipes--2026-10-10). Release/pack/own-process/native UI не запускались.

**Предыдущий Core review2026-10-10:** TrueMoon.Tests **Passed3/Failed1/Skipped0**, discovered4; AppCreate падал на `ServiceResolvingException<IAppLifetime>` через чужой LifeTimeExecutor resolver. TrueMoon.Core incremental build exit0/errors0. [Команды, scope и выводы](core/HISTORY.md#ревью-ядра-и-core0-baseline--2026-10-10). Два passing RunAsync теста тогда содержали Assert.True(true); в текущем исправлении заменены behavioral assertions.

Предыдущая UI investigation: consumer Debug build/published Release, native WGL/thread240-cycle comparisons и shared-loop prototype с двумя live окнами/close/recreate. [Команды и ограничения](alloy/HISTORY.md#wgl-thread-lifetime-и-shared-owner-loop--2026-10-10). Production dispatcher и полный mixed resource result этим шагом не исправлены; unit suite/общий baseline/другие модули той UI работой не запускались заново.

Предыдущая lifecycle работа2026-10-10: UI suite **480passed/0failed/0skipped**, short/callback/retirement proofs и два полных240-window Debug/published Release прогона. Оба полных window resource results **failed** на USER growth при нулевых hooks/registry/subscriptions/retained UI roots. [Команды и ограничения](alloy/HISTORY.md#lifecycle-resize-soak-и-native-resource-growth--2026-10-10). Предыдущие TextBox/Name20repeats и backend comparison — [отдельный запуск](alloy/HISTORY.md#textbox-control-text-и-name-smoke--2026-10-10).

**Интеграция2026-10-10, main `d56f217`:** Windows/Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. Enerit runtime **10passed**, generator **9passed**, Alloy UI **469passed**, во всех трёх проектах Failed0/Skipped0. AlloyVulkanTest consumer build exit0, **0errors/227warnings**. Debug native callback probe exit0:6direct+12hosted failures+12healthy recreations, HWND/tree/registry/hooks cleanup, Vulkan validation0errors/0warnings. Команды и локальный scope — [Enerit история](enerit/HISTORY.md#интеграция-в-main--2026-10-10), [UI история](alloy/HISTORY.md#интеграция-в-main--2026-10-10). Артефакты — ignored `TestResults/AgentMerge` в основном checkout. Общий solution build/tests, Release publish и physical DPI matrix при интеграции не повторялись; их исторические результаты не являются новым запуском.

Из записи предыдущего UI code run **2026-10-09** сохранено: non-incremental TrueMoon.slnx build **0errors/1341warnings**, SDK10.0.401/net10.0; локальные Release publish AlloyTest/AlloyVulkanTest exit0. Подробные команды, configuration и native ограничения — [UI история](alloy/HISTORY.md#49d--k3-native-windows-dpi--реализация-и-запуск-2026-10-09).

Это предыдущий общий build и два UI consumers, не successful full solution test suite и не проверка package consumers остальных модулей. При реорганизации2026-10-09 и интеграции2026-10-10 эти команды не повторялись.

Из прежних записей **2026-09-27**: AppCreate failure и отсутствие найденных Mithril tests. AppCreate отдельно воспроизведён review2026-10-10, затем исправлен и проверен тем run; Mithril discovery0 подтверждён2026-10-10, текущая suite14+14 добавлена MIT0. Старые index/Enerite записи сохранены в архиве UI документа как исторический контекст, не являются текущим blocker roadmap.

## Общие блокеры и неопределённости

- ENE5 measurements и MIT5 operational diagnostics проверены2026-10-15 в отдельных bounded scopes; fresh package108 повторён для MIT5 с проверкой diagnostics. Это не общий platform/release acceptance и не новый performance run.
- G2 headless package/analyzer consumer подтверждён предыдущим CORE5/COB4 run. Cooperative shutdown не прерывает user Task, игнорирующий cancellation; Microsoft DI alias disposal отличается от Cobalt. Контракты/limitations закреплены в локальных PLAN.
- Политика поддержанных configuration/SDK/OS, warnings и package consumer matrix общего выпуска требует отдельной фиксации.
- UI physical DPI matrix ограничена доступным150% монитором. Это ограничение конкретной проверки UI, не препятствие работе над Core/DI/IPC/units.
- UI K7: предыдущий full check **failed** USER26→26/GDI23→137. Текущий native trace локализует screen HDC allocation/lifetime до первого swapchain в NVIDIA ICD32.0.16.1742; raw AMD control не воспроизводит GDI trend, но full UI на AMD не проверен. Safe app-side fix не установлен; supported NVIDIA fix или полностью проверенная alternate configuration, затем full matrix с прежними limits. Private/handles и VRAM остаются; G4 не закрыт.
- Предыдущие same-build App/process/IPC/restart и package proofs не подтверждают automatically other OS/RID, uncooperative shutdown или UI/engine workloads; scope остаётся в локальных STATUS/HISTORY.

## Следующий конкретный шаг

1. Свести bounded quality Core/Cobalt/Enerit/Mithril и оставшийся UI acceptance в общую матрицу перед выбором ENG0. MIT5 diagnostics/package проверены; остальные module/package/platform criteria сохраняют собственный scope.
2. Cobalt hot-path allocation investigation и Enerit scalar c1/c8 allocation/CPU profile — отдельные measured backlog задачи перед оптимизацией. Cobalt default/adapter и отложенная migration прежние. Engine/editor ждёт сводки рабочего уровня остальных модулей.
3. UI: fixed-font golden K2/K4 проверен2026-10-11 в Debug/published Release; следующий [локальный scope](alloy/STATUS.md#следующий-конкретный-шаг) — оставшаяся physical4.9/K3, real loss/K8/VRAM и итоговая matrix. Independent package consumer5.4–5.5 — предыдущий отдельный run, новые font overloads через NuGet ещё не проверены. По решению пользователя NVIDIA GDI defect — внешний K7 блокер; поиск app-side workaround пока прекращён. Возврат при supported NVIDIA fix или полностью проверенной alternate GPU configuration, затем full check с прежними limits; private/handles отдельно. Alpha acceptance сохраняется; пользовательские драйвер/Windows settings без соответствующего запроса не менять.

## Изменения формата — 2026-10-09

Созданы общий PLAN/STATUS, правила и четыре модульных PLAN/STATUS. UI PLAN явно ограничен Alloy/Argentis и интеграциями; его цели/архитектура/идентификаторы/критерии сохраняются. Текущий UI STATUS сокращён; полный прежний документ сохранён в HISTORY. AGENTS указывает общую и локальные точки входа.

Проверки текущей правки прошли: чтение исходников/ProjectReference;17planning/AGENTS документов,270local links/21anchors, Markdown fences/whitespace/conflicts/NUL; полный прежний UI STATUS сохранён verbatim, K1–K9 table rows идентичны, PA1–PA10/net10.0/xUnit сохранены, пять модульных PLAN/STATUS пар присутствуют. Scoped git diff --check прошёл, source worktree status при финальной сверке не изменился, staged entries0. Артефакты документальной проверки — ignored TestResults/PlanningFormat. Новых passing build/tests/GPU/publish результатов эта запись не создаёт.

</details>

## Матрица качества модулей — 2026-10-15

По запросу следующего шага после MIT5 сведены общие PLAN/STATUS/PLANNING,
локальные Core/Cobalt/Enerit/Mithril/Alloy PLAN/STATUS и будущее engine направление.
[Результат](QUALITY.md): bounded проход первых четырёх модулей завершён в собственных
scopes; UI alpha остаётся частичной. Сравнение не складывает разные предыдущие runs
в current full-solution pass. Даты собственных suites, отдельных package и perf
runs сохранены; в частности COB7 own runtime/generator179/91 — Release,
Core107 Debug/Release — headless proof, Enerit114generator — ENE5 baseline,
Mithril94/Enerit126 и package108 — более поздний MIT5 run того же дня.

Исходная сверка AlloyPackageConsumer Program/csproj/runner и Hosting AlloyOptions
подтвердила пробел acceptance: consumer выбирает только UseDI и overloads без
explicit SkiaFontCatalog, runner не сравнивает source SHA в конце. Новый runtime
failure не воспроизводился. Следующий code scope уточнён только в
[плане владельца UI](alloy/PLAN.md#проход-качества-ui-после-headless-модулей--2026-10-15):
fresh independent package/Hosting proof на Cobalt/DI, bundled analyzer, fonts/native
provenance, direct renderer и bounded lifecycle. Это дополнение existing consumer,
без массовой migration других UseDI apps и без нового engine проекта.

NVIDIA K7 и решение пользователя остановить workaround search, physical DPI,
real loss/VRAM/final UI matrix сохранены. Scope quality работы не требует закрыть
все будущие PA1–PA10/DI scopes/MMF/Hot Reload пункты; выбор ограниченного ENG0
прототипа требует явного рассмотрения оставшегося UI риска. Game scenario/API
сверка не выбрала за пользователя. Cobalt/Enerit measured allocation backlog
сохраняется отдельно от функционального порядка работы.

Общий STATUS сокращён до актуальной сводки; полный прежний текст сохранён в
[архиве](#сводка-до-quality-matrix--2026-10-15). Local STATUS/навигация синхронизированы:
убраны устаревшие следующие шаги Core/Enerit/Mithril. В Cobalt PLAN исправлена
старая фраза two-package на действующий один consumer package. Existing module
contracts, общие G1–G6/R1–R6, UI K1–K9/PA1–PA10 и engine ENG0–ENG8 не отменены.

Code/build/tests/pack/publish/process/GPU/performance в этом документационном шаге
не запускались. Артефакты сверки — ignored `TestResults/QualityReview/run-20261015-135635200`:
STATUS.before.md, runtime-sources.before.json и последующий отчёт проверки документов.
Изменений runtime/project files, системных настроек, commits/push нет.

Фактическая проверка документов: `VerifyReview.ps1`/`review.json` в указанном artifact
directory —29Markdown documents/623local links/202heading anchors,0problems;
balanced fences/conflict markers/NUL проверены. Прежний STATUS из347строк сохранён
verbatim в архиве, новая текущая сводка84строки.299runtime/project files совпали
по SHA256 с начальным snapshot; K1–K9 и PA1–PA10 rows UI PLAN/STATUS не изменены
относительно исходного tracked UI baseline. Scoped git diff --check passed.
Это documentation validation, не новый module regression/build/native pass.

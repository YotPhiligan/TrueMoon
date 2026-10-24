# Cobalt / DI: состояние и точка продолжения

Связанная проверка — **2026-10-23, ENE6a/MIT6a integration**: runtime179/179 и
generator91/91 Debug/Release в полном solution2361/configuration; App options/
factory/lifecycle и fresh package132 проверяют оба providers, включая DI без
Cobalt analyzer. Runtime/generator/adapter API не менялись; allocation/performance
baseline не повторён. [Команды и scope](../enerit/HISTORY.md#ene6a-finite-admission-и-наблюдаемая-остановка--2026-10-23).

Последняя работа — **2026-10-23, сверка предпосылок Aether**.
Повторный полный DI quality проход перед локальным authoring API не требуется:
текущие регистрации/lifetime/IFactory достаточны в проверенном scope. Allocation
backlog и новые scopes не становятся engine blockers. [Общая сверка](../PLAN.md#сверка-основы-перед-aether--2026-10-23).
Код/API не менялись, build/tests/performance/consumers не запускались.

Интеграция потребителя — **2026-10-22, Aether DI1–DI4**:
owned service graphs Editor/runtime переведены на injected factories и App-local
registrations; общие Factory/typed IFactory/lifecycle adapters находятся в Contracts.
Cobalt runtime/generator/adapter API прежние. Полный solution повторил runtime179/
generator91 Debug/Release; новые consumer cases проверяют оба providers, ownership,
factory overrides и failure cleanup. Собственные allocation/performance baseline
не повторялись. [Решения](../engine/PLAN.md#приведение-к-правилу-cobaltifactory),
[команды/ограничения](../engine/HISTORY.md#единые-фабрики-и-di--2026-10-22).

Документационное решение — **2026-10-22**: в AGENTS.md закреплены Cobalt,
constructor injection и внедрённые IFactory для создания систем/сервисов;
границы Resolve, lifetime и ownership описаны в [плане](PLAN.md#организация-систем-и-сервисов-через-di-и-ifactory--2026-10-22).
Runtime/API не менялись, аудит consumers и build/tests/package/performance
не запускались. Применять при следующих адресных изменениях; массовая UseDI
migration, новые factory APIs/scopes и изменение очередности не назначены.

Исправление структуры — **2026-10-21**:9global-type files ManualTests/CobaltTest
получили namespace CobaltTest; entry point и generated references согласованы.
Runtime179/generator91 Debug/Release прошли в общем solution run. Свежие Core
80cases/3README, units108cases и UI source/package consumers подтвердили оба
providers и generation в своих scopes. Собственный runtime API не менялся.
[Миграция и точные команды](../HISTORY.md#исправление-структуры-проектов--2026-10-21).

App entry-point audit — **2026-10-19**: Cobalt выбран default App builder;
UseDI переключает provider через IAppBuilderConfigurationContext. Отдельный
UseCobalt не требуется, generators — compile-time integration. Focused Core46,
Enerit13 и Mithril9 включают оба providers; own Cobalt/generator suites и package/
performance matrix не повторялись, runtime code не менялся.
[Общая таблица API и команды](../HISTORY.md#аудит-точек-подключения-модулей-к-app--2026-10-19).

Package metadata check — **2026-10-19**: Cobalt/DI adapter получили тематические
tags и общие project/repository metadata. Release pack/nuspec/README/license/icon
и bundled Cobalt analyzer/targets payloads проверены; Cobalt.Generator остаётся
непакуемой отдельной сборкой внутри Cobalt package. Own unit/native/performance
checks в этом шаге не повторялись. [Общие evidence](../HISTORY.md#nuget-package-metadata--2026-10-19).

UI integration check — **2026-10-19, INF7**: tracked runner повторил Cobalt/DI
source factory/legacy и independent package graphs на OpenGL/Vulkan в Debug и
published Release. По24source App cases и4package native runs/provider прошли;
package analyzer dump пустой, dependency disposal и source/artifact hashes сверены.
Own Cobalt/generator/adapter code и suites не менялись/не запускались.
[Команды/границы proof](../alloy/HISTORY.md#inf7-воспроизводимый-integration-runner--2026-10-19).

Предыдущий UI integration check — **2026-10-19, INF6**: explicit root factory не регистрирует
view service и не требует generated view resolver для factory-based graph.
Cobalt и Microsoft DI проверены на OpenGL/Vulkan, Debug apphost/published Release:
по24source App cases и4independent package native runs/provider; package analyzer
отключён и dump проверен. Source legacy generic graph/analyzer сохранён отдельно.
Dependency lifetime/disposal, owner root, snapshot и startup failures прошли.
Cobalt179/generator91 Debug в прежнем full solution1490/1490; own Release suites/
performance не повторялись, runtime/generator/adapter code не менялся.
[Точные команды/границы UI proof](../alloy/HISTORY.md#inf6-явные-factories-и-совместимость--2026-10-19).

Документационная сверка2026-10-15: [общая quality matrix](../QUALITY.md)
определяет следующий scope — UI package/Hosting с Cobalt и explicit fonts.
Новых code/build/tests/package/performance запусков эта сверка не создаёт.

Последняя интеграционная проверка **2026-10-15, Core headless quality proof**:
на Cobalt source28 +package headless28/direct4 processes, Debug/Release build/publish.
Проверены JSON/configuration/diagnostics, normal/cancel/start/stop/build/config
failures и совместный startup+Dispose error с продолжением cleanup реального
файлового ресурса. Весь общий run:52source +80package +3README, negative CS0246;
Core107/107 Debug/Release. [Команды/границы](../core/HISTORY.md#сквозной-headless-proof--2026-10-15).
Cobalt/runtime/generator/adapter code и собственные suites не менялись/не запускались.
Старые COB7 graph v1/benchmarks ниже не повторялись; их full-source fingerprints
описывают предыдущий run, а не текущие Core sources. Default/migration решение прежнее.

Предыдущая собственная работа **2026-10-15, COB7: сравнение providers и решение о migration**.
Один неизменённый graph v1 прошёл Cobalt и Microsoft DI через собственный TrueMoon
API. Добавлены paired benchmarks,10functional scenarios/provider и independent
consumer с теми же sources. [Результат и решение](COMPARISON.md),
[контракт](PLAN.md#cob7-сравнение-providers-на-app-graph-v1--2026-10-15),
[команды/артефакты](HISTORY.md#cob7-provider-comparison--2026-10-15).
Runtime/Contracts/generator/adapter logic не менялась; optimization и migration нет.

**Решение:** сохранить Cobalt как существующий default TrueMoon, отложить массовый
перевод explicit Microsoft DI consumers. Регистрация этого graph одинакова, Cobalt
даёт structured errors/reference-unique cleanup, MS дешевле в горячем resolution.
UI-specific consumers требуют собственной проверки, provider selectors сохраняются.

## Текущий запуск

Ниже сохранены результаты предыдущего собственного COB7 run; текущая Core
integration указана выше и не является новым performance измерением.

Windows11 x64/Ryzen9 9900X/SDK10.0.402/.NET10.0.13, Release, Concurrent Workstation
GC. BDN0.15.8, Microsoft baseline: **20Dry +20default calibrated cases +24fresh
startup samples** (12/provider), Completed=true.127source fingerprints/mismatches0,
36commands exit0; power plan не изменён. Scenario.Register byte-for-byte совпадает
с COB5. Historical COB5 numbers не используются как before/after speedup.

| Операция | Cobalt | Microsoft DI |
| --- | --- | --- |
| Warm singleton |52.10ns /32B |14.91ns /0B |
| Transient request |433.13ns /440B |34.17ns /184B |
| Graph build+dispose |3.300us /11562B |1.191us /9848B |
| App build+Start+Stop+async dispose |16.906us /72200B |19.320us /75125B |
| First App.Build+Start median, fresh process |29.175ms |26.711ms |

Все20source functional scenarios прошли; package copy — **12processes/120behavior
scenarios** Debug/Release build/publish, оба providers и MS без analyzer. Standard
package matrix64/64 +3actual README, library/app4/4 и2expected COB001–COB006 controls
также прошли; opt-out expected CS0246. Actual analyzer/cache/SHA/generated initializer,
clean runtime payload и sources проверены. Packages local/unpublished.

Current regression: Cobalt179/179, generator91/91, Core81/81 **Release отдельно**:
3TRX/351passed/0failed/0skipped. Full nonincremental Release build exit0/errors0/
warnings1185. Debug unit suites/IPC/units/UI/native не повторены.

## Готовность и ограничения

- COB6a/b/c/d, COB5 и COB7 закрыты в согласованном собственном headless scope.
  Это не общий release и не подтверждение equivalence всех DI contracts.
- App lifecycle включает фактический cleanup каждого provider: Worker aliases
  shared, Cobalt Dispose1/MS Dispose3. Borrowed0/owned factory1 на обоих.
- Cold timing исключает process/CLR launch, module initializers, input creation
  и cleanup; OS/file caches прогреты.12samples/provider дают exploratory median/
  min/max, nearest-rank P95 равен max. Cold process-wide bytes не равны BDN bytes/op.
- Desktop background work не контролировалось; absolute timings относятся к этой
  машине/graph. Thread contention, other OS/SDK/RID/self-contained/trimming/AOT,
  UI и engine workloads не измерены. Причины perf gap не профилировались.
- Supported constructor/generic/optional/registration forms и recursion/error
  limits — в [плане](PLAN.md) и [package README](../../TrueMoon.Cobalt/PACKAGE.md).
  Runtime synchronous под resolver lock; scopes/reflection fallback не добавлены.
- Generator net10.0/RS1041 и shared warnings сохраняются. NuGet analyzer bundled;
  source consumers имеют explicit analyzer ProjectReference. Adapter поддерживается.

## Следующий конкретный шаг

Актуальный общий порядок — [engine STATUS](../engine/STATUS.md#следующий-конкретный-шаг).
ARCH1b.1, ED1a и первый ENE6a/MIT6a срез выполнены в своих границах. Прежний UI
package/Hosting проход выполнен в своём scope и не является текущим prerequisite.
[Сверка зависимости](../PLAN.md#сверка-основы-перед-aether--2026-10-23).

Cobalt performance backlog перед пересмотром migration: исследовать32B warm resolve,
96B open-generic и transient overhead на том же paired workload, затем повторить
измерения/регрессии. Это отдельная будущая задача, не сделанная оптимизация и не
новое обязательное условие следующего модульного прохода. Explicit UseDI consumers сохранены;
адресная migration возможна после проверки конкретного consumer и его limits.

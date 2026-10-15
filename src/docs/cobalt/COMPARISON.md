# Cobalt и Microsoft DI в TrueMoon — 2026-10-15

**Решение COB7: Cobalt остаётся default для собственного TrueMoon API; массовый
перевод consumers с явным Microsoft DI сейчас не выполнять.** Оба providers прошли
одинаковый headless App scenario. Регистрация на этом API одинакова по удобству;
Cobalt даёт проверенные compile-time diagnostics, structured dependency path и
reference-unique cleanup. Microsoft DI заметно дешевле в горячем resolution.
Перед широким переходом нужны работа над Cobalt hot-path allocations и отдельная
регрессия целевых consumers, особенно UI. Существующий adapter сохраняется.

Это решение относится к TrueMoon API. Полная замена Microsoft DI ecosystem,
IServiceCollection/Host bridges, scopes/decorators/keyed contracts в задачу не входят.
[План](PLAN.md#cob7-сравнение-providers-на-app-graph-v1--2026-10-15),
[команды и evidence](HISTORY.md#cob7-provider-comparison--2026-10-15),
[исходник/протокол](../../Benchmarks/TrueMoon.Cobalt.Benchmarks/README.md).

## Что сравнивалось

Один unchanged Scenario.Register из COB5:12registrations/10keys, borrowed settings,
owned factory, Worker Composite lifecycle, open repository/exact closed repository,
transient request/ticket и3plugins. Его SHA256 совпадает с COB5:
`B8E4EAEF7F1C1EB57A311941D303240DBE171CE9548AA04E28577BE48934098E`.
Обоим providers передаётся тот же registration module; дополнительных factories
для обхода дефектов одного из них нет. Разница выбора App — default Cobalt либо
один `UseDI()` с альтернативным adapter package.

20paired BenchmarkDotNet0.15.8 cases, Microsoft baseline, MemoryDiagnoser/default
calibration;20Dry до final measurement. App paths симметрично используют
App.Builder/Setup/Build; предыдущий COB5 App.Build helper в это сравнение не включён.
Окружение: Windows11 x64, Ryzen9 9900X12/24cores, SDK10.0.402/.NET10.0.13,
Release, Concurrent Workstation GC. Существующая Ultimate Performance схема питания
и default GC/JIT settings сохранены. Desktop background workloads не контролировались.

## Производительность

Mean и allocated bytes/op; Error — half-width99.9% CI. Ratio — отношение means
Cobalt/Microsoft, а не заявление о скорости приложения целиком. Bytes округлены;
raw full JSON сохраняет исходные значения и все measurements.

| Операция | Cobalt mean ± Error | Microsoft mean ± Error | Cobalt/MS | Bytes Cobalt / MS |
| --- | --- | --- | --- | --- |
| Warm singleton |52.101 ±0.655ns |14.908 ±0.175ns |3.50 |32 /0 |
| Warm exact closed generic |41.885 ±0.735ns |15.680 ±0.260ns |2.67 |32 /0 |
| Warm open generic |152.868 ±2.813ns |17.295 ±0.222ns |8.84 |96 /0 |
| Transient request с dependencies/plugins |433.133 ±6.186ns |34.171 ±0.677ns |12.68 |440 /184 |
| Materialized plugins |189.740 ±1.882ns |35.386 ±0.641ns |5.36 |248 /120 |
| Empty resolver build+dispose |130.692 ±2.109ns |633.915 ±12.687ns |0.21 |1392 /7280 |
| Graph build+dispose |3299.889 ±64.754ns |1191.229 ±23.571ns |2.77 |11562 /9848 |
| Build+first Request+dispose |4610.506 ±92.062ns |3993.987 ±79.914ns |1.15 |14290 /17073 |
| App build+dispose |14688.855 ±289.355ns |11020.774 ±202.813ns |1.33 |68005 /62210 |
| App build+Start+Stop+async dispose |16906.445 ±316.130ns |19320.142 ±380.649ns |0.88 |72200 /75125 |

Cobalt выигрывает на пустом контейнере и полном прогретом lifecycle этого App,
использует меньше памяти при first graph activation. Microsoft выигрывает во всех
проверенных warm resolutions, построении рабочего graph и App build. Его warm
singleton/generics обходятся без measured managed allocations; Cobalt выделяет
32–96B. Причины разницы не профилировались, оптимизации этим этапом не выполнялись.
App lifecycle включает разные фактические cleanup semantics (Composite Dispose1/3);
это результат реального provider workflow, не isolated activation comparison.

Отдельный первый **App.Build+Start внутри fresh process**,12samples/provider с
чередованием порядка и отдельным discarded file-cache prime:

| Provider | Median | Min–max / nearest-rank P95 | Process-wide bytes |
| --- | --- | --- | --- |
| Cobalt |29.175ms |28.730–32.122ms /32.122ms |111832–118024 |
| Microsoft |26.711ms |26.347–27.697ms /27.697ms |106864–113400 |

Process/CLR launch, module initializers, input creation и cleanup вне cold boundary.
OS/file caches прогреты; это не machine-cold boot. С12samples P95 равен максимуму
и не является SLA. Cold bytes process-wide могут включать runtime/background work;
их нельзя приравнивать steady-state MemoryDiagnoser. На этой машине MS median
примерно на2.46ms меньше; вывод о другом hardware/SDK или production tail не делается.

## Удобство, ошибки и lifetime

| Область | Cobalt | Microsoft DI через TrueMoon adapter |
| --- | --- | --- |
| Регистрация этого graph | Тот же module, одна explicit owned factory | Тот же module и та же factory; `UseDI()` выбирает provider |
| Conditional/Try/named Type calls | Выполняются, repeated Try suppresses duplicates | Те же проверенные результаты |
| Нормальный App / external cancellation / rollback | Пройдены; original failure/token, Start/Stop1/1 | Пройдены с теми же assertions |
| Factory/constructor failure | Original exception; failed singleton retry | Original exception; failed singleton retry |
| Missing/cycle | ServiceResolvingException и typed dependency path | InvalidOperationException с provider message |
| Shared Composite aliases | Один instance, Dispose1 | Один instance, Dispose3 после разрешения всех aliases |
| Owned factory / borrowed instance | Owned disposed1, borrowed0 | Owned disposed1, borrowed0 |
| Generation | Собственные constructor/type rules, COB001–COB006 на registration site | Consumer analyzer можно отключить; activation выполняет provider |
| Dynamic construction | Type variables/generic wrappers не специализируются generator; нужен matching resolver или explicit factory | Текущий package probe работает без consumer generator |
| Поставка | Один bundled runtime/analyzer package, SDK10 compiler | Альтернативный adapter package, analyzer opt-out проверен |

Проверка10functional scenarios/provider выполнена из source и повторена теми же
исходниками через independent PackageReference:12processes/120behavior scenarios,
Debug/Release build/publish, оба providers и отдельно Microsoft без analyzer.
Сравнение не утверждает совпадение всех registration/constructor/nullability,
generic constraints или cleanup-on-error contracts. Подробные собственные правила
Cobalt остаются в [плане](PLAN.md) и [package README](../../TrueMoon.Cobalt/PACKAGE.md).

## Решение о consumers и продолжение

В workspace найдено22UseDI call sites:13в UI-примерах/probes,3manual dual-provider
selectors,1legacy MithrilTest,4unit-test selectors и1benchmark selector. Headless
proof не подтверждает Cobalt для этих UI flows; их собственные platform/lifecycle/
package checks перед migration обязательны. Provider selectors нужны для регрессий.
External/private consumers за пределами workspace не исследовались.

COB7 закрыт как comparison и решение **отложить массовую migration**. Cobalt уже
является default App.Builder и пригоден для проверенного собственного graph;
переход ради одной performance или «меньше регистрационного кода» не обоснован.
Адресная migration возможна после проверки graph/constructor forms, ownership и
допустимых costs конкретного приложения. Выбор provider не меняется этим этапом.

Следующее общее направление — quality review Core bootstrap/configuration/
diagnostics по согласованному порядку модулей. Для Cobalt остаётся измеренный
performance backlog: исследовать32B warm resolve, open generic96B и transient
overhead на том же paired workload, затем повторить comparison перед новым
решением о migration. Этот backlog не объявлен общей release blocker или новым
обязательным условием начала Core quality.

# Kton: App, configuration, DI и ownership

PHYS4 добавляет публичное подключение в `TrueMoon.Kton.Bepu`. Neutral
`TrueMoon.Kton` остаётся BCL-only; provider использует общие App/configuration/factory
контракты `TrueMoon.Contracts`, не зависит от bootstrap, Cobalt implementation,
Microsoft DI implementation, Aether или UI. Bootstrap/provider выбирает composition
root приложения. Проверки: [HISTORY](HISTORY.md#phys4-2026-11-12),
[manifest](PHYS4_RESULTS.json), [consumer/runner](../../ManualTests/KtonApp/README.md).

## Подключение

```csharp
using TrueMoon;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;

await using var app = App.Build(context => context
    .UseBepuPhysics(options => options.DefaultWorld = new PhysicsWorldOptions
    {
        Quality = PhysicsQualityProfile.Baseline60,
        Gravity = new(0, -9.81f, 0),
        Limits = new() { MaxBodies = 256 }
    }));
await app.StartAsync();
// Системы/сессии получают фабрику через constructor DI.
await app.StopAsync();
```

Тот же Setup используется в `App.Builder(...).Setup(...)` и `App.RunAsync(...)`.
Cobalt — штатный resolver. Для существующего Microsoft adapter composition root
выбирает `App.Builder(builder => builder.UseDI())` с
`using TrueMoon.Extensions.DependencyInjection;` и отдельной зависимостью приложения
от adapter. Provider не подключает его автоматически.

UseBepuPhysics вызывается **до регистрации consumer startup services**. Повторное
подключение, включая mixed configured/callback пути, и уже зарегистрированные
typed/base world factories либо low-level provider factory отвергаются при сборке
графа. Override регистраций после Use не является поддерживаемым способом замены
backend; один physics provider на App.

## Фабрики и параметры

В DI доступны две aliases одного недиспозабельного adapter:

| Dependency | Вызов | Параметры |
| --- | --- | --- |
| `IFactory<IPhysicsWorld>` | `Create()` | Валидированный startup snapshot `BepuPhysicsOptions.DefaultWorld` |
| `IFactory<IPhysicsWorld, PhysicsWorldOptions>` | `Create(options)` | Полный explicit snapshot; configured defaults не подмешиваются |
| `IFactory<IPhysicsWorld>` | `Create<PhysicsWorldOptions>(options)` | Тот же explicit путь |

Null/wrong generic runtime arguments отвергаются; для defaults нужен именно `Create()`.
Проверка options выполняется до backend resource creation. Каждый результат — новый
независимый мир с собственным world incarnation, pools, simulation, dispatcher,
лимитами и tick. Между мирами не разделяются handles/shapes/contacts.

Фабрика доступна для injection после Build, но создание разрешается только после
успешного старта Kton. Consumer IStartable, зарегистрированный после Use, может
создать мир в своём StartAsync. Ни Build, ни Kton StartAsync не создают simulation,
backend pool, worker или скрытый default world. После stop/dispose admission закрыт
навсегда; новый запуск требует нового App. Repeated Start работающего App harmless.

Сессия хранит внедрённую фабрику и результат, вызывает `Step(nextTick)` сама и
закрывает мир при завершении. На каждом tick нет configuration read, IO или Resolve.
Полный пример constructor DI и обратного lifecycle порядка — `ProbeSession` в
[consumer](../../ManualTests/KtonApp/Program.cs).

## Общая configuration

```csharp
using TrueMoon.Configuration;

var profile = ConfigurationProfiles.Create(builder =>
{
    builder.AddSchema(BepuPhysicsConfigurationSchema.Default);
    builder.AddArguments("args", args);
});
await using var app = App.Build(context => context
    .UseConfiguration(profile)
    .UseConfiguredBepuPhysics());
await app.StartAsync();
await app.StopAsync();
```

Регистрируется **точный instance** `BepuPhysicsConfigurationSchema.Default`.
Providers и приоритеты задаются общим configuration profile; отдельного чтения
environment/args/JSON в Kton нет. Explicit callback в UseConfiguredBepuPhysics
применяется после sources, затем выполняются neutral validation и detached snapshot.
Reader.GetOptions и сохранённый callback object не меняют module defaults.
App configuration reload не переустанавливает startup snapshot или существующие миры.

Schema section `Kton`, 16 scalar fields:

| Относительный путь | Тип / назначение |
| --- | --- |
| `DefaultWorld:Quality` | `PhysicsQualityProfile`, Baseline60 или FastLinear60 |
| `DefaultWorld:Gravity:X/Y/Z` | Три отдельных float field, m/s² |
| `DefaultWorld:StepDiagnosticBudgetMilliseconds` | Positive finite double, diagnostic target |
| `DefaultWorld:Limits:MaxBodies/MaxShapes/MaxJoints` | Три отдельных int admission count |
| `DefaultWorld:Limits:MaxCommandsPerTick/MaxContactEvents` | Два отдельных int buffer/admission count |
| `DefaultWorld:Limits:MaxQueryHits/MaxQueryScratchBytes` | Два отдельных int query limits |
| `DefaultWorld:Limits:MaxMeshVertices/MaxMeshTriangles/MaxMeshInputBytes` | Три отдельных int mesh input limits |
| `DefaultWorld:Limits:PoolDiagnosticBudgetBytes` | Long diagnostic sampled pool threshold |

Пример CLI: `--Kton:DefaultWorld:Quality=FastLinear60`
`--Kton:DefaultWorld:Gravity:Y=-5` `--Kton:DefaultWorld:Limits:MaxBodies=128`.
Пределы и semantics сохраняют [PHYS1](CONTRACTS.md); runtime options не могут поднять
version-one ceilings. Значения budgets не объявляют hard RAM или frame-time cap.

## Lifecycle и повторная очистка

Единственный App-owned disposable — `Hosting.BepuPhysicsHost`. Фабрика и общий
IStartable/IStoppable adapter заимствуют его и не реализуют IDisposable. DI aliases
не создают второго владельца, включая Microsoft DI, который может capture каждый
disposable alias отдельно. Сам host публичен для диагностики и cleanup retry;
его обычное время жизни контролирует App.

| Этап | Поведение Kton / владельца сессии |
| --- | --- |
| Build | Validate/snapshot/register. Backend resources отсутствуют |
| Start | Проверка cancellation, открытие factory admission. Мир создаёт consumer |
| Session stop | Сессия прекращает новые ticks, join своей работы, Dispose своего мира |
| Kton stop | Закрывает factory admission и очищает только failed unpublished candidate |
| App DI Dispose | Fallback Dispose того же host; успешная очистка идемпотентна |

Миры **исключительно caller-owned**, не записываются в root-container disposal list.
App не перечисляет и не освобождает выданные миры. Это сохраняет PHYS1 ownership;
добавление host не вводит скрытый fallback owner. Нормальный порядок — session →
world → App. У мира нет borrowed App resources: при преждевременном закрытии App
original owner по-прежнему может завершить/освободить его. Отсутствие автоматического
Dispose не разрешает забывать мир. `host.IsReleased` означает очистку host-owned
candidate, а `Resources.PublishedWorlds` отдельно показывает caller-owned results.

После успешного синхронного Step workers уже joined. Host не владеет очередью ticks
или работой сессии и не подменяет её drain. Stop выполняет синхронную owned
cleanup без прерывания cancellation token; отдельный cancellation waiter/async
activation/scopes не вводится. Cleanup failure оставляет admission закрытым и не
объявляет IsReleased. То же относится к stop с заранее cancelled token: cleanup
всё равно выполняется.

При ошибке создания backend candidate не публикуется. Успешная rollback cleanup
позволяет следующую Create. Если cleanup тоже упал, host сохраняет candidate;
новые Create отвергаются до закрытия host. Stop/Dispose повторяют оставшиеся стадии,
не освобождая уже завершённые второй раз.

Failed Kton startup закрывает admission и очищает своё unpublished ownership.
При более поздней startup ошибке App останавливает уже успешно запущенные consumers
в обратном порядке; поэтому consumer освобождает мир до Kton. Сам падающий consumer
отвечает за свои незавершённые создания. Original и rollback cleanup errors сохраняются.

App/DI могут **запомнить первый Dispose failure**. Повторный `app.DisposeAsync()`
может вернуть ту же ошибку без новой попытки. Для этого случая composition/session
owner заранее удерживает borrowed `BepuPhysicsHost`, затем явно повторяет его
`StopAsync()` или `Dispose()`; новый Resolve после App disposal невозможен.
Host никогда не передаёт pending ownership в потерянный temporary object.
Успешные repeated stop/dispose — no-op. World cleanup failure аналогично требует
сохранить **сам мир** и повторить его Dispose; host не забирает это ownership.

Friend fault seam моделирует ошибки между завершёнными cleanup stages; произвольный
partial failure внутри unsafe BEPU Dispose, heap corruption и power loss этим не
доказываются. Матрица PHYS4 — Windows x64/.NET11/JIT/one worker, оба DI, source и
fresh packages. [PHYS5](PLAN.md#phys5) ECS bridge, full solution/GPU/other OS/AOT/
parallel worker/release acceptance остаются отдельной работой.

# TrueMoon.Aether

Экспериментальный модуль игрового движка TrueMoon. Одна библиотека содержит
contracts игровой сессии, supervisor, control protocol и CPU frame transport.
Текущая реализация рассчитана на Windows x64 и .NET 10.

[План](../docs/engine/PLAN.md), [статус и ограничения](../docs/engine/STATUS.md),
[пример Editor/Game.Runtime](../ManualTests/EngineSessionProbe/README.md).

## Состав

| Область | Назначение |
| --- | --- |
| `TrueMoon.Aether.Contracts` | Session identity, scene snapshots, `IGameSession`, control DTOs и frame codec |
| `SessionSupervisor` / `RuntimeProtocol` | Serialized commands, readiness, generations, heartbeat, stop/reap и observable failures |
| `SharedFrameRing` / `FrameProducer` / `FrameReceiver` | Bounded Windows shared-memory transport, validated frames и latest-frame receiver |
| `FrameSession` | Surface generations, first-present readiness, resize, drain и frame ownership |
| `NativeRuntimeFactory` / `BuildIdentity` | Owned Mithril child, Enerit client, bootstrap и same-build fingerprints |

Contracts и реализация находятся в `TrueMoon.Aether.dll`. Модуль использует
Contracts/Enerit/Mithril; зависимостей на Alloy/Skia/Silk у него нет. Enerit generator
создаёт control client/server adapters при сборке самого Aether.

## Подключение

```xml
<ProjectReference Include="../TrueMoon.Aether/TrueMoon.Aether.csproj" />
```

Основные namespaces — `TrueMoon.Aether` (App extension), `TrueMoon.Aether.Contracts`,
`TrueMoon.Aether.Hosting`, `TrueMoon.Aether.Sessions` и `TrueMoon.Aether.Frames`.
Editor владеет supervisor и receiver; child владеет игровой сессией и producer.
Host executable выбирает роли и создаёт конкретную реализацию `IGameSession`.
Готовый образец такой composition находится в `ManualTests/EngineSessionProbe`.

`ManualTests/Game.Logic` остаётся отдельной игровой DLL, подключённой к Aether.
Её нельзя объединять с engine assembly: смена игровой логики при сохранении
host/contracts является целью ENG3. Исполняемый пример и `TrueMoon.Aether.Tests`
также имеют отдельные сборки.

## Текущие границы

ENG1/ENG2 реализуют session lifecycle и CPU viewport. Compiler/checkpoint/restore/
versioned replacement, 3D renderer, shared GPU transport и Hot Reload относятся
к последующим этапам. UI OpenGL viewport живёт в примере и подключает Alloy явно.
Новый namespace не добавляет поддержку других платформ или release readiness.

Типы пока экспериментальные; проект packable, публикация NuGet отдельно.
Metadata и самостоятельный [package README](PACKAGE.md) включены в local nupkg.
Сохраняются same-build/local same-user trust, thread/lease ownership и bounded
cleanup. Failed reap блокирует следующий launch до успешного cleanup.

## Подключение к App

```csharp
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Hosting;
using TrueMoon.Dependencies;

await using var app = App.Build(context => context.UseAether(options =>
{
    options.HostContractId = BuildIdentity.HostId;
    options.GameBuildId = BuildIdentity.GameId;
    options.ReportDirectory = "engine-reports";
}));
await app.StartAsync();
var engine = app.Services.Resolve<AetherHost>();
await engine.Supervisor.StartAsync(new SceneSnapshot("demo", 0, []));
await engine.Supervisor.StopAsync();
await app.StopAsync();
```

Этот native пример требует entry executable с child-role/bootstrap handler,
как в EngineSessionProbe, и matching Game.Logic.dll. Сам UseAether регистрирует
App-owned services/lifecycle, не генерирует runtime entry point и не запускает игру.
Свой IRuntimeFactory задаётся через options.RuntimeFactory; callback должен вернуть
новый App-owned instance, а зависимости может получать из resolver. Factory/host
имеют одну регистрацию владельца; raw Supervisor/Frames не регистрируются aliases.

Options валидируются и копируются во время UseAether. Повторное подключение или
отдельная регистрация IRuntimeFactory отвергаются, чтобы не создать два владельца.
App Stop/Dispose permanently closes admission, завершает frames и supervisor;
Start после этого запрещён. Ошибки cleanup наблюдаемы, host Dispose допускает
явный retry после отказа. UI выбирает приложение и drains своё work до освобождения
зависимых canvas/native resources. Headless и viewport examples используют UseAether.

## Проверки

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -IncludeViewport
```

Тесты используют прямой ProjectReference на Aether; linked production source
copies и dependency на executable примера не требуются. Native runner отдельно
проверяет Debug apphost и published Release с own-process cleanup.

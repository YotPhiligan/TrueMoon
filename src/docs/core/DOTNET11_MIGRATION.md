# .NET 11: поддержанный профиль и переход

Текущие workspace projects используют `$(TF)` или `$(TestsTF)` из
`Directory.Build.props`: оба значения — `net11.0`. `global.json` выбирает
стабильный SDK `11.0.100`, `allowPrerelease=false`, `rollForward=latestMinor`.
Для независимой поставки runner фиксирует точный установленный SDK без roll-forward.
Общая базовая версия — `0.3.0.1-alpha`; локальные acceptance builds получают
уникальный prerelease от неё. Используйте согласованные версии всех TrueMoon пакетов.

| Настройка | Текущий контракт |
| --- | --- |
| Runtime / TFM | .NET 11 / net11.0; прежняя net10 поставка не означает совместимость новых DLL с .NET 10 |
| Compiler | LangVersion=latest, Features включает runtime-async=on; не обещает измеренного ускорения |
| Тесты | xUnit 2 / VSTest; MTP/xUnit 3 не включались |
| DI | Cobalt по умолчанию; Microsoft DI adapter остаётся поддержанным явным выбором |
| Native UI | Проверяемый профиль Windows x64, framework-dependent; checked-in extended Skia DLL и отдельные OpenGL/Vulkan проверки |
| Другие OS/RID, self-contained, AOT/trimming | Требуют отдельной проверки; не следуют из смены TFM |

## Исходники и независимые потребители

Из `src`:

```powershell
dotnet restore TrueMoon.slnx '-bl:restore-{}.binlog'
dotnet build TrueMoon.slnx -c Debug --no-restore '-bl:build-Debug-{}.binlog'
dotnet build TrueMoon.slnx -c Release --no-restore '-bl:build-Release-{}.binlog'
```

Package versions внешних зависимостей остаются в `Directory.Packages.props`.
Этот файл включает CPM только для принадлежащих checkout проектов. Внешний game
project с явным импортом не должен случайно становиться CPM consumer.
Устаревший DotNetCliToolReference удалён; обычному nested restore не требуется
test-only DirectoryPackagesPropsPath override.

Не копируйте старые bin/net10.0 пути и SDK pins в новые runners. Общий
`ManualTests/BuildProfile.ps1` получает профиль через MSBuild; consumer получает
снимок TF/TestsTF/SDK/LangVersion/Features, собственные props, NuGet.Config и cache.
Standalone package consumer не должен наследовать repo CPM или использовать
ProjectReference как запасной путь. Обычные PackageReference сохраняют единый
номер версии. Cobalt включает matching generator и buildTransitive target;
Enerit generator поставляется отдельным согласованным пакетом.

## Aether game artifacts

Target и допустимый stable SDK major следуют TargetFrameworkAttribute модуля
Aether. Frozen game compile использует согласованные target/SDK/Features;
Features входят в captured compiler inputs и fingerprint. Manifest/lock schema 1
сохранены с target `net11.0`. Старый net10 artifact требуется явно пересобрать:
Open отвергает несовместимый target и не мигрирует cached assembly автоматически.
Capture bounds, package policy, identity, verification и child ownership сохраняются.

Game projects, реализующие IGameSessionEntry с IServiceResolver, должны явно
ссылаться на TrueMoon.Contracts, как и на TrueMoon.Aether. Для compiler-owned
HintPath fixtures используются AetherContractPath и TrueMoonContractsPath;
ссылки не копируют contract DLL в game payload.

## Проверка поставки и границы

[FrameworkDelivery](../../ManualTests/FrameworkDelivery/README.md) собирает одну
свежую согласованную поставку и проверяет App/configuration на обоих DI,
generated DI/IPC, actual game child, native viewport и все 11 suites D/R.
Точные датированные результаты находятся в [Core STATUS](STATUS.md) и HISTORY;
наличие runner само по себе не является passing результатом.

Исторические net10 результаты, намеренные unsupported-target fixtures и
совместимость принимаемых внешних managed libraries не переписываются сменой TFM.
Physical input, K7/device loss/VRAM, Nom VM/power-loss, performance и прежние
diagnostics/drain root causes сохраняют собственные критерии. Эта миграция
не завершает новый mesh surface renderer и не принимает отдельную game заявку.

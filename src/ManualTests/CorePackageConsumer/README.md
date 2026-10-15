# Независимые headless package consumers: CORE5 / COB4

Consumer использует только PackageReference, собственные исходники и .NET 10.
Он намеренно не входит в solution. Локальные Directory.Build.props/targets и
Directory.Packages.props прекращают наследование repository build/CPM settings.
Скрипт копирует проект в новый ignored каталог и проверяет фактические NuGet
libraries и source metadata; библиотечные исходники не копируются.

## Запуск

Из `src`, PowerShell 7 и .NET SDK 10:

```powershell
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunPackageSmoke.ps1
```

Необязательные `-RunDirectory` (новый каталог внутри `TestResults/CorePackages`)
и `-PackageVersion` (prerelease) позволяют задать конкретные артефакты. По умолчанию
создаются уникальная версия и новый каталог; предыдущие результаты сохраняются.
Глобальная версия репозитория и настройки NuGet не меняются. Публикации пакетов нет.

Скрипт пакует Release graph App/Cobalt/Contracts/Microsoft DI adapter
в local feed. Consumer restore использует уникальную версию, изолированный cache
и source mapping `TrueMoon`/`TrueMoon.*` → local feed. Существующий кэш служит
read-only fallback для внешних dependencies. Каждый TrueMoon package должен
реально восстановиться из нового feed в изолированный cache.

## Матрица и проверки

| Consumer | Debug/Release × build/publish | Сценарии | Процессы |
| --- | --- | --- | --- |
| Headless app с bundled generator | Cobalt и Microsoft DI | 6 общих lifecycle/config scenarios + cleanup-failure на Cobalt | 52 |
| Direct Cobalt без App/adapter | Cobalt | Composite, open generic, transient, owned/borrowed disposal | 4 |
| Headless Microsoft DI без consumer generator | Microsoft DI | normal, cancel, start-failure, stop-failure, build-failure, config-failure | 24 |

Каждый процесс ограничен timeout и записывает JSON; failure завершает runner
с ненулевым кодом. Проверяются:

- consumer-generated sources и ModuleInitializer, реальные compiler analyzer
   path/SHA256 из bundled Cobalt package; runtime output не содержит analyzer/Roslyn;
- shared Composite и lifetime aliases, open/closed generic dependencies,
  singleton/runtime-factory/instance/transient identity;
- реальные JSON GetOrCreate/persistence, failed refresh/recovery, configuration
  priority/conversion и app name; validation failure до ресурсов; observable diagnostics,
  observer error isolation, source/subscription cleanup после shutdown/build failure;
- нормальное lifetime.Cancel, внешняя cancellation с исходным token, исходные
  startup/stop/build exceptions, reverse rollback/stop и отдельный shutdown token;
- owned service/factory/transient disposal и borrowed instance retention; реальный
  эксклюзивный FileStream удерживается во время Start/Stop и повторно открывается
  после cleanup; Cobalt startup+Dispose errors сохраняются вместе.

Cobalt освобождает shared Worker один раз; Microsoft DI может вызвать Dispose
через несколько aliases. Worker поддерживает повторный Dispose и учитывает
освобождение собственного ресурса отдельно от числа вызовов. Provider differences
не скрываются. Отрицательная сборка отключает bundled generator через
`EnableCobaltGenerator=false`, сохраняя ссылку
на consumer-generated namespace; ожидается CS0246/CS0234, а не passing build.

Normal output не содержит UI/Skia/Silk/IPC/units dependencies. Delivery —
framework-dependent, без RID, trimming, single-file или NativeAOT. Ctrl+C/kill,
Linux/macOS и другие compiler hosts не входят в эту матрицу.

В run-каталоге: local feed, isolated packages, copies consumers, generated sources,
build/publish output, logs, 80 process reports и `summary.json` с source/package/
analyzer SHA256 и SDK. `Completed=true` выставляется только после всей матрицы
и отрицательного контроля. Не считать промежуточный failed runner успешным итогом.

Дополнительно скрипт извлекает точные C# examples из доставленных PACKAGE.md
TrueMoon/Cobalt/adapter: 3 отдельные Release сборки и запуска на тех же пакетах.
Примеры компилируются без ручной адаптации исходников; logs и source SHA256 входят
в summary. Они учитываются отдельно от 80 сценариев основной матрицы.
Перед Completed=true source fingerprints сверяются повторно. Исторические64+3
runs относятся к прежней5-scenario матрице. Общий source/package запуск:
[CoreHeadlessProbe](../CoreHeadlessProbe/README.md).

## Автоматическая поставка generator

```xml
<PackageReference Include="TrueMoon" Version="YOUR_VERSION" />
```

TrueMoon.Cobalt содержит runtime и analyzer в одном пакете; приложение с TrueMoon
получает его транзитивно. Отдельный generator project остаётся build component,
его nupkg больше не создаётся. Direct Cobalt consumer имеет одну ссылку на Cobalt.
Source ProjectReference consumers сохраняют явный analyzer reference. Microsoft DI
проверяется с `EnableCobaltGenerator=false`. Подробнее: [Cobalt package README](../../TrueMoon.Cobalt/PACKAGE.md)
и [Core package README](../../TrueMoon/PACKAGE.md).

## Library → app и migration control

После completed main matrix можно запустить дополнительную проверку:

```powershell
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunDeliveryChecks.ps1 -PackageRunDirectory FULL_PATH_TO_COMPLETED_CORE_RUN
```

Она создаёт отдельную Library с одной PackageReference на Cobalt и App с одной
ProjectReference на собственную Library. App не ссылается на framework sources.
Проверяются analyzer delivery в обе compilations, библиотечная TrySingleton
registration, internal implementations, unique longest constructors, nullable/default
closed/open parameters, registered-value precedence, singleton identity и runtime
output в Debug/Release build/publish (4процесса).
В этих4processes также проверяются runtime cycle path и recovery, required missing
dependency, null singleton retry, original closed/open constructor exception/stack
и cleanup completed owned dependencies при сохранении borrowed instances.
Дополнительно: runtime Type validation без изменения context, detached read-only
snapshot, instance/self pair queries/Remove/Try, mixed closed/open generic global
order/lifetime/closed-single precedence и built graph isolation.
Две отдельные negative builds проверяют COB001–COB006 на строках регистрации
без вторичных CS/MSBuild errors.
Дополнительный `-LegacyGeneratorPackage FULL_PATH_TO_OLD_NUPKG`
проверяет реальную старую standalone generator ссылку: компилятор выбирает одну
matching DLL из нового Cobalt package, direct consumer выполняет свой graph
(ещё1Release процесс). Legacy control необязателен без сохранённого старого пакета.
Каждый запуск имеет собственный fresh delivery каталог, logs/summary.json и
script/analyzer SHA; прежние результаты не перезаписываются.

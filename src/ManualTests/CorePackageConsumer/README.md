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

Скрипт пакует Release graph App/Cobalt/generator/Contracts/Microsoft DI adapter
в local feed. Consumer restore использует уникальную версию, изолированный cache
и source mapping `TrueMoon`/`TrueMoon.*` → local feed. Существующий кэш служит
read-only fallback для внешних dependencies. Каждый TrueMoon package должен
реально восстановиться из нового feed в изолированный cache.

## Матрица и проверки

| Consumer | Debug/Release × build/publish | Сценарии | Процессы |
| --- | --- | --- | --- |
| Headless app с явным generator | Cobalt и Microsoft DI | normal, cancel, start-failure, stop-failure, build-failure | 40 |
| Direct Cobalt без App/adapter | Cobalt | Composite, open generic, transient, owned/borrowed disposal | 4 |
| Headless Microsoft DI без consumer generator | Microsoft DI | те же 5 lifecycle scenarios | 20 |

Каждый процесс ограничен timeout и записывает JSON; failure завершает runner
с ненулевым кодом. Проверяются:

- consumer-generated sources и ModuleInitializer, реальные compiler analyzer
  path/SHA256 из отдельного package; runtime output не содержит analyzer/Roslyn;
- shared Composite и lifetime aliases, open/closed generic dependencies,
  singleton/runtime-factory/instance/transient identity;
- configuration priority/conversion и app name, observable app/consumer diagnostics,
  observer error isolation, source/subscription cleanup после shutdown/build failure;
- нормальное lifetime.Cancel, внешняя cancellation с исходным token, исходные
  startup/stop/build exceptions, reverse rollback/stop и отдельный shutdown token;
- owned service/factory/transient disposal и borrowed instance retention.

Cobalt освобождает shared Worker один раз; Microsoft DI может вызвать Dispose
через несколько aliases. Worker поддерживает повторный Dispose и учитывает
освобождение собственного ресурса отдельно от числа вызовов. Provider differences
не скрываются. Отрицательная сборка удаляет generator package, сохраняя ссылку
на consumer-generated namespace; ожидается CS0246/CS0234, а не passing build.

Normal output не содержит UI/Skia/Silk/IPC/units dependencies. Delivery —
framework-dependent, без RID, trimming, single-file или NativeAOT. Ctrl+C/kill,
Linux/macOS и другие compiler hosts не входят в эту матрицу.

В run-каталоге: local feed, isolated packages, copies consumers, generated sources,
build/publish output, logs, 64 process reports и `summary.json` с source/package/
analyzer SHA256 и SDK. `Completed=true` выставляется только после всей матрицы
и отрицательного контроля. Не считать промежуточный failed runner успешным итогом.

Дополнительно скрипт извлекает точные C# examples из доставленных PACKAGE.md
TrueMoon/Cobalt/adapter: 3 отдельные Release сборки и запуска на тех же пакетах.
Примеры компилируются без ручной адаптации исходников; logs и source SHA256 входят
в summary. Они учитываются отдельно от 64 сценариев основной матрицы.

## Явное подключение generator

```xml
<PackageReference Include="TrueMoon" Version="YOUR_VERSION" />
<PackageReference Include="TrueMoon.Cobalt.Generator" Version="YOUR_VERSION" PrivateAssets="all" />
```

TrueMoon/Cobalt runtime NuGet packages не доставляют analyzer автоматически.
ProjectReference analyzer в сборке фреймворка генерирует только его registrations;
registrations приложения требуют отдельного generator reference. Microsoft DI
строит свой graph без consumer generator. Подробнее: [Cobalt package README](../../TrueMoon.Cobalt/PACKAGE.md)
и [Core package README](../../TrueMoon/PACKAGE.md).

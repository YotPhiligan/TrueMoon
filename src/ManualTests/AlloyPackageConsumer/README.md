# Независимый UI package consumer

Проверка поставки5.4–5.5: приложение создаёт собственные model/view и получает TrueMoon только через NuGet. ProjectReference и compile-link библиотечных исходников отсутствуют. Проект намеренно не входит в solution; его локальные Directory.Build.props/targets и Directory.Packages.props прекращают наследование repository build/CPM settings.

## Воспроизведение

Из `src`, PowerShell7, Windows x64, .NET SDK10:

```powershell
pwsh -NoProfile -File ManualTests/AlloyPackageConsumer/RunPackageSmoke.ps1
```

Скрипт создаёт новый каталог `TestResults/AlloyPackages/run-*` и сохраняет каждый предыдущий failure. Он:

1. Проходит ProjectReference graph Hosting/App/Microsoft DI adapter и пакует необходимые проекты в локальный feed с уникальной prerelease версией. Общая версия в Directory.Build.props не меняется, публикации пакетов нет.
2. Копирует только собственные исходники consumer и его настройки сборки. Генерирует NuGet.Config с source mapping TrueMoon/TrueMoon.* → local feed и отдельным package cache; существующий cache используется как read-only fallback для внешних зависимостей. Проверяет фактическую локальную source metadata каждого восстановленного TrueMoon package.
3. Проверяет layout renderer nupkg и SHA-256 native DLL в build/publish output. Пакет Hosting — единственная прямая UI ссылка consumer; renderer, platform, runtime и controls должны прийти транзитивно.
4. Запускает raster/OpenGL/Vulkan в Debug и Release из build и framework-dependent publish output: **12 отдельных процессов**. Raster сохраняет PNG, windows закрываются после20кадров. Проверяются deferred native initialization, layout, two-way binding, theme/queued update, сохранение модели, освобождение дерева/подписок/session registry и App startup/shutdown/disposal.
5. Создаёт отдельный direct renderer consumer только с PackageReference на Rendering.Skia, без Hosting/App/Core/Contracts/platform. Его raster/native smoke выполняется из build/publish в Debug/Release: ещё **4 процесса**.

Фиксированный каталог/версию можно задать явно; каталог должен быть новым и находиться внутри `src/TestResults/AlloyPackages`:

```powershell
pwsh -NoProfile -File ManualTests/AlloyPackageConsumer/RunPackageSmoke.ps1 `
  -RunDirectory E:/source/my/TrueMoon/src/TestResults/AlloyPackages/my-run `
  -PackageVersion 0.2.2.7-alpha.consumer.my-run
```

Артефакты: `summary.json`, input fingerprints, package SHA-256, feed/cache, project.assets.json и NuGet-generated imports, consumer source/output, publish folders,12runtime JSON reports,4raster PNG, command logs. При restore внешних зависимостей может потребоваться nuget.org; machine-wide NuGet/Windows/driver настройки не меняются.

Скрипт проверяет build/publish на net10.0 без RID, framework-dependent, в Windows x64 процессе. Self-contained/single-file/trimming/NativeAOT, ARM64 и другие OS не входят в область проверки. Применён Microsoft DI adapter; Core5/COB4 headless/analyzer delivery и другие providers этим UI consumer не закрываются. Native GPU процессы запускаются последовательно. Vulkan validation, full golden, физический DPI, real device loss, long resource/VRAM soak и alpha acceptance не проверяются этим smoke.

NU5100 при pack native DLL вне lib — известное предупреждение: DLL не должна добавляться как managed reference. Остальные pack warnings сохраняются в логах, а не скрываются настройками NoWarn.

## Использование подготовленного consumer

В каталоге `consumer` конкретного прогона уже есть локальный NuGet.Config. Сохраните версию из summary:

```powershell
dotnet build AlloyPackageConsumer.csproj -p:TrueMoonPackageVersion=VERSION_FROM_SUMMARY
dotnet bin/Debug/net10.0/AlloyPackageConsumer.dll raster raster.json
dotnet bin/Debug/net10.0/AlloyPackageConsumer.dll opengl opengl.json
dotnet bin/Debug/net10.0/AlloyPackageConsumer.dll vulkan vulkan.json
```

`ConsumerModel` реализует INotifyPropertyChanged. `ConsumerView` смешивает фабрики, Fluent composition и BindTwoWay; модель живёт в App singleton, дерево принадлежит сессии. В standalone режиме callback FramePresented работает на общем UI owner thread; из других потоков используйте HostedUiWindow.Post. Raster session создаётся/используется/dispose на одном потоке до async App stop.

Результат поставки и открытые alpha критерии — [UI STATUS](../../docs/alloy/STATUS.md), устройство renderer — [README](../../TrueMoon.Alloy.Rendering.Skia/README.md), полные контракты App/ownership — [Hosting](../../TrueMoon.Alloy.Hosting/README.md).

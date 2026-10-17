# Независимый UI package consumer

Проверка поставки5.4–5.5: собственные model/view получают TrueMoon только через
PackageReference. Проект намеренно не входит в solution. Локальные
Directory.Build.props/targets и Directory.Packages.props прекращают наследование
repository build/CPM settings; ProjectReference запрещены проверкой сборки.

## Воспроизведение

Из `src`, PowerShell7, Windows x64, .NET SDK10:

```powershell
pwsh -NoProfile -File ManualTests/AlloyPackageConsumer/RunPackageSmoke.ps1
```

Runner создаёт новый `TestResults/AlloyPackages/run-*`, уникальную prerelease
версию, local feed и isolated cache. Предыдущие failures не перезаписываются.
Общая версия репозитория и machine-wide настройки не меняются; пакеты не публикуются.

1. Обходит producer ProjectReference graph, пропускает IsPackable=false generator
   и создаёт10runtime nupkgs. Cobalt содержит собственный matching analyzer;
   producer/package SHA и package README/license/dependency versions проверяются.
2. Копирует только consumer source/configuration и лицензированные Noto Sans,
   Noto Sans Symbols/OFL из `AlloyTest/Assets/Fonts`. Это assets приложения,
   renderer NuGet package не доставляет demo fonts.
3. Восстанавливает только packages: source mapping TrueMoon/TrueMoon.* → local
   feed, отдельный cache; existing cache служит read-only fallback для сторонних
   зависимостей. Проверяет source metadata и точные версии всех10TrueMoon packages.
4. Собирает два независимых consumer:
   - `generated`: bundled analyzer включён, один binary запускается на Cobalt и DI;
   - `di-only`: EnableCobaltGenerator=false/RequireCobaltGeneration=false,
     компиляция и DI работают без Cobalt analyzer/generated graph.
5. Для каждого варианта выполняет Debug/Release × build/framework-dependent publish
   без RID. Raster/OpenGL/Vulkan ×3provider/compilation combinations дают
   **36 App процессов**. Проверяются constructor graph/module initializer,
   explicit fonts, native LoadedPath, layout, two-way binding, theme/queued update,
   raster pixels/PNG, сохранение модели и cleanup после Stop/Dispose:
   session registry/subscriptions0, owned model disposed once. Consumer также
   рендерит RichTextEditor, применяет formatting/undo и проверяет его disposal;
   Topten.RichTextKit/HarfBuzz приходят транзитивно через renderer.
6. Создаёт direct renderer consumer только с Rendering.Skia и explicit fonts,
   без Hosting/App/Core/Contracts/platform: **4 процесса** Debug/Release ×build/publish.
   Проверяет реальный raster text, static redraw и disposal дерева.
7. В отдельном consumer отключает analyzer, сохраняя compile-time ссылку на generated
   registration. Build должен завершиться CS0246/CS0234; это **negative control**,
   а не successful runtime case.
8. Сверяет финальные source/nupkg/copied source/generated/native/font fingerprints.
   Completed=true требует всех36+4cases и expected negative control.

Каждый дочерний процесс ограничен deadline: build/restore/pack180s, runtime45s.
При timeout runner завершает только собственное дерево процессов и сохраняет logs.
GPU процессы выполняются последовательно. Для restore внешних зависимостей может
потребоваться nuget.org.

Фиксированный каталог/версию можно задать явно; каталог должен быть новым и внутри
`src/TestResults/AlloyPackages`:

```powershell
pwsh -NoProfile -File ManualTests/AlloyPackageConsumer/RunPackageSmoke.ps1 `
  -RunDirectory E:/source/my/TrueMoon/src/TestResults/AlloyPackages/my-run `
  -PackageVersion 0.2.2.7-alpha.consumer.my-run
```

Артефакты: summary.json с commands/PIDs/exit codes и fingerprints, feed/cache,
project.assets.json, compiler analyzer lists и generated sources, copied consumer,
build/publish output,36runtime JSON reports,12raster PNG, stdout/stderr каждого
command, negative-control diagnostics. Summary сохраняется и при failure.

## Подключение и использование

Для default Cobalt consumer явно регистрирует concrete constructor graph:

```csharp
var builder = App.Builder(_ => { });
builder.Setup(app =>
{
    app.UsePresentation<ConsumerView>(options =>
        options.UseSkiaOpenGL(fonts).UseSilkWindow());
    app.Services(services => services.Singleton<ConsumerModel>().Transient<ConsumerView>());
});
```

Одного generic вызова UsePresentation недостаточно, чтобы analyzer увидел constructor
пользовательского view. DI выбирается через `App.Builder(options => options.UseDI())`;
регистрации остаются собственным TrueMoon API. Microsoft adapter сохраняется.
Singleton model не имеет aliases: его1disposal не утверждает равенство disposal counts
оконных aliases у обоих providers.

`SkiaFontCatalog.Load` валидирует immutable font snapshots через native Skia до App
configuration. Поэтому smoke проверяет отсутствие native initialization до Load и
отсутствие UI sessions/bindings до App start; Build не создаёт window/GPU context.
Font files/license доставляются собственным Content consumer в build/publish.

В `generated` каталоге готового run сохранён локальный NuGet.Config.
Используйте версию из summary:

```powershell
dotnet build AlloyPackageConsumer.csproj -p:TrueMoonPackageVersion=VERSION_FROM_SUMMARY
dotnet bin/Debug/net10.0/AlloyPackageConsumer.dll raster raster.json cobalt
dotnet bin/Debug/net10.0/AlloyPackageConsumer.dll opengl opengl.json di
dotnet bin/Debug/net10.0/AlloyPackageConsumer.dll vulkan vulkan.json cobalt
```

В standalone callback FramePresented работает на UI owner thread; из других потоков
используйте HostedUiWindow.Post. Raster session создаётся, используется и dispose
на одном потоке до async App stop. Сессия владеет деревом; model принадлежит App.

Self-contained/single-file/trimming/NativeAOT, ARM64 и другие OS не входят в scope.
Vulkan validation, full golden, physical DPI, real loss, long resources/VRAM и
alpha acceptance этим smoke не закрываются. K7 NVIDIA остаётся внешним блокером.
Pack warnings сохраняются: NU5100 для native DLL вне lib не означает managed
reference; предупреждения не скрываются через NoWarn.

[UI STATUS](../../docs/alloy/STATUS.md), [renderer](../../TrueMoon.Alloy.Rendering.Skia/README.md),
[Hosting/ownership](../../TrueMoon.Alloy.Hosting/README.md).

# Независимый UI package consumer

Проверка поставки5.4–5.5: собственные model/view получают TrueMoon только через
PackageReference. Проект намеренно не входит в solution. Локальные
Directory.Build.props/targets и Directory.Packages.props прекращают наследование
repository build/CPM settings; ProjectReference запрещены проверкой сборки.

## Воспроизведение

Из `src`, PowerShell 7, Windows x64, .NET SDK 11:

```powershell
pwsh -NoProfile -File ManualTests/AlloyPackageConsumer/RunPackageSmoke.ps1
```

Runner создаёт новый `TestResults/AlloyPackages/run-*`, уникальную prerelease
версию, local feed и isolated cache. Предыдущие failures не перезаписываются.
Общая версия репозитория и machine-wide настройки не меняются; пакеты не публикуются.

1. Обходит producer ProjectReference graph, пропускает IsPackable=false generator
   и создаёт девять runtime nupkgs. Cobalt и Argentis содержат собственные анализаторы;
   producer/package SHA и package README/license/dependency versions проверяются.
2. Копирует только consumer source/configuration и лицензированные Noto Sans,
   Noto Sans Symbols/OFL из `AlloyTest/Assets/Fonts`. Это assets приложения,
   renderer NuGet package не доставляет demo fonts.
3. Восстанавливает только packages: source mapping TrueMoon/TrueMoon.* → local
   feed, отдельный cache; existing cache служит read-only fallback для сторонних
   зависимостей. Проверяет точный набор из девяти TrueMoon-пакетов, их версии и источники. `TrueMoon.Core` не требуется.
4. Собирает два независимых consumer:
   - `generated`: bundled analyzer включён, один binary запускается на Cobalt и DI;
   - `di-only`: EnableCobaltGenerator=false/RequireCobaltGeneration=false,
     компиляция и DI работают без Cobalt analyzer/generated graph.
   Оба варианта получают Argentis транзитивно от Hosting; проверяются один
   анализатор из пакета и генерация именованных частей, ролей и фабрики View.
5. Для каждого варианта выполняет Debug/Release × build/framework-dependent publish
   без RID. Raster/OpenGL/Vulkan ×3provider/compilation combinations дают
   **36 App процессов**. Проверяются constructor graph/module initializer,
   explicit fonts, native LoadedPath, layout, two-way binding, theme/queued update,
   raster pixels/PNG, сохранение модели и cleanup после Stop/Dispose:
   session registry/subscriptions0, owned model disposed once. Consumer также
   рендерит RichTextEditor, применяет formatting/undo и проверяет его disposal;
   Topten.RichTextKit/HarfBuzz приходят транзитивно через renderer.
6. Создаёт direct renderer consumer только с Rendering.Skia и explicit fonts,
   без Hosting/App/Core/platform; Contracts приходит для IUiSessionFactory/IFactory:
   **4 процесса** Debug/Release ×build/publish. Проверяется точный набор4TrueMoon packages.
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

### Адресная проверка композиции View (UIA1)

```powershell
pwsh -NoProfile -File ManualTests/AlloyPackageConsumer/RunPackageSmoke.ps1 -ViewCompositionOnly
```

Свежий feed/cache и та же проверка анализаторов; только Debug build, шесть
процессов: Raster/OpenGL × Cobalt и Microsoft DI с анализатором Cobalt,
а также Microsoft DI без него. На первом кадре проверяются конкретный дочерний
View из собственной фабрики, именованные части, scoped-стили и строки
`ViewWithArgs` с аргументом коллекции и зависимостью из DI. Перемещение
сохраняет экземпляр строки и привязку, удаление освобождает строку, закрытие
освобождает дерево и подписки. Этот профиль не запускает publish, Vulkan,
direct renderer и negative control полной матрицы.

### Адресная проверка презентеров (UIA1h)

Из `src` одна команда упаковывает только граф `TrueMoon.Alloy.Rendering.Skia`,
восстанавливает независимый проект из нового feed/cache, собирает Release и
запускает один CPU raster процесс без App, окна или GPU:

```powershell
pwsh -NoProfile -File ManualTests/AlloyPackageConsumer/RunPackageSmoke.ps1 -PresentersOnly
```

Пример миграции с `DrawCore` — [PresenterScreen.cs](PresenterScreen.cs).
Закрытый `SignalButton` сохраняет ввод `Button` и не ссылается на `Signal`,
который его рисует. В `UiSession.Create(..., configurePresenters: ...)`
регистрация связывает точный
тип контрола, захват `Bounds` и фабрику презентера; `RegisterIconButton` в той же
сессии заменяет только штатный рисунок `IconButton`. Код сверяет конкретные пиксели
обоих элементов и по нажатию/отпусканию проверяет захват, фокус и одну активацию
только целевой кнопки. Вторая сессия со штатным значком продолжает рисовать
и принимать ввод после закрытия первой. В `direct-Release-build-presenters.json`
сохранены semantic ID, типы, основание выбора и результат проверки изоляции;
команды pack/restore/build/run записаны в `summary.json`.

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

Потребитель использует сгенерированную фабрику View без ручного подключения анализатора:

```csharp
var builder = App.Builder(_ => { });
builder.Setup(app =>
{
    app.Services(services => services.Singleton<ConsumerModel>().View<ConsumerView, ConsumerStyle>());
    app.UsePresentation<ConsumerView>(options => options.UseSkiaOpenGL(fonts).UseSilkWindow());
});
```

DI выбирается через `App.Builder(options => options.UseDI())`;
регистрации и создание корня не меняются.
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
dotnet bin/Debug/net11.0/AlloyPackageConsumer.dll raster raster.json cobalt
dotnet bin/Debug/net11.0/AlloyPackageConsumer.dll opengl opengl.json di
dotnet bin/Debug/net11.0/AlloyPackageConsumer.dll vulkan vulkan.json cobalt
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

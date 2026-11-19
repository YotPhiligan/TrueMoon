# Приёмка mesh surface — ARCH1f.4 и ARCH1f.5

Windows x64, общий SDK из `global.json`, доступные OpenGL external-memory/semaphore
extensions и matching Vulkan adapter. Используется уже установленный/сохранённый
`VK_LAYER_KHRONOS_validation` с synchronization validation; runner его не скачивает.

Из `src` в PowerShell 7:

```powershell
./ManualTests/RenderSurface/Verify.ps1 -ValidationDirectory <каталог-с-DLL-и-JSON-слоя>
./ManualTests/RenderSurface/Verify.ps1 -BaseColorTexture -ValidationDirectory <каталог-с-DLL-и-JSON-слоя>
```

`-RunDirectory` и `-EvidenceDirectory` необязательны, должны быть новыми. RunDirectory
обязательно вне checkout. Script сохраняет общий BuildProfile, unique MSBuild binlogs,
source/package/validation hashes, private offline feed/cache и результаты трёх native
профилей: source Debug, published Release, package-only Release. Нужные внешние nupkg
берутся в точных версиях из существующего restored cache. 18 пакетов TrueMoon
пересобираются для каждого прогона. `-BaseColorTexture` использует текущую версию
из `Directory.Build.props` в отдельном локальном feed.

`-BaseColorTexture` собирает два артефакта одного меша с UV0, одним sampler
`NearestClamp` и разными ревизиями текстуры 2×2. Запуск A → B → A проверяет
ревизии Vulkan upload, четыре texels, две точки с UV вне [0,1] и равенство
кадров A. [Контракт и проверки](../../docs/engine/RENDER_SURFACE.md#base-color-texture).

В обоих сценариях actual Vulkan→OpenGL RGBA readback показан в Skia window. Только
DrawCore плюс `FramePresented` разрешают подтверждение stamp. ARCH1f.4 проверяет два куба
с общим mesh/material, independent linear tint, reset, shared material reload,
delete, geometry revision, resize/suspend/resume, Stop/new generation, Unsupported
checkpoint Replace без остановки текущей игры и failed camera после native upload.
Каждый штатный профиль даёт10captures/3children, validation0errors/0warnings и нулевые
остаточные ownership counters. Третий child обязан сохранить ожидаемую startup error.

Fixture [RenderSurface.Game](../RenderSurface.Game/MeshGame.cs) загружается только
в child; parent не ссылается на его проект/сборку. Runtime использует обычный example
host, включая App/Cobalt integration. UI/renderer types не загружаются в child.

Полный контракт, точные unit-test области и границы — [RENDER_SURFACE](../../docs/engine/RENDER_SURFACE.md).
Этот runner не проверяет logical material/content checkpoint migration, textures,
lighting, device loss, многосуточную стабильность или physical multi-adapter matrix.

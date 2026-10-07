# Эксперимент внешнего Vulkan handoff

Продолжение: `--interop-compose --validation` проверяет **36 GPU-композиций** независимыми Vulkan graphics pipelines на **18 leases** с возвратом shader-read layout; validation 0 ошибок/0 предупреждений. Readback выполняется только после композиции. Исходники shaders, инструкции пересборки SPIR-V и команды: [Shaders/README.md](../Shaders/README.md). Следующий графический шаг — оконный swapchain/resize/presentation.

DLL теперь хранится в `TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64`, копируется при build/publish и автоматически загружается AlloyVulkanTest на Windows x64. Постоянная инструкция хранения, использования и пересборки: [README renderer](../../../TrueMoon.Alloy.Rendering.Skia/README.md). Для обновления DLL после native source build добавьте `-InstallToRenderer`; скрипт проверит версию managed-пакета и обновит DLL/build.json. Явный `--native-skia` сохраняется для альтернативных сборок.

На 2026-10-04 native extension для текущего SkiaSharp **4.153.1** собран и проверен: **18 внешних Vulkan чтений**, возврат состояния и повторная отрисовка; core/synchronization validation — **0 ошибок, 0 предупреждений**, включая teardown. Это эксперимент в ManualTests, а не публичный контракт renderer. Эта первичная проверка handoff была расширена GPU-композицией выше; оконный swapchain остаётся следующим шагом.

## Зафиксированные исходники

Скрипты читают [SourcePins.psd1](SourcePins.psd1) и отвергают неизвестные revisions. DLL должна совпадать с текущим managed-пакетом; официальный SkiaSharp version gate выполняется до проверки C ABI и создания Vulkan device.

| SkiaSharp | SkiaSharp commit | Skia commit |
| --- | --- | --- |
| 4.152.1 | 47f1630989b6d4d5c347b01dcfc10f0fb49cce74 | 723c5a03a7d18de5fb1a041e6cae2f09c4cb3c7e |
| 4.153.1 | 4783f51448f9b070dda4f87b83e941c9599e466e | 45afab4f1f0921f3feb97f58cf89b136fffa85e6 |

Обе native-сборки прошли. DLL 4.152.1 отклоняется текущим managed 4.153.1 с кодом 2; проверка совместимости не обходится.

## ABI и владение

[truemoon_vk_interop.inc](truemoon_vk_interop.inc) включается один раз в upstream src/c/sk_surface.cpp и собирается **в ту же libSkiaSharp**, которую использует SkiaSharp. Отдельная копия Skia не загружается.

- tm_skia_vk_interop_abi_version и tm_skia_vk_texture_info_size проверяют ABI v1 и 48-байтный descriptor.
- tm_skia_vk_surface_acquire использует SkSurfaces::GetBackendTexture / GrBackendTextures::GetVkImageInfo и создаёт backend descriptor с shared mutable state. Поддерживается непротектированная single-level/single-sample RGBA8888-текстура с transfer-source usage.
- tm_skia_vk_texture_return применяет MutableTextureStates::MakeVulkan через setMutableState. Vulkan barrier записывает хост.
- tm_skia_vk_texture_delete удаляет descriptor; VkImage остаётся собственностью surface, device — собственностью хоста.

Managed lease удерживает владельца и SafeHandle descriptor. Рисование, повторный acquire и Dispose surface при активном borrow запрещены; возврат явно принимает фактический layout. Контекст/surface живут до возврата текстуры.

## Последовательность

1. UI рисует в поверхность на устройстве хоста.
2. Native acquire экспортирует VkImage/state; managed вызывает Submit(true) и проверяемый DeviceWaitIdle.
3. Внешний consumer на той же graphics queue выполняет barrier в TransferSrcOptimal, CmdCopyImageToBuffer и barrier для host read assertion-buffer.
4. После завершения GPU-work lease возвращает фактические layout/queue family в shared mutable state Skia.
5. Следующий кадр рисуется в той же surface. Probe проверяет 2 контекста × 3 размера × 3 кадра, прозрачность, premultiplied panel, marker, текст, стабильность handle и borrow/return guards.

Операции идут последовательно на потоке создания. Разные очереди, ownership transfer и async semaphore handoff не поддерживаются. CPU readback в --interop нужен для assertions UI; режим --interop-compose отдельно проверяет внешнюю GPU-композицию.

## Сборка

Команды выполняются из src. TestResults игнорируется Git. Системный NuGet cache не подменяется.

Нужны Git, .NET 10, Python 3, Ninja, MSVC x64, Windows SDK и x64 Spectre libraries. В текущем запуске установлены Build Tools 2022 17.14.41 в C:/BuildTools/TrueMoonVS2022, MSVC 14.44.35207 и SDK 10.0.26100.0. Ninja 1.13.2 взят из [официального release](https://github.com/ninja-build/ninja/releases/tag/v1.13.2); SHA-256 архива проверен: 07FC8261B42B20E71D1720B39068C2E14FFCEE6396B76FB7A795FB460B78DC65.

Пример использует реальный Python из LibreOffice; WindowsApps alias и launcher LibreOffice не подходят для дочерних процессов. При другом Python пути/переменные надо заменить.

```powershell
git clone --depth 1 --branch v4.153.1 https://github.com/mono/SkiaSharp.git TestResults/VulkanInterop/SkiaSharp-4.153.1
$env:PYTHONHOME = 'C:/Program Files/LibreOffice/program/python-core-3.13.15'
$env:PATH = 'C:/Program Files/LibreOffice/program;' + $env:PATH
pwsh -NoProfile -File ManualTests/AlloyVulkanTest/NativeInterop/BuildNativeInterop.ps1 `
  -SkiaSharpSourcePath TestResults/VulkanInterop/SkiaSharp-4.153.1 `
  -VisualStudioPath C:/BuildTools/TrueMoonVS2022 `
  -PythonPath 'C:/Program Files/LibreOffice/program/python-core-3.13.15/bin/python.exe' `
  -NinjaPath TestResults/VulkanInterop/tools/ninja/ninja.exe -CheckOnly
# Повторить без -CheckOnly для source build.
```

BuildNativeInterop.ps1 инициализирует shallow pinned submodules, проверяет gitlink, применяет extension и запускает upstream Cake libSkiaSharp с Vulkan, без Direct3D. Git core.longpaths и PATH настраиваются только для процесса и восстанавливаются; глобальные Git/Windows настройки не меняются. Скрипт подбирает MSVC prefix 14.4 для pinned GN recipe. ApplyNativeInterop.ps1 создаёт feature branch при detached HEAD/защищённой ветке и допускает повторное применение идентичного extension; отличающиеся файлы не перезаписывает. externals-download не вызывается.

Готовая DLL: TestResults/VulkanInterop/SkiaSharp-4.153.1/output/native/windows/x64/libSkiaSharp.dll.

SHA-256 текущего артефакта: 96525E8EFC1E3BB4B9C64000307D4D11C378FEDAEEB7C1E62D518EF84CDD1ABE.

## Запуск

Одна DLL передаётся resolver-ам обоих bindings до первого native-вызова; пакетная DLL не подменяется.

```powershell
dotnet build ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj
$probe = 'ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll'
$native = 'TestResults/VulkanInterop/SkiaSharp-4.153.1/output/native/windows/x64/libSkiaSharp.dll'
dotnet $probe --interop-check --native-skia $native
dotnet $probe --interop --native-skia $native

$env:VK_LAYER_PATH = (Resolve-Path TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin).Path
dotnet $probe --interop --validation --native-skia $native
dotnet $probe --validation --native-skia $native
# Исходный smoke (теперь автоматически использует bundled DLL):
dotnet $probe
```

Для validation нужны VkLayer_khronos_validation.dll и его JSON manifest. В этом запуске они извлечены в указанный локальный Bin из подписанного LunarG Vulkan SDK 1.4.363.0 (Authenticode Valid); SDK installer не выполнялся для установки, системный Vulkan runtime/registry не менялись. Можно указать Bin другого установленного SDK. VK_LAYER_PATH действует для процесса/дочерних процессов. --validation требует слой явно, включает synchronization validation, сохраняет callback delegate живым до DestroyInstance и проверяет счётчики после полного teardown. Отсутствие слоя возвращает код 2, без пропуска проверки.

## Фактические проверки 2026-10-04

| Проверка | Результат |
| --- | --- |
| Native source build 4.152.1 / 4.153.1 | Оба exit 0 |
| Managed build | 0 ошибок; финальный incremental: 7 предупреждений (restore/build зависимостей: 464) |
| ABI preflight 4.153.1 | exit 0 |
| Внешний Vulkan handoff | 18 чтений, exit 0; также повторён с validation |
| Interop core/synchronization validation | 0 ошибок / 0 предупреждений после teardown |
| Исходный smoke, штатная DLL | 12 GPU-композиций, exit 0 |
| Исходный smoke, patched DLL + validation | 12 композиций, exit 0; 0 ошибок / 0 предупреждений |
| Несовместимая native 4.152 / managed 4.153 | Явный preflight отказ, exit 2 |
| Отсутствующий validation layer | Явный отказ, exit 2 |

Логи находятся в TestResults/VulkanInterop: native-build-4.153.1.log, managed-build-validation.log, interop-validation.log, smoke-validation.log, default-smoke.log, missing-validation-layer.log. Native build имеет upstream warnings; нулевое число build warnings не утверждается. UI unit tests и всё решение в этот день не запускались; 29 тестов от 2026-09-27 — исторический результат.

На 2026-10-02 были готовы managed prototype и smoke, но native build был заблокирован отсутствием MSVC. Блокер снят текущей сборкой/запусками.

Внешний graphics pipeline уже реализован и проверен через --interop-compose. Следующий конкретный шаг — swapchain, оконный resize/presentation и длительные проверки.

# Shaders внешнего Vulkan consumer

`fullscreen.vert` задаёт fullscreen triangle без vertex buffer. `scene.frag` рисует независимую сцену с двумя цветными областями; `hud.frag` читает заимствованную UI-текстуру. HUD pipeline использует premultiplied alpha: `ONE / ONE_MINUS_SRC_ALPHA` для RGB и alpha. Композиция выполняется на GPU; CPU получает только итоговое изображение для assertions.

SPIR-V файлы хранятся рядом с GLSL в Git и включаются в сборку примера как embedded resources. Обычный build/publish не требует shader compiler. После изменения GLSL необходимо пересобрать SPIR-V и выполнить probe.

Из `src`, с инструментами текущей машины:

```powershell
pwsh -NoProfile -File ManualTests/AlloyVulkanTest/Shaders/CompileShaders.ps1 `
  -GlslangValidator (Resolve-Path TestResults/VulkanInterop/tools/shaders-1.4.363.0/Bin/glslangValidator.exe) `
  -SpirvValidator (Resolve-Path TestResults/VulkanInterop/tools/shaders-1.4.363.0/Bin/spirv-val.exe)

dotnet build ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj
$env:VK_LAYER_PATH = (Resolve-Path TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin).Path
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --interop-compose --validation
```

Вместо этих путей можно указать `Bin` установленного LunarG SDK. Скрипт компилирует под Vulkan 1.1 и запускает `spirv-val` для каждого модуля; ошибка любого шага останавливает выполнение. В текущей машине инструменты извлечены из ранее скачанного подписанного SDK, без установки системного runtime.

Проверка: 2 Skia-контекста × 3 размера × 3 отрисовки × 2 сцены = **36 GPU-композиций** на **18 leases**. Проверяются неизменённый фон вне HUD, смешивание панели с обоими фонами, opaque marker, белые пиксели текста, opacity итоговой сцены и следующий redraw после возврата `ShaderReadOnlyOptimal`. UI VkImage остаётся у surface; host compositor владеет своим target, pipelines, descriptor, sampler и command buffer.

Переход изображения выполняется barrier перед fragment sampling; lease сообщает окончательный layout обратно Skia после завершения GPU-work. Последовательная схема использует то же устройство/graphics queue. Swapchain и оконное presentation в этот probe не входят.

Ссылки на используемые правила Vulkan: [image layout barriers](https://docs.vulkan.org/refpages/latest/refpages/source/VkImageMemoryBarrier.html), [blend attachment](https://docs.vulkan.org/refpages/latest/refpages/source/VkPipelineColorBlendAttachmentState.html).

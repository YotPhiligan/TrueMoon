# Legacy presentation compatibility

Переходная область в сборке `TrueMoon.Alloy.Hosting`, **не рекомендуется для нового UI**. В PLAN 2.1b сюда перенесён 31 legacy-файл из корня `TrueMoon.Alloy` и его `Presenters`. Перенос отделяет зависимости старого presentation от общего runtime, но не исправляет старый UI и не завершает этап 2.3/alpha.

## Breaking alpha migration

| До переноса | После переноса |
| --- | --- |
| Сборка `TrueMoon.Alloy`, namespace `TrueMoon.Alloy` для legacy типов | Сборка `TrueMoon.Alloy.Hosting`, namespace `TrueMoon.Alloy.Hosting.Compatibility` |
| Namespace `TrueMoon.Alloy.Presenters` | `TrueMoon.Alloy.Hosting.Compatibility.Presenters` |
| Legacy `UsePresentation<TView>()` без настроек | Opt-in extension из `TrueMoon.Alloy.Hosting.Compatibility` |

Сюда относятся Visual/VisualTree и builder, IGraphicsPlatform/GlGraphicsPlatform, ViewHandle/ViewManager, presentation initializer/configuration, content/view presenters и их factories. Независимые контракты Argentis и новый Alloy runtime этим переносом не меняются.

**Type forwarders отсутствуют.** Старые assembly-qualified имена изменены; замена DLL или только сохранение ссылки на Alloy не обеспечивают binary compatibility. Legacy consumers должны:

1. Добавить ProjectReference на `TrueMoon.Alloy.Hosting` вместо ссылки на Alloy как поставщика legacy типов; ссылку на Alloy оставлять только при прямом использовании runtime.
2. Заменить legacy imports на `using TrueMoon.Alloy.Hosting.Compatibility;` и, для content presenters, `using TrueMoon.Alloy.Hosting.Compatibility.Presenters;`. Обновить fully qualified/assembly-qualified имена, если они используются.
3. Пересобрать consumers и отдельно проверить их поведение. Перенос не является подтверждением работоспособности старого пути.

No-options `UsePresentation<TView>()` и его optional `Action<PresentationConfiguration>` доступны только при таком opt-in; они регистрируют прежний OpenGL/VisualTree путь (`TView : class, IView`). Не импортируйте Compatibility для нового UI.

## Рекомендуемый путь

Новый explicit API остаётся в `TrueMoon.Alloy.Hosting`: `UsePresentation<TView>(Action<AlloyOptions>)` для Argentis Element с явным backend/window или `UseAlloy(...)` для внешнего хоста. Он не изменён переносом 2.1b и не использует legacy VisualTree. [Примеры Hosting](../README.md), [AlloyTest с explicit OpenGL/Silk](../../ManualTests/AlloyTest/README.md).

`TrueMoon.Alloy` теперь содержит только Runtime/csproj и ProjectReference на Argentis, без PackageReference и Core/Contracts. Hosting явно подключает Silk.NET, SkiaSharp, Topten.RichTextKit и Core/Contracts вместе с Alloy/Rendering.Skia/Platform.Silk. Compatibility — область Hosting, не отдельная сборка; выбор только explicit API не удаляет эти зависимости из Hosting. Для direct `UiSession.Create` Hosting не требуется.

## Сохранённые ограничения

- `Visual.GetEnumerator` по-прежнему обходит весь backing array, а не только `_itemsCount`, включая незаполненные слоты. Эта ошибка не исправлена переносом.
- Legacy lifetime/cleanup bugs и старый порядок window/presenter/subscription disposal не исправлены. Гарантии нового HostedUiWindow/UiSession нельзя автоматически переносить на ViewHandle/ViewManager.
- `SkiaVulkanViewPresenter.cs`, `RenderObject.cs` и `TextureData.cs` — целиком закомментированные архивные прототипы, не рабочие рендереры и не доступные compiled API. Рабочие Vulkan/OpenGL/raster backends находятся в Rendering.Skia.
- Native ABI, GPU lease/ownership и алгоритм swapchain retirement нового пути не менялись.

Фактические проверки переноса и ограничения — в [STATUS](../../docs/alloy/STATUS.md). Compatibility consumer проверяет сборку и opt-in no-options регистрацию/factories/DI без запуска старого окна; успешные GPU/оконные проверки нового runtime не доказывают исправность legacy window/lifetime пути.

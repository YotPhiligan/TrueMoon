# K2/K4: фиксированные шрифты и полные кадры Settings

Контрольная сцена — исходные `View1`, `SettingsModel` и `SettingsFormSmoke` из
`ManualTests/AlloyTest`. Golden probe compile-link использует те же исходники,
40 шагов ввода/изменения модели и сравнивает logical bounds, focus/selection,
scroll, model state и redraw count на raster/OpenGL/Vulkan.

## Шрифты и владение

`SkiaFontCatalog.Load(defaultFamily, files)` читает и валидирует файлы один раз.
Каталог хранит managed byte snapshots, не удерживает native font handles и может
использоваться на нескольких потоках. Surface создаёт и освобождает собственные
SKFont в прежнем ограниченном FIFO-кеше16; resize/suspend не меняет font metrics.
Изменение или удаление исходного файла после Load не меняет загруженный каталог.

Точные aliases сравниваются ordinal/case-sensitive. Незарегистрированное семейство
использует explicit default face. В этом режиме нет `FromFamilyName`, системного
fallback или автоматического выбора другого шрифта для отсутствующего glyph.
Стандартные parameterless backend constructors и Hosting методы сохраняют
прежний выбор системных шрифтов.

```csharp
var fonts = SkiaFontCatalog.Load("UI", new Dictionary<string, string>
{
    ["UI"] = Path.Combine(AppContext.BaseDirectory, "Assets", "NotoSans-Regular.ttf"),
    ["Segoe UI Symbol"] = Path.Combine(AppContext.BaseDirectory, "Assets", "NotoSansSymbols-Regular.ttf")
});
var backend = new SkiaRasterRenderBackend(fonts);
// Также: new SkiaOpenGLRenderBackend(fonts), new SkiaVulkanRenderBackend(fonts).
// В Hosting: options.UseSkiaRaster(fonts), UseSkiaOpenGL(fonts), UseSkiaVulkan(fonts).
```

Load выполняет native font validation; вызывающий код доставляет лицензированные
font files в свой build/publish output. Это opt-in renderer API, не автоматическая
установка шрифтов Windows. NuGet renderer не включает демонстрационные шрифты.
Golden/test проекты доставляют собственные assets через PreserveNewest.

Fixture использует неизменённые Noto Sans и Noto Sans Symbols, revision и SHA256 —
в [Assets/Fonts](../../ManualTests/AlloyTest/Assets/Fonts/README.md), полная OFL1.1
лицензия доставляется рядом. Symbols alias рисует `↑`; Noto Sans Mono — отдельная
test fixture для различимых alias/monospaced metrics. Unsupported emoji сохраняют
фиксированный missing-glyph symbol. Script проверяет grapheme editing/selection;
emoji rendering, shaping и IME этим этапом не добавлены.

## Fixture matrix и допуск

30 сохранённых CPU raster PNG: logical viewport900×1100, scale1/1.5/2,
физические размеры900×1100/1350×1650/1800×2200. Это логические масштабы поверхности,
а не physical Windows DPI transitions. Settings view720×1000 окружён прозрачной
областью с полупрозрачными перекрывающимися shapes и clipped текстом.

Кадры снимаются **после Update и перед соответствующим шагом script**:

| Frame | Состояние |
| --- | --- |
| 0 | Начальная тёмная тема, image/controls/rows |
| 1 | Редактирование, focus/selection, изменённые checkbox/slider |
| 15/16 | Loading/Error conditional branches, scroll и retained neighbor selection |
| 22/23 | Светлая тема, обычная/compact плотность |
| 24 | Тёмная compact тема |
| 26 | Disabled controls |
| 28 | Scrolled list после ввода/wheel |
| 30 | Завершённый script, восстановленное состояние |

Все pixels сравниваются как **RGBA8888 premultiplied**, включая alpha. Нет masks,
процента допустимых outliers или исключений текста. Правила зафиксированы в manifest:

- Raster ↔ сохранённый golden и OpenGL ↔ Vulkan: абсолютный допуск2 на канал,
  без пространственного допуска; один пиксель за пределом означает failure.
- GPU ↔ сохранённый golden и GPU ↔ текущий raster: симметричная проверка каждого
  канала в3×3 соседстве (radius1). Sample обеих сторон должен попасть в min/max
  соседства другой стороны. Для локального contrast≥8 допуск к envelope24;
  для contrast<8 остаётся2. Alpha проходит те же проверки.
- CPU/GPU отличаются analytic coverage на сглаженных glyph/rounded-stroke краях:
  strict2 отвергал тысячи edge pixels; envelope+8/+12 оставлял реальные glyph
  coverage extrema, например rasterR50 против minimumGpuR70. Финальный24 ограничен
  этим edge rule. Он допускает мелкие различия coverage/позиции края, поэтому
  дополняется точным logical layout и строгим полным OpenGL/Vulkan comparison.

Normal run:90 golden comparisons +60 GPU/current-raster +30 Vulkan/current-OpenGL
= **180**. Recording:60 GPU/current-raster +30 Vulkan/current-OpenGL =90,
без утверждения, что freshly recorded кадры уже прошли regression против эталона.
30 reference PNG и manifest SHA256 доставляются в build/publish output.

## Запуск и диагностика

Из `src`, Windows x64, .NET10:

```powershell
pwsh -NoProfile -File ManualTests/AlloyVulkanTest/RunSettingsGolden.ps1 `
  -ValidationLayerPath TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin
```

ValidationLayerPath должен указывать на доступный manifest Khronos validation;
этот локальный TestResults путь — подготовленное окружение данного checkout,
не обязательная часть репозитория. Без параметра script запускается без validation
и не заявляет validation proof. Environment меняется только для своего child.

Script создаёт новый каталог TestResults, собирает Debug, публикует Release,
запускает каждый процесс с timeout180s и сверяет180 passing comparisons в JSON.
После двух запусков source fixtures должны сохранить SHA256. Existing output не
перезаписывается. Native HWND/context belong to bounded probe; WSI soak не выполняется.

В output: все90 actual PNG/configuration, `report.json`, diff PNG при failure,
stdout/stderr, build/publish logs и `summary.json`. Report содержит raw difference
counts/max/mean/first coordinate, edge-matched counts, source/binary/font/native/asset
SHA256, backend hardware, runtime/configuration и validation scope включая teardown.

Для намеренного обновления baseline записывать в **новый** отдельный каталог:

```powershell
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- `
  --settings-golden --record-golden `
  --golden-fixtures TestResults/AlloyGolden/new-fixtures `
  --golden-output TestResults/AlloyGolden/new-record
```

Затем визуально проверить кандидаты, их provenance и diff metrics, явно заменить
файлы `ManualTests/AlloyVulkanTest/Golden/Settings` и выполнить normal Debug/Release
run. Normal verification никогда автоматически не записывает baseline.
Несовпадение font/native/asset/PNG fingerprint отвергается до GPU запуска;
source hash сохраняется как provenance, но не блокирует проверку нового кода.

Unit regressions — [SkiaFontCatalogTests](../../TrueMoon.Alloy.Tests/SkiaFontCatalogTests.cs)
и [PixelDifferenceTests](../../TrueMoon.Alloy.Tests/PixelDifferenceTests.cs): snapshots,
aliases, missing glyph guard, eviction/disposal/thread ownership, допуск before/at/after,
alpha/last pixel, симметрия, missing edge, shift>1, flat/low-contrast changes и diff bytes.

Этот этап не закрывает alpha. Physical DPI/K3, внешний NVIDIA K7, real loss/K8,
VRAM и итоговая supported configuration matrix учитываются в [STATUS](STATUS.md).

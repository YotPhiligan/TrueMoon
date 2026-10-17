# RichTextEditor

`TrueMoon.Argentis.RichTextEditor` — retained multiline editor. `Topten.RichTextKit`
0.6.170 выполняет shaping/layout/hit testing/painting в Rendering.Skia.
Argentis содержит только нейтральные document/style/options contracts и editing;
зависимости Argentis → SkiaSharp/RichTextKit не добавлены. Raster/OpenGL/Vulkan
используют один adapter. net10.0/xUnit и общие UI ownership rules сохраняются.

## Создание и опции

```csharp
using TrueMoon.Argentis;

var editor = RichTextEditor.Create(new RichTextEditorOptions
{
    WordWrap = true,
    IsReadOnly = false,
    AcceptsReturn = true,
    AcceptsTab = false,
    UndoLimit = 100,
    Alignment = RichTextAlignment.Left,
    ShowSelectionWhenUnfocused = true,
    SelectionColor = new Color(60, 120, 200, 100)
});
editor.Height = 300;
editor.Padding = new Thickness(12);
editor.FontFamily = "Segoe UI";
editor.FontSize = 16;
editor.Document = new RichTextDocument([
    new RichTextRun("Заголовок\n", new RichTextStyle { Bold = true, FontSize = 24 }),
    new RichTextRun("Обычный текст и "),
    new RichTextRun("выделенный фрагмент", new RichTextStyle
    {
        Italic = true, Underline = true,
        Foreground = new Color(220, 80, 40)
    })
]);
```

Options — immutable record: `editor.Options = editor.Options with { IsReadOnly = true }`.
UndoLimit0..1000,0 выключает историю. Шрифт/размер/foreground/padding используют
обычные Element properties; формат runs может их переопределять. RichTextStyle
также поддерживает StrikeThrough, Background и положительный LineHeight multiplier.
Null members наследуются; ApplyStyle меняет только non-null members.

Read-only блокирует user edits/formatting/undo/redo, но разрешает focus, selection,
copy и navigation. Programmatic Text/Document replacement разрешён.
Disabled/disabled ancestor блокирует routed input и edit commands.
WordWrap=false включает горизонтальную прокрутку; wheel прокручивает вертикально.
ScrollTo задаёт логический offset, clamped к content extent; изменение selection
или layout возвращает caret в видимую область, обычный wheel не сбрасывается на caret.

## Документ, binding и индексы

RichTextDocument/RichTextRun/RichTextStyle — immutable snapshots. Constructor
копирует runs и объединяет соседние одинаковые styles. Runs используют LF
и valid UTF-16; RichTextDocument.FromText и editor.Text нормализуют CR/CRLF.
Прямой Set(TextProperty, ...) / TextProperty binding требует LF.
Невалидные surrogate sequences отклоняются до изменения текста/истории.

Выберите один источник binding:

```csharp
editor.BindTwoWay(RichTextEditor.DocumentProperty, model, state => state.Document);
// Или plain-text model:
// editor.BindTwoWay(RichTextEditor.TextProperty, model, state => state.Text);
```

Модель реализует INotifyPropertyChanged; обновления доставляются через owner queue
UiSession, а subscriptions освобождаются при disposal. DocumentProperty отражает
также formatting-only изменения. TextProperty содержит plain text.
External Text replacement сбрасывает formatting; external Text/Document replacement
очищает undo/redo. Одновременные bindings обоих свойств к разным источникам не нужны.

CaretIndex, SelectionStart/Length, Select и SelectedText — **UTF-16**, совместимые
со string.Substring. Control округляет selection boundaries вниз до grapheme start,
Left/Right/Backspace/Delete работают с целыми graphemes, включая surrogate pairs,
combining marks и ZWJ sequences. RichTextKit code-point offsets наружу не выходят.
Left/Right — logical grapheme navigation; Up/Down и Home/End — visual lines.
Полная bidi caret affinity остаётся отдельной задачей.

```csharp
editor.Select(0, 9);
editor.ApplyStyle(new RichTextStyle { Bold = true, Foreground = new Color(40, 140, 220) });
editor.InsertText("Новый текст");
editor.Undo();
editor.Redo();
editor.SelectAll();
```

При collapsed caret ApplyStyle задаёт стиль следующего ввода. Undo/redo сохраняет
document formatting и selection; после undo новая edit ветка удаляет redo.
Один InsertText/Text event или formatting command — один history step, typing
coalescing не добавлен. History ограничена числом snapshots, не суммарными bytes.
StateChanged сообщает изменение selection/document/history для внешнего toolbar.

## Ввод и toolbar

Ctrl+A/C/X/V, Ctrl+Z/Ctrl+Shift+Z/Ctrl+Y, Ctrl+B/I/U; Enter — paragraph.
Ctrl+Left/Right — переход между whitespace-delimited words; Ctrl+Home/End —
границы документа. Mouse down/drag использует session focus/capture;
focus loss/detach сбрасывает drag. Clipboard — plain Unicode text, включая LF.

AcceptsTab=true вставляет4spaces. Shift+Tab продолжает focus traversal;
read-only editor не удерживает Tab. Session сначала даёт focused control обработать
Tab, затем выполняет прежнюю traversal policy. Silk mapping дополнен Up/Down/Z/Y/B/I/U.

Toolbar — отдельная композиция Button/CheckBox вокруг editor. Selection можно
показывать без focus через ShowSelectionWhenUnfocused. Готовый пример с кнопками
и options — [RichTextEditorTest](../../ManualTests/RichTextEditorTest/README.md).

## Renderer и шрифты

Rendering.Skia транзитивно доставляет Topten.RichTextKit/SkiaSharp.HarfBuzz и
соответствующие dependencies. Custom backend реализует IRichTextLayoutService и
IRichTextDrawingContext; без capability editor сообщает NotSupportedException,
а существующие IDrawingContext/ITextLayoutService implementations не меняют API.

Layouts принадлежат editor, освобождаются при изменении/reflow/detach/dispose.
Painting/hit testing/caret metrics используют один shaped layout и owner thread.
Другой surface или foreign thread использовать layout не может. Прокрутка и
рисование clipped к padded editor bounds.

Без explicit catalog RichTextKit выбирает реальные system font styles/fallback.
Для fixed fonts приложение может зарегистрировать normal/bold/italic faces:

```csharp
var fonts = SkiaFontCatalog.LoadFaces("UI", [
    new SkiaFontFace("UI", "Fonts/Regular.ttf"),
    new SkiaFontFace("UI", "Fonts/Bold.ttf", Weight: 700),
    new SkiaFontFace("UI", "Fonts/Italic.ttf", Italic: true),
    new SkiaFontFace("UI", "Fonts/BoldItalic.ttf", Weight: 700, Italic: true)
]);
// options.UseSkiaOpenGL(fonts), UseSkiaRaster(fonts), UseSkiaVulkan(fonts).
```

Каждая family требует normal400 face. Missing exact variant использует normal face;
synthetic bold/italic поверх Regular не обещаются. Existing SkiaFontCatalog.Load
сохраняет прежний API. Files/license доставляет приложение; catalog хранит snapshots.

При explicit catalog missing glyph рисуется как U+FFFD с сохранением code-point
mapping; font должен содержать replacement glyph. Системный glyph fallback для
такого rich run не используется. Emoji appearance зависит от выбранных fonts;
grapheme editing не означает поддержку каждого emoji glyph/sequence.

RichTextKit имеет собственные process-wide shaper caches/pools. Adapter повторно
использует face identity (по bytes для catalog), не создавая retained shaper на
каждое нажатие или повторную session. Layout cleanup не объявляется очисткой
всех global third-party caches; long native-memory trend не является результатом
компонентных smoke tests. Process-wide FontMapper/CharacterMatcher не подменяются.

IME/composition/platform accessibility, RTF/HTML/Markdown import/export, tables,
links/lists, spellcheck и full beta editor не входят в этот control. Их
расширение относится к PA7/реальным consumer requirements; исходные UI alpha
K1–K9 и внешний K7 не отменяются.

## Проверки и источники

[Отдельный план дальнейших улучшений](RICH_TEXT_EDITOR_PLAN.md) сохранён2026-10-17;
работа отложена до развития других функций UI-модуля. Описанные там новые API
и возможности запланированы; текущий реализованный контракт описан выше.

Компонентные regressions — RichTextEditorTests в существующей UI xUnit suite.
Датированные commands/counts/native smoke/package evidence находятся в
[HISTORY](HISTORY.md#richtexteditor--2026-10-17), текущая сводка — [STATUS](STATUS.md).

API сверено с установленным0.6.170 и [upstream commit6362fcd](https://github.com/toptensoftware/RichTextKit/tree/6362fcdc33ea43b359ada2da946bf025a6d14630).
[TextBlock reference](https://www.toptensoftware.com/richtextkit/ref/Topten.RichTextKit.TextBlock)
описывает layout/paint/hit-testing; фактические signatures проверены по local package.

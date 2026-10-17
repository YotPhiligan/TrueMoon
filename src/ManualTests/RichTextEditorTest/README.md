# RichTextEditor demo

Из src, Windows x64/.NET10:

```powershell
dotnet run --project ManualTests/RichTextEditorTest/RichTextEditorTest.csproj -- opengl
# Или vulkan. Raster сохраняет PNG и завершается.
```

Одна View содержит RichTextEditor и независимые toolbar Buttons/option CheckBoxes.
Ctrl+B/I/U, Ctrl+Z/Y, selection, clipboard, multiline input, word wrap/read-only.
Форматирование selection сохраняется при переходе focus на toolbar.

Воспроизводимый native smoke на Raster/OpenGL/Vulkan, Debug build и опубликованном
framework-dependent Release, без RID:

```powershell
pwsh -NoProfile -File ManualTests/RichTextEditorTest/RunSmoke.ps1
```

Runner создаёт свежий TestResults/RichTextEditor/smoke-*;6owned processes выполняют
format/edit/undo/redo/read-only/wrap/scroll и проверяют cleanup. Каждый runtime
ограничен50s. Сохраняются commands/exit codes,6JSON reports, initial/final raster
PNG и summary.json; предыдущие outputs не перезаписываются. Native input вручную
и long resource trends этим scripted smoke не подтверждаются.
Окно/K7/physical DPI/real loss/VRAM имеют прежние ограничения.

[Control API/options/fonts/binding](../../docs/alloy/RICH_TEXT_EDITOR.md).

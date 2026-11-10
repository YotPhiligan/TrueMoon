# Разделение панелей: SplitView v1

`TrueMoon.Argentis.SplitView` размещает два retained `ContentControl` и focusable
divider. `Horizontal` означает слева/справа, `Vertical` — сверху/снизу. Вложенные
SplitView дают navigation/workspace/inspector/timeline. Задача: [UIA3e](PLAN.md#uia3e-split-view),
[заявка Studio](../requests/alloy/20261107-studio-resizable-panes-5d593644/Request.md).

```csharp
var split = new SplitView(navigation, workspace)
{
    Orientation = SplitOrientation.Horizontal,
    FirstPaneSize = 220
};
split.FirstPane.MinWidth = 140;
split.FirstPane.MaxWidth = 400;
split.SecondPane.MinWidth = 240;
split.Divider.AccessibleName = "Ширина навигации";
```

Content должны быть разными живыми, не принадлежащими родителю или UiSession.
SplitView владеет hosts, divider и оставшимся content; window/parent владеет
SplitView. `FirstPane.SetContent` / `SecondPane.SetContent` передают снятое
поддерево вызывающему без Dispose. Поворот, resize, ввод и смена темы не
пересоздают hosts или детей. Все операции — на UI owner thread.

`FirstPaneSize` — preferred **logical pixels**, не доля или framebuffer size.
`NaN` (default) означает половину пространства без divider. Bind/BindTwoWay
используют `FirstPaneSizeProperty`; файл настроек и момент сохранения принадлежат
consumer. Во время drag property меняется на каждом move; это не редактирование
документа. `ActualFirstPaneSize` доступен после layout; clamp не перезаписывает
preference. При возврате места восстанавливается исходный preferred размер.

Axis `MinWidth/MaxWidth` либо `MinHeight/MaxHeight` задаются на **pane hosts**.
В feasible диапазоне выполняются оба ограничения. При дефиците места панели
сжимаются пропорционально minima; при нуле обе нулевые. Divider занимает до
`DividerThickness` (default8); всё остаётся в parent slot. Если сумма конечных
maxima меньше места, остаток остаётся после второй панели. Content дополнительно
подчиняется своим обычным Element constraints.

Первичная кнопка на divider передаёт ему focus/capture. Release, также за
границами окна, завершает drag. Escape, focus/capture loss, скрытие/disable предка,
detach/dispose, смена orientation и изменение host geometry/effective constraints
отменяют gesture с возвратом preference. Внешний `FirstPaneSize` прерывает drag
и сохраняет новое значение. Capture освобождается до rollback callbacks.
Настоящая доставка OS release проверяется отдельно от routed input.

Tab/Shift+Tab достигают divider. Axis arrows меняют размер на `KeyboardStep`
(default8), Shift умножает шаг на10; Home/End выбирают feasible границы. Wheel,
orthogonal arrows и Control shortcuts не меняют размер. AccessibleName — metadata;
системный accessibility provider этой функцией не добавлен.

Divider использует Theme.Surface и Border/BorderHover/Focus/Disabled с local/style/
binding переопределениями Background/Foreground. В прикладном View оформление
задаётся через `IViewStyle<TView>` с явно выбранными частями. Нет таймеров, idle
animation, файлового IO или своих DI сервисов. Collapse/restore, docking,
persisted ratios и cursor shape не входят в v1.

`ManualTests/AlloyTest --split-view` — demo; `--split-view-smoke` — bounded probe,
дополнительно `--vulkan --validation`. Используются Cobalt, `IFactory<TView>` и
отдельные Data/View/Style. Studio интегрирует контрол у своего владельца;
соседний репозиторий этим change не меняется. Фактические проверки:
[STATUS](STATUS.md), [HISTORY](HISTORY.md#split-view-2026-11-09).

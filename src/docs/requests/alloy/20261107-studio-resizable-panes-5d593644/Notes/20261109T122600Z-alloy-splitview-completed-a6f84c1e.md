# Передача Studio: SplitView v1 готов во фреймворке

2026-11-09, owner `codex-alloy-split-view`, заявка `STUDIO-20261107-5d593644`.
Предыдущая consumer заметка: [компоновка и меню](20261107T123741Z-studio-menu-consumer-5a216ec7.md).
Решение и фактический scope: [Decision](../Decision.md).

Готовы `TrueMoon.Argentis.SplitView` и `SplitOrientation`, public guide:
[SPLIT_VIEW.md](../../../../alloy/SPLIT_VIEW.md). Horizontal = left/right,
Vertical = top/bottom; вложение даёт navigation/workspace/inspector/timeline.
Ограничения задаются на FirstPane/SecondPane hosts. FirstPaneSize — preferred
logical pixels (NaN = half), ActualFirstPaneSize доступен после layout. Bindable
FirstPaneSizeProperty позволяет Studio сохранить UI preference отдельно от
project/document. При drag изменения идут на каждом move; cancel возвращает
исходное значение. Сам контрол файлов не пишет.

Children/draft/selection/scroll сохраняются при resize/theme. Capture/Esc/focus/
hide/disable/detach/dispose имеют terminal policy; есть Tab/arrows/Shift/Home/End
и доступное имя Divider.AccessibleName. Styles выбирают Divider.Background/
Foreground и pane hosts через typed View parts. Открытый popup должен сохранять
свой существующий input priority; фактический Studio popup/capture/layout scenario
проверяется его владельцем при интеграции. Studio.project.json не изменялся.

Source demo: AlloyTest --split-view; bounded probe: --split-view-smoke, optional
--vulkan --validation. Local package proof `0.2.5.2-splitview.20261109.1` не означает
публикацию обычного `0.2.5.2-alpha`. [Проверки и ограничения](../../../../alloy/HISTORY.md#split-view-2026-11-09).
Actual consumer migration/physical acceptance не объявлены выполненными.

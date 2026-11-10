# ED5b: перемещение объектов в viewport v1

Решения и реализация v1, 2026-11-09. [Проверки и ограничения](HISTORY.md#ed5b-translation-2026-11-09).
Единственная очередь/acceptance: [EDITOR_PLAN](EDITOR_PLAN.md#ed5b-translation).
Основа: actual-presentation receipt [ED5a](EDITOR_CAMERA_SELECTION.md).

## Первый профиль

Один выбранный видимый cube из `editor.opaque-cubes/1`, точный built-in Transform v1.
World XYZ и world planes XY/XZ/YZ. Local означает **записываемую позицию относительно
родителя**: world delta преобразуется inverse parent linear matrix без декомпозиции
rotation/scale/shear. Local-oriented handles, snapping, rotation/scale, multiple
selection и live World editing следуют отдельными задачами.

Handles строятся из матриц/геометрии последнего actual-presented receipt. Осевые
стрелки имеют примерно72logical pixels; почти вырожденные направления/плоскости
не принимают ввод. Сначала hit оси, затем plane region. UI chrome, popup, document
overlay и чужой capture имеют прежний приоритет. Letterbox/clip/outdated receipt
не начинают жест. Программное включение инструмента не выбирает другой объект.

Admission требует active UI binding, current opening/content/camera/surface/layout,
совпадения selection intent и доступного document editing. Любой inspector draft,
pending reset, conflict или numeric gesture запрещает начало/продолжение translation;
поля и черновики не очищаются. Во время drag камера и selection не меняются.

## Временный preview и commit

На каждом move меняется только caller-owned immutable gesture data. UI рисует
контуры bounds выбранного объекта и его видимых descendants в **будущем** world
положении с Theme.Modified; исходный solid runtime frame остаётся видимым до commit.
Этот outline preview явно отличается от полученного кадра, не получает фиктивный
FrameId/receipt и не заменяет picking source. Это v1 preview policy, не live render
mutation/новый протокол. GPU resources не создаются на каждом движении.

Projection/ray вычисляются по frozen actual view. Axis — closest ray/axis parameter,
plane — bounded ray/plane intersection. На release вне image используются finite
logical координаты, но исходные clip/layout/context fences остаются обязательными.
Invalid/parallel/overflow candidate сохраняет последний допустимый preview; invalid
release не коммитит его неявно. Возврат точно в начальную точку — exact no-op.

Первичная кнопка на handle фокусирует viewport и захватывает pointer. При release
capture и gesture state освобождаются **до** callbacks. Затем admission проверяется
повторно. Только изменённые x/y/z записываются одним `ComponentEditBatch` через
`EditorDocumentCommands`; rotation/scale, inheritance источники остальных полей,
имена/selection и чужие component overrides сохраняются. No-op не создаёт history
и не очищает Redo. Обычный committed preview synchronizer обновляет solid после
успеха. Apply failure даёт существующую inspector diagnostic, не late retry.

Esc/focus/capture loss, popup, hide/detach/dispose, resize/DPI/layout/clip, смена
source/opening/selection/camera/surface, Stop/Close/project transition отменяют
outline без изменения документа/history. Новая camera revision не откатывается.
Same-ID selection intent также отменяет старый gesture. Новые frame tickets с тем
же semantic view сами по себе жест не отменяют. Исключения не удерживают capture.

## Владение и проверки

Существующий parent-owned `EditorViewportController` получает зависимости через
App/IFactory и владеет gesture/подписками. Geometry/target — detached CPU values,
не сервисы, не удерживают controls/native handles. Inspector model/commands,
workspace и preview заимствуются. View рисует overlay на owner thread, Theme
предоставляет AxisX/Y/Z, Focus/Modified; model не зависит от controls/renderers.

Проверки: CPU axis/plane/inverse-parent/finite/degenerate/no-op; real document
history/one commit/Undo/Redo/inheritance; routed input/cancel/stale/popup/draft/
retained identity; source/published native outline+solid-after-commit и independent
package API. Physical input/multi-monitor/release gates отмечаются отдельно.

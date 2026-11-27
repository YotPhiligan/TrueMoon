# TrueMoon: общий статус

**Kton / Aether, 27.11.2026.** Opt-in автошаг `1.2611.27.11-alpha` проверен
через ECS на исходниках и в независимом потребителе через закрытый NuGet feed.
Pause/Step/Replace проверен отдельно на исходниках; схема v1 не изменилась.
[Результаты и границы](physics/STATUS.md#phys6-aether-package-auto-step-2026-11-27).

**Aether, 27.11.2026.** Локальная поставка `1.2611.27.3-alpha` продолжает
Player-сценарий с точного тика. Tales закрыла М6: после Ctrl+C итоговый слот
совпал с непрерывным прогоном. [Приёмка](engine/HISTORY.md#player-scenario-disk-resume-2026-11-27).

**Alloy / Argentis, 25.11.2026.** В `main` на `c595660` (`1.2611.25.17-alpha`)
удалён `Hosting.Compatibility`, генератор поставляется с Argentis, а конкретные
дочерние View внедряются через их фабрики. Адресная проверка исходников прошла.
[Результаты и границы](alloy/HISTORY.md#uia1-view-main-2026-11-25).

**UIA1f.11, 24.11.2026.** Editor и демонстрационные View переведены на
[новый контракт UI](alloy/PLAN.md#uia1f11-contextual-content). Локальная
регрессия `1.2611.24.11-alpha` повторно прошла в Debug и Release. В каждой
конфигурации прошёл 2 961 тест Alloy, Hosting и Editor. ENG9b подтвердил
сохранение состояния и геометрии в девяти контрольных точках Editor.
[Повторная проверка](alloy/HISTORY.md#uia1f11-recheck-2026-11-24).

**UIA1h.5, `1.2611.25.7-alpha`.** Рисование пользовательского контрола теперь
регистрируется снаружи в Alloy; `Element.Presentation` удалено. Обновлять
Argentis, Alloy и приложения с собственными контролами вместе.
[Контракт и миграция](alloy/PLAN.md#uia1h5-external-presenters).

**UI-архитектура, 25.11.2026.** В `1.2611.25.6-alpha` общий обход Alloy
заменил `DrawCore`, Editor и Player передают квитанции рисования отдельно
от показа хостом. Для потребителей Argentis это breaking change:
нужны новые импорты и пересборка. [Проверки и границы](alloy/STATUS.md#uia1-architecture),
[карта типов](alloy/PLAN.md#uia1i-argentis-structure).

**Ревизия 2026-11-21.** Основание `main` — `8a4e22c` на версии
`1.2611.21.9-alpha`. В этой версии проверен первый оконный сценарий игры.
NOM6.1 принят раньше, в чистой копии дерева `27129fe`.
[Проверки исправления](nom/HISTORY.md#nom6-1-verification-2026-11-21).

**Дополнение 23.11.2026.** Пилот [UIA1f](alloy/PLAN.md#uia1f-minimal-view-authoring)
завершён на обозревателе и консоли диагностики. Итоговая локальная проверка
версии `1.2611.23.11-alpha` приведена в
[профиле UIA1f](engine/HISTORY.md#uia1f-delivery-2026-11-23).

**Сценарные проверки 24.11.2026.** [ENG9b.1](engine/PLAN.md#eng9b1-player-scenario-capture)
завершён. Пакетный CLI сверяет именованные числовые наблюдения на контрольных
тиках и сохраняет соответствующие PNG. [UIQA.1](alloy/PLAN.md#uiqa-agent-checks)
добавил семантический снимок обозревателя и инспектора. В UIQA.2 на обоих
экранах проверена маршрутизация ввода по ID. [ENG9b.2](engine/PLAN.md#eng9b2-ui-scenarios)
проверил полный сценарий Editor от создания объекта до повторного открытия
сохранённой сцены. [Сравнение прогонов](engine/SCENARIO_TESTING.md#editor-scenario-comparison)
в ENG9b.3 сохраняет различия состояния, геометрии и PNG. Editor воспроизвёл
записанные действия в новой сессии. В
[ENG9b.4](engine/SCENARIO_TESTING.md#headless-player-scenario) оконный и
headless Player совпали по вводу и игровым наблюдениям на контрольных тиках.

## Состояние модулей

| Модуль | Что готово в проверенном профиле | Продолжение |
| --- | --- | --- |
| [Aether / Editor и Player](engine/STATUS.md) | ARCH1g.2a–p — точные сборки статических моделей, каталог и размещение. ED4i — независимые Apply слотов, перенос материалов и проекция камеры с Undo и восстановлением после перезапуска. ARCH1f.5 — импорт PNG, переимпорт, Build и Replace во время Playing. Миграция View прошла пакетную приёмку обоих хостов [Editor](engine/HISTORY.md#editor-package-view-acceptance-2026-11-26) и независимого [Player](engine/HISTORY.md#player-package-view-acceptance-2026-11-26). | [Расширение ED4i](engine/EDITOR_PLAN.md#ed4i-next) — отдельная очередь. |
| [Kton](physics/STATUS.md) | PHYS0–6 и PHYS8.1–3 проверены. В PHYS8.4 прошла автоматическая приёмка визуального consumer | [10 000 тел и воркеры](physics/PLAN.md#phys8). Ручной Win32 input/close открыт |
| [Kul](audio/STATUS.md) | AUD1 и ARCH1h.2a проверены на Windows x64. Принятый аудио backend и ECS bridge покрыты своими профилями | [AUD2–3](audio/PLAN.md#current-audio-queue) |
| [Nom](nom/STATUS.md) | NOM6.1 в `main`. Прошли чистая сборка, по 874 теста Nom в Debug/Release, backup/restore и пакетные проверки | [Приёмка NOM6.1](nom/PLAN.md#nom6-1-clean-checkout). VM-приёмка отложена |
| [Alloy / Argentis](alloy/STATUS.md#current-ui-baseline) | Пакетная композиция проверена на прежнем наборе. В `1.2611.26.17-alpha` прокрутка и наблюдение resize прошли адресные тесты из исходников; resize и ввод консоли проверены в обоих нативных хостах Editor на свежих пакетах. | [Очередь UI](alloy/PLAN.md#current-ui-queue): физический переход DPI на двух мониторах и итоговая alpha-приёмка — отдельные проверки. |
| [Core](core/STATUS.md), [Cobalt](cobalt/STATUS.md), [Enerit](enerit/STATUS.md), [Mithril](mithril/STATUS.md) | CORE7 и CORE8 завершены в своих профилях. Последняя общая launch-регрессия — ENG7b.3 от 16.11.2026. ENE6e прошёл отдельную приёмку для consumer со строгими XML docs | [Core](core/PLAN.md), [Cobalt](cobalt/PLAN.md), [Enerit](enerit/PLAN.md), [Mithril](mithril/PLAN.md) |

<a id="общие-проверки--датированные-сведения"></a>
## Датированные проверки

- **Alloy, 21.11, `1.2611.21.7-alpha`.** В чистой копии проверены четыре
  графических профиля. Vulkan Validation 0/0. В Debug и Release прошли
  1 225 Alloy и 171 Alloy.Hosting. По 1 522 теста Editor прошли после
  добавления локальных исходников Backup.
  [Проверки поставки](alloy/HISTORY.md#alloy-main-glass-icons-2026-11-21).
- **ARCH1f.5, 20.11, `1.2611.20.2-alpha`.** Шесть Vulkan-профилей, включая
  свежие пакеты, и по 1 513 тестов Editor в Debug и Release.
  [Приёмка](engine/HISTORY.md#arch1f5-editor-playing-texture-workflow-2026-11-20).
- **ENG7b.3, 16.11, `1.2611.16.1-alpha`.** Общая регрессия и приёмка запуска
  Editor/Player зафиксированы отдельно. [Матрица и границы](engine/LAUNCH_ACCEPTANCE.md).
- [Датированные сводки до ревизии](HISTORY.md#common-status-before-review-2026-11-20).

## Границы и ближайший шаг

[Видимость и слои MeshRenderer](engine/EDITOR_PLAN.md#ed4i-mesh-visibility)
проверены в `1.2611.21.9-alpha`. Следующие варианты собраны в
[очереди ED4i](engine/EDITOR_PLAN.md#ed4i-next).

[Авторские Texture и Mesh](engine/EDITOR_PLAN.md#ed4i-authoring-assets)
входят в Build и committed Edit Preview. Управляемые и нативные результаты
ведутся раздельно в [статусе ED4i](engine/STATUS.md#ed4i-authoring-assets-status).

Результаты относятся к проверенным Windows x64 профилям. K7, физический ввод
и DPI, device loss, другие OS/RID, performance/soak и итоговые
[критерии выпуска](PLAN.md#общие-критерии-выпуска) остаются открыты.
Причина исторического drain timeout ведётся в [ARCH2e.4b](engine/PLAN.md#arch2e4b-drain-diagnostics).

[Приёмка Player с импортированной PNG](engine/HISTORY.md#arch1f5-player-imported-png-2026-11-20) прошла в шести профилях.
[Первый оконный срез TrueMoon.Tales](engine/PLAN.md#eng7b4-tales-consumer)
прошёл из пакетов `1.2611.21.9-alpha`: World, игровое действие, кадры и закрытие.
[Ортографический маршрут TrueMoon.Tales](engine/PLAN.md#arch1f7-orthographic-camera)
проверен локально из пакетов `1.2611.21.10-alpha`: Vulkan показывает
четыре ориентира в прежнем масштабе после resize, ресурсные счётчики закрыты.
Приоритеты и независимые ветки собраны в [общей очереди](PLAN.md#current-roadmap).

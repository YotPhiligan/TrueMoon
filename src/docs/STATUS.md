# TrueMoon: общий статус

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
тиках и сохраняет соответствующие PNG. Семантические снимки и ввод для Editor
ведутся в [ENG9b.2](engine/PLAN.md#eng9b2-ui-scenarios) и [Alloy UIQA](alloy/PLAN.md#uiqa-agent-checks).

## Состояние модулей

| Модуль | Что готово в проверенном профиле | Продолжение |
| --- | --- | --- |
| [Aether / Editor](engine/STATUS.md) | ARCH1g.2a–p — точные сборки статических моделей, каталог и размещение. ED4i — независимые Apply слотов, перенос материалов и проекция камеры с Undo и восстановлением после перезапуска. ARCH1f.5 — импорт PNG, переимпорт, Build и Replace из Editor во время Playing. Player показал A → B → A из готовых артефактов | [Расширение ED4i](engine/EDITOR_PLAN.md#ed4i-next) |
| [Kton](physics/STATUS.md) | PHYS0–6 и PHYS8.1–3 проверены. В PHYS8.4 прошла автоматическая приёмка визуального consumer | [10 000 тел и воркеры](physics/PLAN.md#phys8). Ручной Win32 input/close открыт |
| [Kul](audio/STATUS.md) | AUD1 и ARCH1h.2a проверены на Windows x64. Принятый аудио backend и ECS bridge покрыты своими профилями | [AUD2–3](audio/PLAN.md#current-audio-queue) |
| [Nom](nom/STATUS.md) | NOM6.1 в `main`. Прошли чистая сборка, по 874 теста Nom в Debug/Release, backup/restore и пакетные проверки | [Приёмка NOM6.1](nom/PLAN.md#nom6-1-clean-checkout). VM-приёмка отложена |
| [Alloy](alloy/STATUS.md) | В `main` стекло, цветная подложка и 32 векторных знака. Raster, OpenGL и Vulkan проверены. Есть попиксельные расхождения | [PA8c.1a](alloy/PLAN.md#pa8c1a-positioning-rollout) запланирован. Ввод, DPI, K7/K8 и матрица выпуска открыты |
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

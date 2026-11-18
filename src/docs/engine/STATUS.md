# Aether / Editor: состояние и точка продолжения

**2026-11-18 — ARCH1g.2d.** Горячий Replace опубликованной сборки для полной
сцены в Editor. Version **1.2611.18.4-alpha**.
[Контракт](MODEL_BUILD_DELIVERY.md#arch1g2d-world-replace),
[план](PLAN.md#arch1g2d-full-scene-replace), [проверки](HISTORY.md#arch1g2d-2026-11-18).

В одном Editor успешный Replace сменил геометрию с 25 096 на 50 422 красных
пикселя. Повреждённая сборка отклонена с прежним child и поверхностью.
При отмене после загрузки сцены кандидата восстановлена предыдущая сборка
с прежней маской, checkpoint и Playing. Затем повторный Replace вернул старую
геометрию. Хэш сцены и ревизия открытого документа сохранены.

Пройдены source Debug и published Release на Cobalt и Microsoft DI в Editor.
Все 20 runtime child завершены. Vulkan validation и счётчики ресурсов
проверены после освобождения. Aether 294/294, Editor 367/367 и Player 17/17
в Debug и Release без пропусков. Далее интерактивный выбор артефакта и приёмка
Editor на свежих пакетах. [Приёмка Player g.2b](HISTORY.md#arch1g2b-2026-11-18).

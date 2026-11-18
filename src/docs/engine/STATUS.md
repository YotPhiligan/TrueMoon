# Aether / Editor: состояние и точка продолжения

**2026-11-18 — ARCH1g.2c.** Смена опубликованной сборки для открытой сцены
в одном Editor. Version **1.2611.18.3-alpha**.
[Контракт](MODEL_BUILD_DELIVERY.md#arch1g2c-editor-builds),
[план](PLAN.md#arch1g2c-model-editor-builds), [проверки](HISTORY.md#arch1g2c-2026-11-18).

Обычный Editor открывает полную `.aetherscene` с камерой и двумя экземплярами
одного импортированного меша. Команда координатора принимает точный артефакт.
Старая сборка → Stop → новая → Stop → старая показывает 25 096 → 50 422 →
25 096 красных пикселей. Первая и повторная маски совпали. После каждого Stop
освобождены runtime child и viewport. Хэш файла и открытая ревизия сохранены.

Пройдены source Debug и published Release на Cobalt и Microsoft DI в Editor.
12 нативных запусков с Vulkan validation без ошибок и предупреждений. Целевые
наборы Aether 176/176 и Editor 367/367 в Debug и Release без пропусков. Далее
интерактивный выбор артефакта, reload/Replace полной сцены и приёмка Editor на
свежих пакетах. [Приёмка Player g.2b](HISTORY.md#arch1g2b-2026-11-18).

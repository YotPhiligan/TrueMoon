# Aether / Editor: состояние и точка продолжения

**2026-11-18 — ARCH1g.2b.** Импортированная модель размещена в сцене и
проверена через нативный Player. Version **1.2611.18.2-alpha**.
[Контракт](MODEL_BUILD_DELIVERY.md#arch1g2b-scene-gpu),
[план](PLAN.md#arch1g2b-model-scene-gpu), [проверки](HISTORY.md#arch1g2b-2026-11-18).

Одна `.aetherscene` содержит камеру и два экземпляра одного импортированного
меша. Игровая сессия получает манифест точного BuildId. При неизменных ID, сцене
и игровой DLL число красных пикселей выросло с 8 753 до 17 496. Повторно открытая
старая сборка дала прежнюю маску после реимпорта.

Пройдены source Debug и fresh NuGet published Release на Cobalt и Microsoft DI,
19 локальных пакетов. 12 GPU-запусков с Vulkan validation без ошибок и
предупреждений. Целевой набор Aether 1155/1155 и полный Player 17/17 в Debug и
Release, без пропусков. Далее матрица выбора сборки в Editor и проверка
владения ресурсами при reload/replace. [Приёмка g.2a](HISTORY.md#arch1g2a-2026-11-18).

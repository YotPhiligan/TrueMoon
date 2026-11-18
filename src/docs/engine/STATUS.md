# Aether / Editor: состояние и точка продолжения

**2026-11-18 — ARCH1g.2a:** одна модель проходит Nom → Build → Runtime.
Version **1.2611.18.1-alpha**. [Контракт](MODEL_BUILD_DELIVERY.md),
[план](PLAN.md#arch1g2a-model-delivery), [проверки](HISTORY.md#arch1g2a-2026-11-18).

Optional TrueMoon.Aether.Nom публикует ресурсы модели одной пачкой и захватывает
точный снимок. Build с schema 3 включает render payload в BuildId. Runtime проверяет
байты при открытии и выдаёт CPU-каталогу манифест выбранной сборки. Старая сборка
даёт X=1, новая X=2 после реимпорта при одинаковой игровой DLL. ID меша, слота,
материала, привязки и ledger-экземпляров сохранены. Старый снимок снова даёт старый
BuildId. Оба Runtime-процесса работают с артефактами после закрытия и перемещения
Nom и исходников. Повреждённая копия отклонена до загрузки игры.

Проверены source Debug и fresh NuGet published Release на обоих DI, 13 локальных
пакетов. Aether 3698/3698 и Player 17/17 в Debug/Release, failed/skipped 0.
Следующий срез g.2 — привязка экземпляров к сцене и проверка native pixels.
[Предыдущий статус](HISTORY.md#engine-status-before-g2a-2026-11-18).

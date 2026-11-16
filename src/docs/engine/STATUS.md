# Aether / Editor: состояние и точка продолжения

**2026-11-16 — ENG7b.3 завершён в Windows x64 launch/delivery scope.**
[Единственная задача](PLAN.md#eng7b3-launch-acceptance),
[матрица/миграция](LAUNCH_ACCEPTANCE.md), [история/команды](HISTORY.md#eng7b3-2026-11-16),
[manifest/SHA](ENG7B3_BASELINE.json). Версия — `1.2611.16.1-alpha`.

Исправлен второй mesh Play после Stop: GPU generation начинается с1 для новой
runtime connection, resize продолжает текущую generation; stale child/tickets
отвергаются. Combined child использует standalone Runtime/frozen bootstrap.
Новый регрессионный тест и реальные native повторные циклы прошли.

Source Debug/published Release/fresh NuGet:34profiles на Cobalt/MS DI,54game cycles
с crash/recovery,36identity rejections,60CLI cases,12окон реальных примеров.
Player-only/combined Player отдельно прошли4SDK-free runs. Документ/история/selection
сохранены; Editor не загружает game, нормальный child validation0/0, owners/reap
проверены.19точных package archives,7closed-feed graphs, native/shared/font payloads
и1033source hashes согласованы; это не новая полная поставка всех27packages.

Полная свежая регрессия:16suites по9111passed каждый Debug/Release,0failed/0skipped;
Aether3534/Editor1507/Player17/Abstractions26 и bridges68/103 входят в totals.
Non-incremental solution builds D/R0errors. SDK/TFM/dependencies/native ABI прежние;
native Skia4.156.1 повторно не пересобиралась. [Прежний STATUS](HISTORY.md#engine-status-before-eng7b3-2026-11-16).

ENG7b.3 больше не следующая задача. Далее выбрать functional scope: PHYS6 controller,
production import g.1→g.2 либо ED4i/дальнейшие ED5 инструменты. AUD2–3, physical/
release/device-loss/other OS и историческая drain cause самостоятельны.
Requests/Claims/Decisions и код потребителей не менялись.

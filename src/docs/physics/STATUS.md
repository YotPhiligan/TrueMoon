# TrueMoon.Kton: состояние и следующий шаг

**2026-11-15 — DEP1: BEPU остаётся актуальным2.5.0-beta.33, PHYS5 regression прошёл.**
NuGet показывает stable2.4.0 и latest existing beta2.5.0-beta.33; downgrade не выполнялся.
Exact BepuPhysics/BepuUtilities ranges, provider/C# API/source и ownership сохранены.
Полный Aether.Kton suite68passed каждый Debug/Release,0failed/0skipped; actual BEPU
bridge/scene lifecycle входят в этот suite. Самостоятельный309-case provider runner
в dependency refresh отдельно не повторялся.

[Контракт PHYS5](ECS_BRIDGE.md), [его детальная задача](PLAN.md#phys5),
[DEP1 и новая межмодульная приёмка](../core/HISTORY.md#dep1-2026-11-15),
[manifest/SHA](../core/DEPENDENCY_REFRESH_20261115.json).
Прежние full provider и independent PHYS5 delivery results сохраняют свои даты:
[предыдущий STATUS](HISTORY.md#physics-status-before-dep1-2026-11-15).

Следующий физический feature — PHYS6 controller, PHYS8 по отдельному scope;
PHYS7 необязателен. Общая очередь остаётся у engine, Requests/Claims не менялись.

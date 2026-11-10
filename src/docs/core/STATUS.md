# Core: состояние и точка продолжения

**2026-11-11 — CORE7.0–7.6 завершены в configuration scope.**
[Рабочий guide](CONFIGURATION_GUIDE.md), [139keys/17schemas](CONFIGURATION_KEYS.md),
[единый runner](../../ManualTests/ConfigurationDelivery/README.md),
[команды/результаты/границы](HISTORY.md#core76-2026-11-10).

## Реализация и проверки

| Область | Текущий запуск CORE7.6 |
| --- | --- |
| Solution | Debug0errors/256warnings, Release0errors/190warnings, --no-restore |
|7full suites | Core661, Mithril155, Enerit354, Nom874, Hosting171, Editor1496, Aether3234 каждый D/R |
| Test total |6945/6945 каждый D/R, Failed:0, Skipped:0; не все test projects solution |
| Delivery |16packages version0.2.5.2-core76.20261110234501;5programs×source/published/fresh;230checks/profile |
| Owned children |4/profile,12total; Cobalt/MS DI, actual bootstrap/identity/control, graceful stop/reap; без renderer/game/native runtime |
| Metadata/catalog |16projects/16archives verified; public metadata17schemas/139keys; shared/module README/license/icon/analyzers/native payload hashes |
| API/examples |8managed surfaces added0/removed0;4API doc blocks compiled, guide example executed во всех3forms |
| Preservation |1673/1673baseline C#/csproj сохранены; runtime API/dependencies/TF/source version0.2.5.2-alpha не менялись |

Reader/profile/options, atomic reload/notifications/explicit writer, Core/Mithril
и модульные adapters готовы по guide. Старые callbacks и protected launch contracts
сохранены. [Предыдущий STATUS целиком](HISTORY.md#status-before-core76-2026-11-10).

## Ограничения

Aether full passing получен последовательно с documented test-only props copy:
obsolete dotnet-xunit CLI reference исключён, CPM ограничен workspace descendants.
Root props не менялся, ordinary nested restore без override не объявлен исправленным.
Старые diagnostics timeout причины не закрываются этим passing run.

Стандартные module fields StartupOnly. Configuration proof не включает новый
native/GPU/hardware/device-loss/VM/release acceptance. Полные generator/UI suites
не повторялись. Межпроцессная запись, power-loss durability, other OS/RID/AOT
и hard termination произвольного callback/driver не обещаны.

## Следующий конкретный шаг

Применять общий слой при целевых изменениях consumers; новые providers/options/
lifecycle возможности согласовывать отдельными задачами. Самостоятельные CORE6
routing/metrics/quota, Mithril MIT6, engine f.4 и wider release очередь сохранены.

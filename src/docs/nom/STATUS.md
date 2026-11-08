# TrueMoon.Nom: состояние модуля хранения

**Предкоммитная сверка2026-11-08:** повторены862/862 Nom tests каждый Debug/Release,0failed/0skipped. Код не менялся; [scope/команды](HISTORY.md#nom-commit-2026-11-08). Остальная таблица ниже — предыдущие проверки source organization.

**2026-11-08 — исходники организованы по функциональным каталогам.**
[Карта namespace и совместимость](STRUCTURE.md#nom-source-layout-2026-11-08):
Catalog, Resources, Retention, Backup, Hosting, Storage и Storage/Format.
60файлов перенесены; NomLifecycle выделен в Hosting, итого66C# файлов.
UseNom/общий store facade/open arguments остаются в корне. Public subsystem types
получили новые namespace; clients требуют обновления imports и пересборки.
Implementations/tests/lifetime/format/dependencies сохранены; [история и команды](HISTORY.md#nom-structure-2026-11-08).

| Проверка текущего запуска | Результат |
| --- | --- |
| Source layout/body audit |66namespace/path pairs,65прежних implementations+extraction и64consumer C#/template bodies без using/namespace/whitespace совпадают; partial groups согласованы |
| Full Nom xUnit Debug/Release |862/862 каждый,0failed/0skipped; новых test cases нет |
| Functional regression |120groups/profile source Debug/published Release, actual killed/reaped children |
| Public source consumers |App34/Buffer28/Raw31checks каждый Debug/published Release |
| Fresh NuGet delivery |4packages/4consumers/8fresh success graphs; App34/Buffer28/Raw31/Tool211checks каждый portable Debug/published Release/win-x64 FDD Release; namespace/internal/managed-T/package guards прошли |
| Durability template |Только compile Debug/published Release из fresh packages; не исполнялся |
| Full solution nonincremental Debug |0errors/1560warnings; Nom/Nom.Tests0warnings |

NOM0–NOM8 реализованы в описанных local scope; byte/raw API работает из Resources,
snapshots/GC — Retention, backup/restore — Backup, host/options — Hosting. Формат и
bytes не зависят от CLR namespace. Следующий consumer bridge — engine ARCH1g.2 после
g.1 importer adapter, по согласованию; engine/UI/release очередь сохраняется.

Actual VM crash verification отложена пользователем; новых VM/disk-full trials0.
Hardware power-off исключён. Прежние UAC disk-full5cases/166assertions и VM protocol
proofs, heavy NOM6 workload/capacity — история, здесь не повторялись. Другие
OS/RID/AOT/trimming/self-contained/external publish не подтверждаются этой задачей.
[Предыдущий STATUS](HISTORY.md#nom-status-before-structure-2026-11-08) сохранён после
точной адаптации source-link targets; остальной текст и датированные результаты сохранены.

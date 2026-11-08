# TrueMoon.Nom: имя, проекты и проверки

> NOM2 завершён2026-11-08: [текущий backend и проверки](CATALOG.md#nom2-catalog-2026-11-08).
> Vendor runners/downloads удалены; команды и результаты этого документа исторические.

> Исторический LiteDB срез. Последующим решением2026-11-07 принята полная замена
> собственной реализацией Nom: [решение](STORAGE_DESIGN.md#nom-own-store-2026-11-07),
> [активная очередь NOM0–NOM7](PLAN.md#nom-own-queue). Результаты ниже относятся
> только к старому backend; vendor fix/upgrade/isolation больше не активная задача.

> Историческая запись до AST2a. Публичное подключение и текущие проверки: [App API](APP_INTEGRATION.md#nom-app-2026-11-07).

<a id="nom-rename-2026-11-07"></a>
## Переименование — 2026-11-07

По решению пользователя модуль хранения называется **TrueMoon.Nom**. В лоре
«Сказок истинной луны» Ном связан с порядком, знаниями, рунами и структурой.
Прежнее рабочее обозначение Assets в исторических записях относится к этому модулю;
concept asset и существующие Aether assets API при этом сохраняются.

Раньше весь код находился в ManualTests/LiteDbPrototype, а runner генерировал
временный console project. Теперь:

| Путь | Назначение |
| --- | --- |
| [TrueMoon.Nom](../../TrueMoon.Nom/TrueMoon.Nom.csproj) | Class library:6implementation files, namespace TrueMoon.Nom; strict catalog/owner, streamed resources/CAS/journal |
| [TrueMoon.Nom.Probe](../../ManualTests/TrueMoon.Nom.Probe/TrueMoon.Nom.Probe.csproj) | Console consumer:4proof/fixture files, namespace TrueMoon.Nom.Probe, ProjectReference на библиотеку |
| [TrueMoon.slnx](../../TrueMoon.slnx) | Оба проекта зарегистрированы: Nom и ManualTests folders |
| [Directory.Packages.props](../../Directory.Packages.props) | LiteDB5.0.21 централизован; новые csproj используют общие TF/TestsTF |
| [docs/nom](PLAN.md) | Единственный текущий план и статус; docs/assets содержит compatibility pointers |

Library internal и IsPackable=false: public App/Cobalt/IFactory registration и
provider/package split остаются AST2. Probe имеет явный InternalsVisibleTo для
исследовательских проверок; включение в solution не объявляет production API готовым.
Предлагаемое имя будущего provider — TrueMoon.Nom.LiteDb, такого проекта пока нет.

Storage code перенесён с изменением namespace; алгоритмы, record schemas,
ast1a/ast1b format/hash domains, filenames CAS и правила ownership не переименованы.
Дополнительная проверка Program.LibraryInfo подтверждает отдельную output-local
сборку TrueMoon.Nom.dll в обоих профилях. Child probes продолжают запускать Probe,
а работа с каталогом идёт через отдельную библиотеку.

## Выполненные проверки

Из `src`, PowerShell7/SDK.NET10, Windows x64:

```powershell
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/MyFreshNomRun
dotnet build TrueMoon.slnx -c Debug --no-incremental '-bl:TestResults/Nom-Solution-{}.binlog'
dotnet sln TrueMoon.slnx list
```

Фактический passing run: TestResults/NomRename-20261107/Attempt2. Source Debug и
framework-dependent published Release —18/18groups каждый,5commands exit0,
3SDK binlogs,15frozen source/project/config hashes, drift0. Все прежние streaming,
atomicity, cancel/commit, corruption, reader/writer drain и4actual killed/reaped
children/profile выполнялись заново после переноса. Это console proofs, не xUnit count.

Runner собирает **реальные** csproj, не копии C# в сгенерированном проекте. Isolated
SDK artifacts path отделяет bin/obj от рабочей копии; feed/cache и temporary exact CPM
исключают unrelated legacy CLI tool. В графе один NuGet package LiteDB/5.0.21.
Прежний прямой research reference System.Buffers4.5.1 не перенесён: netstandard2.0
asset LiteDB не требует его; runtime предоставляет System.Buffers. SHA actual LiteDB.dll
остаётся AE31AC6A93549217B9E8B81497D1EE831658ADE6DF9E1EE20F2838F22DE5E218,
assembly5.0.21.0/runtime10.0.14/x64. Различие dependency graph явно отделено от
исторических AST1a/AST1b frozen records.

Attempt1 завершился после restore на старом graph guard, ожидавшем лишний прямой
System.Buffers. Guard и archive list исправлены под фактический новый graph;
Attempt2 затем прошёл целиком. Общая nonincremental solution Debug сборка также
прошла:0errors/1555existing warnings, delta0; новые проекты warnings0.
`dotnet sln list` подтвердил оба проекта; source/hash, namespace/path, links и
git diff whitespace checks выполнены отдельно.

Raw artifacts ignored: TestResults/NomRename-20261107/Attempt2/verification.json,
Debug.json, Release.json, stdout/stderr/binlogs и solution-debug.log. Эта versioned
запись сохраняет результат и повторяемую команду при отсутствии raw artifacts.
Предыдущие численные allocations/source hashes в AST1A_PROTOTYPE и AST1B_STREAMING
остаются историческими. Перенос не означает migration старых AST1a stores без control.

Full unit suite не запускалась; прежние Aether diagnostic failures не закрыты.
Symlink fixture всё ещё privilege-gated0x80070522. Disk-full/cleanup fault gates,
power-loss, snapshots/GC, public App/package и decoder/GPU integration не проверялись.
Следующий шаг — [AST1b.f](PLAN.md#litedb-ast1); очередь engine f.3→f.4 сохранена.

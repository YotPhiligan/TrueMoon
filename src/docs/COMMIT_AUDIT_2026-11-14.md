# Предкоммитная сверка TrueMoon — 14 ноября 2026

Основание: запрос включить завершённую работу, особенно два новых модуля и docs.
Проверена рабочая копия `main` поверх `73df16229da1db54e1170ac0d56b9d385f91abd7`;
origin/main перед commit/push совпадал с этим HEAD. Эта запись фиксирует проверку
состава и текущие прогоны; факт публикации определяется Git remote после push.

## Что входит

- ARCH3.0–6 и предшествующие завершённые engine срезы: карта, Player/surface fence,
  Abstractions/ECS, Runtime, headless Tooling, Editor.UI/optional Editor.Tooling,
  Mod SDK/manifest/collectible loader, consumers/tests/migration maps/baselines.
- Kton: нейтральный physics API, BEPU provider, public App/configuration/factories,
  ownership, probes, PHYS0–4 планы/статусы/история/результаты.
- Kul: App/DI audio sessions, sound/music, тесты/fixtures/probe, AUD1/rename docs,
  exact MiniAudioEx source lock, lifetime patch, build/install/verify scripts и
  notices. Локальный fork package/feed воспроизводится по build guide; его
  generated nupkg/cache не являются исходниками и не добавляются в Git.
- Согласованные package pins и bundled SkiaSharp native update, examples,
  solution/package wiring, общие инструкции, навигация и связанные docs.

Изменения существовали в общей рабочей копии; при сверке не удалялись и не
отменялись. `TestResults`, `.codex`, bin/obj, локальные caches и downloaded native
sources остаются вне коммита. Candidate inventory не обнаружил private keys/
credential patterns, отсутствующих ProjectReference или иных крупных файлов,
кроме предусмотренной bundled Skia DLL и versioned engine HISTORY.

## Новые проверки

Окружение: Windows x64, SDK11.0.100/net11.0, общий runtime-async build profile.
Evidence root: `TestResults/CommitAudit-20261114-215302` (ignored, дополнительный
raw evidence; команды, scope и outcomes сохранены здесь).

| Область | Команда из src, PowerShell7 | Результат |
| --- | --- | --- |
| Kton App | `ManualTests/KtonApp/Verify.ps1 -EvidenceDirectory TestResults/CommitAudit-20261114-215302/kton-app` | Completed; по192checks source Debug/fresh package Debug/published Release, Cobalt и Microsoft DI |
| Kton provider | `ManualTests/KtonProvider/Verify.ps1 -EvidenceDirectory TestResults/CommitAudit-20261114-215302/kton-provider` | Completed; по309checks source Debug/fresh package Debug/published Release, реальные BEPU resources/queries/contacts и cleanup |
| Kul delivery | `ManualTests/KulModuleProbe/Verify.ps1 -EvidenceDirectory TestResults/CommitAudit-20261114-215302/kul` | Completed; по23offline checks source Debug/published Release/fresh package Release; final resources0 |
| Kul tests | `dotnet test TrueMoon.Kul.Tests/TrueMoon.Kul.Tests.csproj -c Debug` и Release, с `--no-build --no-restore`, TRX/binlog в evidence root | По23passed, failed/skipped0, xUnit2/VSTest |
| Package metadata | `eng/Check-PackageMetadata.ps1 -Pack -NoBuild -IncludeProject @('TrueMoon.Kton/TrueMoon.Kton.csproj','TrueMoon.Kton.Bepu/TrueMoon.Kton.Bepu.csproj','TrueMoon.Kul/TrueMoon.Kul.csproj')` | 3projects/3intended packages/3archives verified |

Для всех MSBuild-команд использовались уникальные binlogs; delivery runners сами
фиксируют SDK/TFM/CPM, reports и hashes. Слышимое аудио/device acceptance, native
rebuild, PHYS1 standalone suite, GPU/GUI и другие module suites в этой сверке не
запускались. Более ранние результаты сохранены с исходными датами и границами.

[ARCH3.6 baseline](engine/ARCH36_BASELINE.json) содержит недавние non-incremental
solution D/R0errors/1570warnings, четыре engine suites по4956passed/configuration
и12Mod SDK profiles, включая SDK-free. В этой сверке все его production/test/
consumer SourceInputs по-прежнему совпали с SHA; эти длительные прогоны повторно
не запускались. Документационные снимки baseline остаются датированной историей;
последующие навигационные исправления перечислены ниже.

## Сверка docs

При проверке index/checkout воспроизведён и исправлен Windows source-delivery
дефект MiniAudioEx: `core.autocrlf=true` менял patch SHA на
`78E9845DE2D83F0B2285B3A10546855F6F95DE59024AE93B1A1EAC61299CD5FF`.
Локальный `.gitattributes` закрепляет LF и сохраняет context whitespace патча.
Проверка checkout подтвердила точное совпадение patch с pinned
`D9DDCA3DA584DEEF9FF4549CD18453AB92A23B270D1217826E5FC235A0C00DF5`;
его содержимое и native binary не менялись. Native rebuild не требовался.
Исходный EOF spacing перенесённого ScenePreviewDocument сохранён; staged whitespace
проверка допускает blank-at-EOF и проверяет остальные source/docs отдельно от
значимых context spaces upstream patch.

Включаются полностью новые `docs/audio` и `docs/physics`, карты/руководства/
versioned manifests Aether и изменения общих PLAN/STATUS/HISTORY. Исправлены три
physics ссылки на старые Aether ECS paths. Пять ссылок на отсутствующие старые
ignored TestResults заменены явными текстовыми путями с отметкой недоступности;
исторические passing counts не изменялись. Текущие local-file links изменённых
docs и ProjectReference проверены без пропусков.

Корневая навигация дополнена Kton и текущими слоями Aether. Configuration docs
ссылаются на public Kul/Kton schemas отдельно от generated CORE7.6 catalog;
проверка старого каталога не объявляется проверкой всех новых module schemas.

Следующие этапы остаются прежними: ARCH3.7 system replacement, ARCH3.8 migration,
PHYS5 ECS bridge, AUD2–3 расширение audio scope. Наличие этих планов в коммите
не означает завершённой реализации.

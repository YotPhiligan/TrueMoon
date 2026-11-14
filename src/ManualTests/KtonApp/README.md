# Kton PHYS4: App, configuration и оба DI providers

Из `src`, PowerShell 7 и SDK из общего `global.json`:

```powershell
pwsh -NoProfile -File ManualTests/KtonApp/Verify.ps1 -EvidenceDirectory TestResults/KtonPHYS4-local-001
```

EvidenceDirectory должен отсутствовать. Consumers, feed, caches и artifacts создаются
в новом temp directory **вне checkout**; можно указать новый `-RunDirectory`.
Общий `ManualTests/BuildProfile.ps1` передаёт SDK/TF/TestsTF/LangVersion/Features;
CPM отключается явно. Source consumer использует реальные project references и
Cobalt generator. Свежий consumer использует только пакеты, bundled Cobalt analyzer
и изолированные local feed/cache. Каждый restore/build/pack/publish имеет unique
binlog, stdout/stderr, timeout/reap и exit code. В конце сверяются source/profile SHA.

Точные внешние архивы берутся из уже восстановленных project.assets.json; если
архив отсутствует в выбранном cache, скачивается эта же версия с nuget.org.
BEPU beta.33 дополнительно сверяется с принятыми PHYS0 SHA256. Обновления пакетов
runner не выбирает. Для полностью offline запуска восстановленные assets/caches
должны содержать архивы. Запуск не публикует remote packages.

Шесть пакетов TrueMoon одной новой версии: Contracts, Cobalt, bootstrap TrueMoon,
Microsoft DI adapter, Kton и Kton.Bepu. В net11 fresh graph ровно **9 packages**:
эти 6, BEPU 2 и Microsoft.Extensions.DependencyInjection 11.0.0. DI Abstractions в
этом SDK приходит из .NET shared framework; его actual path/SHA фиксируется отдельно.
Loaded package DLL SHA сверяются с nupkg. Для обоих consumers проверяется реальный
сгенерированный Cobalt constructor для ProbeSession, без ручного consumer resolver.

Один program выполняется source Debug, fresh NuGet Debug и published Release.
Каждый запуск проверяет Cobalt и Microsoft DI: duplicates/conflicts, invalid options,
все 16 schema fields, общие приоритеты/provenance и detached callback snapshots,
default/explicit фабрики, startup без BEPU resources, App isolation и независимые миры.
Сессия получает фабрики через constructor DI; reverse stop и startup rollback
закрывают её мир до host. App не владеет выданными мирами.

Failure checks используют реальные BEPU allocations и внутренний friend seam
`KtonAppProbe`: failed creation + failed cleanup, failed startup rollback, stop,
final DI Dispose, cached App disposal failure, explicit retained-host retry,
world cleanup retry после App и repeated successful cleanup. Это ошибки **между
стадиями** освобождения, а не моделирование произвольной unsafe corruption внутри
BEPU.Dispose. Программные проверки используют fake configuration source только как
data input общего configuration stage; фабрики/миры/DI/App настоящие.

Правила использования: [App guide](../../docs/physics/APP.md),
[STATUS](../../docs/physics/STATUS.md), [HISTORY](../../docs/physics/HISTORY.md).
PHYS5 ECS/session bridge, GPU/native launch, другие OS/AOT/parallel workers и full
solution suites этим acceptance не проверяются. PHYS2–3 provider regression запускается
отдельно через [KtonProvider](../KtonProvider/README.md).

# Mithril: состояние и точка продолжения

Дата: **2026-10-14 — MIT5/ENE5 package portion**.
[Контракт](PLAN.md#mit5ene5-independent-package-contract--2026-10-14),
[команды и scope](HISTORY.md#mit5ene5-independent-package-consumer--2026-10-14),
[runner](../../ManualTests/UnitsPackageConsumer/README.md), [общий статус](../STATUS.md).

## Реализация и фактические проверки

| Область | Изменения / scope | Запуск2026-10-14 |
| --- | --- | --- |
| Package | Description/самостоятельный packaged README; runtime без новых dependencies на Enerit/UI |Mithril и transitive Contracts/Core доставлены из local feed; одинаковые версии/provenance |
| Own child delivery | Independent PackageReference App, delivered apphost/assembly/deps/runtimeconfig |108/108Debug/Release ×build/publish cases Cobalt/DI и DI без Cobalt analyzer, Completed=true |
| Restart/lifetime | Existing MIT3/MIT4 contracts; runtime без изменений | Actual distinct PID/control generation, old pending failure без replay, limit1 exhaustion и terminal App cancellation |
| Stop/rollback/parent exit | Owned controller/transport cleanup на installed runtime | Stop pending, failed second executable, graceful child exit0/local Stop1; before App.Dispose workers/controller drained; killed parent client не наблюдаем |
| Mithril regression | Existing89cases | Debug89/89 и Release89/89, Failed0/Skipped0 |
| Related suites | Source graph после pack, отдельно | Enerit126/generator114, Core81, Cobalt37/generator6 в каждой Debug/Release configuration |
| Solution | Final non-incremental Debug | Exit0/errors0/warnings1227; UI/native не повторены |

Windows x64/build26300/net10.0/SDK10.0.402. Source/nupkg/compiler hashes,
metadata/provenance и generation evidence сохранены в ignored TestResults/UnitsPackages.
Previous MIT4 source36/isolated12 и decode hardening — предыдущие отдельные runs;
current package108 не является новым standalone stress/performance measurement.

## Ограничения

MIT5 package portion проверена; **operational diagnostics ещё открыты**, полный
MIT5 не объявляется закрытым. Framework-dependent без RID, same-build cooperative
owned child. Main общий App graph; External launched/owned без borrowed attach,
external executable restart отдельно не подтверждён. Default Always bounded3/delay250,
terminal-only OnExit/stop fence и остальные MIT0–MIT4 contracts сохраняются.

Package acceptance не обещает other OS/compiler/RID/self-contained/single-file/
trimming/NativeAOT, arbitrary callbacks/hooks/transports, uncooperative services,
long resource trend/zero-leak или готовность игрового runtime. Parent-exit proof
подтверждает child/server cleanup, не workers killed parent.

## Следующий конкретный шаг

Общий функциональный приоритет — [Enerit IPC baseline](../enerit/STATUS.md),
затем MIT5 operational diagnostics: policy/generation/exit/restart-failure/cleanup
trace и reproducible configuration/commands без расширения process ownership.

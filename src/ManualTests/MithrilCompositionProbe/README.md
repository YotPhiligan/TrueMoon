# Mithril App composition probe

Bounded source consumer для MIT1/MIT2: Windows x64, net10.0, `RestartPolicy.Never`,
App lifetime. Проверяет Cobalt и Microsoft DI в Debug и framework-dependent
published Release. Это отдельная проверка App composition; независимый NuGet
consumer и Enerit IPC сюда не входят.

Из `src`:

```powershell
pwsh -NoProfile -File ManualTests/MithrilCompositionProbe/RunCompositionSmoke.ps1
```

Runner строит Debug, публикует Release и выполняет 28 случаев:

| Сценарий | Проверяемый результат |
| --- | --- |
| `main` | Unit services/configuration входят в текущий App graph; PID совпадает, start/stop выполняются один раз |
| `composition` | AddUnit запускает child; identity/readiness подтверждены, App Stop завершает его с exit0 и очищает controller |
| `external` | Controller запускает и владеет отдельным executable; shutdown и cleanup подтверждены |
| `startup-failure` | После готовности первого delayed child второй executable отсутствует; исходная Win32 ошибка сохраняется, первый child остановлен |
| `cancel-start` | После ручного запуска delayed child pre-cancelled App Start отменяется; App Dispose завершает уже принадлежащий ему child |
| `control-eof` | Закрытие подключённого control pipe отменяет lifetime child; parent App остаётся работающим до своего Stop |
| `parent-exit` | Supervisor завершает только созданный им parent; child сам обнаруживает parent exit и выполняет App Stop |

Готовность подтверждает atomically published `ready.json`, остановку сервиса —
`stopped.json`; exit code проверяется после наблюдения настоящего процесса.
Все процессы принадлежат probe. Timeout ограничен; аварийный cleanup завершает
только созданные процессы. У каждого случая новый каталог, поэтому прежний
readiness не принимается за текущий.

Reader использует единый deadline8s для ожидания публикации и чтения: повторяет
только missing-file и Windows sharing/lock conflicts, с token у IO и retry delay.
Malformed/null JSON завершается ошибкой. Directed режим
`--read-ready <directory> <cancelAfterMilliseconds>` проверяется ReadyFileProcessTests:
`RETRY` подтверждает первую transient ошибку,0сохраняет обычный8s budget,
положительное значение дополнительно задаёт раннюю отмену.

В ignored `TestResults/MithrilComposition/manual/summary.json` сохраняются UTC,
SDK/OS/architecture, команды, exit codes, результаты и SHA256 runtime/fixture
sources. Логи build/publish/stdout/stderr находятся рядом. `Completed=true`
устанавливается после успеха всех 28 случаев. SHA256 не является coverage.

Новый child control channel — отдельный asynchronous named pipe; уникальный
endpoint передаётся в environment `TrueMoon.Mithril.ControlPipe` вместе с `-u`
и `-p`. Parent observer не владеет parent process и никогда его не завершает.
Старым stdin-only children handle по-прежнему отправляет `qe` и flush.
При ручном запуске child без control endpoint доступно только наблюдение parent.

Main services используют общий App DI graph/lifecycle; отдельного scope нет.
External означает запуск собственного executable, attach к чужому процессу
не реализован. Main delayed startup, Request lifetime, launch parameters и
nested AddUnit отвергаются явно. Остальные restart policies требуют MIT3;
сохранённый default `Always` не подтверждён этой матрицей.

[План и контракт](../../docs/mithril/PLAN.md),
[текущий статус](../../docs/mithril/STATUS.md),
[история проверок](../../docs/mithril/HISTORY.md).

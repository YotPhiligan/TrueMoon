# Core headless quality proof

Один console consumer использует конфигурацию, диагностику и lifecycle App на Cobalt.
Microsoft DI adapter остаётся compatibility control. Исходники Program/Graph/
ConsumerProbe общие с [независимым package consumer](../CorePackageConsumer/README.md);
source project подключает текущие framework projects и generator явно.
Проект не входит в solution и не является engine или benchmark.

Из src, PowerShell7 и SDK10:

```powershell
pwsh -NoProfile -File ManualTests/CoreHeadlessProbe/RunHeadlessProof.ps1
```

Необязательный RunDirectory должен быть новым каталогом внутри
TestResults/CoreHeadless. Runner сначала выполняет52source processes:
Debug/Release × build/publish,7сценариев Cobalt и6Microsoft DI.
Затем запускает RunPackageSmoke с новым local feed/version/cache:
80package matrix processes,3delivered README examples и negative analyzer control.
Framework packages не публикуются; пользовательские настройки не меняются.

| Сценарий | Проверка |
| --- | --- |
| normal | lifetime.Cancel, штатный reverse stop, Dispose |
| cancel | Внешний token возвращается caller после shutdown/cleanup |
| start-failure | Исходная ошибка, reverse rollback только запущенных services |
| stop-failure | Stop остальных services и disposal после ошибки Stop |
| build-failure | Созданный до ошибки ресурс и diagnostics очищаются без startup |
| config-failure | Исходная ошибка validation до services/resources/subscriptions |
| cleanup-failure, Cobalt | Startup и Dispose errors сохраняются вместе, cleanup остальных объектов продолжается |

Cleanup-failure ограничен Cobalt: native Microsoft DI имеет собственные semantics
ошибок Dispose; адаптация этого поведения не является целью TrueMoon.

Каждый процесс создаёт отдельный work-каталог рядом с отчётом. JSON null заменяется
через GetOrCreate, повторное чтение сохраняет identity до refresh, файл содержит
созданные settings. Malformed refresh сохраняет старые настройки; после восстановления
файла refresh снова работает. Later provider сохраняет приоритет при merged section.
Повторный UseDiagnostics оставляет одну subscription/factory и последнюю конфигурацию;
исключение observer не прерывает приложение. После shutdown sources отвергают запись,
новый DiagnosticListener не подписан.

OwnedFactory держит реальный FileStream с FileShare.None во время Start/Stop.
После cleanup тот же файл открывается эксклюзивно, содержимое сохранено; counters
owned/transient/borrowed и полный lifecycle trace проверяются отдельно.
В config-failure ресурс не создаётся. Evidence work-файлы остаются для ревью.

Дочерние процессы ограничены timeout, завершает runner только собственное дерево.
summary.json содержит команды/exit codes,52reports с SHA256, source fingerprints
и ссылку/SHA на completed package summary. Перед Completed=true повторно сверяются
обе source manifests. Предыдущие каталоги не перезаписываются.

Scope: Windows x64/net10.0, framework-dependent без RID. Программная cancellation
не является проверкой реального Ctrl+C/kill. Cooperative shutdown, глобальные prefix
diagnostics, in-process configuration refresh и disposal limitations сохраняются.
UI/IPC/units/other OS/RID/self-contained/trimming/NativeAOT и performance отдельно.

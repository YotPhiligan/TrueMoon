# Nom: расширенная проверка отказов и восстановления

<a id="nom3-recovery-2026-11-08"></a>
## NOM3 — профиль Windows x64/net10.0

Этап проверяет собственный backend после [NOM2 cutover](CATALOG.md). Не использует
LiteDB, vendor workers или provider fallback. Текущие counts/commands/source hashes
и фактические результаты — [STATUS](STATUS.md), [HISTORY](HISTORY.md#nom3-2026-11-08).
Инъекция ошибки, process kill и physical disk-full учитываются раздельно.

## Матрица

| Граница | Проверяемое свойство |
| --- | --- |
| High-water allocation | До/после write/flush, zero/partial/full write before throw. Не выданный ticket не считается принятым; durable sequence gap не переиспользуется |
| Pending operation/items | Один batch, отсутствие половины items. Unknown с ticket требует close/reopen; существующие bytes/generation не меняются |
| Resource publication | Согласованные metadata/head/revision/outcome/generation; ambiguous IO не допускает дальнейший append. Reopen разрешает exact committed/aborted outcome; retry не читает replacement input |
| Explicit abort | Durable terminal state либо pending до recovery, прежний content остаётся доступен. Ошибки metadata и staging cleanup сохраняются обе |
| Prune | Floor/outcomes/items меняются целиком; до commit прежние tickets доступны, после commit expired. Content bytes/heads остаются живыми |
| Recovery abort | После неудачного Open фактические handles освобождаются или остаются в registry до явного retry. Reopen не повторяет content publication |
| Tail repair | Truncate/flush errors не превращаются в успешный Open; повторный recovery сохраняет committed prefix |
| Create authority | Segment/header/pending root/publication/initial catalog cuts не вызывают reset. Uninitialized root остаётся явным отказом; complete catalog открывается |
| Lifecycle | Cancel/commit и stop/reservation races, poisoned reads, ignored input cancellation, actual handle close и retry; root не освобождается при продолжающейся IO |
| Capacity | До allocation проверяется место для high-water, pending и publication/abort. Segment/replay/index и per-frame budgets имеют equality/one-short witnesses |

## Исправленные состояния

Резервирование ранее могло занять последние frames/bytes/index budget, оставив
незавершённую operation без возможности записать abort. Теперь перед первым
allocation проверяется консервативный `NomKvAppendBudget`:

| Бюджет | Верхняя оценка для n descriptors размера Lᵢ |
| --- | --- |
| Frames |3: allocation + pending + publication (либо меньший abort) |
| Sum payload |344+Σ(4Lᵢ+710) bytes; ещё3×144 bytes framing |
| Largest batch payload |183+Σ(3Lᵢ+567) |
| Largest mutation count |6n+2, включая возможные old binding tombstones |
| Largest key/value |40 bytes / max(Lᵢ+116) bytes |
| New index keys/bytes |5n+1 /112+632n |

Во время reservation worker запрещает другие catalog writes. Отказ происходит
до изменения Next, выдачи ticket и staging. Оценка намеренно консервативна: обновление
с dedup/reused keys может занимать меньше. Rotation/checkpoint/compaction не добавлены.
Internal StorageLimits нужны для реальных малых boundary fixtures; публичный API и
NomOptions не расширены.

Poisoned KV read также мог блокировать stop незапущенной reservation: append abort
уже запрещён, но reservation удерживала owner бесконечно. Internal stop теперь
освобождает только принадлежащий staging и закрывает handles без metadata append.
Pending outcome завершает validated reopen. Public Abort/Find/Publish на poisoned
owner по-прежнему требуют close/reopen, не сообщают ложный успех.

Stop во время allocation/pending IO больше не теряет передачу reservation в cleanup:
после снятия Reserving worker замечает stopping и ставит internal abort в reserved
control slot. Первый stop ждёт фактическую IO и завершает cleanup без лишнего retry.

## Реальные process cuts

`NomFaultProof` — отдельный console профиль, **41 сценарий на конфигурацию**:

-6domain phases ×4cuts: allocation, pending reserve, commit, abort, prune,
  recovery abort; before-write, partial100bytes, after-write, after-flush.
-3repair cuts: before-truncate, after-truncate, after-repair-flush.
-14Create cuts: segment, pending root и initial catalog ×4IO cuts, root publication
  before/after no-overwrite move.

Дополнительные7prepare children оставляют настоящую durable pending operation.
Итого **48children/profile**. Parent проверяет live exclusive lock и exact cut marker,
вызывает Kill(tree), затем ждёт exit/reap. Timeout/failure также reaps owned children.
Проверяются raw committed prefix до recovery, затем catalog reopen, exact old/new
resource bytes, generation, request outcomes/floors и no-reset snapshots. Это обычный
process kill на работающей ОС; device cache/power-loss behavior им не доказывается.

`Verify.ps1` default All запускает прежние Core18/Kv6 и новый Fault41 профили в
source Debug и framework-dependent published Release. Пустые feed/cache подтверждают
отсутствие external packages у Nom/Contracts/probe. Public App consumer остаётся
отдельным `VerifyApp.ps1`, без friend access.

## Повторение

Из src, .NET10 SDK, PowerShell7, Windows x64; каждый evidence path новый:

```powershell
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug '-bl:TestResults/Nom3-Debug-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Release '-bl:TestResults/Nom3-Release-{}.binlog'
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/MyFreshNom3
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -EvidenceDirectory TestResults/MyFreshNom3App
& ./ManualTests/TrueMoon.Nom.Probe/CheckFaultVolume.ps1 -EvidenceDirectory TestResults/MyFreshNom3Volume
```

## Physical environment gate

**NOM7 UAC2026-11-08:** [real-volume harness](DURABILITY.md) прошёл:
staging/catalog append/checkpoint/export/restore на новом owned128MiB VHDX, public
fresh-package consumer,5cases/166assertions каждый Debug/published Release. Free=0 и
OS112, exact old state/outcomes, reopen/append liveness подтверждены; VHDX отключён,
cleanup0. Power-loss остаётся отдельным открытым gate. Дальнейший текст сохраняет
исторический NOM3 отказ без Administrator; [новый результат](HISTORY.md#nom7-uac-2026-11-08).

Свежая попытка New-VHD64MiB отклонена Windows authorization policy; текущий процесс
не Administrator. Это OS permission gate, не отказ автоматического review. VHDX не
создан, том не подключён/не форматирован; рабочий диск не заполнялся. Versioned
CheckFaultVolume.ps1 только проверяет создание нового isolated VHDX внутри workspace,
никогда не подключает и не заполняет существующие тома. Успешное создание само по
себе также не доказывает disk-full acceptance.

Физическая acceptance остаётся открытой: в отдельном разрешённом окружении нужны
изолированный ограниченный том, реальный disk-full в staging и catalog append,
освобождение только принадлежащего test filler, close/reopen и exact outcome/bytes.
Fault injections с IOException/HRESULT0x80070070 не подменяют этот результат.
Power-loss, hostile filesystem, RID matrix, maintenance и performance остаются
отдельными проверками. Полный NOM3 не объявляется закрытым при открытом physical gate.

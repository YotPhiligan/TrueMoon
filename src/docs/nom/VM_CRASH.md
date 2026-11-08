# Nom: стенд аварийного выключения гостевой VM

<a id="nom-vm-crash-2026-11-08"></a>
## Принятый scope — 2026-11-08

**Actual VM verification отложена по просьбе пользователя2026-11-08.** Подготовка
параметров сейчас не требуется; source/guards/protocol сохраняются. Возврат к guest
trials — только после явного возобновления. Acceptance не закрыта; независимые
Nom/engine задачи продолжаются. [Решение](HISTORY.md#nom-vm-deferred-2026-11-08).

По решению пользователя тестируется **аварийный Hyper-V TurnOff отдельной VM**.
Аппаратное отключение питания host/device исключено из этого этапа и его acceptance,
не имитируется и не объявляется пройденным. Существующая [disk-full acceptance](DURABILITY.md)
сохраняет свой отдельный результат. VM crash требует фактического Off, нового guest
boot и восстановления данных; host process kill служит только проверкой протокола.

Реализованы controller и семь guest cutpoints. **Реальный VM run пока не выполнен:**
elevated inventory показала0VM и0подготовленных образов в стандартных каталогах;
путь к Windows VHDX и guest credential не предоставлены. Source/published host protocol
и98guard cases проверены, они не являются VM acceptance.

## Подготовленный образ и запуск

Нужен trusted standalone detached Windows Gen2/UEFI VHDX, virtual size8–256GiB,
с настроенным локальным administrator account/profile, PowerShell Direct service и
установленным x64.NET10 runtime в PATH. Guest SDK/PowerShell7 не обязательны: remote
bootstrap использует Windows PowerShell, а compiled probe запускается через dotnet.
Host — Windows x64, PowerShell7, .NET10 SDK, Hyper-V/Storage cmdlets и UAC Administrator.
Network adapter стенду не подключается; runtime/guest components готовятся заранее.

В elevated PowerShell из src, используя **новый** evidence path:

```powershell
$guest = Get-Credential -Message 'Локальная учётная запись подготовленного тестового Windows guest'
& ./ManualTests/TrueMoon.Nom.Probe/VerifyVmCrash.ps1 `
  -EvidenceDirectory TestResults/MyFreshNomVmCrash `
  -BaseOsImage 'E:\VM-Images\PreparedWindows.vhdx' `
  -GuestCredential $guest
```

Это пример caller-supplied image, не обещание наличия такого файла. Credential передаётся
локально как PSCredential; password не записывается в JSON/command text и не требуется
в чате. Controller не скачивает OS images и не устанавливает систему автоматически.

Без образа/credential команда сначала проверяет guards/protocol, затем возвращает
exit2/Blocked до New-VHD/New-VM/Start-VM/Stop-VM. Exit1 означает harness/build/trial/
cleanup failure; exit0 требует всех7фактических guest cuts и recovery reports. В отличие
от прежнего disk-full runner аппаратный power-loss не удерживает VM acceptance открытой.
Legacy disk-full JSON с PowerLossOpen не переписываются; их ограничение остаётся историей.

## Ownership и controller

[VerifyVmCrash.ps1](../../ManualTests/TrueMoon.Nom.Probe/VerifyVmCrash.ps1) создаёт только
свою `NomCrash-<random-token>` VM. Existing VM ID/name не принимается как параметр.
OS — новый differencing child; parent image открыт read-only с deny-write lease,
проверены standalone/detached/reparse/size и SHA. Source image не меняется. Data — новый
fixed128MiB VHDX; до host initialize/format повторно проверяются exact path, VHD ID,
disk number/UniqueId/size, RAW/no-partition и non-system/non-boot. В guest data partition
опознаётся по persistent GPT GUID,128MiB disk, token marker и label.

Перед каждой VM operation проверяются ID/name/owned path, ровно два owned disk paths,
Gen2, disabled checkpoints и0network adapters. Save/resume/restore snapshot не являются
допустимым crash proof. Только fresh VM получает `Stop-VM -TurnOff -Force`; код не
вызывает host restart/shutdown или device power commands. После run собственная VM
останавливается, registration/OS child/data images остаются evidence в Off состоянии.
Automatic recursive deletion/Remove-VM нет; cleanup error не становится success.

Host получает ready witness только от live worker с matching PID/start time, trial,
phase,boot,all IDs и module SHA. Expected byte digests вычислены независимо на host.
Перед TurnOff сохраняется host-witness.json через CreateNew+Flush(true). После actual
Off сохраняются host-cut.json и raw data VHDX **до reboot/recovery**. Затем новый boot
должен отличаться от исходного. Expected config/witness заново копируются из host
ledger, проверяются owned data partition mapping и recovery assertions. Один guest log,
произвольный marker или обычный рестарт процесса не закрывает gate.

PowerShell Direct вызовы — jobs с bounded wait; worker hold≤180s, cut-ready≤60s,
boot default180s (60–300 configurable), TurnOff wait≤30s, recovery call≤30s.
Admitted artifacts≤8GiB/4096files и host free≥2GiB проверяются между remote calls.
Эти логические budgets не являются физической reservation места; OS image copy/hash
и Hyper-V storage/bootstrap операции зависят от подготовленного окружения.

PowerShell Direct не требует сети; [официальный механизм](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/powershell-direct).
Флаги [Set-VM](https://learn.microsoft.com/en-us/powershell/module/hyper-v/set-vm)
задают explicit checkpoint/automatic-action policy только новой VM.

## Семь точек сбоя

[NomVmCrashProof](../../ManualTests/TrueMoon.Nom.Probe/NomVmCrashProof.cs) использует
существующий friend probe и per-factory IO seam. Hook выполняет реальное IO, затем
удерживает поток для внешнего cut; IOException/physical failure не подбрасывается.
Это instrumented source/published core proof, не новый public fault API и не fresh
NuGet VM consumer. Package consumer proof остаётся отдельной [NOM7 delivery](DELIVERY.md).

| Phase | Точка удержания | Recovery invariant |
| --- | --- | --- |
| commit-ack | После acknowledged resource commit | Exact generation2/new head/bytes и committed ticket не теряются |
| cas-finalized | CAS move выполнен, metadata commit ещё не начинался | Old generation1/head/bytes; issued ticket разрешается aborted, orphan CAS не становится head |
| journal-partial |100bytes настоящего commit frame до flush | Whole old state, validated tail repair и aborted candidate |
| checkpoint-ready | Checkpoint flush выполнен, authority ещё старая | Old acknowledged state; selected authority и retry остаются валидными |
| root-switched | Root replace выполнен до ответа CompactAsync | Полный old/new authority set, exact unchanged content/outcomes, без удобного fallback |
| backup-published | Backup directory move выполнен до API ack | Destination отсутствует либо complete verified backup, исходные данные сохранены |
| restore-published | Restore directory move выполнен до API ack | Destination отсутствует либо complete exact restored store; source/backup живы |

Во всех случаях проверяются persistent project/store IDs, old seed version и retained
committed outcome, independently generated geometry bytes, length/EOF. При acknowledged
new commit проверяется также new version/outcome. После recovery проходит новая durable
запись с generation+1. Regular file вместо publication directory, foreign witness,
unknown schema/phase/identity — явный отказ. Corrupt selected authority не заменяется
другим root/reset. VM profile ещё требует actual execution на предоставленном образе.

## Проверки без guest image

```powershell
& ./ManualTests/TrueMoon.Nom.Probe/TestVmCrashGuards.ps1 -Report TestResults/MyFreshVmGuards.json
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -Profile VmProtocol -EvidenceDirectory TestResults/MyFreshVmProtocol
```

98guard cases: owned identity/disks/states, all7phase witnesses, stale/foreign IDs,
no-save/no-checkpoint/no-network, independent golden digests, distinct boot/actual
TurnOff requirement, CreateNew/no overwrite и ASCII/UTF8 exact64KiB/one-byte-over.
Host protocol —10groups каждый source Debug/published Release:7actual process cuts,
ForeignWitnessRejectedWithoutMutation, RegularBackupPublicationRejected и invalid
configuration rejection.7children/profile started/killed/reaped; этот report всегда
VmCrashTested=false/HardwarePowerLossTested=false. Default All сохраняет120functional
groups; VmProtocol включается явно и не выдаётся за VM run.

Фактические команды/результаты и blocked prerequisite — [HISTORY](HISTORY.md#nom-vm-2026-11-08),
единственная очередь — [PLAN](PLAN.md#nom-own-queue). Реальные VM trials требуют image
и локального guest credential. Аппаратный стенд не является следующим обязательным шагом.

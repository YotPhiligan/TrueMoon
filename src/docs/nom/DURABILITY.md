# Nom: physical durability acceptance

<a id="nom7-physical-2026-11-08"></a>
## NOM7 follow-up — 2026-11-08

По запросу пользователя добавлен runnable isolated-volume disk-full harness.
**После разрешённого пользователем запуска через UAC real-volume disk-full и recovery
проверены в Debug/published Release:5cases/166assertions каждый.** Новый128MiB VHDX
создан/отформатирован и отключён, cleanup errors0. По следующему решению пользователя
аппаратное отключение питания исключено из текущей приёмки; [VM crash harness](VM_CRASH.md)
реализован, actual run ожидает подготовленный Windows image и guest credential.
Прежний отказ New-VHD относился к процессу без Administrator.
Package delivery остаётся проверенным в [своём scope](DELIVERY.md).

| Gate | Проверка | Текущее состояние |
| --- | --- | --- |
| Real disk-full | Новый ограниченный128MiB VHDX/NTFS, реальные несжатые non-sparse filler writes, OS disk-full и storage failure |5cases каждый D/R, free=0 и Win32 disk-full112 во всех10trials |
| Recovery after disk-full | Закрытие своего DeleteOnClose filler, close/reopen, exact old bytes/head/IDs/generation/acknowledged outcome и append liveness | Exact old generation1/bytes/outcome сохраняются; новая generation2 в каждом случае; compaction/transfer retries прошли |
| Abrupt guest power-off | Внешний VM controller, независимый acknowledgement ledger, cut без guest shutdown, последующий boot/recovery | [Runner реализован](VM_CRASH.md),98guards/10host protocol groups D/R passed; actual VM0trials без образа |
| Physical power loss | Потеря питания host/device и их volatile caches | Исключена пользователем из текущего acceptance, не выполняется и не заявляется проверенной |

## Запуск

Windows x64, PowerShell7, .NET10 SDK, Hyper-V/Storage cmdlets и права на создание/
подключение собственного VHDX. Только новая evidence directory внутри workspace:

```powershell
& ./ManualTests/TrueMoon.Nom.Probe/VerifyDurability.ps1 -EvidenceDirectory TestResults/MyFreshNomDurability
```

Runner сначала выполняет [VerifyPackages](../../ManualTests/TrueMoon.Nom.Probe/VerifyPackages.ps1),
затем копирует [публичный consumer](../../ManualTests/TrueMoon.Nom.Probe/PublicDurabilityConsumer.cs.template)
в отдельный OS TEMP project с PackageReference. Fresh private feed/cache, ровно четыре
согласованных пакета, без ProjectReference/fallback; SHA cached nupkg и output Nom DLL
сверяются с delivery. Компилируются Debug и framework-dependent published Release,
выполняются nondestructive guard checks. Storage API не расширяется; friend access нет.

После этого runner создаёт **только новый** `isolated-<random-token>.vhdx` внутри evidence.
128MiB fixed backing allocation ограничивает потребление host; перед созданием нужно
≥512MiB host free space. Existing VHDX/диск/том не принимаются как аргумент.

Перед Initialize-Disk/partition/format и каждым physical запуском повторно проверяются
exact VHD path+DiskIdentifier+DiskNumber, stable disk UniqueId,128MiB size и отсутствие
boot/system role. Initialize допускает только fresh RAW/no-partition disk. Format
получает конкретный partition object этого диска,64–128MiB и non-system/non-boot.
Volume получает unique label и owner marker. Consumer принимает только маленький NTFS
drive root с matching token/label/marker; обычные directories/UNC/host volumes отклоняет.
Это защита от неверного выбора диска, а не security boundary против администратора,
меняющего устройство одновременно с тестом.

Каждый consumer process bounded180s; timeout/failure kills/reaps только свой процесс.
Cleanup проверяет exact path/identity и отключает только собственный attached VHDX,
включая частично неудачный Mount. Ошибка detach сохраняется отдельно и не считается
успехом. VHDX и каталоги не удаляются рекурсивно; они остаются evidence. Существующие
host volumes не форматируются и не заполняются.

## Пять real-volume сценариев

Каждый создаёт отдельный store с acknowledged old resource и opaque48KiB metadata.
Filler пишется64KiB→4KiB реальными WriteThrough writes до Win32 ERROR_DISK_FULL112
или ERROR_HANDLE_DISK_FULL39, проверяются allocated/non-sparse bytes и remaining space.
Обычная IOException или ошибка domain budget не удовлетворяет gate. Отдельная Nom
операция также обязана вернуть настоящий disk-full в exception chain.

| Сценарий | Операция на полном томе | Проверка после освобождения filler |
| --- | --- | --- |
| staging | Publish1MiB resource по заранее выданному ticket | Aborted outcome, нет нового head, старые bytes и generation1 |
| catalog-append | До16Reserve/Abort с большим metadata descriptor до реального disk-full: первый append может ещё пройти при free=0 | В текущем D/R отказ на3attempt; whole old content и last issued aborted ticket после reopen; невыданный ticket не выдумывается |
| checkpoint | CompactAsync с большим selected catalog | Old authoritative root/state читается; explicit compaction retry успешен |
| export | ExportBackupAsync в absent destination на полном томе | Backup не опубликован, source exact state сохранён; export/restore retry в новый root |
| restore | Typed restore factory из заранее verified backup | Destination не опубликован; source/backup сохранены, restore retry exact |

Filler остаётся открыт FileShare.None/WriteThrough/DeleteOnClose до конца fault phase:
освобождается в finally **до** cleanup/reopen. Проверяются actual removal и возврат
≥16MiB free space с bounded15s wait, чтобы следующий шаг не зависел от delayed reclaim.
Проверки WholeOldStateAfterReopen,
AcknowledgedOutcomeSurvivesDiskFull, ExactBytesAfterDiskFull/ExactLengthAndEofAfterDiskFull
требуют exact IDs/revision/generation/committed seed ticket и каждый byte/EOF. Затем
AppendLivenessAfterDiskFullRecovery подтверждает новую acknowledged generation2.
Failed prepared export/restore directories не удаляются и не переиспользуются.
Все пять сценариев прошли после исправления этих условий самого harness;
[failure attempts и final evidence](HISTORY.md#nom7-uac-2026-11-08) сохранены отдельно.

## Machine-readable gates

`durability.json` различает GuardProfiles, physical Profiles, PhysicalDiskFullTested,
DiskFullStatus и PowerLossTested. Pure guards всегда PhysicalDiskFullTested=false.
Disk-full alone не устанавливает AllPhysicalGatesClosed/Completed=true и не доказывает
power-loss; текущий runner оставляет их false. Exit1 означает harness/build/cleanup
failure, exit2 — открытый/заблокированный physical gate, в том числе успешный disk-full
при прежнем PowerLossOpen. Эти legacy поля не меняют новый scope: [VM runner](VM_CRASH.md)
имеет HardwarePowerLossRequired=false и требует actual guest TurnOff/boot/recovery
для своего success. Старые отчёты не переписываются; promotion из process-kill/guards нет.

Без прав на volume operations можно отдельно выполнить защитные проверки:

```powershell
& ./ManualTests/TrueMoon.Nom.Probe/TestDurabilityGuards.ps1 -Report TestResults/MyFreshNomGuards.json
```

Report должен отсутствовать, parent существовать.24pure admission cases проверяют
owned positive cases и rejection boot/system/large/reidentified/wrong-number disks,
existing partitions, wrong VHD path/token, escaped evidence и unsafe partitions.
Consumer дополнительно проверяет4unsafe root inputs и classification112/39/other error,
всего12assertions/profile. Classification fixtures не являются физическими IO faults.

## Исключённая аппаратная проверка и отдельный VM scope

Следующий текст — справочные ограничения аппаратной durability, **не активная задача**
после решения пользователя2026-11-08. Текущий исполняемый этап — [VM_CRASH](VM_CRASH.md):
новая owned VM, external witness и guest TurnOff. Аппаратный стенд для его завершения
не требуется, гарантии поведения host/device при потере питания не заявляются.

Использовать disposable VM либо hardware rig, подготовленный владельцем окружения;
рабочую машину и существующие пользовательские VM runner не выключает. Сначала
зафиксировать guest OS/filesystem, host/storage/write-cache/flush policy и exact package
SHA. Независимый controller/witness должен хранить за пределами обесточиваемого домена
acknowledged tickets/versions/generation, expected byte digests и cut/boot identifiers.
Один guest log или изменившееся время загрузки не доказывает abrupt power cut.

Матрица cutpoints: после acknowledged content commit, между CAS finalize/metadata
commit, после checkpoint flush/на authority root switch, на export/restore directory
publication. После фактического cut сначала сохранить raw root/journal/CAS image, затем
на копии выполнить normal Open/typed restore и exact checks. Acknowledged state не
теряется; для не подтверждённой операции разрешены целые old/new states с разрешением
ticket outcome, без half references/reset. Missing/corrupt selected authority — явный
failure, не fallback на удобную backup copy. Сохранить все repeated trials/failures.

[NomAssetTool create/verify](../../ManualTests/NomAssetTool/README.md) уже предоставляет
независимые expected bytes/receipt/exact snapshot/outcome checks для acknowledged
baseline; сам по себе его запуск до/после reboot не является power-loss proof.
Hyper-V TurnOff удаляет guest volatile state, но host/device остаются powered: такой
результат нужно маркировать guest abrupt-power-off, не hardware power-loss acceptance.
Различие buffered IO и flush описано в [Microsoft File Caching](https://learn.microsoft.com/en-us/windows/win32/fileio/file-caching)
и [FlushFileBuffers](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-flushfilebuffers).

Фактические UAC команды, ошибки и frozen artifacts — [HISTORY](HISTORY.md#nom7-uac-2026-11-08).
Actual VM run отложен пользователем; подготовка image/credential не активная задача.
Real disk-full на NTFS/VHDX не является power-loss, remote filesystem или универсальной OS/RID durability acceptance.
Изменение package/API только по воспроизведённому дефекту.

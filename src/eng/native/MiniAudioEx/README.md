# MiniAudioEx libvorbis lifetime correction

Проверка2026-11-15: upstream managed4.0.1 и native HEAD совпадают с текущим lock;
TrueMoon.MiniAudioEx4.0.1-truemoon.1/patch/binary не менялись, новая пересборка не нужна.
Полные Kul/bridge tests и новый muted WASAPI delivery прошли в [DEP1](../../../docs/core/HISTORY.md#dep1-2026-11-15).
Original source rebuild/RED-GREEN ниже сохраняют прежний scope и дату.
Исходники MiniAudioEx0.11.26 зафиксированы на `9f85b56157027cc57c7cd0a13fea7fee3f1e167e`.
Managed MiniAudioEx.NET4.0.1 берётся неизменённым из точного NuGet archive.
Source/archive/file/patch SHA находятся в `Source.lock.json`; сторонние исходники
скачиваются в отдельное временное дерево, пользовательские checkouts не меняются.

`libvorbis-lifetime.patch` хранится с LF через локальный `.gitattributes`: иначе
Windows checkout с `core.autocrlf=true` меняет bytes и ломает pinned patch SHA.
Пробелы в context lines patch сохраняются как часть точного upstream diff;
их нельзя удалять ради форматирования.

## Исправление

`ma_libvorbis_init_internal` выделяет `OggVorbis_File` через заданный allocator.
`ov_open_callbacks/ov_fopen/ov_wfopen/ov_mopen` очищают собственное частично созданное
состояние при отказе, но не освобождают эту внешнюю allocation. Native patch
освобождает её тем же allocator, завершает data-source base и обнуляет pointer.
Повторный `ov_clear` на failed-open state не выполняется. Required callback/null/empty
arguments проверяются до allocation; успешный uninit также обнуляет освобождённый pointer.
Public C signatures/structures и managed DLL не меняются.

## Собрать и проверить

Из `src`, PowerShell7, Windows x64, MSVC C++ Build Tools и Windows SDK:

```powershell
# Offline, native regression и fresh NuGet consumer.
pwsh -NoProfile -File eng/native/MiniAudioEx/Verify.ps1 `
  -EvidenceDirectory TestResults/MiniAudioExFix-offline

# Дополнительно actual WASAPI с нулевым gain только своего output stream.
pwsh -NoProfile -File eng/native/MiniAudioEx/Verify.ps1 `
  -EvidenceDirectory TestResults/MiniAudioExFix-device -Device
```

Для согласованной проверки на слух добавить `-Audible`. Без него устройство
не издаёт звук. Системная громкость и остальные приложения не меняются.
`-SourceArchive`/`-ManagedArchive` позволяют использовать локальные архивы с теми же SHA.

Только build/package без запуска consumer:

```powershell
pwsh -NoProfile -File eng/native/MiniAudioEx/Build.ps1 `
  -OutputDirectory TestResults/MiniAudioExFix-build
```

Build создаёт baseline и patched DLL одним MSVC toolset (`/O2 /MD`, linker `/Brepro`),
native regression executable, export/dependency logs и отдельный NuGet:
`feed/TrueMoon.MiniAudioEx.4.0.1-truemoon.1.nupkg`.
В `native-build.json` — hashes, compiler/toolset, source pins, commands, original/patched
exports, package entries и предупреждения. Исходные four C4034 предупреждения upstream
о `sizeof` пустых platform-specific типов не скрываются; Regression.c компилируется
с `/W4 /WX`. Native DLL воспроизводилась byte-for-byte на одном toolset; NuGet ZIP
может отличаться временными метками, его SHA фиксируется для каждого артефакта.

Package содержит оба неизменённых netstandard managed assets, только win-x64 native,
patch/provenance и лицензии. Не подключать одновременно upstream JAJ package:
assembly/native names одинаковы. Это отдельная third-party dependency version,
не изменение общей версии пакетов фреймворка. `/MD` сохраняет требование установленного
совместимого MSVC runtime; точные DLL dependencies записываются в build logs.
Другие OS/RIDs этим пакетом не поставляются.

## Подключение к TrueMoon.Kul

После успешного `Build.ps1` установить exact nupkg из receipt в локальный source feed:

```powershell
pwsh -NoProfile -File eng/native/MiniAudioEx/InstallLocalFeed.ps1 `
  -NativeBuildReceipt TestResults/MiniAudioExFix-build/native-build.json
```

Installer проверяет archive/patch/native SHA и не заменяет существующий ID/version
другим архивом. Feed `eng/native/MiniAudioEx/feed` игнорируется Git; binary нужно
собрать на новом checkout. Root NuGet.Config направляет туда только exact ID
`TrueMoon.MiniAudioEx`, версия задаётся в Directory.Packages.props. Если пакет уже
есть, повторно использовать его matching receipt; для другого build artifact брать
отдельный feed/cache либо новую dependency version при изменении распространяемых bytes.

[Сборка и App/DI подключение модуля](../../../docs/audio/README.md),
[public API/lifetime](../../../TrueMoon.Kul/PACKAGE.md),
[module acceptance](../../../ManualTests/KulModuleProbe/README.md).
Модуль использует исходный native vtable; FileVorbisBackend guard не подключается.

## Приёмка

Native regression сначала запускается на исходной source-built DLL и воспроизводит
утечки (RED), затем на patched DLL проверяет (GREEN):

- Callback, memory, narrow-file и wide-file entry points на WAV, malformed OggS,
  truncated OGG и invalid CRC; по8повторов каждого сочетания.
- Корректное OGG decoding, успешный cleanup, invalid callbacks, read failure,
  обе failed allocations vtable, null/empty input и repeated uninit после отказа.
- Нулевые adapter allocation bytes/blocks после исправленных отказов.

Independent consumer использует новый NuGet package из private feed/cache с общим
BuildProfile. Source Debug и published Release проверяют исходный native vtable
**без FileVorbisBackend guard**, прежние leak cases как успешные regressions,
64malformed load failures с file-unlock и allocation-baseline checks, затем valid playback.
Повторяются PCM/streaming,32dispose cycles, ABI всех68allocation types и managed SHA
относительно original shipped DLL/control process; native exports сравниваются с1286
upstream exports. Device/streaming checks и native tests имеют отдельные отчёты.

Этот fix снимает конкретный failed-init leak blocker для Windows x64 audio dependency.
App/DI модуль реализован отдельно в TrueMoon.Kul; данный native profile его не проверяет.
Остаются отдельными: ECS integration, расширенные production budgets/queues, долгий real-time soak,
device loss, доступность duration API, AOT/trimming и другие платформы. Allocator
наблюдает внешний adapter; не объявляет все внутренние CRT allocations libvorbis подсчитанными.
Отчёты native/consumer содержат exact scope; прежние failing vendor results сохранены в engine history.

# INF6: explicit root factory consumer

## INF7: воспроизводимый integration runner

Из корня `src`, PowerShell7.2+, .NET10:

```powershell
pwsh -NoProfile -File ManualTests/AlloyFactoryConsumer/RunIntegration.ps1 -Profile Assembly

$validationBin = Join-Path $env:VULKAN_SDK 'Bin'
pwsh -NoProfile -File ManualTests/AlloyFactoryConsumer/RunIntegration.ps1 -Profile All -ValidationLayerPath $validationBin
```

Второй пример использует уже установленный SDK; вместо VULKAN_SDK можно передать
собственный путь к Bin с VkLayer_khronos_validation.json. Native profiles требуют
Windows x64, desktop OpenGL/Vulkan и validation layer. Если задан VK_LAYER_PATH,
runner использует его; machine/driver settings не меняются, environment восстанавливается
в finally. Без validation native profile завершится до dotnet/build, сохранив failure.
Assembly не создаёт окна и не требует Vulkan layer; UI suite использует штатные
native Skia assets. Проверенная платформа runner — Windows x64.

| Profile | Фактически выполняемые проверки |
| --- | --- |
| Assembly | Hosting real assembly через ProjectReference + UI suite, Debug/Release,4TRX; пустая/failed/skipped discovery отклоняется |
| Source |8factory consumer processes/48App cases Cobalt/DI×OpenGL/Vulkan, Debug apphost/published Release; scheduling/close/dispatcher/HUD ещё8native processes |
| Package | Fresh dependency packages и isolated consumer без ProjectReference/CPM inheritance;8native factory processes, без view registration и Cobalt analyzer |
| All (default) | Все три профиля;24native processes и обе test suites в двух конфигурациях |

`-RunDirectory` задаёт новый каталог строго внутри `src/TestResults/AlloyInfrastructure`;
relative path считается от `src`, иначе создаётся timestamp directory. Existing
directory никогда не перезаписывается, ничего не удаляется. `-TimeoutSeconds`
ограничивает каждый command (default180); timeout завершает только owned process tree.
`-PackageVersion` допускает prerelease; default выводится из repository version с
уникальным timestamp suffix. NuGet push отсутствует. Запускайте один runner на checkout:
producer bin/obj общие; concurrent builds или source drift могут сделать proof невалидным.

`summary.json` содержит выбранный scope, phase status, SDK/OS, commands/PIDs/exit codes,
TRX counters, reports и SHA256 source inputs/artifacts. Logs/partial manifest сохраняются
при ошибке; Completed=true устанавливается только после проверки всех выбранных фаз.
Source input set также сверяется: новый файл не скрывается за старыми hashes.
Failed/skipped/zero-discovery, missing commands/reports, package graph/source mismatch,
analyzer/native payload omission, changed loaded DLL/native asset отклоняются.
Source/golden fixtures runner читает; эталоны не записывает.

Package scope имеет собственные Directory.Build/Packages/global.json/NuGet.Config,
source mapping и fresh cache. TrueMoon assets должны прийти из нового локального feed;
third-party packages могут использовать существующий NuGet cache/nuget.org. Фактический
analyzer dump должен содержать0Cobalt generator, а nupkg обязан содержать analyzer DLL
и buildTransitive targets. Native Skia в build/publish сверяется с renderer nupkg.
Loaded Hosting/Alloy/renderer/App DLL проверяются по fresh package hashes.

Read-only повторная проверка сохранённого результата в том же checkout:

```powershell
pwsh -NoProfile -File ManualTests/AlloyFactoryConsumer/VerifyIntegration.ps1 -RunDirectory TestResults/AlloyInfrastructure/<run>
pwsh -NoProfile -File ManualTests/AlloyFactoryConsumer/TestRunner.ps1
```

Verifier проверяет scope/полноту матрицы, reports, graph, payloads и все fingerprints;
он не rebuild/repack-ит и не обновляет hashes. Это проверка drift, не подписанная
аттестация. TestRunner выполняет directed guards без GPU: paths/quoting/UTF-8,
nonzero exit/logs, owned timeout tree, prerequisite failure, TRX discovery, incomplete
matrix, source/artifact drift, source dependency и отсутствующий archive payload.

`Integration/` — обычные tracked helpers и consumer template. Runner не читает scripts
из TestResults и не извлекает функции substring/dynamic-scriptblock из других runners.
Existing linked UI tests сохраняются; Hosting proof отдельно проверяет реальную
production assembly. Golden/RTE/engine viewport/performance/hardware acceptance и
full-solution regression в этот runner не входят; это самостоятельные проверки.

## Consumer scenarios

Windows x64/net10.0. Проверяет App/Hosting на Cobalt и Microsoft DI, OpenGL/Vulkan.
Сборка с ProjectReference; independent NuGet consumer использует тот же Program.cs
с собственными build/CPM settings и отключённым Cobalt analyzer.

```powershell
dotnet build ManualTests/AlloyFactoryConsumer/AlloyFactoryConsumer.csproj --configuration Debug
./ManualTests/AlloyFactoryConsumer/bin/Debug/net10.0/AlloyFactoryConsumer.exe cobalt opengl all TestResults/AlloyFactory/report.json
```

Аргументы: `cobalt|di`, `opengl|vulkan`, `factory|all`, report path. `all` выполняет
шесть App lifetimes: factory, прежний generic overload, throw/null/disposed/parented
root. Factory success также проверяет rejection already-attached root вторым окном
без повреждения первого. `factory` оставляет только новый overload без view service
registration; для проверки package API он запускается без Cobalt analyzer.

Source `legacy` намеренно сохраняет explicit view graph и analyzer; это отдельная
проверка совместимости generic overload, а не условие для нового factory overload.
Program проверяет root owner thread, dependency lifetime/disposal, options snapshot,
native target, Registry0 и наблюдаемость ошибок. Только returned detached root
переходит во владение окна. Borrowed/invalid roots сохраняют прежнего владельца.

`--validation` требует установленный Vulkan validation layer; runner включает уже
имеющийся SDK только через process-local VK_LAYER_PATH. Запуски bounded, окно и
GPU принадлежат этому consumer. Internal hook counters этот public consumer не читает;
их проверяет отдельный native AlloyVulkanTest. Это не hardware/performance acceptance.

Текущие результаты и scope — [UI STATUS](../../docs/alloy/STATUS.md),
подробности — [UI HISTORY](../../docs/alloy/HISTORY.md).

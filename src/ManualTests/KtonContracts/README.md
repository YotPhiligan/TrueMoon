# Kton PHYS1: независимый contract consumer

Из корня `src`, PowerShell 7 и установленный SDK из общего `global.json`:

```powershell
pwsh -NoProfile -File ManualTests/KtonContracts/Verify.ps1 -EvidenceDirectory TestResults/KtonPHYS1-local-001
```

Используйте новый каталог результата. Runner получает общий build profile через
`ManualTests/BuildProfile.ps1`; отдельные consumer props/global.json/CPM isolation,
NuGet config с единственным local feed и private package cache сохраняются в evidence.
Сначала source Debug consumer ссылается на реальный TrueMoon.Kton project; затем
создаётся свежий Release nupkg и независимый exact-version consumer запускается в
Debug и published Release. Restore без внешних sources, runtime DLL SHA сверяется
с package DLL; nuspec и restored graph должны содержать ровно один Kton package
без BEPU/Aether/Contracts/Tooling/UI/native dependencies.

Проверяются настоящие pure validation/identity/profile API, ошибки, geometry,
numeric/capacity/tick boundaries, command/query input и unmanaged value payloads.
Пример `IPhysicsWorld` полностью компилируется, но не запускается с fake world:
BEPU provider, mutation/simulation, live slot allocator, many-hit query ordering,
contact event reduction и cleanup реализуются PHYS2–3. Их runtime acceptance не
выводится из этих checks. App/IFactory/ECS проверки относятся к следующим этапам.

`verification.json` сохраняет hashes/profile/package/commands/report outcomes;
MSBuild вызовы имеют уникальные binlog, процессы — stdout/stderr, timeout/reap.
Исходники сверяются по SHA в конце. Версионируемая сводка и команды — в
[HISTORY](../../docs/physics/HISTORY.md), контракт — в [CONTRACTS](../../docs/physics/CONTRACTS.md).

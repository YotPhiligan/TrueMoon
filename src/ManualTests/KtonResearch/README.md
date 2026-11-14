# Kton: PHYS0 headless research

Изолированный low-level прототип BEPU для будущего TrueMoon.Kton. Он не входит в
solution и production dependency graph. Публичные API/provider, App/Cobalt/IFactory
и ECS bridge реализуются следующими этапами [плана](../../docs/physics/PLAN.md#phys0).
`ResearchWorldFactory` здесь — владелец исследовательских ресурсов, не новый
публичный factory contract. BEPU handles не выходят за границу прототипа.

## Запуск

Из корня `src`, Windows x64, PowerShell 7 и SDK из общего `global.json`:

```powershell
pwsh -NoProfile -File ManualTests/KtonResearch/Verify.ps1 -EvidenceDirectory TestResults/KtonPHYS0-local-001
```

Каталог результата должен быть новым. Скрипт скачивает ровно два официальных
nupkg; для повторения без скачивания можно передать `-PackageDirectory` с файлами
`bepuphysics.2.5.0-beta.33.nupkg` и `bepuutilities.2.5.0-beta.33.nupkg`.
В обоих случаях SHA256 проверяется до restore.

`Verify.ps1` получает снимок SDK/TFM/LangVersion/Features через общий
[BuildProfile.ps1](../BuildProfile.ps1), копирует исходники в consumer, отключает
наследование CPM и использует отдельные local feed, NuGet.Config и package cache.
TFM не закреплён вторым независимым значением в Probe.csproj. `packages.lock.json`
и `--locked-mode` фиксируют graph из BepuPhysics/BepuUtilities **2.5.0-beta.33**.
Непосредственный build исходного Probe.csproj вне runner не является штатным запуском.

Runner выполняет restore, source Debug build/run и framework-dependent Release
publish/run. Для MSBuild сохраняются уникальные binlog; для процессов — exit code,
stdout/stderr, timeout/reap. `verification.json` содержит package/DLL SHA, metadata,
build profile, hashes исходников и отчётов. `probe-Debug.json`/`probe-Release.json`
содержат каждый outcome, runtime/SIMD, timings и counters. Ненулевой exit означает
ошибку runner/проверки; поле `ProductionEligible` всегда false для PHYS0.

## Проверяемое поведение

- Два мира с разной gravity; dynamic/kinematic, impulse и force как impulse `F*dt`.
- Box/capsule на static triangle mesh, sleep/wake, общий shape и удаление тела.
- Nearest ray, convex sweep и overlap через stationary sweep, включая AABB false
  positive; query filter. Это не готовый many-hit API и не all-shapes overlap.
- Raw contact samples, sensor без solver response и collision filter. В callbacks
  нет IO/ECS/бросания исключений; bounded overflow обрабатывается owner после Step.
  Begin/Stay/End, generation-safe IDs и многопоточность остаются PHYS2–3.
- BallSocket двух тел, лимит 1024 bodies, invalid input/world preset, idempotent
  disposal и 64 цикла mesh/body/create/delete/dispose с нулевыми pool/owner counters.
- CCD и speculative contacts: sphere radius 0.1, 600 units/s, wall 0.04, dt 1/60,
  8 iterations × 4 solver substeps, один collision step, 61 tick наблюдения.

## Presets, допуски и пределы

Исследовательские единицы — метр, килограмм, секунда; базовый timestep 1/60 s,
один worker, contact spring 120 Hz / damping 1, friction 0.8, recovery velocity 2.
Для отдельного быстрого CCD fixture contact frequency **600 Hz**, sweep temporal
tolerance **1e-6 s**, position tolerance **0.005 m**. Эти числа не являются defaults
публичного API и не доказывают устойчивость произвольных сцен.

**Известный предел:** contact spring 120 Hz в том же CCD fixture допускает проход
сквозь стену, даже с Continuous. В raw отчётах сохраняется `SoftContinuousKnownLimitation`.
Режим 600 Hz проверяет capped margin 0.01 и unbounded margin отдельно; Passive с
capped margin — отрицательный контроль, а Passive с unbounded margin показывает
работу speculative contacts. После удара возможен небольшой отскок, поэтому
follow-up проверяет сторону стены, а не покой без gravity. Результаты и история
подбора записаны в [HISTORY](../../docs/physics/HISTORY.md#phys0-2026-11-12).

Численные допуски заданы непосредственно в Program.cs: free motion 1e-5–1e-3,
resting box 0.04, capsule 0.06, BallSocket anchor error 0.04, sweep 0.01.
Probe ограничивает bodies 1024, shapes 2048 и raw manifold samples 8192; запросы
возвращают ближайший hit. Предложенные в плане 4096 joints/commands и 256 query hits
пока не реализованы как admission limits: fixture создаёт один joint и не имеет
очереди команд. Capacity enforcement расширяется в PHYS1–3.

Временной diagnostic budget — p99 <=1 ms для конкретных 100 active spheres после
120 warmup и на 600 measured ticks; превышение отражается в JSON и не делает
запуск на другой машине ошибочным. Измеряется весь research Step, включая reset
samples и pool counters. Это исходный baseline, не BenchmarkDotNet, не сравнение
с Jolt и не FPS/budget игры. Cold create/first Step/dispose учитывается отдельно.
Лимит наблюдаемого high-water BEPU pools — 16 MiB на мир, проверяется как smoke
guard после Step. Он не ограничивает все managed allocations и не является hard
RAM cap. Disposal проверяет реальные owner/pool counters; доказательства отсутствия
всех process/GC leaks этот probe не даёт.

Проверенный профиль: Windows x64/.NET 11, JIT, один worker. Linux/macOS/ARM64,
mobile/WebAssembly/consoles, AOT/trimming, parallel callbacks и ECS не проверены.
Версионированная сводка: [PHYS0_RESULTS.json](../../docs/physics/PHYS0_RESULTS.json).

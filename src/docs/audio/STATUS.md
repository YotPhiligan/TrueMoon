# Kul / Audio: состояние

**2026-11-15 — optional Aether ECS/session adapter ARCH1h.2a реализован и проверен.**
[API/границы](ECS_BRIDGE.md), [engine задача](../engine/PLAN.md#arch1h2a-kul-session),
[полная история/команды](../engine/HISTORY.md#kul-ecs-2026-11-15), [manifest/SHA](KUL_ECS_RESULTS.json).

`TrueMoon.Aether.Kul` — neutral SDK без Runtime/native; `TrueMoon.Aether.Kul.Runtime`
связывает captured files с caller-owned AudioSession. Sound clips/fixed music voices
готовятся до publication; actual Play/Pause/Stop и silent paused Step контролируются
общей session. Пулы удерживаются после EOF, commands monotonic, source gain/loop/seek
и entity generation/rebind имеют явные правила. Cleanup: native voices/streams → pins → World.

Самостоятельный Kul получил additive StartPaused/RetainAtEnd options и mutable
AudioPlayback.Loop. Старые defaults/collect-on-admission сохранены. Backend/native DLL
`TrueMoon.MiniAudioEx4.0.1-truemoon.1` прежние; native rebuild не выполнялся.

Новые проверки:103adapter tests и27Kul tests каждый Debug/Release, failed/skipped0;
103cases найдены через точный solution.104common+68Kton regression каждый D/R.
Source Debug/published Release/fresh NuGet Debug/published Release прошли каждый
59offline checks и отдельно43muted actual WASAPI checks, оба DI; final sessions/clips/
voices/streams/resident bytes0, files unlocked.3package metadata/payload checks;
exact managed/native SHA и closed package-only graph проверены. Solution D/R0errors;
non-incremental warning backlog1570/config отдельно от новых пакетов.

Предыдущие AUD1/rename/precommit результаты сохранены в
[истории](HISTORY.md#audio-status-before-kul-ecs-2026-11-15) со своими датами и manifests.
Это не новый full framework regression, слушание или повтор original native RED/GREEN.

Следующий audio этап — AUD2: async admission/preparation, device-loss/long workload и
hard peak/RT budgets по отдельному запросу. AUD3 — другие OS, затем spatial.
Editor import/Nom, automatic GameBuildArtifact/ALC extension и native audio-state
migration отдельно. OGG length unavailable и временная silence при seek сохраняются.

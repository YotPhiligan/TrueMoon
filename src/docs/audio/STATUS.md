# Kul / Audio: состояние

**2026-11-15 — DEP1: актуальность native fork и новые зависимости проверены.**
Latest upstream managed4.0.1 и native HEAD9f85b56157027cc57c7cd0a13fea7fee3f1e167e
совпали с Source.lock.json. TrueMoon.MiniAudioEx4.0.1-truemoon.1/patch/binary сохранены;
новая аудио-native пересборка не требовалась. Skia native обновляется отдельно у Alloy.

Полные Kul27 и Aether.Kul103passed каждый Debug/Release,0failed/0skipped.
Новый source Debug/published Release/fresh NuGet Debug/published Release runner:
4reports×43checks, оба DI, actual WASAPI с mute только этого stream. Exact native
SHA/closed graph проверены; sessions/clips/voices/streams/resident bytes0, files unlocked.
Original native leak RED/GREEN/rebuild и физическое прослушивание не повторялись.

[Единственная dependency задача](../core/PLAN.md#dep1-dependency-refresh),
[история/команды](../core/HISTORY.md#dep1-2026-11-15), [manifest/SHA](../core/DEPENDENCY_REFRESH_20261115.json).
[ARCH1h.2a API/ownership](ECS_BRIDGE.md) и [его функциональная задача](../engine/PLAN.md#arch1h2a-kul-session)
сохранены. [Предыдущий STATUS](HISTORY.md#audio-status-before-dep1-2026-11-15).

Далее AUD2 async/admission/device-loss/long workload/RT/peak budgets; AUD3 other OS,
затем spatial. Editor import/Nom/automatic game ABI/ALC extension и native-state
migration отдельно. OGG length unavailable/seek refill silence сохраняются.

# TrueMoon: общий статус

**2026-11-16 — ENG7b.3: Editor/Player launch acceptance завершена.**
[Задача](engine/PLAN.md#eng7b3-launch-acceptance), [матрица/миграция](engine/LAUNCH_ACCEPTANCE.md),
[команды/границы](engine/HISTORY.md#eng7b3-2026-11-16), [manifest/SHA](engine/ENG7B3_BASELINE.json).
Версия — `1.2611.16.1-alpha`; SDK/TFM/dependencies/native ABI прежние.

Исправлен повторный mesh Play после Stop; сохранились stale generation fences.
Combined child использует standalone Runtime graph.34native profiles на обоих DI,
54game cycles с crash/recovery,36identity rejections,60CLI cases и12окон примеров
прошли. Player отдельно проверен без SDK. Документы/history/selection сохраняются,
Editor не загружает игру; normal validation0/0 и parent reap/resources проверены.

19package archives одной версии и7fresh closed-feed graphs проверены. Полные
16suites по9111passed каждый Debug/Release,0failed/0skipped; non-incremental
solution builds0errors. Это launch/delivery scope Windows x64, не all27 architecture
acceptance и не общий release. [Модульный статус](engine/STATUS.md),
[общий build/version статус](core/STATUS.md), [прежний STATUS](HISTORY.md#common-status-before-eng7b3-2026-11-16).

Следующий functional scope выбирается отдельно: PHYS6, import g.1→g.2, ED4i/ED5,
AUD2–3. Physical/DPI/K7/device-loss/VRAM/soak/other OS, historical drain cause и
общая release matrix открыты. Requests и consumer-код игры/Studio не менялись.

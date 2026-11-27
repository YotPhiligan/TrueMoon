# TrueMoon.Kton: состояние и следующий шаг

<a id="phys6-aether-auto-step-2026-11-27"></a>
**2026-11-27 — PHYS6: автошаг в Aether проверен на исходниках, `1.2611.27.11-alpha`.**
Release/net11.0: 128/128. На обоих профилях `CharacterV2` с opt-in поднимается
на 0,25 м и публикует позу и верхнюю опору в одном тике. V1, v2 без opt-in
и уступ 0,30 м блокируют подъём. In-process Pause/Step/Replace на Cobalt и
Microsoft DI восстанавливает опору в новом мире. Replace освобождает старый мир, Stop — новый.
Пакетная приёмка, native Player и переход движения Tales остаются отдельными.
[Прогон и границы](HISTORY.md#phys6-aether-auto-step-2026-11-27) ·
[План](PLAN.md#phys6-aether-auto-step).

<a id="phys6-headless-corpus-2026-11-27"></a>
**2026-11-27 — PHYS6: расширенный headless-корпус проверен.** Source
Release/net11.0: 92/92 provider-теста, из них 24 новых (по 12 на `Baseline60`
и `FastLinear60`). Проверены габариты проёмов, проход под настилом, стоп и
отход от тонкой стены, ступени со скосом в обе стороны и падение с открытого
края. Четыре пары повторов смешанного маршрута совпали точно по всем 166 кадрам.
Эта проверка ограничена provider-уровнем.
[Прогон и ограничения](HISTORY.md#phys6-headless-corpus-2026-11-27) ·
[План](PLAN.md#phys6-headless-corpus).

<a id="phys6-auto-step-2026-11-27"></a>
**2026-11-27 — PHYS6: опциональный шаг, `1.2611.27.9-alpha`.** На обоих
профилях капсула поднимается на 0,25 м с реальной опорой в том же тике;
уступ 0,30 м, короткая проступь и недостаточный зазор отклоняются.
Release/net11.0: 90/90 фильтрованных тестов, включая 26 новых сценарных
случаев и валидацию настроек. В headless-прогоне пройдены четыре ступени
по порядку и получена опора комнаты; 22 точных повтора в одном процессе.
Спуск и падение с края не изменились. Default, в том числе в Aether, выключен.
[Команды и результаты](HISTORY.md#phys6-auto-step-2026-11-27) ·
[Границы](PLAN.md#phys6-auto-step).

<a id="phys6-tales-route-2026-11-27"></a>
**2026-11-27 — PHYS6: сценарный baseline без окна.** Source Release, Windows x64,
SDK 11.0.100, BEPU 2.5.0-beta.33. Проверены 11 сценариев и 22 точных повтора
Kton внутри одного процесса (`Baseline60` / `FastLinear60`). Ровный пол, скос
в обе стороны и осевой проход двери достигли цели. Оба профиля остановились
перед ступенью 0,25 м и уступом 0,30 м, спустились на улицу с 18/21 тиками без
опоры, а на задней кромке пересекли край на тике 32 и потеряли опору на 39-м.
Production-код, версии и готовность auto-stairs не менялись; PHYS5/Aether,
поставку и замену игрового правила не проверяли.
[Следующий сценарий](PLAN.md#phys6-tales-route-baseline) ·
[Локальный отчёт и трассы](../../../../TrueMoon.Tales.Game/src/.local/prototype-toolchain/kton-route-20261127-c872d78b-fef77682/summary.md).

**2026-11-17 — PHYS8.4: визуальный debug-consumer реализован, автоматическая приёмка прошла.**
[Задача](PLAN.md#phys84), [уровень/скриншот/границы](VISUAL_PLAYGROUND.md),
[команды](HISTORY.md#phys84-2026-11-17), [manifest/SHA](PHYS84_RESULTS.json).

ManualTests/KtonVisual использует настоящую PrimitiveGameSession, SceneWorld/scheduler
и Kton. Пол, склон, движущаяся платформа, прыжок, низкий потолок и dynamic cubes;
отображение через Vulkan/OpenGL surface. Капсула показана ступенчатым proxy.
Consumer интерполирует копии поз; Physics World и metadata tick сохраняются.
Pause показывает завершённую позу, Step выполняет один tick, Stop/Reset закрывают
старые tickets и освобождают World/surface. Есть helper Play.ps1 с WASD/Space и командами.

Фактически прошли 16 профилей: source Debug/published Release/fresh NuGet Debug/
published Release, каждый CPU/native на Cobalt/MS DI. 96 CPU-групп и 96 PNG;
validation 0/0, финальные GPU owners/handles/UI sessions/physics worlds/pools — 0.
19 собственных архивов одной версии, isolated graph/cache и loaded DLL SHA проверены;
extended Skia/font snapshots совпадают в четырёх outputs. Full unit/solution suites
в этом срезе не повторялись. [Предыдущий статус](HISTORY.md#physics-status-before-phys84-2026-11-17).

Ручной интерактивный Win32-прогон не подтверждён: Computer Use не смог надёжно
активировать/наблюдать тестовое окно; отдельный пробный процесс завершён адресно.
Это не failure scripted-матрицы и не proof штатного закрытия interactive loop.
Общая Version 1.2611.16.4-alpha сохранена: production/native/dependencies прежние.
Далее — explicit 10k-body/worker profiles и raw callbacks/stalls; ручной input/close,
точная collider mesh, DPI/device loss/other OS/AOT и общая PHYS8 readiness открыты.

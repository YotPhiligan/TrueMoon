# Core: состояние и точка продолжения

На `1.2611.26.19-alpha` с обновлёнными AssimpNetter и RichTextKit прошли
restore решения и 44 теста RichText. После исправления атомарной замены
head прошли все 10 профилей ledger.
[Отчёты](../engine/HISTORY.md#model-ledger-atomic-replace-2026-11-26).

**2026-11-21 — CORE6a: изоляция событий App.** В `1.2611.21.11-alpha`
настройка `AppLocalSourcesOnly` привязывает штатные источники DI и фабрики
к pipeline своего App. По умолчанию действует подписка на весь процесс.
Тесты Core прошли по 666 в Debug и Release на SDK 11.0.100. Проверены
Cobalt, Microsoft DI, конфигурация, фильтры и завершение одного из двух App.
Четыре локальных пакета прошли 80 сценариев потребителей, три примера из README
и отрицательную проверку без генератора.
[Контракт](DIAGNOSTICS.md#события-и-фильтры) ·
[Проверки](HISTORY.md#core6-app-local-sources-2026-11-21).

В CORE6 остаются диагностика до создания resolver, агрегаты операций CORE6d,
генерация событий CORE6e и общая квота артефактов.
[План](PLAN.md#core6-app-local-sources).

## Предыдущая общая проверка

**2026-11-16 — ENG7b.3: новая согласованная версия и общая регрессия.**
Детальная задача у [engine](../engine/PLAN.md#eng7b3-launch-acceptance),
[результаты/границы](../engine/HISTORY.md#eng7b3-2026-11-16),
[manifest/SHA](../engine/ENG7B3_BASELINE.json). `Version=1.2611.16.1-alpha` впервые
применяет [VER1](PLAN.md#ver1-agent-versioning); SDK11/net11/runtime-async прежние.

Полные16suites по9111passed каждый Debug/Release,0failed/0skipped; non-incremental
solution builds0errors.19точных launch-package archives и7closed-feed consumers,
34native profiles/12окон/60CLI cases прошли. Это scope Editor/Player launch, не
новая all27 architecture acceptance. Core/Cobalt/Enerit/Mithril source/API,
dependencies и native binaries не менялись. DEP1 и VER1 сохраняют прежние даты:
[предыдущий STATUS](HISTORY.md#core-status-before-eng7b3-2026-11-16).

Следующий этап — выбранная пользователем функциональная задача engine/модуля.
CORE6, historical drain cause и physical/release/device-loss/other OS остаются
отдельными. Скрипт автоматического повышения версии/CI ещё не реализован.

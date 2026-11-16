# PHYS8.1 — первый Windows x64 baseline

Baseline ниже сохранён на1.2611.16.2-alpha. Следующий PHYS8.2 завершён:
[источники allocations/оптимизация/сравнение](ALLOCATION_OPTIMIZATION.md),
версия1.2611.16.3-alpha. Исторические числа и manifests PHYS8.1 не переписаны.

Измерен 16.11.2026 на версии `1.2611.16.2-alpha`. [Задача](PLAN.md#phys81),
[команды/границы](HISTORY.md#phys81-2026-11-16), [report/package/source SHA и числа](PHYS81_RESULTS.json).
Это завершённый первый срез PHYS8; весь PHYS8 пока не завершён.

## Профиль и метод

AMD Ryzen9 9900X,24logical processors; Windows10.0.26340 x64, .NET11.0.0/SDK11.0.100,
runtime-async=on, workstation GC, Vector<float>8lanes, AVX2/AVX512F доступны.
BEPU/BepuUtilities2.5.0-beta.33, Baseline60,60Hz,1worker,8iterations/4substeps.
Наличие SIMD capability не означает, что все участки используют AVX512.

Выполняется отдельный [измерительный consumer](../../ManualTests/KtonPerformance/README.md)
через публичный App/DI и caller-/scene-owned world. Новый production API и
инструментирование backend не добавлены. Все93source SHA предыдущей PHYS6
приёмки сохранились. Изменены только ManualTests и документация; общая Version
не повышалась, новая стадия beta/rc не выводится из passing результатов.

Матрица:100/1000active boxes,100/1000sleeping boxes,1/32/256walking capsules;
standalone и PrimitiveGameSession.Tick; Cobalt и Microsoft DI. В каждом Release
профиле28workloads, в каждом Debug smoke12малых workloads. Всего4профиля/80workloads,
48000измеренных native Steps. У каждого fixture свой App/world и120warmup ticks,
затем600измерений. Setup/input/валидация/сортировка/JSON/cleanup за пределами окна.
Принудительных GC нет. Percentiles — nearest rank; оба таймера имеют небольшой
невычтенный observer cost. Fixed warmup и системная нагрузка могут влиять на p99.

Активные rigid bodies движутся1м/с без гравитации и контактов, sleeping partition
явно усыплена. Walking capsules движутся2м/с по одному flat floor с гравитацией;
подтверждены поддержка и published contact events. ECS-сущности не имеют drawable
компонентов; остаётся штатный bookkeeping пустого render snapshot. Это не workload
для renderer/stacking/CCD. Default body/shape/physics limits и worker count прежние.
Для256controllers **явно** выбран уже разрешённый bridge projection capacity256
вместо default64; остальные ECS fixtures сохраняют64. Значение записано в каждом
результате; production defaults и ceilings не менялись.

Source Debug и fresh NuGet Debug — только smoke. Source published Release и fresh
published Release — полный baseline. Пакеты14TrueMoon одной persisted версии,
точный closed feed, package-only graph, fresh cache/output DLL SHA проверены.
Debug и Release числа не смешиваются; различия между профилями не считаются
измеренным ускорением/регрессией одного варианта.

## Release наблюдения

Диапазон ниже — min/max четырёх наблюдений: source/fresh Release × два DI.
Это не confidence interval. Step — полный публичный `IPhysicsWorld.Step`, включая
admission/validation/contacts/join; Full — весь синхронный session tick. Сюда не
входят GPU и ожидание realtime таймера. Перцентили участков нельзя вычитать друг из
друга для вычисления «p99 overhead».

| Workload | Путь | Step p99, мс | Full p99, мс | All-thread allocations, Б/tick |
| --- | --- | --- | --- | --- |
| 100active | standalone | 0.0144–0.0584 | 0.0145–0.0599 | 0–20.9 |
| 1000active | standalone | 0.0570–0.3660 | 0.0571–0.3662 | 0 |
| 100sleeping | standalone | 0.0053–0.0338 | 0.0053–0.0338 | 0 |
| 1000sleeping | standalone | 0.0177–0.1114 | 0.0178–0.1116 | 0 |
| 1controller | standalone | 0.0114–0.0725 | 0.0115–0.0727 | 0 |
| 32controllers | standalone | 0.0390–0.2482 | 0.0390–0.2486 | 56 |
| 256controllers | standalone | 0.2696–1.7557 | 0.2698–1.7561 | 56.0–56.1 |
| 100active | ECS | 0.0173–0.0243 | 0.0841–0.5616 | 45042–45550 |
| 1000active | ECS | 0.0754–0.0796 | 0.8970–1.1785 | 444653–444764 |
| 100sleeping | ECS | 0.0071–0.0134 | 0.0771–0.0864 | 45041–45042 |
| 1000sleeping | ECS | 0.0389–0.0442 | 0.8099–1.0057 | 444654 |
| 1controller | ECS | 0.0105–0.0214 | 0.0134–0.0341 | 2960 |
| 32controllers | ECS | 0.0409–0.0539 | 0.0869–0.1432 | 40865–40866 |
| 256controllers | ECS | 0.2873–0.3124 | 0.6933–1.0137 | 334267 |

Все p50/p95/p99/max/mean, обе allocation величины, GC counts и отдельные56Release
наблюдений сохраняются в manifest. GC.GetTotalAllocatedBytes охватывает весь процесс,
включая runtime/другие потоки; primary-thread counter записан отдельно. Approximate
managed heap before/after не равен retained size конкретного мира и может уменьшаться
при GC. BEPU main/dispatcher pool counter — sampled high-water, максимум5160960Б;
это не полный native/process RSS. После cleanup у всех80fixtures0published worlds,
0active owners,0remaining pool bytes. Максимум single scene/world teardown4.229мс
по всем профилям и3.0446мс только Release;
время App Stop туда не входит.

Diagnostic budget Step p99=1мс остаётся наблюдением: превышение на256controllers
в двух standalone наблюдениях Cobalt записано, budget не увеличен и не стал hardware-independent
failure gate. Callback totals здесь означают published gameplay events; raw narrowphase
invocation count ещё не инструментирован.

Порядок обхода фиксирован: Cobalt перед Microsoft. Возможный bias tiered JIT/нагрева
не устранялся; наблюдаемый разрыв256-controller p99 нельзя приписывать DI overhead.
Перед сравнительным performance target нужна стабилизация/повторение измерений.

## Вывод и следующий срез

Полный ECS-путь выделяет около445КБ/tick для1000тел, включая sleeping partition,
то есть около26.7МБ/с при60Hz. Это воспроизведённое allocation наблюдение всего
session tick. Точный вклад scheduler, transform propagation и physics bridge
нужно разделить; этот macrobaseline не устанавливает виновника методом исключения.
Следующий срез PHYS8.2 — allocation attribution и адресное уменьшение повторной
подготовки/reconciliation без ослабления полного preflight, phase-boundary lifecycle,
identity, ownership и контактов. После изменения — те же fixtures и повторная
функциональная регрессия. Для32/256standalone controllers также наблюдаются56Б/tick;
это отдельная меньшая цель для исследования.

PHYS8 остаётся открыт: explicit10000-body/2/4/8worker presets требуют отдельного
решения по limits/quality; ragdoll optional; raw callbacks/stalls, visual pose/
interpolation/Stop, Linux/macOS/ARM64/AOT и общая release matrix не подтверждены.
Текущий baseline не обещает hardware SLA или универсальной CCD/stacking гарантии.

## Использование, обновление и ограничения

Standalone App/DI — [PHYS4](APP.md), scene subsystem — [PHYS5](ECS_BRIDGE.md),
controller authoring/input/state — [PHYS6](CHARACTERS.md). Лимиты —
[neutral contracts](CONTRACTS.md), queries/events — [контракт PHYS3](QUERIES_CONTACTS.md).
Consumer обновляет согласованный набор пакетов своего graph; номера независимых
Studio/игры не синхронизируются автоматически. Этот срез не меняет API/ABI/profile.
При смене версии backend/solver/limits старый baseline не переносится без повторения.
Native world checkpoint/restore/replay и native-state mod migration не поддержаны;
пересоздание scene/world получает новые runtime identities. Auto-stairs, crouch,
root motion, air steering и ragdoll не добавляются этой performance работой.

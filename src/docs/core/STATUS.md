# Core: состояние и точка продолжения

Дата: **2026-10-09 — чтение исходников и создание плана**. [План](PLAN.md), [общий статус](../STATUS.md). Build/tests этого модуля в рамках этой работы не запускались.

## Реализация и проверки

| Область | Наличие реализации | Свидетельство |
| --- | --- | --- |
| Bootstrap/lifecycle | App/AppBuilder/DefaultApp/DefaultAppLifetime, start/stop/disposal | [App](../../TrueMoon/App.cs), [DefaultApp](../../TrueMoon/DefaultApp.cs); только чтение |
| Configuration/diagnostics | Configuration providers, builder, events/observers/subscriptions | Каталоги TrueMoon/Configuration и Diagnostics; успешный runtime проход здесь не подтверждён |
| Общие contracts/utilities | App/service/configuration/diagnostics interfaces, buffers/pools/task scheduler | TrueMoon.Contracts и TrueMoon.Core; только наличие исходников |
| Проверки приложения | Есть TrueMoon.Tests и AppTests | [AppTests](../../TrueMoon.Tests/AppTests.cs); новые discovery/results отсутствуют |

Историческая запись2026-09-27 об AppCreate failure перенесена из UI журнала как задача CORE0. Она не является current failing результатом. Архив с исходной записью — [UI HISTORY](../alloy/HISTORY.md#выполненная-проверка-2026-09-27); bootstrap/DI investigation относится к Core/Cobalt, не к UI alpha.

## Блокеры и ограничения

Актуальный baseline Core не получен; повторный Start/Stop, startup rollback/cancellation, configuration precedence и disposal требуют проверки и явного contract review. Конкретные дефекты не объявлены установленными чтением документации.

## Следующий конкретный шаг

CORE0: проверить фактическую discovery/результаты TrueMoon.Tests на текущем SDK, отдельно воспроизвести AppCreate; зафиксировать provider/configuration и исходную причину, затем выбрать CORE1/COB совместную задачу.

## Последнее изменение документации

2026-10-09 создан локальный PLAN/STATUS по общему формату. Проверки этой работы: чтение source/project files и проверка документационных ссылок/структуры. Длинной собственной истории запусков пока нет; HISTORY будет создан при накоплении фактических записей.

# Mithril: состояние и точка продолжения

Дата: **2026-10-09 — чтение исходников и создание плана**. [План](PLAN.md), [общий статус](../STATUS.md). Новые build/discovery/process checks не запускались.

Дополнение **2026-10-10:** полный non-incremental solution build после Core/DI fixes exit0; Mithril runtime и ManualTests consumer собраны. Solution test и отдельная list-tests discovery подтвердили **0 discovered / TRX total0** в Mithril.Tests. Windows/Debug/net10.0/SDK10.0.401, xUnit2.9.3/VSTest. Это отсутствие executable tests, не passing lifecycle proof. [Команды и общие артефакты](../core/HISTORY.md#исправление-проблем-ядра--2026-10-10). Production/process checks этим дополнением не менялись.

## Реализация и проверки

| Область | Наличие реализации | Свидетельство |
| --- | --- | --- |
| Units/policies | UnitsController, UnitConfiguration, startup/hosting/lifetime/restart policy types | [UnitsController](../../TrueMoon.Mithril/UnitsController.cs), [Configuration](../../TrueMoon.Mithril/UnitConfiguration.cs); чтение |
| Process handles | MainProcess/ChildProcess/External handles и parent events handler | Каталог TrueMoon.Mithril/Units; successful lifecycle proof не получен |
| Test project | Mithril.Tests входит в solution и ссылается на runtime | [Project](../../TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj); среди source .cs вне bin/obj найден только Usings.cs. Текущая discovery2026-10-10:0 |
| Manual consumer | Есть ManualTests/MithrilTest | [Project](../../ManualTests/MithrilTest/MithrilTest.csproj); успешный process run не подтверждён этой работой |

Историческая запись2026-09-27 о zero discovered Mithril tests находится в [архиве](../alloy/HISTORY.md#выполненная-проверка-2026-09-27). Она не заменяет текущую discovery и не означает successful process checks. Для MIT0 важны фактические scenarios, а не только наличие test csproj.

## Блокеры и ограничения

Нет текущей evidence startup/restart/shutdown/parent exit/cleanup. Реально поддержанные OS/hosting policies и владение external processes требуют явной фиксации. Наличие policy enum не означает завершённую проверку соответствующего режима.

## Следующий конкретный шаг

MIT0: разобрать один bounded own-child ManualTests scenario и добавить executable process checks. Discovery0 уже подтверждена. Сохранить запуск/exit/cleanup и выбрать минимальный MIT1/MIT2 contract. Проверки чужих процессов и глобальные process/settings изменения не требуются.

## Последнее изменение документации

2026-10-09 создан PLAN/STATUS.2026-10-10 добавлены фактические solution build/discovery0 сведения; passing counts других проектов не перенесены на Mithril. HISTORY создаётся при накоплении собственных process проверок.

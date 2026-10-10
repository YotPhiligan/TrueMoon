# Enerit: состояние и точка продолжения

Дата: **2026-10-10 — совместимость с pools/scheduler Core и завершение pipes**. [План](PLAN.md), [история](HISTORY.md#совместимость-pools-и-завершение-pipes--2026-10-10), [общий статус](../STATUS.md).

## Реализация и проверки

| Область | Реализация | Текущий запуск |
| --- | --- | --- |
| Runtime / ENE0 | Invocation, factories/handlers, serializer и оба transports | Runtime30passed/0failed/0skipped, discovery30; прежние10 плюс20 новых UtilityLifetimeTests |
| Pools/scheduler ownership | Tracked deserialized bytes, cancellable connects, owned worker shutdown, async handler drain и buffers в finally | Serialization/connection/client/server disposal tests Passed |
| Pipes response lifecycle | Pending requests завершаются при dispose/disconnect/cancel; empty success возвращает результат, truncated/invalid response даёт ошибку | Direct raw-pipe regression cases Passed; pending map cleanup и scheduler Completion проверены |
| Framing | Полное чтение fragmented headers и observable EOF вместо spin | Request/response header и payload EOF tests Passed |
| Generator / ENE1 | Declared/inherited mapping, equivalent signature dedup и dispatch через declaring interface | Generator9passed/0failed/0skipped, discovery9; consumer compilation/emit cases прошли повторно |
| Memory-mapped | Implementation существует; scenarios закомментированы | Discovery0 этого transport, readiness не подтверждена |

Windows, Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. Итоговый полный solution build/test exit0, TRX counts сверены. Framing/method IDs не изменены этой работой. Предыдущие main runtime10/10 и generator9/9 сохранены в HISTORY как отдельный запуск.

## Ограничения

- Новые проверки — in-process Windows pipes. Own-process transport, package consumer и Release не запускались. Handler, игнорирующий cancellation, может удерживать synchronous Dispose.
- Общий serialization/null/collection/unsupported payload contract и payload-size policy требуют ENE1. Memory-mapped cleanup/fault matrix остаётся отдельно неподтверждённой.
- Inherited method order зависит от Roslyn symbols; межверсионная mapping compatibility не установлена. Предел256 byte codes, разные return types одинаковых signatures и generic/static members не исправлялись.
- Старые PipesTests имеют слабые assertions, четыре исходных generator tests не проверяют output compilation; их passing не подменяет новые поведенческие20/runtime и5/consumer cases.
- Shared XML/nullability/RS1041 warnings остаются; NU1506/NU1507 устранены общими build settings. UI checks не подтверждают transport readiness.

## Следующий конкретный шаг

ENE1: зафиксировать supported payload/null/collections и size/error contract. ENE2: bounded own-process pipes scenario с cancellation/disconnect/error/cleanup. Сохранить pending-response и async-handler regression cases как критерий shutdown.

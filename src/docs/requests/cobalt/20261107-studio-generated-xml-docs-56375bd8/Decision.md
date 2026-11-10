# Решение владельца Cobalt — 2026-11-09

- Состояние: `completed` — реализация и local package proof; Studio acceptance отдельно.
- Owner: Codex `/root`, `main`, рабочая копия TrueMoon.
- Задача: [COB6e](../../../cobalt/PLAN.md#cob6e-generator-consumer-fixes).
- Основание: явный запрос пользователя реализовать две адресные правки генератора.
- Scope: XML summary публичной служебной регистрации без изменения видимости и без NoWarn потребителя.
- Реализация: [CobaltGenerator](../../../../TrueMoon.Cobalt.Generator/CobaltGenerator.cs), [регрессии](../../../../TrueMoon.Cobalt.Generator.Tests/GeneratedSourceTests.cs).
- Проверки09ноября: generator96/runtime179 каждый Debug/Release; независимый consumer XML docs + CS1591-as-error — D/R0warnings/0errors, initializer/identity/disposal11checks. Negative controls сохранили собственный CS1591 consumer. [Команды и ограничения](../../../cobalt/HISTORY.md#cob6e-generator-2026-11-09).
- Локально упакован TrueMoon.Cobalt0.2.5.2-alpha с matching bundled generator; external publish не выполнялся.
- Передача потребителю: после получения обновлённого пакета Studio отдельно повторяет свой Debug/Release XML build с CS1591 в WarningsAsErrors и graph/lifecycle acceptance. Результаты этой проверки пока отсутствуют.

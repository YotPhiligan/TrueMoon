# Решение владельца Cobalt — 2026-11-09

- Состояние: `completed` — реализация и local package proof; Studio acceptance отдельно.
- Owner: Codex `/root`, `main`, рабочая копия TrueMoon.
- Задача: [COB6e](../../../cobalt/PLAN.md#cob6e-generator-consumer-fixes).
- Основание: явный запрос пользователя реализовать две адресные правки генератора.
- Scope: bounded stable hint names; generated class identity и runtime contracts сохраняются.
- Реализация: [CobaltGenerator](../../../../TrueMoon.Cobalt.Generator/CobaltGenerator.cs), [регрессии](../../../../TrueMoon.Cobalt.Generator.Tests/GeneratedSourceTests.cs).
- Проверки09ноября: generator96/runtime179 каждый Debug/Release; независимый consumer с emit прошёл D/R,9sources/11checks,0warnings/0errors. [Команды и ограничения](../../../cobalt/HISTORY.md#cob6e-generator-2026-11-09).
- Локально упакован TrueMoon.Cobalt0.2.5.2-alpha с matching bundled generator; external publish не выполнялся.
- Передача потребителю: после получения обновлённого пакета Studio отдельно повторяет свой Debug/Release emit и graph/lifecycle acceptance. Результаты этой проверки пока отсутствуют.

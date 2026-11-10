# Cobalt generated sources: независимый package consumer

Проверка COB6e из [плана Cobalt](../../docs/cobalt/PLAN.md#cob6e-generator-consumer-fixes).
Windows x64, .NET SDK 10.0.403. Перед запуском нужны восстановленные зависимости
исходных TrueMoon.Contracts/Cobalt/Generator (обычный запуск тестов Cobalt выполняет restore).

Из корня `src`:

```powershell
./ManualTests/CobaltGeneratorConsumer/Run.ps1
```

Скрипт пакует Contracts/Cobalt текущей общей версии в новый локальный feed,
копирует consumer в `TestResults/CobaltGeneratorFixes/packages-<UTC>` и восстанавливает
только эти два пакета в изолированный cache. Подключён только bundled analyzer;
его путь и SHA сравниваются с текущим Release build генератора.

Debug/Release собираются с emit generated files, XML docs и CS1591 в
WarningsAsErrors. Проверяются девять файлов, ограничение128символов/ASCII,
различимость и стабильность имён между конфигурациями. Граф включает длинные
похожие имена, nested/Unicode и open/closed generic типы;11runtime checks проверяют
initializer, singleton/transient/Composite identity и owned disposal.

После положительных запусков добавляется undocumented public class только в
scratch-копию: обе сборки обязаны завершиться ошибкой CS1591 именно в consumer.
`summary.json`, логи и уникальные binlogs сохраняются в каталоге запуска; это
ignored локальные артефакты без гарантии retention. Постоянная запись результатов
находится в [HISTORY](../../docs/cobalt/HISTORY.md#cob6e-generator-2026-11-09).
Studio и external package publish этот consumer не проверяет.

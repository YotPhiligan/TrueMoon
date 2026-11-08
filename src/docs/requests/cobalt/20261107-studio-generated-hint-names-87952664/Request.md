# Короткие имена generated sources для Windows

- ID: cobalt-studio-generated-hint-names-87952664
- Дата UTC: 2026-11-07T16:02:34.963286+00:00
- Consumer: TrueMoon.Tales.Studio, net10.0, Windows x64, SDK 10.0.403.
- Пакет: TrueMoon.Cobalt 0.2.5.1-alpha, bundled generator SHA256
  `62eb4fa4ed44a69c68e5a204cae07e80d72ee6f21db3fd8de509406b31f9cd5a`.
- Модуль: Cobalt.Generator. Capability: `generated-source-hint-name-length`.
- Источник: [StudioServices.cs](../../../../../../TrueMoon.Tales.Studio/src/TrueMoon.Tales.Studio/Infrastructure/StudioServices.cs).
- Состояние: входящая заявка потребителя; Claim/Decision и реализация — владельцу модуля.

При переходе Studio на Cobalt обычные Debug/Release работают, 71 тест прошёл.
Диагностическая сборка с `-p:EmitCompilerGeneratedFiles=true` возвращает CS0016:
компонент имени generated-файла превышает лимит Windows. Для пяти resolver-файлов
из CobaltResolversRegistration получены длины 252/372/275/265/208 символов.
Файл с длиной 252 записан, три имени длиннее 255 — нет. Это не только длина
пути: сокращение корня не исправит 372-символьный компонент имени.

Воспроизведение из Studio/src после restore:

```powershell
dotnet test TrueMoon.Tales.Studio.slnx --no-restore --artifacts-path .local/studio-build --configuration Debug -p:EmitCompilerGeneratedFiles=true '-bl:.local/cobalt-emit-{}.binlog'
```

Минимальная задействованная регистрация:

```csharp
services.Singleton<IFactory<StudioTitleBar, StudioTitleBarArguments>, StudioTitleBarFactory>();
```

Типы находятся в `TrueMoon.Tales.Studio.Presentation.Shell`. Двухаргументная
IFactory и имена сервисов — обычный API фреймворка и полные namespace потребителя.
Текущий CobaltGenerator строит имя из экранированных полных имён service/implementation
и передаёт `name + ".g.cs"` в AddSource. Текущие исходники содержат тот же алгоритм;
подходящей активной заявки/задачи по capability при проверке не найдено.

Ожидание: diagnostic emit поддерживает такие регистрации без переименования
типов Studio. Вариант — ограниченный читаемый префикс и стабильный hash полного
ключа только для hint name; имя класса resolver менять не обязательно. Выбор
формата и дедупликации принадлежит владельцу Cobalt.

Критерии: compile с emit в Debug/Release на Windows; имена компонентов не длиннее
255 (желателен меньший запас); стабильность между сборками; различение похожих
длинных/Unicode/nested/generic типов; обычное разрешение графа и singleton/lifecycle
сохраняются. Runtime contracts и владение ресурсами не меняются.

Временный путь Studio: оставить штатный emit выключенным, генератор включён.
Миграция DI не заблокирована. Негативный журнал Studio:
`src/.local/cobalt-tests-debug-20261107-155709--66592--0V19Np.binlog`.
Исправление генератора и срок не назначаются автором заявки.

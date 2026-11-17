# XML-документация generated-кода Enerit

- ID: enerit-studio-generated-xml-docs-f2ad173c
- Дата UTC: 2026-11-16.
- Consumer: TrueMoon.Tales.Studio 1.2611.16.3-alpha; Windows x64, SDK 11.0.100, net11.0, runtime-async=on.
- Пакеты: TrueMoon.Enerit и TrueMoon.Enerit.Generator 1.2611.16.1-alpha из ENG7B3.
- Generator archive SHA256: 40E2BC69E4C7657B9A2B2D808E114B0BAA8F7F124B61092AE5875F83E1E82FBF.
- Модуль: Enerit.Generator. Capability: `enerit-generated-public-xml-docs-cs1591`.
- Статус: воспроизведённый quality issue, не блокирует runtime backend. Claim, Decision и реализация принадлежат владельцу модуля.

Studio включает GenerateDocumentationFile=true и явно подключает generator с PrivateAssets=all.
Типизированный IStudioBackend имеет HelloAsync и OpenProjectAsync, оба возвращают
mutable DTO с документированными публичными членами. Вызовы UseInvocationService
и ListenInvocationService запускают штатную генерацию.

Полная Debug/Release сборка даёт по 15 CS1591 из generated public classes/methods:
BackendIdentitySerializationExtensions, BackendProjectReplySerializationExtensions,
StudioBackendGeneratedImplementation, StudioBackendInvocationServerHandler и
ModuleInitializer. Также присутствуют 5 CS8669; nullable-context уже описан в
[существующем quality backlog Enerit](../../../enerit/PLAN.md), отдельная задача на него здесь не создаётся.
Собственный C# код Studio предупреждений не даёт; NoWarn не добавлен.

Пример consumer: [контракт](../../../../../../TrueMoon.Tales.Studio/src/TrueMoon.Tales.Studio/Backend/IStudioBackend.cs),
[композиция](../../../../../../TrueMoon.Tales.Studio/src/TrueMoon.Tales.Studio/Backend/BackendConfiguration.cs).
Воспроизведение из Studio/src:

```powershell
dotnet build TrueMoon.Tales.Studio.slnx --no-restore --no-incremental --configuration Release --artifacts-path .local/studio-build '-bl:.local/enerit-xml-{}.binlog'
```

Сверены API закреплённых пакетов, текущие исходники generator, PLAN/STATUS и
существующие requests. Заявки Enerit по generated XML docs при проверке не было.
Текущие шаблоны SourceHandleExtensions/GenerationContextExtensions по-прежнему
выдают служебные публичные объявления без XML summary. Исправление Cobalt по
аналогичному признаку относится к другому генератору и не устраняет этот сценарий.

Ожидание: потребитель с XML docs и CS1591 в WarningsAsErrors собирается в обеих
конфигурациях, module initializer и typed IPC сохраняют работу. Собственный
недокументированный public member потребителя продолжает вызывать CS1591.
Не требуется отключение документации либо глобальный NoWarn. Вариант решения
(summary, обоснованная видимость или локальная политика generated-файла) определяет
владелец. Протокол, endpoint, DTO и ownership менять не требуется.

Временное решение Studio: сохранить предупреждения и явно указать границу проверки.
Код фреймворка и его активные планы этой заявкой не изменяются.

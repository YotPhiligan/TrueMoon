# XML-документация служебного кода Cobalt

- ID: cobalt-studio-generated-xml-docs-56375bd8
- Дата UTC: 2026-11-07T16:02:34.963286+00:00
- Consumer: TrueMoon.Tales.Studio, net10.0, Windows x64, SDK 10.0.403.
- Пакет: TrueMoon.Cobalt 0.2.5.1-alpha, bundled generator SHA256
  `62eb4fa4ed44a69c68e5a204cae07e80d72ee6f21db3fd8de509406b31f9cd5a`.
- Модуль: Cobalt.Generator. Capability: `generated-registration-cs1591`.
- Источник: [StudioServices.cs](../../../../../../TrueMoon.Tales.Studio/src/TrueMoon.Tales.Studio/Infrastructure/StudioServices.cs).
- Состояние: входящая заявка потребителя; Claim/Decision и реализация — владельцу модуля.

Studio включает GenerateDocumentationFile и не подавляет CS1591. После включения
Cobalt generator обычная полная сборка успешно заканчивается с двумя новыми
предупреждениями CS1591, оба из generated `CobaltResolversRegistration.g.cs`:
публичный `CobaltResolversRegistration` и его публичный `Init()` не имеют XML docs.
Собственный код Studio/test graph даёт 71 passed, 0 failed, 1 optional skipped.

Минимальная конфигурация consumer:

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<EnableCobaltGenerator>true</EnableCobaltGenerator>
<PackageReference Include="TrueMoon.Cobalt" Version="0.2.5.1-alpha" />
```

Любая generated-регистрация, например Singleton<SceneViewModel>(), включает
публичный module initializer. Для воспроизведения из Studio/src:

```powershell
dotnet build TrueMoon.Tales.Studio.slnx --no-restore --no-incremental --artifacts-path .local/studio-build --configuration Release '-bl:.local/cobalt-xml-{}.binlog'
```

Текущий CobaltGenerator по-прежнему выдаёт эти публичные объявления без XML docs.
Подходящей активной заявки/задачи по capability при проверке не найдено.
Генератор поставлен из того же пакета, отдельного analyzer reference нет.

Ожидание: consumers с XML docs не получают CS1591 из служебной регистрации.
Варианты: добавить summary к generated public members либо обосновать internal
видимость служебного кода. Изменение видимости и совместимость решает владелец
модуля; необходимы рабочий module initializer и прежнее разрешение графа.

Критерии: consumer с GenerateDocumentationFile=true и CS1591 в WarningsAsErrors
компилируется в Debug/Release; module initializer регистрирует все resolver types;
ошибки XML в собственном коде consumer продолжают обнаруживаться. Не требуется
глобальный NoWarn или отключение документации у приложения. Lifetime/ownership
контракты не меняются.

Временный путь Studio: два предупреждения явно зафиксированы, CS1591 не подавляется.
Миграция DI не заблокирована. Полная сборка Studio: 0 errors, 2 warnings,
`src/.local/cobalt-final-build-20261107-160109--38648--egMxRM.binlog`.
Исправление генератора и срок не назначаются автором заявки.

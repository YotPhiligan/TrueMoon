# Fluent-композиция на примере EditorHierarchyView

В обозревателе поиск ведёт к совпадению, раскрывает ветвь и сохраняет
список сущностей. Переименование редактирует черновик выбранной строки.
Эти состояния принадлежат модели. View связывает их с полями и передаёт
фокус нужному контролу. Геометрию редактора над строкой задаёт `RenameLayer`.
[Дерево и обработчики](../../../../src/TrueMoon.Aether.Editor.UI/Hierarchy/EditorHierarchyView.cs),
[команды и клавиатура](../../../../src/TrueMoon.Aether.Editor.UI/Hierarchy/EditorHierarchyView.Entities.cs),
[оформление](../../../../src/TrueMoon.Aether.Editor.UI/Hierarchy/EditorHierarchyViewStyle.cs).

## Короткий пример

Это сокращённая композиция на том же интерфейсе модели. Она показывает
поле поиска, общую команду, прокручиваемое дерево и привязки редактора имени.
Готовую команду, модель и стиль передаёт фабрика View. В рабочем
обозревателе обновление строк, подтверждение переименования и фокус
реализованы в связанных выше исходниках.

```csharp
using TrueMoon.Aether.Editor.UI.Hierarchy;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;

namespace Examples;

public sealed class HierarchyPanelExample : View<IEditorHierarchyViewModel>
{
    public HierarchyPanelExample(IEditorHierarchyViewModel model, IUiCommand find)
        => Content(model, ui => ui.VStack(
            ui.Text("ОБОЗРЕВАТЕЛЬ").Named("Title"),
            ui.HStack(
                ui.TextBox()
                    .Named("Search")
                    .AccessibleName("Поиск по имени или ID")
                    .MinWidth(0)
                    .BindValueTwoWay(data => data.SearchText),
                ui.Button("Найти").WithCommand(find))
                .Named("SearchRow"),
            ui.ScrollViewer(
                ui.TreeView()
                    .Named("Tree")
                    .AccessibleName("Обозреватель сущностей")
                    .OnSelectionChanged(id => DataContext?.Select(id)))
                .Fill(),
            ui.TextBox()
                .Named("NameEditor")
                .AccessibleName("Имя сущности")
                .MinWidth(0)
                .BindValueTwoWay(data => data.RenameText)
                .Bind(UiProperties.Visible, data => data.IsRenaming)
                .Bind(UiProperties.HasError, data => data.HasRenameError)
                .Bind(UiProperties.ErrorMessage, data => data.RenameError)));

    public static HierarchyPanelExample Create(
        IEditorHierarchyViewModel model, IUiCommand find, IViewStyle<HierarchyPanelExample> style) =>
        ViewCreation.Prepare(new HierarchyPanelExample(model, find), style);

    public void FocusSearch(IInputContext input) => input.Focus(this.Search);
}

public sealed class HierarchyPanelExampleStyle : IViewStyle<HierarchyPanelExample>
{
    public void Configure(IViewStyleBuilder<HierarchyPanelExample> styles)
    {
        styles.Root().Padding(8).Background(theme => theme.Surface);
        styles.Part(view => view.Title).FontSize(13);
        styles.Part(view => view.SearchRow).Set(UiProperties.Spacing, 4f);
        styles.Part(view => view.Search).FontSize(11).Set(UiProperties.Width, 156f);
        styles.TypedPart(view => view.Tree).FontSize(12).RowHeight(24f).Indent(16f);
        styles.Part(view => view.NameEditor).Padding(new Thickness(2, 0, 2, 0));
    }
}
```

- **Дерево.** Заголовок и строка поиска идут первыми. `Fill()` на
  `ScrollViewer` отдаёт ему остаток высоты вертикального стека.
  `MinWidth(0)` — локальное разрешение сжать поле. Ширина, размеры строк
  и отступы объявлены в стиле.
- **Привязки.** `BindValueTwoWay` связывает значение поля со строкой в текущем
  `DataContext`. `Bind` читает видимость и ошибки. Смена модели сохраняет
  экземпляры контролов. Фабрика передаёт общую команду поиска. Если готовая
  команда находится в текущей модели, используй `WithCommand(data => data.FindCommand)`.
  Для наблюдения за контекстом в `OnContentPrepared()` подходит
  `this.ObservePropertyChanged(this, handler, synchronize)` со своей
  синхронизацией при подключении и замене источника.
- **Именованные части.** `Named("Search")` — техническое имя и ключ
  реестра. `AccessibleName` предназначен пользователю. В поведении
  обращайся к `this.Search`, а в стиле — к `view.Search`. Фабрика View
  регистрирует стиль через `ViewCreation.Prepare`. При первом attach
  сессия строит дерево, регистрирует части и применяет стиль.

## Как получается свойство

Генератор находит прямой вызов `Named("Search")` в inline-фабрике
`Content(...)`, объявленной в конструкторе, и выводит тип контрола из
Fluent-цепочки. Свойство читает реестр текущего View. Упрощённый фрагмент
вывода для примера выглядит так.

```csharp
public static class HierarchyPanelExampleParts
{
    extension(HierarchyPanelExample view)
    {
        public TextBox Search => view.GetRequiredPart<TextBox>("Search");
    }
}
```

Используй верхнеуровневый public или internal View без параметров типа,
публичные верхнеуровневые типы контролов без параметров типа и имена,
допустимые как идентификаторы C#. Держи декларации частей в теле фабрики
`Content(...)`. Генератор пропускает вложенные обработчики, локальные
функции и вызовы `Named(...)` на самих вложенных View. Имя берётся из
строки или независимой константы.
`nameof(view.Search)` для свойства-расширения выдаёт `CS9316`.
[Генератор](../../../../src/TrueMoon.Argentis.Generator/ViewPartsGenerator.cs).

В репозитории подключай `TrueMoon.Argentis.Generator` как анализатор проекта
с `OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"` и
`PrivateAssets="all"`. Пример подключения есть в проекте Editor UI.
Потребителям сгенерированных свойств нужен импорт пространства имён
прикладного View (в примере `Examples`), а не только `TrueMoon.Argentis.Views`.
Тип поля `TextBox` в иллюстрации относится к `TrueMoon.Argentis.Editors`.
NuGet-пакет Argentis подключает анализатор автоматически, в том числе
транзитивно через Hosting. Без генератора доступны `GetRequiredPart<T>()`
и `TryGetPart<T>()`.
При миграции существующего CLR-свойства пересобери потребителей.
[Подключение анализатора](../../../../src/TrueMoon.Aether.Editor.UI/TrueMoon.Aether.Editor.UI.csproj).

Свойство-расширение разрешает текущую часть живого View. Для ручной
подписки запомни экземпляр при подключении и используй его при очистке.
Так обозреватель снимает `FocusChanged` с исходного `NameEditor`. Имена
внутренних частей вложенного View принадлежат его собственному реестру.
[Область и поиск частей](../../../../src/TrueMoon.Argentis/Views/View.cs).

## Как проверить

- Введи запрос через `UiSession`. Проверь раскрытие ветви, прокрутку к
  совпадению и сохранение фокуса поля поиска.
- Начни переименование с клавиатуры. Проверь подтверждение, отмену и
  сохранение черновика при смене темы.
- Помести экран во внешний View с одноимёнными частями. Проверь, что стиль
  и ввод работают с нужным экземпляром. При `Dispose` проверяй очистку по
  ссылке на контрол, полученной до освобождения.
- Замени `DataContext` при активном поле. Проверь, что фокус сохранился,
  ввод записался в новую модель, а отложенное событие старой модели
  проигнорировано. После detach/reattach проверь тот же экземпляр контрола
  и одну подписку на актуальную модель.

Проверенные сценарии обозревателя находятся в тестах Editor.
[Именованные части и поиск](../../../../src/TrueMoon.Aether.Editor.Tests/Hierarchy/EditorHierarchyViewTests.cs),
[переименование](../../../../src/TrueMoon.Aether.Editor.Tests/Hierarchy/HierarchyMetadataTests.cs),
[регрессия UIA1f.11](../../../../src/docs/alloy/HISTORY.md#uia1f11-regression-2026-11-24).

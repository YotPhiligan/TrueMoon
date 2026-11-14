# Комбинированный Aether host

Роль выбирается до App.Build; один запуск строит один App/DI owner graph.

- Без selector или с `--editor`: Editor. `--project`, `--runtime-host`, `--font`,
  `--report`; без `--runtime-host` запускает owned child собственного executable.
- `--player --artifact <directory> --scene <scene.aetherscene>`: local Player.
  Те же window/input options, что в [Player-only host](../AetherPlayer/README.md).
- `--runtime`: внутренний offscreen child. Требует существующий валидный protected
  bootstrap и проверенный counterpart fingerprint; не является пользовательским Player.

Unknown/duplicate/несовместимые параметры и malformed protected identity отвергаются
до App. `--runtime-host` обозначает executable для Editor, `--runtime` — роль child.
CLI старого `Examples/AetherEditor --runtime <path>` не изменён.

В deployment этого host присутствуют Editor dependencies. Player branch не создаёт
Editor services. Для поставки без Editor assembly используйте Player-only host.
Runtime branch создаёт прежний Runtime/Game либо EditPreview, без окна/HUD.
Успех минимального local Player не заменяет дальнейшую ENG7b.3 matrix этих ветвей.

# TrueMoon.Kul: принятое название аудиомодуля

2026-11-12 пользователь выбрал **TrueMoon.Kul** после просмотра Лора «Сказок истинной луны».
Кул связан с Доменом Воздуха; в его проекции явно перечислены звуковые волны.
Латинская форма руны — `Kul`. Это техническая метафора названия, она не меняет лор.

[Домен Воздуха](../../../../TrueMoon.Tales.Lore/Handbooks/World/Handbook.Domains.md#домен-воздуха),
[Кул](../../../../TrueMoon.Tales.Lore/Handbooks/Deities/Handbook.Deities.md#божество-странствий-кул).
Соседний Lore checkout используется только как источник имени, runtime dependency нет.

| Объект | Прежде | Теперь |
| --- | --- | --- |
| Проект/каталог | TrueMoon.Audio | TrueMoon.Kul |
| Assembly/PackageId/root namespace | TrueMoon.Audio | TrueMoon.Kul |
| Тестовый проект/assembly/namespace | TrueMoon.Audio.Tests | TrueMoon.Kul.Tests |
| Consumer probe | AudioModuleProbe | KulModuleProbe |

Функциональные имена API сохранены: `UseAudio`, `UseConfiguredAudio`, `IAudioService`,
`AudioSession`, `AudioClip`, `AudioPlayback`, options/schema и остальные Audio-типы.
Теперь они находятся в `TrueMoon.Kul`. Конфигурационные ключи остаются `Audio:*`.
Новая совместимая оболочка под прежним namespace/package ID не создаётся:
потребители переходят на `using TrueMoon.Kul;` и PackageReference `TrueMoon.Kul`.

Исправленная сторонняя зависимость сохраняет ID `TrueMoon.MiniAudioEx`, version
`4.0.1-truemoon.1` и прежний native binary. Общая framework version берётся из
Directory.Build.props. Семантика воспроизведения и владения ресурсами не меняется.

Направление документации остаётся `docs/audio/`; прежний `AUD1_RESULTS.json` и
фактические команды в HISTORY сохраняют исходные имена и SHA как историю.
Новая приёмка Kul записывается отдельно, старые результаты не переименовываются задним числом.

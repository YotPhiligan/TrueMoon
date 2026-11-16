# Запуск Editor и Player: ENG7b.3

Приёмочный профиль — Windows x64, .NET 11/runtime-async, Vulkan producer →
OpenGL/Skia viewport. Схема версий впервые применена к этой поставке:
`1.2611.16.1-alpha`. Нативная Skia 4.156.1 и её ABI не менялись.

## Матрица

[Runner и команда](../../ManualTests/LaunchProfiles/README.md) выполняют source
Debug, published Release и fresh package-only Release. В каждом профиле Cobalt
и Microsoft DI проверяют три варианта Editor child: собственный executable,
комбинированный `Examples/AetherHost` и отдельный `Examples/AetherRuntimeHost`.
Standalone Player и Player комбинированного consumer проверяются отдельно.
Дополнительные четыре Player-профиля используют копию .NET runtime без SDK.

| Требование | Свидетельство |
| --- | --- |
| Роль выбирается до App, несовместимые CLI и protected identity не дают fallback | `PlayerProbe/VerifyLaunch.ps1`: 20 cases на source/published/fresh, проверяются exit и причина отказа/help |
| Повторные Play/Pause/Step/Stop, crash и восстановление | `LaunchProfiles/EditorProfile.cs`: 18 Editor-профилей × 3 цикла; второй child принудительно завершается, третий запускается заново |
| Сохранность Editor и документа | Те же циклы: окно/View/selection сохраняются, persisted bytes и immutable content неизменны, UndoCount/RedoCount = 1/1; после циклов реально выполняются Redo и Undo |
| Выбранная игра загружается только в child | Передача exact artifact fingerprint, game отсутствует в Editor CLR до/после циклов, child сообщает game loaded; Editor не запускает compiler |
| Child проверяет свою identity самостоятельно | По два direct-child отказа на Editor-профиль: forged fingerprint и другая claimed runtime entry, без parent precheck и без App diagnostics |
| Выбранные Player artifact/scene и native output | 16 Player-профилей: exact build/scene hash/ID/revision, пять GPU captures, input/resize/suspend/resume, immutable scene capture, отсутствие Editor/Tooling и zero remaining owners |
| Реальные entry points примеров | 12 отдельных окон: default/explicit Editor, combined/separate Player; собственному окну посылается close, процесс завершается с exit0 |
| Независимая поставка | 19 пакетов одной сохранённой версии; 7 внешних graphs без ProjectReference, закрытый feed, пустой cache, CPM isolation и exact shared/native/font/license payload checks |

Native-результаты этого профиля: 34 instrumented runs, 54 game cycles, 36
direct-child identity rejections, 684 Editor assertions, 60 CLI cases и 12 запусков
окон примеров. Нормальный child Stop проверяет validation errors/warnings = 0,
баланс surfaces/devices и отсутствие live slots/handles. Аварийный child не
приписывает себе graceful native teardown: parent отдельно подтверждает reap и
освобождение своих imports/frames/resources.

## Исправление повторного запуска

Первый mesh Play проходил, а второй после Stop падал с
`Mesh frame requires the matching GPU surface profile.` Родительский FrameSession
продолжал общий surface counter, тогда как новая GPU connection/producer начинали
его с 1. Теперь GPU generation начинается заново для новой runtime connection и
возрастает при resize/suspend/resume внутри неё. Полная SessionIdentity и instance
presentation продолжают отвергать tickets предыдущего child.

Регрессия:
`ViewportTransportTests.Restart_UsesFreshRuntimeSurfaceGenerationAndRejectsPreviousChildTickets`
проверяет 1 → resize 2 → новый child 1, новую runtime generation, отказ старого ticket,
ожидание новой actual presentation и освобождение обеих connections. Native-циклы
проверяют тот же переход с реальным renderer и process lifetime.

Комбинированный пример теперь собирает внутренний child через standalone Runtime
registration, используя полный frozen bootstrap. В нём не создаётся Tooling/Editor
граф; child completion сохраняет диагностические role/resource/validation сведения.

## Границы

Это приёмка запуска и доставки, не общий release. Physical input/DPI, device loss,
другие OS/RID, performance/soak, subsystem checkpoint/native-state migration и
оставшаяся ED/import очередь сохраняются. Kton/Kul не подключены к этому fixture;
пакетная матрица не является новой полной поставкой всех 27 framework packages.

Исторические ARCH3.9/ENG7b.1–2 manifests остаются самостоятельными снимками.
При переходе на новую версию нужно пересобрать/перепубликовать game artifacts с
точными shared binaries выбранного host: прежний binary fingerprint не подменяется
совпадением имени пакета или API. Новых native/GPU ABI версий этот переход не вводит.

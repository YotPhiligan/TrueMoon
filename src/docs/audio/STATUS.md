# Kul / Audio: состояние

**2026-11-14 — предкоммитная сверка:**23tests D/R, source/published/fresh delivery
по23offline checks и package metadata прошли. Device/слушание и native rebuild
не повторялись. [Состав, команды и границы](../COMMIT_AUDIT_2026-11-14.md).

**2026-11-12 — переименование в TrueMoon.Kul завершено и проверено.**
Project/PackageId/assembly/root namespace — `TrueMoon.Kul`, тесты — `TrueMoon.Kul.Tests`,
consumer — `KulModuleProbe`. [Выбор имени/переход](NAMING.md), [сборка/App API](README.md),
[история команд](HISTORY.md#kul-rename-2026-11-12), [новый manifest/SHA](KUL_RENAME_RESULTS.json).
UseAudio/UseConfiguredAudio, AudioSession/AudioClip/AudioPlayback и ключи Audio:* сохранены.

Новые проверки Kul: **23passed/0failed/0skipped каждый Debug/Release**;23tests также
найдены через основной TrueMoon.slnx без сборки чужих проектов. Source Debug/published
Release/fresh package-only Release прошли каждый23offline+18muted actual WASAPI checks.
Final logical owners/resident bytes0, files closed; exact new Kul DLL/package SHA проверен.
Native MiniAudioEx package/binary прежние, его original RED/GREEN этим rename не повторялся.

Предыдущий [AUD1 под именем TrueMoon.Audio](HISTORY.md#aud1-2026-11-12) и
[его manifest](AUD1_RESULTS.json) сохранены с исходными именами/SHA. Full solution regression
и слышимое прослушивание не повторялись; warning backlog зависимостей остаётся отдельно.

Следующий шаг: требования engine ECS/asset adapter отдельно от самостоятельного модуля;
AUD2 — async preparation/admission, device loss/soak и hard peak budgets по workload.
OGG duration unavailable, synchronous preparation/drain и возможная silence после seek
остаются явными ограничениями. Linux/macOS — AUD3; физическая слышимость повторно не проверялась.

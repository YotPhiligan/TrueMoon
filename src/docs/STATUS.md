# TrueMoon: общий статус

**2026-11-15 — DEP1: зависимости обновлены до latest stable, native Skia пересобрана.**
[Единственная детальная задача](core/PLAN.md#dep1-dependency-refresh),
[команды/результаты](core/HISTORY.md#dep1-2026-11-15), [manifest/SHA](core/DEPENDENCY_REFRESH_20261115.json).
Обновлены6central entries: Roslyn5.12.0 (2IDs), SkiaSharp4.156.1,
RichTextKit0.6.173, Silk Veldrid extension2.23.0, RecyclableMemoryStream4.0.1.
Два последних ID не имеют consumers в текущем solution. Остальные публичные entries
актуальны в stable/existing-BEPU-beta policy; SDK/TFM/общая framework version прежние.

| Направление | Новая проверка15ноября | Что дальше |
| --- | --- | --- |
| [Core/Cobalt/Enerit/Mithril](core/STATUS.md) |Roslyn5.12.0; полные runtime/generator/DI/IPC suites,27framework metadata archives |Функциональные задачи у владельцев |
| [Alloy/Argentis](alloy/STATUS.md) |Exact native4.156.1 source build, ABI1/48bytes, GPU/typography validation0/0; fresh UI10packages/40cases |PA8c/physical input/DPI/K7/K8/release |
| [Aether/Editor](engine/STATUS.md) |Полные Aether3533/Editor1507/Player17/Abstractions26 каждый D/R; bridges103+68 |ENG7b.3 кандидат, ED/import отдельно |
| [Kton](physics/STATUS.md) |BEPU beta33 актуален, PHYS5 68D/R |PHYS6/PHYS8 отдельно |
| [Kul](audio/STATUS.md) |27module+103bridge D/R;4delivery reports×43muted WASAPI checks, native fork актуален |AUD2–3/spatial/import/Nom отдельно |
| [Nom](nom/STATUS.md) |Полный Nom874D/R, storage/source API не менялись |Прежние g.2/VM/hardware boundaries |

Полные16suites по9110passed каждый Debug/Release,0failed/0skipped. Non-incremental
solution0errors/1570warnings/config; warning location/ID sets совпали с прежними.
Restore NuGetAuditMode=all advisories/NU1900 не сообщил. Проверены27metadata/payload
archives и отдельные fresh UI/audio deliveries; единую поставку всех27framework
packages во все architecture consumers этот run не повторяет.

Запрошенные ARCH1j → PHYS5 → Kul ранее завершены в своих bounded profiles;
функциональная очередь ENG7b.3/ED/import и release gates сохранены.
Historical drain cause, physical input/listening/device loss/long soak/hard RT,
other OS/RID/AOT/trimming и общий release не закрыты. [Предыдущий STATUS](HISTORY.md#common-status-before-dep1-2026-11-15).

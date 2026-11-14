# ARCH3.1 independent contract proof

```powershell
pwsh -NoProfile -File ManualTests/Abstractions.Host/Verify.ps1 -EvidenceDirectory <fresh-directory>
```

SDK/TFM/LangVersion/runtime-async и CPM isolation берутся из общего
`ManualTests/BuildProfile.ps1`. Source Debug, published Release и fresh NuGet
consumers вне checkout выполняются на Cobalt и Microsoft DI. Все package restores
fresh профиля используют только private offline feed и отдельный пустой cache.
Extension graph содержит лишь Abstractions и Contracts, без ProjectReference.
Проверяются package provenance и installed/output Abstractions DLL SHA.

Host активирует `ContractExtension.dll` в collectible ALC и разделяет с ним точные
host Abstractions/Contracts assemblies. Mismatched private shared copy отвергается
до загрузки. Factory lazy, созданная система caller-owned; Execute→Stop→Dispose
выполняются один раз. Host не загружает legacy Aether, Runtime, Tooling, Editor,
Silk/Skia. Backend — `ProbeAccess`, намеренно ограниченный single-callback double,
без World/pools/scheduler/playback. Данные компонентов не хранятся boxed.

Это proof контрактного SDK, не игровой loop и не production mod loader. Unload
запрашивается при cleanup; сборка ALC после GC не объявляется подтверждённой.
Реальный ECS binding относится к ARCH3.2, artifact loader/shared set — ARCH3.3/3.6.

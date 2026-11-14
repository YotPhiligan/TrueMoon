# TrueMoon.Aether.Runtime

Самостоятельное исполнение подготовленной игры и сцены: World/ECS, sessions,
frames/control, scene activation, render/material/resource systems и backend paths.
Без source compiler, authoring workflow и Editor UI. Общий профиль — .NET 11.

[API пакета](PACKAGE.md), [миграция и ownership](../docs/engine/RUNTIME.md),
[текущие проверки](../docs/engine/STATUS.md),
[независимый consumer без SDK](../ManualTests/RuntimeConsumer/README.md),
[ECS binding](../docs/engine/ECS_BINDING.md),
[отдельный child host](../Examples/AetherRuntimeHost/README.md).

Общие значения и game/mod contracts находятся в Abstractions. Для полного
compiler/authoring/editor workflow временно подключается TrueMoon.Aether;
отдельный Tooling появится в ARCH3.4. Runtime не зависит от этого workflow.

# Прежний TrueMoon.Aether package — историческое описание

ARCH3.4 заменяет package references на TrueMoon.Aether.Tooling/Runtime/Abstractions.
[Текущий Tooling package guide](../TrueMoon.Aether.Tooling/PACKAGE.md).
Ниже сохранено прежнее описание; этот проект больше не упаковывается.

# TrueMoon.Aether

Temporary Aether workflow package after ARCH3.3 Runtime extraction. Uses the common
.NET 11 profile and matching TrueMoon package versions. Runtime execution has moved
to TrueMoon.Aether.Runtime; canonical game/ECS/scene values to Abstractions.
This package is an explicit workflow composition, not a binary compatibility facade.

UseAether(options => ...) registers Runtime plus compiler, GameEditor and static
Edit preview coordination. AetherOptions extends RuntimeOptions with build directory,
timeout and retention. AetherHost borrows the App-owned RuntimeEngine, owns compiler/
preview products, drains workflow before requesting Runtime Stop, and preserves
retry ownership after failed cleanup. Services and factories come from App/DI.

UseAetherAuthoring and UseAetherScenePersistence register UI-independent document
editing, history and explicit file operations. SceneFileCodec applies authoring
admission over Runtime SceneDocumentCodec's unchanged scene JSON grammar.
Authoring sessions/documents and factory products remain caller-owned.

GameBuildCompiler.BuildAsync and GameEditor return GameBuildResult: a read-only
Runtime GameBuildArtifact plus process-local Inputs/Compilation witnesses.
Opening a delivered artifact never reconstructs trusted source provenance.
GameArtifactPublisher.Publish is the explicit tooling publication API.

Artifacts use schema 2: exact Runtime/pair identity, shared assembly identities/hashes
and private DLL hashes. Default game references are Abstractions + Contracts;
GameBuildRequest.RuntimeAccess=true explicitly admits concrete Runtime APIs.
Compiler properties are AetherAbstractionsPath/TrueMoonContractsPath and optional
AetherRuntimePath. Use non-copying references. Ordinary, tracked, captured and frozen
builds retain their input validation, cancellation, process reap and ownership rules.
Schema 1 artifacts and games using the old monolithic ABI must be rebuilt.

Common configuration: AetherConfigurationSchemas.Engine/Authoring with
UseConfiguredAether/UseConfiguredAetherAuthoring. Runtime-only schemas and helpers
live in TrueMoon.Aether.Runtime; use their exact instances in a Runtime-only App.
No UI, source compilation or native backend is activated just by registration.

Headless Tooling extraction is ARCH3.4; Editor UI separation is ARCH3.5. Runtime-only
consumers should reference Runtime directly. Native/release/device-loss, other OS,
production mod unload and state migration are separate acceptance scopes.

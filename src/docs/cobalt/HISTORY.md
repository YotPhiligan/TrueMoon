# Cobalt / DI: история проверок

[План](PLAN.md), [текущий статус](STATUS.md), [общий статус](../STATUS.md).

## Исправление registration, generator и ownership — 2026-10-10

Запрос «давай починим все проблемы» продолжает [review Core](../core/HISTORY.md#ревью-ядра-и-core0-baseline--2026-10-10). Исправлены exact service/implementation/lifetime matching, generated source names, runtime-owned factories, Composite2–8 aliases, closed/open generic dependency resolution и typed enumeration. TypeContainer полностью инициализирует все factories, singleton resolution синхронизирован и изолирован между resolvers.

Cobalt владеет generated/factory/transient values, сохраняет borrowed instance registrations, учитывает unique references и reverse creation order. Sync/async disposal сохраняет ошибки и продолжает cleanup. Shared completion + AsyncLocal reentrancy guard позволяют concurrent calls ждать одно завершение, а owned service не блокироваться при recursive resolver disposal.

Microsoft DI wrapper зарегистрирован один раз как borrowed instance до provider construction и затем attached к provider. Factories получают тот же IServiceResolver; wrapper владеет provider и поддерживает matching sync/async mode, failures/concurrent/reentrant cleanup. Стандартный provider учёт disposable Composite aliases сохранён: shared identity и три Dispose для concrete+двух aliases закреплены тестом; требования unique cleanup/continue after failure не перенесены на Microsoft DI.

### Проверки

Agent direct runs exit0: `dotnet test TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj --no-restore --verbosity quiet` —37/37; generator project аналогичной командой —6/6. Root повторил после заморозки всех правок полный non-incremental solution build и стандартный solution test/discovery. Windows/Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest: **runtime37passed/0failed/0skipped, generator6passed/0failed/0skipped**. Core81passed отдельно подтверждает AppCreate и lifecycle обоих providers.

[Точные итоговые команды, counters и артефакты](../core/HISTORY.md#исправление-проблем-ядра--2026-10-10). TRX runtime `TestResults/CoreFixes-Final-20261010/MEGA6_PH-DEV_2026-10-10_16_58_57_net10.0.trx`; generator — timestamp16:58:58 `[1].trx`. Generator tests проверяют diagnostics/compiler/emit и выполняют emitted alias/open-generic graph; references Core/TrueMoon удалены из generator test project как не нужные этому consumer.

| Requirement | Evidence |
| --- | --- |
| Exact registration/lifetime/isolation | `RuntimeGraph_SelectsImplementationAndLifetime_WithoutAssemblyPollution`, `GeneratedSingletons_AreIndependentAcrossResolvers` |
| Concurrent singleton | `SingletonFactory_ConcurrentResolution_CreatesOneOwnedInstance`, `FactoryResolver_DirectConcurrentSingletonCalls_RunFactoryOnce` |
| Composite2–8 identity | `Composite_AllAliasesShareOneOwnedInstance`; executable emitted graphs в GeneratorTests |
| Unique ownership/order/errors | `OwnedFactories_DisposeUniqueInstancesInReverseOrder_AndPreserveBorrowedInstances`, `CleanupFailure_StillDisposesRemainingServices_AndIsNotRetried`, `TransientFactories_OwnEachCreatedDisposable` |
| Modes/concurrent/reentrant disposal | `SyncDisposal_HandlesAsyncOnlyFactoryValues`, `DisposablesContainer_PrefersMatchingMode_ForDualDisposableValues`, `DisposablesContainer_ConcurrentAsyncCleanup_SharesCompletion`, `OwnedServiceDisposal_CanReenterResolverDisposal_WithoutDeadlocking` |
| Generics/enumerable/missing | `OpenGenerics_ResolveClosedDependenciesAndTypedEnumerables_WithConcurrentSingletonIdentity`, `MissingServices_AreOptionalOrRequired_AndOnlyTypedEnumerableIsSynthesized` |
| Containers/queries/legacy typed path | `TypeContainer_InitializesAllFactoriesOnce_WhenSingleAndMultipleResolutionMix`, `RegistrationQueries_ReflectRemovalAndExactImplementation`, `TypeOverload_ForClosedServicesUsesGeneratedConstruction`, `TypedOnlyResolvers_WorkThroughProviderAndEnumerable_AndPreserveOriginalErrors` |
| Adapter wrapper/provider ownership | `Builder_ReturnsSameResolverToFactories_AndDisposesOwnedProviderOnce`, `Wrapper_PrefersMatchingDisposalMode_AndDisposesProviderOnce`, `ConcurrentAsyncDisposal_WaitsForSameProviderCleanup`, `SyncDisposal_CleansAsyncOnlyProvider`, `ProviderCleanupFailure_PreservesErrorAndDoesNotRetry`, `OwnedServiceDisposal_CanReenterWrapperDisposal_WithoutDeadlocking` |
| Provider difference | `CompositeAliases_ResolveSameConcreteSingleton` — same objects и explicit3 Dispose |

Assertion-quality/static test-gap review:32 named regression/generator methods,150 static Assert call sites включая shared compiler helper;160 effective assertions с helper expansion, average5/method. Assertion-free/trivial/self-field0.10/12 категорий; approximation/comparison не нужны этому contract. Removing metadata/lifetime filter, owned tracking/reverse order/uniqueness, optional resolution or recursive bypass изменит identity/count/error/completion assertions. Это static likely-killed reasoning, реальные mutants не запускались.

Package/analyzer delivery, Release/scopes и performance не проверялись. RS1041/net10 generator и прочие shared warnings остаются; ManualTests/CobaltTest собран, отдельный запуск не выполнялся.

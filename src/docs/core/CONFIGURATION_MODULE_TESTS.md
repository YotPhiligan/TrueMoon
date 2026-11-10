# CORE7.5: матрица проверок конфигурации

110 новых test cases /49 methods: Aether42, Editor28, Enerit11, Nom12,
Hosting17. Theory cases считаются отдельно. Это scoped configuration acceptance;
не измерение line/branch coverage. Все новые файлы добавлены отдельно от прежних
тестов. Точные результаты и commands — [HISTORY](HISTORY.md#core75-2026-11-10).

Проверки выполнены на существующих xUnit2/VSTest. Cobalt/MS DI используются там,
где указаны оба провайдера; Nom/Hosting unit suites используют Cobalt, а отдельный
[consumer](../../ManualTests/ConfigurationModulesConsumer/README.md) проверяет их
на обоих DI. Static assertion review и behavioral gap review дополняют запуск:
фактические consumers, invalid-before-factory, independent copies, restart rejection,
cleanup и compatibility проверяются непосредственно. Synthetic metadata/defaults
проверки не выдаются за native/GPU или concurrency proof.

## TrueMoon.Aether.Tests.Configuration.ModuleConfigurationMigrationTests

[Исходник](../../TrueMoon.Aether.Tests/Configuration/ModuleConfigurationMigrationTests.cs). В таблице точные method names этого класса.

| Method | Ожидаемое поведение |
| --- | --- |
| `StandardModuleSchemasBindTypedInputsAndReturnIndependentOptions` | Типы, вложенные данные и independent copies. |
| `ModuleMetadataContainsOnlyStartupDataAndExcludesCompositionAndCameraIdentity` | StartupOnly; resource/identity/camera fields отсутствуют в sources. |
| `InvalidModuleWinnerFailsBeforeAnyRegistration` | Invalid winning fields отклоняются до регистрации на обоих DI. |
| `ConfiguredHeadlessFactoriesEnforcePreparedLimitsAndKeepProductsCallerOwned` | Настоящие World/scheduler/scene/simulation/render/authoring factories; лимиты и caller cleanup. |
| `ConfiguredEngineCopiesNestedSettingsAndKeepsIdentityAndFactoriesInComposition` | Engine data копируется, runtime factory lazy/App-owned, identity явно задана. |
| `StartupModuleChangeRejectsCandidateWithoutRecreatingFactories` | Reload требует restart, reader/factories сохраняются. |
| `ConfiguredEngineRejectsInvalidWinningBudgetBeforeRuntimeFactory` | Ошибка вложенного budget до runtime activation. |
| `ConfiguredSchedulerUsesThePreparedSystemLimitForItsExplicitGraph` | Один explicit system допускается, граф из двух отвергается при limit1. |

## TrueMoon.Aether.Tests.Configuration.RuntimeSettingsMigrationTests

[Исходник](../../TrueMoon.Aether.Tests/Configuration/RuntimeSettingsMigrationTests.cs). В таблице точные method names этого класса.

| Method | Ожидаемое поведение |
| --- | --- |
| `StartupFlagsUseCommonConversionWithoutReadingIdentity` | Boolean/1/0/absent conversion не читает protocol identity. |
| `CanonicalStartupFlagWinsOverLegacyAlias` | Canonical public flag имеет приоритет над legacy. |
| `InvalidWinningStartupFlagDoesNotFallBackAndIsRedacted` | Invalid winner не заменяется fallback и не раскрывает исходное значение. |
| `ProtectedBootstrapCapturesIdentityParentControlAndFlagsIndependentlyOfLaterInputChanges` | Изменение исходного capture не меняет frozen launch/parent/control/settings. |
| `SelectedBootstrapRejectsInvalidInputBeforeAppOrResourceActivation` | Invalid flag/control отклоняется до App. |
| `NativeCommandSettingsAreIndependentAndAffectOnlyChildStartInfo` | Descriptor copy и child environment; parent environment не меняется. |
| `RuntimeRegistrationUsesCapturedParentInsteadOfMutableLegacyConfiguration` | Factory использует frozen parent/control на обоих DI. |
| `ConfiguredRuntimeBindsBudgetsAndPreservesProtectedLaunchDeliveryAndStartupFlags` | Ordinary config/callback не подменяют protected report/delivery/identity/flags. |
| `LegacyRuntimeOverloadCapturesFlagsAtRegistrationAndRetainsCompatibility` | Старый overload фиксирует selected environment один раз; test восстанавливает vars. |

## TrueMoon.Aether.Editor.Tests.Configuration.EditorConfigurationMigrationTests

[Исходник](../../TrueMoon.Aether.Editor.Tests/Configuration/EditorConfigurationMigrationTests.cs). В таблице точные method names этого класса.

| Method | Ожидаемое поведение |
| --- | --- |
| `EditorSchemasBindSourceTrackingPresentationAndSourceCommandWithoutIo` | Project/source/diagnostics/command data bind без directory/process IO. |
| `EditorDefaultDataIsCopiedWithoutPromotingUiStateToConfiguration` | Independent nested copies, selection/viewport/factories не являются fields. |
| `InvalidEditorWinnerFailsBeforeAnyRegistration` | Ошибки project/watch/diagnostics/command до registration на обоих DI. |
| `ConfiguredEditorPreservesEmptyProjectCaptureDefaultViewportAndOwnedRuntimeFactory` | Empty Editor startup, default viewport alias, no child/GPU, factory dispose ровно1. |
| `ConfiguredPresentationBudgetsAndSourceCommandReachExistingConsumers` | Настоящие diagnostic model/command получают бюджеты и copied StartInfo. |
| `ConfiguredEditorRejectsInvalidNestedWinnerBeforeRuntimeFactory` | Invalid nested source-watching budget до engine activation. |
| `EditorCliCommonGrammarKeepsOldAndCanonicalAliasesAndTreatsFalseFlagsAsDisabled` | Старые/canonical/inline/separate aliases, presence и false flags; absent CLI наследует legacy/canonical env, explicit true/false перекрывают invalid lower env; programmatic true сохранён. |
| `EditorCliRejectsDuplicatesIncludingCanonicalAliasesAndRepeatedEqualValues` | Повтор одного scalar/flag через любой alias отвергается. |
| `EditorCliInvalidAndUnsupportedInputKeepsValuesOutOfErrors` | Unsupported/invalid CLI не раскрывает значения. |
| `EditorWindowSchemaPreservesStandardDefaultsAndIndependentScalarCopies` | 1280x800 и прежние frame/chrome defaults; copies независимы. |
| `EditorWindowSchemaAcceptsItsExactMinimumWithoutOpeningAWindow` | 1100x700 допустим, окно не открывается. |
| `EditorWindowSchemaRejectsImmediatelyBelowEachMinimum` | 1099x700 и1100x699 отвергаются. |

## TrueMoon.Enerit.Tests.Configuration.TransportConfigurationMigrationTests

[Исходник](../../TrueMoon.Enerit.Tests/Configuration/TransportConfigurationMigrationTests.cs). В таблице точные method names этого класса.

| Method | Ожидаемое поведение |
| --- | --- |
| `StandardTransportSchemaDeclaresAllDataLimitsAndDetachedDefaults` | Все9 transport data fields, defaults и копии. |
| `ConfiguredTransportUsesPreparedCopiedLimitsForActualClientAdmission` | Настоящий client admission отклоняет лишний request до payload; оба DI и owned cleanup. |
| `InvalidTransportWinnerFailsBeforeAnyModuleRegistrationOrEndpointFactory` | Invalid budgets до registration/endpoint factory. |
| `TransportStartupReloadRetainsFactoryAndPreparedOptionsWithoutReattachment` | Startup reload не меняет transport factory/options. |
| `DuplicateConfiguredTransportAttachmentIsRejectedWithoutBuildingResources` | Повтор attachment отвергается до resource creation. |

## TrueMoon.Nom.Tests.Configuration.NomConfigurationMigrationTests

[Исходник](../../TrueMoon.Nom.Tests/Configuration/NomConfigurationMigrationTests.cs). В таблице точные method names этого класса.

| Method | Ожидаемое поведение |
| --- | --- |
| `StandardNomSchemaExposesOnlyAdmissionSettingsAndDetachedDefaults` | Все8 limits/drain fields; roots/IDs отсутствуют. |
| `ConfiguredNomAppliesCopiedSourceAndCallbackLimitsToTheActualStoreFactory` | Source+callback влияют на настоящий store limit, no startup IO, slot возвращается после dispose. |
| `InvalidNomWinnerRejectsBeforeModuleRegistrationAndStoreIo` | Invalid limits до registration/store IO; ошибки redacted. |
| `NomStartupReloadKeepsCurrentFactoryAndItsOriginalCapacity` | Restart rejection сохраняет reader/factory и настоящий limit1. |
| `ConfiguredAppsKeepIndependentNomHostsAndReaderCopies` | Два App не делят hosts/options/lifetime. |
| `LegacyAndConfiguredNomCannotAttachTwoOwners` | Legacy+configured повтор не создаёт второго owner. |

## TrueMoon.Alloy.Hosting.Tests.Configuration.WindowConfigurationMigrationTests

[Исходник](../../TrueMoon.Alloy.Hosting.Tests/Configuration/WindowConfigurationMigrationTests.cs). В таблице точные method names этого класса.

| Method | Ожидаемое поведение |
| --- | --- |
| `WindowSchemaDeclaresScalarStartupDataWithoutRendererOrViewFactories` | Все10 scalar fields; renderer/view factories не становятся data. |
| `ConfiguredPresentationAppliesPreparedDataToExistingWindowAndKeepsConstructionLazy` | Источники/callback применяются, snapshots независимы, no window/session/root activation. |
| `ConfiguredChromeFlagsPreserveBorrowedTitlebarHookWithoutExecutingIt` | Borrowed chrome callback сохраняется без вызова. |
| `ConfiguredPresentationCreatesMissingWindowWithoutReplacingExplicitRenderer` | Window defaults добавляются, explicit renderer сохраняется. |
| `InvalidWinningWindowDataFailsBeforeCompositionRootOrNativeStartup` | Invalid size/FPS/title/enum до composition/native. |
| `FrameRateBoundaryValuesBindWithoutCreatingNativeResources` | FPS1 и240 допустимы без native activation. |
| `WindowStartupReloadRejectsWholeCandidateAndDoesNotRecomposeTheWindow` | Startup candidate отвергнут атомарно; compose не повторяется. |
| `WindowDataCannotOverrideExplicitRendererCompatibilityOrAttachTwice` | Renderer/API mismatch и повтор attachment отклоняются. |
| `ApplyingScalarWindowDataPreservesBorrowedAppearanceChromeAndFactories` | Прямое scalar apply сохраняет appearance/fonts/hooks/factories. |

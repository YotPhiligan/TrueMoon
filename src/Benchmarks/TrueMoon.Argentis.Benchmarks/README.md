# View baseline: inspector scene v1

Permanent UIA4.2 measurement suite for the Argentis View layer, `net11.0`/Release. It pins the cost of typed
properties, layout walks and bindings before any optimization, so UIA4.3 (addressable layout) and UIA4.4 (per-node
constants) are judged with a measured pair instead of an estimate. Plan and confirmed limits:
`../../docs/alloy/PLAN.md#uia4-view-layer-audit`, recorded figures:
`../../docs/alloy/PERFORMANCE_BASELINE.md`.

BenchmarkDotNet is not used: version 0.15.8 pinned by Central Package Management aborts on `net11.0`
(`GetRuntimeVersion not implemented for NotRecognized`). The suite follows the Settings K9 convention instead —
own process, `Stopwatch` and `GC.GetAllocatedBytesForCurrentThread`, raw samples kept in full, nearest-rank median and
P95. No package pin changes were made for it.

## Scene and boundaries

The scene is inspector-shaped: one row per index with a label `Text`, a fixed-size editor leaf and two value labels —
5 elements per row plus the root, so 39 rows reproduce the 195-element K9 Settings tree. Sizes are 39, 312 and 2496
rows (196, 1561 and 12481 elements) for the scene, collection-binding and property-store groups; property-store counts
nodes rather than rows, because it builds no tree; property-binding runs at 1, 64 and 512 bindings.

Text metrics come from `FixedTextLayout` (`length*size/2` by `size`), so no font engine participates.
`HeadlessSurface.Render` neither walks nor draws the tree, so a dirty `Update` covers notification, binding work and
layout only. `property-access` and `node-cost` cases run on detached trees without a session at all. No window, GPU,
input, drawing, fonts or native memory is measured here; whole-frame numbers with real rendering stay in the K9
section of `../../docs/alloy/PERFORMANCE_BASELINE.md`.

Each case owns its scene: `Setup` builds it, warmup then samples call `Run` repeatedly, `Cleanup` disposes it.
`MeasureScene` walks an unchanged tree, `LeafEditThenLayout` edits one leaf value and walks the same tree, so the pair
shows directly whether dirty-node position matters. Results are per call, not per frame; binding-group figures include
the tree construction or session teardown named in the case. Do not subtract separate means to claim isolated phase
time: a collection mutation also lays out what it dirtied.

## Cases

| Case | Timed operation |
| --- | --- |
| node-cost.BuildScene | Builds the detached scene, including the eagerly allocated per-node collections |
| node-cost.BuildAndDisposeScene | Builds and disposes the subtree, one screen open and close |
| node-cost.MeasureScene | Full measure walk over the unchanged tree; there is no measure-validity cache |
| node-cost.ArrangeScene | Full arrange walk after a previous measure |
| node-cost.MeasureAndArrangeScene | Measure plus arrange of the unchanged tree |
| node-cost.LeafEditThenLayout | Edits one leaf value, then measures and arranges the whole tree |
| property-access.GetLocalValue | Reads a locally set value |
| property-access.GetThemeValue | Reads a value answered from the theme |
| property-access.GetUnknownDefault | Reads a property nothing knows; every theme branch is compared first |
| property-access.SetSameValue | Writes the value the element already has |
| property-access.SetChangedValue | Changed write: previous lookup, invalidation to the root, observer dispatch |
| property-access.InvalidateRender | Direct leaf invalidation, which walks to the root |
| property-store.StoreOneProperty | Creates and disposes N detached nodes holding one local value each |
| property-store.StoreTwoProperties | The same with two local values: the storage the second value needs |
| property-store.StoreThreeProperties | The same with three local values: the pair prices the dictionary from the second value on |
| collection-binding.UpdateStatic | `Update` with no source or UI change; the floor of the group |
| collection-binding.AppendRemoveRow | Alternating append and remove, one mutation plus one `Update` |
| collection-binding.MoveFirstRowToEnd | `Move` of the first row to the end, identity preserved |
| collection-binding.NotifyOneRowName | One row's bound source property changes, then `Update` |
| collection-binding.RefreshAllRows | `RefreshItems` re-snapshot with no collection change |
| collection-binding.ResetAllRows | `Clear` plus re-adding the same count, fresh subtrees |
| keyed-binding.UpdateStatic | `Update` with a key-identified binding and no source or UI change; the floor of the group |
| keyed-binding.KeyedRefreshUnchanged | `RefreshItems` over `IReadOnlyList` where every key is already present |
| keyed-binding.KeyedAppendRemoveRow | Alternating append and remove in a plain `List<T>` plus `RefreshItems` and `Update` |
| keyed-binding.KeyedMoveFirstRowToEnd | `Move` of the first item to the end; identity carried by key, not reference |
| keyed-binding.KeyedRebuildSameKeys | `Clear` plus re-adding the same ids as new instances; every row must survive |
| keyed-binding.KeyedSwapPrebuiltModels | the same keys swapped between two pre-built model sets; no model allocation inside the sample, every rebind writes a different name |
| keyed-binding.KeyedReplaceRowInstance | One item instance replaced under a preserved key; `rebind` runs, no new subtree |
| property-binding.CreateBindings | K detached bindings: expression handling and binding objects |
| property-binding.AttachAndFirstSync | K bindings, attach, first synchronization, teardown through the session |
| property-binding.NotifyOneAndUpdate | One source change through K live bindings |
| property-binding.NotifyAllAndUpdate | Every source changes once, then `Update` |
| property-binding.UiEditWritesSource | A two-way UI edit commits to the source, then `Update` |
| `NoOp` (every group) | Empty operation through the same probe brackets: the harness floor |

For `property-access` the size column is the chain depth, and the leaf sits at that depth, which is what the
invalidation walk costs. Per-element and per-row figures come from dividing a case by its element or row count; the
size column keeps that derivation visible. Every accumulated result is stored in a typed field — never `object` — so
the sink itself allocates nothing; the recorded `NoOp` floor is 0 bytes and 0 µs.

## Protocol

```powershell
pwsh -NoProfile -File Benchmarks/TrueMoon.Argentis.Benchmarks/RunBaseline.ps1
```

The runner builds Release, runs `--verify` (behavior of every scenario on one thread, so session owner-thread affinity
and binding semantics fail before any measurement), then executes one fresh process per group, size and pass:
42 processes for the default two passes (seven groups × three sizes). Per case it warms up until both
`-WarmupSamples` calls and `-WarmupMilliseconds` elapsed are satisfied, forces a full GC, then takes `-Samples`
(default 200) raw samples.
Median and P95 are nearest rank, matching K9. Passes are reported side by side and distributions are never merged; a
difference smaller than the spread between passes of the same case is not a result.

`TestResults/ViewBaseline/run-*` is ignored output: raw per-case sample arrays as JSON, per-probe stdout,
`summary.md` with the whole table and `summary.json` with git HEAD and dirty state, SDK and runtime list, source
SHA256 for `TrueMoon.Argentis`, `TrueMoon.Alloy` and this project plus `TrueMoon.slnx` and the shared props, binary
SHA256, GC/JIT environment values, power plan before and after. The checkout may be dirty: file fingerprints identify
the measured content. The run fails if a measured source changes during measurement, if a probe reuses a process, if
a group loses a method, if the `NoOp` floor allocates, or if the power plan changes. Do not edit measured inputs or
run builds, tests or other benchmarks concurrently; other desktop workloads remain uncontrolled.

Comparisons reuse the same scene, sizes, sample counts, runtime, hardware and power plan, and keep the P95 and
allocation columns. A result on this workstation does not establish performance on other hardware, OS, SDK or RID.

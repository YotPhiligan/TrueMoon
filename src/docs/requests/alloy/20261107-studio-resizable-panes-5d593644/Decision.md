# UI owner decision — 2026-11-09

State: **completed — framework/local delivery scope, 2026-11-09**.
Owner: `codex-alloy-split-view`, exclusive [Claim](Claim.json).
User selected this work; Cobalt requests belong to a separate session.
Single implementation task: [UIA3e](../../../alloy/PLAN.md#uia3e-split-view).

V1: retained two-pane `SplitView`, horizontal/vertical axis, preferred first-pane
size in logical pixels (NaN means half), constraints on the retained pane hosts.
Feasible min/max constraints are respected; insufficient space compresses minima
proportionally. Finite maxima leave unused trailing space. Host resize preserves
the preferred size. No collapse, persistence, file IO, or application policy.

The separator owns pointer capture until release; Escape/focus/capture loss,
hide/disable/detach/dispose cancel and restore the pre-drag preference. External
size changes interrupt without overwriting the new value. Tab + axis arrows,
Shift for larger steps, Home/End, accessible name and themed states are included.
Layout/theme changes retain children and their drafts. Native consumer and
package delivery have separate acceptance; physical input/DPI are not inferred
from routed input. Studio integration remains with the consumer owner.

Implementation: [SplitView](../../../../TrueMoon.Argentis/SplitView.cs),
[orientation](../../../../TrueMoon.Argentis/SplitOrientation.cs),
[guide](../../../alloy/SPLIT_VIEW.md),
[tests](../../../../TrueMoon.Alloy.Tests/SplitViewTests.cs),
[source demo and package runner](../../../../ManualTests/AlloyTest/README.md).
UI1224/Hosting154/Editor1412 passed in both Debug and Release, failed0/skipped0.
Source Debug/published Release OpenGL/Vulkan passed, registry0 and Vulkan
warnings/errors0. Three local packages `0.2.5.2-splitview.20261109.1` passed an
independent consumer with21checks in each Debug/Release. No external publication.
[Commands/scope/hashes](../../../alloy/HISTORY.md#split-view-2026-11-09).
Physical input/multi-monitor and actual Studio integration remain separate.

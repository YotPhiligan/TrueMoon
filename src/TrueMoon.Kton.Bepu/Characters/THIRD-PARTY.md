# BEPU capsule controller adaptation

`CharacterControllers.cs`, `CharacterMotionConstraint.cs` and the original
`CharacterMotionConstraint.tt` come from BEPUphysics2, commit
`29ed466870534c413aba9eeccd813cf15d135d0b` (BepuPhysics 2.5.0-beta.33),
`Demos/Demos/Characters/`. Upstream: https://github.com/bepu/bepuphysics2/tree/29ed466870534c413aba9eeccd813cf15d135d0b/Demos/Demos/Characters

Copyright BEPUphysics contributors. Distributed under Apache-2.0; the complete
upstream license is in `LICENSE.BEPU.txt`. No upstream NOTICE file was present
at this commit. These notices and the template are included in the provider package.

TrueMoon maintains this adaptation. Changes: internal types and TrueMoon namespace,
nullable context for imported source, expose the selected support normal, tolerate
cleanup before initialization, and use the **current support candidate** for jump
velocity/impulse rather than the previous frame's support. The motion-constraint
math is unchanged. The original template is retained for maintenance; regenerate
the constraint variants from it and then reapply the namespace/internal/header
changes. Do not edit one constraint variant without updating its counterpart.

The public, bounded profile, body borrowing/inertia restoration, validation,
world gate, discontinuity invalidation, and Aether bridge are maintained separately
by TrueMoon. Demo keyboard/camera/renderer code is not included.

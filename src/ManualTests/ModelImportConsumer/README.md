# Isolated model import acceptance

Run `pwsh -NoProfile -File ManualTests/ModelImportConsumer/Verify.ps1 -EvidenceDirectory <fresh-directory>`
from this workspace with its pinned SDK. Builds/tests are serialized. The script uses
`BuildProfile.ps1`, a closed local feed, separate source/fresh artifact trees and a
new package cache. Existing worker/project assets must have been restored once so
third-party archives can be copied from the recorded caches; network is not required
for the matrix itself.

The source-owned synthetic fixtures are linked from ModelImportResearch. Both DI
providers check exact triangles/winding, shared instances, local/world transforms,
linear-to-sRGB color, deleted source files, deterministic revision and grid bounds.
Unsupported and invalid native input must fail without a candidate. The separate fake
worker checks crash, damaged/oversized output, real Job commit denial, blocked deadline,
cancellation/admission, cleanup failure/retry and Stop. OS exit and drained-worker state
are asserted; these do not claim cancellation at a particular native instruction.

Ten profiles: eight source/published/fresh package × provider profiles using the same
Release worker, plus two Debug-worker profiles. Fresh consumers have zero project
references and no Assimp dependency; loaded framework DLLs must match the fresh cache.
The full Release worker closure/licenses, expected hashes and closed feed are retained
under the evidence directory. No packages are published to a remote feed.

See [API and limits](../../docs/engine/MODEL_IMPORT_WORKER.md). This is g.1b CPU acceptance;
stable IDs/ledger (g.1c), Nom/build/runtime delivery (g.2), GPU and other OS/AOT are separate.

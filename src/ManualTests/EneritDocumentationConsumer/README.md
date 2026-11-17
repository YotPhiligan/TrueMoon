# Enerit generated XML documentation consumer

From `src`, PowerShell 7: `./ManualTests/EneritDocumentationConsumer/Run.ps1`.
The helper captures the shared BuildProfile, packs six matching framework packages,
then restores a copied consumer using a new private feed/cache outside the source tree.
Exact package graph, analyzer path/SHA and unchanged source inputs are checked.

Debug/Release builds enable XML documentation and promote CS1591 to an error.
Two mutable DTOs exercise generated serializers, client, handler and module initializer.
Two App-owned Cobalt graphs call the generated service over a real same-process named
pipe, then stop and check client drain. Four calls/configuration include Unicode and
empty strings. Negative builds add an undocumented consumer method and must fail with
CS1591 at that method. NoWarn is not set. CS8669 is a separate existing backlog.

This is a Windows x64 focused package/generator acceptance. It does not claim a full
Studio build, separate-process restart, MMF, other OS, trimming or release readiness.
Raw logs/binlogs, package hashes and input hashes stay in the new TestResults directory;
the task's versioned outcome is recorded in `docs/enerit/HISTORY.md`.

# Process output file-worker probe

Core-only executable fixture for `TrueMoon.Tests`. It references Core/App and has
no Aether, Editor, platform or rendering dependency. The test build copies it to
`process-output-probe` in its output directory.

Build with a unique binlog, then run against a fresh evidence directory:

```powershell
dotnet build ManualTests/ProcessOutputProbe/ProcessOutputProbe.csproj -c Release '-bl:TestResults/process-output-probe-{}.binlog'
dotnet ManualTests/ProcessOutputProbe/bin/Release/net10.0/ProcessOutputProbe.dll --starvation TestResults/process-output-starvation
```

`--starvation` changes thread-pool settings only in this separate process. It
admits a 24-byte chunk, waits for its write, completes the reader, and holds the
only shared worker inside a controlled completion callback. FileCompletion and
Completion must finish within the unchanged two-second wait while the callback
is still held. The borrowed reader must stay undisposed. Finally, the controller
releases the callback, restores pool settings and awaits cleanup.

`--files <directory> <iterations>` checks two short captures per iteration;
`--app-files <directory> <iterations>` adds real App/JSON diagnostics. Iterations
are limited to 1–10000. These are content/lifecycle checks, not benchmarks.
On failure the process retains its state for 15 seconds for external collection.

The file-enabled capture uses one dedicated background worker, an eight-chunk
bounded queue and synchronous file IO. EOF and each admitted chunk wake it
directly. Pipe drainage never waits for file IO; overflow/IO failures retain their
existing counters. No worker is created when file capture is disabled. A caller
still owns its reader and must await capture Completion before releasing it.

The controlled starvation regression establishes a shared-worker dependency in
the old implementation. It does not prove the exact cause of every historical
intermittent timeout. See [Core status](../../docs/core/STATUS.md) and
[engine history](../../docs/engine/HISTORY.md).

# Enerit ENE5: same-build pipes baseline

Permanent cross-process workload before optimizations. It uses actual generated
client/handler/DTO codecs and a single PipesInvocationClient connection to an owned
server process. No App/DI/Mithril, package-consumer or MMF performance claim.

From src, Windows x64, PowerShell7 and pinned SDK10:

```powershell
pwsh -NoProfile -File Benchmarks/TrueMoon.Enerit.Benchmarks/RunBaseline.ps1
```

The optional RunDirectory must be new and inside TestResults/EneritBaseline.
Release framework-dependent publish is frozen. Cases: scalar int increment,
byte echoes of256/16384/65536bytes, DTO with int/Unicode string/16int list/1024byte
buffer. Each has concurrency1/8 through one shared connection. Inputs are prepared
outside measurement. Server performs real copies/transformation without Task.Delay;
input rentals are returned by service, result rentals by client. Every result is
verified, including all bytes/list values, with original payloads immutable.

Ten verify runs execute all shapes/concurrency combinations before measurements.
Thirty fresh client/server pairs cover3trials ×10cases, rotating the case order by3
positions per trial. Each pair warms for at least1.5s, pilots512calls, calibrates
roughly1s of calls/sample (512–131072), then takes3samples. A sample under250ms fails.
No startup/connect/serialization of report JSON or raw-array allocation is timed.
No forced GC, runtime/power-plan changes, delay emulation or replay.

Timing uses Stopwatch timestamps. Per-call RTT stops when typed await completes;
result validation and rental return follow. Batch calls/sec includes validation,
rental return, timestamps and worker overhead. Closed-loop1/8outstanding calls is
not an open-loop load test and does not provide coordinated-omission-free tails.
Percentiles use nearest rank; published case metrics are the median of9sample
metrics, not pooled confidence bounds or SLA. Throughput min/max remains available.

Allocations use GC.GetTotalAllocatedBytes(true), independently in both processes,
across all threads. Reported B/call includes transport/generated/service and small
harness overhead. Server begin/end control acknowledgement/read overhead and
finite-batch boundary cleanup are not subtracted. Values are allocated managed
bytes, not retained memory, native memory, exact wire bytes or per-thread BDN output.
Raw per-call ticks, sample windows/op counts, GC collections and allocation deltas
are preserved. There is no BenchmarkDotNet dependency for this cross-process runner.

Each pair validates exact server call counts, no measurement transport errors or
reconnects, pending0 and completed client workers after Dispose, child exit0/stderr
empty. EOF diagnostics during normal teardown are outside measurement. Parent and
runner reap only their owned process trees on timeout/failure, without user processes.

summary.json records source/generated/binary/report SHA256, SDK/runtime/GC/CPU,
commands, initial/final unchanged power plan,10verification/30measurement reports
and10case aggregates. Raw JSON and results.csv stay in the run directory.
Completed=true requires full execution and final source/binary checks. Other modules'
tests and historical package results are not evidence from this measurement run.
CPU frequency, background load and OS caches are not controlled; use the same
protocol/hardware for later comparisons and retain process-to-process spread.

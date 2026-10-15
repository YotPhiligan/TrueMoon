# Enerit pipes baseline — 2026-10-15

ENE5 initial measurement, workload v1. Runtime/generator/transport logic unchanged;
no optimization or before/after speedup claimed. [Protocol/runner](../../Benchmarks/TrueMoon.Enerit.Benchmarks/README.md),
[plan](PLAN.md#ene5-измерительный-контракт--2026-10-15), [status](STATUS.md).

Windows11 x64/build26300, Ryzen9 9900X12cores/24threads, SDK10.0.402/.NET10.0.13,
Release, Workstation GC/Interactive. Same-build generated typed client/handler/DTO,
one real named-pipe connection between two owned processes. App/DI/Mithril excluded.
Existing Ultimate Performance power scheme was read before/after and unchanged;
frequency/background tasks/caches were not controlled. Started12:06:42UTC,
finished12:09:06UTC. No tests/builds ran in parallel with timed samples.

10verification pairs passed;30measurement pairs (3fresh pairs/case ×3samples)
yielded90samples and571944measured RPCs, all results validated. Sample windows
0.456–1.294s; process warmup≥1.5s plus512-call calibration. Measurement clients and
servers exited0, no invocation errors/reconnects, pending0, client workers completed.

## Results

Each cell is the median of9sample metrics; these are3independent process pairs,
not9independent launches. RTT is nearest-rank per-sample percentile of typed await,
before validation/rental return. Calls/s includes validation/rental return and
clock/worker overhead. c is outstanding calls through one connection.

| Shape | c | RTT P50 µs | RTT P95 µs | Calls/s median | Calls/s min–max | Client B/call | Server B/call |
| --- | --- | --- | --- | --- | --- | --- | --- |
| scalar int |1|183.9|328.7|4896|4041–5685|2512|3098|
| scalar int |8|779.3|2062.1|9062|5307–9772|2214|1585|
| bytes256 |1|178.2|289.2|5239|2940–5531|2683|3531|
| bytes256 |8|754.1|2049.1|9193|8785–9735|2328|2023|
| bytes16384 |1|180.0|292.0|5269|4117–5451|2676|19661|
| bytes16384 |8|891.1|2567.8|7608|5516–9625|2344|18164|
| bytes65536 |1|181.4|312.1|5070|4260–5269|2687|68819|
| bytes65536 |8|808.6|2167.2|8664|8075–9212|2346|67324|
| DTO +1024byte buffer |1|180.1|282.2|5199|3837–5528|2986|4872|
| DTO +1024byte buffer |8|783.4|2066.0|9040|7625–9787|2638|3343|

DTO additionally contains int, Unicode string and16int list; its name is the byte
buffer size, not serialized frame length. Byte echo intentionally copies service
input to a new result array; its server allocation includes that payload-sized
array. DTO also builds transformed string/list/output bytes. No artificial delay.
Input rentals are returned by service, response/nested rentals by caller.

## Interpretation and limits

- Scalar concurrency8 gives approximately1.85× median throughput with4.24× RTT P50
  versus concurrency1. This is closed-loop multiplexing/queueing, not8× capacity
  or a real-time latency guarantee. P95 here is not open-loop/SLA tail latency.
- Within these cases, c1 throughput is broadly similar across payload sizes.
  Scheduling/transport overhead is a plausible contributor, **an inference** from
  this scale comparison; no CPU trace establishes its share. The non-monotonic
  c8 results and broad min/max prohibit claiming a precise bandwidth curve.
- Client allocates roughly2.2–3.0KB/call across all managed threads. Server cost
  depends on service work and concurrency;64KiB echo includes an intentional64KiB
  result copy. These numbers are not transport-only allocations or leaks.
- Counters are GC.GetTotalAllocatedBytes(true) process deltas divided by calls,
  with framework/service/harness and server control/batch-boundary costs included.
  Raw latency arrays/report serialization/startup are outside the client window.
  GC collections remain in raw data; no forced collections or overhead subtraction.
- Scalar/DTO/bytes success paths only. Cancellation/disconnect/errors have
  functional regression tests, not performance measurements here. MMF, App/DI,
  process restart, packages, other OS/SDK/RID/AOT, native/retained memory, wire
  bandwidth, open-loop overload/backpressure and game/editor frames are unmeasured.

## Evidence and continuation

Run: TestResults/EneritBaseline/run-20261015-120642176, summary Completed=true.
42commands: publish/list,10verify,30measure.104source files,10published binaries,
4generated sources, all raw reports have SHA256; initial/final power plan matches.
results.csv retains unrounded medians and ranges; each measurement JSON includes
raw Stopwatch ticks, operation counts, windows, client/server allocation deltas
and GC counts. The report files and DLLs are frozen for later comparisons.

The existing ENE5 package portion is a separate2026-10-14 proof, not rerun by
these measurements. Current runtime/generator regression and full build commands
are recorded in [HISTORY](HISTORY.md#ene5-pipes-performance-baseline--2026-10-15).
Next performance investigation: profile allocation/CPU on scalar c1/c8 to separate
CTS/task/generated/transport costs before editing them. Keep this workload unchanged
for comparisons. This is a measured backlog, not a newly confirmed correctness
blocker or a requirement to optimize every path before Mithril MIT5 diagnostics.

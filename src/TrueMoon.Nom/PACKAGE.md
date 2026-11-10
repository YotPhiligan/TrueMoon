# TrueMoon.Nom

Standalone asset storage for .NET 10. Transactional Key-Value metadata, streamed
immutable content-addressed resources, exact dependency queries, frozen snapshots,
durable pins, explicit checkpoint/GC and verified directory backup/restore.
The package depends only on **TrueMoon.Contracts**. It has no Aether, UI, GPU,
importer, codec or database-provider dependency.

## Namespace migration

Source folders now define functional namespaces: `TrueMoon.Nom.Catalog` for records
and queries, `.Resources` for resource versions/tickets/streams and byte/raw helpers,
`.Retention` for snapshots/GC, `.Backup` for backup/restore, `.Hosting` for NomOptions
and NomStoreHost, and `.Storage` for compaction. Internal format/recovery lives under
`.Storage.Format`. The general store interface, open arguments/mode and `UseNom`
entry point remain in `TrueMoon.Nom`.

This changes fully qualified names in the alpha API. Recompile consumers and add
the relevant imports; previous compiled binaries require rebuilding. No old-namespace
type aliases are shipped. Store formats, resource bytes and lifecycle behavior are
preserved; runtime CLR type names are not part of the Nom storage encoding.

## Connect to an application

Reference `TrueMoon.Nom` and `TrueMoon` at the same package version. Use the .NET 10
SDK, including the compiler required by Cobalt's bundled generator.

```csharp
using TrueMoon;
using TrueMoon.Dependencies;
using TrueMoon.Nom;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;

await using var app = App.Build(context => context.UseNom());
await app.StartAsync();

// Resolve factories in the composition root; inject them into application services.
var stores = app.Services.Resolve<IFactory<IProjectAssetStore, OpenAssetStoreArguments>>()!;
var projectId = Guid.Parse("f0f2675a-a537-4321-abdc-21df2611fce2");
var storeId = Guid.Parse("8e7c8dfb-3fde-40d6-b131-4a56dc3c86a2");
var root = Path.GetFullPath("assets.nom");
await using var store = stores.Create(new(root, projectId, storeId, AssetStoreOpenMode.Create));

var asset = new AssetRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
    "shaders/example", Array.Empty<byte>(), tags: new[] { "shader" });
var bytes = "opaque shader source"u8.ToArray();
var ticket = await store.ReserveResourcesAsync(store.ContentGeneration,
    new[] { new AssetResourceWrite(asset, bytes.Length, MaximumBytes: bytes.Length) });
using var input = new MemoryStream(bytes, writable: false);
var committed = await store.PublishResourcesAsync(ticket,
    new Dictionary<Guid, Stream> { [asset.Id] = input });

await using var read = await store.OpenResourceAsync(committed.Resources.Single());
// Read the owned, verified, seekable stream; close it before the store/App.
```

`Create` requires a new/empty root. Persist the project/store GUIDs and use `Open`
when reopening it. Open never replaces an unknown/corrupt root with an empty store.
Legacy database roots are rejected; implicit migration is not provided.

## Ownership and consistency

`UseNom` validates and copies options and registers one App-owned `NomStoreHost`.
Build/start opens no store. Factory products, snapshot handles and read streams
are caller-owned; dispose them when their project/session closes, before the App.
The host retains failed/live owners until actual IO/cleanup finishes. A timeout
does not release their root lock; finish IO/return leases, then retry store/host cleanup.
Factories are borrowed DI services and must not be disposed by consumers.

Inputs are borrowed: Nom never seeks or disposes them. Publication checks lengths
and SHA256 and commits the whole metadata batch. Unknown commit acknowledgement
requires close/reopen and `LookupResourceOperationAsync(ticket)`; never replay an
expired identity. `OpenResourceAsync` verifies the complete object on the same handle
before returning a stream. It does not promise immediate first-chunk delivery.

Logical IDs differ from exact resource revisions and CAS byte digests. Query pages
use exact source/kind/ordinal tag filters. Continuations belong to a complete catalog
sequence and become stale after metadata changes. Dependencies name exact versions.
Snapshots freeze their dependency closure; explicit durable pins survive reopen.
Closing a snapshot stops new opens; existing streams protect it until actual close.

## Byte buffers

`AssetStoreBufferExtensions` adds byte helpers without changing `IProjectAssetStore`
or the storage format. Reserve the complete batch first, as with streaming:

```csharp
using TrueMoon.Nom.Resources;

var next = await store.ReserveResourcesAsync(store.ContentGeneration,
    new[] { new AssetResourceWrite(asset, bytes.Length, MaximumBytes: bytes.Length) });
var result = await store.PublishResourceAsync(next, asset.Id, bytes.AsMemory());
var exact = result.Resources.Single();
var destination = new byte[bytes.Length + 16];
var count = await store.ReadResourceAsync(exact, destination.AsMemory());
ReadOnlyMemory<byte> detached = await store.LoadResourceAsync(exact, maximumBytes: bytes.Length);
```

Publication borrows `ReadOnlyMemory<byte>` (including a writable `Memory<byte>` view)
until the returned task actually finishes. Do not mutate, return to a pool or dispose
its backing owner sooner; requesting cancellation is insufficient. Nom never disposes
that owner. Non-array/non-pinnable memory and slices are supported. A complete batch
uses `IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>>`; its descriptors are captured
before the first await. Overlapping immutable views are allowed. A dictionary of
`Memory<byte>` does not implicitly convert to this dictionary type.

For short-lived `Span<byte>`/`ReadOnlySpan<byte>`, call
`PublishResourceCopyAsync(ticket, assetId, span, maximumCopyBytes)`. This non-async
entry point validates and copies before returning its task, so the caller can reuse
the span immediately. Every call copies, including committed replay; memory replay
does not read its source. The single-resource helpers require a matching single-resource
ticket and never publish part of a batch. Invalid pending sets follow the stream
publisher's existing abort policy.

Read requires room for the complete exact resource; rejection leaves the destination
unchanged, success preserves its unused tail, and entered IO failure/cancellation may
leave a partial copy. Read uses at most64KiB of rented IO memory and closes its verified
stream before completion. Load allocates a separate array after verified open, bounded
by the required positive `maximumBytes`; its read-only view survives store/App disposal
and holds no lease. Read-only memory is not an immutability security boundary.
Copy/load ceilings must be positive and at most512MiB, even for empty resources.
The byte API stores bytes without serialization.

## Explicit unmanaged raw data

`AssetStoreRawBufferExtensions` provides `PublishRawResourceAsync<T>`,
`PublishRawResourceCopyAsync<T>` and `ReadRawResourceAsync<T>`, all constrained to
`where T : unmanaged`. Writable/read-only Memory and Span inputs have forwarding
overloads for generic inference. Use `.AsMemory()`/`.AsSpan()` views or an explicit
type argument for arrays. The raw helpers require a matching single-resource ticket;
heterogeneous atomic batches continue to use the byte/Stream API.

```csharp
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;

var vertices = new float[] { 1, 2, 3, 4, 5, 6 };
var rawAsset = new AssetRecord(Guid.NewGuid(), asset.Source, asset.Kind, "raw/float-v1", []);
var byteCount = checked(vertices.Length * sizeof(float));
var rawTicket = await store.ReserveResourcesAsync(store.ContentGeneration,
    new[] { new AssetResourceWrite(rawAsset, byteCount, MaximumBytes: byteCount) });
var rawVersion = (await store.PublishRawResourceAsync(rawTicket, rawAsset.Id, vertices.AsMemory())).Resources.Single();
var output = new float[vertices.Length + 4];
int elements = await store.ReadRawResourceAsync(rawVersion, output.AsMemory());
```

Element count times runtime `sizeof(T)` is checked in `long` before byte projection
or allocation. Publication remains capped at512MiB and copy budgets are **bytes**,
not elements. `PublishRawResourceCopyAsync(ticket, id, span, maximumCopyBytes)` copies
synchronously on every call, including replay. Borrowed memory follows the same
actual-task-completion and no-caller-disposal/pinning rules as the byte API.

Read rejects negative/oversized or non-divisible exact byte lengths and short
destinations before opening or modifying memory. It returns **element count**,
preserves the unused tail and supports non-pinnable typed destinations. Entered
failure/cancellation can leave partial bytes, including part of one element; it
does not return success. Empty reads still verify and close the resource stream.

Raw bytes contain native layout, endianness and padding. Choose and version the
resource kind/metadata accordingly and initialize padding deterministically; it
participates in the digest/revision. There is no conversion, portable serialization
or runtime validation of element values. Managed references are rejected by the
compiler. For read-only typed data, load detached bytes, validate divisibility, then
use `MemoryMarshal.Cast<byte, T>(loaded.Span)`; writable synchronous typed reads can
use `ReadExactly(MemoryMarshal.AsBytes(destinationSpan))` on a verified stream.

## Explicit maintenance and backup

Finish reservations/close read streams before `CompactAsync` or GC. Compaction
checkpoints metadata without changing content generation. GC uses a dry-run plan,
atomic prune/deletion intents and bounded slices; `HasMore` requires a new plan.
`CollectGarbageAsync()` without a plan resumes durable pending cleanup. It does not
discover new garbage. Automatic scheduling, PID/age pin expiration and background
recursive deletion are not performed.

`ExportBackupAsync(absentBackupRoot)` verifies an immutable directory containing
selected metadata and all stored revision bytes. Restore through the App-provided
`IFactory<IProjectAssetStore, RestoreAssetStoreArguments>` into an absent root:

```csharp
using TrueMoon.Nom;
using TrueMoon.Nom.Backup;

var backup = await store.ExportBackupAsync(absoluteBackupRoot);
var restores = app.Services.Resolve<IFactory<IProjectAssetStore, RestoreAssetStoreArguments>>()!;
await using var restored = restores.Create(new RestoreAssetStoreArguments(
    backup.Root, absentRestoreRoot, store.ProjectId, store.StoreId));
```

Close the read stream from the first example before exporting. Restore preserves
IDs, revisions, operation floor, snapshots/pins and generation. It verifies prepared
files/domain references before directory publication and never overwrites a destination.
Failed prepared directories remain for diagnosis. Unknown future formats need an
explicit offline converter; backup/restore does not silently upgrade them.

## Limits and platform scope

The verified delivery scope is local Windows x64 with .NET 10, using portable and
`win-x64` framework-dependent consumers. The package is managed `net10.0`/AnyCPU;
other OS/architectures and self-contained/AOT/trimming modes require separate acceptance.
No native payload is shipped. Network/multi-process writers and hostile/reparse
filesystems are outside the storage profile.

Defaults include64KiB IO buffers,512MiB/object,1GiB/resource batch,256affected assets,
4MiB atomic metadata payload,16read streams,64snapshot handles,4096closure members,
2048GC revision/object candidates per slice and256MiB/checkpoint. Domain index admission
defaults to64MiB logical accounting and can be explicitly configured up to256MiB through
`NomOptions.DomainIndexBytes`; this is not an RSS or physical disk-space reservation.
Full domain index preparation on content commit has a measurable cost on large catalogs.

The API/package is alpha. Isolated-volume Windows/NTFS disk-full recovery has been
checked separately; it does not prove power-loss durability. Actual guest-VM crash
verification is deferred, and hardware power-off is excluded from current acceptance
by the user. This package does not import models, decode images/audio, compile shaders
or own GPU/voice resources.

## Unified admission settings

Add `NomConfigurationSchema.Default` (`Nom`) to the App profile, then call
`UseConfiguredNom(configure)`. Store/queue/batch/document/index limits and drain
budget are startup-only, validated and copied before registration. Startup opens
no store; `OpenAssetStoreArguments` still supplies each project's identity/root,
and the caller closes each factory product. Existing `UseNom` remains available.

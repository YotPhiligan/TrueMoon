using System.Buffers;
using System.Collections;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TrueMoon.Enerit.IO;

namespace TrueMoon.Enerit.Generator.Tests;

public class GeneratedDecodeOwnershipTests
{
    private static readonly Lazy<Type> Fixture = new(Compile);
    private static readonly string[] Names = ["scalar", "nullable", "boolean", "text", "bytes", "readonly-bytes", "dto", "nested", "array", "list", "raw-list", "map", "raw-map", "async-dto", "async-scalar", "key-map"];
    public static IEnumerable<object[]> Shapes => Names.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task GeneratedResponses_ConsumeExactlyAndTransferWholeRentalsToCaller(string shape)
    {
        Reset();
        object? result = null;
        try
        {
            result = await ReadAsync(shape, Payload(shape));
            switch (shape)
            {
                case "scalar" or "async-scalar": Assert.Equal(73, Assert.IsType<int>(result)); break;
                case "nullable": Assert.Equal(-27, Assert.IsType<int>(result)); break;
                case "boolean": Assert.True(Assert.IsType<bool>(result)); break;
                case "text": Assert.Equal("Луна 🌙", Assert.IsType<string>(result)); break;
                case "dto" or "async-dto": Assert.Equal(101, Property(result!, "Tag")); break;
                case "nested":
                    Assert.Equal(73, Property(result!, "Id"));
                    Assert.Equal(101, Property(Property(result!, "Child")!, "Tag"));
                    Assert.Equal(202, Property(Property(result!, "Tail")!, "Tag"));
                    break;
                case "array" or "list":
                    var items = ((IEnumerable)result!).Cast<object?>().ToArray();
                    Assert.Equal(3, items.Length); Assert.Null(items[1]);
                    Assert.Equal(101, Property(items[0]!, "Tag")); Assert.Equal(202, Property(items[2]!, "Tag"));
                    break;
                case "key-map":
                    var keyMap = (IDictionary)result!;
                    Assert.Equal(2, keyMap.Count);
                    Assert.Equal(new object?[] { 101, 202 }, keyMap.Keys.Cast<object>().Select(key => Property(key, "Tag")).ToArray());
                    Assert.Equal(new object?[] { 11, 22 }, keyMap.Values.Cast<object?>().ToArray());
                    break;
                case "map":
                    var map = (IDictionary)result!;
                    Assert.Equal(3, map.Count); Assert.Null(map["none"]);
                    Assert.Equal(101, Property(map["first"]!, "Tag")); Assert.Equal(202, Property(map["second"]!, "Tag"));
                    break;
            }
            var rentals = Rentals(result).ToArray();
            var count = shape switch { "bytes" or "readonly-bytes" or "dto" or "async-dto" => 1, "nested" or "array" or "list" or "map" or "key-map" or "raw-list" or "raw-map" => 2, _ => 0 };
            Assert.Equal(count, rentals.Length);
            if (count > 0) Assert.Equal(new byte[] { 7, 11, 23 }, rentals[0].ToArray());
            if (count > 1) Assert.Equal(new byte[] { 29 }, rentals[1].ToArray());
            foreach (var rental in rentals) { Assert.True(rental.TryReturn()); Assert.False(rental.TryReturn()); }
        }
        finally { foreach (var rental in Rentals(result).Concat(Captured().Select(memory => (ReadOnlyMemory<byte>)memory))) rental.TryReturn(); }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task GeneratedResponses_RejectTrailingBytesAndRollBackNestedRentals(string shape)
    {
        Reset();
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(shape, [.. Payload(shape), 255]));
            AssertReturnedCaptured(shape is "dto" or "async-dto" ? 1 : shape is "nested" or "array" or "list" or "map" or "key-map" ? 2 : 0);
        }
        finally { ReturnCaptured(); }
    }

    [Theory]
    [InlineData("dto")]
    [InlineData("nested")]
    [InlineData("array")]
    [InlineData("list")]
    [InlineData("map")]
    [InlineData("key-map")]
    [InlineData("async-dto")]
    public async Task GeneratedResponses_LaterTruncationReturnsPreviouslyDecodedBytes(string shape)
    {
        Reset();
        try
        {
            var payload = Payload(shape);
            // Map ends with a null-valued third entry; use a two-entry payload for final DTO truncation.
            if (shape == "map") payload = MapPayload(twoEntries: true);
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(shape, payload[..^1]));
            AssertReturnedCaptured(shape is "dto" or "async-dto" ? 1 : 2);
        }
        finally { ReturnCaptured(); }
    }

    [Theory]
    [InlineData("nullable")]
    [InlineData("text")]
    [InlineData("dto")]
    [InlineData("nested")]
    [InlineData("array")]
    [InlineData("list")]
    [InlineData("raw-list")]
    [InlineData("map")]
    [InlineData("key-map")]
    [InlineData("raw-map")]
    [InlineData("async-dto")]
    public async Task GeneratedResponses_NullConsumesItsMarkerAndRejectsExtraByte(string shape)
    {
        Reset();
        Assert.Null(await ReadAsync(shape, new byte[] { 0 }));
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(shape, new byte[] { 0, 1 }));
        Assert.Empty(Captured());
    }

    [Theory]
    [InlineData("array")]
    [InlineData("list")]
    [InlineData("raw-list")]
    [InlineData("map")]
    [InlineData("key-map")]
    [InlineData("raw-map")]
    public async Task GeneratedResponses_EmptyCollectionIsDistinctFromNull(string shape)
    {
        Reset();
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.WriteInstanceState(true, writer); SerializationUtils.Write(0, writer);
        var result = await ReadAsync(shape, writer.WrittenSpan.ToArray());
        Assert.NotNull(result);
        Assert.Empty((IEnumerable)result);
        Assert.Empty(Captured());
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("readonly-bytes")]
    public async Task GeneratedResponses_EmptyBytesRemainAnOwnedWholeRental(string shape)
    {
        Reset();
        var result = await ReadAsync(shape, BitConverter.GetBytes(0));
        var rental = Assert.Single(Rentals(result));
        try { Assert.Equal(0, rental.Length); Assert.True(rental.TryReturn()); Assert.False(rental.TryReturn()); }
        finally { rental.TryReturn(); }
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("null")]
    public async Task GeneratedDictionary_InvalidKeyReturnsBothDecodedValueRentals(string key)
    {
        Reset();
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync("map", MapPayload(twoEntries: true, invalidKey: key)));
            AssertReturnedCaptured(2);
        }
        finally { ReturnCaptured(); }
    }

    [Fact]
    public async Task GeneratedDto_ThrowingSetterReturnsRentalAndPreservesOriginalError()
    {
        Reset(); Fixture.Value.GetProperty("ThrowSetter")!.SetValue(null, true);
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ReadAsync("dto", Payload("dto")));
            Assert.Same(Fixture.Value.GetProperty("SetterError")!.GetValue(null), error);
            AssertReturnedCaptured(1);
        }
        finally { ReturnCaptured(); }
    }

    [Fact]
    public void PublicDtoReader_ConsumesPrefixAtNonzeroOffsetAndTransfersOwnership()
    {
        Reset();
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.Write(41, writer); Blob(writer, 101, [7, 11, 23]); SerializationUtils.Write(93, writer);
        var result = (object[])Fixture.Value.GetMethod("ReadPrefix")!.Invoke(null, [writer.WrittenSpan.ToArray()])!;
        try
        {
            Assert.Equal(101, Property(result[0], "Tag")); Assert.Equal(16, result[1]); Assert.Equal(93, result[2]);
            var memory = Assert.Single(Captured()); Assert.Equal(new byte[] { 7, 11, 23 }, memory.ToArray()); Assert.True(memory.TryReturn());
        }
        finally { ReturnCaptured(); }
    }

    [Fact]
    public void PublicDtoReader_FailedDecodeReturnsItsPartialRentals()
    {
        Reset();
        try
        {
            var error = Assert.Throws<TargetInvocationException>(() => Fixture.Value.GetMethod("ReadPrefix")!.Invoke(null, [new byte[] { 0, 0, 0, 0 }.Concat(Payload("dto")[..^1]).ToArray()]));
            Assert.IsType<InvalidDataException>(error.InnerException); AssertReturnedCaptured(1);
        }
        finally { ReturnCaptured(); }
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("trailing")]
    public async Task GeneratedHandler_InvalidLaterArgumentDoesNotDispatchAndReturnsEarlierRentals(string invalid)
    {
        Reset();
        try
        {
            var payload = SubmitPayload();
            payload = invalid == "truncated" ? payload[..^1] : [.. payload, 255];
            var result = await HandleAsync(payload);
            Assert.False(result.Item1); Assert.IsType<InvalidDataException>(result.Item2);
            Assert.Equal(0, Fixture.Value.GetProperty("Calls")!.GetValue(null)); AssertReturnedCaptured(2);
        }
        finally { ReturnCaptured(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedHandler_ValidatedArgumentsTransferOwnershipBeforeServiceEntry(bool throws)
    {
        Reset(); Fixture.Value.GetProperty("ThrowService")!.SetValue(null, throws);
        try
        {
            var result = await HandleAsync(SubmitPayload());
            Assert.Equal(!throws, result.Item1);
            if (throws) Assert.Same(Fixture.Value.GetProperty("ServiceError")!.GetValue(null), result.Item2); else Assert.Null(result.Item2);
            Assert.Equal(1, Fixture.Value.GetProperty("Calls")!.GetValue(null));
            Assert.Equal(83, Fixture.Value.GetProperty("Marker")!.GetValue(null));
            var captured = Captured(); Assert.Equal(2, captured.Length);
            Assert.Equal(new byte[] { 7, 11, 23 }, captured[0].ToArray()); Assert.Equal(new byte[] { 29 }, captured[1].ToArray());
            foreach (var memory in captured) Assert.True(memory.TryReturn());
        }
        finally { ReturnCaptured(); }
    }

    [Fact]
    public async Task GeneratedResponse_EmptyStringConsumesItsHeaderAndRejectsTrailingByte()
    {
        Reset();
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.WriteString("", writer);
        Assert.Equal(5, writer.WrittenCount);
        Assert.Equal("", await ReadAsync("text", writer.WrittenSpan.ToArray()));
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync("text", [.. writer.WrittenSpan.ToArray(), 0]));
        Assert.Empty(Captured());
    }

    [Theory]
    [InlineData("boolean", new byte[] { 2 })]
    [InlineData("boolean", new byte[] { 255 })]
    [InlineData("nullable", new byte[] { 2 })]
    [InlineData("text", new byte[] { 2 })]
    [InlineData("dto", new byte[] { })]
    [InlineData("dto", new byte[] { 2 })]
    [InlineData("array", new byte[] { 255 })]
    [InlineData("bytes", new byte[] { 255, 255, 255, 255 })]
    [InlineData("scalar", new byte[] { 1, 2, 3 })]
    public async Task GeneratedResponses_InvalidScalarOrPresenceFailsBeforeReturningValue(string shape, byte[] payload)
    {
        Reset();
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(shape, payload));
        Assert.Empty(Captured());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterDtoConstructorFailureReturnsEarlierRentalsAndPreservesOriginalError(bool request)
    {
        Reset(); Fixture.Value.GetProperty("ThrowConstructorAt")!.SetValue(null, 2);
        try
        {
            var expected = (Exception)Fixture.Value.GetProperty("ConstructorError")!.GetValue(null)!;
            if (request)
            {
                var result = await HandleAsync(SubmitPayload());
                Assert.False(result.Item1); Assert.Same(expected, result.Item2);
                Assert.Equal(0, Fixture.Value.GetProperty("Calls")!.GetValue(null));
            }
            else Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => ReadAsync("nested", Payload("nested"))));
            AssertReturnedCaptured(1);
        }
        finally { ReturnCaptured(); }
    }

    private static object? Property(object value, string name) => value.GetType().GetProperty(name)!.GetValue(value);
    private static void Reset() => Fixture.Value.GetMethod("Reset")!.Invoke(null, null);
    private static Memory<byte>[] Captured() => ((IEnumerable<Memory<byte>>)Fixture.Value.GetProperty("Captured")!.GetValue(null)!).ToArray();
    private static void ReturnCaptured() { foreach (var memory in Captured()) memory.TryReturn(); }
    private static void AssertReturnedCaptured(int count)
    {
        var captured = Captured(); Assert.Equal(count, captured.Length);
        foreach (var memory in captured) { Assert.Throws<ObjectDisposedException>(() => _ = memory.Span.Length); Assert.False(memory.TryReturn()); }
    }
    private static Task<object?> ReadAsync(string shape, byte[] payload) => (Task<object?>)Fixture.Value.GetMethod("ReadAsync")!.Invoke(null, [shape, payload])!;
    private static Task<(bool, Exception?)> HandleAsync(byte[] payload) => (Task<(bool, Exception?)>)Fixture.Value.GetMethod("HandleAsync")!.Invoke(null, [payload])!;
    private static IEnumerable<ReadOnlyMemory<byte>> Rentals(object? value)
    {
        if (value is Memory<byte> memory) { yield return memory; yield break; }
        if (value is ReadOnlyMemory<byte> readOnly) { yield return readOnly; yield break; }
        if (value is IDictionary dictionary) { foreach(var item in dictionary.Keys.Cast<object>().Concat(dictionary.Values.Cast<object>())) foreach(var rental in Rentals(item)) yield return rental; yield break; }
        if (value is IEnumerable items && value is not string) { foreach(var item in items) foreach(var rental in Rentals(item)) yield return rental; yield break; }
        if (value?.GetType().FullName == "Consumer.Blob") { yield return (Memory<byte>)Property(value, "Bytes")!; yield break; }
        if (value?.GetType().FullName == "Consumer.Root") { foreach(var name in new[] { "Child", "Tail" }) foreach(var rental in Rentals(Property(value, name))) yield return rental; }
    }
    private static void Blob(IBufferWriter<byte> writer, int tag, byte[] bytes)
    {
        SerializationUtils.WriteInstanceState(true, writer); SerializationUtils.WriteBytes(bytes, writer); SerializationUtils.Write(tag, writer);
    }
    private static byte[] SubmitPayload()
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        Blob(writer, 101, [7, 11, 23]); Blob(writer, 202, [29]); SerializationUtils.Write(83, writer);
        return writer.WrittenSpan.ToArray();
    }
    private static byte[] MapPayload(bool twoEntries = false, string? invalidKey = null)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.WriteInstanceState(true, writer); SerializationUtils.Write(twoEntries ? 2 : 3, writer);
        SerializationUtils.WriteString("first", writer); Blob(writer, 101, [7, 11, 23]);
        SerializationUtils.WriteString(invalidKey == "null" ? null : invalidKey == "duplicate" ? "first" : "second", writer); Blob(writer, 202, [29]);
        if (!twoEntries) { SerializationUtils.WriteString("none", writer); SerializationUtils.WriteInstanceState(false, writer); }
        return writer.WrittenSpan.ToArray();
    }
    private static byte[] Payload(string shape)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        switch(shape)
        {
            case "scalar" or "async-scalar": SerializationUtils.Write(73, writer); break;
            case "nullable": SerializationUtils.WriteInstanceState(true, writer); SerializationUtils.Write(-27, writer); break;
            case "boolean": SerializationUtils.Write(true, writer); break;
            case "text": SerializationUtils.WriteString("Луна 🌙", writer); break;
            case "bytes" or "readonly-bytes": SerializationUtils.WriteBytes(new byte[] { 7, 11, 23 }, writer); break;
            case "dto" or "async-dto": Blob(writer, 101, [7, 11, 23]); break;
            case "nested": SerializationUtils.WriteInstanceState(true, writer); SerializationUtils.Write(73, writer); Blob(writer, 101, [7, 11, 23]); Blob(writer, 202, [29]); break;
            case "array" or "list":
                SerializationUtils.WriteInstanceState(true, writer); SerializationUtils.Write(3, writer);
                Blob(writer, 101, [7, 11, 23]); SerializationUtils.WriteInstanceState(false, writer); Blob(writer, 202, [29]); break;
            case "raw-list":
                SerializationUtils.WriteInstanceState(true, writer); SerializationUtils.Write(2, writer);
                SerializationUtils.WriteBytes(new byte[] { 7, 11, 23 }, writer); SerializationUtils.WriteBytes(new byte[] { 29 }, writer); break;
            case "key-map":
                SerializationUtils.WriteInstanceState(true, writer); SerializationUtils.Write(2, writer);
                Blob(writer, 101, [7, 11, 23]); SerializationUtils.Write(11, writer);
                Blob(writer, 202, [29]); SerializationUtils.Write(22, writer); break;
            case "map": return MapPayload();
            case "raw-map":
                SerializationUtils.WriteInstanceState(true, writer); SerializationUtils.Write(2, writer);
                SerializationUtils.WriteString("first", writer); SerializationUtils.WriteBytes(new byte[] { 7, 11, 23 }, writer);
                SerializationUtils.WriteString("second", writer); SerializationUtils.WriteBytes(new byte[] { 29 }, writer); break;
            default: throw new ArgumentOutOfRangeException(nameof(shape));
        }
        return writer.WrittenSpan.ToArray();
    }

    private static Type Compile()
    {
        const string assemblyName = "DecodeOwnershipFixture";
        var source = Source.Replace("ASSEMBLY", assemblyName);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(IInvocationClient).Assembly.Location).Append(typeof(TrueMoon.Diagnostics.IEventsSource<>).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(assemblyName, [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ServicesGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Null(Assert.Single(driver.GetRunResult().Results).Exception);
        using var output = new MemoryStream();
        var emission = generated.Emit(output);
        Assert.True(emission.Success, string.Join(Environment.NewLine, emission.Diagnostics));
        return Assembly.Load(output.ToArray()).GetType("Consumer.Probe")!;
    }

    private const string Source = """
        #nullable enable
        global using System;
        global using System.Buffers;
        global using System.Collections.Generic;
        global using System.Threading;
        global using System.Threading.Tasks;
        using System.Diagnostics;
        using TrueMoon.Enerit.IO;
        using TrueMoon.Diagnostics;
        using TrueMoon.Generated.ASSEMBLY;
        namespace Consumer;
        public class Blob
        {
            public Blob() { if (++Probe.Constructors == Probe.ThrowConstructorAt) throw Probe.ConstructorError; }
            private Memory<byte> _bytes;
            public Memory<byte> Bytes
            {
                get => _bytes;
                set { _bytes = value; Probe.Captured.Add(value); if(Probe.ThrowSetter) throw Probe.SetterError; }
            }
            public int Tag { get; set; }
        }
        public class Root { public int Id { get; set; } public Blob? Child { get; set; } public Blob? Tail { get; set; } }
        public interface ITestService
        {
            int Scalar(); int? Nullable(); bool Boolean(); string? Text(); Memory<byte> Bytes(); ReadOnlyMemory<byte> ReadOnlyBytes();
            Blob? Dto(); Root? Nested(); Blob?[]? Array(); List<Blob?>? List(); IReadOnlyList<Memory<byte>>? RawList();
            Dictionary<string, Blob?>? Map(); IReadOnlyDictionary<string, Memory<byte>>? RawMap(); Task<Blob?> AsyncDto();
            Task<int> AsyncScalar(int __tmDecodeScope); Task Submit(Blob? first, Blob? second, int marker, CancellationToken token = default); Dictionary<Blob, int>? KeyMap();
        }
        public sealed class Service : ITestService
        {
            public int Scalar() => 0; public int? Nullable() => null; public bool Boolean() => false; public string? Text() => null;
            public Memory<byte> Bytes() => default; public ReadOnlyMemory<byte> ReadOnlyBytes() => default;
            public Blob? Dto() => null; public Root? Nested() => null; public Blob?[]? Array() => null; public List<Blob?>? List() => null;
            public IReadOnlyList<Memory<byte>>? RawList() => null; public Dictionary<string, Blob?>? Map() => null;
            public IReadOnlyDictionary<string, Memory<byte>>? RawMap() => null; public Task<Blob?> AsyncDto() => Task.FromResult<Blob?>(null);
            public Task<int> AsyncScalar(int __tmDecodeScope) => Task.FromResult(0);
            public Task Submit(Blob? first, Blob? second, int marker, CancellationToken token = default)
            { Probe.Calls++; Probe.Marker = marker; if(Probe.ThrowService) throw Probe.ServiceError; return Task.CompletedTask; }
            public Dictionary<Blob, int>? KeyMap() => null;
        }
        public static class Probe
        {
            public static List<Memory<byte>> Captured { get; } = new();
            public static int Constructors { get; set; } public static int ThrowConstructorAt { get; set; }
            public static Exception ConstructorError { get; } = new InvalidOperationException("constructor-threw");
            public static bool ThrowSetter { get; set; } public static bool ThrowService { get; set; }
            public static Exception SetterError { get; } = new InvalidOperationException("setter-threw");
            public static Exception ServiceError { get; } = new InvalidOperationException("service-threw");
            public static int Calls { get; set; } public static int Marker { get; set; }
            public static void Reset() { Captured.Clear(); Constructors = 0; ThrowConstructorAt = 0; ThrowSetter = false; ThrowService = false; Calls = 0; Marker = 0; }
            public static async Task<object?> ReadAsync(string name, byte[] payload)
            {
                ITestService service = new TestServiceGeneratedImplementation(new Client(payload));
                return name switch
                {
                    "scalar" => service.Scalar(), "nullable" => service.Nullable(), "boolean" => service.Boolean(), "text" => service.Text(),
                    "bytes" => service.Bytes(), "readonly-bytes" => service.ReadOnlyBytes(), "dto" => service.Dto(), "nested" => service.Nested(),
                    "array" => service.Array(), "list" => service.List(), "raw-list" => service.RawList(), "map" => service.Map(), "raw-map" => service.RawMap(), "key-map" => service.KeyMap(),
                    "async-dto" => await service.AsyncDto(), "async-scalar" => await service.AsyncScalar(5), _ => throw new ArgumentOutOfRangeException(nameof(name))
                };
            }
            public static object[] ReadPrefix(byte[] payload)
            {
                Blob? value = null; var offset = 4; value = value.Deserialize(payload, ref offset);
                var consumed = offset; var next = SerializationUtils.Read<int>(payload.AsSpan(offset), ref offset);
                return new object[] { value!, consumed, next };
            }
            public static async Task<(bool, Exception?)> HandleAsync(byte[] payload)
            {
                var handler = new TestServiceInvocationServerHandler(new Service(), new SilentEvents<TestServiceInvocationServerHandler>());
                using var writer = new TrueMoon.ArrayPoolBufferWriter<byte>();
                return await handler.HandleAsync(15, payload, writer);
            }
            public static void Register() => UseInvocationService<ITestService>(); private static void UseInvocationService<T>() { }
        }
        public sealed class Client(ReadOnlyMemory<byte> payload) : IInvocationClient<ITestService>
        {
            public Task<T> InvokeAsync<T>(byte method, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>, T> decode, CancellationToken token = default) => Task.FromResult(decode(payload));
            public T Invoke<T>(byte method, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>, T> decode) => decode(payload);
            public Task InvokeAsync(byte method, Action<IBufferWriter<byte>>? action, CancellationToken token = default) => Task.CompletedTask;
            public void Invoke(byte method, Action<IBufferWriter<byte>>? action) { }
        }
        public sealed class SilentEvents<T> : IEventsSource<T>
        {
            public string Name => typeof(T).Name;
            public Activity StartActivity(string? details = null, string? category = null, string? caller = null) => new(Name);
            public void StopActivity(Activity activity) => activity.Stop();
            public void Write(Func<object>? func, string? category = null, string? caller = null) { }
        }
        """;
}

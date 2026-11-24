using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlloyVulkanTest;
using SkiaSharp;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Runtime.Semantics;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Tests.DocumentsUi;

internal static class EditorScenarioComparison
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    internal static Checkpoint Capture(string id, Document document, UiSemanticSnapshot snapshot, Guid? transform,
        Rect? selectionIdLabel, SKImage image)
    {
        if (snapshot.IsTruncated) throw new InvalidOperationException($"The semantic snapshot for {id} is incomplete.");
        if (snapshot.Viewport.Width != image.Width || snapshot.Viewport.Height != image.Height)
            throw new InvalidOperationException($"The frame for {id} does not match the layout viewport.");
        var component = transform?.ToString("N");
        var targets = new SortedDictionary<string, Target>(StringComparer.Ordinal);

        foreach (var node in snapshot.Nodes)
        {
            if (node.Id is not { } target) continue;
            string? parent = null;
            for (var index = node.ParentIndex; index.HasValue && parent == null; index = snapshot.Nodes[index.Value].ParentIndex)
                parent = snapshot.Nodes[index.Value].Id;

            targets.Add(Normalize(target)!, new(
                new(Normalize(parent), node.Role, node.IsVisible, node.IsEnabled, node.IsFocusable, node.IsFocused,
                    node.HasPointerCapture, node.IsSelected, node.HasError, node.IsActivePopup),
                new(node.Bounds, node.VisibleBounds)));
        }

        using var png = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException($"Could not encode the frame for {id}.");
        return new(id, document, snapshot.Viewport, targets, selectionIdLabel, png.ToArray());

        string? Normalize(string? target) => component == null ? target : target?.Replace(component, "$transform", StringComparison.Ordinal);
    }

    internal static Report Compare(Checkpoint[] baseline, Checkpoint[] candidate, string directory)
    {
        Directory.CreateDirectory(directory);
        WriteJson(Path.Combine(directory, "baseline.json"), new Run(1, "SkiaRaster", baseline));
        WriteJson(Path.Combine(directory, "candidate.json"), new Run(1, "SkiaRaster", candidate));
        var before = baseline.ToDictionary(point => point.Id, StringComparer.Ordinal);
        var after = candidate.ToDictionary(point => point.Id, StringComparer.Ordinal);
        var points = new List<CheckpointComparison>();

        foreach (var id in before.Keys.Union(after.Keys, StringComparer.Ordinal))
        {
            before.TryGetValue(id, out var left);
            after.TryGetValue(id, out var right);
            var state = new List<Change>();
            var geometry = new List<Change>();
            PixelDifference? pixels = null;
            byte[]? diff = null;
            var width = 0;
            var height = 0;
            if (left == null || right == null)
            {
                AddChange(state, "checkpoint.present", left != null, right != null);
            }
            else
            {
                CompareFields(state, "document", left.Document, right.Document);
                CompareFields(geometry, "viewport", left.Viewport, right.Viewport);
                foreach (var target in left.Targets.Keys.Union(right.Targets.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
                {
                    left.Targets.TryGetValue(target, out var a);
                    right.Targets.TryGetValue(target, out var b);
                    if (a == null || b == null)
                    {
                        AddChange(state, target, a?.State, b?.State);
                        AddChange(geometry, target, a?.Geometry, b?.Geometry);
                        continue;
                    }
                    if (a.State != b.State) CompareFields(state, target, a.State, b.State);
                    if (a.Geometry != b.Geometry) CompareFields(geometry, target, a.Geometry, b.Geometry);
                }

                width = Math.Max(left.Viewport.Width, right.Viewport.Width);
                height = Math.Max(left.Viewport.Height, right.Viewport.Height);
                if (left.Png.AsSpan().SequenceEqual(right.Png))
                    pixels = new(true, 0, 0, 0, 0, 0, null, null);
                else
                {
                    var leftPixels = ReadPixels(left, width, height);
                    var rightPixels = ReadPixels(right, width, height);
                    MaskSelectionIdLabel(leftPixels, rightPixels, width, height, left);
                    MaskSelectionIdLabel(leftPixels, rightPixels, width, height, right);
                    pixels = PixelDifference.Compare(leftPixels, rightPixels, width, height, tolerance: 0);
                    if (!pixels.Passed || geometry.Count != 0 || state.Count != 0)
                        diff = PixelDifference.Diff(leftPixels, rightPixels);
                }
            }

            var passed = state.Count == 0 && geometry.Count == 0 && pixels is { Passed: true };
            var frames = passed ? null : WriteFrames(directory, points.Count + 1, left, right, width, height, diff);
            points.Add(new(id, passed, state.ToArray(), geometry.ToArray(), pixels, frames));
        }

        var report = new Report(1, points.Count != 0 && points.All(point => point.Passed), points.ToArray());
        WriteJson(Path.Combine(directory, "comparison.json"), report);
        return report;
    }

    private static void CompareFields<T>(List<Change> changes, string target, T baseline, T candidate)
    {
        var before = JsonSerializer.SerializeToElement(baseline, Json);
        var after = JsonSerializer.SerializeToElement(candidate, Json);
        foreach (var field in before.EnumerateObject())
        {
            var value = after.GetProperty(field.Name);
            if (!JsonElement.DeepEquals(field.Value, value))
                changes.Add(new($"{target}.{field.Name}", field.Value, value));
        }
    }

    private static void AddChange<T>(List<Change> changes, string target, T baseline, T candidate) =>
        changes.Add(new(target, JsonSerializer.SerializeToElement(baseline, Json), JsonSerializer.SerializeToElement(candidate, Json)));

    private static byte[] ReadPixels(Checkpoint checkpoint, int width, int height)
    {
        using var image = SKImage.FromEncodedData(checkpoint.Png);
        if (image == null || image.Width != checkpoint.Viewport.Width || image.Height != checkpoint.Viewport.Height)
            throw new InvalidDataException($"Invalid frame for checkpoint {checkpoint.Id}.");
        using var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (!image.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0))
            throw new InvalidDataException($"Could not read frame for checkpoint {checkpoint.Id}.");

        var pixels = new byte[checked(width * height * 4)];
        for (var y = 0; y < bitmap.Height; y++)
            Marshal.Copy(bitmap.GetPixels() + y * bitmap.RowBytes, pixels, y * width * 4, bitmap.Width * 4);
        return pixels;
    }

    private static Frames WriteFrames(string directory, int index, Checkpoint? baseline, Checkpoint? candidate,
        int width, int height, byte[]? diff)
    {
        // Checkpoint IDs are data, so artifact paths use the report index.
        var stem = $"checkpoint-{index:D2}";
        var left = WriteFrame(baseline, "baseline");
        var right = WriteFrame(candidate, "candidate");
        string? difference = null;
        if (baseline != null && candidate != null)
        {
            // An otherwise identical image has an all-black pixel diff for state-only changes.
            using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
            if (diff == null) bitmap.Erase(SKColors.Black);
            else
            {
                for (var y = 0; y < height; y++)
                    Marshal.Copy(diff, y * width * 4, bitmap.GetPixels() + y * bitmap.RowBytes, width * 4);
            }
            using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException($"Could not encode the diff for {stem}.");
            difference = $"{stem}-diff.png";
            using var output = new FileStream(Path.Combine(directory, difference), FileMode.CreateNew);
            png.SaveTo(output);
        }
        return new(left, right, difference);

        string? WriteFrame(Checkpoint? checkpoint, string side)
        {
            if (checkpoint == null) return null;
            var file = $"{stem}-{side}.png";
            using var output = new FileStream(Path.Combine(directory, file), FileMode.CreateNew);
            output.Write(checkpoint.Png);
            return file;
        }
    }

    private static void MaskSelectionIdLabel(byte[] baseline, byte[] candidate, int width, int height, Checkpoint checkpoint)
    {
        if (checkpoint.SelectionIdLabel is not { } bounds) return;
        var scale = checkpoint.Viewport.Scale;
        var left = Math.Clamp((int)MathF.Floor(bounds.X * scale) - 1, 0, width);
        var right = Math.Clamp((int)MathF.Ceiling((bounds.X + bounds.Width) * scale) + 1, 0, width);
        var top = Math.Clamp((int)MathF.Floor(bounds.Y * scale) - 1, 0, height);
        var bottom = Math.Clamp((int)MathF.Ceiling((bounds.Y + bounds.Height) * scale) + 1, 0, height);
        for (var y = top; y < bottom; y++)
        {
            var offset = (y * width + left) * 4;
            baseline.AsSpan(offset, (right - left) * 4).Clear();
            candidate.AsSpan(offset, (right - left) * 4).Clear();
        }
    }

    private static void WriteJson<T>(string path, T value)
    {
        using var output = new FileStream(path, FileMode.CreateNew);
        JsonSerializer.Serialize(output, value, Json);
    }

    internal sealed record Document(string Scene, long? Revision, int EntityCount, double? PositionX, string? EditorX,
        int UndoCount, int RedoCount, string? SelectedEntity);
    internal sealed record TargetState(string? Parent, UiSemanticRole Role, bool Visible, bool Enabled, bool Focusable,
        bool Focused, bool HasPointerCapture, bool? Selected, bool HasError, bool ActivePopup);
    internal sealed record TargetGeometry(Rect Bounds, Rect VisibleBounds);
    internal sealed record Target(TargetState State, TargetGeometry Geometry);
    internal sealed record Checkpoint(string Id, Document Document, UiViewport Viewport, SortedDictionary<string, Target> Targets,
        Rect? SelectionIdLabel, [property: JsonIgnore] byte[] Png);
    private sealed record Run(int FormatVersion, string Backend, Checkpoint[] Checkpoints);
    internal sealed record Change(string Path, JsonElement Baseline, JsonElement Candidate);
    internal sealed record Frames(string? Baseline, string? Candidate, string? Diff);
    internal sealed record CheckpointComparison(string Id, bool Passed, Change[] StateChanges, Change[] GeometryChanges,
        PixelDifference? Pixels, Frames? Frames);
    internal sealed record Report(int FormatVersion, bool Passed, CheckpointComparison[] Checkpoints);
}

using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Scenes.Authoring;

namespace TrueMoon.Aether.Editor.Tooling.Editing;

public partial class EditorInspectorViewModel
{
    private InspectorComponent? _committedCamera;
    private InspectorProperty? _replacementLens;
    private ComponentSchema? _destinationCameraSchema;
    private (ComponentId Occurrence, InspectorCameraProjectionState State)? _publishedCameraProjection;

    private static bool IsGameCamera(ComponentTypeId type) =>
        type == MeshRenderingComponents.CameraId || type == MeshRenderingComponents.OrthographicCameraId;

    private static ComponentSchema CanonicalCameraSchema(ComponentTypeId type) => type == MeshRenderingComponents.CameraId
        ? MeshRenderingComponents.CameraSchema : MeshRenderingComponents.OrthographicCameraSchema;

    private static bool HasCameraProjection(ComponentSchema schema)
    {
        if (schema.Version != 1 || !IsGameCamera(schema.TypeId)) return false;
        var lens = schema.TypeId == MeshRenderingComponents.CameraId ? "fov" : "verticalSize";
        return IsNumber(lens) && IsNumber("near") && IsNumber("far");

        bool IsNumber(string key) => schema.Properties.TryGetValue(key, out var property) &&
            property.Kind == PropertyValueKind.Number && !property.IsNullable;
    }

    private static int ProjectionOrder(string key) => key switch
    {
        "fov" or "verticalSize" => -3, "near" => -2, "far" => -1, _ => 0
    };

    private static string ProjectionLabel(string key) => key switch
    {
        "fov" => "Угол обзора, °",
        "verticalSize" => "Высота кадра, ед.",
        "near" => "Ближняя грань",
        "far" => "Дальняя грань",
        _ => key
    };

    private static ComponentSchema? FindSchema(SceneDocument document, ComponentTypeId type, int version) =>
        document.Schemas.FirstOrDefault(schema => schema.TypeId == type && schema.Version == version);

    private static IEnumerable<ComponentSchema> InspectorSchemas(SceneDocument document)
    {
        foreach (var schema in document.Schemas) yield return schema;
        foreach (var schema in MeshRenderingComponents.Schemas)
            if (IsGameCamera(schema.TypeId) && FindSchema(document, schema.TypeId, schema.Version) == null)
                yield return schema;
    }

    private static bool CanAddSchema(ComponentSchema schema, SceneDocument document, HashSet<ComponentTypeId> types)
    {
        if ((!schema.AllowMultiple && types.Contains(schema.TypeId)) || !schema.RequiredTypes.All(types.Contains)) return false;
        if (!IsGameCamera(schema.TypeId)) return true;
        if (!types.Contains(BuiltInComponents.TransformId) || types.Any(IsGameCamera)) return false;
        if (schema.Version != 1) return true;
        var canonical = CanonicalCameraSchema(schema.TypeId);
        return ComponentEditBatch.SameSchema(schema, canonical) && CanUseCameraSchema(document, canonical);
    }

    private static bool CanUseCameraSchema(SceneDocument document, ComponentSchema canonical) =>
        FindSchema(document, canonical.TypeId, canonical.Version) is { } existing
            ? ComponentEditBatch.SameSchema(existing, canonical)
            : document.Schemas.Length < SceneDocumentCodec.MaximumSchemas;

    private void LoadCameraChoice(SceneDocument document, ResolvedEntity entity, List<InspectorComponent> groups)
    {
        InspectorComponent? camera = null;
        foreach (var group in groups)
        {
            if (!IsGameCamera(group.TypeId)) continue;
            if (camera != null) return;
            camera = group;
        }
        if (camera == null || !entity.Components.Values.Any(component => component.TypeId == BuiltInComponents.TransformId)) return;
        var source = CanonicalCameraSchema(camera.TypeId);
        if (!ComponentEditBatch.SameSchema(FindSchema(document, camera.TypeId, camera.SchemaVersion), source)) return;
        var destination = source.TypeId == MeshRenderingComponents.CameraId
            ? MeshRenderingComponents.OrthographicCameraSchema : MeshRenderingComponents.CameraSchema;
        if (!CanUseCameraSchema(document, destination)) return;
        foreach (var group in groups)
            if (group.Id != camera.Id && FindSchema(document, group.TypeId, group.SchemaVersion)!.RequiredTypes.Contains(source.TypeId))
                return;
        _committedCamera = camera;
        _destinationCameraSchema = destination;
    }

    /// <inheritdoc />
    public InspectorCameraProjectionState? CameraProjection(ComponentId component)
    {
        foreach (var item in _components)
            if (item.Id == component) return ReadCameraProjection(item);
        return null;
    }

    private InspectorCameraProjectionState? ReadCameraProjection(InspectorComponent item)
    {
        if (!TryCameraFields(item, out var lens, out _, out _)) return null;
        var canSwitch = false;
        if (_committedCamera?.Id == item.Id && _available && !_conflict && IsCopyGestureIdle)
        {
            var document = _commands.Read().Document;
            canSwitch = document != null && document.OpeningId == _openingId && ReferenceEquals(document.Content?.Document, _baseline);
        }
        return new(lens.Key == "fov" ? InspectorCameraProjection.Perspective : InspectorCameraProjection.Orthographic, canSwitch);
    }

    /// <inheritdoc />
    public void SelectCameraProjection(ComponentId component, InspectorCameraProjection projection)
    {
        VerifyOwner();
        var baseline = _baseline;
        var opening = _openingId;
        var selection = _selection;
        Refresh(selection);
        if (!ReferenceEquals(baseline, _baseline) || opening != _openingId || selection != _selection ||
            !Enum.IsDefined(projection) || CameraProjection(component) is not { CanSwitch: true } state ||
            state.Projection == projection || _committedCamera is not { } original) return;

        InspectorComponent shown;
        if (_replacementLens != null)
        {
            ClearReplacementLens();
            shown = original;
        }
        else
        {
            var key = projection == InspectorCameraProjection.Orthographic ? "verticalSize" : "fov";
            var property = _destinationCameraSchema!.Properties[key];
            var lens = new InspectorProperty(component, property, ProjectionLabel(key), VerifyOwner) { DisplayNumberInDegrees = key == "fov" };
            lens.Load(property.DefaultValue, Source(default, true), false);
            lens.Changed += DraftChanged;
            _replacementLens = lens;
            shown = original with { Properties = original.Properties.Select(field =>
                field.Key is "verticalSize" or "fov" ? lens : field).ToArray() };
        }

        _components = _components.Select(item => item.Id == component ? shown : item).ToArray();
        _active = _components.SelectMany(item => item.Properties).ToArray();
        OnPropertyChanged(nameof(Components));
        DraftChanged();
    }

    private void ClearReplacementLens()
    {
        if (_replacementLens == null) return;
        _replacementLens.Changed -= DraftChanged;
        _replacementLens = null;
    }

    private void UpdateCameraProjection()
    {
        (ComponentId Occurrence, InspectorCameraProjectionState State)? current = null;
        foreach (var item in _components)
        {
            if (ReadCameraProjection(item) is not { } state) continue;
            current = (item.Id, state);
            break;
        }
        if (_publishedCameraProjection == current) return;
        _publishedCameraProjection = current;
        OnPropertyChanged(nameof(IEditorInspectorViewModel.CameraProjection));
    }

    private ComponentEditBatch CreateInspectorBatch(SceneAuthoringState content, PropertyEdit[] edits)
    {
        if (_committedCamera is not { } camera || _replacementLens == null) return new(Operation(), edits);
        var entity = content.Resolved.Entities[_entity];
        var source = entity.Components[camera.Id];
        var shown = _components.Single(item => item.Id == camera.Id);
        var schema = _destinationCameraSchema!;
        var id = new ComponentId(Guid.NewGuid());
        var replacement = new ComponentData(id, schema.TypeId, schema.Version, shown.Properties.Select(field =>
            KeyValuePair.Create(field.Key, EffectiveValue(field, source, entity, content.Document))));
        return new(Operation(), edits,
            [new(_entity, camera.Id, ComponentChangeKind.Remove), new(_entity, id, ComponentChangeKind.Add, replacement)],
            FindSchema(content.Document, schema.TypeId, schema.Version) == null ? schema : null);
    }

    private bool TryCameraFields(InspectorComponent component, out InspectorProperty lens,
        out InspectorProperty near, out InspectorProperty far)
    {
        lens = near = far = null!;
        if (!IsGameCamera(component.TypeId) || component.SchemaVersion != 1) return false;
        var lensKey = _committedCamera?.Id == component.Id && _replacementLens != null ? _replacementLens.Key
            : component.TypeId == MeshRenderingComponents.CameraId ? "fov" : "verticalSize";
        foreach (var field in component.Properties)
        {
            if (field.Kind != PropertyValueKind.Number || field.IsNullable) continue;
            if (field.Key == lensKey) lens = field;
            else if (field.Key == "near") near = field;
            else if (field.Key == "far") far = field;
        }
        return lens != null && near != null && far != null;
    }

    private bool ValidateCameraProjections(SceneAuthoringState? content)
    {
        if (content == null || !ReferenceEquals(content.Document, _baseline) ||
            !content.Resolved.Entities.TryGetValue(_entity, out var entity)) return true;
        var valid = true;
        foreach (var component in _components)
        {
            if (!TryCameraFields(component, out var lens, out var near, out var far) ||
                !ReferenceEquals(lens, _replacementLens) && !component.Properties.Any(field => field.HasDraft)) continue;
            var resolved = entity.Components[component.Id];
            PropertyValue Value(InspectorProperty field) => EffectiveValue(field, resolved, entity, content.Document);
            uint layers = 0;
            System.Numerics.Vector3 clear = default;
            var commonValid = true;
            foreach (var field in component.Properties)
            {
                if (field.IsLayerMask)
                {
                    if (field.HasError) { commonValid = false; continue; }
                    var mask = Value(field).GetInteger();
                    if (mask is < 0 or > uint.MaxValue)
                    {
                        field.SetError("Введите маску слоёв от 0 до 4294967295");
                        commonValid = false;
                    }
                    else layers = (uint)mask;
                }
                else if (field.Key is "clearR" or "clearG" or "clearB" &&
                    field.Kind == PropertyValueKind.Number && !field.IsNullable)
                {
                    if (field.HasError) { commonValid = false; continue; }
                    var channel = Value(field).GetNumber();
                    if (channel is not (>= 0 and <= 1))
                    {
                        field.SetError("Компонент цвета должен быть в диапазоне [0, 1].");
                        commonValid = false;
                    }
                    else if (field.Key == "clearR") clear.X = (float)channel;
                    else if (field.Key == "clearG") clear.Y = (float)channel;
                    else clear.Z = (float)channel;
                }
            }
            valid &= commonValid;
            if (lens.HasError || near.HasError || far.HasError) { valid = false; continue; }
            var size = Value(lens).GetNumber();
            var nearDistance = Value(near).GetNumber();
            var farDistance = Value(far).GetNumber();
            valid &= lens.Key == "fov" ? ValidateCameraProjection(lens, near, far, size, nearDistance, farDistance)
                : ValidateOrthographicProjection(lens, near, far, size, nearDistance, farDistance, layers, clear);
        }
        return valid;
    }

    private PropertyValue EffectiveValue(InspectorProperty field, ResolvedComponent component, ResolvedEntity entity, SceneDocument document)
    {
        if (ReferenceEquals(field, _replacementLens))
        {
            if (!field.HasDraft || field.IsReset) return field.Schema.DefaultValue;
        }
        else
        {
            var committed = component.Properties[field.Key];
            if (!field.HasDraft || field.IsReset && committed.Source.EntityId == null) return committed.Value;
            if (field.IsReset)
            {
                // A locally restored presence can still inherit values. A local Add has no inherited values.
                if (component.Source.EntityId != null) return field.Schema.DefaultValue;
                for (var i = entity.DefinitionChain.Length - 1; i >= 0; i--)
                {
                    var id = entity.DefinitionChain[i];
                    var changes = document.Definitions.Single(definition => definition.Id == id).Changes;
                    var inherited = changes.PropertyOverrides.FirstOrDefault(item => item.ComponentId == component.Id && item.PropertyId == field.Key);
                    if (inherited != null) return inherited.Value;
                    var addition = changes.AddedComponents.FirstOrDefault(item => item.Id == component.Id);
                    if (addition != null) return addition.Properties.GetValueOrDefault(field.Key, field.Schema.DefaultValue);
                }
                return field.Schema.DefaultValue;
            }
        }
        field.TryValue(out var value);
        return value;
    }

    private static bool ValidateOrthographicProjection(InspectorProperty height, InspectorProperty near, InspectorProperty far,
        double size, double nearDistance, double farDistance, uint layers, System.Numerics.Vector3 clear)
    {
        if (!(size > 0 && size <= float.MaxValue && (float)size > 0))
            height.SetError("Высота кадра должна быть больше 0 и представима в проекции.");
        if (!(nearDistance > 0)) near.SetError("Расстояние до ближней плоскости должно быть больше 0.");
        else if (nearDistance > float.MaxValue) near.SetError("Расстояние выходит за пределы числового диапазона проекции.");
        else if ((float)nearDistance <= 0) near.SetError("Значение выходит за пределы точности проекции.");
        if (farDistance > float.MaxValue) far.SetError("Расстояние выходит за пределы числового диапазона проекции.");
        else if (!(farDistance > nearDistance)) far.SetError("Дальняя плоскость должна быть дальше ближней.");
        else if ((float)farDistance <= (float)nearDistance) far.SetError("Значение выходит за пределы точности проекции.");
        if (height.HasError || near.HasError || far.HasError) return false;
        new OrthographicCamera((float)size, (float)nearDistance, (float)farDistance, layers, clear).Validate();
        return true;
    }

    private static bool ValidateCameraProjection(InspectorProperty fov, InspectorProperty near, InspectorProperty far,
        double angle, double nearDistance, double farDistance)
    {
        if (angle <= 0 || angle >= Math.PI) fov.SetError("Вертикальный угол должен быть больше 0° и меньше 180°.");
        if (nearDistance <= 0) near.SetError("Расстояние до ближней плоскости должно быть больше 0.");
        if (farDistance <= nearDistance) far.SetError("Дальняя плоскость должна быть дальше ближней.");
        if (nearDistance > float.MaxValue) near.SetError("Расстояние выходит за пределы числового диапазона проекции.");
        if (farDistance > float.MaxValue) far.SetError("Расстояние выходит за пределы числового диапазона проекции.");
        if (fov.HasError || near.HasError || far.HasError) return false;

        var projection = new PerspectiveProjection((float)angle, (float)nearDistance, (float)farDistance);
        try
        {
            projection.Validate();
            return true;
        }
        catch (RenderContentException error) when (error.Error == RenderContentError.InvalidCamera)
        {
            var field = projection.FieldOfView <= 0 || projection.FieldOfView >= MathF.PI ? fov
                : projection.Near <= 0 || !float.IsFinite(projection.Near) ? near : far;
            field.SetError("Значение выходит за пределы точности проекции.");
            return false;
        }
    }
}

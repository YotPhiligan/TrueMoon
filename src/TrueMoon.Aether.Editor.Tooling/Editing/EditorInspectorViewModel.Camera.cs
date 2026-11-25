using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Scenes.Authoring;

namespace TrueMoon.Aether.Editor.Tooling.Editing;

public partial class EditorInspectorViewModel
{
    private static bool HasCameraProjection(ComponentSchema schema)
    {
        return schema.TypeId == MeshRenderingComponents.CameraId && schema.Version == 1 &&
            IsNumber("fov") && IsNumber("near") && IsNumber("far");

        bool IsNumber(string key) => schema.Properties.TryGetValue(key, out var property) &&
            property.Kind == PropertyValueKind.Number && !property.IsNullable;
    }

    private static int ProjectionOrder(string key) => key switch { "fov" => -3, "near" => -2, "far" => -1, _ => 0 };

    private static string ProjectionLabel(string key) => key switch
    {
        "fov" => "Угол обзора, °",
        "near" => "Ближняя грань",
        "far" => "Дальняя грань",
        _ => key
    };

    private bool ValidateCameraProjections(SceneAuthoringState? content)
    {
        if (content == null || !ReferenceEquals(content.Document, _baseline) ||
            !content.Resolved.Entities.TryGetValue(_entity, out var entity)) return true;

        var valid = true;
        foreach (var component in _components.Where(item => item.TypeId == MeshRenderingComponents.CameraId))
        {
            if (!_properties.TryGetValue((component.Id, "fov"), out var fov) || !fov.DisplayNumberInDegrees) continue;
            var near = _properties[(component.Id, "near")];
            var far = _properties[(component.Id, "far")];
            if (!fov.HasDraft && !near.HasDraft && !far.HasDraft) continue;
            if (fov.HasError || near.HasError || far.HasError) { valid = false; continue; }
            var resolved = entity.Components[component.Id];
            valid &= ValidateCameraProjection(fov, near, far,
                EffectiveValue(fov, resolved, entity, content.Document),
                EffectiveValue(near, resolved, entity, content.Document),
                EffectiveValue(far, resolved, entity, content.Document));
        }
        return valid;
    }

    private static double EffectiveValue(InspectorProperty field, ResolvedComponent component, ResolvedEntity entity, SceneDocument document)
    {
        var committed = component.Properties[field.Key];
        if (!field.HasDraft) return committed.Value.GetNumber();
        if (!field.IsReset)
        {
            field.TryValue(out var value);
            return value.GetNumber();
        }
        if (committed.Source.EntityId == null) return committed.Value.GetNumber();
        // Reset removes both the instance override and the value supplied by a locally added occurrence.
        foreach (var id in entity.DefinitionChain.Reverse())
        {
            var changes = document.Definitions.Single(definition => definition.Id == id).Changes;
            var inherited = changes.PropertyOverrides.FirstOrDefault(item => item.ComponentId == component.Id && item.PropertyId == field.Key);
            if (inherited != null) return inherited.Value.GetNumber();
            if (changes.AddedComponents.FirstOrDefault(item => item.Id == component.Id) is { } added)
                return added.Properties.GetValueOrDefault(field.Key, field.Schema.DefaultValue).GetNumber();
        }
        return field.Schema.DefaultValue.GetNumber();
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

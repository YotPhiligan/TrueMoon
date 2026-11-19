using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Tooling.Documents;

namespace TrueMoon.Aether.Editor.Tooling.Editing;

public partial class EditorInspectorViewModel
{
    private InspectorProperty? _renderMesh, _renderBinding;
    private SceneRenderAssets? _sceneAssets, _renderDraftAssets;
    private string? _renderReferenceDiagnostic;
    private InspectorRenderAssetSelection? _renderAssets;
    private (SceneRenderAssets? Assets, ComponentId Component, EntityId Entity, Guid Opening, string Mesh, string Binding, bool Enabled) _renderKey;
    private IReadOnlyList<InspectorRenderAssetChoice> _meshChoices = [], _materialChoices = [];

    /// <inheritdoc />
    public InspectorRenderAssetSelection? RenderAssets => _renderAssets;

    private bool HasRenderDraft => _renderMesh?.HasDraft == true || _renderBinding?.HasDraft == true;

    private void RefreshRenderAssets()
    {
        if (_renderMesh == null || _renderBinding == null)
        {
            _renderKey = default;
            _renderReferenceDiagnostic = null;
            _meshChoices = _materialChoices = [];
            SetProperty(ref _renderAssets, null, nameof(RenderAssets));
            return;
        }

        var enabled = _available && !_conflict && _numericGesture == null && _sceneAssets != null &&
            (!HasRenderDraft || ReferenceEquals(_renderDraftAssets, _sceneAssets));
        var key = (_sceneAssets, _renderMesh.ComponentId, _entity, _openingId, _renderMesh.Value, _renderBinding.Value, enabled);
        if (_renderKey == key) return;
        if (!ReferenceEquals(_renderKey.Assets, _sceneAssets))
        {
            _meshChoices = Choices(_sceneAssets?.Meshes);
            _materialChoices = Choices(_sceneAssets?.Materials);
        }
        _renderKey = key;
        var references = SceneRenderAssets.ResolveReferences(_sceneAssets, _renderMesh.Value, _renderBinding.Value);
        _renderReferenceDiagnostic = references.Diagnostic;
        var mesh = references.Mesh;
        var imported = mesh.HasValue ? _sceneAssets!.DefaultDefinition(mesh.Value) : null;
        var resolved = references.Definition;
        var defaultBinding = mesh.HasValue ? _sceneAssets!.DefaultBinding(mesh.Value) : null;
        var overrideSlot = resolved?.Bindings.FirstOrDefault(slot =>
            imported?.Bindings.Any(item => item.SlotId == slot.SlotId && item.Material != slot.Material) == true).SlotId;
        var slots = imported is { Bindings.Length: > 1 }
            ? imported.Bindings.Select((slot, index) => new InspectorRenderSlot(slot.SlotId, _sceneAssets!.SlotName(slot.SlotId),
                resolved?.Bindings[index].Material.AssetId, enabled && (overrideSlot is null || overrideSlot == Guid.Empty || overrideSlot == slot.SlotId))).ToArray()
            : [];
        var next = new InspectorRenderAssetSelection(_renderMesh.ComponentId, _meshChoices, mesh.HasValue ? _materialChoices : [],
            mesh, resolved is { Bindings.Length: 1 } ? resolved.Bindings[0].Material.AssetId : null, enabled)
        {
            CanRestoreMeshMaterial = enabled && defaultBinding.HasValue && defaultBinding != references.Binding,
            Slots = Array.AsReadOnly(slots)
        };
        // Equivalent displayed values can still target a different entity, opening or import generation.
        _renderAssets = next;
        OnPropertyChanged(nameof(RenderAssets));

        static IReadOnlyList<InspectorRenderAssetChoice> Choices(IReadOnlyList<SceneRenderAssets.Choice>? source)
            => source == null ? [] : Array.AsReadOnly(source.Select(choice => new InspectorRenderAssetChoice(choice.Id, choice.Name)).ToArray());
    }

    private IEnumerable<string> RenderDiagnostic()
    {
        if (_renderAssets == null) yield break;
        if (HasRenderDraft && !ReferenceEquals(_renderDraftAssets, _sceneAssets))
            yield return "Ресурсы сцены изменились. Отмените черновик и повторите выбор.";
        else if (_renderReferenceDiagnostic is { } diagnostic)
            yield return diagnostic;
        else if (_sceneAssets == null)
            yield return "Для выбора меша и материала отметьте импорт модели в каталоге сцены.";
        else if (_renderAssets.Slots.Count > 0 && _renderAssets.Slots.Any(slot => !slot.CanSelect) && _renderAssets.Enabled)
            yield return "Можно переопределить один слот. Верните материал меша, чтобы изменить другой слот.";
    }

    /// <inheritdoc />
    public void SelectRenderAsset(InspectorRenderAssetSelection selection, InspectorProperty property, Guid assetId)
    {
        VerifyOwner();
        Refresh(_selection);
        if (!ReferenceEquals(_renderAssets, selection) || !selection.Enabled || _sceneAssets is not { } assets ||
            _renderMesh == null || _renderBinding == null) return;

        Guid mesh;
        Guid? binding;
        if (ReferenceEquals(property, _renderMesh))
        {
            if (assetId == selection.Mesh) return;
            mesh = assetId;
            binding = assets.DefaultBinding(mesh);
        }
        else if (ReferenceEquals(property, _renderBinding) && selection.Mesh is { } currentMesh)
        {
            if (assetId == selection.Material || selection.Slots.Count != 0 || assets.DefaultDefinition(currentMesh) is not { } original) return;
            mesh = currentMesh;
            binding = assets.BindingForMaterial(mesh, original.Bindings[0].SlotId, assetId);
        }
        else return;

        if (binding == null) return;
        _renderDraftAssets = assets;
        _loading = true;
        try
        {
            _renderMesh.Value = mesh.ToString("D");
            _renderBinding.Value = binding.Value.ToString("D");
        }
        finally { _loading = false; }
        DraftChanged();
    }

    /// <inheritdoc />
    public void SelectRenderSlotMaterial(InspectorRenderAssetSelection selection, Guid slot, Guid material)
    {
        VerifyOwner();
        Refresh(_selection);
        if (!ReferenceEquals(_renderAssets, selection) || !selection.Enabled || selection.Mesh is not { } mesh ||
            _sceneAssets is not { } assets || _renderBinding == null ||
            selection.Slots.FirstOrDefault(item => item.Id == slot) is not { CanSelect: true } selected || selected.Material == material ||
            assets.BindingForMaterial(mesh, slot, material) is not { } binding) return;

        _renderDraftAssets = assets;
        _renderBinding.Value = binding.ToString("D");
    }

    /// <inheritdoc />
    public void RestoreMeshMaterial(InspectorRenderAssetSelection selection)
    {
        VerifyOwner();
        Refresh(_selection);
        if (!ReferenceEquals(_renderAssets, selection) || !selection.CanRestoreMeshMaterial ||
            selection.Mesh is not { } mesh || _sceneAssets is not { } assets || _renderBinding == null ||
            assets.DefaultBinding(mesh) is not { } binding) return;

        _renderDraftAssets = assets;
        _renderBinding.Value = binding.ToString("D");
    }
}

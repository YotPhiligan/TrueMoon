using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Tooling.Documents;

namespace TrueMoon.Aether.Editor.Tooling.Editing;

public partial class EditorInspectorViewModel
{
    private InspectorProperty? _renderMesh, _renderBinding;
    private SceneRenderAssets? _sceneAssets, _renderDraftAssets;
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
        var mesh = Guid.TryParseExact(_renderMesh.Value, "D", out var meshId) && _sceneAssets?.ContainsMesh(meshId) == true ? meshId : (Guid?)null;
        var binding = Guid.TryParseExact(_renderBinding.Value, "D", out var bindingId) ? bindingId : (Guid?)null;
        var material = mesh.HasValue && binding.HasValue ? _sceneAssets!.MaterialForBinding(mesh.Value, binding.Value) : null;
        var defaultBinding = mesh.HasValue ? _sceneAssets!.DefaultBinding(mesh.Value) : null;
        var next = new InspectorRenderAssetSelection(_renderMesh.ComponentId, _meshChoices, mesh.HasValue ? _materialChoices : [], mesh, material, enabled)
        {
            CanRestoreMeshMaterial = enabled && defaultBinding.HasValue && defaultBinding != binding
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
        else if (_sceneAssets == null)
            yield return "Для выбора меша и материала отметьте импорт модели в каталоге сцены.";
        else if (_renderAssets.Material == null)
            yield return "Меш или его материал недоступен. Выберите меш из ресурсов сцены.";
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
            if (assetId == selection.Material) return;
            mesh = currentMesh;
            binding = assets.BindingForMaterial(mesh, assetId);
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

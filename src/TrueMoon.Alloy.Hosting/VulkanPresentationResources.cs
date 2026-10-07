namespace TrueMoon.Alloy.Hosting;

/// <summary>Counts of Vulkan objects owned by a presenter; excludes device/driver allocations and borrowed source images.</summary>
/// <param name="LiveSwapchains">Created swapchains not yet destroyed.</param>
/// <param name="LiveSemaphores">Owned acquire/presentation semaphores.</param>
/// <param name="LiveFences">Owned presentation fences.</param>
/// <param name="LiveCommandPools">Owned pools, including their command buffers.</param>
/// <param name="RetiredSwapchains">Generations awaiting retirement proof.</param>
/// <param name="PeakLiveSwapchains">Maximum simultaneous generations.</param>
/// <param name="CreatedSwapchains">Total successful creations.</param>
/// <param name="DestroyedSwapchains">Total destructions.</param>
/// <param name="ConfirmedPresentations">Fence waits or completed replacement-image reacquisitions.</param>
public readonly record struct VulkanPresentationResources(int LiveSwapchains, int LiveSemaphores, int LiveFences,
    int LiveCommandPools, int RetiredSwapchains, int PeakLiveSwapchains,
    int CreatedSwapchains, int DestroyedSwapchains, int ConfirmedPresentations);

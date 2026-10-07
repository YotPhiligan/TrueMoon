// Included once at the end of src/c/sk_surface.cpp in the pinned Skia fork.
// Must be built into libSkiaSharp itself, never a second copy of Skia in a sidecar DLL.
#include "include/c/truemoon_vk_interop.h"
#include <cstddef>
#include <new>
#if SK_VULKAN
#include "include/gpu/ganesh/GrBackendSurface.h"
#include "include/gpu/ganesh/vk/GrVkBackendSurface.h"
#include "include/gpu/ganesh/vk/GrVkTypes.h"
#include "include/gpu/vk/VulkanMutableTextureState.h"
#endif

static_assert(sizeof(tm_vk_texture_info_t) == 48, "Unexpected interop ABI packing");
static_assert(offsetof(tm_vk_texture_info_t, width) == 8, "Unexpected interop ABI offset");
static_assert(offsetof(tm_vk_texture_info_t, sharing_mode) == 44, "Unexpected interop ABI offset");

uint32_t tm_skia_vk_interop_abi_version(void) { return 1; }
uint32_t tm_skia_vk_texture_info_size(void) { return sizeof(tm_vk_texture_info_t); }

gr_backendtexture_t* tm_skia_vk_surface_acquire(sk_surface_t* surface, tm_vk_texture_info_t* out) {
    if (!surface || !out) return nullptr;
    *out = {};
#if SK_VULKAN
    try {
        auto texture = SkSurfaces::GetBackendTexture(AsSurface(surface),
            SkSurfaces::BackendHandleAccess::kFlushRead);
        GrVkImageInfo info;
        if (!texture.isValid() || texture.isProtected() ||
            !GrBackendTextures::GetVkImageInfo(texture, &info) ||
            !info.fImage || info.fSampleCount != 1 || info.fLevelCount != 1 ||
            info.fFormat != VK_FORMAT_R8G8B8A8_UNORM ||
            !(info.fImageUsageFlags & VK_IMAGE_USAGE_TRANSFER_SRC_BIT)) return nullptr;
        auto* copy = new (std::nothrow) GrBackendTexture(texture);
        if (!copy) return nullptr;
        // VkImage is a pointer on Windows x64. Preserve all handle bits in the stable ABI.
        out->image = (uint64_t)info.fImage;
        out->width = texture.width();
        out->height = texture.height();
        out->format = (uint32_t)info.fFormat;
        out->layout = (uint32_t)info.fImageLayout;
        out->queue_family = info.fCurrentQueueFamily;
        out->usage = info.fImageUsageFlags;
        out->level_count = info.fLevelCount;
        out->sample_count = info.fSampleCount;
        out->tiling = (uint32_t)info.fImageTiling;
        out->sharing_mode = (uint32_t)info.fSharingMode;
        return ToGrBackendTexture(copy);
    } catch (...) { return nullptr; }
#else
    return nullptr;
#endif
}

int32_t tm_skia_vk_texture_return(gr_backendtexture_t* texture, uint32_t layout, uint32_t queue_family) {
#if SK_VULKAN
    if (!texture) return 0;
    try {
        GrVkImageInfo info;
        auto* backend = AsGrBackendTexture(texture);
        if (!GrBackendTextures::GetVkImageInfo(*backend, &info)) return 0;
        // Copies share Skia's Vulkan mutable state with the surface.
        backend->setMutableState(skgpu::MutableTextureStates::MakeVulkan(
            (VkImageLayout)layout, queue_family));
        return 1;
    } catch (...) { return 0; }
#else
    return 0;
#endif
}

void tm_skia_vk_texture_delete(gr_backendtexture_t* texture) {
    delete AsGrBackendTexture(texture);
}

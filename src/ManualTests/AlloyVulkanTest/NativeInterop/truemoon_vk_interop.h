#ifndef truemoon_vk_interop_DEFINED
#define truemoon_vk_interop_DEFINED

#include "include/c/sk_types.h"

SK_C_PLUS_PLUS_BEGIN_GUARD

// ABI v1: fixed-width fields, no pointers to Skia's private allocation structs.
typedef struct {
    uint64_t image;
    uint32_t width;
    uint32_t height;
    uint32_t format;
    uint32_t layout;
    uint32_t queue_family;
    uint32_t usage;
    uint32_t level_count;
    uint32_t sample_count;
    uint32_t tiling;
    uint32_t sharing_mode;
} tm_vk_texture_info_t;

SK_C_API uint32_t tm_skia_vk_interop_abi_version(void);
SK_C_API uint32_t tm_skia_vk_texture_info_size(void);
// Allocates only a backend descriptor. The surface still owns the image and allocation.
// The caller must submit/wait Skia work before using the image outside Skia.
SK_C_API gr_backendtexture_t* tm_skia_vk_surface_acquire(sk_surface_t* surface, tm_vk_texture_info_t* info);
// Host work must have completed; this updates tracking, it does not execute a barrier.
SK_C_API int32_t tm_skia_vk_texture_return(gr_backendtexture_t* texture, uint32_t layout, uint32_t queue_family);
// Deletes the descriptor only; never destroys VkImage or VkDevice.
SK_C_API void tm_skia_vk_texture_delete(gr_backendtexture_t* texture);

SK_C_PLUS_PLUS_END_GUARD
#endif

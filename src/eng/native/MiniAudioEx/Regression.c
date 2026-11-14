/* Native lifetime regression for the pinned MiniAudioEx libvorbis adapter. */
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <wchar.h>
#include "miniaudio_libvorbis.h"

/* Exported upstream, but omitted from its public header. */
MA_API ma_result ma_libvorbis_init_file_w(const wchar_t*, const ma_decoding_backend_config*, const ma_allocation_callbacks*, ma_libvorbis*);

typedef union allocation_header {
    struct { size_t size; } value;
    long double alignment;
    void* pointerAlignment;
    unsigned char minimumSize[16];
} allocation_header;
typedef struct ledger { size_t bytes, blocks, calls, failAt, peak; } ledger;
typedef struct reader { const unsigned char* data; size_t length, cursor; int failRead; } reader;
typedef struct blob { unsigned char* data; size_t length; } blob;
static int checks, failures;
static size_t leakedBytes, leakedBlocks;

static void* tracked_malloc(size_t size, void* user)
{
    ledger* state = (ledger*)user;
    allocation_header* header;
    state->calls++;
    if (state->calls == state->failAt) return NULL;
    header = (allocation_header*)malloc(sizeof(*header) + size);
    if (header == NULL) return NULL;
    header->value.size = size;
    state->bytes += size; state->blocks++;
    if (state->bytes > state->peak) state->peak = state->bytes;
    return header + 1;
}
static void tracked_free(void* pointer, void* user)
{
    ledger* state = (ledger*)user;
    allocation_header* header;
    if (pointer == NULL) return;
    header = (allocation_header*)pointer - 1;
    state->bytes -= header->value.size; state->blocks--; free(header);
}
static void* tracked_realloc(void* pointer, size_t size, void* user)
{
    void* replacement;
    if (size == 0) { tracked_free(pointer, user); return NULL; }
    replacement = tracked_malloc(size, user);
    if (replacement != NULL && pointer != NULL) {
        size_t old = ((allocation_header*)pointer - 1)->value.size;
        memcpy(replacement, pointer, old < size ? old : size); tracked_free(pointer, user);
    }
    return replacement;
}
static ma_allocation_callbacks callbacks(ledger* state)
{
    ma_allocation_callbacks result;
    result.pUserData = state; result.onMalloc = tracked_malloc;
    result.onRealloc = tracked_realloc; result.onFree = tracked_free;
    return result;
}
static ma_result read_data(void* user, void* output, size_t requested, size_t* actual)
{
    reader* source = (reader*)user;
    size_t remaining = source->length - source->cursor;
    *actual = 0;
    if (source->failRead) return MA_ERROR;
    if (requested > remaining) requested = remaining;
    if (requested > 0) memcpy(output, source->data + source->cursor, requested);
    source->cursor += requested; *actual = requested;
    return requested == 0 ? MA_AT_END : MA_SUCCESS;
}
static ma_result seek_data(void* user, ma_int64 offset, ma_seek_origin origin)
{
    reader* source = (reader*)user;
    ma_int64 start = origin == ma_seek_origin_start ? 0 : origin == ma_seek_origin_current ? (ma_int64)source->cursor : (ma_int64)source->length;
    ma_int64 next = start + offset;
    if (next < 0 || (ma_uint64)next > source->length) return MA_BAD_SEEK;
    source->cursor = (size_t)next; return MA_SUCCESS;
}
static ma_result tell_data(void* user, ma_int64* cursor)
{
    *cursor = (ma_int64)((reader*)user)->cursor; return MA_SUCCESS;
}
static void require(int condition, const char* name, const ledger* state)
{
    checks++;
    if (!condition) { failures++; fprintf(stderr, "FAIL %s: bytes=%zu blocks=%zu\n", name, state->bytes, state->blocks); }
}
static void require_clean(const char* name, ledger* state)
{
    leakedBytes += state->bytes; leakedBlocks += state->blocks;
    require(state->bytes == 0 && state->blocks == 0, name, state);
}
static blob load(const wchar_t* path)
{
    blob data = { 0 }; FILE* file = _wfopen(path, L"rb"); long length;
    if (file == NULL) return data;
    fseek(file, 0, SEEK_END); length = ftell(file); rewind(file);
    if (length > 0) {
        data.length = (size_t)length; data.data = (unsigned char*)malloc(data.length);
        if (data.data == NULL || fread(data.data, 1, data.length, file) != data.length) {
            free(data.data); data.data = NULL; data.length = 0;
        }
    }
    fclose(file); return data;
}
static void ascii_path(const wchar_t* wide, char* output, size_t capacity)
{
    BOOL substituted = FALSE;
    if (!WideCharToMultiByte(CP_ACP, WC_NO_BEST_FIT_CHARS, wide, -1, output, (int)capacity, NULL, &substituted) || substituted) {
        fprintf(stderr, "ANSI test path cannot be represented; use an ASCII evidence path.\n"); exit(2);
    }
}
static ma_result open_decoder(int entry, const wchar_t* path, const blob* data, reader* source,
                              const ma_allocation_callbacks* allocations, ma_libvorbis* decoder)
{
    char narrow[32768];
    switch (entry) {
        case 0: return ma_libvorbis_init(read_data, seek_data, tell_data, source, NULL, allocations, decoder);
        case 1: return ma_libvorbis_init_memory(data->data, data->length, NULL, allocations, decoder);
        case 2: ascii_path(path, narrow, sizeof(narrow)); return ma_libvorbis_init_file(narrow, NULL, allocations, decoder);
        default: return ma_libvorbis_init_file_w(path, NULL, allocations, decoder);
    }
}
static void invalid_fixture(const wchar_t* path)
{
    blob data = load(path); int entry, repeat;
    if (data.data == NULL) { fprintf(stderr, "Missing native regression fixture.\n"); exit(2); }
    for (entry = 0; entry < 4; entry++) for (repeat = 0; repeat < 8; repeat++) {
        ledger state = { 0 }; ma_allocation_callbacks allocations = callbacks(&state);
        ma_libvorbis decoder = { 0 }; reader source = { data.data, data.length, 0, 0 };
        ma_result result = open_decoder(entry, path, &data, &source, &allocations, &decoder);
        require(result != MA_SUCCESS, "invalid input rejected", &state);
        if (result == MA_SUCCESS) ma_libvorbis_uninit(&decoder, &allocations);
        /* Only uninit initialized decoders. Failed init must already be clean. */
        require_clean("failed init releases all adapter allocations", &state);
    }
    free(data.data);
}
static void valid_fixture(const wchar_t* path)
{
    blob data = load(path); int entry;
    if (data.data == NULL) exit(2);
    for (entry = 0; entry < 4; entry++) {
        ledger state = { 0 }; ma_allocation_callbacks allocations = callbacks(&state);
        ma_libvorbis decoder = { 0 }; reader source = { data.data, data.length, 0, 0 };
        ma_result result = open_decoder(entry, path, &data, &source, &allocations, &decoder);
        require(result == MA_SUCCESS, "valid OGG opens", &state);
        if (result == MA_SUCCESS) {
            float pcm[512 * 2]; ma_uint64 frames = 0; double energy = 0; size_t i;
            result = ma_libvorbis_read_pcm_frames(&decoder, pcm, 512, &frames);
            for (i = 0; i < (size_t)frames * 2; i++) energy += (double)pcm[i] * pcm[i];
            require(result == MA_SUCCESS && frames == 512 && energy > .001, "valid OGG decodes known generated audio", &state);
            ma_libvorbis_uninit(&decoder, &allocations);
        }
        require_clean("successful decoder closes cleanly", &state);
    }
    free(data.data);
}
static void argument_and_failure_cases(const wchar_t* validPath)
{
    blob data = load(validPath); int missing, failAt;
    for (missing = 0; missing < 2; missing++) {
        ledger state = { 0 }; ma_allocation_callbacks allocations = callbacks(&state); ma_libvorbis decoder = { 0 };
        reader source = { data.data, data.length, 0, 0 };
        ma_result result = ma_libvorbis_init(missing == 0 ? NULL : read_data, missing == 1 ? NULL : seek_data,
            tell_data, &source, NULL, &allocations, &decoder);
        require(result == MA_INVALID_ARGS, "required callback validation", &state);
        require_clean("invalid callbacks allocate no retained state", &state);
    }
    /* Fail each of the two vtable allocations: outer ma_libvorbis and OggVorbis_File. */
    for (failAt = 1; failAt <= 2; failAt++) {
        ledger state = { 0 }; ma_allocation_callbacks allocations = callbacks(&state); ma_data_source* backend = NULL;
        reader source = { data.data, data.length, 0, 0 }; ma_result result;
        state.failAt = (size_t)failAt;
        result = ma_libvorbis_get_decoding_backend()->onInit(NULL, read_data, seek_data, tell_data,
            &source, NULL, &allocations, &backend);
        require(result == MA_OUT_OF_MEMORY && backend == NULL, "vtable allocation failure outcome", &state);
        require_clean("vtable allocation failure releases partial state", &state);
    }
    {
        ledger state = { 0 }; ma_allocation_callbacks allocations = callbacks(&state); ma_libvorbis decoder = { 0 };
        reader source = { data.data, data.length, 0, 1 };
        ma_result result = ma_libvorbis_init(read_data, seek_data, tell_data, &source, NULL, &allocations, &decoder);
        require(result != MA_SUCCESS, "injected read failure rejected", &state);
        require_clean("read failure releases decoder state", &state);
    }
    free(data.data);
}
static void fixed_only_cases(void)
{
    int kind;
    for (kind = 0; kind < 4; kind++) {
        ledger state = { 0 }; ma_allocation_callbacks allocations = callbacks(&state); ma_libvorbis decoder = { 0 };
        unsigned char byte = 0; ma_result result;
        switch (kind) {
            case 0: result = ma_libvorbis_init_file(NULL, NULL, &allocations, &decoder); break;
            case 1: result = ma_libvorbis_init_file_w(NULL, NULL, &allocations, &decoder); break;
            case 2: result = ma_libvorbis_init_memory(NULL, 1, NULL, &allocations, &decoder); break;
            default: result = ma_libvorbis_init_memory(&byte, 0, NULL, &allocations, &decoder); break;
        }
        require(result == MA_INVALID_ARGS, "null/empty input rejected before allocation", &state);
        ma_libvorbis_uninit(&decoder, &allocations); ma_libvorbis_uninit(&decoder, &allocations);
        require_clean("failed-init repeated uninit is safe", &state);
    }
}
int wmain(int argc, wchar_t** argv)
{
    const wchar_t* invalidNames[] = { L"constant.wav", L"corrupt.ogg", L"truncated.ogg", L"bad-crc.ogg" };
    wchar_t path[32768]; size_t i; FILE* report;
    if (argc != 3 && argc != 4) { fprintf(stderr, "fixtures-directory report.json [--fixed]\n"); return 2; }
    for (i = 0; i < sizeof(invalidNames)/sizeof(invalidNames[0]); i++) {
        _snwprintf_s(path, 32768, _TRUNCATE, L"%ls\\%ls", argv[1], invalidNames[i]); invalid_fixture(path);
    }
    _snwprintf_s(path, 32768, _TRUNCATE, L"%ls\\valid.ogg", argv[1]);
    valid_fixture(path); argument_and_failure_cases(path);
    if (argc == 4 && wcscmp(argv[3], L"--fixed") == 0) fixed_only_cases();
    report = _wfopen(argv[2], L"wb"); if (report == NULL) return 2;
    fprintf(report, "{\"Completed\":%s,\"Checks\":%d,\"Failures\":%d,\"ObservedLeakedBytes\":%zu,\"ObservedLeakedBlocks\":%zu}\n",
        failures == 0 ? "true" : "false", checks, failures, leakedBytes, leakedBlocks);
    fclose(report);
    printf("Native regression: checks=%d failures=%d leaked_bytes=%zu leaked_blocks=%zu\n", checks, failures, leakedBytes, leakedBlocks);
    return failures == 0 ? 0 : 1;
}

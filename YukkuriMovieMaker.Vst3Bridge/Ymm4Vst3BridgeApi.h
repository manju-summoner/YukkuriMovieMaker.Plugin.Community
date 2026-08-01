#pragma once

#include <cstddef>
#include <cstdint>

#if defined(YMM4VST3_BRIDGE_EXPORTS)
#define YMM4VST3_API extern "C" __declspec(dllexport)
#else
#define YMM4VST3_API extern "C"
#endif

inline constexpr std::int32_t Ymm4Vst3ApiVersion = 2;

// C#側 Vst3Native.NativeClassInfo と同一レイアウトを保つこと
struct Ymm4Vst3ClassInfo
{
    char classId[64];
    char name[256];
    char vendor[256];
    char category[128];
    char subCategories[256];
    char version[64];
};

static_assert(sizeof(Ymm4Vst3ClassInfo) == 1024);
static_assert(offsetof(Ymm4Vst3ClassInfo, classId) == 0);
static_assert(offsetof(Ymm4Vst3ClassInfo, name) == 64);
static_assert(offsetof(Ymm4Vst3ClassInfo, vendor) == 320);
static_assert(offsetof(Ymm4Vst3ClassInfo, category) == 576);
static_assert(offsetof(Ymm4Vst3ClassInfo, subCategories) == 704);
static_assert(offsetof(Ymm4Vst3ClassInfo, version) == 960);

YMM4VST3_API std::int32_t __cdecl Ymm4Vst3GetApiVersion();
YMM4VST3_API void* __cdecl Ymm4Vst3ModuleOpen(
    const char* utf8Path,
    char* errorBuf,
    std::int32_t errorBufSize);
YMM4VST3_API void __cdecl Ymm4Vst3ModuleClose(void* moduleHandle);
YMM4VST3_API std::int32_t __cdecl Ymm4Vst3ModuleGetClassCount(void* moduleHandle);
YMM4VST3_API std::int32_t __cdecl Ymm4Vst3ModuleGetClassInfo(
    void* moduleHandle,
    std::int32_t index,
    Ymm4Vst3ClassInfo* info);

using Ymm4Vst3GetApiVersionProc = std::int32_t(__cdecl*)();
using Ymm4Vst3ModuleOpenProc = void* (__cdecl*)(const char*, char*, std::int32_t);
using Ymm4Vst3ModuleCloseProc = void (__cdecl*)(void*);
using Ymm4Vst3ModuleGetClassCountProc = std::int32_t(__cdecl*)(void*);
using Ymm4Vst3ModuleGetClassInfoProc = std::int32_t(__cdecl*)(
    void*,
    std::int32_t,
    Ymm4Vst3ClassInfo*);

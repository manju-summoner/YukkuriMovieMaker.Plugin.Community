//-----------------------------------------------------------------------------
// YukkuriMovieMaker.Vst3Bridge
// VST3 SDK公式のホスティングヘルパー（Module/PlugProvider/HostProcessData）を
// C#からP/Invokeできるフラットな C API に包むブリッジDLL。
// 処理モデルはSDK付属サンプル audiohost / editorhost を踏襲している。
//-----------------------------------------------------------------------------

#include <windows.h>
#include <objbase.h>

#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <mutex>
#include <string>
#include <unordered_map>
#include <vector>

#include "public.sdk/source/vst/hosting/eventlist.h"
#include "public.sdk/source/vst/hosting/hostclasses.h"
#include "public.sdk/source/vst/hosting/module.h"
#include "public.sdk/source/vst/hosting/parameterchanges.h"
#include "public.sdk/source/vst/hosting/plugprovider.h"
#include "public.sdk/source/vst/hosting/processdata.h"
#include "public.sdk/source/common/memorystream.h"
#include "public.sdk/source/vst/utility/memoryibstream.h"
#include "pluginterfaces/base/funknownimpl.h"
#include "pluginterfaces/gui/iplugview.h"
#include "pluginterfaces/gui/iplugviewcontentscalesupport.h"
#include "pluginterfaces/vst/ivstaudioprocessor.h"
#include "pluginterfaces/vst/ivsteditcontroller.h"
#include "pluginterfaces/vst/ivstprocesscontext.h"
#include "pluginterfaces/vst/vstspeaker.h"

using namespace Steinberg;
using namespace Steinberg::Vst;

#define YMM4VST3_API extern "C" __declspec(dllexport)

namespace
{
    //------------------------------------------------------------------------
    // 共通ユーティリティ
    //------------------------------------------------------------------------

    void CopyUtf8(char* dest, int32_t destSize, const std::string& src)
    {
        if (!dest || destSize <= 0)
            return;
        auto count = std::min<size_t>(src.size(), static_cast<size_t>(destSize) - 1);
        std::memcpy(dest, src.data(), count);
        dest[count] = '\0';
    }

    HostApplication gHostApplication;
    std::once_flag gInitFlag;

    void EnsureInitialized()
    {
        std::call_once(gInitFlag, []
        {
            PluginContextFactory::instance().setPluginContext(&gHostApplication);
        });
    }

    //------------------------------------------------------------------------
    // プラグインGUIからのパラメータ編集を受け取り、process()へ転送するためのキュー
    //------------------------------------------------------------------------
    class BridgeComponentHandler : public U::Implements<U::Directly<IComponentHandler>>
    {
    public:
        BridgeComponentHandler(
            ParameterChangeTransfer& transfer,
            std::atomic<bool>& paramValuesDirty,
            std::atomic<int32>& restartFlags,
            std::mutex& editorParamMutex,
            std::unordered_map<ParamID, ParamValue>& pendingEditorParams)
            : transfer(transfer),
              paramValuesDirty(paramValuesDirty),
              restartFlags(restartFlags),
              editorParamMutex(editorParamMutex),
              pendingEditorParams(pendingEditorParams)
        {
        }

        tresult PLUGIN_API beginEdit(ParamID) override { return kResultOk; }

        tresult PLUGIN_API performEdit(ParamID id, ParamValue valueNormalized) override
        {
            transfer.addChange(id, valueNormalized, 0);
            std::lock_guard lock(editorParamMutex);
            pendingEditorParams[id] = valueNormalized;
            return kResultOk;
        }

        tresult PLUGIN_API endEdit(ParamID) override { return kResultOk; }

        tresult PLUGIN_API restartComponent(int32 flags) override
        {
            restartFlags.fetch_or(flags);
            // プリセット読み込み等で全パラメータが変わった場合は次のPumpで全転送する
            if (flags & kParamValuesChanged)
                paramValuesDirty.store(true);
            return kResultOk;
        }

    private:
        ParameterChangeTransfer& transfer;
        std::atomic<bool>& paramValuesDirty;
        std::atomic<int32>& restartFlags;
        std::mutex& editorParamMutex;
        std::unordered_map<ParamID, ParamValue>& pendingEditorParams;
    };

    //------------------------------------------------------------------------
    // ハンドル実体
    //------------------------------------------------------------------------

    struct BridgeModule
    {
        VST3::Hosting::Module::Ptr module;
    };

    struct MeterParameterChange
    {
        ParamID id{};
        ParamValue value{};
        int64 samplePosition{};
    };

    constexpr size_t MeterParameterCapacity = 8192;

    struct BridgePlugin
    {
        VST3::Hosting::Module::Ptr module;
        IPtr<PlugProvider> provider;
        IPtr<IComponent> component;
        IPtr<IEditController> controller;
        FUnknownPtr<IAudioProcessor> processor;
        IPtr<BridgeComponentHandler> componentHandler;

        ParameterChangeTransfer paramTransfer;
        std::atomic<bool> paramValuesDirty{ false };
        std::atomic<int32> restartFlags{ 0 };
        std::mutex editorParamMutex;
        std::unordered_map<ParamID, ParamValue> pendingEditorParams;
        std::array<MeterParameterChange, MeterParameterCapacity> meterParameters{};
        size_t meterParameterReadIndex = 0;
        size_t meterParameterWriteIndex = 0;

        HostProcessData processData;
        ParameterChanges inputParameterChanges;
        ParameterChanges outputParameterChanges;
        EventList inputEvents;
        ProcessContext processContext{};

        double sampleRate = 0;
        int32 maxBlockSize = 0;
        int32 mainInBus = -1;
        int32 mainOutBus = -1;
        int32 mainInChannels = 0;
        int32 mainOutChannels = 0;
        bool processingActive = false;
        int64_t pumpPosition = 0;
    };

    struct BridgeView
    {
        BridgePlugin* plugin = nullptr;
        IPtr<IPlugView> view;
        IPtr<IPlugFrame> frame;
        bool attached = false;
    };

    using ViewResizeCallback = void(__stdcall*)(void* context, int32_t width, int32_t height);
    using ParameterChangeCallback = void(__stdcall*)(void* context, uint32_t paramId, double normalizedValue);
    using MeterParameterChangeCallback = void(__stdcall*)(
        void* context,
        uint32_t paramId,
        double normalizedValue,
        int64 samplePosition);

    //------------------------------------------------------------------------
    // プラグイン都合のリサイズ要求をC#へ通知するIPlugFrame
    //------------------------------------------------------------------------
    class BridgePlugFrame : public U::Implements<U::Directly<IPlugFrame>>
    {
    public:
        BridgePlugFrame(ViewResizeCallback callback, void* context)
            : callback(callback), context(context)
        {
        }

        tresult PLUGIN_API resizeView(IPlugView* view, ViewRect* newSize) override
        {
            if (!view || !newSize)
                return kInvalidArgument;
            // C#側でウィンドウサイズを合わせたのち、ビューへ確定サイズを通知する
            if (callback)
                callback(context, newSize->getWidth(), newSize->getHeight());
            return view->onSize(newSize);
        }

    private:
        ViewResizeCallback callback;
        void* context;
    };

    BusInfo GetBusInfoSafe(IComponent* component, MediaType type, BusDirection dir, int32 index)
    {
        BusInfo info{};
        component->getBusInfo(type, dir, index, info);
        return info;
    }

    // メインバス（BusInfo.busType == kMain）を探す。見つからなければ先頭を使う
    int32 FindMainBusIndex(IComponent* component, BusDirection dir)
    {
        auto count = component->getBusCount(kAudio, dir);
        for (int32 i = 0; i < count; i++)
        {
            if (GetBusInfoSafe(component, kAudio, dir, i).busType == kMain)
                return i;
        }
        return count > 0 ? 0 : -1;
    }

    void CaptureOutputParameters(
        BridgePlugin& plugin,
        int64 projectTimeSamples,
        bool captureMeterParameters)
    {
        const auto count = plugin.outputParameterChanges.getParameterCount();
        for (int32 i = 0; i < count; i++)
        {
            auto* queue = plugin.outputParameterChanges.getParameterData(i);
            if (!queue || queue->getPointCount() <= 0)
                continue;
            int32 sampleOffset{};
            ParamValue value{};
            if (queue->getPoint(queue->getPointCount() - 1, sampleOffset, value) == kResultOk)
            {
                const auto id = queue->getParameterId();
                if (captureMeterParameters)
                {
                    plugin.meterParameters[plugin.meterParameterWriteIndex] =
                        { id, value, projectTimeSamples + sampleOffset };
                    const auto nextWriteIndex =
                        (plugin.meterParameterWriteIndex + 1) % MeterParameterCapacity;
                    if (nextWriteIndex == plugin.meterParameterReadIndex)
                    {
                        plugin.meterParameterReadIndex =
                            (plugin.meterParameterReadIndex + 1) % MeterParameterCapacity;
                    }
                    plugin.meterParameterWriteIndex = nextWriteIndex;
                }
            }
        }
    }

    // 編集キューの内容をinputParameterChangesへ移し、1ブロック分processを回す
    bool ProcessBlock(
        BridgePlugin& plugin,
        const float* inL, const float* inR,
        float* outL, float* outR,
        int32 numFrames, int64_t projectTimeSamples, bool isPlaying,
        bool isTempoValid, double tempo,
        int32 timeSigNumerator, int32 timeSigDenominator,
        bool captureMeterParameters)
    {
        auto& data = plugin.processData;
        data.numSamples = numFrames;

        // 入力バスへコピー（モノラルプラグインはL/Rをダウンミックス）
        if (plugin.mainInBus >= 0 && data.numInputs > plugin.mainInBus && inL && inR)
        {
            auto& bus = data.inputs[plugin.mainInBus];
            bus.silenceFlags = 0;
            if (plugin.mainInChannels >= 2)
            {
                std::memcpy(bus.channelBuffers32[0], inL, sizeof(float) * numFrames);
                std::memcpy(bus.channelBuffers32[1], inR, sizeof(float) * numFrames);
            }
            else if (plugin.mainInChannels == 1)
            {
                auto* mono = bus.channelBuffers32[0];
                for (int32 i = 0; i < numFrames; i++)
                    mono[i] = (inL[i] + inR[i]) * 0.5f;
            }
        }

        plugin.paramTransfer.transferChangesTo(plugin.inputParameterChanges);

        plugin.processContext.projectTimeSamples = projectTimeSamples;
        plugin.processContext.continousTimeSamples = projectTimeSamples;
        plugin.processContext.state =
            ProcessContext::kContTimeValid | (isPlaying ? ProcessContext::kPlaying : 0);
        if (isTempoValid && plugin.sampleRate > 0 && tempo > 0 && timeSigNumerator > 0 && timeSigDenominator > 0)
        {
            plugin.processContext.tempo = tempo;
            plugin.processContext.timeSigNumerator = timeSigNumerator;
            plugin.processContext.timeSigDenominator = timeSigDenominator;
            plugin.processContext.projectTimeMusic =
                projectTimeSamples / plugin.sampleRate * (tempo / 60.0);
            const auto barLength = timeSigNumerator * 4.0 / timeSigDenominator;
            plugin.processContext.barPositionMusic =
                std::floor(plugin.processContext.projectTimeMusic / barLength) * barLength;
            plugin.processContext.state |=
                ProcessContext::kTempoValid | ProcessContext::kTimeSigValid |
                ProcessContext::kProjectTimeMusicValid | ProcessContext::kBarPositionValid;
        }
        else
        {
            plugin.processContext.tempo = 0;
            plugin.processContext.timeSigNumerator = 0;
            plugin.processContext.timeSigDenominator = 0;
            plugin.processContext.projectTimeMusic = 0;
            plugin.processContext.barPositionMusic = 0;
        }

        auto result = plugin.processor->process(data);

        plugin.inputParameterChanges.clearQueue();
        if (result == kResultOk && captureMeterParameters)
        {
            CaptureOutputParameters(
                plugin,
                projectTimeSamples,
                captureMeterParameters);
        }
        plugin.outputParameterChanges.clearQueue();
        plugin.inputEvents.clear();

        if (result != kResultOk)
            return false;

        // 出力バスからコピー（モノラルプラグインはLをRへ複製）
        if (plugin.mainOutBus >= 0 && data.numOutputs > plugin.mainOutBus && outL && outR)
        {
            auto& bus = data.outputs[plugin.mainOutBus];
            if (plugin.mainOutChannels >= 2)
            {
                std::memcpy(outL, bus.channelBuffers32[0], sizeof(float) * numFrames);
                std::memcpy(outR, bus.channelBuffers32[1], sizeof(float) * numFrames);
            }
            else if (plugin.mainOutChannels == 1)
            {
                std::memcpy(outL, bus.channelBuffers32[0], sizeof(float) * numFrames);
                std::memcpy(outR, bus.channelBuffers32[0], sizeof(float) * numFrames);
            }
        }
        return true;
    }
}

//------------------------------------------------------------------------
// モジュール（.vst3ファイル）
//------------------------------------------------------------------------

YMM4VST3_API void* Ymm4Vst3ModuleOpen(const char* utf8Path, char* errorBuf, int32_t errorBufSize)
{
    EnsureInitialized();
    if (!utf8Path)
        return nullptr;
    std::string error;
    auto module = VST3::Hosting::Module::create(utf8Path, error);
    if (!module)
    {
        CopyUtf8(errorBuf, errorBufSize, error);
        return nullptr;
    }
    return new BridgeModule{ module };
}

YMM4VST3_API void Ymm4Vst3ModuleClose(void* moduleHandle)
{
    delete static_cast<BridgeModule*>(moduleHandle);
}

// C#側 Vst3Native.ClassInfo と同一レイアウトを保つこと
struct Ymm4Vst3ClassInfo
{
    char classId[64];
    char name[256];
    char vendor[256];
    char category[128];
    char subCategories[256];
    char version[64];
};

YMM4VST3_API int32_t Ymm4Vst3ModuleGetClassCount(void* moduleHandle)
{
    auto* bridgeModule = static_cast<BridgeModule*>(moduleHandle);
    if (!bridgeModule)
        return 0;
    return static_cast<int32_t>(bridgeModule->module->getFactory().classCount());
}

YMM4VST3_API int32_t Ymm4Vst3ModuleGetClassInfo(void* moduleHandle, int32_t index, Ymm4Vst3ClassInfo* info)
{
    auto* bridgeModule = static_cast<BridgeModule*>(moduleHandle);
    if (!bridgeModule || !info)
        return 0;
    auto classInfos = bridgeModule->module->getFactory().classInfos();
    if (index < 0 || index >= static_cast<int32_t>(classInfos.size()))
        return 0;
    const auto& classInfo = classInfos[index];
    *info = {};
    CopyUtf8(info->classId, sizeof(info->classId), classInfo.ID().toString());
    CopyUtf8(info->name, sizeof(info->name), classInfo.name());
    CopyUtf8(info->vendor, sizeof(info->vendor), classInfo.vendor());
    CopyUtf8(info->category, sizeof(info->category), classInfo.category());
    CopyUtf8(info->subCategories, sizeof(info->subCategories), classInfo.subCategoriesString());
    CopyUtf8(info->version, sizeof(info->version), classInfo.version());
    return 1;
}

//------------------------------------------------------------------------
// プラグインインスタンス
//------------------------------------------------------------------------

YMM4VST3_API void* Ymm4Vst3PluginCreate(void* moduleHandle, const char* classIdHex, char* errorBuf, int32_t errorBufSize)
{
    EnsureInitialized();
    auto* bridgeModule = static_cast<BridgeModule*>(moduleHandle);
    if (!bridgeModule || !classIdHex)
        return nullptr;

    auto uid = VST3::UID::fromString(std::string(classIdHex));
    if (!uid)
    {
        CopyUtf8(errorBuf, errorBufSize, "invalid class id");
        return nullptr;
    }

    VST3::Hosting::ClassInfo targetInfo;
    bool found = false;
    for (const auto& classInfo : bridgeModule->module->getFactory().classInfos())
    {
        if (classInfo.category() == kVstAudioEffectClass && classInfo.ID() == *uid)
        {
            targetInfo = classInfo;
            found = true;
            break;
        }
    }
    if (!found)
    {
        CopyUtf8(errorBuf, errorBufSize, "class not found in module");
        return nullptr;
    }

    auto provider = owned(new PlugProvider(bridgeModule->module->getFactory(), targetInfo, true));
    if (!provider->initialize())
    {
        CopyUtf8(errorBuf, errorBufSize, "failed to initialize plug-in");
        return nullptr;
    }

    auto plugin = std::make_unique<BridgePlugin>();
    plugin->module = bridgeModule->module;
    plugin->provider = provider;
    plugin->component = provider->getComponentPtr();
    plugin->controller = provider->getControllerPtr();
    plugin->processor = FUnknownPtr<IAudioProcessor>(plugin->component);
    if (!plugin->component || !plugin->processor)
    {
        CopyUtf8(errorBuf, errorBufSize, "plug-in has no audio processor");
        return nullptr;
    }

    plugin->paramTransfer.setMaxParameters(8192);
    plugin->componentHandler = owned(new BridgeComponentHandler(
        plugin->paramTransfer,
        plugin->paramValuesDirty,
        plugin->restartFlags,
        plugin->editorParamMutex,
        plugin->pendingEditorParams));
    if (plugin->controller)
        plugin->controller->setComponentHandler(plugin->componentHandler);

    plugin->processData.inputParameterChanges = &plugin->inputParameterChanges;
    plugin->processData.outputParameterChanges = &plugin->outputParameterChanges;
    plugin->processData.inputEvents = &plugin->inputEvents;
    plugin->processData.processContext = &plugin->processContext;
    plugin->processContext.tempo = 120;
    plugin->processContext.timeSigNumerator = 4;
    plugin->processContext.timeSigDenominator = 4;

    return plugin.release();
}

YMM4VST3_API int32_t Ymm4Vst3PluginSetup(void* pluginHandle, double sampleRate, int32_t maxBlockSize)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (!plugin || sampleRate <= 0 || maxBlockSize <= 0)
        return 0;

    auto* component = plugin->component.get();
    auto& processor = plugin->processor;

    plugin->mainInBus = FindMainBusIndex(component, kInput);
    plugin->mainOutBus = FindMainBusIndex(component, kOutput);
    if (plugin->mainOutBus < 0)
        return 0;

    // 全バスにステレオを要求する（拒否されても実際の配置を後で取得して追従する）
    auto numInBuses = component->getBusCount(kAudio, kInput);
    auto numOutBuses = component->getBusCount(kAudio, kOutput);
    std::vector<SpeakerArrangement> inArrs(std::max<int32>(numInBuses, 1), SpeakerArr::kStereo);
    std::vector<SpeakerArrangement> outArrs(std::max<int32>(numOutBuses, 1), SpeakerArr::kStereo);
    processor->setBusArrangements(
        numInBuses > 0 ? inArrs.data() : nullptr, numInBuses,
        numOutBuses > 0 ? outArrs.data() : nullptr, numOutBuses);

    ProcessSetup setup{ kRealtime, kSample32, maxBlockSize, sampleRate };
    if (processor->setupProcessing(setup) != kResultOk)
        return 0;

    plugin->sampleRate = sampleRate;
    plugin->maxBlockSize = maxBlockSize;
    plugin->processContext.sampleRate = sampleRate;

    if (!plugin->processData.prepare(*component, maxBlockSize, kSample32))
        return 0;

    SpeakerArrangement arr{};
    plugin->mainInChannels = 0;
    if (plugin->mainInBus >= 0 && processor->getBusArrangement(kInput, plugin->mainInBus, arr) == kResultOk)
        plugin->mainInChannels = SpeakerArr::getChannelCount(arr);
    plugin->mainOutChannels = 0;
    if (processor->getBusArrangement(kOutput, plugin->mainOutBus, arr) == kResultOk)
        plugin->mainOutChannels = SpeakerArr::getChannelCount(arr);

    // メインバスのみ有効化する
    for (int32 i = 0; i < numInBuses; i++)
        component->activateBus(kAudio, kInput, i, i == plugin->mainInBus);
    for (int32 i = 0; i < numOutBuses; i++)
        component->activateBus(kAudio, kOutput, i, i == plugin->mainOutBus);

    if (component->setActive(true) != kResultOk)
        return 0;
    processor->setProcessing(true);
    plugin->processingActive = true;
    return 1;
}

YMM4VST3_API int32_t Ymm4Vst3PluginProcessWithTransport(
    void* pluginHandle,
    const float* inL, const float* inR,
    float* outL, float* outR,
    int32_t numFrames, int64_t projectTimeSamples,
    double tempo, int32_t timeSigNumerator, int32_t timeSigDenominator,
    int32_t isTempoValid, int32_t captureMeterParameters);

YMM4VST3_API int32_t Ymm4Vst3PluginProcess(
    void* pluginHandle,
    const float* inL, const float* inR,
    float* outL, float* outR,
    int32_t numFrames, int64_t projectTimeSamples)
{
    return Ymm4Vst3PluginProcessWithTransport(
        pluginHandle,
        inL, inR, outL, outR,
        numFrames, projectTimeSamples,
        120.0, 4, 4, 1, 1);
}

YMM4VST3_API int32_t Ymm4Vst3PluginProcessWithTransport(
    void* pluginHandle,
    const float* inL, const float* inR,
    float* outL, float* outR,
    int32_t numFrames, int64_t projectTimeSamples,
    double tempo, int32_t timeSigNumerator, int32_t timeSigDenominator,
    int32_t isTempoValid, int32_t captureMeterParameters)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (!plugin || !plugin->processingActive || numFrames < 0)
        return 0;

    // maxBlockSize単位に分割して処理する
    int32_t offset = 0;
    while (offset < numFrames)
    {
        auto chunk = std::min(numFrames - offset, plugin->maxBlockSize);
        if (!ProcessBlock(
            *plugin,
            inL ? inL + offset : nullptr, inR ? inR + offset : nullptr,
            outL ? outL + offset : nullptr, outR ? outR + offset : nullptr,
            chunk, projectTimeSamples + offset, true,
            isTempoValid != 0, tempo, timeSigNumerator, timeSigDenominator,
            captureMeterParameters != 0))
        {
            return 0;
        }
        offset += chunk;
    }
    return 1;
}

// エディタ表示中にパラメータ編集をプロセッサへ反映させるための無音プロセス
YMM4VST3_API int32_t Ymm4Vst3PluginPump(void* pluginHandle)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (!plugin || !plugin->processingActive)
        return 0;

    if (plugin->paramValuesDirty.exchange(false) && plugin->controller)
    {
        // プリセット切り替え等の一括変更を全パラメータ転送で反映する
        auto count = plugin->controller->getParameterCount();
        for (int32 i = 0; i < count; i++)
        {
            ParameterInfo parameterInfo{};
            if (plugin->controller->getParameterInfo(i, parameterInfo) != kResultOk)
                continue;
            const auto value = plugin->controller->getParamNormalized(parameterInfo.id);
            plugin->paramTransfer.addChange(
                parameterInfo.id,
                value,
                0);
            std::lock_guard lock(plugin->editorParamMutex);
            plugin->pendingEditorParams[parameterInfo.id] = value;
        }
    }

    constexpr int32 pumpFrames = 64;
    float silenceL[pumpFrames]{};
    float silenceR[pumpFrames]{};
    float discardL[pumpFrames]{};
    float discardR[pumpFrames]{};
    auto frames = std::min(pumpFrames, plugin->maxBlockSize);
    auto result = ProcessBlock(
        *plugin,
        silenceL, silenceR, discardL, discardR,
        frames, plugin->pumpPosition, false,
        false, 0, 0, 0,
        false);
    plugin->pumpPosition += frames;
    return result ? 1 : 0;
}

// GUI操作で変更されたパラメータをマネージド側へ転送する。
// 同じパラメータの連続変更は最新値へまとめ、コールバックはロック外で呼び出す。
YMM4VST3_API int32_t Ymm4Vst3PluginDrainEditorParameterChanges(
    void* pluginHandle,
    ParameterChangeCallback callback,
    void* context)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (!plugin || !callback)
        return 0;

    std::unordered_map<ParamID, ParamValue> values;
    {
        std::lock_guard lock(plugin->editorParamMutex);
        values.swap(plugin->pendingEditorParams);
    }
    for (const auto& [id, value] : values)
        callback(context, id, value);
    return static_cast<int32_t>(values.size());
}

YMM4VST3_API int32_t Ymm4Vst3PluginDrainMeterParameterChanges(
    void* pluginHandle,
    MeterParameterChangeCallback callback,
    void* context)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (!plugin || !callback)
        return 0;

    int32_t count = 0;
    while (plugin->meterParameterReadIndex != plugin->meterParameterWriteIndex)
    {
        const auto& change = plugin->meterParameters[plugin->meterParameterReadIndex];
        callback(context, change.id, change.value, change.samplePosition);
        plugin->meterParameterReadIndex =
            (plugin->meterParameterReadIndex + 1) % MeterParameterCapacity;
        count++;
    }
    return count;
}

// パラメータを設定する（正規化値）。次のprocess/Pumpでプロセッサへ反映される
YMM4VST3_API int32_t Ymm4Vst3PluginSetParameter(void* pluginHandle, uint32_t paramId, double normalizedValue)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (!plugin)
        return 0;
    if (plugin->controller)
        plugin->controller->setParamNormalized(paramId, normalizedValue);
    plugin->paramTransfer.addChange(paramId, normalizedValue, 0);
    return 1;
}

// シーク時のリセット。内部バッファ（ディレイライン等）をクリアする
YMM4VST3_API int32_t Ymm4Vst3PluginReset(void* pluginHandle)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (!plugin || !plugin->processingActive)
        return 0;
    plugin->processor->setProcessing(false);
    plugin->component->setActive(false);
    if (plugin->component->setActive(true) != kResultOk)
    {
        plugin->processingActive = false;
        return 0;
    }
    plugin->processor->setProcessing(true);
    return 1;
}

YMM4VST3_API int32_t Ymm4Vst3PluginGetLatencySamples(void* pluginHandle)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (!plugin)
        return 0;
    return plugin->processor->getLatencySamples();
}

// メーター等のoutput parameterをGUIへ表示するため、コントローラーだけを更新する。
YMM4VST3_API int32_t Ymm4Vst3PluginSetControllerParameter(
    void* pluginHandle,
    uint32_t paramId,
    double normalizedValue)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (!plugin || !plugin->controller)
        return 0;
    return plugin->controller->setParamNormalized(paramId, normalizedValue) == kResultOk ? 1 : 0;
}

YMM4VST3_API int32_t Ymm4Vst3PluginConsumeRestartFlags(void* pluginHandle)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (!plugin)
        return 0;
    return plugin->restartFlags.exchange(0);
}

#if defined(DEVELOPMENT)
YMM4VST3_API void Ymm4Vst3PluginRequestRestartForTest(void* pluginHandle, int32_t flags)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (plugin)
        plugin->restartFlags.fetch_or(flags);
}

YMM4VST3_API void Ymm4Vst3PluginPerformEditForTest(
    void* pluginHandle,
    uint32_t paramId,
    double normalizedValue)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (plugin && plugin->componentHandler)
        plugin->componentHandler->performEdit(paramId, normalizedValue);
}

YMM4VST3_API double Ymm4Vst3PluginGetControllerParameterForTest(void* pluginHandle, uint32_t paramId)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (!plugin || !plugin->controller)
        return -1;
    return plugin->controller->getParamNormalized(paramId);
}
#endif

YMM4VST3_API int32_t Ymm4Vst3PluginGetState(
    void* pluginHandle,
    void** componentData, int32_t* componentSize,
    void** controllerData, int32_t* controllerSize)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (!plugin || !componentData || !componentSize || !controllerData || !controllerSize)
        return 0;

    *componentData = nullptr;
    *componentSize = 0;
    *controllerData = nullptr;
    *controllerSize = 0;

    auto copyToCoTaskMem = [](ResizableMemoryIBStream& stream, void** data, int32_t* size)
    {
        auto byteCount = stream.getCursor();
        if (byteCount == 0)
            return true;
        auto* buffer = ::CoTaskMemAlloc(byteCount);
        if (!buffer)
            return false;
        std::memcpy(buffer, stream.getData(), byteCount);
        *data = buffer;
        *size = static_cast<int32_t>(byteCount);
        return true;
    };

    auto componentStream = owned(new ResizableMemoryIBStream());
    if (plugin->component->getState(componentStream) == kResultOk)
    {
        if (!copyToCoTaskMem(*componentStream, componentData, componentSize))
            return 0;
    }

    if (plugin->controller)
    {
        auto controllerStream = owned(new ResizableMemoryIBStream());
        if (plugin->controller->getState(controllerStream) == kResultOk)
        {
            if (!copyToCoTaskMem(*controllerStream, controllerData, controllerSize))
                return 0;
        }
    }
    return 1;
}

YMM4VST3_API int32_t Ymm4Vst3PluginSetState(
    void* pluginHandle,
    const uint8_t* componentData, int32_t componentSize,
    const uint8_t* controllerData, int32_t controllerSize)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (!plugin)
        return 0;

    if (componentData && componentSize > 0)
    {
        auto stream = owned(new MemoryStream(const_cast<uint8_t*>(componentData), componentSize));
        plugin->component->setState(stream);
        if (plugin->controller)
        {
            int64 pos = 0;
            stream->seek(0, IBStream::kIBSeekSet, &pos);
            plugin->controller->setComponentState(stream);
        }
    }
    if (plugin->controller && controllerData && controllerSize > 0)
    {
        auto stream = owned(new MemoryStream(const_cast<uint8_t*>(controllerData), controllerSize));
        plugin->controller->setState(stream);
    }
    return 1;
}

YMM4VST3_API void Ymm4Vst3PluginDestroy(void* pluginHandle)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (!plugin)
        return;
    if (plugin->processingActive)
    {
        plugin->processor->setProcessing(false);
        plugin->component->setActive(false);
        plugin->processingActive = false;
    }
    plugin->processData.unprepare();
    if (plugin->controller)
        plugin->controller->setComponentHandler(nullptr);
    plugin->processor = nullptr;
    plugin->component = nullptr;
    plugin->controller = nullptr;
    plugin->provider = nullptr; // PlugProviderの破棄でterminateされる
    delete plugin;
}

YMM4VST3_API void Ymm4Vst3Free(void* buffer)
{
    ::CoTaskMemFree(buffer);
}

//------------------------------------------------------------------------
// エディタビュー
//------------------------------------------------------------------------

YMM4VST3_API void* Ymm4Vst3ViewCreate(void* pluginHandle)
{
    auto* plugin = static_cast<BridgePlugin*>(pluginHandle);
    if (!plugin || !plugin->controller)
        return nullptr;
    auto* view = plugin->controller->createView(ViewType::kEditor);
    if (!view)
        return nullptr;
    auto bridgeView = std::make_unique<BridgeView>();
    bridgeView->plugin = plugin;
    bridgeView->view = owned(view);
    if (bridgeView->view->isPlatformTypeSupported(kPlatformTypeHWND) != kResultTrue)
        return nullptr;
    return bridgeView.release();
}

YMM4VST3_API int32_t Ymm4Vst3ViewGetSize(void* viewHandle, int32_t* width, int32_t* height)
{
    auto* bridgeView = static_cast<BridgeView*>(viewHandle);
    if (!bridgeView || !width || !height)
        return 0;
    ViewRect rect{};
    if (bridgeView->view->getSize(&rect) != kResultTrue)
        return 0;
    *width = rect.getWidth();
    *height = rect.getHeight();
    return 1;
}

// IPlugViewContentScaleSupportを実装しているか（高DPI対応の判定）
YMM4VST3_API int32_t Ymm4Vst3ViewIsContentScaleSupported(void* viewHandle)
{
    auto* bridgeView = static_cast<BridgeView*>(viewHandle);
    if (!bridgeView)
        return 0;
    FUnknownPtr<IPlugViewContentScaleSupport> scaleSupport(bridgeView->view);
    return scaleSupport ? 1 : 0;
}


YMM4VST3_API int32_t Ymm4Vst3ViewCanResize(void* viewHandle)
{
    auto* bridgeView = static_cast<BridgeView*>(viewHandle);
    if (!bridgeView)
        return 0;
    return bridgeView->view->canResize() == kResultTrue ? 1 : 0;
}

YMM4VST3_API int32_t Ymm4Vst3ViewAttach(void* viewHandle, void* hwnd, ViewResizeCallback resizeCallback, void* callbackContext)
{
    auto* bridgeView = static_cast<BridgeView*>(viewHandle);
    if (!bridgeView || !hwnd || bridgeView->attached)
        return 0;
    bridgeView->frame = owned(new BridgePlugFrame(resizeCallback, callbackContext));
    bridgeView->view->setFrame(bridgeView->frame);
    if (bridgeView->view->attached(hwnd, kPlatformTypeHWND) != kResultTrue)
        return 0;
    bridgeView->attached = true;
    return 1;
}

// ウィンドウ側都合のリサイズ。制約を適用した実サイズをin/outで返す
YMM4VST3_API int32_t Ymm4Vst3ViewOnSize(void* viewHandle, int32_t* width, int32_t* height)
{
    auto* bridgeView = static_cast<BridgeView*>(viewHandle);
    if (!bridgeView || !width || !height || !bridgeView->attached)
        return 0;
    ViewRect rect{ 0, 0, *width, *height };
    bridgeView->view->checkSizeConstraint(&rect);
    if (bridgeView->view->onSize(&rect) != kResultTrue)
        return 0;
    *width = rect.getWidth();
    *height = rect.getHeight();
    return 1;
}

// 高DPI用のコンテンツスケール通知。プラグインが対応していない場合は0を返す
YMM4VST3_API int32_t Ymm4Vst3ViewSetContentScale(void* viewHandle, float scaleFactor)
{
    auto* bridgeView = static_cast<BridgeView*>(viewHandle);
    if (!bridgeView || scaleFactor <= 0)
        return 0;
    FUnknownPtr<IPlugViewContentScaleSupport> scaleSupport(bridgeView->view);
    if (!scaleSupport)
        return 0;
    return scaleSupport->setContentScaleFactor(scaleFactor) == kResultTrue ? 1 : 0;
}

YMM4VST3_API void Ymm4Vst3ViewDestroy(void* viewHandle)
{
    auto* bridgeView = static_cast<BridgeView*>(viewHandle);
    if (!bridgeView)
        return;
    if (bridgeView->attached)
        bridgeView->view->removed();
    bridgeView->view->setFrame(nullptr);
    delete bridgeView;
}

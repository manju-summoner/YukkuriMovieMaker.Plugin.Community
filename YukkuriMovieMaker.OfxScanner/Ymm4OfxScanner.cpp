//-----------------------------------------------------------------------------
// YukkuriMovieMaker.OfxScanner
// OFXバイナリ（.ofx）のプラグイン列挙・describeを行うコンソールツール。
// 壊れた・ハングするプラグインの読み込みからYMM4本体を隔離するため、
// スキャン時のバイナリロードは本体ではなくこのプロセス内で行う
// （クラッシュしてもYMM4は巻き込まれず、親側がスキップして継続する）。
// ネイティブEXEなのでYMM4のself-contained配布（.NETランタイム無し環境）でも動作する。
//
// このファイルは describe アクションまでを駆動する最小のOFXホストを実装する。
// 本実装のホスト能力宣言・既定プロパティは、Community側のC#ホスト
// （OfxHostDescriptor / OfxEffectDescriptor / OfxParam）と一致させること。
// 対応可否のフィルタリングはここでは行わず、describe結果を生のまま親へ返し、
// 親（OpenFxScannerProcess / OpenFxPluginScanner）がプロセス内スキャンと同じ基準で判定する。
//
// プロトコル（UTF-8・タブ区切り・1行1メッセージ）:
//   stdin : スキャン対象バイナリパスを1行1件で受け取り、EOFで終了する
//   stdout: #BEGIN <path>
//           PLUGIN <id> <verMajor> <verMinor> <label> <grouping> <contexts('|'区切り)> <pixelDepths('|'区切り)> <singleInstance(0/1)> <temporalClipAccess(0/1)>
//                  <OpenGL> <CUDA> <CUDAStream> <OpenCLRender> <OpenCL> <Metal> <CPU>（GPU関連値はtrue/false/needed）
//           #END <path>
//           バイナリを開けない場合・プラグインのdescribe失敗は #ERROR <message>（バイナリ単位で継続）
//           プラグインが標準出力へ書き込む可能性があるため、親は不明な行を無視する
//-----------------------------------------------------------------------------

#include <windows.h>

#include <cfloat>
#include <climits>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <cwchar>
#include <deque>
#include <map>
#include <memory>
#include <string>
#include <vector>

#include "openfx/include/ofxCore.h"
#include "openfx/include/ofxProperty.h"
#include "openfx/include/ofxParam.h"
#include "openfx/include/ofxImageEffect.h"
#include "openfx/include/ofxMemory.h"
#include "openfx/include/ofxMultiThread.h"
#include "openfx/include/ofxGPURender.h"
#include "openfx/include/ofxProgress.h"
#include "openfx/include/ofxTimeLine.h"
#include "openfx/include/ofxMessage.h"

namespace
{
    //=========================================================================
    // 入出力（Vst3Scannerと同じ流儀）
    //=========================================================================

    std::string WideToUtf8(const std::wstring& value)
    {
        if (value.empty())
            return {};
        auto requiredSize = WideCharToMultiByte(CP_UTF8, 0, value.data(), static_cast<int>(value.size()), nullptr, 0,
                                                nullptr, nullptr);
        if (requiredSize <= 0)
            return "<UTF-8変換失敗>";
        std::string result(static_cast<size_t>(requiredSize), '\0');
        if (WideCharToMultiByte(CP_UTF8, 0, value.data(), static_cast<int>(value.size()), result.data(), requiredSize,
                                nullptr, nullptr) <= 0)
            return "<UTF-8変換失敗>";
        return result;
    }

    std::wstring Utf8ToWide(const std::string& value)
    {
        if (value.empty())
            return {};
        auto requiredSize = MultiByteToWideChar(CP_UTF8, 0, value.data(), static_cast<int>(value.size()), nullptr, 0);
        if (requiredSize <= 0)
            return {};
        std::wstring result(static_cast<size_t>(requiredSize), L'\0');
        if (MultiByteToWideChar(CP_UTF8, 0, value.data(), static_cast<int>(value.size()), result.data(),
                                requiredSize) <= 0)
            return {};
        return result;
    }

    // タブ・改行はプロトコルの区切りと衝突するため空白へ潰す
    std::string Sanitize(std::string value)
    {
        for (auto& c : value)
        {
            if (c == '\t' || c == '\r' || c == '\n')
                c = ' ';
        }
        return value;
    }

    void WriteLine(const std::string& line)
    {
        // プラグインが標準出力へ改行なしの断片を書き残していても、プロトコル行が
        // その断片へ連結されないよう先頭にも改行を出す（空行・不明行は親が無視する）
        std::fputc('\n', stdout);
        std::fwrite(line.data(), 1, line.size(), stdout);
        std::fputc('\n', stdout);
        std::fflush(stdout);
    }

    bool ReadLine(std::string& line)
    {
        line.clear();
        while (true)
        {
            auto c = std::fgetc(stdin);
            if (c == EOF)
                return !line.empty();
            if (c == '\n')
                return true;
            line.push_back(static_cast<char>(c));
        }
    }

    //=========================================================================
    // プロパティセット（C#側 OfxPropertySet の describe 用サブセット）
    //=========================================================================

    enum class PropKind
    {
        Int,
        Double,
        Pointer,
        String,
    };

    struct PropEntry
    {
        PropKind kind = PropKind::Int;
        std::vector<int> ints;
        std::vector<double> doubles;
        std::vector<void*> pointers;
        // propGetString が返す char* の寿命を「値の変更まで」保証するため、
        // 要素の再配置で既存要素が移動しない deque に格納する
        std::deque<std::string> strings;

        int Dimension() const
        {
            switch (kind)
            {
            case PropKind::Int: return static_cast<int>(ints.size());
            case PropKind::Double: return static_cast<int>(doubles.size());
            case PropKind::Pointer: return static_cast<int>(pointers.size());
            case PropKind::String: return static_cast<int>(strings.size());
            }
            return 0;
        }

        void Clear()
        {
            ints.clear();
            doubles.clear();
            pointers.clear();
            strings.clear();
        }
    };

    class PropertySet
    {
    public:
        //---------------------------------------------------------------------
        // ホスト側からの構築用
        //---------------------------------------------------------------------

        void SetInt(const char* name, int value, int index = 0)
        {
            Entry(name, PropKind::Int).SetAt(*this, name, index, value);
        }

        void SetDouble(const char* name, double value, int index = 0)
        {
            Entry(name, PropKind::Double).SetAt(*this, name, index, value);
        }

        void SetPointer(const char* name, void* value, int index = 0)
        {
            Entry(name, PropKind::Pointer).SetAt(*this, name, index, value);
        }

        void SetString(const char* name, const std::string& value, int index = 0)
        {
            Entry(name, PropKind::String).SetAt(*this, name, index, value);
        }

        void SetIntN(const char* name, std::initializer_list<int> values)
        {
            auto& entry = RawEntry(name, PropKind::Int);
            entry.Clear();
            entry.ints.assign(values);
        }

        void SetStringN(const char* name, std::initializer_list<const char*> values)
        {
            auto& entry = RawEntry(name, PropKind::String);
            entry.Clear();
            for (auto value : values)
                entry.strings.emplace_back(value);
        }

        /// <summary>プロパティを空の次元0で定義する（存在はするが値が無い状態）</summary>
        void SetEmpty(const char* name, PropKind kind)
        {
            RawEntry(name, kind).Clear();
        }

        int GetIntOrDefault(const char* name, int defaultValue, int index = 0) const
        {
            auto found = entries.find(name);
            if (found == entries.end())
                return defaultValue;
            const auto& entry = found->second;
            if (entry.kind == PropKind::Int && index < static_cast<int>(entry.ints.size()))
                return entry.ints[index];
            if (entry.kind == PropKind::Double && index < static_cast<int>(entry.doubles.size()))
                return static_cast<int>(entry.doubles[index]);
            return defaultValue;
        }

        std::string GetStringOrDefault(const char* name, const std::string& defaultValue, int index = 0) const
        {
            auto found = entries.find(name);
            if (found == entries.end())
                return defaultValue;
            const auto& entry = found->second;
            if (entry.kind == PropKind::String && index < static_cast<int>(entry.strings.size()))
                return entry.strings[index];
            return defaultValue;
        }

        std::vector<std::string> GetStrings(const char* name) const
        {
            std::vector<std::string> result;
            auto found = entries.find(name);
            if (found == entries.end() || found->second.kind != PropKind::String)
                return result;
            for (const auto& value : found->second.strings)
                result.push_back(value);
            return result;
        }

        /// <summary>現在の値を propReset の復元先（既定値）としてスナップショットする</summary>
        void SealDefaults()
        {
            defaults = entries;
        }

        //---------------------------------------------------------------------
        // OfxPropertySuiteV1 実装用
        //---------------------------------------------------------------------

        template <typename T>
        OfxStatus NativeSet(const char* name, PropKind kind, int index, T value)
        {
            if (index < 0)
                return kOfxStatErrBadIndex;
            Entry(name, kind).SetAt(*this, name, index, value);
            return kOfxStatOK;
        }

        template <typename T>
        OfxStatus NativeSetN(const char* name, PropKind kind, int count, const T* values)
        {
            if (count < 0 || (count > 0 && values == nullptr))
                return kOfxStatErrValue;
            RawEntry(name, kind).Clear();
            auto entry = Entry(name, kind);
            for (int i = 0; i < count; i++)
                entry.Append(values[i]);
            return kOfxStatOK;
        }

        OfxStatus NativeGetInt(const char* name, int index, int* value)
        {
            if (value == nullptr)
                return kOfxStatErrValue;
            *value = 0;
            const PropEntry* entry;
            if (auto status = FindEntry(name, index, entry); status != kOfxStatOK)
                return status;
            if (entry->kind == PropKind::Int)
                *value = entry->ints[index];
            else if (entry->kind == PropKind::Double)
                *value = static_cast<int>(entry->doubles[index]);
            else
                return kOfxStatErrValue;
            return kOfxStatOK;
        }

        OfxStatus NativeGetDouble(const char* name, int index, double* value)
        {
            if (value == nullptr)
                return kOfxStatErrValue;
            *value = 0;
            const PropEntry* entry;
            if (auto status = FindEntry(name, index, entry); status != kOfxStatOK)
                return status;
            if (entry->kind == PropKind::Double)
                *value = entry->doubles[index];
            else if (entry->kind == PropKind::Int)
                *value = entry->ints[index];
            else
                return kOfxStatErrValue;
            return kOfxStatOK;
        }

        OfxStatus NativeGetPointer(const char* name, int index, void** value)
        {
            if (value == nullptr)
                return kOfxStatErrValue;
            *value = nullptr;
            const PropEntry* entry;
            if (auto status = FindEntry(name, index, entry); status != kOfxStatOK)
                return status;
            if (entry->kind != PropKind::Pointer)
                return kOfxStatErrValue;
            *value = entry->pointers[index];
            return kOfxStatOK;
        }

        // 返す char* はこのプロパティセットが所有し、値の変更または破棄まで有効
        OfxStatus NativeGetString(const char* name, int index, char** value)
        {
            if (value == nullptr)
                return kOfxStatErrValue;
            *value = nullptr;
            const PropEntry* entry;
            if (auto status = FindEntry(name, index, entry); status != kOfxStatOK)
                return status;
            if (entry->kind != PropKind::String)
                return kOfxStatErrValue;
            *value = const_cast<char*>(entry->strings[index].c_str());
            return kOfxStatOK;
        }

        OfxStatus NativeReset(const char* name)
        {
            auto found = entries.find(name);
            if (found == entries.end())
                return kOfxStatErrUnknown;
            // 仕様上「既定値へ戻す」。既定値をスナップショット済み（SealDefaults）ならそれを復元する
            auto defaultFound = defaults.find(name);
            if (defaultFound != defaults.end())
                found->second = defaultFound->second;
            else
                found->second.Clear();
            return kOfxStatOK;
        }

        OfxStatus NativeGetDimension(const char* name, int* count)
        {
            if (count == nullptr)
                return kOfxStatErrValue;
            *count = 0;
            auto found = entries.find(name);
            if (found == entries.end())
                return kOfxStatErrUnknown;
            *count = found->second.Dimension();
            return kOfxStatOK;
        }

    private:
        struct EntryRef
        {
            PropEntry& entry;

            void SetAt(PropertySet&, const char*, int index, int value)
            {
                Fill(entry.ints, index, 0);
                entry.ints[index] = value;
            }

            void SetAt(PropertySet&, const char*, int index, double value)
            {
                Fill(entry.doubles, index, 0.0);
                entry.doubles[index] = value;
            }

            void SetAt(PropertySet&, const char*, int index, void* value)
            {
                Fill(entry.pointers, index, static_cast<void*>(nullptr));
                entry.pointers[index] = value;
            }

            void SetAt(PropertySet&, const char*, int index, const std::string& value)
            {
                while (static_cast<int>(entry.strings.size()) <= index)
                    entry.strings.emplace_back();
                entry.strings[index] = value;
            }

            void SetAt(PropertySet& owner, const char* name, int index, const char* value)
            {
                SetAt(owner, name, index, std::string(value != nullptr ? value : ""));
            }

            void Append(int value) { entry.ints.push_back(value); }
            void Append(double value) { entry.doubles.push_back(value); }
            void Append(void* value) { entry.pointers.push_back(value); }
            void Append(const char* value) { entry.strings.emplace_back(value != nullptr ? value : ""); }

        private:
            template <typename T>
            static void Fill(std::vector<T>& values, int index, T defaultValue)
            {
                while (static_cast<int>(values.size()) <= index)
                    values.push_back(defaultValue);
            }
        };

        PropEntry& RawEntry(const char* name, PropKind kind)
        {
            auto& entry = entries[name];
            if (entry.kind != kind)
            {
                // 型が変わる上書きは値・既定値を破棄して置き換える（propResetで旧型の値を復元しない）
                entry.kind = kind;
                entry.Clear();
                defaults.erase(name);
            }
            return entry;
        }

        EntryRef Entry(const char* name, PropKind kind)
        {
            return EntryRef{RawEntry(name, kind)};
        }

        OfxStatus FindEntry(const char* name, int index, const PropEntry*& entry) const
        {
            auto found = entries.find(name);
            if (found == entries.end())
                return kOfxStatErrUnknown;
            entry = &found->second;
            if (index < 0 || index >= entry->Dimension())
                return kOfxStatErrBadIndex;
            return kOfxStatOK;
        }

        std::map<std::string, PropEntry> entries;
        std::map<std::string, PropEntry> defaults;
    };

    //=========================================================================
    // describe対象のオブジェクト（エフェクト記述子・クリップ・パラメータ）
    //=========================================================================

    struct ParamDescriptor
    {
        std::string name;
        std::string type;
        PropertySet props;
    };

    struct ParamSetDescriptor
    {
        PropertySet props;
        std::vector<std::unique_ptr<ParamDescriptor>> params;
    };

    struct ClipDescriptor
    {
        std::string name;
        PropertySet props;
    };

    struct EffectDescriptor
    {
        PropertySet props;
        ParamSetDescriptor paramSet;
        std::vector<std::unique_ptr<ClipDescriptor>> clips;
    };

    int GetParamDimension(const std::string& type)
    {
        if (type == kOfxParamTypeInteger || type == kOfxParamTypeDouble || type == kOfxParamTypeBoolean
            || type == kOfxParamTypeChoice || type == kOfxParamTypeString || type == kOfxParamTypeCustom
            || type == kOfxParamTypeStrChoice)
            return 1;
        if (type == kOfxParamTypeDouble2D || type == kOfxParamTypeInteger2D)
            return 2;
        if (type == kOfxParamTypeDouble3D || type == kOfxParamTypeInteger3D || type == kOfxParamTypeRGB)
            return 3;
        if (type == kOfxParamTypeRGBA)
            return 4;
        return 0;
    }

    bool IsDoubleParamType(const std::string& type)
    {
        return type == kOfxParamTypeDouble || type == kOfxParamTypeDouble2D || type == kOfxParamTypeDouble3D
            || type == kOfxParamTypeRGB || type == kOfxParamTypeRGBA;
    }

    bool IsIntParamType(const std::string& type)
    {
        return type == kOfxParamTypeInteger || type == kOfxParamTypeInteger2D || type == kOfxParamTypeInteger3D
            || type == kOfxParamTypeBoolean || type == kOfxParamTypeChoice;
    }

    bool IsStringParamType(const std::string& type)
    {
        return type == kOfxParamTypeString || type == kOfxParamTypeCustom || type == kOfxParamTypeStrChoice;
    }

    // C#ホストが受理するパラメータ型と一致させる（未知の型は kOfxStatErrUnsupported）
    bool IsKnownParamType(const std::string& type)
    {
        return GetParamDimension(type) > 0
            || type == kOfxParamTypeGroup
            || type == kOfxParamTypePage
            || type == kOfxParamTypePushButton;
    }

    // C#側 OfxParam.FillDefaultProperties と同じ既定プロパティを埋める。
    // openfx-misc等が使うOFX C++ Supportライブラリはプロパティの存在検証を行うため、
    // 主要プロパティは読み出しに応えられるよう事前定義しておく必要がある
    void FillParamDefaultProperties(ParamDescriptor& param)
    {
        auto& props = param.props;
        const auto& type = param.type;
        const auto& name = param.name;
        props.SetString(kOfxPropType, kOfxTypeParameter);
        props.SetString(kOfxPropName, name);
        props.SetString(kOfxPropLabel, name);
        props.SetString(kOfxPropShortLabel, name);
        props.SetString(kOfxPropLongLabel, name);
        props.SetString(kOfxParamPropType, type);
        props.SetInt(kOfxParamPropSecret, 0);
        props.SetInt(kOfxParamPropCanUndo, 1);
        props.SetString(kOfxParamPropHint, "");
        props.SetString(kOfxParamPropScriptName, name);
        props.SetString(kOfxParamPropParent, "");
        props.SetInt(kOfxParamPropEnabled, 1);
        props.SetPointer(kOfxParamPropDataPtr, nullptr);

        auto dimension = GetParamDimension(type);
        if (dimension > 0)
        {
            props.SetInt(kOfxParamPropPersistant, 1);
            props.SetInt(kOfxParamPropEvaluateOnChange, 1);
            props.SetInt(kOfxParamPropIsAnimating, 0);
            props.SetInt(kOfxParamPropIsAutoKeying, 0);
            props.SetString(kOfxParamPropCacheInvalidation, kOfxParamInvalidateValueChange);
            // 本ホストはアニメーション非対応と申告する（C#側 OfxParam と同じ理由）
            props.SetInt(kOfxParamPropAnimates, 0);
        }

        if (IsDoubleParamType(type))
        {
            for (int i = 0; i < dimension; i++)
            {
                props.SetDouble(kOfxParamPropDefault, 0.0, i);
                props.SetDouble(kOfxParamPropMin, -DBL_MAX, i);
                props.SetDouble(kOfxParamPropMax, DBL_MAX, i);
                props.SetDouble(kOfxParamPropDisplayMin, -DBL_MAX, i);
                props.SetDouble(kOfxParamPropDisplayMax, DBL_MAX, i);
            }
            props.SetDouble(kOfxParamPropIncrement, 1.0);
            props.SetInt(kOfxParamPropDigits, 2);
            if (type == kOfxParamTypeDouble || type == kOfxParamTypeDouble2D || type == kOfxParamTypeDouble3D)
            {
                props.SetString(kOfxParamPropDoubleType, kOfxParamDoubleTypePlain);
                props.SetString(kOfxParamPropDefaultCoordinateSystem, kOfxParamCoordinatesCanonical);
            }
            if (type == kOfxParamTypeDouble)
                props.SetInt(kOfxParamPropShowTimeMarker, 0);
            if (type == kOfxParamTypeDouble2D || type == kOfxParamTypeInteger2D)
                props.SetStringN(kOfxParamPropDimensionLabel, {"x", "y"});
            else if (type == kOfxParamTypeDouble3D)
                props.SetStringN(kOfxParamPropDimensionLabel, {"x", "y", "z"});
            else if (type == kOfxParamTypeRGB)
                props.SetStringN(kOfxParamPropDimensionLabel, {"r", "g", "b"});
            else if (type == kOfxParamTypeRGBA)
                props.SetStringN(kOfxParamPropDimensionLabel, {"r", "g", "b", "a"});
        }
        else if (IsIntParamType(type))
        {
            for (int i = 0; i < dimension; i++)
            {
                props.SetInt(kOfxParamPropDefault, 0, i);
                if (type != kOfxParamTypeBoolean && type != kOfxParamTypeChoice)
                {
                    props.SetInt(kOfxParamPropMin, INT_MIN, i);
                    props.SetInt(kOfxParamPropMax, INT_MAX, i);
                    props.SetInt(kOfxParamPropDisplayMin, INT_MIN, i);
                    props.SetInt(kOfxParamPropDisplayMax, INT_MAX, i);
                }
            }
            if (type == kOfxParamTypeInteger2D)
                props.SetStringN(kOfxParamPropDimensionLabel, {"x", "y"});
            else if (type == kOfxParamTypeInteger3D)
                props.SetStringN(kOfxParamPropDimensionLabel, {"x", "y", "z"});
            else if (type == kOfxParamTypeChoice)
                props.SetEmpty(kOfxParamPropChoiceOption, PropKind::String);
        }
        else if (IsStringParamType(type))
        {
            props.SetString(kOfxParamPropDefault, "");
            if (type == kOfxParamTypeString)
            {
                props.SetString(kOfxParamPropStringMode, kOfxParamStringIsSingleLine);
                props.SetInt(kOfxParamPropStringFilePathExists, 1);
            }
            if (type == kOfxParamTypeStrChoice)
            {
                props.SetEmpty(kOfxParamPropChoiceOption, PropKind::String);
                props.SetEmpty(kOfxParamPropChoiceEnum, PropKind::String);
            }
        }
        else if (type == kOfxParamTypeGroup)
        {
            props.SetInt(kOfxParamPropGroupOpen, 1);
        }
        else if (type == kOfxParamTypePage)
        {
            props.SetEmpty(kOfxParamPropPageChild, PropKind::String);
        }
        props.SealDefaults();
    }

    // C#側 OfxClipDescriptor と同じ既定プロパティを埋める
    void FillClipDefaultProperties(ClipDescriptor& clip)
    {
        auto& props = clip.props;
        props.SetString(kOfxPropType, kOfxTypeClip);
        props.SetString(kOfxPropName, clip.name);
        props.SetString(kOfxPropLabel, clip.name);
        props.SetString(kOfxPropShortLabel, clip.name);
        props.SetString(kOfxPropLongLabel, clip.name);
        props.SetEmpty(kOfxImageEffectPropSupportedComponents, PropKind::String);
        props.SetInt(kOfxImageEffectPropTemporalClipAccess, 0);
        props.SetInt(kOfxImageClipPropOptional, 0);
        props.SetString(kOfxImageClipPropFieldExtraction, kOfxImageFieldDoubled);
        props.SetInt(kOfxImageClipPropIsMask, 0);
        props.SetInt(kOfxImageEffectPropSupportsTiles, 1);
        props.SealDefaults();
    }

    // kOfxPluginPropFilePath 用のパスを得る。仕様上このプロパティはバンドルの場所を指すため、
    // バンドル形式（(名前).ofx.bundle\Contents\Win64\(名前).ofx）ならバンドルルートを返す
    // （C#側 OfxEffectDescriptor.ResolveBundlePath と同じ判定）
    std::string ResolveBundlePath(const std::string& binaryPath)
    {
        auto parent = [](const std::string& path) -> std::string
        {
            auto separator = path.find_last_of("\\/");
            return separator == std::string::npos ? std::string() : path.substr(0, separator);
        };
        auto fileName = [](const std::string& path) -> std::string
        {
            auto separator = path.find_last_of("\\/");
            return separator == std::string::npos ? path : path.substr(separator + 1);
        };
        auto equalsIgnoreCase = [](const std::string& a, const char* b)
        {
            return _stricmp(a.c_str(), b) == 0;
        };
        auto win64 = parent(binaryPath);
        auto contents = win64.empty() ? std::string() : parent(win64);
        auto bundle = contents.empty() ? std::string() : parent(contents);
        if (!bundle.empty()
            && bundle.size() >= 11 && equalsIgnoreCase(bundle.substr(bundle.size() - 11), ".ofx.bundle")
            && equalsIgnoreCase(fileName(win64), "Win64")
            && equalsIgnoreCase(fileName(contents), "Contents"))
        {
            return bundle;
        }
        return binaryPath;
    }

    // C#側 OfxEffectDescriptor.FillDefaultProperties と同じ既定プロパティを埋める
    void FillEffectDefaultProperties(EffectDescriptor& descriptor, const std::string& binaryPath)
    {
        auto& props = descriptor.props;
        props.SetString(kOfxPropType, kOfxTypeImageEffect);
        props.SetString(kOfxPropLabel, "");
        props.SetString(kOfxPropShortLabel, "");
        props.SetString(kOfxPropLongLabel, "");
        props.SetString(kOfxPropPluginDescription, "");
        props.SetIntN(kOfxPropVersion, {0});
        props.SetString(kOfxPropVersionLabel, "");
        props.SetStringN(kOfxPropIcon, {"", ""});
        props.SetEmpty(kOfxImageEffectPropSupportedContexts, PropKind::String);
        props.SetEmpty(kOfxImageEffectPropSupportedPixelDepths, PropKind::String);
        props.SetString(kOfxImageEffectPluginPropGrouping, "");
        props.SetInt(kOfxImageEffectPluginPropSingleInstance, 0);
        props.SetString(kOfxImageEffectPluginRenderThreadSafety, kOfxImageEffectRenderFullySafe);
        props.SetInt(kOfxImageEffectPluginPropHostFrameThreading, 0);
        props.SetInt(kOfxImageEffectPropSupportsMultiResolution, 1);
        props.SetInt(kOfxImageEffectPropSupportsTiles, 1);
        props.SetInt(kOfxImageEffectPropTemporalClipAccess, 0);
        props.SetInt(kOfxImageEffectPluginPropFieldRenderTwiceAlways, 1);
        props.SetInt(kOfxImageEffectPropSupportsMultipleClipDepths, 0);
        props.SetInt(kOfxImageEffectPropSupportsMultipleClipPARs, 0);
        props.SetEmpty(kOfxImageEffectPropClipPreferencesSlaveParam, PropKind::String);
        props.SetPointer(kOfxImageEffectPluginPropOverlayInteractV1, nullptr);
        // C#側 OfxEffectDescriptor と同じ、ofxGPURender.h準拠のプラグインdescriptor既定値
        props.SetString(kOfxImageEffectPropOpenGLRenderSupported, "false");
        props.SetString(kOfxImageEffectPropCudaRenderSupported, "false");
        props.SetString(kOfxImageEffectPropCudaStreamSupported, "false");
        props.SetString(kOfxImageEffectPropOpenCLRenderSupported, "false");
        props.SetString(kOfxImageEffectPropOpenCLSupported, "false");
        props.SetString(kOfxImageEffectPropMetalRenderSupported, "false");
        props.SetString(kOfxImageEffectPropCPURenderSupported, "true");
        props.SetString(kOfxPluginPropFilePath, ResolveBundlePath(binaryPath));
        props.SealDefaults();
    }

    //=========================================================================
    // スイート実装（describe 駆動に必要な範囲のみ）
    //=========================================================================

    PropertySet* AsPropertySet(OfxPropertySetHandle handle) { return reinterpret_cast<PropertySet*>(handle); }
    EffectDescriptor* AsEffect(OfxImageEffectHandle handle) { return reinterpret_cast<EffectDescriptor*>(handle); }
    ParamSetDescriptor* AsParamSet(OfxParamSetHandle handle) { return reinterpret_cast<ParamSetDescriptor*>(handle); }
    ParamDescriptor* AsParam(OfxParamHandle handle) { return reinterpret_cast<ParamDescriptor*>(handle); }

    //------------------------------------------------------------------ Property

    OfxStatus PropSetPointer(OfxPropertySetHandle properties, const char* name, int index, void* value)
    {
        if (properties == nullptr) return kOfxStatErrBadHandle;
        if (name == nullptr) return kOfxStatErrValue;
        return AsPropertySet(properties)->NativeSet(name, PropKind::Pointer, index, value);
    }

    OfxStatus PropSetString(OfxPropertySetHandle properties, const char* name, int index, const char* value)
    {
        if (properties == nullptr) return kOfxStatErrBadHandle;
        if (name == nullptr) return kOfxStatErrValue;
        return AsPropertySet(properties)->NativeSet(name, PropKind::String, index, value);
    }

    OfxStatus PropSetDouble(OfxPropertySetHandle properties, const char* name, int index, double value)
    {
        if (properties == nullptr) return kOfxStatErrBadHandle;
        if (name == nullptr) return kOfxStatErrValue;
        return AsPropertySet(properties)->NativeSet(name, PropKind::Double, index, value);
    }

    OfxStatus PropSetInt(OfxPropertySetHandle properties, const char* name, int index, int value)
    {
        if (properties == nullptr) return kOfxStatErrBadHandle;
        if (name == nullptr) return kOfxStatErrValue;
        return AsPropertySet(properties)->NativeSet(name, PropKind::Int, index, value);
    }

    OfxStatus PropSetPointerN(OfxPropertySetHandle properties, const char* name, int count, void* const* value)
    {
        if (properties == nullptr) return kOfxStatErrBadHandle;
        if (name == nullptr) return kOfxStatErrValue;
        return AsPropertySet(properties)->NativeSetN(name, PropKind::Pointer, count, value);
    }

    OfxStatus PropSetStringN(OfxPropertySetHandle properties, const char* name, int count, const char* const* value)
    {
        if (properties == nullptr) return kOfxStatErrBadHandle;
        if (name == nullptr) return kOfxStatErrValue;
        return AsPropertySet(properties)->NativeSetN(name, PropKind::String, count, value);
    }

    OfxStatus PropSetDoubleN(OfxPropertySetHandle properties, const char* name, int count, const double* value)
    {
        if (properties == nullptr) return kOfxStatErrBadHandle;
        if (name == nullptr) return kOfxStatErrValue;
        return AsPropertySet(properties)->NativeSetN(name, PropKind::Double, count, value);
    }

    OfxStatus PropSetIntN(OfxPropertySetHandle properties, const char* name, int count, const int* value)
    {
        if (properties == nullptr) return kOfxStatErrBadHandle;
        if (name == nullptr) return kOfxStatErrValue;
        return AsPropertySet(properties)->NativeSetN(name, PropKind::Int, count, value);
    }

    OfxStatus PropGetPointer(OfxPropertySetHandle properties, const char* name, int index, void** value)
    {
        if (properties == nullptr) return kOfxStatErrBadHandle;
        if (name == nullptr) return kOfxStatErrValue;
        return AsPropertySet(properties)->NativeGetPointer(name, index, value);
    }

    OfxStatus PropGetString(OfxPropertySetHandle properties, const char* name, int index, char** value)
    {
        if (properties == nullptr) return kOfxStatErrBadHandle;
        if (name == nullptr) return kOfxStatErrValue;
        return AsPropertySet(properties)->NativeGetString(name, index, value);
    }

    OfxStatus PropGetDouble(OfxPropertySetHandle properties, const char* name, int index, double* value)
    {
        if (properties == nullptr) return kOfxStatErrBadHandle;
        if (name == nullptr) return kOfxStatErrValue;
        return AsPropertySet(properties)->NativeGetDouble(name, index, value);
    }

    OfxStatus PropGetInt(OfxPropertySetHandle properties, const char* name, int index, int* value)
    {
        if (properties == nullptr) return kOfxStatErrBadHandle;
        if (name == nullptr) return kOfxStatErrValue;
        return AsPropertySet(properties)->NativeGetInt(name, index, value);
    }

    OfxStatus PropGetPointerN(OfxPropertySetHandle properties, const char* name, int count, void** value)
    {
        if (count < 0)
            return kOfxStatErrValue;
        for (int i = 0; i < count; i++)
        {
            if (auto status = PropGetPointer(properties, name, i, value + i); status != kOfxStatOK)
                return status;
        }
        return kOfxStatOK;
    }

    OfxStatus PropGetStringN(OfxPropertySetHandle properties, const char* name, int count, char** value)
    {
        if (count < 0)
            return kOfxStatErrValue;
        for (int i = 0; i < count; i++)
        {
            if (auto status = PropGetString(properties, name, i, value + i); status != kOfxStatOK)
                return status;
        }
        return kOfxStatOK;
    }

    OfxStatus PropGetDoubleN(OfxPropertySetHandle properties, const char* name, int count, double* value)
    {
        if (count < 0)
            return kOfxStatErrValue;
        for (int i = 0; i < count; i++)
        {
            if (auto status = PropGetDouble(properties, name, i, value + i); status != kOfxStatOK)
                return status;
        }
        return kOfxStatOK;
    }

    OfxStatus PropGetIntN(OfxPropertySetHandle properties, const char* name, int count, int* value)
    {
        if (count < 0)
            return kOfxStatErrValue;
        for (int i = 0; i < count; i++)
        {
            if (auto status = PropGetInt(properties, name, i, value + i); status != kOfxStatOK)
                return status;
        }
        return kOfxStatOK;
    }

    OfxStatus PropReset(OfxPropertySetHandle properties, const char* name)
    {
        if (properties == nullptr) return kOfxStatErrBadHandle;
        if (name == nullptr) return kOfxStatErrValue;
        return AsPropertySet(properties)->NativeReset(name);
    }

    OfxStatus PropGetDimension(OfxPropertySetHandle properties, const char* name, int* count)
    {
        if (properties == nullptr) return kOfxStatErrBadHandle;
        if (name == nullptr) return kOfxStatErrValue;
        return AsPropertySet(properties)->NativeGetDimension(name, count);
    }

    OfxPropertySuiteV1 propertySuite =
    {
        PropSetPointer, PropSetString, PropSetDouble, PropSetInt,
        PropSetPointerN, PropSetStringN, PropSetDoubleN, PropSetIntN,
        PropGetPointer, PropGetString, PropGetDouble, PropGetInt,
        PropGetPointerN, PropGetStringN, PropGetDoubleN, PropGetIntN,
        PropReset, PropGetDimension,
    };

    //------------------------------------------------------------------ ImageEffect

    OfxStatus EffectGetPropertySet(OfxImageEffectHandle imageEffect, OfxPropertySetHandle* propHandle)
    {
        if (imageEffect == nullptr || propHandle == nullptr) return kOfxStatErrBadHandle;
        *propHandle = reinterpret_cast<OfxPropertySetHandle>(&AsEffect(imageEffect)->props);
        return kOfxStatOK;
    }

    OfxStatus EffectGetParamSet(OfxImageEffectHandle imageEffect, OfxParamSetHandle* paramSet)
    {
        if (imageEffect == nullptr || paramSet == nullptr) return kOfxStatErrBadHandle;
        *paramSet = reinterpret_cast<OfxParamSetHandle>(&AsEffect(imageEffect)->paramSet);
        return kOfxStatOK;
    }

    OfxStatus EffectClipDefine(OfxImageEffectHandle imageEffect, const char* name, OfxPropertySetHandle* propertySet)
    {
        if (imageEffect == nullptr || name == nullptr) return kOfxStatErrBadHandle;
        auto effect = AsEffect(imageEffect);
        ClipDescriptor* clip = nullptr;
        for (const auto& existing : effect->clips)
        {
            if (existing->name == name)
            {
                clip = existing.get();
                break;
            }
        }
        if (clip == nullptr)
        {
            auto created = std::make_unique<ClipDescriptor>();
            created->name = name;
            FillClipDefaultProperties(*created);
            clip = created.get();
            effect->clips.push_back(std::move(created));
        }
        if (propertySet != nullptr)
            *propertySet = reinterpret_cast<OfxPropertySetHandle>(&clip->props);
        return kOfxStatOK;
    }

    OfxStatus EffectClipGetHandle(OfxImageEffectHandle, const char*, OfxImageClipHandle*, OfxPropertySetHandle*)
    {
        // インスタンス専用API。describeでは呼ばれない
        return kOfxStatErrBadHandle;
    }

    OfxStatus EffectClipGetPropertySet(OfxImageClipHandle, OfxPropertySetHandle*)
    {
        return kOfxStatErrBadHandle;
    }

    OfxStatus EffectClipGetImage(OfxImageClipHandle, OfxTime, const OfxRectD*, OfxPropertySetHandle*)
    {
        return kOfxStatErrBadHandle;
    }

    OfxStatus EffectClipReleaseImage(OfxPropertySetHandle)
    {
        return kOfxStatErrBadHandle;
    }

    OfxStatus EffectClipGetRegionOfDefinition(OfxImageClipHandle, OfxTime, OfxRectD*)
    {
        return kOfxStatErrBadHandle;
    }

    int EffectAbort(OfxImageEffectHandle)
    {
        return 0;
    }

    OfxStatus EffectImageMemoryAlloc(OfxImageEffectHandle, size_t nBytes, OfxImageMemoryHandle* memoryHandle)
    {
        if (memoryHandle == nullptr) return kOfxStatErrBadHandle;
        auto memory = std::malloc(nBytes > 0 ? nBytes : 1);
        if (memory == nullptr) return kOfxStatErrMemory;
        *memoryHandle = reinterpret_cast<OfxImageMemoryHandle>(memory);
        return kOfxStatOK;
    }

    OfxStatus EffectImageMemoryFree(OfxImageMemoryHandle memoryHandle)
    {
        std::free(memoryHandle);
        return kOfxStatOK;
    }

    OfxStatus EffectImageMemoryLock(OfxImageMemoryHandle memoryHandle, void** returnedPtr)
    {
        if (returnedPtr == nullptr) return kOfxStatErrBadHandle;
        *returnedPtr = memoryHandle;
        return kOfxStatOK;
    }

    OfxStatus EffectImageMemoryUnlock(OfxImageMemoryHandle)
    {
        return kOfxStatOK;
    }

    OfxImageEffectSuiteV1 imageEffectSuite =
    {
        EffectGetPropertySet, EffectGetParamSet, EffectClipDefine, EffectClipGetHandle,
        EffectClipGetPropertySet, EffectClipGetImage, EffectClipReleaseImage, EffectClipGetRegionOfDefinition,
        EffectAbort, EffectImageMemoryAlloc, EffectImageMemoryFree, EffectImageMemoryLock, EffectImageMemoryUnlock,
    };

    //------------------------------------------------------------------ Parameter

    OfxStatus ParamDefine(OfxParamSetHandle paramSet, const char* paramType, const char* name,
                          OfxPropertySetHandle* propertySet)
    {
        if (paramSet == nullptr || paramType == nullptr || name == nullptr) return kOfxStatErrBadHandle;
        if (!IsKnownParamType(paramType))
            return kOfxStatErrUnsupported;
        auto set = AsParamSet(paramSet);
        for (const auto& existing : set->params)
        {
            if (existing->name == name)
                return kOfxStatErrExists;
        }
        auto param = std::make_unique<ParamDescriptor>();
        param->name = name;
        param->type = paramType;
        FillParamDefaultProperties(*param);
        if (propertySet != nullptr)
            *propertySet = reinterpret_cast<OfxPropertySetHandle>(&param->props);
        set->params.push_back(std::move(param));
        return kOfxStatOK;
    }

    OfxStatus ParamGetHandle(OfxParamSetHandle paramSet, const char* name, OfxParamHandle* param,
                             OfxPropertySetHandle* propertySet)
    {
        if (paramSet == nullptr || name == nullptr || param == nullptr) return kOfxStatErrBadHandle;
        auto set = AsParamSet(paramSet);
        for (const auto& existing : set->params)
        {
            if (existing->name == name)
            {
                *param = reinterpret_cast<OfxParamHandle>(existing.get());
                if (propertySet != nullptr)
                    *propertySet = reinterpret_cast<OfxPropertySetHandle>(&existing->props);
                return kOfxStatOK;
            }
        }
        return kOfxStatErrUnknown;
    }

    OfxStatus ParamSetGetPropertySet(OfxParamSetHandle paramSet, OfxPropertySetHandle* propHandle)
    {
        // パラメータセット自体のプロパティは未定義（セットごとの空プロパティを返す）
        if (paramSet == nullptr || propHandle == nullptr) return kOfxStatErrBadHandle;
        *propHandle = reinterpret_cast<OfxPropertySetHandle>(&AsParamSet(paramSet)->props);
        return kOfxStatOK;
    }

    OfxStatus ParamGetPropertySet(OfxParamHandle param, OfxPropertySetHandle* propHandle)
    {
        if (param == nullptr || propHandle == nullptr) return kOfxStatErrBadHandle;
        *propHandle = reinterpret_cast<OfxPropertySetHandle>(&AsParam(param)->props);
        return kOfxStatOK;
    }

    // 値の取得・設定はインスタンス専用API。describeでは呼ばれない
    OfxStatus ParamGetValue(OfxParamHandle, ...) { return kOfxStatErrBadHandle; }
    OfxStatus ParamGetValueAtTime(OfxParamHandle, OfxTime, ...) { return kOfxStatErrBadHandle; }
    OfxStatus ParamGetDerivative(OfxParamHandle, OfxTime, ...) { return kOfxStatErrBadHandle; }
    OfxStatus ParamGetIntegral(OfxParamHandle, OfxTime, OfxTime, ...) { return kOfxStatErrBadHandle; }
    OfxStatus ParamSetValue(OfxParamHandle, ...) { return kOfxStatErrBadHandle; }
    OfxStatus ParamSetValueAtTime(OfxParamHandle, OfxTime, ...) { return kOfxStatErrBadHandle; }

    OfxStatus ParamGetNumKeys(OfxParamHandle, unsigned int* numberOfKeys)
    {
        if (numberOfKeys != nullptr)
            *numberOfKeys = 0;
        return kOfxStatOK;
    }

    OfxStatus ParamGetKeyTime(OfxParamHandle, unsigned int, OfxTime*) { return kOfxStatErrBadIndex; }
    OfxStatus ParamGetKeyIndex(OfxParamHandle, OfxTime, int, int*) { return kOfxStatFailed; }
    OfxStatus ParamDeleteKey(OfxParamHandle, OfxTime) { return kOfxStatErrBadIndex; }
    OfxStatus ParamDeleteAllKeys(OfxParamHandle) { return kOfxStatOK; }

    OfxStatus ParamCopy(OfxParamHandle, OfxParamHandle, OfxTime, const OfxRangeD*)
    {
        return kOfxStatErrMissingHostFeature;
    }

    OfxStatus ParamEditBegin(OfxParamSetHandle, const char*) { return kOfxStatOK; }
    OfxStatus ParamEditEnd(OfxParamSetHandle) { return kOfxStatOK; }

    OfxParameterSuiteV1 parameterSuite =
    {
        ParamDefine, ParamGetHandle, ParamSetGetPropertySet, ParamGetPropertySet,
        ParamGetValue, ParamGetValueAtTime, ParamGetDerivative, ParamGetIntegral,
        ParamSetValue, ParamSetValueAtTime, ParamGetNumKeys, ParamGetKeyTime,
        ParamGetKeyIndex, ParamDeleteKey, ParamDeleteAllKeys, ParamCopy,
        ParamEditBegin, ParamEditEnd,
    };

    //------------------------------------------------------------------ Memory / MultiThread / Message

    OfxStatus MemoryAlloc(void*, size_t nBytes, void** allocatedData)
    {
        if (allocatedData == nullptr) return kOfxStatErrBadHandle;
        auto memory = std::malloc(nBytes > 0 ? nBytes : 1);
        if (memory == nullptr) return kOfxStatErrMemory;
        *allocatedData = memory;
        return kOfxStatOK;
    }

    OfxStatus MemoryFree(void* allocatedData)
    {
        std::free(allocatedData);
        return kOfxStatOK;
    }

    OfxMemorySuiteV1 memorySuite = {MemoryAlloc, MemoryFree};

    OfxStatus MultiThread(OfxThreadFunctionV1 func, unsigned int nThreads, void* customArg)
    {
        if (func == nullptr) return kOfxStatFailed;
        // スキャナーは describe しか駆動しないため並列化は不要。呼び出しスレッドで逐次実行する。
        // nThreads=0 は「ホストが決める」の意味なのでCPU数（本ホスト申告値=1）で実行する
        if (nThreads == 0)
            nThreads = 1;
        for (unsigned int i = 0; i < nThreads; i++)
            func(i, nThreads, customArg);
        return kOfxStatOK;
    }

    OfxStatus MultiThreadNumCPUs(unsigned int* nCPUs)
    {
        if (nCPUs == nullptr) return kOfxStatFailed;
        *nCPUs = 1;
        return kOfxStatOK;
    }

    OfxStatus MultiThreadIndex(unsigned int* threadIndex)
    {
        if (threadIndex == nullptr) return kOfxStatFailed;
        *threadIndex = 0;
        return kOfxStatOK;
    }

    int MultiThreadIsSpawnedThread()
    {
        return 0;
    }

    OfxStatus MutexCreate(OfxMutexHandle* mutex, int lockCount)
    {
        if (mutex == nullptr) return kOfxStatFailed;
        auto section = new CRITICAL_SECTION();
        InitializeCriticalSection(section);
        for (int i = 0; i < lockCount; i++)
            EnterCriticalSection(section);
        *mutex = reinterpret_cast<OfxMutexHandle>(section);
        return kOfxStatOK;
    }

    OfxStatus MutexDestroy(OfxMutexHandle mutex)
    {
        if (mutex == nullptr) return kOfxStatErrBadHandle;
        auto section = reinterpret_cast<CRITICAL_SECTION*>(mutex);
        DeleteCriticalSection(section);
        delete section;
        return kOfxStatOK;
    }

    OfxStatus MutexLock(OfxMutexHandle mutex)
    {
        if (mutex == nullptr) return kOfxStatErrBadHandle;
        EnterCriticalSection(reinterpret_cast<CRITICAL_SECTION*>(mutex));
        return kOfxStatOK;
    }

    OfxStatus MutexUnLock(OfxMutexHandle mutex)
    {
        if (mutex == nullptr) return kOfxStatErrBadHandle;
        LeaveCriticalSection(reinterpret_cast<CRITICAL_SECTION*>(mutex));
        return kOfxStatOK;
    }

    OfxStatus MutexTryLock(OfxMutexHandle mutex)
    {
        if (mutex == nullptr) return kOfxStatErrBadHandle;
        return TryEnterCriticalSection(reinterpret_cast<CRITICAL_SECTION*>(mutex)) ? kOfxStatOK : kOfxStatFailed;
    }

    OfxMultiThreadSuiteV1 multiThreadSuite =
    {
        MultiThread, MultiThreadNumCPUs, MultiThreadIndex, MultiThreadIsSpawnedThread,
        MutexCreate, MutexDestroy, MutexLock, MutexUnLock, MutexTryLock,
    };

    OfxStatus Message(void*, const char* messageType, const char*, const char* format, ...)
    {
        // スキャン中のメッセージはstderrへ流すだけ（親は読み捨てる）
        if (format != nullptr)
        {
            std::fprintf(stderr, "[message:%s] ", messageType != nullptr ? messageType : "");
            va_list args;
            va_start(args, format);
            std::vfprintf(stderr, format, args);
            va_end(args);
            std::fputc('\n', stderr);
        }
        return kOfxStatOK;
    }

    OfxStatus SetPersistentMessage(void*, const char*, const char*, const char*, ...)
    {
        return kOfxStatOK;
    }

    OfxStatus ClearPersistentMessage(void*)
    {
        return kOfxStatOK;
    }

    OfxMessageSuiteV1 messageSuiteV1 = {Message};
    OfxMessageSuiteV2 messageSuiteV2 = {Message, SetPersistentMessage, ClearPersistentMessage};

    // ofxProgress.h / ofxTimeLine.h のV1 ABI。スキャナーには実インスタンスやタイムラインがないため、
    // Progressは受理し、TimeLine照会はスキャン時の中立値（時刻0、範囲0..0）を返す。
    OfxStatus ProgressStart(void*, const char*) { return kOfxStatOK; }
    OfxStatus ProgressUpdate(void*, double) { return kOfxStatOK; }
    OfxStatus ProgressEnd(void*) { return kOfxStatOK; }
    OfxProgressSuiteV1 progressSuite = {ProgressStart, ProgressUpdate, ProgressEnd};

    OfxStatus GetTime(void*, double* time)
    {
        if (time == nullptr) return kOfxStatErrValue;
        *time = 0.0;
        return kOfxStatOK;
    }

    OfxStatus GotoTime(void*, double) { return kOfxStatFailed; }

    OfxStatus GetTimeBounds(void*, double* firstTime, double* lastTime)
    {
        if (firstTime == nullptr || lastTime == nullptr) return kOfxStatErrValue;
        *firstTime = 0.0;
        *lastTime = 0.0;
        return kOfxStatOK;
    }

    OfxTimeLineSuiteV1 timeLineSuite = {GetTime, GotoTime, GetTimeBounds};

    OfxStatus CompileOpenCLProgramForScanner(const char*, int, void*)
    {
        // スキャナーはdescribeまでしか実行せずOpenCL contextを持たない。
        return kOfxStatFailed;
    }

    OfxOpenCLProgramSuiteV1 openCLProgramSuite = {CompileOpenCLProgramForScanner};

    //=========================================================================
    // ホスト
    //=========================================================================

    // C#側 OfxHostDescriptor.CreateHostProperties と同じ能力宣言。
    // describe結果が実行時ホストと食い違わないよう、必ず同期させること
    // （特に対応コンテキストがずれると「一覧に出ないが実行時は対応」等の静かな不整合になる。
    //   このEXEは別ビルド成果物のため、C#側の変更時は再ビルドも忘れないこと）。
    // ホストバージョンは起動引数（"major.minor.build.revision"）で親から受け取る
    void FillHostProperties(PropertySet& props, const int (&version)[4], bool cudaAvailable, bool openCLAvailable)
    {
        props.SetString(kOfxPropType, kOfxTypeImageEffectHost);
        props.SetString(kOfxPropName, "net.manjubox.YukkuriMovieMaker4");
        props.SetString(kOfxPropLabel, "YukkuriMovieMaker4");
        props.SetIntN(kOfxPropVersion, {version[0], version[1], version[2], version[3]});
        props.SetString(kOfxPropVersionLabel,
                        std::to_string(version[0]) + "." + std::to_string(version[1])
                        + "." + std::to_string(version[2]) + "." + std::to_string(version[3]));
        // CUDA能力はOFX 1.5で追加された。DrawSuiteはoverlays=falseでは必須でなく、
        // 未対応GPU APIは下の個別プロパティでfalseを明示する。
        // kOfxImageEffectPropCPURenderSupportedを含むOFX 1.5.1の能力を扱う。
        props.SetIntN(kOfxPropAPIVersion, {1, 5, 1});

        props.SetInt(kOfxImageEffectHostPropIsBackground, 0);
        props.SetInt(kOfxImageEffectPropSupportsOverlays, 0);
        props.SetInt(kOfxImageEffectPropSupportsMultiResolution, 1);
        props.SetInt(kOfxImageEffectPropSupportsTiles, 0);
        props.SetInt(kOfxImageEffectPropTemporalClipAccess, 0);
        props.SetStringN(kOfxImageEffectPropSupportedContexts, {
                             kOfxImageEffectContextFilter, kOfxImageEffectContextTransition,
                             kOfxImageEffectContextGenerator
                         });
        props.SetStringN(kOfxImageEffectPropSupportedComponents, {kOfxImageComponentRGBA});
        props.SetStringN(kOfxImageEffectPropSupportedPixelDepths, {kOfxBitDepthFloat});
        props.SetInt(kOfxImageEffectPropSupportsMultipleClipDepths, 0);
        props.SetInt(kOfxImageEffectPropSupportsMultipleClipPARs, 0);
        props.SetInt(kOfxImageEffectPropSetableFrameRate, 0);
        props.SetInt(kOfxImageEffectPropSetableFielding, 0);
        props.SetInt(kOfxImageEffectPropRenderQualityDraft, 0);
        props.SetInt(kOfxImageEffectInstancePropSequentialRender, 0);
        props.SetString(kOfxImageEffectPropOpenGLRenderSupported, "false");
        props.SetString(kOfxImageEffectPropCudaRenderSupported, cudaAvailable ? "true" : "false");
        props.SetString(kOfxImageEffectPropCudaStreamSupported, cudaAvailable ? "true" : "false");
        props.SetString(kOfxImageEffectPropOpenCLRenderSupported, openCLAvailable ? "true" : "false");
        props.SetString(kOfxImageEffectPropOpenCLSupported, "false");
        props.SetString(kOfxImageEffectPropMetalRenderSupported, "false");
        // ホストはCPUレンダリングを常時提供する（1.5.1でこのプロパティを照会するプラグイン対策。C#ホストと一致させること）
        props.SetString(kOfxImageEffectPropCPURenderSupported, "true");
        props.SetPointer(kOfxPropHostOSHandle, nullptr);
        props.SetString(kOfxImageEffectHostPropNativeOrigin, kOfxHostNativeOriginBottomLeft);

        props.SetInt(kOfxParamHostPropSupportsCustomAnimation, 0);
        props.SetInt(kOfxParamHostPropSupportsStringAnimation, 0);
        props.SetInt(kOfxParamHostPropSupportsBooleanAnimation, 0);
        props.SetInt(kOfxParamHostPropSupportsChoiceAnimation, 0);
        props.SetInt(kOfxParamHostPropSupportsStrChoice, 0);
        props.SetInt(kOfxParamHostPropSupportsStrChoiceAnimation, 0);
        props.SetInt(kOfxParamHostPropSupportsCustomInteract, 0);
        props.SetInt(kOfxParamHostPropMaxParameters, -1);
        props.SetInt(kOfxParamHostPropMaxPages, 0);
        props.SetIntN(kOfxParamHostPropPageRowColumnCount, {0, 0});
        props.SealDefaults();
    }

    const void* FetchSuite(OfxPropertySetHandle, const char* suiteName, int suiteVersion)
    {
        if (suiteName == nullptr)
            return nullptr;
        std::string name = suiteName;
        if (name == kOfxPropertySuite && suiteVersion == 1)
            return &propertySuite;
        if (name == kOfxImageEffectSuite && suiteVersion == 1)
            return &imageEffectSuite;
        if (name == kOfxParameterSuite && suiteVersion == 1)
            return &parameterSuite;
        if (name == kOfxMemorySuite && suiteVersion == 1)
            return &memorySuite;
        if (name == kOfxMultiThreadSuite && suiteVersion == 1)
            return &multiThreadSuite;
        if (name == kOfxMessageSuite && suiteVersion == 1)
            return &messageSuiteV1;
        if (name == kOfxMessageSuite && suiteVersion == 2)
            return &messageSuiteV2;
        if (name == kOfxProgressSuite && suiteVersion == 1)
            return &progressSuite;
        if (name == kOfxTimeLineSuite && suiteVersion == 1)
            return &timeLineSuite;
        if (name == kOfxOpenCLProgramSuite && suiteVersion == 1)
            return &openCLProgramSuite;
        return nullptr;
    }

    PropertySet hostProps;
    OfxHost host = {nullptr, FetchSuite};

    //=========================================================================
    // バイナリ走査
    //=========================================================================

    std::string JoinWithPipe(const std::vector<std::string>& values)
    {
        std::string result;
        for (const auto& value : values)
        {
            if (!result.empty())
                result += '|';
            result += value;
        }
        return result;
    }

    void ScanBinary(const std::string& path)
    {
        WriteLine("#BEGIN\t" + path);

        auto widePath = Utf8ToWide(path);
        if (widePath.empty())
        {
            WriteLine("#ERROR\tパスをUTF-16へ変換できませんでした。");
            WriteLine("#END\t" + path);
            return;
        }

        // 一度ロードしたバイナリはアンロードしない（プラグインが登録したままの
        // コールバックが無効な飛び先になる事故を避けるため。プロセス終了で回収される）
        // フルパスでロードし、同梱依存DLLはバイナリ自身のディレクトリから解決する。
        auto library = LoadLibraryExW(widePath.c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
        if (library == nullptr)
        {
            WriteLine("#ERROR\tバイナリを読み込めませんでした。win32Error=" + std::to_string(GetLastError()));
            WriteLine("#END\t" + path);
            return;
        }

        using GetNumberOfPluginsProc = int (*)();
        using GetPluginProc = OfxPlugin* (*)(int);
        using SetHostBinaryProc = OfxStatus (*)(const OfxHost*);
        auto getNumberOfPlugins = reinterpret_cast<GetNumberOfPluginsProc>(GetProcAddress(
            library, "OfxGetNumberOfPlugins"));
        auto getPlugin = reinterpret_cast<GetPluginProc>(GetProcAddress(library, "OfxGetPlugin"));
        if (getNumberOfPlugins == nullptr || getPlugin == nullptr)
        {
            WriteLine("#ERROR\tOFXのエクスポート関数が見つかりません。");
            WriteLine("#END\t" + path);
            return;
        }

        // OfxSetHost はOFX 1.4以降の任意エクスポート。列挙より先に呼ぶ決まり。
        // kOfxStatFailed が返った場合は「このホスト向けではない」ため黙ってスキップする
        if (auto setHostBinary = reinterpret_cast<SetHostBinaryProc>(GetProcAddress(library, "OfxSetHost"));
            setHostBinary != nullptr)
        {
            if (setHostBinary(&host) == kOfxStatFailed)
            {
                WriteLine("#END\t" + path);
                return;
            }
        }

        auto count = getNumberOfPlugins();
        std::vector<OfxPlugin*> candidates;
        for (int i = 0; i < count; i++)
        {
            auto plugin = getPlugin(i);
            if (plugin == nullptr)
                continue;
            if (plugin->pluginApi == nullptr || std::strcmp(plugin->pluginApi, kOfxImageEffectPluginApi) != 0)
                continue;
            if (plugin->apiVersion != kOfxImageEffectPluginApiVersion)
                continue;
            if (plugin->pluginIdentifier == nullptr || plugin->pluginIdentifier[0] == '\0')
                continue;
            // 壊れたバイナリの部分初期化された構造体でNULL関数を呼ばないよう検査する
            if (plugin->setHost == nullptr || plugin->mainEntry == nullptr)
                continue;
            candidates.push_back(plugin);
        }

        // 同一IDの複数バージョン登録（後方互換用）は最新バージョンだけをdescribeする
        // （プロセス内スキャンのScanBinaryと同じ挙動。古いバージョンのdescribe結果が
        // 最新バージョンの代わりに一覧へ載る不一致を防ぐ）
        auto isNewer = [](const OfxPlugin* a, const OfxPlugin* b)
        {
            return a->pluginVersionMajor != b->pluginVersionMajor
                       ? a->pluginVersionMajor > b->pluginVersionMajor
                       : a->pluginVersionMinor > b->pluginVersionMinor;
        };
        for (auto plugin : candidates)
        {
            auto isLatest = true;
            for (auto other : candidates)
            {
                if (other != plugin && _stricmp(other->pluginIdentifier, plugin->pluginIdentifier) == 0 && isNewer(
                    other, plugin))
                {
                    isLatest = false;
                    break;
                }
            }
            if (!isLatest)
                continue;
            std::string identifier = plugin->pluginIdentifier;

            plugin->setHost(&host);
            auto loadStatus = plugin->mainEntry(kOfxActionLoad, nullptr, nullptr, nullptr);
            if (loadStatus != kOfxStatOK && loadStatus != kOfxStatReplyDefault)
            {
                WriteLine(
                    "#ERROR\tplugin=" + Sanitize(identifier) + " kOfxActionLoadが失敗しました。status=" + std::to_string(
                        loadStatus));
                continue;
            }

            auto descriptor = std::make_unique<EffectDescriptor>();
            FillEffectDefaultProperties(*descriptor, path);
            auto describeStatus = plugin->mainEntry(
                kOfxActionDescribe,
                reinterpret_cast<void*>(descriptor.get()),
                nullptr,
                nullptr);
            if (describeStatus != kOfxStatOK)
            {
                WriteLine(
                    "#ERROR\tplugin=" + Sanitize(identifier) + " kOfxActionDescribeが失敗しました。status=" + std::to_string(
                        describeStatus));
                continue;
            }

            const auto& props = descriptor->props;
            WriteLine(
                "PLUGIN\t" + Sanitize(identifier)
                + "\t" + std::to_string(plugin->pluginVersionMajor)
                + "\t" + std::to_string(plugin->pluginVersionMinor)
                + "\t" + Sanitize(props.GetStringOrDefault(kOfxPropLabel, ""))
                + "\t" + Sanitize(props.GetStringOrDefault(kOfxImageEffectPluginPropGrouping, ""))
                + "\t" + Sanitize(JoinWithPipe(props.GetStrings(kOfxImageEffectPropSupportedContexts)))
                + "\t" + Sanitize(JoinWithPipe(props.GetStrings(kOfxImageEffectPropSupportedPixelDepths)))
                + "\t" + std::to_string(props.GetIntOrDefault(kOfxImageEffectPluginPropSingleInstance, 0) != 0 ? 1 : 0)
                + "\t" + std::to_string(props.GetIntOrDefault(kOfxImageEffectPropTemporalClipAccess, 0) != 0 ? 1 : 0)
                + "\t" + Sanitize(props.GetStringOrDefault(kOfxImageEffectPropOpenGLRenderSupported, "false"))
                + "\t" + Sanitize(props.GetStringOrDefault(kOfxImageEffectPropCudaRenderSupported, "false"))
                + "\t" + Sanitize(props.GetStringOrDefault(kOfxImageEffectPropCudaStreamSupported, "false"))
                + "\t" + Sanitize(props.GetStringOrDefault(kOfxImageEffectPropOpenCLRenderSupported, "false"))
                + "\t" + Sanitize(props.GetStringOrDefault(kOfxImageEffectPropOpenCLSupported, "false"))
                + "\t" + Sanitize(props.GetStringOrDefault(kOfxImageEffectPropMetalRenderSupported, "false"))
                + "\t" + Sanitize(props.GetStringOrDefault(kOfxImageEffectPropCPURenderSupported, "true")));
            // descriptor はプラグインが kOfxActionUnload まで参照しうるため、プロセス終了まで保持する
            descriptor.release();
        }

        WriteLine("#END\t" + path);
    }
}

int wmain(int argc, wchar_t* argv[])
{
    // 壊れたプラグインのクラッシュ時にWER・CRTのダイアログで止まらず即終了させる
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
    _set_abort_behavior(0, _WRITE_ABORT_MSG | _CALL_REPORTFAULT);

    // 第1引数: ホストのバージョン（"major.minor.build.revision"。省略時は0.0.0.0）
    // 第2引数: CUDAバックエンドの実可用性（"true" / "false"）
    // 第3引数: OpenCLバックエンドの実可用性（"true" / "false"）
    int version[4] = {0, 0, 0, 0};
    if (argc >= 2)
        swscanf_s(argv[1], L"%d.%d.%d.%d", &version[0], &version[1], &version[2], &version[3]);

    const bool cudaAvailable = argc >= 3 && wcscmp(argv[2], L"true") == 0;
    const bool openCLAvailable = argc >= 4 && wcscmp(argv[3], L"true") == 0;
    FillHostProperties(hostProps, version, cudaAvailable, openCLAvailable);
    host.host = reinterpret_cast<OfxPropertySetHandle>(&hostProps);

    std::string line;
    while (ReadLine(line))
    {
        while (!line.empty() && (line.back() == '\r' || line.back() == '\n'))
            line.pop_back();
        if (line.empty())
            continue;
        ScanBinary(line);
    }
    return 0;
}

//-----------------------------------------------------------------------------
// YukkuriMovieMaker.Vst3Scanner
// VST3モジュールのクラス列挙を行うコンソールツール。
// 壊れた・ハングするプラグインの読み込みからYMM4本体を隔離するため、
// スキャン時のモジュールロードは本体ではなくこのプロセス内で行う
// （クラッシュしてもYMM4は巻き込まれず、親側がスキップして継続する）。
//
// プロトコル（UTF-8・タブ区切り・1行1メッセージ）:
//   stdin : スキャン対象モジュールパスを1行1件で受け取り、EOFで終了する
//   stdout: #BEGIN <path> → CLASS <classId> <name> <vendor> <category> <subCategories> → #END <path>
//           モジュールを開けない場合は #ERROR <message> を出力して #END で次へ進む
//           プラグインが標準出力へ書き込む可能性があるため、親は不明な行を無視する
//-----------------------------------------------------------------------------

#include <windows.h>

#include <algorithm>
#include <cstdio>
#include <cstdlib>
#include <string>
#include <vector>

#include "../YukkuriMovieMaker.Vst3Bridge/Ymm4Vst3BridgeApi.h"

namespace
{
    constexpr DWORD MaxModulePathLength = 32768;
    constexpr size_t ErrorBufferSize = 4096;
    constexpr wchar_t BridgeDllName[] = L"YukkuriMovieMaker.Vst3Bridge.dll";

    std::string WideToUtf8(const std::wstring& value)
    {
        if (value.empty())
            return {};

        auto requiredSize = WideCharToMultiByte(
            CP_UTF8,
            WC_ERR_INVALID_CHARS,
            value.data(),
            static_cast<int>(value.size()),
            nullptr,
            0,
            nullptr,
            nullptr);
        if (requiredSize <= 0)
            return "<UTF-8変換失敗>";

        std::string result(static_cast<size_t>(requiredSize), '\0');
        if (WideCharToMultiByte(
            CP_UTF8,
            WC_ERR_INVALID_CHARS,
            value.data(),
            static_cast<int>(value.size()),
            result.data(),
            requiredSize,
            nullptr,
            nullptr) <= 0)
        {
            return "<UTF-8変換失敗>";
        }
        return result;
    }

    bool TryGetExecutablePath(std::wstring& path, std::string& error)
    {
        DWORD capacity = MAX_PATH;
        while (true)
        {
            std::vector<wchar_t> buffer(capacity);
            auto length = GetModuleFileNameW(nullptr, buffer.data(), capacity);
            if (length == 0)
            {
                error = "Scannerの実行ファイルパスを取得できませんでした。win32Error="
                    + std::to_string(GetLastError());
                return false;
            }
            if (length < capacity)
            {
                path.assign(buffer.data(), length);
                return true;
            }
            if (capacity == MaxModulePathLength)
            {
                error = "Scannerの実行ファイルパスが長すぎます。";
                return false;
            }
            capacity = std::min(capacity * 2, MaxModulePathLength);
        }
    }

    bool TryGetBridgePath(std::wstring& path, std::string& error)
    {
        if (!TryGetExecutablePath(path, error))
            return false;

        auto separator = path.find_last_of(L"\\/");
        if (separator == std::wstring::npos)
        {
            error = "Scannerの実行ファイルパスからフォルダーを特定できませんでした。path="
                + WideToUtf8(path);
            return false;
        }
        path.replace(separator + 1, std::wstring::npos, BridgeDllName);
        return true;
    }

    class BridgeApi
    {
    public:
        BridgeApi() = default;
        BridgeApi(const BridgeApi&) = delete;
        BridgeApi& operator=(const BridgeApi&) = delete;

        ~BridgeApi()
        {
            if (library)
                FreeLibrary(library);
        }

        bool Load(std::string& error)
        {
            std::wstring path;
            if (!TryGetBridgePath(path, error))
                return false;

            library = LoadLibraryW(path.c_str());
            if (!library)
            {
                auto errorCode = GetLastError();
                error = "Bridge DLLを読み込めませんでした。path=" + WideToUtf8(path)
                    + " win32Error=" + std::to_string(errorCode);
                return false;
            }

            if (!Resolve("Ymm4Vst3GetApiVersion", getApiVersion, error))
                return false;
            auto apiVersion = getApiVersion();
            if (apiVersion != Ymm4Vst3ApiVersion)
            {
                error = "Bridge DLLのAPIバージョンが一致しません。required="
                    + std::to_string(Ymm4Vst3ApiVersion)
                    + " actual=" + std::to_string(apiVersion)
                    + " path=" + WideToUtf8(path);
                return false;
            }

            return Resolve("Ymm4Vst3ModuleOpen", moduleOpen, error)
                && Resolve("Ymm4Vst3ModuleClose", moduleClose, error)
                && Resolve("Ymm4Vst3ModuleGetClassCount", moduleGetClassCount, error)
                && Resolve("Ymm4Vst3ModuleGetClassInfo", moduleGetClassInfo, error);
        }

        Ymm4Vst3ModuleOpenProc moduleOpen = nullptr;
        Ymm4Vst3ModuleCloseProc moduleClose = nullptr;
        Ymm4Vst3ModuleGetClassCountProc moduleGetClassCount = nullptr;
        Ymm4Vst3ModuleGetClassInfoProc moduleGetClassInfo = nullptr;

    private:
        template<typename T>
        bool Resolve(const char* name, T& function, std::string& error)
        {
            auto address = GetProcAddress(library, name);
            if (!address)
            {
                auto errorCode = GetLastError();
                error = "Bridge DLLのエクスポートを取得できませんでした。name="
                    + std::string(name) + " win32Error=" + std::to_string(errorCode);
                return false;
            }
            function = reinterpret_cast<T>(address);
            return true;
        }

        HMODULE library = nullptr;
        Ymm4Vst3GetApiVersionProc getApiVersion = nullptr;
    };

    class ModuleHandle
    {
    public:
        ModuleHandle(void* handle, Ymm4Vst3ModuleCloseProc close)
            : handle(handle), close(close)
        {
        }

        ModuleHandle(const ModuleHandle&) = delete;
        ModuleHandle& operator=(const ModuleHandle&) = delete;

        ~ModuleHandle()
        {
            if (handle)
                close(handle);
        }

        void* Get() const { return handle; }
        explicit operator bool() const { return handle != nullptr; }

    private:
        void* handle;
        Ymm4Vst3ModuleCloseProc close;
    };

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

    template<size_t N>
    std::string FixedUtf8ToString(const char (&value)[N])
    {
        size_t length = 0;
        while (length < N && value[length] != '\0')
            length++;
        return std::string(value, length);
    }

    void WriteLine(const std::string& line)
    {
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

    void ScanModule(const BridgeApi& api, const std::string& path)
    {
        WriteLine("#BEGIN\t" + path);
        {
            char errorBuffer[ErrorBufferSize]{};
            ModuleHandle module(api.moduleOpen(path.c_str(), errorBuffer, sizeof(errorBuffer)), api.moduleClose);
            if (!module)
            {
                WriteLine("#ERROR\t" + Sanitize(FixedUtf8ToString(errorBuffer)));
            }
            else
            {
                auto count = api.moduleGetClassCount(module.Get());
                if (count < 0)
                {
                    WriteLine("#ERROR\tクラス数が不正です。count=" + std::to_string(count));
                }
                else
                {
                    for (std::int32_t i = 0; i < count; i++)
                    {
                        Ymm4Vst3ClassInfo classInfo{};
                        if (api.moduleGetClassInfo(module.Get(), i, &classInfo) == 0)
                        {
                            WriteLine(
                                "#ERROR\tクラス情報を取得できませんでした。index=" + std::to_string(i)
                                + " count=" + std::to_string(count));
                            break;
                        }
                        WriteLine(
                            "CLASS\t" + FixedUtf8ToString(classInfo.classId)
                            + "\t" + Sanitize(FixedUtf8ToString(classInfo.name))
                            + "\t" + Sanitize(FixedUtf8ToString(classInfo.vendor))
                            + "\t" + Sanitize(FixedUtf8ToString(classInfo.category))
                            + "\t" + Sanitize(FixedUtf8ToString(classInfo.subCategories)));
                    }
                }
            }
        } // スコープを抜けてモジュールをアンロードしてから完了を報告する
        WriteLine("#END\t" + path);
    }
}

int main()
{
    // 壊れたプラグインのクラッシュ時にWER・CRTのダイアログで止まらず即終了させる
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
    _set_abort_behavior(0, _WRITE_ABORT_MSG | _CALL_REPORTFAULT);

    BridgeApi api;
    std::string error;
    if (!api.Load(error))
    {
        std::fwrite(error.data(), 1, error.size(), stderr);
        std::fputc('\n', stderr);
        std::fflush(stderr);
        return 2;
    }

    std::string line;
    while (ReadLine(line))
    {
        while (!line.empty() && (line.back() == '\r' || line.back() == '\n'))
            line.pop_back();
        if (line.empty())
            continue;
        ScanModule(api, line);
    }
    return 0;
}

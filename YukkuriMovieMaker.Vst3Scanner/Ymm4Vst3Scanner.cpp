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

#include <cstdio>
#include <cstdlib>
#include <iostream>
#include <string>

#include "public.sdk/source/vst/hosting/hostclasses.h"
#include "public.sdk/source/vst/hosting/module.h"
#include "public.sdk/source/vst/hosting/plugprovider.h"

using namespace Steinberg;
using namespace Steinberg::Vst;

namespace
{
    HostApplication gHostApplication;

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
        std::fwrite(line.data(), 1, line.size(), stdout);
        std::fputc('\n', stdout);
        std::fflush(stdout);
    }

    void ScanModule(const std::string& path)
    {
        WriteLine("#BEGIN\t" + path);
        {
            std::string error;
            auto module = VST3::Hosting::Module::create(path, error);
            if (!module)
            {
                WriteLine("#ERROR\t" + Sanitize(error));
            }
            else
            {
                for (const auto& classInfo : module->getFactory().classInfos())
                {
                    WriteLine(
                        "CLASS\t" + classInfo.ID().toString()
                        + "\t" + Sanitize(classInfo.name())
                        + "\t" + Sanitize(classInfo.vendor())
                        + "\t" + Sanitize(classInfo.category())
                        + "\t" + Sanitize(classInfo.subCategoriesString()));
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

    PluginContextFactory::instance().setPluginContext(&gHostApplication);

    std::string line;
    while (std::getline(std::cin, line))
    {
        while (!line.empty() && (line.back() == '\r' || line.back() == '\n'))
            line.pop_back();
        if (line.empty())
            continue;
        ScanModule(line);
    }
    return 0;
}

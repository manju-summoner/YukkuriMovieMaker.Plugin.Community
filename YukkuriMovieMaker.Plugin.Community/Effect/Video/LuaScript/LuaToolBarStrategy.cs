using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls.AvalonEdit.ToolBarStrategy;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal sealed class LuaToolBarStrategy : IToolBarStrategy
    {
        public IEnumerable<ToolBarGroup> GetToolBarGroups()
        {
            yield return new ToolBarGroup(
            [
                new ToolBarItem(
                    Texts.ToolBarImport,
                    Application.Current.FindResource("MDI_Import"),
                    new ActionCommand(
                        x => x is not null,
                        x =>
                        {
                            dynamic param = x!;
                            var dlg = new OpenFileDialog
                            {
                                Filter = Texts.LuaFileFilter,
                                DefaultExt = ".lua"
                            };
                            if (dlg.ShowDialog() != true) return;
                            param.Text = File.ReadAllText(dlg.FileName, Encoding.UTF8);
                        })),

                new ToolBarItem(
                    Texts.ToolBarExport,
                    Application.Current.FindResource("MDI_Export"),
                    new ActionCommand(
                        x => x is not null,
                        x =>
                        {
                            dynamic param = x!;
                            var dlg = new SaveFileDialog
                            {
                                Filter = Texts.LuaFileFilter,
                                DefaultExt = ".lua",
                                FileName = "script.lua"
                            };
                            if (dlg.ShowDialog() != true) return;
                            using var stream = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write);
                            using var writer = new StreamWriter(stream, Encoding.UTF8);
                            writer.Write((string?)param.Text);
                        })),

                new ToolBarItem(
                    Texts.ToolBarClear,
                    Application.Current.FindResource("MDI_Delete"),
                    new ActionCommand(
                        x => x is not null,
                        x =>
                        {
                            dynamic param = x!;
                            if (MessageBox.Show(
                                    Texts.ToolBarClearConfirm,
                                    Texts.ToolBarClearTitle,
                                    MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
                            param.Text = LuaScriptEffect.DefaultScript;
                        })),
            ]);
        }
    }
}

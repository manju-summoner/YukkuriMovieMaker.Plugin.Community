using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    public partial class LuaScriptToolBar : UserControl, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ItemProperty[]? ItemProperties { get; set; }

        public LuaScriptToolBar()
        {
            InitializeComponent();
        }

        private IScriptProvider? GetScriptProvider() =>
            ItemProperties is [var first, ..] ? first.PropertyOwner as IScriptProvider : null;

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var provider = GetScriptProvider();
            if (provider is null) return;

            var dlg = new OpenFileDialog
            {
                Filter = Texts.LuaFileFilter,
                DefaultExt = ".lua"
            };
            if (dlg.ShowDialog() == true)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                provider.Script = File.ReadAllText(dlg.FileName, Encoding.UTF8);
                EndEdit?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var provider = GetScriptProvider();
            if (provider is null) return;

            var dlg = new SaveFileDialog
            {
                Filter = Texts.LuaFileFilter,
                DefaultExt = ".lua",
                FileName = "script.lua"
            };
            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, provider.Script, new UTF8Encoding(false));
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            var provider = GetScriptProvider();
            if (provider is null) return;

            if (MessageBox.Show(Texts.ToolBarClearConfirm, Texts.ToolBarClearTitle, MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                provider.Script = provider.DefaultScript;
                EndEdit?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}

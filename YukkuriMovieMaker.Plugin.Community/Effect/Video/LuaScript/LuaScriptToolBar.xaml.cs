using System;
using System.Collections.Generic;
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

        private IEnumerable<IScriptProvider> GetScriptProviders()
        {
            if (ItemProperties is null) yield break;
            foreach (var item in ItemProperties)
            {
                if (item.PropertyOwner is IScriptProvider provider)
                    yield return provider;
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var providers = new List<IScriptProvider>(GetScriptProviders());
            if (providers.Count == 0) return;

            var dlg = new OpenFileDialog
            {
                Filter = Texts.LuaFileFilter,
                DefaultExt = ".lua"
            };
            if (dlg.ShowDialog() != true) return;

            string script;
            try
            {
                script = File.ReadAllText(dlg.FileName, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(ex.Message, Texts.ToolBarImportErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            BeginEdit?.Invoke(this, EventArgs.Empty);
            foreach (var provider in providers)
                provider.Script = script;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var providers = new List<IScriptProvider>(GetScriptProviders());
            if (providers.Count == 0) return;

            var dlg = new SaveFileDialog
            {
                Filter = Texts.LuaFileFilter,
                DefaultExt = ".lua",
                FileName = "script.lua"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(dlg.FileName, providers[0].Script, new UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(ex.Message, Texts.ToolBarExportErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            var providers = new List<IScriptProvider>(GetScriptProviders());
            if (providers.Count == 0) return;

            if (MessageBox.Show(Texts.ToolBarClearConfirm, Texts.ToolBarClearTitle, MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                return;

            BeginEdit?.Invoke(this, EventArgs.Empty);
            foreach (var provider in providers)
                provider.Script = provider.DefaultScript;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }
}

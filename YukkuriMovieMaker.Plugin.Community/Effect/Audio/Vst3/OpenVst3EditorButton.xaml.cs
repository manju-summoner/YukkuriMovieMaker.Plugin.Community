using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    public partial class OpenVst3EditorButton : UserControl, IPropertyEditorControl
    {
        static readonly Dictionary<Vst3Effect, Vst3EditorWindow> openEditors = [];

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ItemProperty[]? ItemProperties { get; set; }

        public OpenVst3EditorButton()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (ItemProperties is null)
                throw new InvalidOperationException("ItemProperties is not set.");
            if (ItemProperties[0].PropertyOwner is not Vst3Effect effect)
                return;
            if (string.IsNullOrWhiteSpace(effect.FilePath))
            {
                MessageBox.Show(Window.GetWindow(this), Texts.PluginNotSelectedMessage, Texts.Vst3Effect);
                return;
            }
            if (openEditors.TryGetValue(effect, out var existing))
            {
                existing.Activate();
                return;
            }
            Vst3InstanceLease lease;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                lease = Vst3InstancePool.AcquireEditor(effect);
            }
            catch (Exception)
            {
                MessageBox.Show(Window.GetWindow(this), Texts.PluginLoadFailedMessage, Texts.Vst3Effect);
                return;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            var instance = lease.Instance;
            if (!instance.TryCreateView())
            {
                lease.Dispose();
                Vst3EditorProbe.SetHasEditor(effect.FilePath, false);
                effect.UpdateHasEditor();
                MessageBox.Show(Window.GetWindow(this), Texts.EditorNotAvailableMessage, Texts.Vst3Effect);
                return;
            }
            Vst3EditorProbe.SetHasEditor(effect.FilePath, true);

            var properties = ItemProperties;
            var window = new Vst3EditorWindow(instance, Path.GetFileNameWithoutExtension(effect.FilePath))
            {
                Owner = Window.GetWindow(this),
            };
            openEditors.Add(effect, window);
            BeginEdit?.Invoke(this, EventArgs.Empty);

            var filePath = effect.FilePath;
            void OnEditCompleted()
            {
                if (lease.IsProcessingActive)
                    return;
                ApplyStates(lease, properties, filePath);
            }
            instance.EditCompleted += OnEditCompleted;

            window.Closed += (_, _) =>
            {
                openEditors.Remove(effect);
                instance.EditCompleted -= OnEditCompleted;
                instance.ReleaseView();
                ApplyStates(lease, properties, filePath);
                EndEdit?.Invoke(this, EventArgs.Empty);
                lease.Dispose();
            };
            try
            {
                window.ShowEditor();
            }
            catch (Exception)
            {
                window.Close();
                MessageBox.Show(Window.GetWindow(this), Texts.PluginLoadFailedMessage, Texts.Vst3Effect);
            }
        }

        static void ApplyStates(Vst3InstanceLease lease, ItemProperty[] properties, string filePath)
        {
            var (componentState, controllerState) = lease.CaptureStates();
            foreach (var property in properties)
            {
                if (property.PropertyOwner is not Vst3Effect target
                    || !string.Equals(target.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                    continue;
                property.SetValue(componentState);
                target.ControllerState = controllerState;
            }
        }
    }
}

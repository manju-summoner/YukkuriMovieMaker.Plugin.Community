using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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
            Vst3EditorSession session;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                session = new Vst3EditorSession(
                    effect.FilePath,
                    DecodeState(effect.PluginState),
                    DecodeState(effect.ControllerState));
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

            if (!session.TryCreateView())
            {
                session.Dispose();
                MessageBox.Show(Window.GetWindow(this), Texts.EditorNotAvailableMessage, Texts.Vst3Effect);
                return;
            }

            var properties = ItemProperties;
            var window = new Vst3EditorWindow(session, Path.GetFileNameWithoutExtension(effect.FilePath))
            {
                Owner = Window.GetWindow(this),
            };
            openEditors.Add(effect, window);
            BeginEdit?.Invoke(this, EventArgs.Empty);

            session.ParameterPerformed += (parameterId, normalizedValue) =>
            {
                foreach (var property in properties)
                {
                    if (property.PropertyOwner is Vst3Effect target)
                        target.NotifyParameterEdited(parameterId, normalizedValue);
                }
            };
            session.EditCompleted += () => ApplyStates(session, properties);

            window.Closed += (_, _) =>
            {
                openEditors.Remove(effect);
                ApplyStates(session, properties);
                EndEdit?.Invoke(this, EventArgs.Empty);
                session.Dispose();
            };
            window.Show();
        }

        static void ApplyStates(Vst3EditorSession session, ItemProperty[] properties)
        {
            var (componentState, controllerState) = session.CaptureStates();
            foreach (var property in properties)
            {
                if (property.PropertyOwner is not Vst3Effect target)
                    continue;
                property.SetValue(EncodeState(componentState));
                target.ControllerState = EncodeState(controllerState);
            }
        }

        static byte[]? DecodeState(string state)
        {
            if (string.IsNullOrEmpty(state))
                return null;
            try
            {
                return Convert.FromBase64String(state);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        static string EncodeState(byte[]? state) =>
            state is null ? string.Empty : Convert.ToBase64String(state);
    }
}

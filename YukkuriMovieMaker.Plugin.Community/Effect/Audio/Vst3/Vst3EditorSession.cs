using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    /// <summary>
    /// モードレスなVST3エディターのセッション（エフェクト1つにつき最大1つ）。
    /// エディター用プラグインインスタンスとウィンドウの寿命、Undo連携を管理する。
    /// - 確定: プラグインGUIの編集が一定時間止まるたびに状態をアイテムへ保存し、
    ///   接続中のボタンからBeginEdit/EndEditを発火してUndo1ユニットとして確定する
    /// - Undo/Redo: エフェクト側の状態が外部から変わったら、エディターのプラグインへ巻き戻し反映する
    /// - 寿命: プロパティエディタから切断されたら保存して閉じる。ただしPropertiesEditorは
    ///   同一アイテムを表示したままでもコントロールを再構築（ClearBindings→SetBindings）するため、
    ///   遅延クローズにして再接続でキャンセルする
    /// </summary>
    internal sealed class Vst3EditorSession
    {
        static readonly Dictionary<Vst3AudioEffect, Vst3EditorSession> sessions = [];

        /// <summary>
        /// プラグインGUIの編集がこの時間止まったら1操作として確定する。
        /// ブリッジはendEdit（操作の区切り）を公開していないため、無通知のプラグインにも効く時間方式を使う
        /// </summary>
        static readonly TimeSpan ConfirmDelay = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// エフェクトのエディターが開いていれば手前に出す。
        /// プラグインが差し替えられていた場合は古いエディターを閉じてfalseを返す
        /// </summary>
        public static bool TryActivate(Vst3AudioEffect effect)
        {
            if (!sessions.TryGetValue(effect, out var session))
                return false;
            if (!session.IsSamePlugin(effect))
            {
                // ファイル変更後の古いエディターは再利用しない
                session.window.Close();
                return false;
            }
            session.window.Activate();
            return true;
        }

        /// <summary>
        /// ボタンがプロパティエディタへ接続された。対象エフェクトのセッションのクローズ予約を取り消す
        /// </summary>
        public static void OnControlAttached(Vst3OpenEditorButton control)
        {
            foreach (var effect in control.GetTargetEffects())
            {
                if (sessions.TryGetValue(effect, out var session))
                    session.Attach(control);
            }
        }

        /// <summary>
        /// ボタンがプロパティエディタから切断された。接続中のセッションへクローズを予約する
        /// </summary>
        public static void OnControlDetached(Vst3OpenEditorButton control)
        {
            foreach (var session in sessions.Values.Where(x => x.attachedControl == control).ToList())
                session.ScheduleClose();
        }

        readonly Vst3AudioEffect effect;
        readonly string pluginPath;
        readonly string classId;
        readonly Vst3Plugin plugin;
        readonly Vst3EditorWindow window;
        readonly byte[]? initialComponentState;
        readonly byte[]? initialControllerState;
        Vst3OpenEditorButton? attachedControl;
        bool isPendingClose;
        bool isSelfWriting;
        bool isStateSyncQueued;
        bool isCloseQueued;
        bool hasPendingEdits;
        DateTime lastEditTime;
        bool isClosed;

        /// <summary>
        /// セッションを開始する。pluginの所有権はセッションへ移り、ウィンドウが閉じたときに破棄される
        /// </summary>
        public Vst3EditorSession(Vst3AudioEffect effect, Vst3OpenEditorButton control, Vst3Plugin plugin, Vst3EditorWindow window)
        {
            this.effect = effect;
            pluginPath = effect.PluginPath;
            classId = effect.ClassId;
            this.plugin = plugin;
            this.window = window;
            attachedControl = control;
            // 未保存状態（ComponentState=null）へのUndoをエディターへ反映するため、開いた時点の状態を控える
            (initialComponentState, initialControllerState) = plugin.GetState();

            sessions[effect] = this;

            effect.PropertyChanged += OnEffectPropertyChanged;
            window.ParameterForwarded += OnParameterForwarded;
            window.Closed += OnWindowClosed;
        }

        bool IsSamePlugin(Vst3AudioEffect target) =>
            Vst3PluginSelector.IsSameId(target.PluginPath, pluginPath)
            && Vst3PluginSelector.IsSameId(target.ClassId, classId);

        void Attach(Vst3OpenEditorButton control)
        {
            attachedControl = control;
            isPendingClose = false;
        }

        void ScheduleClose()
        {
            attachedControl = null;
            if (isPendingClose || isClosed)
                return;
            isPendingClose = true;
            // PropertiesEditorの再構築（同一アイテムのままClearBindings→SetBindings）と本当の選択解除を
            // 区別するため、再構築が完了するBackground優先度まで待ってから閉じる
            window.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                if (isPendingClose && !isClosed)
                    window.Close();
            });
        }

        void OnParameterForwarded(int parameterCount)
        {
            if (parameterCount > 0)
            {
                hasPendingEdits = true;
                lastEditTime = DateTime.UtcNow;
                return;
            }
            if (hasPendingEdits && DateTime.UtcNow - lastEditTime >= ConfirmDelay)
            {
                hasPendingEdits = false;
                SaveStates();
            }
        }

        /// <summary>
        /// エディターのプラグインの現在の状態をアイテムへ保存し、Undo1ユニットとして確定する。
        /// 状態が変わっていない場合は何もしない（空のUndoユニットを作らない）
        /// </summary>
        void SaveStates()
        {
            if (plugin.IsDisposed)
                return;
            var (componentState, controllerState) = plugin.GetState();
            if (StateEquals(effect.ComponentState, componentState) && StateEquals(effect.ControllerState, controllerState))
                return;

            // 接続が外れている間（コントロール再構築の隙間等）でも保存は行う。
            // その場合Undoユニットの確定は次の操作に相乗りするが、データは失われない
            var targets = attachedControl?.GetTargetEffects().ToArray() is { Length: > 0 } bound ? bound : [effect];
            isSelfWriting = true;
            try
            {
                attachedControl?.RaiseBeginEdit();
                Vst3OpenEditorButton.ApplyStateToMatchingEffects(targets, pluginPath, classId, componentState, controllerState);
                attachedControl?.RaiseEndEdit();
            }
            finally
            {
                isSelfWriting = false;
            }
        }

        static bool StateEquals(byte[]? a, byte[]? b) =>
            a is null ? b is null : b is not null && a.AsSpan().SequenceEqual(b);

        void OnEffectPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (isSelfWriting || isClosed)
                return;
            switch (e.PropertyName)
            {
                case nameof(Vst3AudioEffect.PluginPath):
                case nameof(Vst3AudioEffect.ClassId):
                    // 別のプラグインへ差し替えられたらこのエディターは無効。
                    // プロパティ変更の連鎖中に閉じないようDispatcher経由にする
                    if (!IsSamePlugin(effect) && !isCloseQueued)
                    {
                        isCloseQueued = true;
                        window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
                        {
                            if (!isClosed)
                                window.Close();
                        });
                    }
                    break;
                case nameof(Vst3AudioEffect.ComponentState):
                case nameof(Vst3AudioEffect.ControllerState):
                    // Undo/Redo等の外部からの状態変更をエディターのプラグインへ反映する。
                    // 2つのプロパティが続けて変わるため、Dispatcherでまとめて1回だけ反映する
                    if (isStateSyncQueued)
                        return;
                    isStateSyncQueued = true;
                    window.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                    {
                        isStateSyncQueued = false;
                        if (isClosed || plugin.IsDisposed || !IsSamePlugin(effect))
                            return;
                        // 反映で生じる変化を編集として保存し直さない（Undo直後に同じ状態を書き戻すのを防ぐ）
                        hasPendingEdits = false;
                        // 未保存状態（null）へのUndoは、エディターを開いた時点の状態へ戻す
                        plugin.SetState(
                            effect.ComponentState ?? initialComponentState,
                            effect.ControllerState ?? initialControllerState);
                    });
                    break;
            }
        }

        void OnWindowClosed(object? sender, EventArgs e)
        {
            if (isClosed)
                return;
            isClosed = true;
            // ウィンドウのOnClosingで最終Pumpが済んでいる。最後の編集を保存・確定してから破棄する
            SaveStates();
            effect.PropertyChanged -= OnEffectPropertyChanged;
            window.ParameterForwarded -= OnParameterForwarded;
            window.Closed -= OnWindowClosed;
            plugin.Dispose();
            sessions.Remove(effect);
        }
    }
}

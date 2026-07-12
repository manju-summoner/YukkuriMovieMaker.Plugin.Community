using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal sealed class Vst3Message : Vst3SharedObject
    {
        readonly Vst3AttributeList attributes = new();
        IntPtr messageId;

        public Vst3Message() : base(Vst3Native.IMessageUid)
        {
            BuildVtable(
                new GetMessageIdDelegate(GetMessageId),
                new SetMessageIdDelegate(SetMessageId),
                new GetAttributesDelegate(GetAttributes));
        }

        protected override void OnFinalRelease()
        {
            if (messageId != IntPtr.Zero)
                Marshal.FreeHGlobal(messageId);
            messageId = IntPtr.Zero;
            attributes.ReleaseReference();
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate IntPtr GetMessageIdDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate void SetMessageIdDelegate(IntPtr self, IntPtr id);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate IntPtr GetAttributesDelegate(IntPtr self);

        IntPtr GetMessageId(IntPtr self) => messageId;

        void SetMessageId(IntPtr self, IntPtr id)
        {
            var copy = id == IntPtr.Zero
                ? IntPtr.Zero
                : Marshal.StringToHGlobalAnsi(Marshal.PtrToStringAnsi(id));
            if (messageId != IntPtr.Zero)
                Marshal.FreeHGlobal(messageId);
            messageId = copy;
        }

        IntPtr GetAttributes(IntPtr self) => attributes.Handle;
    }
}

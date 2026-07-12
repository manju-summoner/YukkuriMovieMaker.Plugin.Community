using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal static class Vst3Native
    {
        public const int ResultOk = 0;
        public const int ResultFalse = 1;
        public const int MediaTypeAudio = 0;
        public const int BusDirectionInput = 0;
        public const int BusDirectionOutput = 1;
        public const int SymbolicSampleSize32 = 0;
        public const int ProcessModeRealtime = 0;
        public const uint ProcessContextPlaying = 1u << 1;
        public const uint ProcessContextProjectTimeMusicValid = 1u << 9;
        public const uint ProcessContextTempoValid = 1u << 10;
        public const uint ProcessContextBarPositionValid = 1u << 11;
        public const uint ProcessContextTimeSigValid = 1u << 13;
        public const uint ProcessContextContTimeValid = 1u << 17;
        public const ulong SpeakerArrangementStereo = 0x3;
        public const string AudioModuleClassCategory = "Audio Module Class";

        public const string EditorViewType = "editor";
        public const string PlatformTypeHwnd = "HWND";

        public static readonly byte[] FUnknownUid = Uid(0x00000000, 0x00000000, 0xC0000000, 0x00000046);
        public static readonly byte[] IPluginFactoryUid = Uid(0x7A4D811C, 0x52114A1F, 0xAED9D2EE, 0x0B43BF9F);
        public static readonly byte[] IComponentUid = Uid(0xE831FF31, 0xF2D54301, 0x928EBBEE, 0x25697802);
        public static readonly byte[] IAudioProcessorUid = Uid(0x42043F99, 0xB7DA453C, 0xA569E79D, 0x9AAEC33D);
        public static readonly byte[] IEditControllerUid = Uid(0xDCD7BBE3, 0x7742448D, 0xA874AACC, 0x979C759E);
        public static readonly byte[] IConnectionPointUid = Uid(0x70A4156F, 0x6E6E4026, 0x989148BF, 0xAA60D8D1);
        public static readonly byte[] IHostApplicationUid = Uid(0x58E595CC, 0xDB2D4969, 0x8B6AAF8C, 0x36A664E5);
        public static readonly byte[] IBStreamUid = Uid(0xC3BF6EA2, 0x30994752, 0x9B6BF990, 0x1EE33E9B);
        public static readonly byte[] IComponentHandlerUid = Uid(0x93A0BEA3, 0x0BD045DB, 0x8E890B0C, 0xC1E46AC6);
        public static readonly byte[] IPlugFrameUid = Uid(0x367FAF01, 0xAFA94693, 0x8D4DA2A0, 0xED0882A3);
        public static readonly byte[] IParameterChangesUid = Uid(0xA4779663, 0x0BB64A56, 0xB44384A8, 0x466FEB9D);
        public static readonly byte[] IParamValueQueueUid = Uid(0x01263A18, 0xED074F6F, 0x98C9D356, 0x4686F9BA);
        public static readonly byte[] IMessageUid = Uid(0x936F033B, 0xC6C047DB, 0xBB0882F8, 0x13C1E613);
        public static readonly byte[] IAttributeListUid = Uid(0x1E5F0AEB, 0xCC7F4533, 0xA2544011, 0x38AD5EE4);

        public static byte[] Uid(uint l1, uint l2, uint l3, uint l4) =>
        [
            (byte)l1, (byte)(l1 >> 8), (byte)(l1 >> 16), (byte)(l1 >> 24),
            (byte)(l2 >> 16), (byte)(l2 >> 24), (byte)l2, (byte)(l2 >> 8),
            (byte)(l3 >> 24), (byte)(l3 >> 16), (byte)(l3 >> 8), (byte)l3,
            (byte)(l4 >> 24), (byte)(l4 >> 16), (byte)(l4 >> 8), (byte)l4,
        ];

        public static T GetVtableMethod<T>(IntPtr unknown, int slot) where T : Delegate
        {
            var vtable = Marshal.ReadIntPtr(unknown);
            var function = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer<T>(function);
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate IntPtr GetPluginFactoryDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate byte ModuleEntryDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int QueryInterfaceDelegate(IntPtr self, byte[] iid, out IntPtr obj);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate uint ReleaseDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int CountClassesDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int GetClassInfoDelegate(IntPtr self, int index, IntPtr info);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int CreateInstanceDelegate(IntPtr self, byte[] cid, byte[] iid, out IntPtr obj);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int InitializeDelegate(IntPtr self, IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int TerminateDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int GetBusCountDelegate(IntPtr self, int mediaType, int direction);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int ActivateBusDelegate(IntPtr self, int mediaType, int direction, int index, byte state);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int SetActiveDelegate(IntPtr self, byte state);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int SetBusArrangementsDelegate(IntPtr self, ulong[] inputs, int numInputs, ulong[] outputs, int numOutputs);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int GetBusArrangementDelegate(IntPtr self, int direction, int index, ref ulong arrangement);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int CanProcessSampleSizeDelegate(IntPtr self, int symbolicSampleSize);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate uint GetLatencySamplesDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int SetupProcessingDelegate(IntPtr self, ref ProcessSetup setup);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int SetProcessingDelegate(IntPtr self, byte state);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int ProcessDelegate(IntPtr self, ref ProcessData data);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int GetControllerClassIdDelegate(IntPtr self, IntPtr classId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int StreamDelegate(IntPtr self, IntPtr stream);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int SetComponentHandlerDelegate(IntPtr self, IntPtr handler);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate IntPtr CreateViewDelegate(IntPtr self, [MarshalAs(UnmanagedType.LPStr)] string name);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int ConnectDelegate(IntPtr self, IntPtr other);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int NotifyDelegate(IntPtr self, IntPtr message);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int SetParamNormalizedDelegate(IntPtr self, uint id, double value);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int IsPlatformTypeSupportedDelegate(IntPtr self, [MarshalAs(UnmanagedType.LPStr)] string type);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int AttachedDelegate(IntPtr self, IntPtr parent, [MarshalAs(UnmanagedType.LPStr)] string type);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int RemovedDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int GetSizeDelegate(IntPtr self, ref ViewRect size);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int OnSizeDelegate(IntPtr self, ref ViewRect newSize);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int SetFrameDelegate(IntPtr self, IntPtr frame);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int CanResizeDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int CheckSizeConstraintDelegate(IntPtr self, ref ViewRect rect);

        [StructLayout(LayoutKind.Sequential)]
        public struct ViewRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ProcessSetup
        {
            public int ProcessMode;
            public int SymbolicSampleSize;
            public int MaxSamplesPerBlock;
            public double SampleRate;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct AudioBusBuffers
        {
            public int NumChannels;
            public ulong SilenceFlags;
            public IntPtr ChannelBuffers;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ProcessContext
        {
            public uint State;
            public double SampleRate;
            public long ProjectTimeSamples;
            public long SystemTime;
            public long ContinousTimeSamples;
            public double ProjectTimeMusic;
            public double BarPositionMusic;
            public double CycleStartMusic;
            public double CycleEndMusic;
            public double Tempo;
            public int TimeSigNumerator;
            public int TimeSigDenominator;
            public byte ChordKeyNote;
            public byte ChordRootNote;
            public short ChordMask;
            public int SmpteOffsetSubframes;
            public uint FrameRateFramesPerSecond;
            public uint FrameRateFlags;
            public int SamplesToNextClock;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ProcessData
        {
            public int ProcessMode;
            public int SymbolicSampleSize;
            public int NumSamples;
            public int NumInputs;
            public int NumOutputs;
            public IntPtr Inputs;
            public IntPtr Outputs;
            public IntPtr InputParameterChanges;
            public IntPtr OutputParameterChanges;
            public IntPtr InputEvents;
            public IntPtr OutputEvents;
            public IntPtr ProcessContext;
        }
    }
}

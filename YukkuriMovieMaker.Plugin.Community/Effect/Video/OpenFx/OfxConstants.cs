namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OFX C API のステータスコード（openfx/include/ofxCore.h と一致させること）
    /// </summary>
    internal static class OfxStatus
    {
        public const int OK = 0;
        public const int Failed = 1;
        public const int ErrFatal = 2;
        public const int ErrUnknown = 3;
        public const int ErrMissingHostFeature = 4;
        public const int ErrUnsupported = 5;
        public const int ErrExists = 6;
        public const int ErrFormat = 7;
        public const int ErrMemory = 8;
        public const int ErrBadHandle = 9;
        public const int ErrBadIndex = 10;
        public const int ErrValue = 11;
        public const int ReplyYes = 12;
        public const int ReplyNo = 13;
        public const int ReplyDefault = 14;
        public const int ErrImageFormat = 1000;
        public const int GPUOutOfMemory = 1001;
        public const int GPURenderFailed = 1002;
    }

    /// <summary>
    /// OFX C API の文字列定数（openfx/include/*.h の #define と一致させること。
    /// マクロ名と実文字列が一致しないものがあるため、変更時は必ずヘッダーと突き合わせる）
    /// </summary>
    internal static class OfxConstants
    {
        // プラグインAPI種別
        public const string ImageEffectPluginApi = "OfxImageEffectPluginAPI";
        public const int ImageEffectPluginApiVersion = 1;

        // アクション（ofxCore.h / ofxImageEffect.h）
        public const string ActionLoad = "OfxActionLoad";
        public const string ActionDescribe = "OfxActionDescribe";
        public const string ActionUnload = "OfxActionUnload";
        public const string ActionPurgeCaches = "OfxActionPurgeCaches";
        public const string ActionSyncPrivateData = "OfxActionSyncPrivateData";
        public const string ActionCreateInstance = "OfxActionCreateInstance";
        public const string ActionDestroyInstance = "OfxActionDestroyInstance";
        public const string ActionInstanceChanged = "OfxActionInstanceChanged";
        public const string ActionBeginInstanceChanged = "OfxActionBeginInstanceChanged";
        public const string ActionEndInstanceChanged = "OfxActionEndInstanceChanged";
        public const string ActionBeginInstanceEdit = "OfxActionBeginInstanceEdit";
        public const string ActionEndInstanceEdit = "OfxActionEndInstanceEdit";
        public const string ImageEffectActionGetRegionOfDefinition = "OfxImageEffectActionGetRegionOfDefinition";
        public const string ImageEffectActionGetRegionsOfInterest = "OfxImageEffectActionGetRegionsOfInterest";
        public const string ImageEffectActionGetTimeDomain = "OfxImageEffectActionGetTimeDomain";
        public const string ImageEffectActionGetFramesNeeded = "OfxImageEffectActionGetFramesNeeded";
        public const string ImageEffectActionGetClipPreferences = "OfxImageEffectActionGetClipPreferences";
        public const string ImageEffectActionIsIdentity = "OfxImageEffectActionIsIdentity";
        public const string ImageEffectActionRender = "OfxImageEffectActionRender";
        public const string ImageEffectActionBeginSequenceRender = "OfxImageEffectActionBeginSequenceRender";
        public const string ImageEffectActionEndSequenceRender = "OfxImageEffectActionEndSequenceRender";
        public const string ImageEffectActionDescribeInContext = "OfxImageEffectActionDescribeInContext";

        // スイート名
        public const string PropertySuite = "OfxPropertySuite";
        public const string ImageEffectSuite = "OfxImageEffectSuite";
        public const string ParameterSuite = "OfxParameterSuite";
        public const string MemorySuite = "OfxMemorySuite";
        public const string MultiThreadSuite = "OfxMultiThreadSuite";
        public const string MessageSuite = "OfxMessageSuite";
        public const string InteractSuite = "OfxInteractSuite";
        public const string ProgressSuite = "OfxProgressSuite";
        public const string TimeLineSuite = "OfxTimeLineSuite";

        // 汎用プロパティ（ofxCore.h）
        public const string PropAPIVersion = "OfxPropAPIVersion";
        public const string PropTime = "OfxPropTime";
        public const string PropIsInteractive = "OfxPropIsInteractive";
        public const string PluginPropFilePath = "OfxPluginPropFilePath";
        public const string PropInstanceData = "OfxPropInstanceData";
        public const string PropType = "OfxPropType";
        public const string PropName = "OfxPropName";
        public const string PropVersion = "OfxPropVersion";
        public const string PropVersionLabel = "OfxPropVersionLabel";
        public const string PropPluginDescription = "OfxPropPluginDescription";
        public const string PropLabel = "OfxPropLabel";
        public const string PropIcon = "OfxPropIcon";
        public const string PropShortLabel = "OfxPropShortLabel";
        public const string PropLongLabel = "OfxPropLongLabel";
        public const string PropChangeReason = "OfxPropChangeReason";
        public const string PropEffectInstance = "OfxPropEffectInstance";
        public const string PropHostOSHandle = "OfxPropHostOSHandle";
        public const string ChangeUserEdited = "OfxChangeUserEdited";
        public const string ChangePluginEdited = "OfxChangePluginEdited";
        public const string ChangeTime = "OfxChangeTime";

        // 型名（kOfxType*）
        public const string TypeImageEffectHost = "OfxTypeImageEffectHost";
        public const string TypeImageEffect = "OfxTypeImageEffect";
        public const string TypeImageEffectInstance = "OfxTypeImageEffectInstance";
        public const string TypeParameter = "OfxTypeParameter";
        public const string TypeParameterInstance = "OfxTypeParameterInstance";
        public const string TypeClip = "OfxTypeClip";
        public const string TypeImage = "OfxTypeImage";

        // ビット深度
        public const string BitDepthNone = "OfxBitDepthNone";
        public const string BitDepthByte = "OfxBitDepthByte";
        public const string BitDepthShort = "OfxBitDepthShort";
        public const string BitDepthHalf = "OfxBitDepthHalf";
        public const string BitDepthFloat = "OfxBitDepthFloat";

        // 画像コンポーネント
        public const string ImageComponentNone = "OfxImageComponentNone";
        public const string ImageComponentRGBA = "OfxImageComponentRGBA";
        public const string ImageComponentRGB = "OfxImageComponentRGB";
        public const string ImageComponentAlpha = "OfxImageComponentAlpha";

        // コンテキスト
        public const string ImageEffectContextGenerator = "OfxImageEffectContextGenerator";
        public const string ImageEffectContextFilter = "OfxImageEffectContextFilter";
        public const string ImageEffectContextTransition = "OfxImageEffectContextTransition";
        public const string ImageEffectContextPaint = "OfxImageEffectContextPaint";
        public const string ImageEffectContextGeneral = "OfxImageEffectContextGeneral";
        public const string ImageEffectContextRetimer = "OfxImageEffectContextRetimer";

        // 画像エフェクトプロパティ（ofxImageEffect.h）
        public const string ImageEffectPropSupportedContexts = "OfxImageEffectPropSupportedContexts";
        public const string ImageEffectPropPluginHandle = "OfxImageEffectPropPluginHandle";
        public const string ImageEffectHostPropIsBackground = "OfxImageEffectHostPropIsBackground";
        public const string ImageEffectPluginPropSingleInstance = "OfxImageEffectPluginPropSingleInstance";
        public const string ImageEffectPluginRenderThreadSafety = "OfxImageEffectPluginRenderThreadSafety";
        public const string ImageEffectRenderUnsafe = "OfxImageEffectRenderUnsafe";
        public const string ImageEffectRenderInstanceSafe = "OfxImageEffectRenderInstanceSafe";
        public const string ImageEffectRenderFullySafe = "OfxImageEffectRenderFullySafe";
        public const string ImageEffectPluginPropHostFrameThreading = "OfxImageEffectPluginPropHostFrameThreading";
        // 注意: マクロ名は kOfxImageEffectPropSupportsMultipleClipDepths だが実文字列に "Supports" が入らない
        public const string ImageEffectPropSupportsMultipleClipDepths = "OfxImageEffectPropMultipleClipDepths";
        public const string ImageEffectPropSupportsMultipleClipPARs = "OfxImageEffectPropSupportsMultipleClipPARs";
        public const string ImageEffectPropClipPreferencesSlaveParam = "OfxImageEffectPropClipPreferencesSlaveParam";
        public const string ImageEffectPropSetableFrameRate = "OfxImageEffectPropSetableFrameRate";
        public const string ImageEffectPropSetableFielding = "OfxImageEffectPropSetableFielding";
        public const string ImageEffectInstancePropSequentialRender = "OfxImageEffectInstancePropSequentialRender";
        public const string ImageEffectPropSequentialRenderStatus = "OfxImageEffectPropSequentialRenderStatus";
        public const string ImageEffectHostPropNativeOrigin = "OfxImageEffectHostPropNativeOrigin";
        // 注意: 値の実文字列は "k" 始まり（ヘッダーの歴史的経緯）
        public const string HostNativeOriginBottomLeft = "kOfxImageEffectHostPropNativeOriginBottomLeft";
        public const string HostNativeOriginTopLeft = "kOfxImageEffectHostPropNativeOriginTopLeft";
        public const string HostNativeOriginCenter = "kOfxImageEffectHostPropNativeOriginCenter";
        public const string ImageEffectPropInteractiveRenderStatus = "OfxImageEffectPropInteractiveRenderStatus";
        public const string ImageEffectPluginPropGrouping = "OfxImageEffectPluginPropGrouping";
        public const string ImageEffectPropSupportsOverlays = "OfxImageEffectPropSupportsOverlays";
        public const string ImageEffectPluginPropOverlayInteractV1 = "OfxImageEffectPluginPropOverlayInteractV1";
        public const string ImageEffectPropSupportsMultiResolution = "OfxImageEffectPropSupportsMultiResolution";
        public const string ImageEffectPropSupportsTiles = "OfxImageEffectPropSupportsTiles";
        public const string ImageEffectPropTemporalClipAccess = "OfxImageEffectPropTemporalClipAccess";
        public const string ImageEffectPropContext = "OfxImageEffectPropContext";
        public const string ImageEffectPropPixelDepth = "OfxImageEffectPropPixelDepth";
        public const string ImageEffectPropComponents = "OfxImageEffectPropComponents";
        public const string ImagePropUniqueIdentifier = "OfxImagePropUniqueIdentifier";
        public const string ImageClipPropContinuousSamples = "OfxImageClipPropContinuousSamples";
        // GetClipPreferences の outArgs でクリップ毎に使うプロパティ名のプレフィックス
        // （プレフィックス＋クリップ名。ofxImageEffect.h のアクション説明で規定される命名で、#define は存在しない）
        public const string ImageClipPropComponentsPrefix = "OfxImageClipPropComponents_";
        public const string ImageClipPropDepthPrefix = "OfxImageClipPropDepth_";
        public const string ImageClipPropPARPrefix = "OfxImageClipPropPAR_";
        public const string ImageClipPropUnmappedPixelDepth = "OfxImageClipPropUnmappedPixelDepth";
        public const string ImageClipPropUnmappedComponents = "OfxImageClipPropUnmappedComponents";
        public const string ImageEffectPropPreMultiplication = "OfxImageEffectPropPreMultiplication";
        public const string ImageOpaque = "OfxImageOpaque";
        public const string ImagePreMultiplied = "OfxImageAlphaPremultiplied";
        public const string ImageUnPreMultiplied = "OfxImageAlphaUnPremultiplied";
        public const string ImageEffectPropSupportedPixelDepths = "OfxImageEffectPropSupportedPixelDepths";
        public const string ImageEffectPropSupportedComponents = "OfxImageEffectPropSupportedComponents";
        public const string ImageClipPropOptional = "OfxImageClipPropOptional";
        public const string ImageClipPropIsMask = "OfxImageClipPropIsMask";
        public const string ImagePropPixelAspectRatio = "OfxImagePropPixelAspectRatio";
        public const string ImageEffectPropFrameRate = "OfxImageEffectPropFrameRate";
        public const string ImageEffectPropUnmappedFrameRate = "OfxImageEffectPropUnmappedFrameRate";
        public const string ImageEffectPropFrameStep = "OfxImageEffectPropFrameStep";
        public const string ImageEffectPropFrameRange = "OfxImageEffectPropFrameRange";
        public const string ImageEffectPropUnmappedFrameRange = "OfxImageEffectPropUnmappedFrameRange";
        public const string ImageClipPropConnected = "OfxImageClipPropConnected";
        public const string ImageEffectFrameVarying = "OfxImageEffectFrameVarying";
        public const string ImageEffectPropRenderScale = "OfxImageEffectPropRenderScale";
        public const string ImageEffectPropRenderQualityDraft = "OfxImageEffectPropRenderQualityDraft";
        public const string ImageEffectPropOpenGLRenderSupported = "OfxImageEffectPropOpenGLRenderSupported";
        public const string ImageEffectPropCudaRenderSupported = "OfxImageEffectPropCudaRenderSupported";
        public const string ImageEffectPropCudaStreamSupported = "OfxImageEffectPropCudaStreamSupported";
        public const string ImageEffectPropOpenCLRenderSupported = "OfxImageEffectPropOpenCLRenderSupported";
        public const string ImageEffectPropOpenCLSupported = "OfxImageEffectPropOpenCLSupported";
        public const string ImageEffectPropMetalRenderSupported = "OfxImageEffectPropMetalRenderSupported";
        public const string ImageEffectPropCPURenderSupported = "OfxImageEffectPropCPURenderSupported";
        public const string ImageEffectPropOpenGLEnabled = "OfxImageEffectPropOpenGLEnabled";
        public const string ImageEffectPropCudaEnabled = "OfxImageEffectPropCudaEnabled";
        public const string ImageEffectPropCudaStream = "OfxImageEffectPropCudaStream";
        public const string ImageEffectPropOpenCLEnabled = "OfxImageEffectPropOpenCLEnabled";
        public const string ImageEffectPropOpenCLCommandQueue = "OfxImageEffectPropOpenCLCommandQueue";
        public const string ImageEffectPropOpenCLImage = "OfxImageEffectPropOpenCLImage";
        public const string ImageEffectPropOpenGLTextureIndex = "OfxImageEffectPropOpenGLTextureIndex";
        public const string ImageEffectPropOpenGLTextureTarget = "OfxImageEffectPropOpenGLTextureTarget";
        public const string ImageEffectPropProjectExtent = "OfxImageEffectPropProjectExtent";
        public const string ImageEffectPropProjectSize = "OfxImageEffectPropProjectSize";
        public const string ImageEffectPropProjectOffset = "OfxImageEffectPropProjectOffset";
        // 注意: マクロ名は kOfxImageEffectPropProjectPixelAspectRatio だが実文字列に "Project" が入らない
        public const string ImageEffectPropProjectPixelAspectRatio = "OfxImageEffectPropPixelAspectRatio";
        public const string ImageEffectInstancePropEffectDuration = "OfxImageEffectInstancePropEffectDuration";
        public const string ImageClipPropFieldOrder = "OfxImageClipPropFieldOrder";
        public const string ImagePropData = "OfxImagePropData";
        public const string ImagePropBounds = "OfxImagePropBounds";
        public const string ImagePropRegionOfDefinition = "OfxImagePropRegionOfDefinition";
        public const string ImagePropRowBytes = "OfxImagePropRowBytes";
        public const string ImagePropField = "OfxImagePropField";
        public const string ImageEffectPluginPropFieldRenderTwiceAlways = "OfxImageEffectPluginPropFieldRenderTwiceAlways";
        public const string ImageClipPropFieldExtraction = "OfxImageClipPropFieldExtraction";
        public const string ImageEffectPropFieldToRender = "OfxImageEffectPropFieldToRender";
        public const string ImageEffectPropRegionOfDefinition = "OfxImageEffectPropRegionOfDefinition";
        public const string ImageEffectPropRegionOfInterest = "OfxImageEffectPropRegionOfInterest";
        public const string ImageEffectPropRenderWindow = "OfxImageEffectPropRenderWindow";
        public const string ImageFieldNone = "OfxFieldNone";
        public const string ImageFieldLower = "OfxFieldLower";
        public const string ImageFieldUpper = "OfxFieldUpper";
        public const string ImageFieldBoth = "OfxFieldBoth";
        public const string ImageFieldSingle = "OfxFieldSingle";
        public const string ImageFieldDoubled = "OfxFieldDoubled";
        public const string ImageEffectOutputClipName = "Output";
        public const string ImageEffectSimpleSourceClipName = "Source";
        public const string ImageEffectTransitionSourceFromClipName = "SourceFrom";
        public const string ImageEffectTransitionSourceToClipName = "SourceTo";
        // トランジションコンテキストの必須パラメータ（進行度0～1。ホストが毎フレーム設定する）
        public const string ImageEffectTransitionParamName = "Transition";

        // パラメータ型（ofxParam.h）
        public const string ParamTypeInteger = "OfxParamTypeInteger";
        public const string ParamTypeDouble = "OfxParamTypeDouble";
        public const string ParamTypeBoolean = "OfxParamTypeBoolean";
        public const string ParamTypeChoice = "OfxParamTypeChoice";
        public const string ParamTypeStrChoice = "OfxParamTypeStrChoice";
        public const string ParamTypeRGBA = "OfxParamTypeRGBA";
        public const string ParamTypeRGB = "OfxParamTypeRGB";
        public const string ParamTypeDouble2D = "OfxParamTypeDouble2D";
        public const string ParamTypeInteger2D = "OfxParamTypeInteger2D";
        public const string ParamTypeDouble3D = "OfxParamTypeDouble3D";
        public const string ParamTypeInteger3D = "OfxParamTypeInteger3D";
        public const string ParamTypeString = "OfxParamTypeString";
        public const string ParamTypeCustom = "OfxParamTypeCustom";
        public const string ParamTypeBytes = "OfxParamTypeBytes";
        public const string ParamTypeGroup = "OfxParamTypeGroup";
        public const string ParamTypePage = "OfxParamTypePage";
        public const string ParamTypePushButton = "OfxParamTypePushButton";

        // パラメータホストプロパティ
        public const string ParamHostPropSupportsCustomAnimation = "OfxParamHostPropSupportsCustomAnimation";
        public const string ParamHostPropSupportsStringAnimation = "OfxParamHostPropSupportsStringAnimation";
        public const string ParamHostPropSupportsBooleanAnimation = "OfxParamHostPropSupportsBooleanAnimation";
        public const string ParamHostPropSupportsChoiceAnimation = "OfxParamHostPropSupportsChoiceAnimation";
        public const string ParamHostPropSupportsStrChoice = "OfxParamHostPropSupportsStrChoice";
        public const string ParamHostPropSupportsStrChoiceAnimation = "OfxParamHostPropSupportsStrChoiceAnimation";
        public const string ParamHostPropSupportsCustomInteract = "OfxParamHostPropSupportsCustomInteract";
        public const string ParamHostPropMaxParameters = "OfxParamHostPropMaxParameters";
        public const string ParamHostPropMaxPages = "OfxParamHostPropMaxPages";
        public const string ParamHostPropPageRowColumnCount = "OfxParamHostPropPageRowColumnCount";

        // パラメータプロパティ
        public const string ParamPropType = "OfxParamPropType";
        public const string ParamPropAnimates = "OfxParamPropAnimates";
        public const string ParamPropCanUndo = "OfxParamPropCanUndo";
        public const string ParamPropIsAnimating = "OfxParamPropIsAnimating";
        public const string ParamPropPersistant = "OfxParamPropPersistant";
        public const string ParamPropEvaluateOnChange = "OfxParamPropEvaluateOnChange";
        public const string ParamPropSecret = "OfxParamPropSecret";
        public const string ParamPropScriptName = "OfxParamPropScriptName";
        public const string ParamPropCacheInvalidation = "OfxParamPropCacheInvalidation";
        public const string ParamInvalidateValueChange = "OfxParamInvalidateValueChange";
        public const string ParamInvalidateValueChangeToEnd = "OfxParamInvalidateValueChangeToEnd";
        public const string ParamInvalidateAll = "OfxParamInvalidateAll";
        public const string ParamPropHint = "OfxParamPropHint";
        public const string ParamPropDefault = "OfxParamPropDefault";
        public const string ParamPropDoubleType = "OfxParamPropDoubleType";
        public const string ParamDoubleTypePlain = "OfxParamDoubleTypePlain";
        public const string ParamDoubleTypeScale = "OfxParamDoubleTypeScale";
        public const string ParamDoubleTypeAngle = "OfxParamDoubleTypeAngle";
        public const string ParamDoubleTypeTime = "OfxParamDoubleTypeTime";
        public const string ParamDoubleTypeAbsoluteTime = "OfxParamDoubleTypeAbsoluteTime";
        public const string ParamDoubleTypeX = "OfxParamDoubleTypeX";
        public const string ParamDoubleTypeY = "OfxParamDoubleTypeY";
        public const string ParamDoubleTypeXAbsolute = "OfxParamDoubleTypeXAbsolute";
        public const string ParamDoubleTypeYAbsolute = "OfxParamDoubleTypeYAbsolute";
        public const string ParamDoubleTypeXY = "OfxParamDoubleTypeXY";
        public const string ParamDoubleTypeXYAbsolute = "OfxParamDoubleTypeXYAbsolute";
        public const string ParamDoubleTypeNormalisedX = "OfxParamDoubleTypeNormalisedX";
        public const string ParamDoubleTypeNormalisedY = "OfxParamDoubleTypeNormalisedY";
        public const string ParamDoubleTypeNormalisedXY = "OfxParamDoubleTypeNormalisedXY";
        public const string ParamPropDefaultCoordinateSystem = "OfxParamPropDefaultCoordinateSystem";
        public const string ParamCoordinatesCanonical = "OfxParamCoordinatesCanonical";
        public const string ParamCoordinatesNormalised = "OfxParamCoordinatesNormalised";
        public const string ParamPropShowTimeMarker = "OfxParamPropShowTimeMarker";
        public const string PluginPropParamPageOrder = "OfxPluginPropParamPageOrder";
        public const string ParamPropPageChild = "OfxParamPropPageChild";
        public const string ParamPropParent = "OfxParamPropParent";
        public const string ParamPropGroupOpen = "OfxParamPropGroupOpen";
        public const string ParamPropEnabled = "OfxParamPropEnabled";
        public const string ParamPropDataPtr = "OfxParamPropDataPtr";
        public const string ParamPropChoiceOption = "OfxParamPropChoiceOption";
        public const string ParamPropChoiceOrder = "OfxParamPropChoiceOrder";
        public const string ParamPropChoiceEnum = "OfxParamPropChoiceEnum";
        public const string ParamPropMin = "OfxParamPropMin";
        public const string ParamPropMax = "OfxParamPropMax";
        public const string ParamPropDisplayMin = "OfxParamPropDisplayMin";
        public const string ParamPropDisplayMax = "OfxParamPropDisplayMax";
        public const string ParamPropIncrement = "OfxParamPropIncrement";
        public const string ParamPropDigits = "OfxParamPropDigits";
        public const string ParamPropDimensionLabel = "OfxParamPropDimensionLabel";
        public const string ParamPropIsAutoKeying = "OfxParamPropIsAutoKeying";
        public const string ParamPropStringMode = "OfxParamPropStringMode";
        public const string ParamPropStringFilePathExists = "OfxParamPropStringFilePathExists";
        public const string ParamStringIsSingleLine = "OfxParamStringIsSingleLine";
        public const string ParamStringIsMultiLine = "OfxParamStringIsMultiLine";
        public const string ParamStringIsFilePath = "OfxParamStringIsFilePath";
        public const string ParamStringIsDirectoryPath = "OfxParamStringIsDirectoryPath";
        public const string ParamStringIsLabel = "OfxParamStringIsLabel";
        public const string ParamPropCustomValue = "OfxParamPropCustomValue";
        public const string ParamPropInteractV1 = "OfxParamPropInteractV1";
        public const string ParamPropInteractSize = "OfxParamPropInteractSize";
        public const string ParamPropInteractSizeAspect = "OfxParamPropInteractSizeAspect";
        public const string ParamPropInteractMinimumSize = "OfxParamPropInteractMinimumSize";
        public const string ParamPropInteractPreferedSize = "OfxParamPropInteractPreferedSize";
        public const string ParamPropHasHostOverlayHandle = "OfxParamPropHasHostOverlayHandle";

        // メッセージ種別（ofxMessage.h）
        public const string MessageFatal = "OfxMessageFatal";
        public const string MessageError = "OfxMessageError";
        public const string MessageWarning = "OfxMessageWarning";
        public const string MessageMessage = "OfxMessageMessage";
        public const string MessageLog = "OfxMessageLog";
        public const string MessageQuestion = "OfxMessageQuestion";
    }
}

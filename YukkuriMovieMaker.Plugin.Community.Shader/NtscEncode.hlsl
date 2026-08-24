// NTSCコンポジットシミュレーション パス2: エンコード
// ラスター化済み画像(乗算済みアルファRGBA)をYIQへ変換し、I/Qに送信側帯域制限FIRを
// 適用した上で、1チャンネルのコンポジット信号を合成する:
//   composite = setup + (Y + I*sin(theta) + Q*cos(theta)) * (1 - setup)
// setupはセットアップレベル(NTSC-M: 7.5IRE=0.075 / NTSC-J: 0IRE=0)。
//
// 出力: R=コンポジット信号, G=アルファ(帯域制限はデコード側でYと同じFIRを適用), BA=未使用
// 出力バッファはfloat16(信号は[0,1]範囲外の値を取るため)。C#側でSetOutputBufferを設定する。
//
// RGB→YIQ変換行列(SMPTE 170M / ITU-R BT.1700 のNTSC伝送一次式):
//   Y = 0.299 R + 0.587 G + 0.114 B
//   I = 0.5959 R - 0.2746 G - 0.3213 B
//   Q = 0.2115 R - 0.5227 G + 0.3112 B
// 乗算済みアルファのRGBを直接変換する(変換・FIR・変調は全て線形なので
// アルファも同じ系で帯域制限すれば整合が取れる)。

Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    float4 inputRect;    // ラスター化画像の有効矩形(ラスター座標)
    float4 sizeFramePed; // xy: ラスター解像度, z: フレーム番号, w: セットアップレベル
    // 送信側帯域制限FIR(対称カーネルの片側、[0]が中心タップ)。
    // カイザー窓設計(C#側 NtscSignal 参照)。cbuffer内のfloat配列は1要素1レジスタ。
    float yPre[7];       // Y送信LPF 半幅6 (既定4.2MHz)
    float iPre[13];      // I帯域制限 半幅12 (既定1.3MHz)
    float qPre[25];      // Q帯域制限 半幅24 (既定0.4MHz)
};

static const float PI = 3.14159265358979;
// 4fscサンプリングでの1サンプルあたりの副搬送波位相増分 2*PI*fsc/(4fsc) = PI/2
static const float PHASE_PER_SAMPLE = PI / 2;

float3 RgbToYiq(float3 rgb)
{
    return float3(
        dot(rgb, float3(0.299,  0.587,  0.114)),
        dot(rgb, float3(0.5959, -0.2746, -0.3213)),
        dot(rgb, float3(0.2115, -0.5227, 0.3112)));
}

// 現在ピクセルから水平にdxずれた位置のラスター化画像をサンプルする
float4 SampleRaster(float4 posScene, float4 uv0, float dx)
{
    float2 q = posScene.xy + float2(dx, 0);
    q = clamp(q, inputRect.xy + 0.5, inputRect.zw - 0.5);
    return InputTexture.SampleLevel(InputSampler, uv0.xy + (q - posScene.xy) * uv0.zw, 0);
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0
) : SV_Target
{
    if (inputRect.z <= inputRect.x || inputRect.w <= inputRect.y)
        return float4(0, 0, 0, 0);

    float frame = sizeFramePed.z;
    float setup = sizeFramePed.w;

    // 中心タップ
    float4 center = SampleRaster(posScene, uv0, 0);
    float3 yiq0 = RgbToYiq(center.rgb);
    float y = yPre[0] * yiq0.x;
    float i = iPre[0] * yiq0.y;
    float q = qPre[0] * yiq0.z;

    // 対称FIR: 左右のサンプルを加算してから変換する(RGB→YIQは線形なので順序交換可)
    // k=1..6:   Y/I/Q全てに寄与
    [loop]
    for (int k1 = 1; k1 <= 6; k1++)
    {
        float3 s = RgbToYiq(SampleRaster(posScene, uv0, k1).rgb + SampleRaster(posScene, uv0, -k1).rgb);
        y += yPre[k1] * s.x;
        i += iPre[k1] * s.y;
        q += qPre[k1] * s.z;
    }
    // k=7..12:  I/Qのみ
    [loop]
    for (int k2 = 7; k2 <= 12; k2++)
    {
        float3 s = RgbToYiq(SampleRaster(posScene, uv0, k2).rgb + SampleRaster(posScene, uv0, -k2).rgb);
        i += iPre[k2] * s.y;
        q += qPre[k2] * s.z;
    }
    // k=13..24: Qのみ(0.4MHzの狭帯域のため最も長い)
    [loop]
    for (int k3 = 13; k3 <= 24; k3++)
    {
        float3 s = RgbToYiq(SampleRaster(posScene, uv0, k3).rgb + SampleRaster(posScene, uv0, -k3).rgb);
        q += qPre[k3] * s.z;
    }

    // 副搬送波位相 theta(sample, line, frame)
    // 1ライン227.5サイクル→隣接ラインで180°反転、525ライン/フレーム→フレーム間でも180°交番
    // (4フィールドシーケンス)。ライン内はサンプル番号ごとにPI/2進む。
    float sampleIndex = floor(posScene.x);
    float lineIndex = floor(posScene.y);
    float theta = PHASE_PER_SAMPLE * sampleIndex + PI * (lineIndex + frame);

    float sinT, cosT;
    sincos(theta, sinT, cosT);

    // コンポジット信号合成。セットアップレベル分だけ映像振幅を圧縮して黒レベルを持ち上げる
    float composite = setup + (y + i * sinT + q * cosT) * (1 - setup);

    return float4(composite, center.a, 0, 0);
}

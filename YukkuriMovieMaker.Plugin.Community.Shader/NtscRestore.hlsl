// NTSCコンポジットシミュレーション パス4: 復元
// デコード済みのラスター画像(乗算済みアルファRGBA)を元の解像度へ拡大する。
// 水平はバイリニア、垂直はガウシアンのビーム断面で走査線構造を再現する
// (走査線数240のとき往年のゲーム機的な太い走査線の谷間が見える)。

Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    float4 inputRect;  // ラスター画像の有効矩形(ラスター座標)
    float4 sourceRect; // 出力先矩形(元画像のシーン座標)
    float4 rasterSize; // xy: ラスター解像度(有効サンプル数, 走査線数)
};

// 走査線ビーム断面のガウシアンσ(ライン間隔単位)。
// 480ライン時はほぼ隙間が埋まる程度、240ライン時は谷間がはっきり残る値にしている。
// (σ=0.45: ライン中間の重なり 2*exp(-0.5^2/(2σ^2)) ≈ 1.08 → ほぼ平坦
//  σ=0.32: 同 ≈ 0.59 → 谷間が約4割暗くなる)
static const float SCANLINE_SIGMA_480 = 0.45;
static const float SCANLINE_SIGMA_240 = 0.32;

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0
) : SV_Target
{
    if (inputRect.z <= inputRect.x || inputRect.w <= inputRect.y)
        return float4(0, 0, 0, 0);

    float2 srcSize = sourceRect.zw - sourceRect.xy;
    if (srcSize.x <= 0 || srcSize.y <= 0)
        return float4(0, 0, 0, 0);

    // 出力ピクセル → 連続ラスター座標
    float2 raster = (posScene.xy - sourceRect.xy) / srcSize * rasterSize.xy;

    float sigma = rasterSize.y <= 240.5 ? SCANLINE_SIGMA_240 : SCANLINE_SIGMA_480;
    float invTwoSigmaSq = 1.0 / (2.0 * sigma * sigma);

    // 近傍3ラインをビーム断面で重み付けして合成する(水平はサンプラーのバイリニア)
    float centerLine = floor(raster.y - 0.5);
    float4 accum = float4(0, 0, 0, 0);
    float weightSum = 0;

    [unroll]
    for (int j = -1; j <= 1; j++)
    {
        float l = centerLine + j;
        // ラスター外のラインは寄与しない(上下端は自然に減衰する)
        if (l < 0 || l >= rasterSize.y)
            continue;

        float d = raster.y - (l + 0.5);
        float w = exp(-d * d * invTwoSigmaSq);

        float2 q = float2(raster.x, l + 0.5);
        q = clamp(q, inputRect.xy + 0.5, inputRect.zw - 0.5);
        float2 uv = uv0.xy + (q - posScene.xy) * uv0.zw;

        accum += w * InputTexture.SampleLevel(InputSampler, uv, 0);
        weightSum += w;
    }

    // ビーム断面の重なりが1を超える分は正規化し、1未満の谷間は走査線の暗部として残す
    return accum / max(weightSum, 1.0);
}

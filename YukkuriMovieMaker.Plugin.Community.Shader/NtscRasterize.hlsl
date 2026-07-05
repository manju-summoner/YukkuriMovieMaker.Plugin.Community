// NTSCコンポジットシミュレーション パス1: 仮想ラスター化
// 入力画像(元解像度)を有効解像度(既定 754x480)のラスター矩形 {0,0,W,H} へリサンプルする。
// ダウンサンプルフィルタはバイリニア(仕様上、品質不足時はLanczos化を検討)。
// 出力座標系は「x=サンプル番号(4fsc)、y=走査線番号」のラスター空間になる。

Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    // 入力テクスチャの有効矩形(シーン座標)。MapInputRectsToOutputRectで実際の値を設定する。
    // 中間テクスチャはプールされた大きめのテクスチャが割り当てられることがあり、
    // uvの[0,1]範囲チェックでは画像外参照を防げないため、シーン座標側でクランプする。
    float4 inputRect  : packoffset(c0);
    // 論理ソース矩形(プロパティ経由)。ラスター全域がこの矩形に対応する
    float4 sourceRect : packoffset(c1);
    // xy: ラスター解像度(有効サンプル数, 走査線数)
    float4 rasterSize : packoffset(c2);
};

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0
) : SV_Target
{
    // 入力が空(完全透明アイテム等)の場合は透明を返す
    if (inputRect.z <= inputRect.x || inputRect.w <= inputRect.y)
        return float4(0, 0, 0, 0);

    // 出力ピクセル(ラスター座標、テクセル中心 n+0.5)→ ソース上の対応点
    float2 srcSize = sourceRect.zw - sourceRect.xy;
    float2 q = sourceRect.xy + posScene.xy / rasterSize.xy * srcSize;

    // 有効矩形の0.5px内側へクランプ(バイリニアで範囲外のゴミを拾わないように)
    q = clamp(q, inputRect.xy + 0.5, inputRect.zw - 0.5);

    // シーン座標の差分をUVへ変換してサンプル(uv0.zw はシーン座標1pxあたりのUV増分)
    float2 uv = uv0.xy + (q - posScene.xy) * uv0.zw;
    return InputTexture.SampleLevel(InputSampler, uv, 0);
}

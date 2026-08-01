//ARAPパペット変形の頂点シェーダー。
//CPU側で解いた変形後メッシュ頂点をシーン座標へ配置し、
//レスト位置から入力テクスチャのUVを求める。

//Direct2Dが自動供給するシーン→クリップ/入力テクセル変換（公式規約の型のまま宣言すること）
cbuffer Direct2DTransforms : register(b0)
{
    float2x1 sceneToOutputX;
    float2x1 sceneToOutputY;
    float2x1 sceneToInput0X;
    float2x1 sceneToInput0Y;
};

cbuffer Constants : register(b1)
{
    float inputLeft : packoffset(c0.x);
    float inputTop : packoffset(c0.y);
    float inputWidth : packoffset(c0.z);
    float inputHeight : packoffset(c0.w);
};

struct VSIn
{
    //変形後の位置（画像中央原点のローカル座標）
    float2 deformedLocal : POSITION;
    //レスト位置（同ローカル座標）。入力画像のサンプリング位置になる
    float2 restLocal : TEXCOORD;
};

struct VSOut
{
    float4 clipSpaceOutput : SV_POSITION;
    float4 sceneSpaceOutput : SCENE_POSITION;
    float4 texelSpaceInput0 : TEXCOORD0;
};

VSOut main(VSIn input)
{
    float2 center = float2(inputLeft + inputWidth * 0.5f, inputTop + inputHeight * 0.5f);
    float2 scene = input.deformedLocal + center;
    float2 restScene = input.restLocal + center;

    VSOut output;
    output.sceneSpaceOutput = float4(scene.x, scene.y, 0.0f, 1.0f);
    output.clipSpaceOutput = float4(
        scene.x * sceneToOutputX[0] + sceneToOutputX[1],
        scene.y * sceneToOutputY[0] + sceneToOutputY[1],
        0.0f,
        1.0f);
    //xy: 入力テクスチャのUV、zw: シーン→テクセルのスケール（PS側の座標補正用）
    output.texelSpaceInput0 = float4(
        restScene.x * sceneToInput0X[0] + sceneToInput0X[1],
        restScene.y * sceneToInput0Y[0] + sceneToInput0Y[1],
        sceneToInput0X[0],
        sceneToInput0Y[0]);
    return output;
}

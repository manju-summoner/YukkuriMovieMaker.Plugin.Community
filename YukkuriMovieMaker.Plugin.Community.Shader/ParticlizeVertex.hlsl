//粒子化エフェクトの頂点シェーダー。
//頂点バッファは静的（粒子中心＋コーナーオフセットのみ）で、粒子の運動は毎フレームの定数だけで計算する。
//CPU側の頂点再計算・転送が不要になるため、粒子数を増やしても毎フレームのコストはGPUの頂点処理のみ。

//Direct2Dが自動供給するシーン→クリップ/入力テクセル変換（この規約の型のまま宣言すること）
cbuffer Direct2DTransforms : register(b0)
{
    float2x1 sceneToOutputX;
    float2x1 sceneToOutputY;
    float2x1 sceneToInput0X;
    float2x1 sceneToInput0Y;
};

cbuffer Constants : register(b1)
{
    //x: 経過時間(s)、y: 粒子化の伝播期間(s)、z: 1/寿命(1/s)、w: 順序のばらつき(0-1)
    float4 timeSpanLifeRandom : packoffset(c0);
    //xy: 消える方向の単位ベクトル、z: 順序の最小値、w: 1/順序の範囲
    float4 dissolveOrder : packoffset(c1);
    //xy: 飛散速度ベクトル(px/s)、z: 拡散速度(px/s)、w: 重力加速度(px/s^2)
    float4 scatter : packoffset(c2);
    //x: 揺らぎ振幅(px)、y: 回転速度(rad/s)、z: 縮小量(0-1)、w: フェード量(0-1)
    float4 motion : packoffset(c3);
    //x: 乱数シード、yzw: 予約
    float4 seed : packoffset(c4);
};

struct VSIn
{
    //粒子中心（シーン座標）
    float2 center : POSITION;
    //粒子中心からのコーナーオフセット（px）
    float2 corner : TEXCOORD;
};

struct VSOut
{
    float4 clipSpaceOutput : SV_POSITION;
    float4 sceneSpaceOutput : SCENE_POSITION;
    //xy: 入力テクスチャのUV、z: 粒子のアルファ、w: 予約
    float4 texelSpaceInput0 : TEXCOORD0;
};

//粒子中心とシードから決定論的な乱数[0,1)を作る
float Hash(float2 p, float salt)
{
    return frac(sin(dot(p, float2(127.1f, 311.7f)) + salt * 269.5f + seed.x * 419.2f) * 43758.5453f);
}

VSOut main(VSIn input)
{
    float time = timeSpanLifeRandom.x;
    float dissolveSpan = timeSpanLifeRandom.y;
    float lifetimeInv = timeSpanLifeRandom.z;
    float randomness = timeSpanLifeRandom.w;

    float h1 = Hash(input.center, 1.0f);
    float h2 = Hash(input.center, 2.0f);
    float h3 = Hash(input.center, 3.0f);
    float h4 = Hash(input.center, 4.0f);

    //消える方向に沿った進行度(0-1)にばらつきを混ぜて、この粒子の粒子化開始時刻を決める
    float order = saturate((dot(input.center, dissolveOrder.xy) - dissolveOrder.z) * dissolveOrder.w);
    float startTime = lerp(order, h1, randomness) * dissolveSpan;

    //粒子化開始からの経過時間。開始前は0（元の位置に静止）
    float tau = max(time - startTime, 0.0f);
    //寿命に対する進行度。1で完全消滅
    float progress = saturate(tau * lifetimeInv);

    //飛散：指定方向の速度＋粒子毎のランダム方向の拡散＋重力落下＋揺らぎ
    float spreadAngle = h2 * 6.2831853f;
    float2 velocity = scatter.xy + scatter.z * float2(cos(spreadAngle), sin(spreadAngle));
    float2 sway = motion.x * progress * float2(
        sin(input.center.y * 0.043f + tau * 3.1f + h3 * 6.2831853f),
        cos(input.center.x * 0.037f + tau * 2.5f + h4 * 6.2831853f));
    float2 position = input.center + velocity * tau + float2(0.0f, scatter.w * 0.5f * tau * tau) + sway;

    //回転・縮小。寿命が尽きた粒子は面積0にして描画されないようにする
    float angle = motion.y * tau * (h3 * 2.0f - 1.0f);
    float scale = (1.0f - motion.z * progress) * (progress < 1.0f ? 1.0f : 0.0f);
    float s = sin(angle);
    float c = cos(angle);
    float2 corner = float2(
        input.corner.x * c - input.corner.y * s,
        input.corner.x * s + input.corner.y * c) * scale;

    float2 scene = position + corner;
    //サンプリング位置は常に元の位置（粒子は元画像の自分のパッチを持って飛ぶ）
    float2 rest = input.center + input.corner;
    float alpha = 1.0f - motion.w * progress;

    VSOut output;
    output.sceneSpaceOutput = float4(scene, 0.0f, 1.0f);
    output.clipSpaceOutput = float4(
        scene.x * sceneToOutputX[0] + sceneToOutputX[1],
        scene.y * sceneToOutputY[0] + sceneToOutputY[1],
        0.0f,
        1.0f);
    output.texelSpaceInput0 = float4(
        rest.x * sceneToInput0X[0] + sceneToInput0X[1],
        rest.y * sceneToInput0Y[0] + sceneToInput0Y[1],
        alpha,
        0.0f);
    return output;
}

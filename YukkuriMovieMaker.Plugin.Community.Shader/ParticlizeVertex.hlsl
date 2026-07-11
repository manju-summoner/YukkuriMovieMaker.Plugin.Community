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
    //xy: 飛散の初速ベクトル(px/s)、z: 拡散の初速(px/s)、w: 重力の終端速度(px/s)
    float4 scatter : packoffset(c2);
    //x: 渦の角速度(rad/s)、y: 回転速度(rad/s)、z: 縮小量(0-1)、w: フェード量(0-1)
    float4 motion : packoffset(c3);
    //x: 乱数シード、yz: 風の終端速度ベクトル(px/s)、w: 予約
    float4 seedWind : packoffset(c4);
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
    return frac(sin(dot(p, float2(127.1f, 311.7f)) + salt * 269.5f + seedWind.x * 419.2f) * 43758.5453f);
}

//空間的に相関するバリューノイズ[0,1)。粒子化の順序を雲状のまだらにするために使う
float ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0f - 2.0f * f);
    return lerp(
        lerp(Hash(i, 11.0f), Hash(i + float2(1.0f, 0.0f), 11.0f), u.x),
        lerp(Hash(i + float2(0.0f, 1.0f), 11.0f), Hash(i + float2(1.0f, 1.0f), 11.0f), u.x),
        u.y);
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

    //消える方向に沿った進行度(0-1)に、雲状ノイズ（空間相関）主体のばらつきを混ぜて
    //この粒子の粒子化開始時刻を決める。独立乱数だけだと消え際がザラついた直線になるため、
    //低周波＋中周波のバリューノイズで塊単位にまだらに消えるようにする
    float order = saturate((dot(input.center, dissolveOrder.xy) - dissolveOrder.z) * dissolveOrder.w);
    float cloud = ValueNoise(input.center * 0.021f) * 0.65f + ValueNoise(input.center * 0.057f) * 0.35f;
    float jitter = lerp(cloud, h1, 0.15f);
    float startTime = lerp(order, jitter, randomness) * dissolveSpan;

    //粒子化開始からの経過時間。開始前は0（元の位置に静止）
    float tau = max(time - startTime, 0.0f);
    //寿命に対する進行度。1で完全消滅
    float progress = saturate(tau * lifetimeInv);

    //線形抵抗 v' = -k(v - v_wind) の厳密解を初速項と風項に分離して使う。
    //tauD = (1 - e^(-k*tau)) / k は tau→∞ で 1/k に飽和する（k=1.5/寿命）。
    //初速項の変位 = v0 * tauD（ひと押しされて減速）、
    //風・重力項の変位 = v_wind * (tau - tauD)（静止から終端速度へ漸近）
    float decay = 1.5f * lifetimeInv;
    float tauD = (1.0f - exp(-decay * tau)) / decay;
    float tauW = tau - tauD;

    //飛散（初速）：指定方向の初速＋粒子毎のランダム方向の拡散。
    //速度方向を粒子毎の角速度で旋回させ、直線ではなく渦を巻く軌道にする
    float spreadAngle = h2 * 6.2831853f;
    float2 velocity = scatter.xy + scatter.z * float2(cos(spreadAngle), sin(spreadAngle));
    float swirl = motion.x * (h4 * 2.0f - 1.0f) * tauD;
    float swirlSin = sin(swirl);
    float swirlCos = cos(swirl);
    float2 swirlVelocity = float2(
        velocity.x * swirlCos - velocity.y * swirlSin,
        velocity.x * swirlSin + velocity.y * swirlCos);

    //風・重力（加速度型）：静止から終端速度へ漸近しながら流されていく
    float2 driftVelocity = float2(seedWind.y, seedWind.z + scatter.w);
    float2 position = input.center + swirlVelocity * tauD + driftVelocity * tauW;

    //フェードは「最初ゆっくり、最後すっと霧散」の曲線
    float fadeProgress = 1.0f - (1.0f - progress) * (1.0f - progress);
    float alpha = 1.0f - motion.w * fadeProgress;

    //回転・スケール。煙のように膨張しながら薄くなる（縮小量0で1.5倍まで膨張、縮小量1で0まで縮小）。
    //寿命が尽きた粒子は面積0にして描画されないようにする
    float angle = motion.y * tau * (h3 * 2.0f - 1.0f);
    float scale = (1.0f - motion.z * progress) * (1.0f + 0.5f * progress) * (progress < 1.0f ? 1.0f : 0.0f);
    float s = sin(angle);
    float c = cos(angle);
    float2 corner = float2(
        input.corner.x * c - input.corner.y * s,
        input.corner.x * s + input.corner.y * c) * scale;

    float2 scene = position + corner;
    //サンプリング位置は常に元の位置（粒子は元画像の自分のパッチを持って飛ぶ）
    float2 rest = input.center + input.corner;

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

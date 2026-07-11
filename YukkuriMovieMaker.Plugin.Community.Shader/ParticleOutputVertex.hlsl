//パーティクル出力エフェクトの頂点シェーダー。
//頂点バッファは静的（粒子スロット番号＋コーナーオフセットのみ）で、粒子の発生・運動は毎フレームの定数だけで計算する。
//スロットは周期的に再利用され、スロット番号×世代番号のハッシュで乱数を決めるため、どの時刻から評価しても同じ絵になる。

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
    //x: 経過時間(s)、y: 発生間隔(s)、z: 1/寿命(1/s)、w: ばらつき(0-1)
    float4 timeRateLife : packoffset(c0);
    //xy: 入力範囲の中心（シーン座標）、zw: 入力範囲の半径（px）
    float4 boundsCenterHalf : packoffset(c1);
    //x: 射出方向(rad)、y: 拡散の半角(rad)、z: 初速(px/s)、w: 重力の終端速度(px/s)
    float4 emission : packoffset(c2);
    //x: 揺らぎの角速度(rad/s)、y: 回転速度の最大値(rad/s)、z: 寿命終了時のスケール、w: フェード量(0-1)
    float4 motion : packoffset(c3);
    //x: 乱数シード、yz: 風の終端速度ベクトル(px/s)、w: スロット数
    float4 seedWind : packoffset(c4);
    //xy: 粒子の半径（px）、zw: 発生範囲の半径（px）
    float4 patchEmit : packoffset(c5);
};

struct VSIn
{
    //x: 粒子スロット番号、y: 予約
    float2 slot : POSITION;
    //粒子中心からのコーナー（-1～+1）
    float2 corner : TEXCOORD;
};

struct VSOut
{
    float4 clipSpaceOutput : SV_POSITION;
    float4 sceneSpaceOutput : SCENE_POSITION;
    //xy: 入力テクスチャのUV、z: 粒子のアルファ、w: 予約
    float4 texelSpaceInput0 : TEXCOORD0;
};

//スロット番号と世代番号とシードから決定論的な乱数[0,1)を作る
float Hash(float2 p, float salt)
{
    return frac(sin(dot(p, float2(127.1f, 311.7f)) + salt * 269.5f + seedWind.x * 419.2f) * 43758.5453f);
}

VSOut main(VSIn input)
{
    float time = timeRateLife.x;
    float emitInterval = timeRateLife.y;
    float lifetimeInv = timeRateLife.z;
    float randomness = timeRateLife.w;
    float slotCount = seedWind.w;

    //このスロットの直近の発生時刻と世代番号。スロットsは s*間隔 を起点に 周期=スロット数*間隔 ごとに再発生する。
    //周期は寿命より長い（CPU側でスロット数を寿命×レート以上に確保している）ため、生存中の再利用は起きない
    float slot = input.slot.x;
    float cycle = slotCount * emitInterval;
    float generation = floor((time - slot * emitInterval) / cycle);
    float birth = slot * emitInterval + generation * cycle;
    float tau = time - birth;
    float progress = tau * lifetimeInv;

    //発生前（時刻0以前・世代が負）か、寿命が尽きた粒子は面積0にして描画しない。
    //時刻0ちょうどはCPU側が出力範囲を縮退させるため、可視判定もtime>0で揃えて断片が描画されないようにする
    float visible = (time > 0.0f && generation >= 0.0f && progress < 1.0f) ? 1.0f : 0.0f;
    progress = saturate(progress);

    float2 hashKey = float2(slot, generation);
    float h1 = Hash(hashKey, 1.0f);
    float h2 = Hash(hashKey, 2.0f);
    float h3 = Hash(hashKey, 3.0f);
    float h4 = Hash(hashKey, 4.0f);
    float h5 = Hash(hashKey, 5.0f);
    float h6 = Hash(hashKey, 6.0f);
    float h7 = Hash(hashKey, 7.0f);

    //発生位置：入力範囲の中心＋発生範囲内のランダムオフセット
    float2 origin = boundsCenterHalf.xy + (float2(h6, h7) * 2.0f - 1.0f) * patchEmit.zw;

    //初速：射出方向±拡散の半角、速さはばらつきに応じて30%～100%に分散させる
    float direction = emission.x + (h1 * 2.0f - 1.0f) * emission.y;
    float speed = emission.z * lerp(1.0f, 0.3f + 0.7f * h2, randomness);
    float2 velocity = speed * float2(cos(direction), sin(direction));

    //線形抵抗 v' = -k(v - v_terminal) の減衰運動（粒子化と同じモデル）。
    //初速項の変位 = v0 * tauD（1/kに飽和）、風・重力項の変位 = v_terminal * (tau - tauD)（終端速度へ漸近）
    float decay = 1.5f * lifetimeInv;
    float tauD = (1.0f - exp(-decay * tau)) / decay;
    float tauW = tau - tauD;

    //揺らぎ：初速の向きを粒子ごとの角速度で旋回させ、直進ではなく渦を巻く軌道にする
    float swirl = motion.x * (h4 * 2.0f - 1.0f) * tauD;
    float swirlSin = sin(swirl);
    float swirlCos = cos(swirl);
    float2 swirlVelocity = float2(
        velocity.x * swirlCos - velocity.y * swirlSin,
        velocity.x * swirlSin + velocity.y * swirlCos);

    float2 driftVelocity = float2(seedWind.y, seedWind.z + emission.w);
    float2 position = origin + swirlVelocity * tauD + driftVelocity * tauW;

    //フェードは「最初ゆっくり、最後ほど急」の曲線
    float fadeProgress = 1.0f - (1.0f - progress) * (1.0f - progress);
    float alpha = 1.0f - motion.w * fadeProgress;

    //回転とスケール。サイズは寿命に沿って開始1→終了endScaleへ線形補間し、ばらつきで0.5～1.5倍に分散させる
    float angle = motion.y * tau * (h5 * 2.0f - 1.0f);
    float sizeFactor = lerp(1.0f, 0.5f + h3, randomness);
    float scale = sizeFactor * lerp(1.0f, motion.z, progress) * visible;
    float s = sin(angle);
    float c = cos(angle);
    //先に粒子の実サイズへスケールしてから回転する（回転後の非等方スケールは形が歪む）
    float2 scaledCorner = input.corner * patchEmit.xy * scale;
    float2 corner = float2(
        scaledCorner.x * c - scaledCorner.y * s,
        scaledCorner.x * s + scaledCorner.y * c);

    float2 scene = position + corner;
    //サンプリング位置は常に入力範囲全体（粒子は元画像全体の縮小コピー）
    float2 rest = boundsCenterHalf.xy + input.corner * boundsCenterHalf.zw;

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

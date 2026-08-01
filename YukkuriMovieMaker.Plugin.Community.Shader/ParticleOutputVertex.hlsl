//パーティクル出力エフェクトの頂点シェーダー。
//CPU側（ParticleOutputParticleBuilder）が毎フレーム、生存中の粒子の「発生時点で確定した属性」を
//頂点データに焼き込み、本シェーダーは経過時間に応じた運動（減衰・渦・ドリフト・回転・縮小・フェード）を計算する。
//発生時刻とドリフト積分は「現在時刻からの相対値」で渡されるため、タイムライン上の絶対時刻が大きくてもfloat精度が落ちない。

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
    //xy: 入力範囲の中心（シーン座標）、zw: 入力範囲の半径（px）
    float4 boundsCenterHalf : packoffset(c0);
    //x: 焦点距離(px)、y: near判定denominator、z: 透視投影有効、w: 予約
    float4 perspective : packoffset(c1);
};

struct VSIn
{
    //粒子中心からのコーナー（-1から+1）
    float2 corner : POSITION;
    //x: 発生時刻-現在時刻(s, 0以下)、y: サイズ倍率、zw: 発生位置オフセット（px）
    float4 birthSizeOrigin : TEXCOORD0;
    //xy: 初速ベクトル(px/s)、z: 渦の角速度(rad/s)、w: 回転の角速度(rad/s)
    float4 velocitySwirlRotate : TEXCOORD1;
    //x: 1/寿命(1/s)、y: 寿命終了時のスケール、z: フェード量(0-1)、w: curl込み最終Z
    float4 lifeScaleFade : TEXCOORD2;
    //風・重力による現在までのドリフト変位（px。CPU側で減衰カーネルを積分済み）
    float2 driftDisplacement : TEXCOORD3;
};

struct VSOut
{
    float4 clipSpaceOutput : SV_POSITION;
    float4 sceneSpaceOutput : SCENE_POSITION;
    //xy: 入力テクスチャのUV、z: 粒子のアルファ、w: 予約
    float4 texelSpaceInput0 : TEXCOORD0;
};

VSOut main(VSIn input)
{
    float tau = -input.birthSizeOrigin.x;
    float lifetimeInv = input.lifeScaleFade.x;
    float progress = saturate(tau * lifetimeInv);

    //CPU側が生存中の粒子だけを書き込むが、境界の数値誤差で寿命を跨いだ場合に備えて面積0で保険をかける
    float visible = (tau >= 0.0f && tau * lifetimeInv < 1.0f) ? 1.0f : 0.0f;

    //線形抵抗 v' = -k(v - v_terminal) の減衰運動（粒子化と同じモデル）。
    //初速項の変位 = v0 * tauD（1/kに飽和）。風・重力項の変位はCPU側で減衰カーネルを積分して頂点に焼き込み済み
    float decay = 1.5f * lifetimeInv;
    float tauD = (1.0f - exp(-decay * tau)) / decay;

    //揺らぎ：初速の向きを粒子ごとの角速度で旋回させ、直進ではなく渦を巻く軌道にする
    float swirl = input.velocitySwirlRotate.z * tauD;
    float swirlSin = sin(swirl);
    float swirlCos = cos(swirl);
    float2 velocity = input.velocitySwirlRotate.xy;
    float2 swirlVelocity = float2(
        velocity.x * swirlCos - velocity.y * swirlSin,
        velocity.x * swirlSin + velocity.y * swirlCos);


    float2 position = boundsCenterHalf.xy + input.birthSizeOrigin.zw + swirlVelocity * tauD + input.driftDisplacement;

    //z>0を奥とする透視投影。0%時は分岐内へ入らず、旧2D経路と厳密に一致する。
    float projection = 1.0f;
    if (perspective.z > 0.0f)
    {
        float denominator = perspective.x + input.lifeScaleFade.w;
        visible *= denominator > perspective.y ? 1.0f : 0.0f;
        projection = min(20.0f, perspective.x / max(denominator, perspective.y));
        position = boundsCenterHalf.xy + (position - boundsCenterHalf.xy) * projection;
    }

    //フェードは「最初ゆっくり、最後ほど急」の曲線
    float fadeProgress = 1.0f - (1.0f - progress) * (1.0f - progress);
    float alpha = 1.0f - input.lifeScaleFade.z * fadeProgress;

    //回転とスケール。サイズは寿命に沿って開始1から終了endScaleへ線形補間する
    float angle = input.velocitySwirlRotate.w * tau;
    float scale = input.birthSizeOrigin.y * lerp(1.0f, input.lifeScaleFade.y, progress) * projection * visible;
    float s = sin(angle);
    float c = cos(angle);
    //先に粒子の実サイズへスケールしてから回転する（回転後の非等方スケールは形が歪む）
    float2 scaledCorner = input.corner * boundsCenterHalf.zw * scale;
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

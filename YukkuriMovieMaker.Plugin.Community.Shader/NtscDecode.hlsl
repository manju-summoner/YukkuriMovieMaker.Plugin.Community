// NTSCコンポジットシミュレーション パス3: デコード
// コンポジット信号(R=信号, G=アルファ)を水平1D FIRフィルタ群で復調する。
//   Y抽出: ノッチ(fsc近傍除去+LPF合成カーネル) または 2ラインコム + LPF (モード切替)
//   I/Q抽出: fscでの同期検波(sin/cos乗算) + ローパスFIR
// さらに信号ノイズ、VHSモード時のトラッキング揺れ・ヘッドスイッチングノイズ・
// ドロップアウトを付加する。出力は乗算済みアルファのRGBA(float16バッファ)。
//
// YIQ→RGB逆変換行列(SMPTE 170M / ITU-R BT.1700):
//   R = Y + 0.9563 I + 0.6210 Q
//   G = Y - 0.2721 I - 0.6474 Q
//   B = Y - 1.1070 I + 1.7046 Q

#include "Hash.hlsli"

Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    float4 inputRect;    // コンポジット信号テクスチャの有効矩形(ラスター座標)
    float4 sizeFramePed; // xy: ラスター解像度, z: フレーム番号, w: セットアップレベル
    float4 modeNoise;    // x: Y/C分離(0=ノッチ 1=コム), y: ノイズ量(0..1), z: VHS(0/1), w: VHSトラッキング(0..1)
    float4 vhsParams;    // x: VHSノイズ量(0..1), y: ドロップアウト頻度(0..1), zw: 予備
    // 復調FIR(対称カーネルの片側、[0]が中心)。カイザー窓設計(C#側 NtscSignal 参照)。
    float yTaps[17];     // Y抽出 半幅16 (ノッチモード時はノッチ+LPF合成済み)
    float iTaps[25];     // I復調LPF 半幅24 (既定1.3MHz)
    float qTaps[25];     // Q復調LPF 半幅24 (既定0.4MHz)
};

static const float PI = 3.14159265358979;
static const float PHASE_PER_SAMPLE = PI / 2; // 4fscサンプリング: 1サンプル=90°

// ---- ノイズ・VHSパラメータ(劣化度1のときの最大値。VHS劣化度を乗じて使用) ----
static const float NOISE_LUMA_SCALE = 0.35;   // ノイズ量1.0のときの輝度ノイズ振幅
static const float NOISE_CHROMA_SCALE = 0.25; // 同、クロマノイズ振幅
static const float VHS_BASE_NOISE = 0.08;     // VHSで常時追加される輝度ノイズ
static const float TRACKING_NOISE_AMP = 2.5;  // ライン単位のランダム水平ずれ(サンプル)
static const float TRACKING_WAVE_AMP = 1.5;   // ゆっくりした波状の揺れ(サンプル)
static const float HEAD_SWITCH_LINES = 6.0;   // 画面最下部のヘッドスイッチング帯のライン数
static const float HEAD_SWITCH_SHIFT = 24.0;  // ヘッドスイッチング帯の最大水平ずれ(サンプル)
static const float DROPOUT_RATE = 0.02;       // 1ライン・1フレームあたりのドロップアウト発生確率
static const float DROPOUT_LEVEL = 0.85;      // ドロップアウトの明るさ上限(1=完全白。下げるほど元映像が残り馴染む)
static const float DROPOUT_GRAIN = 0.5;       // スジ内の横方向ノイズ質感の深さ(ベタ白感を崩す)
// 水平ずれの上限。C#側の矩形膨張(DecodeChromaHalfTaps + MaxTrackingShift)と一致させること
static const float MAX_TRACK_SHIFT = 40.0;

// 現在ピクセルから(dx, dy)ずれた位置のコンポジット信号をサンプルする。rg=(信号, アルファ)
float2 SampleComposite(float4 posScene, float4 uv0, float dx, float dy)
{
    float2 q = posScene.xy + float2(dx, dy);
    q = clamp(q, inputRect.xy + 0.5, inputRect.zw - 0.5);
    return InputTexture.SampleLevel(InputSampler, uv0.xy + (q - posScene.xy) * uv0.zw, 0).rg;
}

// Y/C分離した1タップ分を取得する。
// ノッチモード: yはコンポジットそのもの(ノッチはyTapsに合成済み)、クロマもコンポジットそのもの。
// コムモード: 隣接ラインは副搬送波位相が180°反転しているため、
//   Y成分 = (2C(l) + C(l-1) + C(l+1)) / 4  (クロマが相殺される)
//   C成分 = (2C(l) - C(l-1) - C(l+1)) / 4  (Yの縦方向共通成分が相殺される)
void GetSeparated(float4 posScene, float4 uv0, float dx, bool comb, out float2 luma, out float chroma)
{
    float2 c = SampleComposite(posScene, uv0, dx, 0);
    if (comb)
    {
        float2 ct = SampleComposite(posScene, uv0, dx, -1);
        float2 cb = SampleComposite(posScene, uv0, dx, +1);
        luma = (2 * c + ct + cb) * 0.25;
        chroma = (2 * c.x - ct.x - cb.x) * 0.25;
    }
    else
    {
        luma = c;
        chroma = c.x;
    }
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0
) : SV_Target
{
    if (inputRect.z <= inputRect.x || inputRect.w <= inputRect.y)
        return float4(0, 0, 0, 0);

    float2 rasterSize = sizeFramePed.xy;
    float frame = sizeFramePed.z;
    float setup = sizeFramePed.w;
    bool comb = modeNoise.x > 0.5;
    float noiseAmount = modeNoise.y;
    bool vhs = modeNoise.z > 0.5;
    float tracking = modeNoise.w;   // VHSトラッキング(横揺れ+ヘッドスイッチング)の強さ
    float vhsNoise = vhsParams.x;   // VHS常時ノイズの量
    float dropout = vhsParams.y;    // ドロップアウト頻度

    float lineIndex = floor(posScene.y);

    // ---- VHS: トラッキング揺れ・ヘッドスイッチング(ライン単位の水平ずれ) ----
    float du = 0;
    float extraNoise = 0;
    if (vhs)
    {
        // ライン単位のランダムずれ + ゆっくりした波状の揺れ(フレーム番号シードのハッシュノイズ)
        du += (hash12(float2(lineIndex, frame)) - 0.5) * 2 * TRACKING_NOISE_AMP * tracking;
        du += sin(lineIndex * 0.11 + frame * 0.63) * TRACKING_WAVE_AMP * tracking;

        // ヘッドスイッチングノイズ: 画面最下部の数ラインが大きく水平にずれ、ノイズが乗る
        float headBandStart = rasterSize.y - HEAD_SWITCH_LINES;
        if (lineIndex >= headBandStart)
        {
            float t = (lineIndex - headBandStart + 1) / HEAD_SWITCH_LINES; // 下端ほど強く
            du += (hash12(float2(frame, lineIndex)) - 0.3) * HEAD_SWITCH_SHIFT * t * tracking;
            extraNoise += 0.5 * t * tracking;
        }
        extraNoise += VHS_BASE_NOISE * vhsNoise;
    }
    du = clamp(du, -MAX_TRACK_SHIFT, MAX_TRACK_SHIFT);

    // ---- 副搬送波位相 ----
    // デコーダはラインごとのバースト同期を仮定し、位相はずれた信号位置に追従させる
    // (トラッキングずれで色相が回らないように)。エンコード側はサンプル番号n=floor(x)で
    // 位相を刻んでいるため、テクセル中心(n+0.5)基準の連続位置からは0.5を引く。
    float x0 = posScene.x + du;
    float theta = PHASE_PER_SAMPLE * (x0 - 0.5) + PI * (lineIndex + frame);
    float sin0, cos0;
    sincos(theta, sin0, cos0);

    // ---- Y/C分離 + 同期検波 + 復調LPF ----
    float2 luma0;
    float chroma0;
    GetSeparated(posScene, uv0, du, comb, luma0, chroma0);

    float2 ySum = yTaps[0] * luma0;          // (Y信号, アルファ)
    float iSum = iTaps[0] * 2 * chroma0 * sin0;
    float qSum = qTaps[0] * 2 * chroma0 * cos0;

    [loop]
    for (int k = 1; k <= 24; k++)
    {
        float2 lumaP, lumaM;
        float chromaP, chromaM;
        GetSeparated(posScene, uv0, du + k, comb, lumaP, chromaP);
        GetSeparated(posScene, uv0, du - k, comb, lumaM, chromaM);

        if (k <= 16)
            ySum += yTaps[k] * (lumaP + lumaM);

        // sin/cos(theta ± k*PI/2) は90°ステップの象限テーブルで求める(三角関数は1回だけ)
        int m = k & 3;
        float sinP = (m == 0) ? sin0 : (m == 1) ? cos0 : (m == 2) ? -sin0 : -cos0;
        float cosP = (m == 0) ? cos0 : (m == 1) ? -sin0 : (m == 2) ? -cos0 : sin0;
        float sinM = (m == 0) ? sin0 : (m == 1) ? -cos0 : (m == 2) ? -sin0 : cos0;
        float cosM = (m == 0) ? cos0 : (m == 1) ? sin0 : (m == 2) ? -cos0 : -sin0;

        iSum += iTaps[k] * 2 * (chromaP * sinP + chromaM * sinM);
        qSum += qTaps[k] * 2 * (chromaP * cosP + chromaM * cosM);
    }

    // ---- セットアップレベル除去・ゲイン復元 ----
    // エンコード側で composite = setup + 映像 * (1 - setup) としているため逆変換する。
    // アルファ(G)にはセットアップが乗っていないのでそのまま使う。
    float invGain = 1.0 / (1.0 - setup);
    float y = (ySum.x - setup) * invGain;
    float i = iSum * invGain;
    float q = qSum * invGain;
    float alpha = ySum.y;

    // ---- 信号ノイズ(復調後のY/I/Qへ独立に付加する近似) ----
    float n = noiseAmount * NOISE_LUMA_SCALE + extraNoise;
    float nc = noiseAmount * NOISE_CHROMA_SCALE + extraNoise * 0.5;
    if (n > 0 || nc > 0)
    {
        // ノイズもアルファに追従させる(透明部分にノイズが浮かないように)
        float3 rnd = hash33(float3(posScene.x, lineIndex, frame)) - 0.5;
        y += rnd.x * 2 * n * alpha;
        i += rnd.y * 2 * nc * alpha;
        q += rnd.z * 2 * nc * alpha;
    }

    // ---- VHSドロップアウト(まれな白い横線) ----
    if (vhs)
    {
        float r = hash12(float2(lineIndex, frame) + 17.17);
        if (r < DROPOUT_RATE * dropout)
        {
            float2 seg = hash22(float2(frame * 3.1, lineIndex * 1.7));
            float start = seg.x * rasterSize.x;
            float len = (0.05 + 0.25 * seg.y) * rasterSize.x;
            float t = (posScene.x - start) / max(len, 1);
            if (0 <= t && t <= 1)
            {
                // 実機のドロップアウトは信号が抜けて輝度が飛ぶが、純白のベタ塗りはCG的で浮く。
                // (1)完全白まで振らず元映像をわずかに残し (2)スジ内に細かな横方向のノイズ質感を
                // 混ぜてベタ感を崩すことで、ザラついた明るいスジとして馴染ませる。
                float envelope = sin(t * PI); // 両端をなめらかに
                float grain = hash12(float2(posScene.x, lineIndex + frame) * 0.7 + 5.3);
                float level = envelope * DROPOUT_LEVEL * (1 - DROPOUT_GRAIN * grain);
                y = lerp(y, alpha, level); // 乗算済みアルファでの白 = アルファ値
                i *= 1 - level;
                q *= 1 - level;
            }
        }
    }

    // ---- YIQ→RGB ----
    float3 rgb = float3(
        y + 0.9563 * i + 0.6210 * q,
        y - 0.2721 * i - 0.6474 * q,
        y - 1.1070 * i + 1.7046 * q);

    // 乗算済みアルファとして不正な値を残さない(rgb <= alpha)。
    // 実機の100%白クリップに相当し、後段のfloatバッファへの不正値伝播も防ぐ
    alpha = saturate(alpha);
    rgb = clamp(rgb, 0, alpha);

    return float4(rgb, alpha);
}

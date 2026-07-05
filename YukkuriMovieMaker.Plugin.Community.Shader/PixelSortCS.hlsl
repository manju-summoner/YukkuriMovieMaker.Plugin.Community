// ピクセルソートエフェクト
// 輝度がしきい値範囲内の連続ピクセル区間(スパン)を方向軸に沿って検出し、
// 区間内のピクセルを輝度順に並べ替える(グリッチアートの定番表現)。
// スレッドグループ単位の並列ソートで実装しており、ロード/書き戻しの
// 計3サンプル/ピクセルとgroupshared上のbitonicソート(固定段数)で、
// 区間の長さに関わらず1ピクセルあたりほぼ定数コストで動作する。
// 一度にソートできる区間はgroupshared容量(32KB)によりMAX_SPAN=4096pxが上限。
// これはYMM4Constants.MaximumShaderImageSize(シェーダーが分割なしで扱える
// 画像サイズ上限)と同値のため、実用上は画像全体を1区間として扱える。
//
// アルゴリズム:
//   1. 1グループが1ライン上の連続する複数チャンク(区間グリッドの区画、合計最大4096px)を
//      groupsharedへロードする(チャンクが短いほど多くのチャンクをまとめて処理する)
//   2. pointer jumpingでセグメント(しきい値内かつ同一チャンクの連続区間)の先頭indexを並列計算
//   3. キー(セグメント先頭12bit << 16 | 輝度16bit)を組み立て、元indexをタイブレークに
//      bitonicソート。セグメントは処理範囲を連続分割するため、全体の安定ソートで
//      「各セグメント内は輝度順・セグメント外は不動」の正確な置換が得られる
//   4. 置換に従って入力をサンプルし出力UAVへ書き込む
//
// fxcの制約(X3663)により、バリアは全スレッドが無条件に到達する定数回ループに置き、
// 実作業のみnpow2等でスキップする構造にしている(早期returnも使わない)。
//
// D2Dコンピュートトランスフォームの規約(公式サンプルD2DCustomEffects/ComputeShader準拠):
//   - 入力はt0/s0。テクスチャ上の画像原点は(0,0)とは限らず、b0のsceneToInput0係数で
//     シーン座標→正規化テクセルUVへ変換してSampleLevelで読む
//   - アプリ定数はb1(b0はD2Dのシステム定数専用)
//   - 出力UAVはregister注釈なしで宣言し、シーン座標+outputOffsetへ書き込む

#define THREADS 256
#define MAX_SPAN 4096
#define MAX_ELEMS (MAX_SPAN / THREADS)

Texture2D<float4> InputTexture : register(t0);
SamplerState InputSampler : register(s0);

RWTexture2D<float4> OutputTexture;

// D2Dが自動で供給するシステム定数
cbuffer systemConstants : register(b0)
{
    int4 resultRect;       // 今回の処理対象矩形(シーン座標、ダーティレクト時は部分矩形)
    int2 outputOffset;     // 出力UAV内へのオフセット
    float2 sceneToInput0X; // シーン座標→入力テクセルUVの変換係数(scale, bias)
    float2 sceneToInput0Y;
};

// アプリ定数(C#側PixelSortCompute参照)
cbuffer constants : register(b1)
{
    float4 imageRect; // 入力画像の有効矩形(シーン座標)
    float4 dirSpan;   // x: 軸(0=横,1=縦), y: 降順フラグ, z: 区間最大長(px), w: 強さ(0-1)
    float4 threshold; // x: 輝度下限, y: 輝度上限, zw: 未使用
};

// groupshared 2本で32KB(cs_5_0の上限いっぱい)
// keys: ロード時 (輝度16bit<<16 | ソート対象フラグ1bit) → ソート時 (セグメント先頭12bit<<16 | 輝度キー16bit)
// idxs: セグメント先頭計算時はポインタ、ソート時は元index
groupshared uint keys[MAX_SPAN];
groupshared uint idxs[MAX_SPAN];

// 画像データはテクスチャ上の(0,0)から始まるとは限らないため、
// D2Dが供給する係数でシーン座標をテクセルUVへ変換する
float2 ConvertInput0SceneToTexelSpace(float2 inputScenePosition)
{
    float2 ret;
    ret.x = inputScenePosition.x * sceneToInput0X[0] + sceneToInput0X[1];
    ret.y = inputScenePosition.y * sceneToInput0Y[0] + sceneToInput0Y[1];
    return ret;
}

// シーン座標のピクセルpの色(ピクセル中心をサンプル)
float4 SampleScene(int2 p)
{
    return InputTexture.SampleLevel(InputSampler, ConvertInput0SceneToTexelSpace(float2(p) + 0.5), 0);
}

float Luminance(float4 color)
{
    // 乗算済みアルファのままBT.601輝度を取る(透明部は0になり自然にスパンが切れる)
    return dot(color.rgb, float3(0.299, 0.587, 0.114));
}

// ライン(交差軸座標)と軸座標からシーン座標を組み立てる
int2 ScenePosition(bool vertical, int lineCoord, int axisCoord)
{
    return vertical ? int2(lineCoord, axisCoord) : int2(axisCoord, lineCoord);
}

// 軸座標aのピクセル(中心a+0.5)が属するチャンク番号(ピクセルシェーダー版と同一の定義)
int ChunkIndex(int axisCoord, int imgAxisMin, float spanLength)
{
    return (int)floor(((float)axisCoord + 0.5 - imgAxisMin) / spanLength);
}

[numthreads(THREADS, 1, 1)]
void main(
    uint3 groupId       : SV_GroupID,
    uint3 groupThreadId : SV_GroupThreadID)
{
    uint tid = groupThreadId.x;
    bool vertical = dirSpan.x != 0;
    bool descending = dirSpan.y != 0;
    float spanLength = dirSpan.z;
    float strength = dirSpan.w;
    float lo = threshold.x;
    float hi = threshold.y;

    // 軸方向(ソート方向)と交差方向の範囲
    int imgAxisMin  = vertical ? (int)imageRect.y : (int)imageRect.x;
    int imgAxisMax  = vertical ? (int)imageRect.w : (int)imageRect.z;
    int outAxisMin  = vertical ? resultRect.y : resultRect.x;
    int outAxisMax  = vertical ? resultRect.w : resultRect.z;
    int outCrossMin = vertical ? resultRect.x : resultRect.y;
    int outCrossMax = vertical ? resultRect.z : resultRect.w;

    // このグループが担当するラインとチャンク範囲。
    // 1グループはchunksPerGroup個の連続チャンク(合計MAX_SPAN px以下)を担当する。
    // chunkFirst/chunksPerGroupはC#側CalculateThreadgroupsと同一の式で求める
    int lineCoord = outCrossMin + (int)groupId.x;
    int chunksPerGroup = max(1, MAX_SPAN / ((int)ceil(spanLength) + 1));
    int chunkFirst = (int)floor(((float)outAxisMin + 0.5 - imgAxisMin) / spanLength);
    int chunkBegin = chunkFirst + (int)groupId.y * chunksPerGroup;
    int chunkEnd = chunkBegin + chunksPerGroup;

    // 担当チャンク範囲のピクセル範囲(軸座標)。ピクセル中心基準の区画境界で、画像端でクリップする
    int rowStart = imgAxisMin + (int)ceil(chunkBegin * spanLength - 0.5);
    int rowEnd   = imgAxisMin + (int)ceil(chunkEnd * spanLength - 0.5);
    rowStart = max(rowStart, imgAxisMin);
    rowEnd   = min(rowEnd, imgAxisMax);

    // 恒等条件(そのままコピー)。UAVは対象矩形の全ピクセルへの書き込みが必須のため、
    // ソートを省く場合もコピーは行う
    bool identity = hi <= lo || spanLength < 2 || strength <= 0;
    bool active = lineCoord < outCrossMax && rowEnd > rowStart;

    uint count = (active && !identity) ? (uint)min(rowEnd - rowStart, MAX_SPAN) : 0;
    uint npow2 = count <= 1 ? 1 : (2u << firstbithigh(count - 1));

    if (active && identity)
    {
        [loop]
        for (uint e0 = 0; e0 < MAX_ELEMS; e0++)
        {
            uint i = tid + e0 * THREADS;
            if (i >= (uint)(rowEnd - rowStart))
                break;
            int a = rowStart + (int)i;
            if (a < outAxisMin || a >= outAxisMax)
                continue;
            int2 p = ScenePosition(vertical, lineCoord, a);
            OutputTexture[uint2(p + outputOffset.xy)] = SampleScene(p);
        }
    }

    // 1. ロード: 輝度16bitとソート対象フラグ
    [loop]
    for (uint e1 = 0; e1 < MAX_ELEMS; e1++)
    {
        uint i = tid + e1 * THREADS;
        if (i >= npow2)
            break;
        uint packed = 0;
        if (i < count)
        {
            float lum = Luminance(SampleScene(ScenePosition(vertical, lineCoord, rowStart + (int)i)));
            uint lum16 = min((uint)(saturate(lum) * 65535.0 + 0.5), 65535u);
            bool inSpan = (lum >= lo) && (lum <= hi);
            packed = (lum16 << 16) | (inSpan ? 1u : 0u);
        }
        keys[i] = packed;
    }
    GroupMemoryBarrierWithGroupSync();

    // 2. セグメント先頭ポインタの初期化。
    //    自分と直前が両方ソート対象で、かつ同一チャンクなら直前を指す
    [loop]
    for (uint e2 = 0; e2 < MAX_ELEMS; e2++)
    {
        uint i = tid + e2 * THREADS;
        if (i >= npow2)
            break;
        uint ptr = i;
        if (i > 0 && i < count)
        {
            int a = rowStart + (int)i;
            bool joined = (keys[i] & 1u) != 0 && (keys[i - 1] & 1u) != 0
                && ChunkIndex(a, imgAxisMin, spanLength) == ChunkIndex(a - 1, imgAxisMin, spanLength);
            ptr = joined ? i - 1 : i;
        }
        idxs[i] = ptr;
    }
    GroupMemoryBarrierWithGroupSync();

    // pointer jumpingでセグメント先頭indexへ収束させる(先頭は自分自身を指すため不動点になる)。
    // バリアを定数回にするためlog2(MAX_SPAN)=12回固定で回し、収束後の反復は実質no-op
    uint tmp[MAX_ELEMS];
    [loop]
    for (uint step = 1; step < MAX_SPAN; step <<= 1)
    {
        [loop]
        for (uint e3 = 0; e3 < MAX_ELEMS; e3++)
        {
            uint i = tid + e3 * THREADS;
            if (i >= npow2)
                break;
            tmp[e3] = idxs[idxs[i]];
        }
        GroupMemoryBarrierWithGroupSync();
        [loop]
        for (uint e4 = 0; e4 < MAX_ELEMS; e4++)
        {
            uint i = tid + e4 * THREADS;
            if (i >= npow2)
                break;
            idxs[i] = tmp[e4];
        }
        GroupMemoryBarrierWithGroupSync();
    }

    // 3. ソートキー構築。セグメント先頭が上位bitなのでセグメント間の順序は保たれ、
    //    セグメント外(単独セグメント)は元位置に留まる。パディングは最大値で末尾に沈める
    [loop]
    for (uint e5 = 0; e5 < MAX_ELEMS; e5++)
    {
        uint i = tid + e5 * THREADS;
        if (i >= npow2)
            break;
        if (i < count)
        {
            uint lum16 = keys[i] >> 16;
            uint lumKey = descending ? (65535u - lum16) : lum16;
            keys[i] = (idxs[i] << 16) | lumKey;
        }
        else
        {
            keys[i] = 0xFFFFFFFFu;
        }
        idxs[i] = i;
    }
    GroupMemoryBarrierWithGroupSync();

    // 4. bitonicソート(keys昇順、同値はidxs昇順=安定)。
    //    各ペアは若い側のindexを担当するスレッドだけが処理するため競合しない。
    //    バリア到達を全スレッドで揃えるためk/jは定数範囲で回し、実作業のみnpow2でスキップする
    [loop]
    for (uint k = 2; k <= MAX_SPAN; k <<= 1)
    {
        [loop]
        for (uint j = k >> 1; j > 0; j >>= 1)
        {
            if (k <= npow2)
            {
                [loop]
                for (uint e6 = 0; e6 < MAX_ELEMS; e6++)
                {
                    uint i = tid + e6 * THREADS;
                    if (i >= npow2)
                        break;
                    uint l = i ^ j;
                    if (l > i)
                    {
                        uint keyI = keys[i];
                        uint keyL = keys[l];
                        uint idxI = idxs[i];
                        uint idxL = idxs[l];
                        bool greater = (keyI > keyL) || (keyI == keyL && idxI > idxL);
                        bool ascending = (i & k) == 0;
                        if (greater == ascending)
                        {
                            keys[i] = keyL;
                            keys[l] = keyI;
                            idxs[i] = idxL;
                            idxs[l] = idxI;
                        }
                    }
                }
            }
            GroupMemoryBarrierWithGroupSync();
        }
    }

    // 5. 書き戻し: ソート後位置iには元index idxs[i]のピクセルが来る
    [loop]
    for (uint e7 = 0; e7 < MAX_ELEMS; e7++)
    {
        uint i = tid + e7 * THREADS;
        if (i >= count)
            break;
        int a = rowStart + (int)i;
        if (a < outAxisMin || a >= outAxisMax)
            continue;
        int2 p = ScenePosition(vertical, lineCoord, a);
        int2 sp = ScenePosition(vertical, lineCoord, rowStart + (int)idxs[i]);
        float4 orig = SampleScene(p);
        float4 sorted = SampleScene(sp);
        OutputTexture[uint2(p + outputOffset.xy)] = lerp(orig, sorted, strength);
    }
}

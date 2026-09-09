#include "ComputeGroup.hlsli"

#define VALID_LENGTH_SQUARED_THRESHOLD 0.25

// 平滑化のタイルは正方形で、一辺をグループの X 幅から導く。
#if GROUP_X != GROUP_Y
#error DirectionalColorKey smoothing tiles assume GROUP_X == GROUP_Y
#endif

#define SMOOTH_RADIUS 4
#define SMOOTH_TILE_SIZE (GROUP_X + SMOOTH_RADIUS * 2)
#define SMOOTH_TILE_COUNT (SMOOTH_TILE_SIZE * SMOOTH_TILE_SIZE)
#define SMOOTH_SPACE_TABLE_STRIDE (SMOOTH_RADIUS * 2 + 1)
#define SMOOTH_SPACE_TABLE_COUNT (SMOOTH_SPACE_TABLE_STRIDE * SMOOTH_SPACE_TABLE_STRIDE)

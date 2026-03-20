#pragma kernel grad

cbuffer TerrainDataBuffer : register(b0)
{
    float2 size;
    uint2 resolution;
}
Texture2D<float> heighmap : register(t0);

RWTexture2D<float2> Result : register(u0);

float height(int2 pos, float _default);

[numthreads(8,8,1)]
void grad (uint3 uv : SV_DispatchThreadID)
{
    if (uv.x >= resolution.x || uv.y >= resolution.y) return;
    float2 pos = float2(uv.xy) / resolution;
    float2 dxy = float2(1.0, 1.0) / resolution;

    float h = height(int2(uv.xy), 0);

    float hL = height(int2(uv.xy) - int2(1, 0), h);
    float hR = height(int2(uv.xy) + int2(1, 0), h);
    float hD = height(int2(uv.xy) - int2(0, 1), h);
    float hU = height(int2(uv.xy) + int2(0, 1), h);
    
    float2 gradient = float2(hR - hL, hU - hD) / (2.0 * dxy);

    Result[uv.xy] = gradient;
}

float height(int2 pos, float _default) {
    if (pos.x >= int(resolution.x) || pos.y >= int(resolution.y) || pos.x < 0 || pos.y < 0) return _default;

    return heighmap[pos];
}

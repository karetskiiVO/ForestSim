#pragma kernel init

RWTexture2D<float> Moisture : register(u0);
cbuffer TerrainDataBuffer : register(b0)
{
    uint2 resolution;
}

[numthreads(8,8,1)]
void init(uint3 uv : SV_DispatchThreadID)
{
    if (uv.x >= resolution.x || uv.y >= resolution.y) return;

    Moisture[uv.xy] = 0.3;
}
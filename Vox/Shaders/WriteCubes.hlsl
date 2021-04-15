#include "Compute.hlsl"

[numthreads(64, 1, 1)]
void main(uint3 DTid : SV_DispatchThreadID)
{
    uint curItem = ctr[0].nextReadIdx + DTid.x;

    if (curItem < ctr[0].readToIdx)
    {
        OctNode o = nodes[curItem];

        if ((o.flags & 1) == 0)
            return;

        uint n;
        InterlockedAdd(ctr[0].nextWriteIdx, 1, n);

        OctCube cube;
        float3 v0, v1;
        Loc_GetBox(o.l, v0, v1);
        
        cube.pos = float4((v0.x + v1.x) * 0.5,
        (v0.y + v1.y) * 0.5,
        (v0.z + v1.z) * 0.5,
        v1.x - v0.x);
        cubes[n] = cube;
    }
}



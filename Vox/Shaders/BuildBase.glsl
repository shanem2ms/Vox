#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

#include "Compute.glsl"

[numthreads(64, 1, 1)]
void main(uint3 DTid : SV_DispatchThreadID)
{
    uint curItem = ctr[0].nextReadIdx + DTid.x;

    if (curItem < ctr[0].readToIdx)
    {
        OctNode o = nodes[curItem];
        if ((o.flags & 1) == 1)
            return;

        for (int i = 0; i < 8; ++i)
        {
            o.child[i] = Oct_Create(Loc_GetChild(o.l, i), false, float3(0, 0, 0));
        }

        nodes[curItem] = o;
    }
}
  
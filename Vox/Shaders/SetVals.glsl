#include "Compute.glsl"

[numthreads(1, 1, 1)]
void main(uint3 DTid : SV_DispatchThreadID)
{
    ctr[0].nextReadIdx = ctr[0].readToIdx;
    ctr[0].readToIdx = ctr[0].nextWriteIdx;
}

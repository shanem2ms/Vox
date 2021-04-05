#include "Compute.hlsl"

[numthreads(1, 1, 1)]
void main(uint3 DTid : SV_DispatchThreadID)
{
    ctr[0].nextReadIdx = 0;
    ctr[0].readToIdx = ctr[0].nextWriteIdx;
    ctr[0].nextWriteIdx = 0;
}


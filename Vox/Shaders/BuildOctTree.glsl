#include "Compute.glsl"

[numthreads(64, 1, 1)]
void main(uint3 DTid : SV_DispatchThreadID)
{
    uint curItem = ctr[0].nextReadIdx + DTid.x;

    if (curItem < ctr[0].readToIdx)
    {
        OctNode o = nodes[curItem];        
        if ((o.flags & 1) == 1) return;

        uint levels = (ctr[0].xyzmask >> 4);
        for (int i = 0; i < 8; ++i)
        {
            Loc cl = Loc_GetChild(o.l, i);
            bool skipx = false; //(ctr[0].xyzmask & 1) == 0;
            bool skipy = true; //(ctr[0].xyzmask & 2) == 0;
            bool skipz = true; //(ctr[0].xyzmask & 4) == 0;
            float cx = 0;
            float cy = 0;
            float cz = 0;
            float3 v0, v1;
            bool lastLevel = (cl.lev == levels);
            Loc_GetBox(cl, v0, v1);
            int hrX = skipx ? eAllInside : IsHitX(v0, v1, Side0, Side1, cl.lev, cx);
            int hrY = skipy ? eAllInside : IsHitY(v0, v1, Side2, Side3, cl.lev, cy);
            int hrZ = skipz ? eAllInside : IsHitZ(v0, v1, Side4, Side5, cl.lev, cz);
            if ((hrX == eAllInside && hrY == eAllInside && hrZ == eAllInside)
                || (lastLevel && hrX != eAllOutside && hrY != eAllOutside &&
                hrZ != eAllOutside))
            {
                float3 c = float3(0,0,0);
                if (cx > 0)
                    c = Decode(cx);
                if (cy > 0)
                    c = Decode(cy);
                if (cz > 0)
                    c = Decode(cz);
                o.child[i] = Oct_Create(cl, true, c);
            }
            else if ((hrX == ePartial || hrY == ePartial || hrZ == ePartial) &&
                !lastLevel)
            {
                o.child[i] = Oct_Create(cl, false, float3(0,0,0));
            }
        }                                

        nodes[curItem] = o;
    }
}

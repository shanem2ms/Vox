#version 450
#extension GL_KHR_shader_subgroup_ballot: enable
#extension GL_KHR_shader_subgroup_arithmetic: enable
#extension GL_EXT_samplerless_texture_functions: enable
//!#extension GL_KHR_vulkan_glsl: enable
//! #define texture2D sampler2D


layout(set = 0, binding = 0) uniform texture2D Side0;
layout(set = 0, binding = 1) uniform texture2D Side1;
layout(set = 0, binding = 2) uniform texture2D Side2;
layout(set = 0, binding = 3) uniform texture2D Side3;
layout(set = 0, binding = 4) uniform texture2D Side4;
layout(set = 0, binding = 5) uniform texture2D Side5;


layout(std140, set=1, binding=0) buffer Ctr {
  uint nextReadIdx;
  uint nextWriteIdx;
  uint readToIdx;
  uint xyzmask;
};

struct Loc
{
    uint lev;
    uint x;
    uint y;
    uint z;    
};

/*
uint lev
uint x
uint y
uint z
uint child[8]
uint flags
uint color
*/
struct OctNode
{
    Loc l;
    uint child[8];
    uint flags;
    uint color;
};

layout(std430, set = 1, binding = 1) buffer OctTree
{
    OctNode nodes[];
};


Loc Loc_GetChild(Loc p, uint i)
{
    Loc lc;
    lc.lev = p.lev + 1;
    lc.x = p.x * 2 + (i & 1);
    lc.y = p.y * 2 + ((i >> 1) & 1);
    lc.z = p.z * 2 + ((i >> 2) & 1);
    return lc;
}

vec3[2] Loc_GetBox(Loc l)
{
    float scale = 1.0f / float(1 << l.lev);
    vec3 vr[2] = { vec3(l.x * scale, l.y * scale, l.z * scale),
        vec3((l.x + 1) * scale, (l.y + 1) * scale, (l.z + 1) * scale)};
    return vr;
}


const int eAllInside = 1;
const int eAllOutside = 2;
const int ePartial = 3;

int IsHit(vec2 v, float nearz, float farz, texture2D near, texture2D far, uint lod, out float c)
{
    int size = 1 << lod;
    int w = int((v.x) * (size - 1));
    int h = int((v.y) * (size - 1));


    vec4 nearcolor = texelFetch(near, ivec2(w, h), 0);
    vec4 farcolor = texelFetch(far, ivec2(w, h), 0);
    float nearmax = nearcolor.r;
    float nearmin = nearcolor.g;
    float farmax = farcolor.r;
    float farmin = farcolor.g;
    if (nearz >= farmax ||
        farz <= nearmin)
    {
        c = 0;
        return eAllOutside;
    }

    float a0 = nearcolor.b;
    c = a0 > 0 ? nearcolor.b : farcolor.b;
    if (nearz > nearmax && farz < farmin)
    {
        return eAllInside; 
    }

    return ePartial;
}


int IsHitX(vec3 mm[2], texture2D sides0, texture2D sides1, uint lod, out float c)
{
    return IsHit(
            vec2((mm[0].x + mm[1].x) * 0.5f,
            (mm[0].y + mm[1].y) * 0.5f),
    mm[0].z, mm[1].z,
    sides0,
    sides1,
    lod,
    c);
}

int IsHitY(vec3 mm[2], texture2D sides2, texture2D sides3, uint lod, out float c)
{
    return IsHit(
            vec2((mm[0].x + mm[1].x) * 0.5f,
            1 - ((mm[0].z + mm[1].z) * 0.5f)),
    mm[0].y, mm[1].y,
    sides2,
    sides3,
    lod,
    c);
}

int IsHitZ(vec3 mm[2], texture2D sides4, texture2D sides5, uint lod, out float c)
{
    return IsHit(
            vec2((mm[0].z + mm[1].z) * 0.5f,
            (mm[0].y + mm[1].y) * 0.5f),
    1 - mm[1].x, 1 - mm[0].x,
    sides4,
    sides5,
    lod,
    c);
}


uint Oct_Create(Loc l, bool isLeaf, vec3 c)
{
    uint n = atomicAdd(nextWriteIdx, 1);
    OctNode newnode;
    newnode.l = l;
    newnode.flags = isLeaf ? 1 : 0;
    nodes[n] = newnode;
    return n;
}

vec3 Decode(float c)
{
    return vec3(1,1,1);
}

#if BUILDBASEOCT
layout(local_size_x = 64, local_size_y = 1, local_size_z = 1) in;
void main()
{
    uint curItem = nextReadIdx + gl_GlobalInvocationID.x;

    if (curItem < readToIdx)
    {
        OctNode o = nodes[curItem];        
        if ((o.flags & 1) == 1) return;

        for (int i = 0; i < 8; ++i)
        {
            o.child[i] = Oct_Create(Loc_GetChild(o.l, i), false, vec3(0,0,0));
        }

        nodes[curItem] = o;
    }
}
#endif

#if BUILDOCT
layout(local_size_x = 64, local_size_y = 1, local_size_z = 1) in;
void main()
{
    uint curItem = nextReadIdx + gl_GlobalInvocationID.x;

    if (curItem < readToIdx)
    {
        OctNode o = nodes[curItem];        
        if ((o.flags & 1) == 1) return;

        bool lastLevel = (o.flags & 4) == 4;
        for (int i = 0; i < 8; ++i)
        {
            Loc cl = Loc_GetChild(o.l, i);
            bool skipx = (xyzmask & 1) == 0;
            bool skipy = (xyzmask & 2) == 0;
            bool skipz = (xyzmask & 4) == 0;
            float cx = 0;
            float cy = 0;
            float cz = 0;
            int hrX = skipx ? eAllInside : IsHitX(Loc_GetBox(cl), Side0, Side1, cl.lev, cx);
            int hrY = skipy ? eAllInside : IsHitY(Loc_GetBox(cl), Side2, Side3, cl.lev, cy);
            int hrZ = skipz ? eAllInside : IsHitZ(Loc_GetBox(cl), Side4, Side5, cl.lev, cz);
            if ((hrX == eAllInside && hrY == eAllInside && hrZ == eAllInside)
                || (lastLevel && hrX != eAllOutside && hrY != eAllOutside &&
                hrZ != eAllOutside))
            {
                vec3 c = vec3(0,0,0);
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
                o.child[i] = Oct_Create(cl, false, vec3(0,0,0));
            }
        }                                

        nodes[curItem] = o;
    }
}
#endif

#if SETVALS
layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;
void main()
{
    nextReadIdx = readToIdx;
    readToIdx = nextWriteIdx;
}
#endif


#if ZEROVALS
layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;
void main()
{
    nextReadIdx = readToIdx;
    readToIdx = nextWriteIdx;
    nextWriteIdx = 0;
}
#endif


#if WRITECUBES
layout(local_size_x = 64, local_size_y = 1, local_size_z = 1) in;
void main()
{
    nextReadIdx = readToIdx;
    readToIdx = nextWriteIdx;
}
#endif
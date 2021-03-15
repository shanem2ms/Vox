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
  uint nextItem;
};

struct OctNode
{
    uint child[8];
    vec4 data1;
    vec4 data2;
};

layout(std140, set = 1, binding = 1) buffer OctTree
{
    OctNode nodes[];
};


struct OctLoc
{
    int x;    
};


layout(local_size_x = 4, local_size_y = 4, local_size_z = 4) in;

void main()
{
    //vec4 val = texelFetch(Side0, ivec2(gl_GlobalInvocationID.xy), 0);
    uint curItem = atomicAdd(nextItem, 1);
    for (int i = 0; i < 8; i++) { nodes[curItem].child[i] = curItem; }
}

const int eAllInside = 1;
const int eAllOutside = 2;
const int ePartial = 3;

int IsHit(vec2 v, float nearz, float farz, texture2D near, texture2D far, int lod, out float c)
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



int IsHitX(vec3 mm0, vec3 mm1, texture2D sides[6], int lod, out float c)
{
    return IsHit(
            vec2((mm0.x + mm1.x) * 0.5f,
            (mm0.y + mm1.y) * 0.5f),
    mm0.z, mm1.z,
    sides[0],
    sides[1],
    lod,
    c);
}

int IsHitY(vec3 mm0, vec3 mm1, texture2D sides[6], int lod, out float c)
{
    return IsHit(
            vec2((mm0.x + mm1.x) * 0.5f,
            1 - ((mm0.z + mm1.z) * 0.5f)),
    mm0.y, mm1.y,
    sides[2],
    sides[3],
    lod,
    c);
}

int IsHitZ(vec3 mm0, vec3 mm1, texture2D sides[6], int lod, out float c)
{
    return IsHit(
            vec2((mm0.z + mm1.z) * 0.5f,
            (mm0.y + mm1.y) * 0.5f),
    1 - mm1.x, 1 - mm0.x,
    sides[4],
    sides[5],
    lod,
    c);
}

#version 450
#extension GL_KHR_shader_subgroup_ballot: enable
#extension GL_KHR_shader_subgroup_arithmetic: enable
#extension GL_EXT_samplerless_texture_functions: enable


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

layout(local_size_x = 4, local_size_y = 4, local_size_z = 4) in;

void main()
{
    //vec4 val = texelFetch(Side0, ivec2(gl_GlobalInvocationID.xy), 0);
    uint curItem = atomicAdd(nextItem, 1);
    for (int i = 0; i < 8; i++) { nodes[curItem].child[i] = curItem; }
}

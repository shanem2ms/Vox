#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable
struct Vox_Shaders_Depth_Transform
{
    mat4 Projection;
    mat4 View;
    mat4 Model;
    vec4 LightPos;
};

struct Vox_Shaders_Depth_Material
{
    vec4 DiffuseColor;
};

layout(set = 0, binding = 0) uniform t
{
    Vox_Shaders_Depth_Transform Transform;
};

layout(location = 0) in vec3 Position;
layout(location = 1) in vec2 UV;
layout(location = 2) in vec3 Color;
layout(location = 3) in vec3 Normal;
layout(location = 0) out vec2 OutDepth;
layout(location = 1) out vec3 OutVtxColor;

void main()
{
    vec4 v4Pos = vec4(Position, 1);
    OutVtxColor = Color;
    vec4 outPos = (Transform.Projection * Transform.View * Transform.Model) * v4Pos;
    gl_Position = outPos;
    OutDepth = vec2(outPos.z, outPos.w);
    gl_Position.y = -gl_Position.y; // Correct for Vulkan clip coordinates
}

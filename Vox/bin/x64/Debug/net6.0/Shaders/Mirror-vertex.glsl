#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable
struct Vox_Shaders_Mirror_VertexInput
{
    vec3 Position;
    vec2 UV;
    vec3 Color;
    vec3 Normal;
};

struct Vox_Shaders_Mirror_FragmentInput
{
    vec4 Position;
    vec3 fsNormal;
    vec3 fsColor;
    vec3 fsEyePos;
    vec3 fsLightVec;
};

layout(set = 0, binding = 0) uniform Projection
{
    mat4 field_Projection;
};

layout(set = 0, binding = 1) uniform View
{
    mat4 field_View;
};

layout(set = 0, binding = 2) uniform Model
{
    mat4 field_Model;
};

layout(set = 0, binding = 3) uniform LightPos
{
    vec4 field_LightPos;
};


Vox_Shaders_Mirror_FragmentInput VS( Vox_Shaders_Mirror_VertexInput input_)
{
    Vox_Shaders_Mirror_FragmentInput output_;
    vec4 v4Pos = vec4(input_.Position, 1);
    output_.fsNormal = input_.Normal;
    output_.fsColor = input_.Color;
    output_.Position = (field_Projection * field_View * field_Model) * v4Pos;
    vec4 eyePos = field_View * field_Model * v4Pos;
    output_.fsEyePos = vec3(eyePos.x, eyePos.y, eyePos.z);
    vec4 eyeLightPos = field_View * field_LightPos;
    output_.fsLightVec = normalize(vec3(field_LightPos.x, field_LightPos.y, field_LightPos.z) - output_.fsEyePos);
    return output_;
}


layout(location = 0) in vec3 Position;
layout(location = 1) in vec2 UV;
layout(location = 2) in vec3 Color;
layout(location = 3) in vec3 Normal;
layout(location = 0) out vec3 fsin_0;
layout(location = 1) out vec3 fsin_1;
layout(location = 2) out vec3 fsin_2;
layout(location = 3) out vec3 fsin_3;

void main()
{
    Vox_Shaders_Mirror_VertexInput input_;
    input_.Position = Position;
    input_.UV = UV;
    input_.Color = Color;
    input_.Normal = Normal;
    Vox_Shaders_Mirror_FragmentInput output_ = VS(input_);
    fsin_0 = output_.fsNormal;
    fsin_1 = output_.fsColor;
    fsin_2 = output_.fsEyePos;
    fsin_3 = output_.fsLightVec;
    gl_Position = output_.Position;
        gl_Position.y = -gl_Position.y; // Correct for Vulkan clip coordinates
}

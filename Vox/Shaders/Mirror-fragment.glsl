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


vec4 FS( Vox_Shaders_Mirror_FragmentInput input_)
{
    return vec4(input_.fsColor, 1);
}


layout(location = 0) in vec3 fsin_0;
layout(location = 1) in vec3 fsin_1;
layout(location = 2) in vec3 fsin_2;
layout(location = 3) in vec3 fsin_3;
layout(location = 0) out vec4 _outputColor_;

void main()
{
    Vox_Shaders_Mirror_FragmentInput input_;
    input_.Position = gl_FragCoord;
    input_.fsNormal = fsin_0;
    input_.fsColor = fsin_1;
    input_.fsEyePos = fsin_2;
    input_.fsLightVec = fsin_3;
    vec4 output_ = FS(input_);
    _outputColor_ = output_;
}

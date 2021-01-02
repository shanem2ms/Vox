#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable
struct Vox_Shaders_Blit_Transform
{
    mat4 MWP;
};

struct Vox_Shaders_Blit_VertexInput
{
    vec3 Position;
    vec3 UVW;
};

struct Vox_Shaders_Blit_FragmentInput
{
    vec4 Position;
    vec2 fsUV;
};

layout(set = 0, binding = 0) uniform texture2D Texture;
layout(set = 0, binding = 1) uniform sampler Sampler;

vec4 FS( Vox_Shaders_Blit_FragmentInput input_)
{
    return texture(sampler2D(Texture, Sampler), input_.fsUV);
}


layout(location = 0) in vec2 fsin_0;
layout(location = 0) out vec4 _outputColor_;

void main()
{
    Vox_Shaders_Blit_FragmentInput input_;
    input_.Position = gl_FragCoord;
    input_.fsUV = fsin_0;
    vec4 output_ = FS(input_);
    _outputColor_ = output_;
}

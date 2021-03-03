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

layout(set = 0, binding = 2) uniform t
{
    Vox_Shaders_Blit_Transform field_t;
};


Vox_Shaders_Blit_FragmentInput VS( Vox_Shaders_Blit_VertexInput input_)
{
    Vox_Shaders_Blit_FragmentInput output_;
    output_.Position = field_t.MWP * vec4(input_.Position, 1);
    output_.fsUV = vec2(input_.UVW.x, input_.UVW.y);
    return output_;
}


layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 UVW;
layout(location = 0) out vec2 fsin_0;

void main()
{
    Vox_Shaders_Blit_VertexInput input_;
    input_.Position = Position;
    input_.UVW = UVW;
    Vox_Shaders_Blit_FragmentInput output_ = VS(input_);
    fsin_0 = output_.fsUV;
    gl_Position = output_.Position;
        gl_Position.y = -gl_Position.y; // Correct for Vulkan clip coordinates
}

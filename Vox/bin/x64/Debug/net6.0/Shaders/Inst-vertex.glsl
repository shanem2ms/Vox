#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable
struct Vox_Shaders_Inst_Transform
{
    mat4 Projection;
    mat4 View;
    mat4 Model;
    vec4 LightPos;
};

struct Vox_Shaders_Inst_VertexInput
{
    vec3 Position;
    vec3 Normal;
    vec4 InstData0;
};

struct Vox_Shaders_Inst_FragmentInput
{
    vec4 Position;
    vec3 fsNormal;
    vec3 fsColor;
    vec3 fsEyePos;
    vec3 fsLightVec;
};

layout(set = 0, binding = 0) uniform t
{
    Vox_Shaders_Inst_Transform field_t;
};


Vox_Shaders_Inst_FragmentInput VS( Vox_Shaders_Inst_VertexInput input_)
{
    Vox_Shaders_Inst_FragmentInput output_;
    vec4 v4Pos = vec4(input_.Position * input_.InstData0.w * 0.45f + vec3(input_.InstData0.x, input_.InstData0.y, input_.InstData0.z), 1);
    output_.fsNormal = input_.Normal;
    output_.fsColor = vec3(1, 1, 1);
    output_.Position = (field_t.Projection * field_t.View * field_t.Model) * v4Pos;
    vec4 eyePos = field_t.View * field_t.Model * v4Pos;
    output_.fsEyePos = vec3(eyePos.x, eyePos.y, eyePos.z);
    vec4 eyeLightPos = field_t.View * field_t.LightPos;
    output_.fsLightVec = normalize(vec3(field_t.LightPos.x, field_t.LightPos.y, field_t.LightPos.z) - output_.fsEyePos);
    return output_;
}


layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 Normal;
layout(location = 2) in vec4 InstData0;
layout(location = 0) out vec3 fsin_0;
layout(location = 1) out vec3 fsin_1;
layout(location = 2) out vec3 fsin_2;
layout(location = 3) out vec3 fsin_3;

void main()
{
    Vox_Shaders_Inst_VertexInput input_;
    input_.Position = Position;
    input_.Normal = Normal;
    input_.InstData0 = InstData0;
    Vox_Shaders_Inst_FragmentInput output_ = VS(input_);
    fsin_0 = output_.fsNormal;
    fsin_1 = output_.fsColor;
    fsin_2 = output_.fsEyePos;
    fsin_3 = output_.fsLightVec;
    gl_Position = output_.Position;
        gl_Position.y = -gl_Position.y; // Correct for Vulkan clip coordinates
}

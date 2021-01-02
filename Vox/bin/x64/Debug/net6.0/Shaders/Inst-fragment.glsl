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
    vec4 InstData1;
};

struct Vox_Shaders_Inst_FragmentInput
{
    vec4 Position;
    vec3 fsNormal;
    vec3 fsColor;
    vec3 fsEyePos;
    vec3 fsLightVec;
};

vec3 Vox_Shaders_Inst_Reflect( vec3 i,  vec3 n)
{
    return i - 2 * n * dot(i, n);
}



vec4 FS( Vox_Shaders_Inst_FragmentInput input_)
{
    vec3 eye = normalize(-input_.fsEyePos);
    vec3 nrm = normalize(input_.fsNormal);
    vec3 reflected = normalize(Vox_Shaders_Inst_Reflect(-input_.fsLightVec, nrm));
    float diff = clamp(dot(nrm, input_.fsLightVec), 0, 1);
    float ambient = 0.2f;
    float specular = 0.75f;
    vec4 specvec = vec4(0, 0, 0, 0);
    if (dot(input_.fsEyePos, nrm) < 0)
{
    specvec = vec4(0.5f, 0.5f, 0.5f, 1.0f) * float(pow(clamp(dot(reflected, eye), 0, 100000), 16.0f)) * specular;
}



    float mul = ambient + (1 - ambient) * diff;
    return (vec4(input_.fsColor, 1) * mul) + specvec;
}


layout(location = 0) in vec3 fsin_0;
layout(location = 1) in vec3 fsin_1;
layout(location = 2) in vec3 fsin_2;
layout(location = 3) in vec3 fsin_3;
layout(location = 0) out vec4 _outputColor_;

void main()
{
    Vox_Shaders_Inst_FragmentInput input_;
    input_.Position = gl_FragCoord;
    input_.fsNormal = fsin_0;
    input_.fsColor = fsin_1;
    input_.fsEyePos = fsin_2;
    input_.fsLightVec = fsin_3;
    vec4 output_ = FS(input_);
    _outputColor_ = output_;
}

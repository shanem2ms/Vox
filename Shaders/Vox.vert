#version 150 compatibility
uniform mat4 uMVP;
uniform mat4 uWorldInvTranspose;
uniform mat4 uWorld;
uniform mat4 uCamMat;
in vec3 aPosition;
in vec3 aNormal;
in vec4 aInstData0;
in vec4 aInstData1;
out vec3 vColor;
out vec3 vWsPos;
out vec3 vNormal;
out vec4 vVox;
void main() {
    vec4 ipos = vec4(aPosition * aInstData0.w + aInstData0.xyz, 1.0);
    gl_Position = uMVP * ipos;
    vec3 norm = aNormal;
    vWsPos = (uWorld * ipos).xyz;
    vColor = aInstData1.xyz;
    vVox = aInstData0;
    vNormal = normalize(norm.xyz);
}

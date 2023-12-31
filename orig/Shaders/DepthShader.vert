#version 150 compatibility
uniform mat4 uMVP;
in vec3 aPosition;
in vec3 aTexCoord0;
in vec3 aNormal;
out vec2 vDepth;
out highp vec3 vColor;
void main() {
    vec4 pspos = uMVP * vec4(aPosition, 1.0);
    vDepth = pspos.zw;
    vColor = aTexCoord0;
    gl_Position = pspos; 
}

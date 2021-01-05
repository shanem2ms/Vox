#version 150 compatibility
uniform mat4 uMVP;
in vec3 aPosition;
in vec3 aTexCoord0;
out vec3 vTexCoord;
void main() {
    gl_Position = uMVP * vec4(aPosition, 1.0);
    vTexCoord = aTexCoord0;
}
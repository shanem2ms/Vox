#version 150 compatibility
uniform vec4 offsetScale;
in vec3 aPosition;
in vec3 aTexCoord0;
out vec2 vTexCoord;
void main() {
    gl_Position = vec4(aPosition.x * offsetScale.z + offsetScale.x, 
            aPosition.y * offsetScale.w + offsetScale.y, 
            0.5, 1.0);
    vTexCoord = aTexCoord0.xy;
}

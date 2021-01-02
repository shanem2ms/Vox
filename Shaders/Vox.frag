#version 150 compatibility
uniform float ambient;
uniform vec3 lightPos;
uniform float opacity;

in vec3 vNormal;
in vec3 vWsPos;
in vec3 vColor;
void main()
{
	vec3 lightVec = normalize(vWsPos - lightPos);
	float lit = abs(dot(lightVec, vNormal));
	gl_FragColor = vec4(vColor * (lit * (1 - ambient) + ambient), 1) * opacity;
}
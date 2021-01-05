#version 150 compatibility
in vec2 vDepth;
in highp vec3 vColor;

void main()
{
	float v = ((vDepth.x / vDepth.y) + 1) / 2;
	float c = vColor.r + floor(vColor.g * 256) + floor(vColor.b * 256) * 256;
	gl_FragColor = vec4(v,1,c,1);
}
#version 150 compatibility
in vec2 vDepth;
void main()
{
	float v = ((vDepth.x / vDepth.y) + 1) / 2;
	gl_FragColor = vec4(v,v,v,v);
}
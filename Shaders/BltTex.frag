#version 150 compatibility
in vec2 vTexCoord;
uniform sampler2D texSampler;            

void main()
{
	gl_FragColor =  texture2D(texSampler, vTexCoord);
}
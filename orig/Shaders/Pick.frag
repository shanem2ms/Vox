#version 150 compatibility
uniform vec4 pickColor;
in vec4 vVox;
void main()
{
	gl_FragColor = vVox;
}
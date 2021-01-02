#version 150 compatibility
in vec3 vTexCoord;
uniform vec2 depthScale;
uniform vec2 depthOffset;
uniform sampler2D ySampler;
uniform sampler2D uvSampler;
uniform highp sampler2D depthSampler;
uniform highp sampler2D markerTex;
             
uniform float imageDepthMix = 0;
uniform vec2 depthVals;              
uniform bool hasDepth;
void main()
{
	vec3 yuv;
	vec2 imgTexCoord = vTexCoord.xy;
	//imgTexCoord.y = 1 - imgTexCoord.y;
	
	yuv.x = texture2D(ySampler, imgTexCoord).r;
	yuv.yz = vec2(0,0);//texture2D(uvSampler, imgTexCoord).rg - vec2(0.5, 0.5);
	vec3 rgb = mat3(1,1,1,  
			0, -0.18732, 1.8556,  
			1.57481, -0.46813, 0) * yuv;
	vec4 imgcolor = vec4(rgb,1);

	float dv = texture2D(depthSampler, imgTexCoord).r;
	if (isnan(dv) || dv == 0 || isinf(dv)) 
		dv = 0;
	else
		dv = (dv - depthVals.x) / (depthVals.y - depthVals.x);

	vec3 rgbdepth = vec3(dv, dv, dv);
	gl_FragColor = imgcolor;//vec4(rgbdepth,1);
}
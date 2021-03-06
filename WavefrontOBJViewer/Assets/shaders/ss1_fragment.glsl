﻿// Copyright(C) David W. Jeske, 2013
// Released to the public domain. Use, modify and relicense at will.

#version 120


uniform sampler2D diffTex;
uniform sampler2D specTex;
uniform sampler2D ambiTex;
uniform sampler2D bumpTex;

uniform int showWireframes;
uniform float animateSecondsOffset;

// eye-space/cameraspace coordinates
varying vec3 f_VV;
varying vec3 f_vertexNormal;
varying vec3 f_lightPosition;
varying vec3 f_eyeVec;
varying vec3 f_vertexPosition_objectspace;

// http://www.clockworkcoders.com/oglsl/tutorial5.htm

float rand(vec2 co){
    return fract(sin(dot(co.xy ,vec2(12.9898,78.233))) * 43758.5453);
}

    // http://www.ozone3d.net/tutorials/bump_mapping_p4.php

void main()
{
	vec4 outputColor = vec4(0.0);

	// lighting strength
	vec4 ambientStrength = gl_FrontMaterial.ambient;
	vec4 diffuseStrength = gl_FrontMaterial.diffuse;
	vec4 specularStrength = gl_FrontMaterial.specular;
	// specularStrength = vec4(0.7,0.4,0.4,0.0);  // test red
	
	// load texels...
	vec4 ambientColor = texture2D (diffTex, gl_TexCoord[0].st);
	vec4 diffuseColor = texture2D (diffTex, gl_TexCoord[0].st);
	vec4 glowColor = texture2D (ambiTex, gl_TexCoord[0].st);
	vec4 specTex = texture2D (specTex, gl_TexCoord[0].st);

	
	// eye space shading
	outputColor = ambientColor * ambientStrength;
	outputColor += glowColor * gl_FrontMaterial.emission;
	
	float diffuseIllumination = clamp(dot(f_vertexNormal, f_lightPosition), 0, 1);
	// boost the diffuse color by the glowmap .. poor mans bloom
	float glowFactor = length(gl_FrontMaterial.emission.xyz) * 0.2;
	outputColor += diffuseColor * max(diffuseIllumination, glowFactor);
	
	// compute specular lighting
	if (dot(f_vertexNormal, f_lightPosition) > 0.0) {   // if light is front of the surface
	
	  vec3 R = reflect(-normalize(f_lightPosition), normalize(f_vertexNormal));
	  float shininess = pow (max (dot(R, normalize(f_eyeVec)), 0.0), gl_FrontMaterial.shininess);
	
	  // outputColor += specularStrength * shininess;
	  outputColor += specTex * specularStrength * shininess;      
	} 
       
	// finally, output the fragment color
    gl_FragColor = outputColor;    
}			

	
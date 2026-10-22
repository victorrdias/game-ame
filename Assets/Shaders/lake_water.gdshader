shader_type spatial;
render_mode cull_disabled, diffuse_lambert, specular_disabled;

uniform vec4 deep_color : source_color = vec4(0.18, 0.46, 0.52, 1.0);
uniform vec4 crest_color : source_color = vec4(0.72, 0.88, 0.86, 1.0);
uniform float amplitude = 0.045;
uniform float speed = 0.55;

varying float crest;

void vertex() {
	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float wave = sin(world.x * 1.35 + TIME * speed) * 0.55;
	wave += sin(world.z * 1.7 - TIME * speed * 0.8) * 0.3;
	wave += sin((world.x + world.z) * 0.72 + TIME * speed * 1.15) * 0.15;
	crest = wave;
	VERTEX.y += wave * amplitude;
}

void fragment() {
	float band = clamp(crest * 0.55 + 0.5, 0.0, 1.0);
	ALBEDO = mix(deep_color.rgb, crest_color.rgb, band);
	float spark = smoothstep(0.78, 1.0, sin(crest * 4.0 + TIME * 1.2) * 0.5 + 0.5);
	EMISSION = crest_color.rgb * spark * 0.16;
	ROUGHNESS = 0.78;
}

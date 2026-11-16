// Still, dark water: almost black body colour, mirror-like surface with two slow counter-drifting ripple layers so
// torches and the moon smear across it. COLOR.r = 1 at the bank, 0 in open water (shallows are lighter and murkier).
// An optional faint glow carries the region accent (toxic shimmer in the mire).
shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx, shadows_disabled;

uniform sampler2D wave_nrm : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform vec3 deep_color : source_color = vec3(0.008, 0.012, 0.016);
uniform vec3 shallow_color : source_color = vec3(0.04, 0.05, 0.045);
uniform float roughness = 0.07;
uniform float wave_scale = 0.11;
uniform float wave_strength = 0.55;
uniform vec2 flow = vec2(0.012, 0.007);
uniform vec3 glow : source_color = vec3(0.0);
uniform float glow_energy = 0.0;

varying vec3 wpos;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	TANGENT = vec3(1.0, 0.0, 0.0);
	BINORMAL = vec3(0.0, 0.0, -1.0);
}

void fragment() {
	float t = TIME;
	vec3 n1 = texture(wave_nrm, wpos.xz * wave_scale + flow * t).rgb;
	vec3 n2 = texture(wave_nrm, wpos.zx * wave_scale * 1.7 - flow.yx * t * 1.3 + vec2(0.5, 0.23)).rgb;
	float scum = texture(noise_tex, wpos.xz * 0.06 + flow * t * 0.4).r;
	float bank = clamp(COLOR.r * 1.3 + (scum - 0.5) * 0.5 * COLOR.r, 0.0, 1.0);

	ALBEDO = mix(deep_color, shallow_color, bank);
	ROUGHNESS = mix(roughness, roughness + 0.25, bank * scum);
	METALLIC = 0.0;
	SPECULAR = 0.35;
	NORMAL_MAP = mix(n1, n2, 0.5);
	NORMAL_MAP_DEPTH = wave_strength;
	float shimmer = smoothstep(0.55, 0.8, scum) * (0.6 + 0.4 * sin(t * 0.7 + scum * 9.0));
	EMISSION = glow * glow_energy * (0.25 + shimmer) * (0.4 + 0.6 * bank);
}

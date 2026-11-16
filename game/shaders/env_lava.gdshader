// Lava: two warped, counter-scrolling layers of molten rock under a cooling crust that gathers along the banks
// (COLOR.r = 1 at the shore, 0 mid-stream) and drifts in slow islands. Strongly emissive so it feeds the glow pass;
// the light it throws on its surroundings comes from the omni lights the level builder scatters along the flow.
shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx, shadows_disabled;

uniform sampler2D molten_alb : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D molten_em : source_color, hint_default_black, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D crust_alb : source_color, hint_default_black, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D crust_em : source_color, hint_default_black, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D crust_nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float uv_scale = 0.5;
uniform vec2 flow = vec2(0.035, 0.014);       // uv units per second
uniform float energy = 4.0;
uniform vec3 glow_color : source_color = vec3(1.0, 0.36, 0.08);
uniform float procedural = 0.25;              // share of the noise-driven glow (1 when the emission maps are missing)

varying vec3 wpos;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	TANGENT = vec3(1.0, 0.0, 0.0);
	BINORMAL = vec3(0.0, 0.0, -1.0);
}

void fragment() {
	float t = TIME;
	vec2 p = wpos.xz * uv_scale;
	float w1 = texture(noise_tex, wpos.xz * 0.045 + vec2(t * 0.012, t * 0.008)).r;
	float w2 = texture(noise_tex, wpos.zx * 0.061 - vec2(t * 0.009, t * 0.013) + vec2(0.37, 0.71)).r;
	vec2 warp = (vec2(w1, w2) - 0.5) * 0.32;

	vec2 uva = p + flow * t + warp;
	vec2 uvb = p * 0.73 - flow.yx * t * 0.8 - warp * 0.7 + vec2(0.41, 0.17);
	vec3 em_a = texture(molten_em, uva).rgb;
	vec3 em_b = texture(molten_em, uvb).rgb;
	vec3 al_a = texture(molten_alb, uva).rgb;

	float veins = smoothstep(0.42, 0.78, w1 * 0.62 + w2 * 0.62);
	vec3 molten = em_a * 0.65 + em_b * 0.55 + glow_color * (0.25 + veins * 1.1) * procedural;

	float isl = smoothstep(0.6, 0.74, texture(noise_tex, wpos.xz * 0.021 + vec2(t * 0.002, 0.0)).r);
	float crust = clamp(COLOR.r * 1.45 + (w2 - 0.5) * 0.9 - 0.12, 0.0, 1.0);
	crust = max(crust, isl * 0.85);

	vec2 uvc = p * 1.1 + flow * t * 0.22;
	vec3 cr_al = texture(crust_alb, uvc).rgb;
	vec3 cr_em = texture(crust_em, uvc).rgb + glow_color * veins * 0.35 * procedural;
	vec3 cr_nm = texture(crust_nrm, uvc).rgb;

	float pulse = 0.86 + 0.14 * sin(t * 0.9 + w1 * 12.0);
	ALBEDO = mix(al_a * 0.45, cr_al * 0.55, crust);
	ROUGHNESS = mix(0.42, 0.86, crust);
	METALLIC = 0.0;
	NORMAL_MAP = cr_nm;
	NORMAL_MAP_DEPTH = crust;
	EMISSION = mix(molten, cr_em * 0.7, crust) * energy * pulse;
}

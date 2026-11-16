// Imported environment props (glTF PBR materials converted at load): theme grading, settled snow / ash / moss on
// up-facing surfaces, a little per-instance value variation, gentle sway for plants, and the same dithered
// see-through disc as the walls so tall props (trees, statues, columns) never hide the hero.
shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx;

global uniform vec3 hero_pos;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec4 albedo : source_color = vec4(1.0);
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float normal_scale = 1.0;
uniform sampler2D orm_tex : hint_default_white, filter_linear_mipmap, repeat_enable;   // r = ao, g = roughness, b = metallic
uniform float roughness = 0.8;
uniform float metallic = 0.0;
uniform float ao_amount = 0.0;
uniform sampler2D emission_tex : source_color, hint_default_black, filter_linear_mipmap, repeat_enable;
uniform vec3 emission : source_color = vec3(0.0);
uniform float emission_energy = 0.0;
uniform float alpha_cut = 0.0;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform vec3 tint = vec3(1.0);               // linear multiplier (theme grading)
uniform float desaturate : hint_range(0.0, 1.0) = 0.0;
uniform vec3 top_color : source_color = vec3(1.0);
uniform float top_amount = 0.0;
uniform float sway = 0.0;                    // metres of wind sway at 1 m above the prop origin
uniform float cut_radius = 2.6;
uniform float cut_floor = 1.1;               // props shorter than this are never cut

varying vec3 wpos;
varying vec3 wnrm;
varying float vary;

void vertex() {
	vec3 origin = MODEL_MATRIX[3].xyz;
	vary = fract(sin(dot(origin.xz, vec2(12.9898, 78.233))) * 43758.5453);
	// SWAY_BEGIN (the level builder strips this block for everything that is not a plant: a vertex shader that reads
	// TIME marks its meshes as animated, and animated meshes force every shadow map they touch to be redrawn each frame)
	if (sway > 0.0) {
		float h = max(VERTEX.y, 0.0);
		float ph = TIME * (0.9 + vary * 0.6) + origin.x * 0.35 + origin.z * 0.27;
		VERTEX.x += sin(ph) * sway * h;
		VERTEX.z += cos(ph * 0.83 + 1.7) * sway * h * 0.7;
	}
	// SWAY_END
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wnrm = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

float see_through(vec3 wp, vec3 cam) {
	vec3 target = hero_pos + vec3(0.0, 1.0, 0.0);
	vec3 ch = target - cam;
	float len = max(length(ch), 0.001);
	vec3 dir = ch / len;
	vec3 cp = wp - cam;
	float t = dot(cp, dir);
	float front = len - t;
	float d = length(cp - dir * t);
	float a = smoothstep(cut_radius * 0.62, cut_radius, d);
	a = max(a, 1.0 - smoothstep(cut_floor, cut_floor + 0.9, wp.y));
	a = max(a, 1.0 - smoothstep(0.5, 1.8, front));
	// nothing may loom right in front of the lens (tree tops and tall rocks under the camera)
	a *= smoothstep(2.2, 5.0, length(cp));
	return a;
}

void fragment() {
	vec4 a = texture(albedo_tex, UV) * albedo;
	vec3 orm = texture(orm_tex, UV).rgb;
	vec3 nm = texture(normal_tex, UV).rgb;
	vec3 em = texture(emission_tex, UV).rgb;
	float n_lo = texture(noise_tex, wpos.xz * 0.05 + wpos.yy * 0.031).r;

	vec3 col = a.rgb * tint * mix(0.84, 1.1, vary);
	float lum = dot(col, vec3(0.299, 0.587, 0.114));
	col = mix(col, vec3(lum), desaturate);

	vec3 wn = normalize(wnrm);
	float rough = clamp(roughness * orm.g, 0.04, 1.0);
	float cover = smoothstep(0.45, 0.8, wn.y + (n_lo - 0.5) * 0.6) * top_amount;
	col = mix(col, top_color, cover);
	rough = mix(rough, 0.9, cover * 0.7);

	ALBEDO = col;
	ROUGHNESS = rough;
	METALLIC = clamp(metallic * orm.b, 0.0, 1.0) * (1.0 - cover);
	AO = mix(1.0, orm.r, ao_amount);
	NORMAL_MAP = nm;
	NORMAL_MAP_DEPTH = normal_scale;
	EMISSION = (em + emission) * emission_energy;

	bool gone = a.a < alpha_cut;
	if (!IN_SHADOW_PASS) {
		float keep = see_through(wpos, CAMERA_POSITION_WORLD);
		float dither = fract(52.9829189 * fract(dot(FRAGCOORD.xy, vec2(0.06711056, 0.00583715))));
		gone = gone || keep < dither;
	}
	if (gone) {
		discard;
	}
}

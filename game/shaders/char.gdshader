// Character surface: PBR from the imported glTF material, plus per-instance gameplay effects (hit flash, tint,
// freeze, the ash-dissolve death and a thin gameplay rim).
//
// A character must look like it belongs in the light of the scene: it is lit by exactly the lights the ground under
// it is lit by, receives the same fog, ambient, shadows and exposure, and its albedo is graded into the same value
// band as the environment (grade_tint / grade_desat, set per level by CharModel.SetSceneGrade). Nothing here adds
// light of its own except:
//   - the emission the model was authored with (eyes, ember seams), kept small;
//   - rim_color: a thin fresnel edge for elites (affix colour) and summons, set by gameplay code;
//   - scene_rim: a barely visible cool edge on the side away from the lens, so a figure in shadow keeps a silhouette;
//   - the hit flash, which lasts a few frames.
shader_type spatial;
render_mode depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec4 albedo : source_color = vec4(1.0);
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float normal_scale = 1.0;
uniform sampler2D orm_tex : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;   // r = ao, g = roughness, b = metallic
uniform float ao_amount = 0.0;          // 1 only when the model really carries an occlusion map in the red channel
uniform float roughness = 0.8;
uniform float metallic = 0.0;
uniform sampler2D emission_tex : source_color, hint_default_black, filter_linear_mipmap, repeat_enable;
uniform vec3 emission : source_color = vec3(0.0);
uniform float emission_energy = 0.0;
uniform float pulse = 0.0;              // emissive accent breathing
uniform float alpha_cut = 0.0;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;

// the grading the world gets (theme tint, levelling to the environment's albedo band, a share of its desaturation)
uniform vec3 grade_tint = vec3(0.6);
uniform float grade_desat = 0.2;
uniform vec3 scene_rim = vec3(0.0);

// fixed slots: a model may mix this shader with spectral.gdshader on one mesh, and both must agree where each value lives
instance uniform vec3 tint : source_color, instance_index(0) = vec3(1.0);
instance uniform float flash : instance_index(1) = 0.0;
instance uniform vec3 flash_color : source_color, instance_index(2) = vec3(1.0);
instance uniform vec3 rim_color : source_color, instance_index(3) = vec3(0.0);
instance uniform float dissolve : instance_index(4) = 0.0;
instance uniform float frozen : instance_index(5) = 0.0;

varying vec3 wpos;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec4 a = texture(albedo_tex, UV) * albedo;
	if (a.a < alpha_cut) discard;
	float n = texture(noise_tex, wpos.xz * 0.45 + vec2(wpos.y * 0.45)).r;
	float edge = 0.0;
	if (dissolve > 0.001) {
		float d = dissolve * 1.15 - 0.05;
		if (n < d) discard;
		edge = 1.0 - smoothstep(d, d + 0.1, n);
	}
	vec3 orm = texture(orm_tex, UV).rgb;
	vec3 col = a.rgb * tint * grade_tint;
	col = mix(col, vec3(dot(col, vec3(0.299, 0.587, 0.114))), grade_desat);
	vec3 ice = vec3(0.55, 0.8, 1.0);
	col = mix(col, ice * (0.4 + 0.6 * dot(col, vec3(0.333))), frozen * 0.75);
	ALBEDO = col;
	// relief maps on plate under a near light sparkle: keep the lobe wide enough that glints stay small and calm
	ROUGHNESS = mix(clamp(roughness * orm.g, 0.24, 1.0), 0.15, frozen * 0.7);
	// Pure metal has no diffuse and, under a sky that is nearly black, nothing to reflect: plate armour measured at
	// 0.4 x the ground it stood on while cloth and skin sat at 0.9. Half of the metal is kept as diffuse, so steel is
	// lit by the lights of the scene like everything else and still glints.
	METALLIC = clamp(metallic * orm.b, 0.0, 1.0) * 0.5;
	AO = mix(1.0, orm.r, ao_amount);
	AO_LIGHT_AFFECT = 0.2 * ao_amount;
	NORMAL_MAP = texture(normal_tex, UV).rgb;
	NORMAL_MAP_DEPTH = normal_scale;

	float ndv = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float fres = pow(1.0 - ndv, 4.0);
	vec3 wn = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	float breathe = 1.0 + pulse * 0.5 * sin(TIME * 2.6 + wpos.x + wpos.z);
	vec3 em = (texture(emission_tex, UV).rgb + emission) * emission_energy * breathe * tint;
	// thin cool edge on what faces up or away from the lens (the camera looks north, -Z)
	em += scene_rim * fres * clamp(0.3 + 0.5 * wn.y - 0.45 * wn.z, 0.0, 1.0);
	em += rim_color * pow(1.0 - ndv, 3.0);
	// a hit reads as a brief pale edge and a partial tint, never a solid white figure
	em += flash_color * min(flash, 1.0) * (0.07 + fres * 0.5);
	em += vec3(1.0, 0.42, 0.08) * edge * 7.0;
	em += ice * frozen * fres * 0.8;
	EMISSION = em;
}

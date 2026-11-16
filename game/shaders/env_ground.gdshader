// Terrain. Blends up to four PBR texture sets by per-vertex weights:
//   COLOR.r = alternate patches, COLOR.g = path / road, COLOR.b = room or arena floor, remainder = base ground.
//   COLOR.a = shore (next to a pit): wet, scorched or fading into the void, depending on the theme uniforms.
//   UV.x    = baked proximity occlusion (1 = open ground, lower next to walls and under tree lines).
// World-space UVs (metres / size_m). Seen from a high camera a 3 m tile repeats seven times across the frame, so
// organic layers (soil, ash, snow, sand) are mixed with a second, larger and rotated look at the same texture in
// noise-shaped patches, and all layers get slow value and hue drift. Damp patches and puddles (theme wetness)
// darken the albedo and turn glossy, so fires and the moon lay streaks across the floor.
shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D alb0 : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D alb1 : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D alb2 : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D alb3 : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D nrm0 : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D nrm1 : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D nrm2 : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D nrm3 : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D arm0 : hint_default_white, filter_linear_mipmap, repeat_enable;   // r = ao, g = roughness, b = metallic
uniform sampler2D arm1 : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform sampler2D arm2 : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform sampler2D arm3 : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform vec4 uv_scale = vec4(0.5, 0.5, 0.5, 0.5);          // 1 / size_m of each layer
uniform vec4 macro = vec4(0.0);                             // per layer: how much of the large-scale second look to mix in (0 for masonry)
uniform vec3 tint0 = vec3(1.0);                             // linear multipliers (theme grading)
uniform vec3 tint1 = vec3(1.0);
uniform vec3 tint2 = vec3(1.0);
uniform vec3 tint3 = vec3(1.0);
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float desaturate : hint_range(0.0, 1.0) = 0.0;
uniform float normal_strength = 1.0;
uniform float rough_mul = 1.0;
uniform float wet : hint_range(0.0, 1.0) = 0.2;             // share of damp ground and puddles
uniform vec3 wet_tint = vec3(0.55, 0.57, 0.6);              // albedo multiplier where the ground is soaked
uniform vec3 drift = vec3(0.0);                             // slow hue drift across the map (added scaled by noise)
uniform vec3 shore_tint = vec3(0.55);                       // linear albedo multiplier at the shore
uniform float shore_rough = 0.5;                            // roughness multiplier at the shore (wet < 1)
uniform vec3 shore_glow : source_color = vec3(0.0);         // heat glow on lava banks
uniform float shore_glow_energy = 0.0;
uniform vec3 deep_color : source_color = vec3(0.0);         // what the pit sides fade to
uniform float deep_fade = 3.0;                              // metres below the ground plane where the fade completes

varying vec3 wpos;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	// tangent frame for uv = world xz: u along +X, v along +Z (same frame Godot uses for up-facing triplanar)
	vec3 n = normalize(NORMAL);
	vec3 t = normalize(cross(n, vec3(0.0, 0.0, 1.0)));
	TANGENT = t;
	BINORMAL = normalize(cross(n, t));
}

void fragment() {
	vec2 p = wpos.xz;
	vec2 dx = dFdx(p);
	vec2 dy = dFdy(p);
	float n_lo = texture(noise_tex, p * 0.0113).r;
	float n_mid = texture(noise_tex, p.yx * 0.031 + vec2(0.61, 0.17)).r;
	float n_hi = texture(noise_tex, p * 0.083 + vec2(0.37, 0.11)).r;
	float nb = (n_hi - 0.5) * 0.9 + (n_lo - 0.5) * 0.5;

	// sharpen the vertex ramps and push them around with noise: ragged borders instead of straight gradients.
	// A weight of exactly 0 stays 0 and exactly 1 stays 1, so textures never bleed into cells that do not own them.
	vec3 w = clamp((COLOR.rgb - 0.5) * 2.4 + 0.5 + nb, 0.0, 1.0);
	float k0 = (1.0 - w.r) * (1.0 - w.g) * (1.0 - w.b);
	float k1 = w.r * (1.0 - w.g) * (1.0 - w.b);
	float k2 = w.g * (1.0 - w.b);
	float k3 = w.b;

	// the second look: 3.3 times larger and turned by 35 degrees, in patches a few metres across
	vec2 pm = vec2(p.x * 0.819 - p.y * 0.574, p.x * 0.574 + p.y * 0.819) * 0.3;
	float patch_m = smoothstep(0.38, 0.62, n_mid * 0.6 + n_hi * 0.4);

	vec3 alb = vec3(0.0);
	vec3 nrm = vec3(0.0);
	vec3 arm = vec3(0.0);
	float ks = 0.0;
	if (k0 > 0.004) {
		float s = uv_scale.x;
		vec3 a = textureGrad(alb0, p * s, dx * s, dy * s).rgb;
		if (macro.x > 0.01) {
			a = mix(a, texture(alb0, pm * s).rgb, patch_m * macro.x);
		}
		alb += a * tint0 * k0;
		nrm += textureGrad(nrm0, p * s, dx * s, dy * s).rgb * k0;
		arm += textureGrad(arm0, p * s, dx * s, dy * s).rgb * k0;
		ks += k0;
	}
	if (k1 > 0.004) {
		float s = uv_scale.y;
		vec3 a = textureGrad(alb1, p * s, dx * s, dy * s).rgb;
		if (macro.y > 0.01) {
			a = mix(a, texture(alb1, pm * s).rgb, patch_m * macro.y);
		}
		alb += a * tint1 * k1;
		nrm += textureGrad(nrm1, p * s, dx * s, dy * s).rgb * k1;
		arm += textureGrad(arm1, p * s, dx * s, dy * s).rgb * k1;
		ks += k1;
	}
	if (k2 > 0.004) {
		float s = uv_scale.z;
		vec3 a = textureGrad(alb2, p * s, dx * s, dy * s).rgb;
		if (macro.z > 0.01) {
			a = mix(a, texture(alb2, pm * s).rgb, patch_m * macro.z);
		}
		alb += a * tint2 * k2;
		nrm += textureGrad(nrm2, p * s, dx * s, dy * s).rgb * k2;
		arm += textureGrad(arm2, p * s, dx * s, dy * s).rgb * k2;
		ks += k2;
	}
	if (k3 > 0.004) {
		float s = uv_scale.w;
		vec3 a = textureGrad(alb3, p * s, dx * s, dy * s).rgb;
		if (macro.w > 0.01) {
			a = mix(a, texture(alb3, pm * s).rgb, patch_m * macro.w);
		}
		alb += a * tint3 * k3;
		nrm += textureGrad(nrm3, p * s, dx * s, dy * s).rgb * k3;
		arm += textureGrad(arm3, p * s, dx * s, dy * s).rgb * k3;
		ks += k3;
	}
	float inv = 1.0 / max(ks, 0.004);
	alb *= inv;
	nrm *= inv;
	arm *= inv;

	// large-scale value and hue variation hides the texture repeat from the high camera
	alb *= mix(0.66, 1.2, n_lo) * mix(0.86, 1.1, n_mid);
	alb += drift * (n_lo - 0.5) * 2.0 * dot(alb, vec3(0.333));
	alb = max(alb, vec3(0.0));
	float lum = dot(alb, vec3(0.299, 0.587, 0.114));
	alb = mix(alb, vec3(lum), desaturate);

	float occ = clamp(UV.x, 0.0, 1.0);
	alb *= mix(0.5, 1.0, occ);

	float rough = clamp(arm.g * rough_mul, 0.04, 1.0);

	// damp ground and standing water: where the low noise bands dip, and wherever the texture's own cavities are deep
	float wn = n_lo * 0.55 + n_mid * 0.3 + n_hi * 0.15 + (1.0 - arm.r) * 0.35 - (1.0 - occ) * 0.1;
	float damp = smoothstep(0.8 - wet * 0.48, 0.92 - wet * 0.42, wn) * step(0.001, wet);
	float puddle = smoothstep(0.93 - wet * 0.42, 0.97 - wet * 0.42, wn) * step(0.001, wet);
	alb *= mix(vec3(1.0), wet_tint, damp * 0.8);
	rough = mix(rough, 0.34, damp * 0.7);
	rough = mix(rough, 0.2, puddle);

	// shore: wet / scorched band next to pits
	float sh = clamp(COLOR.a * 1.5 + nb * COLOR.a, 0.0, 1.0);
	alb = mix(alb, alb * shore_tint, sh);
	rough = mix(rough, clamp(rough * shore_rough, 0.04, 1.0), sh);

	// pit sides sink into the theme's deep colour
	float deep = smoothstep(0.0, 1.0, clamp(-wpos.y / max(deep_fade, 0.05), 0.0, 1.0));
	alb = mix(alb, deep_color, deep);

	ALBEDO = alb;
	ROUGHNESS = rough;
	METALLIC = arm.b * (1.0 - deep) * (1.0 - puddle);
	SPECULAR = 0.5 * (1.0 - deep * 0.8) * mix(0.55, 1.0, occ) * mix(1.0, 0.5, damp);
	AO = arm.r * mix(0.55, 1.0, occ);
	AO_LIGHT_AFFECT = 0.35;
	NORMAL_MAP = mix(nrm, vec3(0.5, 0.5, 1.0), puddle * 0.92);
	NORMAL_MAP_DEPTH = normal_strength * (1.0 + damp * 0.3);

	float below = clamp(-wpos.y * 1.6, 0.0, 1.0);
	float flick = 0.75 + 0.25 * sin(TIME * 1.3 + n_lo * 21.0);
	EMISSION = shore_glow * shore_glow_energy * sh * sh * (0.12 + below) * flick * (1.0 - deep * 0.5);
}

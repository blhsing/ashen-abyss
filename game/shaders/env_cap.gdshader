// Out-of-bounds filler: the unlit surface over solid rock deep inside walls and beyond the edge of the map.
// It is not a black hole: it carries the rock of the wall tops at the level of unlit stone, strongest next to the
// rooms and sinking into darkness the deeper into the mass it lies (depth_tex = metres to the nearest open floor),
// with slow blotches of dust so the eye reads a surface. Still far below anything a torch lights.
// It takes part in the see-through cut so the hole opened around the hero looks into the level, not into this lid.
shader_type spatial;
render_mode unshaded, cull_back, shadows_disabled, fog_disabled;

global uniform vec3 hero_pos;

uniform vec3 color : source_color = vec3(0.0);
uniform sampler2D rock_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float rock_scale = 0.4;               // 1 / size_m of the rock texture
uniform vec3 rock_level = vec3(0.0);          // linear multiplier of the rock texture next to the rooms; 0 = flat colour only
uniform vec3 dust = vec3(0.0);                // colour of the dust lying in the hollows of the rock
uniform float desaturate : hint_range(0.0, 1.0) = 0.0;
uniform float lid_gain = 1.0;                 // lift over the level of ambient + fill alone, at its full strength round the hero
uniform sampler2D depth_tex : hint_default_black, filter_linear, repeat_disable;   // r = metres into the mass / depth_range
uniform vec4 depth_rect = vec4(0.0);          // field origin xz, 1 / size (0 = no depth map)
uniform float depth_range = 24.0;
uniform float fade_m = 7.0;                   // metres over which the rock sinks to its deep level
uniform float deep_level = 0.2;               // share of the level that remains far inside the mass
uniform float cut_radius = 2.6;
uniform float cut_floor = 0.45;
// indoors: r = 1 on open floor, one texel per nav cell (EnvMats.SetWalkMask); walk_rect = grid origin xz, 1 / size
uniform sampler2D walk_mask : hint_default_black, filter_linear, repeat_disable;
uniform vec4 walk_rect = vec4(0.0);

varying vec3 wpos;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
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

// same rule as env_wall: the lid over a block is dissolved wherever it stands between the lens and open floor
float floor_cut(vec3 wp, vec3 cam) {
	if (walk_rect.z <= 0.0) return 1.0;
	float t = cam.y / max(cam.y - wp.y, 0.01);
	vec2 g = cam.xz + (wp.xz - cam.xz) * t;
	float open = texture(walk_mask, (g - walk_rect.xy) * walk_rect.zw).r;
	float a = 1.0 - smoothstep(0.55, 0.95, open);
	return max(a, 1.0 - smoothstep(cut_floor, cut_floor + 0.9, wp.y));
}

void fragment() {
	vec3 c = color;
	if (rock_level.r + rock_level.g + rock_level.b > 0.0) {
		vec2 p = wpos.xz;
		vec3 rock = texture(rock_tex, p * rock_scale).rgb;
		float n1 = texture(noise_tex, p * 0.013).r;
		float n2 = texture(noise_tex, p * 0.047 + vec2(0.37, 0.11)).r;
		float blot = smoothstep(0.3, 0.8, n1 * 0.62 + n2 * 0.5);
		float depth = 0.0;
		if (depth_rect.z > 0.0) {
			vec2 uv = (p - depth_rect.xy) * depth_rect.zw;
			depth = texture(depth_tex, clamp(uv, vec2(0.0), vec2(1.0))).r * depth_range;
			// beyond the built field the mass only gets deeper
			vec2 o = max(max(-uv, uv - vec2(1.0)), vec2(0.0)) / depth_rect.zw;
			depth += length(o);
		}
		float sink = mix(deep_level, 1.0, exp(-max(depth - 2.0, 0.0) / fade_m));
		// next to the rooms the lid is the wall top's own surface (same value drift as env_wall); the blotches and the
		// dust come in with the depth
		float n_lo = texture(noise_tex, p * 0.021 + vec2(wpos.y * 0.013)).r;
		float deep = smoothstep(2.3, 7.0, depth);
		float by_hero = 1.0 - smoothstep(8.0, 22.0, distance(hero_pos.xz, p));
		rock *= rock_level * mix(0.8, 1.12, n_lo) * (1.0 + (lid_gain - 1.0) * mix(0.35, 1.0, by_hero));
		rock = mix(rock, vec3(dot(rock, vec3(0.299, 0.587, 0.114))), desaturate);
		c += (rock * mix(1.0, mix(0.5, 1.15, blot), deep) + dust * (1.0 - blot) * 0.5 * deep) * sink;
	}
	ALBEDO = c;
	if (!IN_SHADOW_PASS) {
		float keep = min(see_through(wpos, CAMERA_POSITION_WORLD), floor_cut(wpos, CAMERA_POSITION_WORLD));
		float dither = fract(52.9829189 * fract(dot(FRAGCOORD.xy, vec2(0.06711056, 0.00583715))));
		if (keep < dither) {
			discard;
		}
	}
}

// Walls, cliffs, wall caps and all procedural set dressing (houses, columns, graves, crystals ...).
// Two mapping modes: mesh UVs in metres with real tangents (architecture), or world-space triplanar (rock, ice).
// COLOR.rgb multiplies the albedo (baked contact darkening / the fade of wall tops into blackness),
// COLOR.a scales the emission (windows, glowing fungus, crystal cores).
// Every occluder cuts a soft dithered disc around the line from the camera to the hero so the fight is never hidden;
// the same is done, a little tighter, for the enemy under the cursor or being attacked (focus_pos).
shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx;

global uniform vec3 hero_pos;
global uniform vec4 focus_pos;                // xyz = the enemy under the cursor / being attacked, w = 0..1 (CameraRig)

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D arm_tex : hint_default_white, filter_linear_mipmap, repeat_enable;   // r = ao, g = roughness, b = metallic
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float uv_scale = 0.5;                 // 1 / size_m
uniform bool triplanar = false;
uniform float tri_sharp = 6.0;
uniform vec3 tint = vec3(1.0);                // linear multiplier (theme grading)
uniform float desaturate : hint_range(0.0, 1.0) = 0.0;
uniform float normal_strength = 1.0;
uniform float rough_mul = 1.0;
uniform float rough_add = 0.0;
uniform float metal_mul = 1.0;
uniform vec3 top_color : source_color = vec3(1.0);   // snow / ash / moss / sand settling on up-facing surfaces
uniform float top_amount = 0.0;
uniform vec3 emission : source_color = vec3(0.0);
uniform float emission_energy = 0.0;
uniform float cut_radius = 2.6;
uniform float cut_floor = 0.45;               // geometry below this height is never cut (walls keep a readable stub)
uniform vec3 sky_fill = vec3(0.0);            // rock under an open sky: extra ambient on up-facing surfaces
uniform vec3 sky_rim = vec3(0.0);             // and a faint grazing-angle sheen, so a cliff in shadow keeps its shape
uniform vec3 self_lit = vec3(0.0);            // indoor wall tops: the unlit level they share with the lid behind them (EnvMats.Lid)
// indoors: r = 1 on open floor, one texel per nav cell (EnvMats.SetWalkMask); walk_rect = grid origin xz, 1 / size
uniform sampler2D walk_mask : hint_default_black, filter_linear, repeat_disable;
uniform vec4 walk_rect = vec4(0.0);

varying vec3 wpos;
varying vec3 wnrm;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wnrm = normalize(MODEL_NORMAL_MATRIX * NORMAL);
	if (triplanar) {
		// same blended tangent frame Godot's own triplanar materials use (these meshes are authored in world space)
		vec3 an = abs(wnrm);
		TANGENT = normalize(vec3(0.0, 0.0, -1.0) * an.x + vec3(1.0, 0.0, 0.0) * an.y + vec3(1.0, 0.0, 0.0) * an.z);
		BINORMAL = normalize(vec3(0.0, 1.0, 0.0) * an.x + vec3(0.0, 0.0, -1.0) * an.y + vec3(0.0, 1.0, 0.0) * an.z);
	}
}

// One function per texture: Godot refuses a shared sampler-argument function when the samplers have different hints.
vec3 tri_albedo(vec3 w, vec3 q) {
	return texture(albedo_tex, q.xy).rgb * w.z + texture(albedo_tex, q.xz).rgb * w.y + texture(albedo_tex, q.zy * vec2(-1.0, 1.0)).rgb * w.x;
}
vec3 tri_normal(vec3 w, vec3 q) {
	return texture(normal_tex, q.xy).rgb * w.z + texture(normal_tex, q.xz).rgb * w.y + texture(normal_tex, q.zy * vec2(-1.0, 1.0)).rgb * w.x;
}
vec3 tri_arm(vec3 w, vec3 q) {
	return texture(arm_tex, q.xy).rgb * w.z + texture(arm_tex, q.xz).rgb * w.y + texture(arm_tex, q.zy * vec2(-1.0, 1.0)).rgb * w.x;
}

// 1 = keep, 0 = fully cut, for one figure standing at `feet`. Only geometry between the camera and the figure, above
// the stub height, is affected: a wide disc round the line lens → chest, and the figure's own outline (whatever
// covers the body between feet and head, however close in front of it the wall stands).
float see_through(vec3 wp, vec3 cam, vec3 feet, float radius) {
	vec3 cp = wp - cam;
	vec3 ch = feet + vec3(0.0, 1.0, 0.0) - cam;
	float len = max(length(ch), 0.001);
	vec3 dir = ch / len;
	float t = dot(cp, dir);
	float front = len - t;                              // how far in front of the figure (towards the camera)
	float d = length(cp - dir * t);
	float a = smoothstep(radius * 0.62, radius, d);
	a = max(a, 1.0 - smoothstep(0.5, 1.8, front));

	vec2 hd = ch.xz / max(length(ch.xz), 0.001);
	float dh = dot(ch.xz, hd);
	float df = dot(cp.xz, hd);
	float ahead = dh - df;                              // metres this fragment stands in front of the figure's plane
	if (ahead > 0.0 && df > 0.1) {
		vec3 q = cam + cp * (dh / df);
		float side = dot(q.xz - feet.xz, vec2(-hd.y, hd.x));
		float up = max(max(q.y - 1.9, -0.15 - q.y), 0.0);
		float fig = smoothstep(0.6, 0.95, length(vec2(side, up)));
		fig = max(fig, 1.0 - smoothstep(0.12, 0.4, ahead));
		a = min(a, fig);
	}
	a = max(a, 1.0 - smoothstep(cut_floor, cut_floor + 0.9, wp.y));
	// nothing may loom right in front of the lens (tree tops and tall rocks under the camera)
	a *= smoothstep(2.2, 5.0, length(cp));
	return a;
}

// 1 = keep, 0 = fully cut. Indoors a wall may never hide floor the player can walk on: the ray from the lens through
// this fragment is followed down to the floor, and if it lands on open floor the fragment is dissolved (down to the
// stub), wherever the hero stands. Walls backed by solid rock, and the far walls of a room, are untouched.
float floor_cut(vec3 wp, vec3 cam) {
	if (walk_rect.z <= 0.0) return 1.0;
	float t = cam.y / max(cam.y - wp.y, 0.01);
	vec2 g = cam.xz + (wp.xz - cam.xz) * t;
	float open = texture(walk_mask, (g - walk_rect.xy) * walk_rect.zw).r;
	float a = 1.0 - smoothstep(0.55, 0.95, open);
	return max(a, 1.0 - smoothstep(cut_floor, cut_floor + 0.9, wp.y));
}

void fragment() {
	vec3 alb;
	vec3 nrm;
	vec3 arm;
	vec3 wn = normalize(wnrm);
	if (triplanar) {
		vec3 w = pow(abs(wn), vec3(tri_sharp));
		w /= max(w.x + w.y + w.z, 0.0001);
		vec3 q = wpos * uv_scale * vec3(1.0, -1.0, 1.0);
		alb = tri_albedo(w, q);
		nrm = tri_normal(w, q);
		arm = tri_arm(w, q);
	} else {
		vec2 uv = UV * uv_scale;
		alb = texture(albedo_tex, uv).rgb;
		nrm = texture(normal_tex, uv).rgb;
		arm = texture(arm_tex, uv).rgb;
	}

	float n_lo = texture(noise_tex, wpos.xz * 0.021 + wpos.yy * 0.013).r;
	alb *= tint * mix(0.8, 1.12, n_lo);
	float lum = dot(alb, vec3(0.299, 0.587, 0.114));
	alb = mix(alb, vec3(lum), desaturate);

	float rough = clamp(arm.g * rough_mul + rough_add, 0.04, 1.0);
	float cover = smoothstep(0.5, 0.82, wn.y + (n_lo - 0.5) * 0.5) * top_amount;
	alb = mix(alb, top_color, cover);
	rough = mix(rough, 0.9, cover * 0.7);

	float shade = clamp(dot(COLOR.rgb, vec3(0.3333)) * 1.6, 0.0, 1.0);
	float up = clamp(wn.y * 0.5 + 0.5, 0.0, 1.0);
	float graze = 1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
	graze *= graze * graze;

	ALBEDO = alb * COLOR.rgb;
	ROUGHNESS = rough;
	METALLIC = clamp(arm.b * metal_mul, 0.0, 1.0) * (1.0 - cover);
	SPECULAR = 0.5 * shade;
	AO = arm.r;
	AO_LIGHT_AFFECT = 0.2;
	NORMAL_MAP = nrm;
	NORMAL_MAP_DEPTH = normal_strength * (1.0 - cover * 0.6);
	EMISSION = emission * emission_energy * COLOR.a + (alb * sky_fill * up * up + sky_rim * graze * (0.35 + 0.65 * up)) * shade;
	if (self_lit.r + self_lit.g + self_lit.b > 0.0) {
		// the same lift, with the same fall-off round the hero, as env_cap gives the lid: one surface to the eye
		float by_hero = 1.0 - smoothstep(8.0, 22.0, distance(hero_pos.xz, wpos.xz));
		EMISSION += alb * COLOR.rgb * self_lit * mix(0.35, 1.0, by_hero);
	}

	// the cut comes last so every texture lookup above keeps valid screen-space derivatives
	if (!IN_SHADOW_PASS) {
		vec3 cam = CAMERA_POSITION_WORLD;
		float keep = see_through(wpos, cam, hero_pos, cut_radius);
		if (focus_pos.w > 0.01) {
			keep = min(keep, mix(1.0, see_through(wpos, cam, focus_pos.xyz, cut_radius * 0.7), focus_pos.w));
		}
		keep = min(keep, floor_cut(wpos, cam));
		float dither = fract(52.9829189 * fract(dot(FRAGCOORD.xy, vec2(0.06711056, 0.00583715))));
		if (keep < dither) {
			discard;
		}
	}
}

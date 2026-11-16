// Out-of-bounds filler: the unlit dark surface over solid rock deep inside walls and beyond the edge of the map.
// It takes part in the see-through cut so the hole opened around the hero looks into the level, not into this lid.
shader_type spatial;
render_mode unshaded, cull_back, shadows_disabled, fog_disabled;

global uniform vec3 hero_pos;

uniform vec3 color : source_color = vec3(0.0);
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
	ALBEDO = color;
	if (!IN_SHADOW_PASS) {
		float keep = min(see_through(wpos, CAMERA_POSITION_WORLD), floor_cut(wpos, CAMERA_POSITION_WORLD));
		float dither = fract(52.9829189 * fract(dot(FRAGCOORD.xy, vec2(0.06711056, 0.00583715))));
		if (keep < dither) {
			discard;
		}
	}
}

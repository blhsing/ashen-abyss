// Vertical column of soft light on an open unit cylinder (radius 1, height 1, centred on its origin): loot beams,
// the level-up pillar and the gathering line before a sky bolt. Reads as a soft beam from any camera angle.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back, shadows_disabled, fog_disabled;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
instance uniform float t = 0.0;          // 0 = full, 1 = gone
instance uniform vec3 tint : source_color = vec3(1.0);
instance uniform float gain = 1.0;
instance uniform float power = 2.0;      // falloff across the column
instance uniform float top_fade = 0.6;   // share of the height that fades out toward the top
instance uniform float shimmer = 0.3;    // strength of the rising streaks
varying float h;
varying vec3 axis_v;
varying float phase;

void vertex() {
	h = VERTEX.y + 0.5;
	axis_v = (MODELVIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz;
	phase = MODEL_MATRIX[3].x * 0.37 + MODEL_MATRIX[3].z * 0.53;
}

void fragment() {
	vec3 v = normalize(VIEW);
	vec3 a = normalize(axis_v);
	// the view direction flattened onto the plane across the column, so a steep camera does not dim it
	vec3 vp = normalize(v - a * dot(v, a) + vec3(0.00001));
	float f = pow(clamp(dot(normalize(NORMAL), vp), 0.0, 1.0), power);
	float up = 1.0 - smoothstep(1.0 - clamp(top_fade, 0.01, 1.0), 1.0, h);
	float foot = smoothstep(0.0, 0.03, h);
	float n = texture(noise_tex, vec2(UV.x + phase, h * 1.2 - TIME * 0.4)).r;
	float s = mix(1.0, 0.45 + 1.1 * n, shimmer);
	ALBEDO = mix(tint, vec3(1.0), f * f * 0.4) * gain * (0.7 + f * 1.0);
	ALPHA = clamp(f * up * foot * s * (1.0 - t), 0.0, 1.0);
}

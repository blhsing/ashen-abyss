// Ground-hugging wave front (cleaving shockwaves, quakes, palm blasts) on the ground quad (PlaneMesh 2 x 2: local
// XZ runs -1..1). A bright curved leading edge bulging toward +Z, the direction of travel, with torn streaks behind it.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
instance uniform vec3 tint : source_color = vec3(1.0, 0.45, 0.1);
instance uniform float gain = 1.0;
instance uniform float fade = 1.0;
instance uniform float seed = 0.0;
varying vec2 lp;

void vertex() {
	lp = VERTEX.xz;
}

void fragment() {
	// the leading edge is an arc of a circle centred behind the quad
	float r = length(lp - vec2(0.0, -1.1));
	float d = 1.95 - r;                                    // > 0 behind the front
	float front = 1.0 - smoothstep(0.0, 0.07, abs(d - 0.05));
	float n = texture(noise_tex, vec2(lp.x * 0.45 + seed, d * 0.35 - TIME * 1.6)).r;
	float tail = step(0.0, d) * exp(-d * 2.4) * (0.35 + 1.1 * n);
	float side = 1.0 - smoothstep(0.72, 1.0, abs(lp.x));
	float back = smoothstep(-1.0, -0.8, lp.y);
	float a = (front * 1.1 + tail * 0.75) * side * back;
	ALBEDO = mix(tint, vec3(1.0), front * 0.4) * gain * (0.7 + front * 1.2 + tail * 0.4);
	ALPHA = clamp(a * fade, 0.0, 1.0);
}

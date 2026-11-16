// Mist layer hanging inside bottomless pits: slow drifting noise in the region hue, thinner at the rim
// (COLOR.r = 1 at the pit edge, 0 over the middle). Several layers are stacked at different depths.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform vec3 color : source_color = vec3(0.4, 0.5, 0.6);
uniform float density = 0.6;
uniform float brightness = 1.0;
uniform float speed = 1.0;
uniform float seed = 0.0;

varying vec3 wpos;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float t = TIME * speed;
	vec2 p = wpos.xz;
	float n1 = texture(noise_tex, p * 0.034 + vec2(t * 0.011, t * 0.006) + vec2(seed)).r;
	float n2 = texture(noise_tex, p * 0.089 - vec2(t * 0.017, -t * 0.009) + vec2(seed * 1.7, 0.3)).r;
	float n = n1 * 0.62 + n2 * 0.5;
	float body = smoothstep(0.28, 0.8, n);
	float rim = 1.0 - smoothstep(0.35, 1.0, COLOR.r);
	ALBEDO = color * brightness * (0.45 + n * 0.9);
	ALPHA = clamp(body * density * rim, 0.0, 1.0);
}

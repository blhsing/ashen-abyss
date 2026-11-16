// Lightning / energy strip: bright core along the centre of the strip (UV.x across, UV.y along its length).
// flow = 0 gives a clean bolt, flow = 1 a streaming beam of energy. The strip thins as it dies.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
instance uniform float t = 0.0;          // 0 = struck, 1 = gone
instance uniform vec3 tint : source_color = vec3(1.0);
instance uniform float gain = 1.0;
instance uniform float flow = 0.0;

void fragment() {
	float c = 1.0 - abs(UV.x * 2.0 - 1.0);
	float core = pow(c, 3.0);
	float k = clamp(t, 0.0, 1.0);
	float n = texture(noise_tex, vec2(UV.x * 0.3, UV.y * 2.0 - TIME * 3.0)).r;
	float body = pow(c, 1.2) * mix(1.0, 0.55 + 0.9 * n, flow);
	float thin = smoothstep(k * 0.7, 1.0, c);
	float ends = smoothstep(0.0, 0.05, UV.y) * smoothstep(0.0, 0.05, 1.0 - UV.y);
	ALBEDO = mix(tint, vec3(1.0), core * 0.6) * gain * (0.8 + core * 1.5);
	ALPHA = clamp(body * thin * (1.0 - k) * ends * 1.25, 0.0, 1.0);
}

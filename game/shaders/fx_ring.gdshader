// Expanding shock ring on the ground quad (PlaneMesh 2 x 2, so local XZ runs -1..1).
// One shared material: progress, colour, brightness and band width are per instance.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
instance uniform float t = 0.0;          // 0 = born, 1 = gone
instance uniform vec3 tint : source_color = vec3(1.0);
instance uniform float gain = 1.0;       // brightness
instance uniform float width = 0.16;     // band half-width as a fraction of the radius
varying vec2 lp;

void vertex() {
	lp = VERTEX.xz;
}

void fragment() {
	float r = length(lp);
	float k = clamp(t, 0.0, 1.0);
	float e = 1.0 - pow(1.0 - k, 3.0);                 // races out, then settles at the rim
	float rad = mix(0.1, 0.93, e);
	float ang = atan(lp.y, lp.x);
	// ang * 2/pi spans -2..2, so the noise wraps without a seam
	float n = texture(noise_tex, vec2(ang * 0.63662, r * 0.8 - k * 0.7)).r;
	float w = max(width * mix(0.8, 0.3, e), 0.008);
	float band = (1.0 - smoothstep(0.0, w, abs(r - rad))) * (0.6 + 0.8 * n);
	float wake = (1.0 - smoothstep(0.0, max(rad, 0.01), r)) * 0.08 * (1.0 - k) * (1.0 - k);
	float life = 1.0 - k * k;                          // stays bright, fades late
	ALBEDO = mix(tint, vec3(1.0), band * 0.2 * (1.0 - k)) * gain * (0.55 + band * 0.65);
	ALPHA = clamp((band * 0.5 + wake) * life * step(r, 1.0), 0.0, 1.0);
}

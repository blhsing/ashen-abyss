// Soft glowing volume (explosion cores, projectile heads, orbs, wisps) on a unit sphere: brightest where it faces
// the camera. One shared material; everything that differs is per instance.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back, shadows_disabled, fog_disabled;
instance uniform float t = 0.0;          // 0 = full, 1 = gone
instance uniform vec3 tint : source_color = vec3(1.0);
instance uniform float power = 2.0;      // falloff: higher = smaller, harder core
instance uniform float gain = 1.0;       // brightness
instance uniform float pulse = 0.0;      // 0..1 breathing amount
instance uniform float bob = 0.0;        // metres of slow vertical drift
varying float phase;

void vertex() {
	phase = MODEL_MATRIX[3].x * 1.7 + MODEL_MATRIX[3].z * 2.3;
	VERTEX.y += bob * sin(TIME * 1.9 + phase) / max(length(MODEL_MATRIX[1].xyz), 0.001);
}

void fragment() {
	float f = pow(clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), power);
	float breathe = 1.0 + pulse * 0.35 * sin(TIME * 5.0 + phase);
	ALBEDO = mix(tint, vec3(1.0), f * f * 0.28) * gain * (0.5 + f * 0.8) * breathe;
	ALPHA = clamp(f * f * (1.0 - t) * (1.0 - t) * breathe, 0.0, 1.0);
}

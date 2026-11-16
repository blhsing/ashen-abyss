// Melee swipe: the trail of a blade — a thin bright edge where the tip is, a short streaked tail in the weapon's or
// element's colour behind it — sweeping across in front of the attacker, drawn on the ground quad
// (PlaneMesh 2 x 2: local XZ runs -1..1, +Z is forward). arc >= 3.1 sweeps the full circle (spins).
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
instance uniform float t = 0.0;
instance uniform float arc = 1.1;        // half-angle in radians
instance uniform vec3 tint : source_color = vec3(1.0);
instance uniform float gain = 1.0;
varying vec2 lp;

void vertex() {
	lp = VERTEX.xz;
}

void fragment() {
	float r = length(lp);
	float ang = atan(lp.x, lp.y);                          // 0 straight ahead
	float full = step(3.1, arc);
	float head = mix(-arc, arc, clamp(t * 1.35, 0.0, 1.0)); // the blade's position in the sweep
	float tail = mix(1.1, 2.6, full);
	float behind = head - ang;
	float lead = step(0.0, behind) * (1.0 - smoothstep(0.0, tail * 0.7, behind));
	float in_arc = step(abs(ang), arc);
	// thick in the middle of the swing, tapering to points at both ends
	float taper = mix(smoothstep(0.0, 0.4, 1.0 - abs(ang) / max(arc, 0.01)), 1.0, full);
	float inner_r = mix(0.86, 0.66, taper);
	// the edge the tip draws: a few centimetres wide
	float dr = (r - 0.9) / 0.03;
	float core = exp(-dr * dr);
	// the body behind it thins out towards the hand and is broken into streaks along the swing
	float body = smoothstep(inner_r, 0.9, r) * (1.0 - smoothstep(0.9, 0.97, r));
	float n = texture(noise_tex, vec2(ang * 0.31831 - t * 0.5, r * 5.0)).r;   // ang / pi spans -1..1: no seam
	float streak = smoothstep(0.3, 0.8, n);
	float sweep = lead * lead * lead;                    // bright at the blade, gone a third of the swing behind it
	float a = in_arc * sweep * (1.0 - t * t) * (core * 0.85 + body * (0.1 + 0.3 * streak) * (0.4 + 0.6 * r));
	ALBEDO = mix(tint, vec3(1.0), core * sweep * 0.3) * gain;
	ALPHA = clamp(a, 0.0, 1.0);
}

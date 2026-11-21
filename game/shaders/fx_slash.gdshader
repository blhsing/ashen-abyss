// Melee swipe: the trail of a blade — a thin bright edge where the tip is, a short streaked tail in the weapon's or
// element's colour behind it — sweeping across in front of the attacker, drawn on the ground quad
// (PlaneMesh 2 x 2: local XZ runs -1..1, +Z is forward). arc >= 3.1 sweeps the full circle (spins).
// The arc is dressed in the school of the blow (the same numbers as fx_telegraph) and shows which way it swings:
// two fine echo arcs lag behind the edge, chevrons ride the trail pointing along the swing, and the edge carries
// 0 fire: tongues blown back   1 frost: long and short shards   2 lightning: a jagged arc with forks
// 3 poison: drops flung off the blade   4 shadow: hooked thorns   5 steel: sparks   6 holy: rays
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
instance uniform float t = 0.0;
instance uniform float arc = 1.1;        // half-angle in radians
instance uniform vec3 tint : source_color = vec3(1.0);
instance uniform float gain = 1.0;
instance uniform float kind = 5.0;       // school of the ornament
instance uniform float radius = 2.5;     // metres: keeps the ornament the same size in the world
instance uniform float seed = 0.0;
varying vec2 lp;

float ln(float d, float w) {
	return 1.0 - smoothstep(w * 0.55, w, abs(d));
}

float tri(float x) {
	return abs(fract(x) * 2.0 - 1.0);
}

float hash1(float n) {
	return fract(sin(n * 12.9898 + 4.1414) * 43758.5453);
}

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

	// ornament, in metres: m_out outside the edge (+) or inside it (-), lat along the path of the blade
	float big = max(radius, 0.3);
	float sc = clamp(big * 0.4, 0.6, 1.3);
	float m_out = (r - 0.9) * big;
	float pitch = clamp(big * 0.14, 0.2, 0.42);
	float lobes = max(floor(6.2832 * 0.9 * big / pitch * 0.5), 3.0) * 2.0;   // a whole, even number round the circle: spins close without a seam
	float cu = ang / 6.2832 * lobes + seed * 13.0;
	float cell = floor(cu);
	float f = cu - cell;
	float id = mod(cell, lobes);
	float h = hash1(id + seed * 7.0);
	float lat = (f - 0.5) * 6.2832 * 0.9 * big / lobes;
	float orn = 0.0;
	if (kind < 0.5) {
		float tall = (0.22 + 0.3 * h) * sc * taper;
		float up = m_out / max(tall, 0.01);
		float hw = 0.085 * sc * smoothstep(-0.15, 0.2, up) * pow(max(1.0 - up, 0.0), 1.1);
		orn = (1.0 - smoothstep(hw * 0.4, hw + 0.01, abs(lat + up * up * 0.14 * sc))) * step(-0.15, up) * step(up, 1.0) * (1.0 - 0.5 * up);
	} else if (kind < 1.5) {
		float tall = mix(0.36, 0.17, step(0.5, fract(id * 0.5))) * sc * taper;
		float up = m_out / max(tall, 0.01);
		float hw = 0.055 * sc * (1.0 - up);
		orn = (1.0 - smoothstep(hw * 0.5, hw + 0.008, abs(lat))) * step(0.0, up) * step(up, 1.0);
	} else if (kind < 2.5) {
		float strike = floor(TIME * 14.0);
		float hz = hash1(seed * 3.0 + strike);
		float zig = (tri(cu * 0.5 + hz) - 0.5) * 0.24 * sc + (tri(cu * 1.7 + hz * 3.0) - 0.5) * 0.1 * sc;
		float jag = ln(m_out - 0.1 * sc - zig * taper, 0.03);
		float fork = ln(lat, 0.022) * step(0.0, m_out) * step(m_out, (0.15 + 0.3 * h) * sc * taper) * step(0.55, h);
		orn = max(jag, fork);
	} else if (kind < 3.5) {
		float fly = (0.1 + 0.26 * h) * sc * taper;
		float drop = 1.0 - smoothstep(0.035, 0.07, length(vec2(lat, (m_out - fly) * 0.7)));
		float thread = (1.0 - smoothstep(0.0, 0.025, abs(lat))) * step(0.0, m_out) * step(m_out, fly) * 0.5;
		orn = max(drop, thread) * step(0.25, h);
	} else if (kind < 4.5) {
		float up = m_out / max(0.26 * sc * taper, 0.01);
		orn = step(0.0, up) * step(up, 1.0) * smoothstep(0.0, 0.07, mix(0.78, 0.18, up * up) - f) * smoothstep(0.0, 0.07, f - mix(0.48, 0.18, up));
	} else if (kind < 5.5) {
		// sparks struck off the edge: small four-pointed stars
		float sy = m_out - 0.04;
		float star = max(ln(lat, 0.012) * (1.0 - smoothstep(0.0, 0.11 * sc, abs(sy))), ln(sy, 0.012) * (1.0 - smoothstep(0.0, 0.11 * sc, abs(lat))));
		orn = star * step(0.6, h);
	} else {
		float tall = mix(0.4, 0.2, step(0.5, fract(id * 0.5))) * sc * taper;
		float up = m_out / max(tall, 0.01);
		float hw = 0.035 * sc * (1.0 - up);
		orn = (1.0 - smoothstep(hw * 0.4, hw + 0.008, abs(lat))) * step(0.0, up) * step(up, 1.0) * (1.0 - 0.5 * up);
	}
	// two echoes of the edge, and chevrons on the trail pointing the way the blade goes
	float echo = max(ln(m_out + 0.15 * sc, 0.016) * lead * lead, ln(m_out + 0.3 * sc, 0.013) * lead * lead * lead * 0.8) * taper;
	float cmid = m_out + 0.3 * sc * taper;
	float cv = (fract(cu * 0.5 + abs(cmid) * 1.1 / (pitch * 2.0)) - 0.5) * pitch * 2.0;
	float chev = ln(cv * 0.7, 0.02) * step(abs(cmid), 0.13 * sc) * taper;
	float deco = max(orn * lead, max(echo, chev * lead * lead * 0.8)) * (0.75 + 0.5 * n);

	float a = in_arc * (1.0 - t * t) * (sweep * (core * 0.85 + body * (0.1 + 0.3 * streak) * (0.4 + 0.6 * r)) + deco * 0.55);
	ALBEDO = mix(tint, vec3(1.0), clamp(core * sweep + orn * lead * 0.5, 0.0, 1.0) * 0.3) * gain;
	ALPHA = clamp(a, 0.0, 1.0);
}

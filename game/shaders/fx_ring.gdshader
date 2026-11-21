// Expanding shock ring on the ground quad (PlaneMesh 2 x 2, so local XZ runs -1..1): it races out to the reach of
// the skill and settles there, so it also shows how far the blow went.
// One shared material: progress, colour, brightness and band width are per instance.
// kind >= 0 dresses the ring in the school of the skill (the same numbers as fx_telegraph); the ornament always
// points the way the ring travels: 0 fire: tongues leaping ahead   1 frost: long and short ice spikes
// 2 lightning: a jagged second ring   3 poison: drops thrown ahead, bubbles behind   4 shadow: hooked thorns
// 5 physical: a broken ring trailing debris lines   6 holy: rays   7 arcane: a graduated double ring
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
instance uniform float t = 0.0;          // 0 = born, 1 = gone
instance uniform vec3 tint : source_color = vec3(1.0);
instance uniform float gain = 1.0;       // brightness
instance uniform float width = 0.16;     // band half-width as a fraction of the radius
instance uniform float kind = -1.0;      // school of the ornament; below 0 = a plain ring
instance uniform float radius = 3.0;     // metres: keeps the ornament the same size in the world
instance uniform float seed = 0.0;
instance uniform float over = 1.0;       // how much larger than the reach of the ring the quad is: room for the ornament
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
	float r = length(lp) * over;
	float k = clamp(t, 0.0, 1.0);
	float e = 1.0 - pow(1.0 - k, 3.0);                 // races out, then settles at the rim
	float rad = mix(0.1, 0.93, e);
	float ang = atan(lp.y, lp.x);
	// ang * 2/pi spans -2..2, so the noise wraps without a seam
	float n = texture(noise_tex, vec2(ang * 0.63662, r * 0.8 - k * 0.7)).r;
	float w = max(width * mix(0.8, 0.3, e), 0.008);
	float band = (1.0 - smoothstep(0.0, w, abs(r - rad))) * (0.6 + 0.8 * n);
	float wake = (1.0 - smoothstep(0.0, max(rad, 0.01), r)) * 0.08 * (1.0 - k) * (1.0 - k);
	float orn = 0.0;
	if (kind > -0.5) {
		float big = max(radius, 0.3);
		float lobes = clamp(floor(big * 5.0 + 0.5), 10.0, 96.0) * (1.0 + step(6.5, kind));
		float cu = ang / 6.2832 * lobes + seed;
		float cell = floor(cu);
		float f = cu - cell;
		float id = mod(cell, lobes);                     // the same lobe on both sides of the seam behind
		float h = hash1(id + seed * 7.0);
		float dr = (r - rad) * big;                      // metres ahead of the ring (+) or behind it (-)
		float lat = (f - 0.5) * 6.2832 * rad * big / lobes;   // metres from the middle of this lobe
		float grow = smoothstep(0.0, 0.35, e);
		float scale = clamp(big * 0.3, 0.5, 1.2) * grow;
		float line = 0.028;
		if (kind < 0.5) {
			float tall = (0.4 + 0.45 * h) * (0.6 + 0.8 * n) * scale;
			float up = dr / max(tall, 0.01);
			float sway = sin(up * 4.0 + h * 6.2832 - TIME * 9.0) * up * up * 0.09;
			float hw = 0.17 * scale * smoothstep(-0.1, 0.25, up) * pow(max(1.0 - up, 0.0), 1.1);
			orn = (1.0 - smoothstep(hw * 0.4, hw + 0.01, abs(lat - sway))) * step(-0.1, up) * step(up, 1.0) * (1.0 - 0.6 * up);
		} else if (kind < 1.5) {
			float tall = mix(0.7, 0.34, step(0.5, fract(id * 0.5))) * scale;
			float up = dr / max(tall, 0.01);
			float hw = 0.095 * scale * (1.0 - up);
			float spike = (1.0 - smoothstep(hw * 0.5, hw + 0.008, abs(lat))) * step(0.0, up) * step(up, 1.0);
			float rime = ln(dr + 0.14 * scale, line);
			orn = max(spike, rime * 0.7);
		} else if (kind < 2.5) {
			float strike = floor(TIME * 12.0);
			float hz = hash1(seed * 3.0 + strike);
			float zig = (tri(cu * 0.5 + hz) - 0.5) * 0.42 * scale + (tri(cu * 1.7 + hz * 3.0) - 0.5) * 0.14 * scale;
			float arc_ring = ln(dr - 0.12 * scale - zig, line * 1.3);
			float spark = ln(lat, line) * step(0.0, dr) * step(dr, (0.25 + 0.6 * h) * scale) * step(0.5, h);
			orn = max(arc_ring, spark);
		} else if (kind < 3.5) {
			float fly = (0.16 + 0.3 * h) * scale;
			float drop = 1.0 - smoothstep(0.05, 0.095, length(vec2(lat, (dr - fly) * 0.7)));
			float tail = (1.0 - smoothstep(0.0, 0.03, abs(lat))) * step(0.0, dr) * step(dr, fly) * 0.5;
			float bub = ln(length(vec2(lat, dr + (0.18 + 0.2 * h) * scale)) - (0.05 + 0.07 * h), line * 0.8) * step(0.3, h);
			orn = max(max(drop, tail), bub);
		} else if (kind < 4.5) {
			float up = dr / (0.34 * scale);
			float thorn = step(0.0, up) * step(up, 1.0) * smoothstep(0.0, 0.06, f - mix(0.22, 0.82, up * up)) * smoothstep(0.0, 0.06, mix(0.52, 0.82, up) - f);
			float briar = ln(dr + 0.12 * scale + 0.04 * sin(cu * 3.1416), line);
			orn = max(thorn, briar * 0.7);
		} else if (kind < 5.5) {
			float trail = ln(lat, 0.02) * step(dr, 0.0) * smoothstep(-(0.4 + 0.8 * h) * scale, 0.0, dr) * step(0.25, h);
			float chip = ln(dr - 0.1 * scale, line * 1.4) * step(0.22, tri(cu * 0.5 + h));
			orn = max(trail * 0.8, chip);
		} else if (kind < 6.5) {
			float tall = mix(0.9, 0.45, step(0.5, fract(id * 0.5))) * scale;
			float up = dr / max(tall, 0.01);
			float hw = 0.065 * scale * (1.0 - up);
			float ray = (1.0 - smoothstep(hw * 0.4, hw + 0.008, abs(lat))) * step(0.0, up) * step(up, 1.0) * (1.0 - 0.5 * up);
			float halo = ln(dr + 0.13 * scale, line * 0.8);
			orn = max(ray, halo * 0.6);
		} else {
			float outer = ln(dr - 0.26 * scale, line);
			float tick = ln(lat, 0.022) * step(0.0, dr) * step(dr, 0.26 * scale * mix(1.0, 0.5, step(0.5, fract(id * 0.5))));
			float gem = ln(abs(lat) + abs(dr - 0.44 * scale) * 0.7 - 0.07 * scale, line) * step(fract(id * 0.25), 0.2);
			orn = max(max(outer, tick), gem);
		}
		orn *= (0.7 + 0.5 * n) * step(length(lp), 1.0);
	}
	float life = 1.0 - k * k;                          // stays bright, fades late
	float shape = max(band, orn * 0.85);
	ALBEDO = mix(tint, vec3(1.0), band * 0.2 * (1.0 - k)) * gain * (0.55 + shape * 0.65);
	ALPHA = clamp((shape * 0.5 + wake) * life * step(length(lp), 1.0), 0.0, 1.0);
}

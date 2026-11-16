// Ground warning shapes that fill up until the attack lands, drawn on the ground quad (PlaneMesh 2 x 2: local XZ
// runs -1..1, +Z is forward). mode: 0 circle, 1 cone, 2 rectangle, 3 ring (donut).
// Hostile shapes (soft = 0) get a hard, bright outline of constant width; friendly ones (soft = 1) are thinner and dimmer.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
instance uniform float t = 0.0;                                         // 0..1 fill progress
instance uniform vec3 tint : source_color = vec3(1.0, 0.22, 0.08);      // fill colour
instance uniform vec3 edge_tint : source_color = vec3(1.0, 0.22, 0.08); // outline colour
instance uniform float mode = 0.0;
instance uniform float arc = 1.0;                                       // cone half-angle in radians (>= 3.1 = full circle)
instance uniform float inner = 0.0;                                     // ring: inner radius as a fraction of the outer
instance uniform float soft = 0.0;
instance uniform float fade = 1.0;                                      // overall opacity (fade in / out)
instance uniform vec2 half_size = vec2(1.0);                            // world half extents in metres
varying vec2 lp;

float line_mask(float d, float w) {
	return 1.0 - smoothstep(w * 0.6, w, abs(d));
}

void vertex() {
	lp = VERTEX.xz;
}

void fragment() {
	vec2 p = lp;
	float px = mix(0.11, 0.05, soft);        // outline half-width in metres
	float inside = 0.0;
	float edge = 0.0;
	float k = 0.0;                           // 0..1 coordinate the fill travels along
	if (mode < 0.5 || mode > 2.5) {
		float big = max(half_size.x, 0.01);
		float r = length(p) * big;
		float lo = mode > 2.5 ? clamp(inner, 0.0, 0.98) * big : 0.0;
		inside = step(r, big) * step(lo, r);
		edge = line_mask(r - (big - px), px);
		if (mode > 2.5) {
			edge = max(edge, line_mask(r - (lo + px), px));
		}
		k = (r - lo) / max(big - lo, 0.001);
	} else if (mode < 1.5) {
		float big = max(half_size.x, 0.01);
		float r = length(p) * big;
		float aa = abs(atan(p.x, p.y));      // 0 straight ahead
		float in_arc = step(aa, arc);
		inside = step(r, big) * in_arc;
		edge = line_mask(r - (big - px), px) * in_arc;
		if (arc < 3.1) {
			float d_side = r * sin(clamp(arc - aa, 0.0, 1.5708));   // metres to the nearest side of the cone
			edge = max(edge, line_mask(d_side - px, px) * inside);
		}
		k = r / big;
	} else {
		vec2 dd = half_size - abs(p) * half_size;
		inside = 1.0;
		edge = line_mask(min(dd.x, dd.y) - px, px);
		k = p.y * 0.5 + 0.5;                 // fills from the near end to the far end
	}
	float filled = inside * step(k, t);
	float front = inside * (1.0 - smoothstep(0.0, 0.035, abs(k - t))) * step(0.001, t) * step(t, 0.999);
	float flash = inside * smoothstep(0.86, 1.0, t);
	float a = inside * mix(0.12, 0.012, soft)
		+ filled * mix(0.30, 0.035, soft)
		+ edge * mix(0.95, 0.42, soft)
		+ front * mix(0.45, 0.16, soft)
		+ flash * mix(0.30, 0.05, soft);
	float pulse = 1.0 + (1.0 - soft) * 0.12 * sin(TIME * 16.0);
	vec3 col = mix(tint, edge_tint, clamp(edge, 0.0, 1.0));
	col *= (1.0 + (edge * 1.1 + front * 1.0 + flash * 1.2) * mix(1.0, 0.45, soft)) * pulse;
	ALBEDO = col;
	ALPHA = clamp(a * fade, 0.0, 0.95);
}

// Ground sigils: the marks that show where and when a skill lands, drawn on the ground quad (PlaneMesh 2 x 2: local
// XZ runs -1..1, +Z is forward). mode: 0 circle, 1 cone, 2 rectangle (line / charge), 3 ring (donut).
//
// Every mark is built from the same four layers, all procedural and sized in world metres:
//   frame   a heavy outer rule, a hairline beside it and an inner rule; the band between them carries runes that
//           turn round a circle or run along a line (iron studs for a physical blow, plain graduations when small)
//   nature  the school of the skill, drawn as "arms" that grow away from the caster (or converge on the centre):
//           0 fire: swaying flame tongues and embers   1 frost: snowflake dendrites inside a faceted frame
//           2 lightning: forked bolts that jump   3 poison: hanging drops and bubbles
//           4 shadow: a star, thorns and brambles   5 physical: blade sweeps / cracks of an impact
//           6 holy: sun rays and a halo   7 arcane (the generic mark): an astrolabe of spokes and lozenges
//   flow    direction: chevrons streaming away from the caster (cone), along the travel (line), darts pointing at
//           the centre (circle), and pale arrows pointing into the safe hole of a ring
//   time    the fill travels through the shape and arrives when the blow lands; ornaments light up as it passes,
//           and a bright head runs round the rim of a circle
// Hostile marks (soft = 0) keep the hard, bright outline and a red fill; friendly ones (soft = 1) are the same
// drawing in thin, dim lines with almost no fill, so the player's own skills never look like danger.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform sampler2D glyph_tex : hint_default_black, filter_linear_mipmap, repeat_enable;   // 16 runes in one row
instance uniform float t = 0.0;                                         // 0..1 fill progress
instance uniform vec3 tint : source_color = vec3(1.0, 0.22, 0.08);      // fill colour
instance uniform vec3 edge_tint : source_color = vec3(1.0, 0.22, 0.08); // outline colour
instance uniform vec3 accent : source_color = vec3(1.0, 0.55, 0.3);     // ornament colour
instance uniform float mode = 0.0;
instance uniform float arc = 1.0;                                       // cone half-angle in radians (>= 3.1 = full circle)
instance uniform float inner = 0.0;                                     // ring: inner radius as a fraction of the outer
instance uniform float soft = 0.0;
instance uniform float fade = 1.0;                                      // overall opacity (fade in / out)
instance uniform vec2 half_size = vec2(1.0);                            // world half extents in metres
instance uniform float kind = 7.0;                                      // school of the ornament (see above)
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

float hash2(vec2 v) {
	return fract(sin(dot(v, vec2(127.1, 311.7))) * 43758.5453);
}

// Arms are drawn in their own space: a.x metres across, a.y metres along, alive from y0 (root) to y1 (tip).
// room is the free half-width at this point; id tells the arms of one mark apart. They return (lines, soft body).

// fire: a tongue of flame: it swells above the root, draws out to a point and sways
vec2 arm_fire(vec2 a, float y0, float y1, float room, float id, float w) {
	float span = max(y1 - y0, 0.05);
	float tl = (a.y - y0) / span;
	float live = step(0.0, tl) * step(tl, 1.0);
	float ph = hash1(id * 3.17 + seed * 9.0);
	float bend = sin(tl * 4.2 + ph * 6.2832 - TIME * 4.5) * tl * tl;
	float cx = bend * min(span * 0.16, room * 0.6);
	float prof = smoothstep(0.0, 0.2, tl) * pow(max(1.0 - tl, 0.0), 1.2);
	float hw = min(span * 0.3, room * 0.8) * prof;
	float dx = abs(a.x - cx) - hw;
	float outline = ln(dx, w * 1.2);
	// the heart of the flame: a smaller tongue inside
	float tl2 = tl / 0.62;
	float prof2 = smoothstep(0.0, 0.2, tl2) * pow(max(1.0 - tl2, 0.0), 1.2);
	float heart = ln(abs(a.x - cx * 0.4) - min(span * 0.13, room * 0.4) * prof2, w * 0.9) * step(tl2, 1.0);
	float glow = 1.0 - smoothstep(-hw, 0.02, dx);
	return vec2(max(outline, heart * 0.85) * live, glow * live);
}

// frost: a dendrite: a straight stem with side branches at 60 degrees, shorter toward the tip, ending in a crystal
vec2 arm_frost(vec2 a, float y0, float y1, float room, float id, float w) {
	float span = max(y1 - y0, 0.05);
	float yy = a.y - y0;
	float live = step(0.0, yy) * step(yy, span);
	float stem = ln(a.x, w);
	float pitch = clamp(span * 0.26, 0.2, 1.2);
	float node = floor(yy / pitch);
	float by = yy - node * pitch;
	float reach = min(pitch * 1.25, room * 1.5) * (1.0 - 0.55 * node * pitch / span) * step(0.5, node);
	float ax = abs(a.x);
	float along = ax * 0.866 + by * 0.5;
	float off = ax * 0.5 - by * 0.866;
	float branch = ln(off, w) * step(0.0, along) * step(along, reach);
	// twigs on the branches
	float twig = ln(by - reach * 0.25, w * 0.9) * step(reach * 0.433, ax) * step(ax, reach * 0.65);
	float cs = clamp(span * 0.05, 0.05, 0.11);
	float tip = 1.0 - smoothstep(0.8, 1.0, abs(a.x) / cs + abs(a.y - y1) / (cs * 2.2));
	return vec2(max(max(stem, branch), twig * 0.9) * live + tip, tip);
}

// lightning: a zigzag that strikes anew several times a second, with one fork
vec2 arm_bolt(vec2 a, float y0, float y1, float room, float id, float w) {
	float span = max(y1 - y0, 0.05);
	float tl = (a.y - y0) / span;
	float live = step(0.0, tl) * step(tl, 1.0);
	float strike = floor(TIME * 9.0);
	float h = hash1(id * 7.31 + seed * 31.0 + strike * 1.73);
	float h2 = hash1(id * 3.11 + strike * 0.37 + 5.0);
	float amp = min(span * 0.1, room * 0.55) * smoothstep(0.0, 0.25, tl);
	float zig = (tri(tl * 2.6 + h) - 0.5) * 2.0 * amp + (tri(tl * 7.3 + h * 5.0) - 0.5) * amp * 0.55;
	float bolt = ln(a.x - zig, w * 1.7);
	float fy = tl - (0.3 + 0.3 * h2);
	float dir = step(0.5, h) * 2.0 - 1.0;
	float fork = ln(a.x - zig - dir * fy * span * 0.6 - (tri(fy * 9.0 + h2) - 0.5) * amp * 0.6, w * 1.2) * step(0.0, fy) * step(fy, 0.3);
	float lines = max(bolt, fork * 0.85) * live;
	float flick = 0.65 + 0.35 * step(0.35, h2);
	return vec2(lines * flick, lines * 0.3);
}

// poison: a thread that swells into a hanging drop
vec2 arm_drip(vec2 a, float y0, float y1, float room, float id, float w) {
	float h = hash1(id * 5.71 + seed * 13.0);
	float span = max(y1 - y0, 0.05) * (0.55 + 0.45 * h) * (0.88 + 0.12 * sin(TIME * 1.7 + h * 6.2832));
	float hr = min(clamp(span * 0.17, 0.05, 0.3), room * 0.85);
	float yy = a.y - y0;
	float neck = max(span - hr, 0.01);
	float swell = smoothstep(0.25, 1.0, yy / neck);
	float d_neck = abs(a.x) - hr * mix(0.1, 1.0, swell * swell);
	float d_bulb = length(vec2(a.x, yy - neck)) - hr;
	float d = mix(d_neck, d_bulb, step(neck, yy));
	float live = step(0.0, yy) * step(yy, span + 0.01);
	float skin = ln(d, w);
	float body = 1.0 - smoothstep(-0.02, 0.0, d);
	float shine = ln(length(vec2(a.x, yy - neck)) - hr * 0.55, w * 0.7) * step(a.x, -hr * 0.1) * step(neck - hr * 0.2, yy);
	return vec2(max(skin, shine * 0.8) * live, body * live);
}

// shadow: a bramble: a wavering stem with hooked thorns on alternate sides
vec2 arm_thorn(vec2 a, float y0, float y1, float room, float id, float w) {
	float span = max(y1 - y0, 0.05);
	float yy = a.y - y0;
	float tl = yy / span;
	float live = step(0.0, tl) * step(tl, 1.0);
	float ph = hash1(id * 2.17 + seed * 7.0) * 6.2832;
	float pitch = clamp(span * 0.2, 0.22, 0.9);
	float wave = min(pitch * 0.22, room * 0.4);
	float x = a.x - sin(yy / pitch * 3.1416 + ph) * wave * smoothstep(0.0, 0.2, tl);
	float stem = ln(x, w * (1.6 - tl));
	float node = floor(yy / pitch);
	float by = (yy - node * pitch) / pitch;
	float side = step(0.5, fract(node * 0.5)) * 2.0 - 1.0;
	float reach = min(pitch * 0.62, room * 0.9) * (1.0 - 0.4 * tl);
	float u = x * side / max(reach, 0.01);
	// a triangle leaning forward: both edges run from the stem to a tip beyond them
	float lower = by - mix(0.12, 0.92, u * u);
	float upper = mix(0.52, 0.92, u) - by;
	float thorn = smoothstep(0.0, 0.07, lower) * smoothstep(0.0, 0.07, upper) * step(0.0, u) * step(u, 1.0);
	return vec2(max(stem, thorn) * live, thorn * live);
}

// physical: a crack that runs out from the blow, heavy at the root, with a splinter to one side
vec2 arm_crack(vec2 a, float y0, float y1, float room, float id, float w) {
	float h = hash1(id * 4.37 + seed * 11.0);
	float h2 = hash1(id * 1.91 + seed * 3.0 + 2.0);
	float span = max(y1 - y0, 0.05) * (0.6 + 0.4 * h2);
	float tl = (a.y - y0) / span;
	float live = step(0.0, tl) * step(tl, 1.0);
	float amp = min(span * 0.07, room * 0.5);
	float zig = (tri(tl * 2.2 + h) - 0.5) * 2.0 * amp * smoothstep(0.0, 0.2, tl) + (tri(tl * 5.7 + h * 3.0) - 0.5) * amp * 0.5;
	float hw = mix(w * 2.6, w * 0.5, tl);
	float cut = 1.0 - smoothstep(hw * 0.55, hw, abs(a.x - zig));
	float fy = tl - (0.25 + 0.3 * h);
	float dir = step(0.5, h2) * 2.0 - 1.0;
	float splinter = ln(a.x - zig - dir * fy * span * 0.55, w * 0.8) * step(0.0, fy) * step(fy, 0.22);
	return vec2(max(cut, splinter) * live, cut * live);
}

// physical, along a line: a long gash, pointed at both ends, with a scratch beside it
vec2 arm_gash(vec2 a, float y0, float y1, float room, float id, float w) {
	float span = max(y1 - y0, 0.05);
	float tl = (a.y - y0) / span;
	float live = step(0.0, tl) * step(tl, 1.0);
	float lens = pow(max(sin(tl * 3.1416), 0.0), 0.7);
	float hw = min(0.04 + span * 0.012, room * 0.35) * lens;
	float cut = 1.0 - smoothstep(hw * 0.5, hw + 0.012, abs(a.x));
	float sx = abs(a.x) - min(0.2 + span * 0.02, room * 0.7);
	float scratch = ln(sx, w * 0.8) * step(0.2, tl) * step(tl, 0.8);
	return vec2(max(cut, scratch * 0.7) * live, cut * live);
}

// holy: a ray of the sun, alternately long and short
vec2 arm_ray(vec2 a, float y0, float y1, float room, float id, float w) {
	float span = max(y1 - y0, 0.05);
	float odd = step(0.5, fract(id * 0.5));
	float reach = span * mix(1.0, 0.58, odd);
	float tl = (a.y - y0) / reach;
	float live = step(0.0, tl) * step(tl, 1.0);
	float hw = min(span * 0.1, room * 0.75) * (1.0 - tl) * mix(1.0, 0.8, odd);
	float dx = abs(a.x) - hw;
	float edge = ln(dx, w);
	float axis = ln(a.x, w * 0.7) * step(tl, 0.75) * (1.0 - odd);
	float body = 1.0 - smoothstep(-hw, 0.01, dx);
	return vec2(max(edge, axis * 0.6) * live, body * live * (0.6 + 0.4 * sin(TIME * 3.0 + id * 2.4)));
}

// arcane: a spoke carrying one lozenge, ending in a bead
vec2 arm_spoke(vec2 a, float y0, float y1, float room, float id, float w) {
	float span = max(y1 - y0, 0.05);
	float yy = a.y - y0;
	float live = step(0.0, yy) * step(yy, span + 0.06);
	float size = min(clamp(span * 0.1, 0.07, 0.24), room * 0.7);
	float dm = abs(a.x) + abs(yy - span * 0.6) * 0.55;
	float lozenge = ln(dm - size, w);
	float spoke = ln(a.x, w * 0.8) * step(size, dm) * step(yy, span);
	float bead = 1.0 - smoothstep(0.035, 0.06, length(vec2(a.x, yy - span)));
	return vec2(max(max(spoke, lozenge), bead) * live, (1.0 - smoothstep(size * 0.2, size, dm)) * live);
}

void vertex() {
	lp = VERTEX.xz;
}

void fragment() {
	vec2 p = lp;
	float px = mix(0.062, 0.045, soft);       // outline half-width in metres
	float w = mix(0.03, 0.02, soft);          // ornament line half-width in metres
	float big = max(half_size.x, 0.01);
	float r = length(p) * big;
	float ang = atan(p.x, p.y);               // 0 straight ahead
	vec2 q = p * half_size;                   // metres from the centre of the quad
	float lo = 0.0;                           // inner radius of a ring
	float inside = 0.0;
	float d_rim = 0.0;                        // metres inward from the nearest boundary
	float d_fr = 0.0;                         // the same, but the sides of a cone count less: their band is narrow
	float k = 0.0;                            // 0..1 coordinate the fill travels along
	float extent = big;                       // the size the ornament is fitted to
	float arc_edge = 1.0;                     // 1 where the nearest boundary carries the rune band
	float cone = 0.0;
	float rect = 0.0;
	float donut = 0.0;
	float cab = ang / 6.2832 * max(floor(6.2832 * big / 0.34), 6.0);   // turns of the cable along the rim
	if (mode < 0.5 || mode > 2.5) {
		lo = clamp(inner, 0.0, 0.98) * big * step(2.5, mode);
		inside = step(r, big) * step(lo, r);
		d_rim = big - r;
		if (lo > 0.0) {
			donut = 1.0;
			d_rim = min(big - r, r - lo);
			arc_edge = step(big - r, r - lo);
			extent = (big - lo) * 0.5;
		}
		d_fr = d_rim;
		k = (r - lo) / max(big - lo, 0.001);
	} else if (mode < 1.5) {
		float aa = abs(ang);
		inside = step(r, big) * step(aa, arc);
		d_rim = big - r;
		d_fr = d_rim;
		if (arc < 3.1) {
			cone = 1.0;
			float d_side = r * sin(clamp(arc - aa, 0.0, 1.5708));
			arc_edge = step(big - r, d_side * 2.6);
			cab = mix(r, ang * big, step(big - r, d_side)) / 0.34;
			d_fr = min(d_rim, d_side * 2.6);
			d_rim = min(d_rim, d_side);
			extent = big * sin(min(arc, 1.5708));
		}
		k = r / big;
	} else {
		rect = 1.0;
		vec2 dd = half_size - abs(q);
		inside = 1.0;
		d_rim = min(dd.x, dd.y);
		d_fr = d_rim;
		arc_edge = step(dd.x, dd.y);
		cab = mix(q.x, q.y, arc_edge) / 0.34;
		k = p.y * 0.5 + 0.5;
		extent = half_size.x;
	}
	// nothing is drawn outside the shape (but for the marks in the safe hole of a ring): leave the corners of the quad early
	if (inside < 0.5 && (donut < 0.5 || r > lo)) {
		discard;
	}
	float whole = (1.0 - cone) * (1.0 - rect);             // a circle or a ring
	float rich = smoothstep(0.3, 0.62, extent);            // small marks stay plain
	float lit = step(k, t);                                // the fill has passed here
	float grain = texture(noise_tex, q * 0.21 + vec2(seed * 3.0, seed * 7.0)).r;

	// ----- frame: heavy rule, hairline, rune band, inner rule
	float bw = clamp(extent * 0.36, 0.16, 0.8);
	float rim = ln(d_rim - px, px) * inside;
	float hair = ln(d_rim - (px * 2.0 + 0.04), w * 0.6);
	float rim2 = ln(d_fr - bw, w);
	float b0 = px * 2.0 + 0.07;
	float b1 = bw - 0.035;
	float bh = max(b1 - b0, 0.01);
	float gv = (d_fr - b0) / bh;
	float in_band = step(0.0, gv) * step(gv, 1.0) * arc_edge;
	float is_blade = step(4.5, kind) * step(kind, 5.5);
	float runes_ok = smoothstep(0.17, 0.24, bh) * (1.0 - is_blade);
	float cw = bh * mix(0.5, 1.02, runes_ok);              // cell width along the band
	float gu = 0.0;
	float turn = (0.5 - soft) * 2.0;                       // hostile bands turn one way, friendly ones the other
	if (rect > 0.5) {
		gu = (q.y - TIME * 0.8) / cw;
	} else {
		float cells = max(floor(6.2832 * (big - bw * 0.5) / cw), 4.0);
		gu = (ang / 6.2832 + TIME * 0.022 * turn / max(big * 0.25, 0.25) * whole) * cells;
	}
	float gi = floor(gu);
	float gf = gu - gi;
	float pick = floor(hash1(gi * 1.37 + seed * 17.0) * 15.99);
	float pip_cell = step(mod(gi, 5.0), 0.5);
	float rune = textureLod(glyph_tex, vec2((pick + gf) / 16.0, gv), log2(max(fwidth(gv) * 64.0, 1.0))).r * (1.0 - pip_cell);
	float pip = ln(abs(gf - 0.5) + abs(gv - 0.5) - 0.2, 0.07) * pip_cell;
	float tick = ln((gf - 0.5) * cw, w * 0.7) * step(abs(gv - 0.5), mix(0.5, 0.28, pip_cell));
	float stud = 1.0 - smoothstep(0.16, 0.26, abs(gf - 0.5) + abs(gv - 0.5) * 1.6);
	float plain = mix(tick, max(tick, stud) * (1.0 - pip_cell), is_blade);
	float band = mix(plain, max(rune, pip), runes_ok) * in_band;
	float frame = max(max(hair, rim2), band) * rich * inside;

	// ----- nature: the arms of the school, and what goes with them
	float k_fire = step(kind, 0.5);
	float k_poison = step(2.5, kind) * step(kind, 3.5);
	float k_holy = step(5.5, kind) * step(kind, 6.5);
	float y0 = max(lo + 0.14, big * 0.17 * (1.0 - donut));
	float y1 = big - bw - 0.1;
	float n_arm = 6.0 + 2.0 * step(1.4, big) + 4.0 * step(4.0, big) + 4.0 * step(8.0, big) + 8.0 * step(14.0, big);
	if (kind > 0.5 && kind < 1.5) {
		n_arm = 6.0 * (1.0 + step(4.0, big) + 2.0 * step(14.0, big));
	}
	n_arm *= 1.0 + k_fire + k_poison + k_holy;
	float w_sec = 6.2832 / n_arm;
	float spin = 0.0;
	if (cone > 0.5) {
		w_sec = 2.0 * arc / clamp(floor(2.0 * arc * big / 1.05 + 0.5) * (1.0 + k_holy), 2.0, 16.0);
		y0 = max(0.45, big * 0.14);
	} else if (rect < 0.5) {
		// only the dark and the arcane turn: slowly, against the rune band
		spin = -TIME * 0.12 * turn * (step(3.5, kind) * step(kind, 4.5) + step(6.5, kind));
	}
	float am = ang + spin;
	float sid = floor(am / w_sec + 0.5);
	float la = am - sid * w_sec;
	sid = mix(sid, mod(sid, n_arm), whole);              // the arm straight behind is one arm, not two halves
	vec2 a = vec2(r * sin(la), r * cos(la));
	float room = r * sin(min(w_sec * 0.5, 1.5708)) * 0.8;
	// lanes: the gaps between the arms (flow marks travel there)
	float lane_id = floor(am / w_sec);
	float lla = am - (lane_id + 0.5) * w_sec;
	lane_id = mix(lane_id, mod(lane_id, n_arm), whole);
	vec2 lane = vec2(r * sin(lla), r * cos(lla));
	if (rect > 0.5) {
		y0 = bw + 0.12;
		y1 = half_size.y * 2.0 - bw - 0.12;
		a = vec2(q.x, q.y + half_size.y);
		sid = 0.0;
		room = max(half_size.x - bw - 0.05, 0.02);
		lane = a;
		lane_id = 0.0;
	}
	float mid = (y0 + y1) * 0.5;
	// fire and poison: on a circle the arms stand on the inner rule and reach for the centre (alternately long and
	// short); along a cone or a line they follow one another away from the caster
	vec2 af = a;
	float f0 = y0;
	float f1 = y1;
	float fid = sid;
	if (whole > 0.5) {
		af = vec2(a.x, y0 + y1 - a.y);
		f1 = y0 + (y1 - y0) * mix(0.62, 0.36, step(0.5, fract(sid * 0.5))) * mix(1.0, 0.8, donut);
	} else {
		float links = max(floor((y1 - y0) / 1.1 + 0.5), 1.0);
		float pitch = (y1 - y0) / links;
		float link = clamp(floor((a.y - y0) / pitch), 0.0, links - 1.0);
		af = vec2(a.x, a.y - link * pitch);
		f1 = y0 + pitch * 0.96;
		fid = sid + link * 7.0;
	}
	vec2 arms = vec2(0.0);
	float extra = 0.0;                                     // lines that are not arms
	float extra_glow = 0.0;
	if (kind < 0.5) {
		arms = arm_fire(af, f0, f1, room, fid, w);
		// embers: lozenges that glint and die
		vec2 cell = floor(q / 0.5);
		float eh = hash2(cell + vec2(seed * 31.0, 7.0));
		vec2 ec = (cell + 0.5 + (vec2(eh, hash2(cell + 3.3)) - 0.5) * 0.6) * 0.5;
		float life = fract(TIME * (0.5 + eh) + eh * 9.0);
		float ember = (1.0 - smoothstep(0.025, 0.05, abs(q.x - ec.x) + abs(q.y - ec.y) * 0.6)) * step(0.5, eh) * sin(life * 3.1416);
		extra = ember;
		extra_glow = ember * 0.5;
	} else if (kind < 1.5) {
		arms = arm_frost(a, y0, y1, room, sid, w);
		// a faceted frame: straight chords from arm to arm, and a smaller crystal inside
		float facet = ln(a.y - (y1 + 0.06) * cos(w_sec * 0.5), w) * whole * (1.0 - donut);
		float facet2 = ln(lane.y - mid * cos(w_sec * 0.5) * 0.9, w * 0.8) * (1.0 - rect) * step(1.6, big);
		extra = max(facet, facet2 * 0.7);
	} else if (kind < 2.5) {
		arms = arm_bolt(a, y0 * 0.6, y1, room, sid, w);
		// the inner rule crackles
		extra = ln(d_fr - bw - 0.05 - 0.09 * tri(gu * 0.5), w * 0.9) * arc_edge;
	} else if (kind < 3.5) {
		arms = arm_drip(af, f0, f1, room, fid, w);
		// bubbles rise and burst
		vec2 cell = floor(q / 0.62);
		float bh1 = hash2(cell + vec2(seed * 17.0, 1.0));
		float life = fract(TIME * (0.35 + 0.4 * bh1) + bh1 * 5.0);
		vec2 bc = (cell + 0.5 + (vec2(bh1, hash2(cell + 5.1)) - 0.5) * 0.5) * 0.62;
		float br = (0.05 + 0.12 * hash2(cell + 9.7)) * (0.4 + 0.6 * life);
		float bd = length(q - bc);
		float bubble = ln(bd - br, w * 0.8) * (1.0 - life * life) * step(0.45, bh1);
		float gleam = (1.0 - smoothstep(0.0, br * 0.35, length(q - bc + vec2(br * 0.4, -br * 0.4)))) * step(0.45, bh1) * (1.0 - life);
		extra = max(bubble, gleam) * (1.0 - arms.y);
	} else if (kind < 4.5) {
		if (whole > 0.5) {
			// a star drawn through the mark (each chord skips two points) and a ring through its inner corners
			float ws = 6.2832 / 7.0;
			float sa = ang - spin * 2.0;
			float sl = sa - floor(sa / ws + 0.5) * ws;
			float rs = y1 + 0.06;
			float d0 = rs * cos(ws * 1.5);
			float c0 = ln(r * cos(sl) - d0, w);
			float c1 = ln(r * cos(sl - ws) - d0, w);
			float c2 = ln(r * cos(sl + ws) - d0, w);
			float star = max(c0, max(c1, c2)) * step(r, rs) * (1.0 - donut) * step(1.2, big);
			float eye = ln(r - d0 * 0.55, w * 0.8) * (1.0 - donut) * step(1.2, big) * step(0.4, tri(sa / ws * 3.5));
			extra = max(star, eye);
			// brambles only where there is no star
			arms = arm_thorn(a, y0, y1, room, sid, w) * max(donut, 1.0 - step(1.2, big));
		} else {
			arms = arm_thorn(a, y0, y1, room, sid, w);
		}
		// thorns on the inner rule, all hooked the same way round (along a line: forward)
		float th_pitch = clamp(extent * 0.3, 0.3, 0.7);
		float tu = fract(mix(ang / 6.2832 * max(floor(6.2832 * (big - bw) / th_pitch), 5.0), q.y / th_pitch, rect));
		float td = (d_fr - bw) / clamp(extent * 0.13, 0.1, 0.27);
		float crown = step(0.0, td) * step(td, 1.0) * smoothstep(0.0, 0.05, tu - mix(0.26, 0.82, td * td)) * smoothstep(0.0, 0.05, mix(0.5, 0.82, td) - tu);
		extra = max(extra, crown * arc_edge);
		extra_glow = crown * arc_edge * 0.5;
	} else if (kind < 5.5) {
		if (cone > 0.5) {
			// the sweep of a blade: three crescents across the cone, thick in the middle of the swing
			float sw = 1.0 - abs(ang) / max(arc, 0.01);
			float lens = pow(max(sin(sw * 1.5708), 0.0), 0.8) * step(0.0, sw);
			float thick = min(big * 0.4, 1.3) * lens;
			float c1 = 1.0 - smoothstep(0.3, 1.0, abs(r - mix(y0, y1, 0.4) - sw * 0.1) / max(0.07 * thick, 0.001));
			float c2 = 1.0 - smoothstep(0.3, 1.0, abs(r - mix(y0, y1, 0.66) - sw * 0.14) / max(0.1 * thick, 0.001));
			float c3 = 1.0 - smoothstep(0.3, 1.0, abs(r - mix(y0, y1, 0.9) - sw * 0.1) / max(0.07 * thick, 0.001));
			float sweep = max(c1, max(c2, c3)) * step(0.02, lens);
			arms = vec2(sweep, sweep);
		} else if (rect > 0.5) {
			arms = arm_gash(a, y0, y1, room, sid, w);
		} else {
			arms = arm_crack(a, y0 * 0.5, y1, room, sid, w);
			// the ground gives: broken rings round the point of impact
			float ring_at = floor(r / max((y1 - y0) * 0.36, 0.3));
			float seg = step(0.42, hash1(sid * 3.3 + seed * 5.0 + ring_at * 1.7));
			float shard = ln(fract(r / max((y1 - y0) * 0.36, 0.3)) - 0.5 - 0.12 * tri(am / w_sec * 2.0 + ring_at * 0.37), 0.06) * seg * step(0.5, ring_at) * step(abs(la), w_sec * 0.4);
			extra = shard * (1.0 - donut);
		}
	} else if (kind < 6.5) {
		arms = arm_ray(a, y0 * (1.0 + whole * (1.0 - donut) * 0.8), y1, room, sid, w);
		// the disc of the sun, and a halo of beads between the rays
		float disc = max(ln(r - y0 * 1.8 + 0.05, w * 1.2), ln(r - y0 * 1.8 + 0.16, w * 0.6)) * whole * (1.0 - donut);
		float halo = (1.0 - smoothstep(0.03, 0.055, length(vec2(lane.x, lane.y - mix(y0, y1, 0.86))))) * (1.0 - rect);
		extra = max(disc, halo);
	} else {
		arms = arm_spoke(a, y0, y1, room, sid, w);
		// astrolabe: a dashed ring half way out, chords from spoke to spoke, a small ring at the heart
		float dash = step(0.3, tri(am / w_sec));
		float ring_a = ln(r - mid, w * 0.8) * dash * (1.0 - rect);
		float chord = ln(lane.y - (y0 + (y1 - y0) * 0.6) * cos(w_sec * 0.5), w * 0.7) * whole * (1.0 - donut) * step(1.2, big);
		float heart = ln(r - y0 + 0.05, w) * whole * (1.0 - donut);
		extra = max(max(ring_a, heart), chord * 0.8);
	}
	float body_ok = step(bw + 0.02, d_fr) * rich * inside;
	float nature = max(arms.x, extra) * body_ok;
	float nature_glow = max(arms.y, extra_glow) * body_ok;

	// ----- flow: which way it goes
	float flow = 0.0;
	float safe_mark = 0.0;
	if (rect > 0.5) {
		// chevrons run along the travel; a double head stands at the far end
		float pitch = clamp(half_size.x * 1.5, 0.8, 2.2);
		float fy = (fract((a.y - TIME * 2.6) / pitch) - 0.5) * pitch;
		float chev = ln((fy + abs(a.x) * 0.85) * 0.76, w * 1.3);
		float chev2 = ln((fy + pitch * 0.16 + abs(a.x) * 0.85) * 0.76, w * 0.8);
		float span_ok = smoothstep(y0, y0 + 0.4, a.y) * (1.0 - smoothstep(y1 - 0.5, y1, a.y)) * step(abs(a.x), room);
		float head_y = a.y - (y1 + 0.02) + abs(a.x) * 0.85;
		float tip_mark = max(ln(head_y * 0.76, w * 2.0), ln((head_y + 0.2) * 0.76, w)) * step(abs(a.x), room);
		flow = max(max(chev, chev2 * 0.7) * span_ok, tip_mark);
	} else if (cone > 0.5) {
		// chevrons stream out between the arms
		float pitch = clamp(big * 0.3, 0.6, 1.5);
		float lh = hash1(lane_id * 2.3 + seed * 3.0);
		float fy = (fract((lane.y - TIME * 2.4) / pitch + lh) - 0.5) * pitch;
		float hwd = min(room * 0.62, 0.34);
		float chev = ln((fy + abs(lane.x) * 0.9) * 0.74, w * 1.25) * step(abs(lane.x), hwd);
		flow = chev * smoothstep(y0, y0 + 0.5, r) * (1.0 - smoothstep(y1 - 0.25, y1, r)) * step(abs(lla), w_sec * 0.5);
		// the apex: a small arc where the blow starts
		flow = max(flow, ln(r - y0 * 0.8, w));
	} else {
		// darts stand on the inner rule and point at the centre; they breathe as the blow nears
		float wd = 6.2832 / (n_arm * (1.0 + step(n_arm, 9.0)));
		float da = ang - (floor(ang / wd) + 0.5) * wd;
		float dx = r * sin(da);
		float depth = mix(-1.0, d_rim - bw, arc_edge);
		float len_d = clamp(big * 0.13, 0.12, 0.4) * (1.0 + 0.2 * sin(TIME * 6.0));
		float dart = step(0.0, depth) * (1.0 - smoothstep(0.0, 0.02, abs(dx) - (len_d - depth) * 0.2));
		// a friendly mark carries round beads where a hostile one bares teeth
		float bead = 1.0 - smoothstep(0.03, 0.055, length(vec2(dx, depth - 0.09)));
		flow = mix(dart, bead, soft);
		if (donut > 0.5) {
			// the safe hole: a pale dotted ring and arrows pointing in
			float hole = lo - r;
			float n_s = max(floor(lo * 3.6), 4.0);
			float ws2 = 6.2832 / n_s;
			float sa = ang - (floor(ang / ws2) + 0.5) * ws2;
			float sx = r * sin(sa);
			float beat = fract(TIME * 1.1) * 0.22;
			float arrow = ln((hole - 0.56 - beat + abs(sx) * 0.9) * 0.74, 0.042) * step(abs(sx), min(0.24, lo * 0.2)) * step(0.0, hole) * (1.0 - smoothstep(0.17, 0.22, beat));
			float dots = (1.0 - smoothstep(0.018, 0.034, abs(hole - 0.12))) * step(0.5, tri(ang / ws2 * 4.0));
			safe_mark = max(arrow, dots) * smoothstep(0.5, 0.8, lo) * (1.0 - soft);
		}
	}
	flow *= rich * inside * step(bw - 0.01, d_fr);

	// ----- time
	float front = inside * (1.0 - smoothstep(0.0, 0.035, abs(k - t))) * step(0.001, t) * step(t, 0.999);
	float flash = inside * smoothstep(0.86, 1.0, t);
	// the rim of a circle is run round by a bright head that closes the circle as the blow lands
	float turned = fract(ang / 6.2832 + 0.5);
	float swept = mix(lit, step(turned, t), whole);
	float head = rim * (1.0 - smoothstep(0.0, 0.25, abs(turned - t) * 6.2832 * big)) * whole * step(0.001, t) * step(t, 0.999);

	float orn = clamp(max(max(frame, nature), flow), 0.0, 1.0) * (0.72 + 0.4 * grain);
	float veil = 0.7 + 0.6 * grain;                        // the fill is smoke, not paint
	float hdr = mix(1.0, 0.45, soft);
	float pulse = 1.0 + (1.0 - soft) * 0.12 * sin(TIME * 16.0);
	// Layers, back to front (acc is premultiplied). The veil is thin, gathers toward the rim and thickens where the
	// fill has passed: the drawing carries the look, and marks lying over one another do not add up to one red slab.
	float edge_glow = 1.0 - smoothstep(0.0, max(bw * 1.6, 0.4), d_rim);
	float a_veil = inside * (mix(0.04, 0.008, soft) + edge_glow * mix(0.07, 0.012, soft) + lit * mix(0.11, 0.03, soft) + flash * mix(0.14, 0.04, soft)) * veil;
	vec3 acc = tint * (1.0 + flash * 0.7 * hdr) * a_veil;
	float cov = a_veil;
	// a hostile mark lays a dark plate under its band: the runes stay legible on top of other marks, the plate keeps
	// neighbouring marks apart, and no friendly mark has one
	float a_plate = step(b0 - 0.03, d_fr) * step(d_fr, bw) * inside * rich * (1.0 - soft) * 0.45;
	acc = acc * (1.0 - a_plate) + vec3(0.03, 0.007, 0.004) * a_plate;
	cov = cov * (1.0 - a_plate) + a_plate;
	float a_orn = clamp(orn * mix(mix(0.5, 0.95, lit), mix(0.2, 0.5, lit), soft) + nature_glow * mix(0.1, 0.04, soft) * (0.4 + 0.6 * lit), 0.0, 1.0);
	acc = acc * (1.0 - a_orn) + accent * (1.0 + (lit * 0.18 + flash * 0.9) * hdr) * a_orn;
	cov = cov * (1.0 - a_orn) + a_orn;
	float a_front = front * mix(0.45, 0.16, soft);
	acc = acc * (1.0 - a_front) + tint * (1.0 + hdr) * a_front;
	cov = cov * (1.0 - a_front) + a_front;
	// the rim of a hostile mark is a twisted cable (bright and dim strands), a friendly one a plain thin line
	float cable = mix(0.62 + 0.38 * smoothstep(0.3, 0.7, tri(cab + d_rim * 4.0)), 1.0, soft);
	float a_rim = clamp(rim * mix(0.95, 0.42, soft) * (0.82 + 0.18 * swept) * (0.75 + 0.25 * cable) + head * 0.5, 0.0, 1.0);
	acc = acc * (1.0 - a_rim) + edge_tint * (cable + (0.3 + 0.55 * swept + head * 2.0 + flash) * hdr) * a_rim;
	cov = cov * (1.0 - a_rim) + a_rim;
	// the safe hole of a ring is marked in pale gold, never in the danger colour
	float a_safe = safe_mark * 0.6;
	acc = acc * (1.0 - a_safe) + vec3(1.0, 0.9, 0.62) * a_safe;
	cov = cov * (1.0 - a_safe) + a_safe;
	ALBEDO = acc / max(cov, 0.001) * pulse;
	ALPHA = clamp(cov * fade, 0.0, 0.95);
}

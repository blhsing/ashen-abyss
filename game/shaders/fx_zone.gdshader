// Persistent ground area (burning ground, frost, storms, pools of venom or shadow, consecration) on the ground quad
// (PlaneMesh 2 x 2: local XZ runs -1..1). It is the floor layer of the effect only: flames, mist, smoke and sparks
// are particles standing on it. Premultiplied blending lets one quad both darken the ground (scorch, pool) and add
// light on top (glowing cracks, arcs, rime).
//   kind: 0 fire, 1 frost, 2 lightning, 3 poison, 4 shadow, 5 dust / physical, 6 plain glow.
// Friendly areas end in a ragged, soft edge; hostile ones are a true circle with a hard, bright rim in the danger
// colour, so the line the player must not cross is exact.
shader_type spatial;
render_mode unshaded, blend_premul_alpha, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
instance uniform vec3 tint : source_color = vec3(1.0, 0.4, 0.1);
instance uniform vec3 edge_tint : source_color = vec3(1.0, 0.22, 0.08);
instance uniform float fade = 1.0;
instance uniform float hostile = 0.0;
instance uniform float seed = 0.0;
instance uniform float radius = 3.0;     // metres: keeps the rim width and the pattern scale constant in the world
instance uniform float kind = 0.0;
varying vec2 lp;

void vertex() {
	lp = VERTEX.xz;
}

void fragment() {
	float r = length(lp);
	vec2 wp = lp * radius;
	float ang = atan(lp.y, lp.x);
	float t = TIME;
	float sd = seed * 7.31;

	// two drifting layers of noise in world metres; where they agree lie the cracks and veins
	float n1 = texture(noise_tex, wp * 0.085 + vec2(sd + t * 0.012, -t * 0.02)).r;
	float n2 = texture(noise_tex, wp * 0.19 - vec2(t * 0.025, sd + t * 0.01)).r;
	float n3 = texture(noise_tex, wp * 0.41 + vec2(sd * 0.7, t * 0.05)).r;
	float veins = 1.0 - smoothstep(0.0, 0.05, abs(n1 - n2));
	float blot = smoothstep(0.42, 0.7, n1 * 0.6 + n2 * 0.5);

	// outline
	float n_edge = texture(noise_tex, vec2(ang * 0.31831 + sd, r * 0.6 + sd)).r;
	float soft_body = 1.0 - smoothstep(0.5 + 0.34 * n_edge, 0.84 + 0.16 * n_edge, r);
	float px = 0.075 / max(radius, 0.1);
	float hard_body = 1.0 - smoothstep(1.0 - px * 0.5, 1.0, r);
	float body = mix(soft_body, hard_body, hostile);
	float rim_hard = 1.0 - smoothstep(px * 0.55, px * 1.1, abs(r - (1.0 - px * 1.2)));
	float inner_band = smoothstep(0.62, 1.0, r) * hard_body;
	float rim_soft = (1.0 - smoothstep(0.0, 0.09, abs(r - (0.72 + 0.16 * n_edge)))) * 0.35;

	float dark = 0.0;        // how much of the ground is covered
	vec3 pool = vec3(0.0);   // colour of what covers it
	vec3 glow = vec3(0.0);   // light added on top
	if (kind < 0.5) {
		// fire: scorched earth, embers breathing in the cracks, heat shimmering toward the centre
		float breathe = 0.7 + 0.3 * sin(t * 2.3 + n1 * 14.0);
		dark = 0.62 * body;
		pool = vec3(0.012, 0.008, 0.006);
		glow = tint * (veins * 1.9 * breathe + blot * 0.35 * (1.0 - r * 0.6)) * body;
		glow += vec3(1.0, 0.75, 0.4) * veins * blot * 0.9 * breathe * body;
	} else if (kind < 1.5) {
		// frost: a skin of rime with bright fracture lines
		float crystals = smoothstep(0.55, 0.8, n3) * 0.5 + veins * 0.9;
		dark = 0.3 * body;
		pool = tint * 0.22 + vec3(0.1, 0.12, 0.14);
		glow = (tint * 0.6 + vec3(0.35)) * crystals * body * (0.75 + 0.25 * sin(t * 1.3 + n2 * 9.0));
	} else if (kind < 2.5) {
		// lightning: charged ground, arcs that jump several times a second
		float jump = floor(t * 11.0);
		float a1 = texture(noise_tex, wp * 0.16 + vec2(jump * 0.37 + sd, jump * 0.61)).r;
		float a2 = texture(noise_tex, wp * 0.16 + vec2(jump * 0.19, jump * 0.83 + sd) + vec2(0.5)).r;
		float arcs = 1.0 - smoothstep(0.0, 0.022, abs(a1 - a2));
		dark = 0.25 * body;
		pool = tint * 0.06;
		glow = (tint * 0.7 + vec3(0.5)) * arcs * 1.6 * body + tint * blot * 0.12 * body;
	} else if (kind < 3.5) {
		// poison: a slick pool with a sheen and slow bubbles
		float bubbles = smoothstep(0.78, 0.86, texture(noise_tex, wp * 0.55 + vec2(sd, -t * 0.09)).r);
		dark = 0.72 * body;
		pool = tint * (0.07 + 0.1 * n2);
		glow = tint * (blot * 0.22 + veins * 0.35 + bubbles * 0.8) * body;
	} else if (kind < 4.5) {
		// shadow: a hole in the light with violet wisps crawling along its edge
		float crawl = smoothstep(0.5, 0.95, r + (n2 - 0.5) * 0.5) * soft_body;
		dark = 0.86 * body;
		pool = vec3(0.004, 0.002, 0.008);
		glow = tint * (veins * 0.55 + crawl * 0.45) * body;
	} else if (kind < 5.5) {
		// dust: churned ground
		dark = 0.22 * body * (0.5 + n2);
		pool = tint * 0.08;
		glow = tint * 0.1 * blot * body;
	} else {
		// plain glow: a pool of light
		dark = 0.0;
		glow = tint * (0.03 + blot * 0.1 + veins * 0.3) * body * (0.5 + 0.5 * r);
	}

	// rims
	float pulse = 0.85 + 0.15 * sin(t * 6.0);
	glow += mix(tint * rim_soft * 0.9, edge_tint * (rim_hard * 2.3 * pulse + inner_band * inner_band * 0.5), hostile);
	dark = max(dark, rim_hard * 0.5 * hostile);

	float a = clamp(dark * fade, 0.0, 1.0);
	ALBEDO = pool * a + glow * fade;
	ALPHA = a;
}

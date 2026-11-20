// The veil in a gateway: a sheet of slowly rising light that exactly fills the rectangular opening of the gate
// (QuadMesh 2 x 2 in local XY, scaled to half the opening; half_size = those half extents in metres).
// Heat shimmer in two climbing layers, thin embers carried up through it, a soft bright seam where it meets the
// jambs and the lintel, thinning out at the threshold where the glow on the ground takes over. Seen from both sides.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
instance uniform vec3 tint : source_color = vec3(1.0, 0.55, 0.22);
instance uniform float gain = 1.0;
instance uniform float fade = 1.0;
instance uniform vec2 half_size = vec2(1.25, 1.5);
varying vec2 lp;

void vertex() {
	lp = VERTEX.xy;
}

void fragment() {
	vec2 m = lp * half_size;                                  // metres from the centre of the opening
	float t = TIME;
	float up = (m.y + half_size.y) / (2.0 * half_size.y);     // 0 at the threshold, 1 under the lintel
	// metres to the frame: the jambs and the lintel (the threshold is not an edge of light)
	float e = min(half_size.x - abs(m.x), half_size.y - m.y);
	float seam = exp(-max(e, 0.0) / 0.17);
	// shimmer: two sheets of noise climbing at different paces, bent sideways by a slower third
	float bend = texture(noise_tex, m * vec2(0.11, 0.07) + vec2(0.0, -t * 0.03)).r - 0.5;
	float n1 = texture(noise_tex, vec2(m.x * 0.16 + bend * 0.25, m.y * 0.09 - t * 0.085)).r;
	float n2 = texture(noise_tex, vec2(m.x * 0.37 - bend * 0.2 + 0.31, m.y * 0.21 - t * 0.17)).r;
	float sheet = smoothstep(0.3, 0.8, n1 * 0.62 + n2 * 0.5);
	// embers: thin bright streaks carried upward
	float n3 = texture(noise_tex, vec2(m.x * 1.9 + 0.7, m.y * 0.33 - t * 0.42)).r;
	float embers = smoothstep(0.76, 0.92, n3) * (0.35 + 0.65 * sheet);
	float foot = smoothstep(0.0, 0.16, up);
	float body = (0.22 + 0.5 * sheet) * mix(0.8, 1.0, up);
	vec3 col = tint * (body + seam * 0.85) + mix(tint, vec3(1.0), 0.45) * (embers * 0.8 + seam * seam * 0.3);
	ALBEDO = col * gain;
	ALPHA = clamp((body + seam * 0.8 + embers) * foot * fade, 0.0, 1.0);
}

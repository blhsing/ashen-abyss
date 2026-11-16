// Flames of torches, braziers, candles and lanterns: one camera-facing quad per flame, all flames of a map sector
// batched in a MultiMesh. INSTANCE_CUSTOM = (height in metres, random phase, width factor, brightness; 0 = 1).
// The quad is three flame-heights square: the flame itself burns in the middle column, the rest carries a soft halo
// so that every fire reads as a light source from the high camera, even a candle a few pixels tall.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform vec3 color_core : source_color = vec3(1.0, 0.82, 0.48);
uniform vec3 color_edge : source_color = vec3(1.0, 0.36, 0.07);
uniform float energy = 3.2;
uniform float halo = 0.26;
instance uniform float fade = 1.0;       // whole batch in and out (burning ground); level flames leave it at 1

varying float phase;
varying float wfac;
varying float hfac;
varying float bright;

// Quad size in flame heights and where the flame's foot sits on it (0 = bottom edge). Lone flames need the room for
// their halo; fields of flames (burning ground, great fires) use a tight quad without halo: a quarter of the fill.
uniform float span = 3.0;
uniform float base = 0.3;

void vertex() {
	vec3 origin = MODEL_MATRIX[3].xyz;
	float size = max(INSTANCE_CUSTOM.x, 0.01);
	wfac = max(INSTANCE_CUSTOM.z, 0.1);
	hfac = clamp(0.32 / size, 0.2, 1.0);
	bright = INSTANCE_CUSTOM.w > 0.001 ? INSTANCE_CUSTOM.w : 1.0;
	phase = INSTANCE_CUSTOM.y;
	vec3 right = normalize(INV_VIEW_MATRIX[0].xyz);
	vec3 up = normalize(mix(vec3(0.0, 1.0, 0.0), INV_VIEW_MATRIX[1].xyz, 0.6));
	float h = VERTEX.y + 0.5 - base;                      // 0 at the foot of the flame
	vec3 wp = origin + right * VERTEX.x * size * span + up * h * size * span;
	POSITION = PROJECTION_MATRIX * (VIEW_MATRIX * vec4(wp, 1.0));
}

void fragment() {
	// flame space: y 0 at the foot, 1 at the tip; x -1..1 across the flame's width
	float y = ((1.0 - UV.y) - base) * span;
	float xm = (UV.x - 0.5) * span;                       // in flame heights
	float x = xm / (0.33 * wfac);
	float t = TIME * 1.6 + phase * 17.0;
	float lean = sin(TIME * 3.1 + phase * 6.2831) * 0.16 * clamp(y, 0.0, 1.0);
	x += lean;

	float body = 0.0;
	float core = 0.0;
	if (y > 0.0 && y < 1.05 && abs(x) < 1.3) {
		float n1 = texture(noise_tex, vec2(x * 0.45 + phase, y * 0.7 - t * 0.55)).r;
		float n2 = texture(noise_tex, vec2(x * 0.95 - phase, y * 1.5 - t * 0.9)).r;
		float n = n1 * 0.65 + n2 * 0.45;
		float width = max(mix(0.8, 0.02, pow(y, 1.25)) * (0.72 + n * 0.6), 0.02);
		float off = abs(x + (n1 - 0.5) * 0.55 * y);
		body = 1.0 - smoothstep(width * 0.25, width, off);
		body *= smoothstep(0.0, 0.08, y);
		body *= 1.0 - smoothstep(0.55, 1.0, y + (1.0 - n) * 0.35);
		core = body * (1.0 - smoothstep(0.05, 0.75, y)) * (1.0 - smoothstep(0.0, 0.6, abs(x)));
	}

	// halo: a soft ball of glow centred a third of the way up the flame, breathing with it
	vec2 hc = vec2(xm, y - 0.35);
	float r2 = dot(hc, hc);
	float breathe = 0.86 + 0.14 * sin(TIME * 9.0 + phase * 31.0) * sin(TIME * 5.3 + phase * 11.0);
	float glow = exp(-r2 * 3.2) * halo * breathe * hfac;
	glow *= 1.0 - smoothstep(1.0, 1.45, sqrt(r2));        // reaches zero before the quad's edge

	vec3 col = mix(color_edge, color_core, clamp(core * 1.5, 0.0, 1.0)) * energy * (0.55 + core) * clamp(body, 0.0, 1.0);
	col += color_edge * glow;
	ALBEDO = col * fade * bright;
	ALPHA = 1.0;
}

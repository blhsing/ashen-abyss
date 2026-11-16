// Portal surface: a disc of slowly spiralling light on an upright quad (QuadMesh 2 x 2 in local XY).
// upright = 1 keeps it standing and turns it toward the camera around the vertical axis; 0 leaves it as placed.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
instance uniform vec3 tint : source_color = vec3(0.4, 0.7, 1.0);
instance uniform float gain = 1.0;
instance uniform float fade = 1.0;
instance uniform float upright = 1.0;
varying vec2 lp;

void vertex() {
	lp = VERTEX.xy;
	if (upright > 0.5) {
		vec3 sc = vec3(length(MODEL_MATRIX[0].xyz), length(MODEL_MATRIX[1].xyz), length(MODEL_MATRIX[2].xyz));
		vec3 right = normalize(cross(vec3(0.0, 1.0, 0.0), INV_VIEW_MATRIX[2].xyz));
		vec3 fwd = normalize(cross(right, vec3(0.0, 1.0, 0.0)));
		mat4 m = mat4(vec4(right * sc.x, 0.0), vec4(0.0, sc.y, 0.0, 0.0), vec4(fwd * sc.z, 0.0), MODEL_MATRIX[3]);
		MODELVIEW_MATRIX = VIEW_MATRIX * m;
		MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
	}
}

void fragment() {
	float r = length(lp);
	float ang = atan(lp.y, lp.x);
	// ang / pi spans -1..1: whole multiples keep the spiral seamless around the disc
	float sw = ang * 0.31831 + r * 1.3 - TIME * 0.22;
	float n1 = texture(noise_tex, vec2(sw, r * 0.9 - TIME * 0.12)).r;
	float n2 = texture(noise_tex, vec2(-sw * 2.0 + 0.37, r * 1.7 + TIME * 0.07)).r;
	float n = smoothstep(0.25, 0.85, n1 * 0.65 + n2 * 0.55);
	float disc = 1.0 - smoothstep(0.86, 0.98, r);
	float rim = 1.0 - smoothstep(0.0, 0.09, abs(r - 0.88));
	float core = 1.0 - smoothstep(0.0, 0.5, r);
	vec3 col = mix(tint, vec3(1.0), rim * 0.2) * (0.25 + n * 0.75 + rim * 0.7) + tint * core * 0.2;
	ALBEDO = col * gain;
	ALPHA = clamp((disc * (0.25 + n * 0.6) + rim * 0.9) * fade * step(r, 1.0), 0.0, 1.0);
}

// Ghost matter: translucent, fresnel-lit, drifting noise. Used for `spectral_*` materials and spirit summons.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform vec3 color : source_color = vec3(0.5, 0.8, 1.0);
uniform float energy = 0.7;      // a ghost glows, but against the dark level a full white body read as a lamp (4.8 x its ground)
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform sampler2D albedo_tex : source_color, hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float detail = 0.0;      // 1 when the model carries an albedo map: its light and dark shape the ghost
// same slots as char.gdshader (see there)
instance uniform vec3 tint : source_color, instance_index(0) = vec3(1.0);
instance uniform float flash : instance_index(1) = 0.0;
instance uniform float dissolve : instance_index(4) = 0.0;
varying vec3 wpos;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	VERTEX += NORMAL * 0.015 * sin(TIME * 3.0 + VERTEX.y * 6.0);
}

void fragment() {
	float fres = pow(1.0 - abs(dot(NORMAL, VIEW)), 1.6);
	float n = texture(noise_tex, wpos.xz * 0.5 + vec2(wpos.y * 0.6 - TIME * 0.25)).r;
	float a = (0.18 + fres * 0.9) * (0.55 + n * 0.7);
	a *= 1.0 - smoothstep(0.0, 1.0, dissolve * 1.3 - n * 0.3);
	float lum = dot(texture(albedo_tex, UV).rgb, vec3(0.299, 0.587, 0.114));
	float shape = mix(1.0, 0.45 + 1.1 * smoothstep(0.02, 0.6, lum), detail);
	ALBEDO = color * tint * energy * shape * (1.0 + min(flash, 1.0) * 1.2);
	ALPHA = clamp(a * 0.8 * mix(1.0, 0.7 + 0.5 * shape, detail), 0.0, 1.0);
}

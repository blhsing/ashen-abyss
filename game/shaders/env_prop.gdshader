// Imported environment props (glTF PBR materials converted at load): theme grading, settled snow / ash / moss on
// up-facing surfaces, a little per-instance value variation, gentle sway for plants, and the see-through cut that
// keeps the hero (and the enemy being aimed at) visible behind tall props:
//   - a wide disc round the line lens → chest, above stump height: canopies, the tops of tall rocks and spires;
//   - the figure itself: whatever part of a tall prop covers the body between feet and head goes, down to the ground
//     (a trunk, a boulder, a pillar right in front of the hero). Low props (crates, graves, bushes) are never cut.
// Partly cut fragments also sink into shadow, so a dissolving trunk never turns into a bright grainy column.
shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx;

global uniform vec3 hero_pos;
global uniform vec4 focus_pos;               // xyz = the enemy under the cursor / being attacked, w = 0..1 (CameraRig)

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec4 albedo : source_color = vec4(1.0);
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float normal_scale = 1.0;
uniform sampler2D orm_tex : hint_default_white, filter_linear_mipmap, repeat_enable;   // r = ao, g = roughness, b = metallic
uniform float roughness = 0.8;
uniform float metallic = 0.0;
uniform float ao_amount = 0.0;
uniform sampler2D emission_tex : source_color, hint_default_black, filter_linear_mipmap, repeat_enable;
uniform vec3 emission : source_color = vec3(0.0);
uniform float emission_energy = 0.0;
uniform float alpha_cut = 0.0;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform vec3 tint = vec3(1.0);               // linear multiplier (theme grading)
uniform float desaturate : hint_range(0.0, 1.0) = 0.0;
uniform vec3 top_color : source_color = vec3(1.0);
uniform float top_amount = 0.0;
uniform float sway = 0.0;                    // metres of wind sway at 1 m above the prop origin
uniform float cut_radius = 2.6;
uniform float cut_floor = 1.1;               // the wide disc never cuts below this height (stumps and bases stay)
uniform float prop_height = 3.0;             // height of the model at scale 1 (metres); props under 2 m are never cut
uniform float disc_from = 0.0;               // trees: share of their height the wide disc leaves alone (the trunk)
uniform float lum_floor = 0.0;               // near-black albedo is lifted towards this (linear), so basalt keeps a surface
uniform vec3 sky_fill = vec3(0.0);           // extra ambient on up-facing rock (the sky is above, not all around)
uniform vec3 sky_rim = vec3(0.0);            // faint grazing-angle sheen in the sky colour: a readable silhouette
uniform vec3 flame_local = vec3(0.0);        // light props: where the prop's own light stands, in mesh space
uniform float flame_guard = 0.0;             // metres; inside it the surface answers as if that light stood this far away

varying vec3 wpos;
varying vec3 wnrm;
varying float vary;
varying float prop_h;
varying float own;

void vertex() {
	vec3 origin = MODEL_MATRIX[3].xyz;
	vary = fract(sin(dot(origin.xz, vec2(12.9898, 78.233))) * 43758.5453);
	prop_h = prop_height * length(MODEL_MATRIX[1].xyz);
	// A lantern's light sits a hand away from its own cage; with inverse-square falloff that burnt the cage to white
	// pixels. The cage's response is scaled by (distance / guard)^2, which cancels the falloff inside the guard.
	own = 1.0;
	if (flame_guard > 0.0) {
		vec3 dl = (VERTEX - flame_local) * length(MODEL_MATRIX[1].xyz);
		own = clamp(dot(dl, dl) / (flame_guard * flame_guard), 0.0, 1.0);
	}
	// SWAY_BEGIN (the level builder strips this block for everything that is not a plant: a vertex shader that reads
	// TIME marks its meshes as animated, and animated meshes force every shadow map they touch to be redrawn each frame)
	if (sway > 0.0) {
		float h = max(VERTEX.y, 0.0);
		float ph = TIME * (0.9 + vary * 0.6) + origin.x * 0.35 + origin.z * 0.27;
		VERTEX.x += sin(ph) * sway * h;
		VERTEX.z += cos(ph * 0.83 + 1.7) * sway * h * 0.7;
	}
	// SWAY_END
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wnrm = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

// 1 = keep, 0 = fully cut, for one figure standing at `feet`.
float see_through(vec3 wp, vec3 cam, vec3 feet, float radius, float body, float stump) {
	vec3 cp = wp - cam;
	// nothing may loom right in front of the lens (tree tops and tall rocks under the camera)
	float lens = smoothstep(2.2, 5.0, length(cp));

	// the wide disc round the line lens → chest
	vec3 ch = feet + vec3(0.0, 1.0, 0.0) - cam;
	float len = max(length(ch), 0.001);
	vec3 dir = ch / len;
	float t = dot(cp, dir);
	float front = len - t;                              // how far in front of the figure (towards the camera)
	float d = length(cp - dir * t);
	float a = smoothstep(radius * 0.74, radius, d);
	a = max(a, 1.0 - smoothstep(stump, stump + 0.3, wp.y));
	a = max(a, 1.0 - smoothstep(0.45, 1.1, front));

	// the figure: follow the ray lens → fragment on to the upright plane the figure stands in
	vec2 hd = ch.xz / max(length(ch.xz), 0.001);
	float dh = dot(ch.xz, hd);
	float df = dot(cp.xz, hd);
	float ahead = dh - df;                              // metres this fragment stands in front of that plane
	if (ahead > 0.0 && df > 0.1) {
		vec3 q = cam + cp * (dh / df);
		float side = dot(q.xz - feet.xz, vec2(-hd.y, hd.x));
		float up = max(max(q.y - 1.9, -0.15 - q.y), 0.0);
		float fig = smoothstep(0.6, 0.95, length(vec2(side, up)));
		fig = max(fig, 1.0 - smoothstep(0.12, 0.4, ahead));
		a = min(a, fig);
	}
	return mix(1.0, a, body) * lens;
}

void fragment() {
	vec4 a = texture(albedo_tex, UV) * albedo;
	vec3 orm = texture(orm_tex, UV).rgb;
	vec3 nm = texture(normal_tex, UV).rgb;
	vec3 em = texture(emission_tex, UV).rgb;
	float n_lo = texture(noise_tex, wpos.xz * 0.05 + wpos.yy * 0.031).r;

	vec3 col = a.rgb * tint * mix(0.84, 1.1, vary);
	float lum = dot(col, vec3(0.299, 0.587, 0.114));
	col = mix(col, vec3(lum), desaturate);
	// a soft knee instead of a clamp: dark texels are lifted, their differences survive
	col += vec3(lum_floor) * (1.0 - smoothstep(0.0, lum_floor * 2.0 + 0.0001, lum));

	vec3 wn = normalize(wnrm);
	float rough = clamp(roughness * orm.g, 0.04, 1.0);
	float cover = smoothstep(0.45, 0.8, wn.y + (n_lo - 0.5) * 0.6) * top_amount;
	col = mix(col, top_color, cover);
	rough = mix(rough, 0.9, cover * 0.7);

	vec3 cam = CAMERA_POSITION_WORLD;
	float keep = 1.0;
	if (!IN_SHADOW_PASS) {
		// only props that can hide a figure are opened; a tree keeps its trunk (a thin column hides nothing but the
		// figure right behind it, and that part goes with the figure's outline) and gives up its crown
		float tall = step(2.0, prop_h);
		float stump = max(cut_floor, disc_from * prop_h);
		keep = see_through(wpos, cam, hero_pos, cut_radius, tall, stump);
		if (focus_pos.w > 0.01) {
			keep = min(keep, mix(1.0, see_through(wpos, cam, focus_pos.xyz, cut_radius * 0.7, tall, stump), focus_pos.w));
		}
	}
	// what is left of a dissolving prop is a shadow of itself: no highlights, a third of its colour
	float solid = smoothstep(0.0, 0.95, keep);

	float up = clamp(wn.y * 0.5 + 0.5, 0.0, 1.0);
	float graze = 1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
	graze *= graze * graze;
	if (!FRONT_FACING) {
		graze = 0.0;                                   // two-sided cards: no sheen on the side turned away
	}

	ALBEDO = col * mix(0.3, 1.0, solid) * own;
	ROUGHNESS = mix(1.0, rough, solid);
	METALLIC = clamp(metallic * orm.b, 0.0, 1.0) * (1.0 - cover) * solid;
	SPECULAR = 0.5 * solid * own;
	AO = mix(1.0, orm.r, ao_amount);
	NORMAL_MAP = nm;
	NORMAL_MAP_DEPTH = normal_scale;
	EMISSION = ((em + emission) * emission_energy + col * sky_fill * up * up + sky_rim * graze * (0.35 + 0.65 * up)) * solid;

	bool gone = a.a < alpha_cut;
	if (!IN_SHADOW_PASS) {
		float dither = fract(52.9829189 * fract(dot(FRAGCOORD.xy, vec2(0.06711056, 0.00583715))));
		gone = gone || keep < dither;
	}
	if (gone) {
		discard;
	}
}

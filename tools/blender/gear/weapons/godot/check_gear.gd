# Headless check of imported gear models:  Godot --headless --path Y:\game -s <this file> -- <id> [<id> ...]
# Prints, per model: node tree, AABB (file axes: +Z up the weapon), surfaces, materials, texture paths, node extras.
extends SceneTree

func _init() -> void:
	var ids := OS.get_cmdline_user_args()
	for id in ids:
		var path := "res://assets/gear/weapons/%s.glb" % id
		if not ResourceLoader.exists(path):
			print("MISSING ", path)
			continue
		var ps: PackedScene = load(path)
		if ps == null:
			print("LOAD FAILED ", path)
			continue
		var root := ps.instantiate()
		print("== ", id, "  root=", root.name, " (", root.get_class(), ")")
		_walk(root, 1)
		root.free()
	quit()

func _tex(t: Texture2D) -> String:
	if t == null:
		return "-"
	return "%s %dx%d %s" % [t.resource_path.get_file(), t.get_width(), t.get_height(), t.get_class()]

func _walk(n: Node, depth: int) -> void:
	var pad := "  ".repeat(depth)
	var line := "%s%s (%s)" % [pad, n.name, n.get_class()]
	if n is Node3D:
		line += " xform=%s" % [str((n as Node3D).transform)]
	if n.has_meta("extras"):
		line += " extras=%s" % [str(n.get_meta("extras"))]
	print(line)
	if n is MeshInstance3D:
		var mi := n as MeshInstance3D
		var m := mi.mesh
		print(pad, "  aabb=", m.get_aabb(), " surfaces=", m.get_surface_count())
		for s in m.get_surface_count():
			var mat := m.surface_get_material(s)
			var arr := m.surface_get_arrays(s)
			var nv: int = (arr[Mesh.ARRAY_VERTEX] as PackedVector3Array).size()
			var ni: int = (arr[Mesh.ARRAY_INDEX] as PackedInt32Array).size() if arr[Mesh.ARRAY_INDEX] != null else nv
			var has_tan: bool = arr[Mesh.ARRAY_TANGENT] != null
			if mat is BaseMaterial3D:
				var b := mat as BaseMaterial3D
				print(pad, "  surf ", s, " '", b.resource_name, "' verts=", nv, " tris=", ni / 3, " tangents=", has_tan,
					" albedo=", _tex(b.albedo_texture), " normal=", _tex(b.normal_texture) if b.normal_enabled else "-",
					" orm(r/m/ao)=", _tex(b.roughness_texture), "/", _tex(b.metallic_texture), "/", _tex(b.ao_texture) if b.ao_enabled else "-",
					" emission=", (_tex(b.emission_texture) + " x%.1f" % b.emission_energy_multiplier) if b.emission_enabled else "-",
					" cull=", b.cull_mode, " vcol=", b.vertex_color_use_as_albedo)
			else:
				print(pad, "  surf ", s, " material=", mat)
	for c in n.get_children():
		_walk(c, depth + 1)

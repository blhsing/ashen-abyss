extends SceneTree
# headless check of imported gear files: dependencies (shared textures) and node / mesh / material structure
#   godot --headless --path Y:\game --script Y:\tools\blender\gear\armor\gd_deps.gd -- <res path> [...]
func _init():
    var args = OS.get_cmdline_user_args()
    for p in args:
        print("== ", p, " exists=", ResourceLoader.exists(p))
        if not ResourceLoader.exists(p):
            continue
        for d in ResourceLoader.get_dependencies(p):
            print("   dep ", d)
        var ps = load(p)
        if ps == null:
            print("   LOAD FAILED")
            continue
        var inst = ps.instantiate()
        _walk(inst, 1)
        inst.free()
    quit()

func _walk(n, depth):
    var s = "   ".repeat(depth) + n.name + " [" + n.get_class() + "]"
    if n is MeshInstance3D and n.mesh != null:
        s += " surfaces=" + str(n.mesh.get_surface_count()) + " skin=" + str(n.skin != null)
        for i in range(n.mesh.get_surface_count()):
            var m = n.mesh.surface_get_material(i)
            if m != null and m is BaseMaterial3D:
                var t = m.albedo_texture
                var nt = m.normal_texture
                s += "\n" + "   ".repeat(depth + 1) + "mat " + str(m.resource_name) + " albedo=" + (t.resource_path if t != null else "none") + " size=" + (str(t.get_size()) if t != null else "-") + " mips=" + (str(t.get_image().has_mipmaps()) if t != null else "-") + " normal=" + (nt.resource_path if nt != null else "none") + " cull=" + str(m.cull_mode)
    for k in n.get_meta_list():
        s += " meta:" + str(k) + "=" + str(n.get_meta(k))
    if not (n is Skeleton3D and depth > 6):
        print(s)
    if n is Skeleton3D:
        print("   ".repeat(depth + 1) + "bones=" + str(n.get_bone_count()))
        return
    for c in n.get_children():
        _walk(c, depth + 1)

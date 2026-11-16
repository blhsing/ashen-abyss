import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
m = bpy.data.materials.new("t"); m.use_nodes = True
nt = m.node_tree
for typ in ("ShaderNodeTexNoise", "ShaderNodeTexVoronoi", "ShaderNodeMix", "ShaderNodeMapRange", "ShaderNodeBump",
            "ShaderNodeBsdfPrincipled", "ShaderNodeValToRGB", "ShaderNodeNewGeometry", "ShaderNodeTexCoord",
            "ShaderNodeCombineColor", "ShaderNodeSeparateColor", "ShaderNodeMapping", "ShaderNodeNormalMap",
            "ShaderNodeAmbientOcclusion", "ShaderNodeTexWave", "ShaderNodeVectorMath", "ShaderNodeEmission"):
    n = nt.nodes.new(typ)
    print("NODE", typ)
    print("   IN ", [(i, s.identifier, s.type) for i, s in enumerate(n.inputs)])
    print("   OUT", [(i, s.identifier, s.type) for i, s in enumerate(n.outputs)])
    for pn in ("noise_type", "feature", "data_type", "blend_type", "interpolation_type", "noise_dimensions", "distance", "normalize", "wave_type"):
        if hasattr(n, pn):
            try:
                items = [e.identifier for e in n.bl_rna.properties[pn].enum_items]
            except Exception:
                items = None
            print("   PROP", pn, getattr(n, pn), items)
sc = bpy.context.scene
sc.render.engine = 'CYCLES'
print("BAKE TYPES", [e.identifier for e in sc.cycles.bl_rna.properties['bake_type'].enum_items])
print("BAKE SETTINGS", [p.identifier for p in sc.render.bake.bl_rna.properties])
print("MESH has set_sharp_from_angle", hasattr(bpy.types.Mesh, 'set_sharp_from_angle'))
print("EEVEE props", [p.identifier for p in sc.eevee.bl_rna.properties])
print("view transforms", [e.identifier for e in sc.view_settings.bl_rna.properties['view_transform'].enum_items])

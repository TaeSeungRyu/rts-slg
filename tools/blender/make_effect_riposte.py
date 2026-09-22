"""Create Riposte: a broad hexagonal counter-formation shield."""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-riposte.glb")
PREVIEW = os.path.join(ROOT, ".tmp-riposte-preview.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True


def material(name, color, metallic, roughness, emission):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Strength"].default_value = emission
    return mat


bronze = material("RiposteBronze", (0.48, 0.20, 0.045), 0.85, 0.20, 0.65)
gold = material("RiposteGold", (1.0, 0.56, 0.10), 0.75, 0.12, 3.2)
dark = material("RiposteDark", (0.07, 0.025, 0.012), 0.70, 0.26, 0.18)

root = bpy.data.objects.new("RiposteRoot", None)
bpy.context.collection.objects.link(root)


def cylinder(name, radius, depth, location, mat, vertices=6, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                       location=location, rotation=(math.pi / 2, 0, 0))
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("ShieldEdgeBevel", "BEVEL")
    bevel.width = 0.018
    bevel.segments = 3
    obj.parent = root
    return obj


# 정면을 향하는 넓은 육각 방패. 뒤판-본판-테두리를 겹쳐 깊이와 반격 진형을 표현한다.
cylinder("Riposte_ShieldBack", 0.48, 0.055, (0, 0.035, 0.03), dark, scale=(0.90, 1.0, 1.05))
cylinder("Riposte_ShieldFace", 0.44, 0.060, (0, -0.008, 0.03), bronze, scale=(0.90, 1.0, 1.05))
cylinder("Riposte_ShieldRim", 0.39, 0.068, (0, -0.050, 0.03), gold, scale=(0.90, 1.0, 1.05))
cylinder("Riposte_ShieldInset", 0.335, 0.073, (0, -0.078, 0.03), dark, scale=(0.90, 1.0, 1.05))

# 중앙에서 되받아치는 창끝 문양.
for angle in (-55, 0, 55):
    bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.050, radius2=0.0, depth=0.25,
                                   location=(math.sin(math.radians(angle)) * 0.14, -0.125,
                                             0.03 + math.cos(math.radians(angle)) * 0.10),
                                   rotation=(math.pi / 2, 0, math.radians(-angle)))
    spike = bpy.context.object
    spike.name = f"Riposte_CounterSpike_{angle + 55}"
    spike.data.materials.append(gold)
    spike.parent = root

# Preview.
bpy.ops.object.light_add(type="AREA", location=(2.0, -3.0, 2.8))
bpy.context.object.data.energy = 900
bpy.context.object.data.size = 3.0
bpy.ops.object.light_add(type="AREA", location=(-2.0, -1.0, 1.5))
bpy.context.object.data.energy = 500
bpy.context.object.data.color = (1.0, 0.25, 0.05)
bpy.ops.object.camera_add(location=(0.0, -3.5, 0.12), rotation=(math.pi / 2, 0, 0))
scene.camera = bpy.context.object
scene.render.filepath = PREVIEW
bpy.ops.render.render(write_still=True)

bpy.ops.object.select_all(action="DESELECT")
root.select_set(True)
for child in root.children_recursive:
    child.select_set(True)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_animations=False, use_selection=True)
print(f"exported {OUT}")
print(f"preview {PREVIEW}")

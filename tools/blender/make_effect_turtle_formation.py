"""Create one compact hex shield used by the falling Turtle Formation barrage."""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-turtle-formation.glb")
PREVIEW = os.path.join(ROOT, ".tmp-turtle-formation-preview.png")

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


jade = material("TurtleJade", (0.055, 0.38, 0.27), 0.62, 0.19, 0.8)
glow = material("TurtleGlow", (0.20, 1.0, 0.66), 0.36, 0.12, 3.6)
dark = material("TurtleDark", (0.018, 0.10, 0.075), 0.68, 0.25, 0.16)
root = bpy.data.objects.new("TurtleFormationRoot", None)
bpy.context.collection.objects.link(root)


def shield(name, radius, depth, y, mat):
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=radius, depth=depth,
                                       location=(0, y, 0), rotation=(math.pi / 2, 0, 0))
    obj = bpy.context.object
    obj.name = name
    obj.scale = (0.90, 1.0, 1.04)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("TurtleShieldBevel", "BEVEL")
    bevel.width = 0.014
    bevel.segments = 3
    obj.parent = root
    return obj


shield("Turtle_ShieldBack", 0.25, 0.042, 0.025, dark)
shield("Turtle_ShieldFace", 0.225, 0.048, -0.010, jade)
shield("Turtle_ShieldRim", 0.185, 0.054, -0.040, glow)
shield("Turtle_ShieldInset", 0.145, 0.059, -0.063, dark)

# 거북 등껍질을 연상시키는 중앙 육각 마디.
shield("Turtle_ShellCore", 0.075, 0.064, -0.088, glow)

bpy.ops.object.light_add(type="AREA", location=(2.0, -3.0, 2.5))
bpy.context.object.data.energy = 850
bpy.context.object.data.size = 3.0
bpy.ops.object.light_add(type="AREA", location=(-2.0, -1.0, 1.4))
bpy.context.object.data.energy = 480
bpy.context.object.data.color = (0.12, 1.0, 0.48)
bpy.ops.object.camera_add(location=(0.0, -3.2, 0.08), rotation=(math.pi / 2, 0, 0))
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

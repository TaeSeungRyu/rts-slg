"""Create the small red cross source mesh used by the Field Medic effect."""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-field-medic.glb")
PREVIEW = os.path.join(ROOT, ".tmp-field-medic-preview.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True

mat = bpy.data.materials.new("FieldMedicRed")
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get("Principled BSDF")
bsdf.inputs["Base Color"].default_value = (0.72, 0.012, 0.018, 1.0)
bsdf.inputs["Roughness"].default_value = 0.28
bsdf.inputs["Emission Color"].default_value = (0.9, 0.018, 0.025, 1.0)
bsdf.inputs["Emission Strength"].default_value = 1.8

root = bpy.data.objects.new("FieldMedicCrossRoot", None)
bpy.context.collection.objects.link(root)

for name, scale in (("FieldMedicCrossVertical", (0.045, 0.020, 0.15)),
                    ("FieldMedicCrossHorizontal", (0.13, 0.020, 0.045))):
    bpy.ops.mesh.primitive_cube_add(location=(0.0, 0.0, 0.0))
    part = bpy.context.object
    part.name = name
    part.scale = scale
    part.data.materials.append(mat)
    part.parent = root

bpy.ops.object.light_add(type="AREA", location=(1.5, -2.5, 2.0))
bpy.context.object.data.energy = 700
bpy.context.object.data.size = 3.0
bpy.ops.object.camera_add(location=(0.0, -3.0, 0.0), rotation=(math.pi / 2, 0, 0))
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

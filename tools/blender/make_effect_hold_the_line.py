"""Create Hold the Line: horizontal rows of arrows over the allied formation."""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-hold-the-line.glb")
PREVIEW = os.path.join(ROOT, ".tmp-hold-the-line-preview.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True


def material(name, color, metallic, emission):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = 0.20
    bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Strength"].default_value = emission
    return mat


shaft_mat = material("HoldArrowDarkWood", (0.15, 0.07, 0.025), 0.10, 0.25)
steel_mat = material("HoldArrowSteel", (0.58, 0.66, 0.72), 0.88, 1.25)
feather_mat = material("HoldArrowRedFeather", (0.54, 0.025, 0.018), 0.18, 0.85)
root = bpy.data.objects.new("HoldTheLineRoot", None)
bpy.context.collection.objects.link(root)
root.scale = (0.8, 0.8, 0.8)


def make_arrow(index, x, z, direction):
    arrow_root = bpy.data.objects.new(f"HoldLine_Group_{index}", None)
    bpy.context.collection.objects.link(arrow_root)
    arrow_root.parent = root
    length = 0.34
    # shaft: local cylinder Z -> world X
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.012, depth=length,
                                        location=(x, 0.0, z), rotation=(0, math.pi / 2, 0))
    shaft = bpy.context.object
    shaft.name = f"HoldLine_ArrowShaft_{index}"
    shaft.data.materials.append(shaft_mat)
    shaft.parent = arrow_root

    tip_x = x + direction * (length * 0.5 + 0.055)
    bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.047, radius2=0.0, depth=0.12,
                                   location=(tip_x, 0.0, z),
                                   rotation=(0, direction * math.pi / 2, 0))
    tip = bpy.context.object
    tip.name = f"HoldLine_ArrowTip_{index}"
    tip.data.materials.append(steel_mat)
    tip.parent = arrow_root

    tail_x = x - direction * (length * 0.5 + 0.025)
    for sign in (-1, 1):
        bpy.ops.mesh.primitive_cube_add(location=(tail_x, 0.0, z + sign * 0.026))
        feather = bpy.context.object
        feather.name = f"HoldLine_ArrowFeather_{index}_{sign + 2}"
        feather.scale = (0.052, 0.010, 0.014)
        feather.data.materials.append(feather_mat)
        feather.parent = arrow_root


layout = ((-0.18, 0.27, 1), (0.18, 0.18, 1), (-0.20, 0.09, 1), (0.18, 0.00, 1),
          (-0.18, -0.09, 1), (0.20, -0.18, 1), (-0.16, -0.27, 1))
for index, (x, z, direction) in enumerate(layout, start=1):
    make_arrow(index, x, z, direction)

bpy.ops.object.light_add(type="AREA", location=(1.8, -3.0, 2.4))
bpy.context.object.data.energy = 800
bpy.context.object.data.size = 3.0
bpy.ops.object.camera_add(location=(0.0, -3.5, 0.0), rotation=(math.pi / 2, 0, 0))
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

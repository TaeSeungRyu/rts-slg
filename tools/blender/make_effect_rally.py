"""Create Rally: a gold-trimmed war drum, two beaters, and morale rings."""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-rally.glb")
PREVIEW = os.path.join(ROOT, ".tmp-rally-preview.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True


def material(name, color, metallic=0.0, roughness=0.3, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Strength"].default_value = emission
    return mat


red = material("RallyDrumCrimson", (0.48, 0.012, 0.016), metallic=0.25, roughness=0.28, emission=0.55)
gold = material("RallyDrumGold", (0.86, 0.48, 0.055), metallic=0.82, roughness=0.18, emission=1.1)
hide = material("RallyDrumHide", (0.70, 0.46, 0.22), roughness=0.58, emission=0.22)
wood = material("RallyBeaterWood", (0.23, 0.055, 0.018), roughness=0.42, emission=0.18)

root = bpy.data.objects.new("RallyWarDrumRoot", None)
bpy.context.collection.objects.link(root)

# Drum faces the game camera; cylinder axis points along screen depth (Y).
bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=0.34, depth=0.30,
                                    location=(0.0, 0.0, -0.02), rotation=(math.pi / 2, 0, 0))
drum = bpy.context.object
drum.name = "Rally_DrumBody"
drum.data.materials.append(red)
drum.parent = root
for y in (-0.165, 0.165):
    bpy.ops.mesh.primitive_torus_add(major_radius=0.34, minor_radius=0.026,
                                    location=(0.0, y, -0.02), rotation=(math.pi / 2, 0, 0))
    rim = bpy.context.object
    rim.name = f"Rally_DrumRim_{'Front' if y < 0 else 'Back'}"
    rim.data.materials.append(gold)
    rim.parent = root
bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=0.30, depth=0.018,
                                    location=(0.0, -0.176, -0.02), rotation=(math.pi / 2, 0, 0))
face = bpy.context.object
face.name = "Rally_DrumFace"
face.data.materials.append(hide)
face.parent = root

for index, x in enumerate((-0.23, 0.23), start=1):
    group = bpy.data.objects.new(f"Rally_MalletGroup_{index}", None)
    bpy.context.collection.objects.link(group)
    group.parent = root
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=0.025, depth=0.48,
                                        location=(x, -0.30, 0.34), rotation=(0.0, math.radians(22 if x < 0 else -22), 0.0))
    stick = bpy.context.object
    stick.name = f"Rally_MalletStick_{index}"
    stick.data.materials.append(wood)
    stick.parent = group
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=10, radius=0.075,
                                        location=(x * 0.45, -0.30, 0.13))
    head = bpy.context.object
    head.name = f"Rally_MalletHead_{index}"
    head.data.materials.append(red)
    head.parent = group

for index, radius in enumerate((0.42, 0.53, 0.65), start=1):
    bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=0.018,
                                    location=(0.0, -0.22, -0.02), rotation=(math.pi / 2, 0, 0))
    ring = bpy.context.object
    ring.name = f"Rally_MoraleRing_{index}"
    ring.data.materials.append(gold)
    ring.parent = root

bpy.ops.object.light_add(type="AREA", location=(1.8, -3.0, 2.5))
bpy.context.object.data.energy = 900
bpy.context.object.data.size = 3.0
bpy.ops.object.camera_add(location=(0.0, -3.6, 0.15), rotation=(math.pi / 2, 0, 0))
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

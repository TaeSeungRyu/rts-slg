"""Create Resupply: a wooden supply crate, opening lid, and ration bundles."""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-resupply.glb")
PREVIEW = os.path.join(ROOT, ".tmp-resupply-preview.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True


def material(name, color, metallic=0.0, roughness=0.4, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Strength"].default_value = emission
    return mat


wood = material("ResupplyDarkWood", (0.23, 0.075, 0.022), roughness=0.48)
band = material("ResupplyIronBand", (0.28, 0.31, 0.32), metallic=0.82, roughness=0.20)
grain = material("ResupplyGrainGold", (0.84, 0.52, 0.08), roughness=0.30, emission=1.0)
glow = material("ResupplyWarmGlow", (0.98, 0.72, 0.17), metallic=0.25, roughness=0.16, emission=2.0)

root = bpy.data.objects.new("ResupplyCrateRoot", None)
bpy.context.collection.objects.link(root)

bpy.ops.mesh.primitive_cube_add(location=(0.0, 0.0, -0.12))
body = bpy.context.object
body.name = "Resupply_CrateBody"
body.scale = (0.40, 0.22, 0.24)
body.data.materials.append(wood)
body.parent = root
for x in (-0.31, 0.31):
    bpy.ops.mesh.primitive_cube_add(location=(x, -0.225, -0.12))
    brace = bpy.context.object
    brace.name = f"Resupply_CrateBrace_{'L' if x < 0 else 'R'}"
    brace.scale = (0.035, 0.018, 0.245)
    brace.data.materials.append(band)
    brace.parent = root

lid_group = bpy.data.objects.new("Resupply_LidGroup", None)
bpy.context.collection.objects.link(lid_group)
lid_group.parent = root
bpy.ops.mesh.primitive_cube_add(location=(0.0, 0.0, 0.15))
lid = bpy.context.object
lid.name = "Resupply_CrateLid"
lid.scale = (0.42, 0.235, 0.055)
lid.data.materials.append(wood)
lid.parent = lid_group

# Five visible ration bundles are animated independently at runtime.
for index, (x, z) in enumerate(((-0.25, 0.02), (-0.12, 0.09), (0.0, 0.03), (0.14, 0.10), (0.27, 0.01)), start=1):
    bundle_group = bpy.data.objects.new(f"Resupply_BundleGroup_{index}", None)
    bpy.context.collection.objects.link(bundle_group)
    bundle_group.parent = root
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=0.085, location=(x, -0.25, z))
    bundle = bpy.context.object
    bundle.name = f"Resupply_RationBundle_{index}"
    bundle.scale = (0.82, 0.42, 1.08)
    bundle.data.materials.append(grain)
    bundle.parent = bundle_group
    bpy.ops.mesh.primitive_torus_add(major_radius=0.045, minor_radius=0.009,
                                    location=(x, -0.29, z + 0.075), rotation=(math.pi / 2, 0, 0))
    tie = bpy.context.object
    tie.name = f"Resupply_BundleTie_{index}"
    tie.data.materials.append(glow)
    tie.parent = bundle_group

bpy.ops.object.light_add(type="AREA", location=(1.6, -3.0, 2.3))
bpy.context.object.data.energy = 850
bpy.context.object.data.size = 3.0
bpy.ops.object.camera_add(location=(0.0, -3.5, 0.15), rotation=(math.pi / 2, 0, 0))
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

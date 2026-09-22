"""Create the Heavy Blow active effect: a black iron bomb and dark explosion pieces."""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-heavy-blow.glb")
PREVIEW = os.path.join(ROOT, ".tmp-heavy-blow-preview.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True


def material(name, color, metallic=0.0, roughness=0.3, emission=0.0, alpha=1.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, alpha)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Emission Color"].default_value = (*color, alpha)
    bsdf.inputs["Emission Strength"].default_value = emission
    if alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
        mat.surface_render_method = "DITHERED"
    return mat


iron = material("HeavyBlowBlackIron", (0.008, 0.010, 0.014), 0.92, 0.16, 0.08)
edge = material("HeavyBlowIronEdge", (0.075, 0.085, 0.10), 0.88, 0.20, 0.18)
ember = material("HeavyBlowFuseEmber", (1.0, 0.16, 0.015), 0.05, 0.15, 8.0)
smoke = material("HeavyBlowBlackSmoke", (0.012, 0.014, 0.020), 0.05, 0.78, 0.08, 0.88)
shock = material("HeavyBlowDarkShockwave", (0.08, 0.025, 0.12), 0.15, 0.22, 2.8, 0.72)

root = bpy.data.objects.new("HeavyBlowRoot", None)
bpy.context.collection.objects.link(root)


def parent(obj):
    obj.parent = root
    return obj


# Main black iron bomb with metal bands and a glowing fuse.
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=0.23, location=(0.0, 0.0, 0.28))
core = parent(bpy.context.object)
core.name = "HeavyBlow_BombCore"
core.scale = (1.05, 0.82, 1.05)
core.data.materials.append(iron)

for index, rotation in enumerate(((0.0, 0.0, 0.0), (math.pi / 2, 0.0, 0.0))):
    bpy.ops.mesh.primitive_torus_add(major_radius=0.205, minor_radius=0.018, major_segments=24,
                                     minor_segments=6, location=(0.0, 0.0, 0.28), rotation=rotation)
    band = parent(bpy.context.object)
    band.name = f"HeavyBlow_BombBand_{index + 1}"
    band.data.materials.append(edge)

bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.028, depth=0.18,
                                    location=(0.08, 0.0, 0.54), rotation=(0.0, math.radians(-28), 0.0))
fuse = parent(bpy.context.object)
fuse.name = "HeavyBlow_Fuse"
fuse.data.materials.append(edge)
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.055, location=(0.12, 0.0, 0.62))
fuse_ember = parent(bpy.context.object)
fuse_ember.name = "HeavyBlow_FuseEmber"
fuse_ember.data.materials.append(ember)

# Jagged black fragments are authored at their final spread positions; Godot moves them from the core.
for index in range(14):
    angle = math.tau * index / 14 + (index % 3) * 0.09
    radius = 0.38 + (index % 4) * 0.055
    z = 0.20 + ((index * 7) % 6) * 0.075
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.055 + (index % 3) * 0.010,
                                         location=(math.cos(angle) * radius, math.sin(angle) * radius, z))
    shard = parent(bpy.context.object)
    shard.name = f"HeavyBlow_Shard_{index + 1:02d}"
    shard.scale = (1.8, 0.55, 0.75)
    shard.rotation_euler = (angle * 0.6, angle, -angle * 0.4)
    shard.data.materials.append(iron if index % 2 == 0 else edge)

# Dense smoke bulbs burst upward and sideways from inside the target formation.
for index in range(9):
    angle = math.tau * index / 9 + 0.22
    radius = 0.19 + (index % 3) * 0.075
    z = 0.27 + (index % 4) * 0.10
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.115 + (index % 2) * 0.035,
                                         location=(math.cos(angle) * radius, math.sin(angle) * radius, z))
    puff = parent(bpy.context.object)
    puff.name = f"HeavyBlow_Smoke_{index + 1:02d}"
    puff.scale = (1.15, 0.88, 1.05)
    puff.data.materials.append(smoke)

bpy.ops.mesh.primitive_torus_add(major_radius=0.28, minor_radius=0.026, major_segments=32,
                                 minor_segments=8, location=(0.0, 0.0, 0.20))
ring = parent(bpy.context.object)
ring.name = "HeavyBlow_Shockwave"
ring.data.materials.append(shock)

# Preview only.
bpy.ops.object.light_add(type="AREA", location=(2.2, -3.0, 3.0))
key = bpy.context.object
key.data.energy = 950
key.data.size = 3.0
bpy.ops.object.light_add(type="AREA", location=(-2.0, -1.0, 1.5))
fill = bpy.context.object
fill.data.energy = 650
fill.data.color = (0.36, 0.18, 0.72)
fill.data.size = 2.5
bpy.ops.object.camera_add(location=(1.45, -3.6, 1.55))
camera = bpy.context.object
direction = core.location - camera.location
camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
scene.camera = camera
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

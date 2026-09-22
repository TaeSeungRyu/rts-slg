"""Create Double Hit: two rapid golden impact bursts on opposite sides of a formation."""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-double-hit.glb")
PREVIEW = os.path.join(ROOT, ".tmp-double-hit-preview.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True


def material(name, color, metallic=0.0, emission=0.0, alpha=1.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, alpha)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = 0.16
    bsdf.inputs["Emission Color"].default_value = (*color, alpha)
    bsdf.inputs["Emission Strength"].default_value = emission
    if alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
        mat.surface_render_method = "DITHERED"
    return mat


gold = material("DoubleHitGold", (1.0, 0.54, 0.055), 0.72, 5.5)
white = material("DoubleHitCore", (1.0, 0.95, 0.70), 0.25, 9.0)
orange = material("DoubleHitOrange", (0.95, 0.17, 0.015), 0.45, 5.0)
ring_mat = material("DoubleHitRing", (1.0, 0.70, 0.16), 0.55, 4.2, 0.86)

root = bpy.data.objects.new("DoubleHitRoot", None)
bpy.context.collection.objects.link(root)


def parent(obj):
    obj.parent = root
    return obj


for hit, x in ((1, -0.22), (2, 0.22)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.115, location=(x, 0.0, 0.30))
    core = parent(bpy.context.object)
    core.name = f"DoubleHit_Core_{hit}"
    core.scale = (1.25, 0.65, 1.05)
    core.data.materials.append(white)

    for ring_index, radius in enumerate((0.14, 0.21)):
        bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=0.014,
                                         major_segments=28, minor_segments=6,
                                         location=(x, 0.0, 0.30), rotation=(math.pi / 2, 0.0, 0.0))
        ring = parent(bpy.context.object)
        ring.name = f"DoubleHit_Ring_{hit}_{ring_index + 1}"
        ring.data.materials.append(ring_mat if ring_index == 0 else orange)

    for ray_index in range(8):
        angle = math.tau * ray_index / 8 + (hit - 1) * 0.20
        radius = 0.25
        bpy.ops.mesh.primitive_cube_add(location=(x + math.cos(angle) * radius,
                                                  0.0,
                                                  0.30 + math.sin(angle) * radius))
        ray = parent(bpy.context.object)
        ray.name = f"DoubleHit_Ray_{hit}_{ray_index + 1:02d}"
        ray.scale = (0.075, 0.016, 0.016)
        ray.rotation_euler[1] = -angle
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        ray.data.materials.append(gold if ray_index % 2 == 0 else orange)

# Preview only.
bpy.ops.object.light_add(type="AREA", location=(1.8, -3.0, 2.4))
key = bpy.context.object
key.data.energy = 850
key.data.size = 3.0
bpy.ops.object.camera_add(location=(0.0, -3.1, 0.45), rotation=(math.pi / 2, 0.0, 0.0))
camera = bpy.context.object
camera.rotation_euler = (math.pi / 2, 0.0, 0.0)
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

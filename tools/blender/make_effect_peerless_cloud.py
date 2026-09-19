"""Create the Peerless active effect: a compact crimson cloud that bursts outward."""
import bpy
import math
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-peerless-red-cloud.glb")

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

def mat(name, color, emission):
    value = bpy.data.materials.new(name)
    value.diffuse_color = (*color, 0.88)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Strength"].default_value = emission
    bsdf.inputs["Roughness"].default_value = 0.82
    return value

crimson = mat("PeerlessCrimson", (0.62, 0.012, 0.02), 3.8)
scarlet = mat("PeerlessScarlet", (1.0, 0.035, 0.015), 6.0)
dark = mat("PeerlessDark", (0.18, 0.004, 0.008), 1.2)

root = bpy.data.objects.new("PeerlessCloudRoot", None)
bpy.context.collection.objects.link(root)

lobes = [
    (0.00, 0.00, 0.10, 0.24), (-0.16, 0.02, 0.13, 0.19), (0.17, 0.01, 0.14, 0.20),
    (-0.08, -0.12, 0.19, 0.17), (0.09, -0.13, 0.18, 0.16),
    (-0.05, 0.14, 0.22, 0.15), (0.10, 0.13, 0.20, 0.14), (0.00, 0.00, 0.31, 0.13),
]
for i, (x, y, z, radius) in enumerate(lobes):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=radius, location=(x, y, z))
    cloud = bpy.context.object
    cloud.name = f"CloudLobe_{i + 1}"
    cloud.scale = (1.25, 0.82, 0.92)
    cloud.data.materials.append((scarlet, crimson, dark)[i % 3])
    cloud.parent = root
    start = cloud.location.copy()
    cloud.scale *= 0.15
    cloud.keyframe_insert("scale", frame=1)
    cloud.keyframe_insert("location", frame=1)
    cloud.scale *= 7.4
    direction = start.normalized() if start.length > 0.01 else start.copy()
    if start.length <= 0.01:
        direction.z = 1.0
    cloud.location = start + direction * (0.18 + i * 0.012)
    cloud.keyframe_insert("scale", frame=11)
    cloud.keyframe_insert("location", frame=11)
    cloud.scale *= 1.45
    cloud.location.z += 0.22
    cloud.keyframe_insert("scale", frame=25)
    cloud.keyframe_insert("location", frame=25)

root.rotation_euler = (0, 0, 0)
root.keyframe_insert("rotation_euler", frame=1)
root.rotation_euler.z = math.radians(105)
root.keyframe_insert("rotation_euler", frame=25)

if root.animation_data and root.animation_data.action:
    root.animation_data.action.name = "PeerlessBurst"
bpy.context.scene.frame_start = 1
bpy.context.scene.frame_end = 25
bpy.context.scene.render.fps = 30
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_animations=True)
print(f"exported {OUT}")

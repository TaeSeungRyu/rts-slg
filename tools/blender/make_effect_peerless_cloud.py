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

crimson = mat("PeerlessCrimson", (0.72, 0.004, 0.012), 5.2)
scarlet = mat("PeerlessScarlet", (1.0, 0.008, 0.012), 7.5)
dark = mat("PeerlessDark", (0.34, 0.001, 0.006), 2.4)

root = bpy.data.objects.new("PeerlessCloudRoot", None)
bpy.context.collection.objects.link(root)

lobes = [
    (-0.18, -0.10, 0.08, 0.11), (0.14, -0.12, 0.10, 0.10), (-0.05, 0.15, 0.09, 0.12),
    (0.20, 0.10, 0.12, 0.09), (-0.20, 0.12, 0.11, 0.10), (0.02, -0.02, 0.16, 0.12),
    (0.08, 0.18, 0.13, 0.09), (-0.10, -0.18, 0.12, 0.09), (0.00, 0.02, 0.20, 0.11),
]
for i, (x, y, z, radius) in enumerate(lobes):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=radius, location=(x, y, z))
    cloud = bpy.context.object
    cloud.name = f"CloudLobe_{i + 1}"
    cloud.scale = (1.18, 0.88, 0.95)
    cloud.data.materials.append((scarlet, crimson, dark)[i % 3])
    cloud.parent = root
    start = cloud.location.copy()
    start_frame = 1 + (i % 5) * 3
    cloud.scale *= 0.05
    cloud.keyframe_insert("scale", frame=start_frame)
    cloud.keyframe_insert("location", frame=start_frame)
    cloud.scale *= 13.0
    direction = start.normalized() if start.length > 0.01 else start.copy()
    if start.length <= 0.01:
        direction.z = 1.0
    cloud.location = start + direction * (0.07 + i * 0.006)
    cloud.keyframe_insert("scale", frame=start_frame + 4)
    cloud.keyframe_insert("location", frame=start_frame + 4)
    cloud.scale *= 0.15
    cloud.location.z += 0.12
    cloud.keyframe_insert("scale", frame=start_frame + 10)
    cloud.keyframe_insert("location", frame=start_frame + 10)

root.rotation_euler = (0, 0, 0)
root.keyframe_insert("rotation_euler", frame=1)
root.rotation_euler.z = math.radians(55)
root.keyframe_insert("rotation_euler", frame=28)

if root.animation_data and root.animation_data.action:
    root.animation_data.action.name = "PeerlessBurst"
bpy.context.scene.frame_start = 1
bpy.context.scene.frame_end = 28
bpy.context.scene.render.fps = 30
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_animations=True)
print(f"exported {OUT}")

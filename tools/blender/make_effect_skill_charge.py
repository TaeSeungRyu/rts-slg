"""Build the reusable active-skill preparation effect.

Five golden motes orbit above a thin rune ring. The exported glTF contains a looping
animation named Charge and is intentionally independent from any troop model.
"""
import bpy
import math
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-skill-charge.glb")

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

def material(name, color, strength):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Strength"].default_value = strength
    bsdf.inputs["Roughness"].default_value = 0.28
    return mat

gold = material("SkillGold", (1.0, 0.44, 0.035), 7.0)
white = material("SkillCore", (1.0, 0.88, 0.42), 10.0)

root = bpy.data.objects.new("SkillChargeRoot", None)
bpy.context.collection.objects.link(root)

for radius, z, minor in ((0.42, 0.05, 0.018), (0.28, 0.10, 0.012)):
    bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=minor, major_segments=48, minor_segments=8,
                                    location=(0, 0, z))
    ring = bpy.context.object
    ring.name = f"RuneRing_{radius:.2f}"
    ring.data.materials.append(gold)
    ring.parent = root

for i in range(5):
    angle = math.tau * i / 5.0
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.055,
                                         location=(math.cos(angle) * 0.36, math.sin(angle) * 0.36, 0.18))
    mote = bpy.context.object
    mote.name = f"ChargeMote_{i + 1}"
    mote.data.materials.append(white if i == 0 else gold)
    mote.parent = root

# One full orbit with a gentle pulse. Linear interpolation prevents pauses at the seam.
root.rotation_euler = (0, 0, 0)
root.scale = (0.82, 0.82, 0.82)
root.keyframe_insert("rotation_euler", frame=1)
root.keyframe_insert("scale", frame=1)
root.rotation_euler.z = math.tau
root.scale = (1.08, 1.08, 1.08)
root.keyframe_insert("rotation_euler", frame=31)
root.keyframe_insert("scale", frame=16)
root.scale = (0.82, 0.82, 0.82)
root.keyframe_insert("scale", frame=31)
if root.animation_data and root.animation_data.action:
    root.animation_data.action.name = "Charge"

bpy.context.scene.frame_start = 1
bpy.context.scene.frame_end = 31
bpy.context.scene.render.fps = 30
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_animations=True)
print(f"exported {OUT}")

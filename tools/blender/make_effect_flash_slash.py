"""Create the Flash active effect: a fast horizontal sword slash across a formation."""
import os
import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-flash-slash.glb")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 30
scene.frame_start = 1
scene.frame_end = 24


def emission_material(name, color, strength):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Strength"].default_value = strength
    bsdf.inputs["Roughness"].default_value = 0.22
    return mat


core = emission_material("FlashSlashCore", (0.94, 0.98, 1.0), 8.0)
blue = emission_material("FlashSlashBlue", (0.18, 0.56, 1.0), 5.0)
pale = emission_material("FlashSlashAfterimage", (0.52, 0.82, 1.0), 3.2)

root = bpy.data.objects.new("FlashSlashRoot", None)
bpy.context.collection.objects.link(root)


def slash(name, length, thickness, z, material, rotation=0.0):
    bpy.ops.mesh.primitive_cube_add(
        location=(0.0, 0.0, z),
        scale=(length * 0.5, thickness * 0.5, thickness * 0.5),
    )
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler[1] = rotation
    obj.data.materials.append(material)
    obj.parent = root
    return obj


# 부대 중앙을 가르는 주 칼선과 위·아래로 짧게 남는 두 잔상.
slash("FlashSlash_Main", 0.72, 0.030, 0.25, core)
slash("FlashSlash_Upper", 0.54, 0.018, 0.31, pale, 0.035)
slash("FlashSlash_Lower", 0.46, 0.016, 0.19, blue, -0.045)

# 중앙에서 순식간에 좌우로 펼쳐지고, 약간 더 길어진 뒤 수축해 사라진다.
root.scale = (0.01, 0.35, 0.35)
root.keyframe_insert("scale", frame=1)
root.scale = (0.12, 0.70, 0.70)
root.keyframe_insert("scale", frame=4)
root.scale = (1.0, 1.0, 1.0)
root.keyframe_insert("scale", frame=8)
root.scale = (1.18, 0.72, 0.72)
root.keyframe_insert("scale", frame=13)
root.scale = (1.28, 0.05, 0.05)
root.keyframe_insert("scale", frame=20)
root.scale = (0.01, 0.01, 0.01)
root.keyframe_insert("scale", frame=24)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_animations=True)
print(f"exported {OUT}")

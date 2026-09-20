"""Create a stylised Flash active effect: a curved sword trail, flare and sparks."""
import math
import os

import bpy
import mathutils

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
    bsdf.inputs["Roughness"].default_value = 0.12
    return mat


core = emission_material("FlashSlashCore", (1.0, 0.98, 0.78), 11.0)
blue = emission_material("FlashSlashBlue", (0.12, 0.52, 1.0), 6.0)
pale = emission_material("FlashSlashAfterimage", (0.50, 0.86, 1.0), 4.0)

root = bpy.data.objects.new("FlashSlashRoot", None)
bpy.context.collection.objects.link(root)


def curved_blade(name, length, width, z, bend, material, depth=0.012):
    """Make a tapered crescent ribbon; pointed ends avoid the old bar silhouette."""
    segments = 18
    verts = []
    for y in (-depth, depth):
        for side in (-1.0, 1.0):
            for i in range(segments + 1):
                t = i / segments
                x = (t - 0.5) * length
                taper = math.sin(math.pi * t) ** 0.72
                center_z = z + bend * (1.0 - (2.0 * t - 1.0) ** 2)
                verts.append((x, y, center_z + side * width * taper * 0.5))

    ring = segments + 1
    faces = []
    for layer in range(2):
        base = layer * ring * 2
        for i in range(segments):
            faces.append((base + i, base + i + 1, base + ring + i + 1, base + ring + i))
    back = ring * 2
    for side in range(2):
        a = side * ring
        b = back + side * ring
        for i in range(segments):
            faces.append((a + i, b + i, b + i + 1, a + i + 1))

    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = root
    return obj


main = curved_blade("FlashSlash_MainCrescent", 1.20, 0.115, 0.27, 0.12, core, 0.018)
upper = curved_blade("FlashSlash_UpperTrail", 0.98, 0.048, 0.34, 0.09, pale, 0.010)
lower = curved_blade("FlashSlash_LowerTrail", 0.84, 0.034, 0.20, -0.06, blue, 0.009)
upper.rotation_euler[1] = math.radians(-5)
lower.rotation_euler[1] = math.radians(6)

bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.13, location=(0.0, 0.0, 0.29))
flare = bpy.context.object
flare.name = "FlashSlash_CentreFlare"
flare.scale = (1.8, 0.35, 0.7)
flare.data.materials.append(core)
flare.parent = root

sparks = []
for i, (x, z, angle) in enumerate(((-0.34, 0.18, -25), (-0.17, 0.43, 35),
                                    (0.20, 0.42, 28), (0.38, 0.17, -32))):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.035, location=(x, 0.0, z))
    spark = bpy.context.object
    spark.name = f"FlashSlash_Spark_{i + 1}"
    spark.scale = (2.8, 0.32, 0.45)
    spark.rotation_euler[1] = math.radians(angle)
    spark.data.materials.append(pale if i % 2 == 0 else blue)
    spark.parent = root
    sparks.append(spark)


def key_scale(obj, keys):
    for frame, scale in keys:
        obj.scale = scale
        obj.keyframe_insert("scale", frame=frame)


key_scale(main, [(1, (0.01, 0.1, 0.1)), (4, (0.28, 0.7, 0.7)),
                 (7, (1.0, 1.0, 1.0)), (11, (1.08, 0.78, 0.78)),
                 (17, (1.14, 0.08, 0.08)), (20, (0.01, 0.01, 0.01))])
key_scale(upper, [(1, (0.01, 0.01, 0.01)), (6, (0.01, 0.2, 0.2)),
                  (9, (1.0, 1.0, 1.0)), (16, (1.10, 0.05, 0.05)), (19, (0.01, 0.01, 0.01))])
key_scale(lower, [(1, (0.01, 0.01, 0.01)), (8, (0.01, 0.2, 0.2)),
                  (11, (1.0, 1.0, 1.0)), (18, (1.08, 0.05, 0.05)), (21, (0.01, 0.01, 0.01))])
key_scale(flare, [(1, (0.01, 0.01, 0.01)), (5, (0.01, 0.01, 0.01)),
                  (7, (2.4, 0.45, 0.9)), (10, (0.55, 0.15, 0.35)),
                  (13, (0.01, 0.01, 0.01))])

for i, spark in enumerate(sparks):
    original = spark.location.copy()
    spark.scale = (0.01, 0.01, 0.01)
    spark.keyframe_insert("scale", frame=6 + i % 2)
    spark.keyframe_insert("location", frame=6 + i % 2)
    spark.scale = (2.8, 0.32, 0.45)
    spark.location = original + mathutils.Vector((original.x * 0.65, 0.0, (original.z - 0.28) * 0.7))
    spark.keyframe_insert("scale", frame=11 + i)
    spark.keyframe_insert("location", frame=11 + i)
    spark.scale = (0.01, 0.01, 0.01)
    spark.location += mathutils.Vector((original.x * 0.35, 0.0, (original.z - 0.28) * 0.5))
    spark.keyframe_insert("scale", frame=18 + i)
    spark.keyframe_insert("location", frame=18 + i)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_animations=True)
print(f"exported {OUT}")

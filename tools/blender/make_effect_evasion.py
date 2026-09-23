"""Create Evasion: layered grey wind streaks sweeping across the allied formation."""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-evasion.glb")
PREVIEW = os.path.join(ROOT, ".tmp-evasion-preview.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True


def material(name, color, emission):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = 0.15
    bsdf.inputs["Roughness"].default_value = 0.25
    bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Strength"].default_value = emission
    return mat


grey = material("EvasionWindGrey", (0.24, 0.27, 0.30), 0.45)
light = material("EvasionWindEdge", (0.46, 0.49, 0.52), 0.70)
root = bpy.data.objects.new("EvasionRoot", None)
bpy.context.collection.objects.link(root)


def wind_curve(name, z, width, phase, mat, thickness):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = thickness
    curve.bevel_resolution = 3
    spline = curve.splines.new("POLY")
    points = 20
    spline.points.add(points - 1)
    for index in range(points):
        t = index / (points - 1)
        x = (t - 0.5) * width
        wave = math.sin(t * math.pi * 2.0 + phase) * 0.035
        taper = math.sin(t * math.pi)
        spline.points[index].co = (x, -0.015 - taper * 0.03, z + wave, 1.0)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    obj.parent = root


for index, (z, width) in enumerate(((0.22, 0.86), (0.11, 1.02), (0.0, 0.92), (-0.11, 1.06), (-0.22, 0.80))):
    wind_curve(f"Evasion_WindStreak_{index + 1}", z, width, index * 0.72,
               light if index % 2 == 0 else grey, 0.012 if index % 2 == 0 else 0.009)

# 바람에 휘날리는 작은 회색 파편.
for index in range(8):
    angle = index * math.tau / 8.0
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.025,
                                         location=(math.cos(angle) * 0.43, -0.025,
                                                   math.sin(angle) * 0.20))
    mote = bpy.context.object
    mote.name = f"Evasion_WindMote_{index + 1}"
    mote.scale = (1.8, 0.35, 0.55)
    mote.rotation_euler.z = angle * 0.35
    mote.data.materials.append(grey)
    mote.parent = root

bpy.ops.object.light_add(type="AREA", location=(1.5, -2.8, 2.3))
bpy.context.object.data.energy = 650
bpy.context.object.data.size = 3.0
bpy.ops.object.camera_add(location=(0.0, -3.4, 0.0), rotation=(math.pi / 2, 0, 0))
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

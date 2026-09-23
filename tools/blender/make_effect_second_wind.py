"""Create Second Wind: a crimson life-heart, returning golden souls, and rebirth rings."""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-second-wind.glb")
PREVIEW = os.path.join(ROOT, ".tmp-second-wind-preview.png")

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


crimson = material("SecondWindCrimsonLife", (0.70, 0.006, 0.018), metallic=0.22, roughness=0.20, emission=1.8)
gold = material("SecondWindSoulGold", (0.98, 0.62, 0.07), metallic=0.42, roughness=0.14, emission=2.3)
white = material("SecondWindSoulWhite", (0.93, 0.88, 0.64), metallic=0.12, roughness=0.18, emission=2.6)

root = bpy.data.objects.new("SecondWindRoot", None)
bpy.context.collection.objects.link(root)
heart = bpy.data.objects.new("SecondWind_HeartGroup", None)
bpy.context.collection.objects.link(heart)
heart.parent = root

# Stylized heart silhouette: two rounded lobes and one tapered lower point.
for index, x in enumerate((-0.12, 0.12), start=1):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=0.18, location=(x, -0.02, 0.13))
    lobe = bpy.context.object
    lobe.name = f"SecondWind_HeartLobe_{index}"
    lobe.scale = (1.0, 0.48, 0.88)
    lobe.data.materials.append(crimson)
    lobe.parent = heart
bpy.ops.mesh.primitive_cone_add(vertices=28, radius1=0.285, radius2=0.025, depth=0.46,
                                location=(0.0, -0.02, -0.10), rotation=(math.pi, 0, 0))
point = bpy.context.object
point.name = "SecondWind_HeartPoint"
point.scale = (1.0, 0.48, 1.0)
point.data.materials.append(crimson)
point.parent = heart

# Returning soul sparks begin around the heart and converge at runtime.
for index in range(6):
    angle = index * math.tau / 6.0
    group = bpy.data.objects.new(f"SecondWind_SoulGroup_{index + 1}", None)
    bpy.context.collection.objects.link(group)
    group.parent = root
    x = math.cos(angle) * (0.52 + (index % 2) * 0.09)
    z = math.sin(angle) * 0.38 + 0.05
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.065, location=(x, -0.04, z))
    soul = bpy.context.object
    soul.name = f"SecondWind_Soul_{index + 1}"
    soul.scale = (0.62, 0.42, 1.25)
    soul.data.materials.append(white if index % 2 == 0 else gold)
    soul.parent = group

for index, radius in enumerate((0.36, 0.48, 0.60), start=1):
    bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=0.018,
                                    location=(0.0, -0.12, 0.02), rotation=(math.pi / 2, 0, 0))
    ring = bpy.context.object
    ring.name = f"SecondWind_RebirthRing_{index}"
    ring.data.materials.append(gold)
    ring.parent = root

bpy.ops.object.light_add(type="AREA", location=(1.6, -3.0, 2.3))
bpy.context.object.data.energy = 900
bpy.context.object.data.size = 3.0
bpy.ops.object.camera_add(location=(0.0, -3.5, 0.05), rotation=(math.pi / 2, 0, 0))
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

"""Create Second Wind: a crimson phoenix rises as embers return to it."""
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


gold = material("SecondWindSoulGold", (0.98, 0.62, 0.07), metallic=0.42, roughness=0.14, emission=2.3)
white = material("SecondWindSoulWhite", (0.93, 0.88, 0.64), metallic=0.12, roughness=0.18, emission=2.6)
crimson = material("SecondWindPhoenixCrimson", (0.78, 0.008, 0.018), metallic=0.30, roughness=0.18, emission=2.2)
deep_red = material("SecondWindPhoenixDeepRed", (0.34, 0.004, 0.01), metallic=0.22, roughness=0.22, emission=1.0)

root = bpy.data.objects.new("SecondWindRoot", None)
bpy.context.collection.objects.link(root)
phoenix = bpy.data.objects.new("SecondWind_PhoenixGroup", None)
bpy.context.collection.objects.link(phoenix)
phoenix.parent = root

# Front-facing phoenix silhouette: body, crowned head, gold beak, spread feathered wings and long tails.
bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=0.18, location=(0.0, -0.02, 0.05))
body = bpy.context.object
body.name = "SecondWind_PhoenixBody"
body.scale = (0.72, 0.42, 1.32)
body.data.materials.append(crimson)
body.parent = phoenix
bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=10, radius=0.105, location=(0.0, -0.02, 0.31))
head = bpy.context.object
head.name = "SecondWind_PhoenixHead"
head.scale = (0.85, 0.52, 1.0)
head.data.materials.append(crimson)
head.parent = phoenix
bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=0.065, radius2=0.0, depth=0.16,
                                location=(0.0, -0.13, 0.31), rotation=(math.pi / 2, 0, 0))
beak = bpy.context.object
beak.name = "SecondWind_PhoenixBeak"
beak.data.materials.append(gold)
beak.parent = phoenix
for index, x in enumerate((-0.055, 0.0, 0.055), start=1):
    bpy.ops.mesh.primitive_cone_add(vertices=10, radius1=0.035, radius2=0.0, depth=0.16,
                                    location=(x, 0.0, 0.47))
    crest = bpy.context.object
    crest.name = f"SecondWind_PhoenixCrest_{index}"
    crest.data.materials.append(gold if index == 2 else crimson)
    crest.parent = phoenix

for side, sign in (("Left", -1), ("Right", 1)):
    wing = bpy.data.objects.new(f"SecondWind_WingGroup_{side}", None)
    bpy.context.collection.objects.link(wing)
    wing.parent = phoenix
    for index in range(5):
        x = sign * (0.18 + index * 0.105)
        z = 0.18 - index * 0.055
        bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=0.085, radius2=0.015, depth=0.38,
                                        location=(x, 0.0, z), rotation=(0, sign * math.radians(66), 0))
        feather = bpy.context.object
        feather.name = f"SecondWind_{side}WingFeather_{index + 1}"
        feather.scale = (1.0, 0.48, 1.0 + index * 0.08)
        feather.data.materials.append(crimson if index < 3 else deep_red)
        feather.parent = wing

for index, x in enumerate((-0.16, -0.08, 0.0, 0.08, 0.16), start=1):
    bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=0.065, radius2=0.012, depth=0.52 + abs(x) * 0.5,
                                    location=(x, 0.02, -0.38), rotation=(math.pi, 0, 0))
    tail = bpy.context.object
    tail.name = f"SecondWind_PhoenixTail_{index}"
    tail.rotation_euler.y = x * 1.8
    tail.data.materials.append(gold if index in (1, 5) else crimson)
    tail.parent = phoenix

# Returning embers begin around the fallen phoenix and converge at runtime.
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

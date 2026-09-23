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


def flat_shape(name, points, mat, parent, y=0.012):
    """Create a broad front-facing emblem shape in the X/Z plane."""
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata([(x, y, z) for x, z in points], [], [list(range(len(points)))])
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    return obj


gold = material("SecondWindSoulGold", (0.98, 0.62, 0.07), metallic=0.42, roughness=0.14, emission=2.3)
white = material("SecondWindSoulWhite", (0.93, 0.88, 0.64), metallic=0.12, roughness=0.18, emission=2.6)
crimson = material("SecondWindPhoenixCrimson", (0.78, 0.008, 0.018), metallic=0.30, roughness=0.18, emission=2.2)
deep_red = material("SecondWindPhoenixDeepRed", (0.34, 0.004, 0.01), metallic=0.22, roughness=0.22, emission=1.0)

root = bpy.data.objects.new("SecondWindRoot", None)
bpy.context.collection.objects.link(root)
phoenix = bpy.data.objects.new("SecondWind_PhoenixGroup", None)
bpy.context.collection.objects.link(phoenix)
phoenix.parent = root

# Front-facing heraldic phoenix silhouette.  The camera looks along +Y, so every identifying
# feature is deliberately laid out in the X/Z plane (the old beak pointed into the camera and
# read as a faceless blob).
bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=0.18, location=(0.0, -0.02, 0.05))
body = bpy.context.object
body.name = "SecondWind_PhoenixBody"
body.scale = (0.58, 0.34, 1.16)
body.data.materials.append(crimson)
body.parent = phoenix
# S-curved neck and side-profile head, facing screen-left like the reference silhouette.
for index, (x, z, sx, sz) in enumerate(((0.00, 0.22, 0.075, 0.15), (-0.07, 0.32, 0.070, 0.13),
                                        (-0.16, 0.40, 0.075, 0.115)), start=1):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=10, radius=1.0, location=(x, -0.02, z))
    neck = bpy.context.object
    neck.name = f"SecondWind_PhoenixNeck_{index}"
    neck.scale = (sx, 0.045, sz)
    neck.data.materials.append(crimson)
    neck.parent = phoenix
bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=0.105, location=(-0.245, -0.02, 0.485))
head = bpy.context.object
head.name = "SecondWind_PhoenixHead"
head.scale = (1.05, 0.48, 0.78)
head.data.materials.append(crimson)
head.parent = phoenix
# Beak points left across the screen, with a bright eye so the face remains readable in-game.
bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=0.055, radius2=0.0, depth=0.18,
                                location=(-0.385, -0.02, 0.485), rotation=(0, -math.pi / 2, 0))
beak = bpy.context.object
beak.name = "SecondWind_PhoenixBeak"
beak.data.materials.append(gold)
beak.parent = phoenix
bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=0.022, location=(-0.275, -0.068, 0.515))
eye = bpy.context.object
eye.name = "SecondWind_PhoenixEye"
eye.data.materials.append(white)
eye.parent = phoenix
for index, (x, tilt) in enumerate(((-0.30, -0.22), (-0.24, 0.0), (-0.18, 0.22)), start=1):
    bpy.ops.mesh.primitive_cone_add(vertices=10, radius1=0.032, radius2=0.0, depth=0.18,
                                    location=(x, -0.01, 0.565), rotation=(0, tilt, 0))
    crest = bpy.context.object
    crest.name = f"SecondWind_PhoenixCrest_{index}"
    crest.data.materials.append(gold if index == 2 else crimson)
    crest.parent = phoenix

for side, sign in (("Left", -1), ("Right", 1)):
    wing = bpy.data.objects.new(f"SecondWind_WingGroup_{side}", None)
    bpy.context.collection.objects.link(wing)
    wing.parent = phoenix
    # One broad heraldic panel makes the bird read as a phoenix rather than a chicken at game scale.
    panel_points = [(sign * 0.08, 0.18), (sign * 0.26, 0.34), (sign * 0.52, 0.56),
                    (sign * 0.82, 0.70), (sign * 0.68, 0.40), (sign * 0.92, 0.48),
                    (sign * 0.72, 0.22), (sign * 0.90, 0.22), (sign * 0.58, 0.04),
                    (sign * 0.30, 0.02)]
    if sign < 0:
        panel_points.reverse()
    flat_shape(f"SecondWind_{side}WingSilhouette", panel_points, crimson, wing)
    for index in range(6):
        angle = math.radians(26 + index * 9)
        length = 0.44 + index * 0.070
        x = sign * (0.20 + math.cos(angle) * length * 0.50)
        z = 0.13 + math.sin(angle) * length * 0.48
        bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=10, radius=1.0,
                                             location=(x, 0.0, z), rotation=(0, sign * -angle, 0))
        feather = bpy.context.object
        feather.name = f"SecondWind_{side}WingFeather_{index + 1}"
        feather.scale = (length * 0.58, 0.035, 0.055 + index * 0.005)
        feather.data.materials.append(crimson if index < 3 else deep_red)
        feather.parent = wing

for index, x in enumerate((-0.18, -0.09, 0.0, 0.09, 0.18), start=1):
    bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=0.065, radius2=0.012, depth=0.52 + abs(x) * 0.5,
                                    location=(x, 0.02, -0.38), rotation=(math.pi, 0, 0))
    tail = bpy.context.object
    tail.name = f"SecondWind_PhoenixTail_{index}"
    tail.scale.z = 1.30
    tail.rotation_euler.y = x * 2.0
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

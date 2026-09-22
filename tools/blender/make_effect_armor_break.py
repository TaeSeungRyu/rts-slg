"""Create the Armor Break active effect asset: a stylised East-Asian plate cuirass."""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-armor-break.glb")
PREVIEW = os.path.join(ROOT, ".tmp-armor-break-preview.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True


def material(name, color, metallic=0.0, roughness=0.25, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Strength"].default_value = emission
    return mat


iron = material("ArmorDarkIron", (0.055, 0.085, 0.13), 0.92, 0.19, 0.28)
steel = material("ArmorSteelEdge", (0.31, 0.44, 0.58), 0.88, 0.16, 0.42)
gold = material("ArmorGoldTrim", (0.70, 0.39, 0.07), 0.83, 0.18, 0.34)
crack = material("ArmorCrackGlow", (0.65, 0.84, 1.0), 0.15, 0.10, 4.0)

root = bpy.data.objects.new("ArmorBreakRoot", None)
bpy.context.collection.objects.link(root)


def bevel(obj, width=0.025, segments=2):
    mod = obj.modifiers.new("ForgedBevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"


def cube(name, location, scale, mat, rotation=(0.0, 0.0, 0.0), bevel_width=0.018):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    bevel(obj, bevel_width)
    obj.parent = root
    return obj


def custom_plate(name, x0, x1, top_z, waist_z, bottom_z, outer_top, outer_waist, outer_bottom, mat):
    """Extruded half breastplate with a tapered waist and raised centre seam."""
    inner = 0.012
    if x1 < 0:
        points = [(-inner, top_z), (-outer_top, top_z - 0.03), (-outer_waist, waist_z),
                  (-outer_bottom, bottom_z), (-inner, bottom_z)]
    else:
        points = [(inner, top_z), (outer_top, top_z - 0.03), (outer_waist, waist_z),
                  (outer_bottom, bottom_z), (inner, bottom_z)]
    depth = 0.055
    verts = [(x, -depth, z) for x, z in points] + [(x, depth, z) for x, z in points]
    n = len(points)
    faces = [tuple(range(n)), tuple(range(n, n * 2))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bevel(obj, 0.018, 3)
    obj.parent = root
    return obj


# Two forged breastplates form a broad chest and narrow waist; there are deliberately no arm meshes.
custom_plate("ArmorBreak_BreastplateLeft", -1, -1, 0.28, 0.02, -0.25, 0.29, 0.25, 0.20, iron)
custom_plate("ArmorBreak_BreastplateRight", 1, 1, 0.28, 0.02, -0.25, 0.29, 0.25, 0.20, iron)

# Raised steel ribs make the torso read as rigid armour instead of cloth.
cube("ArmorBreak_CenterRidge", (0.0, -0.068, 0.015), (0.023, 0.018, 0.275), gold, bevel_width=0.010)
cube("ArmorBreak_UpperBand", (0.0, -0.071, 0.18), (0.245, 0.016, 0.022), steel, bevel_width=0.010)
cube("ArmorBreak_LowerBand", (0.0, -0.071, -0.12), (0.205, 0.016, 0.019), gold, bevel_width=0.009)

# Overlapping lamellar rows and rivets remove the flat-cloth appearance of the torso.
for row, z in enumerate((0.105, 0.025, -0.055)):
    width = 0.205 - row * 0.012
    for side, sign in (("Left", -1), ("Right", 1)):
        cube(f"ArmorBreak_Lamella_{row + 1}_{side}", (sign * width * 0.52, -0.078, z),
             (width * 0.48, 0.012, 0.027), steel, bevel_width=0.007)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=5, radius=0.013,
                                             location=(sign * width * 0.54, -0.096, z))
        rivet = bpy.context.object
        rivet.name = f"ArmorBreak_Rivet_{row + 1}_{side}"
        rivet.scale = (1.0, 0.45, 1.0)
        rivet.data.materials.append(gold)
        rivet.parent = root

# High V-shaped gorget protects the neck. It is visibly rigid armour, not a head or an arm.
cube("ArmorBreak_GorgetLeft", (-0.072, -0.015, 0.315), (0.095, 0.046, 0.026), gold,
     rotation=(0.0, 0.0, math.radians(-17)), bevel_width=0.012)
cube("ArmorBreak_GorgetRight", (0.072, -0.015, 0.315), (0.095, 0.046, 0.026), gold,
     rotation=(0.0, 0.0, math.radians(17)), bevel_width=0.012)

# Layered angular pauldrons stop at the shoulder line; no round mesh hangs down like a human arm.
for side, sign in (("Left", -1), ("Right", 1)):
    for layer, (x, z, sx, sz) in enumerate(((0.31, 0.20, 0.135, 0.060),
                                             (0.35, 0.13, 0.105, 0.050))):
        cube(f"ArmorBreak_Pauldron{side}_{layer + 1}", (sign * x, 0.0, z),
             (sx, 0.052, sz), gold if layer == 0 else steel,
             rotation=(0.0, 0.0, math.radians(sign * -12)), bevel_width=0.020)

# Three hanging tassets make the lower silhouette visibly plated.
for i, x in enumerate((-0.15, 0.0, 0.15)):
    tasset = cube(f"ArmorBreak_Tasset_{i + 1}", (x, 0.0, -0.34), (0.075, 0.048, 0.105),
                  steel if i != 1 else gold, rotation=(0.0, 0.0, math.radians(x * -35)), bevel_width=0.012)

# Inlaid lightning-shaped crack. It stays hidden until Godot scales it during the breaking phase.
crack_points = [(-0.015, -0.083, 0.25), (0.030, -0.083, 0.12), (-0.018, -0.083, 0.02),
                (0.035, -0.083, -0.10), (-0.010, -0.083, -0.27)]
for i in range(len(crack_points) - 1):
    a, b = crack_points[i], crack_points[i + 1]
    dx, dz = b[0] - a[0], b[2] - a[2]
    length = math.sqrt(dx * dx + dz * dz)
    obj = cube(f"ArmorBreak_Crack_{i + 1}", ((a[0] + b[0]) / 2, a[1], (a[2] + b[2]) / 2),
               (0.009, 0.008, length / 2), crack, rotation=(0.0, math.atan2(dx, dz), 0.0), bevel_width=0.004)
    obj.hide_render = False

# Preview lighting/camera, excluded from GLB export by exporting selected armour hierarchy only.
bpy.ops.object.light_add(type="AREA", location=(2.2, -3.0, 2.8))
key = bpy.context.object
key.data.energy = 900
key.data.shape = "DISK"
key.data.size = 3.0
bpy.ops.object.light_add(type="AREA", location=(-2.0, -1.2, 1.2))
fill = bpy.context.object
fill.data.energy = 500
fill.data.color = (0.28, 0.48, 1.0)
fill.data.size = 2.5
bpy.ops.object.camera_add(location=(0.0, -3.6, 0.25), rotation=(math.radians(84), 0.0, 0.0))
camera = bpy.context.object
camera.rotation_euler = (math.radians(90), 0.0, 0.0)
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

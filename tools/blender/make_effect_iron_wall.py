"""Create Iron Wall: a heavy blue-steel cuirass that protects the caster."""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-iron-wall.glb")
PREVIEW = os.path.join(ROOT, ".tmp-iron-wall-preview.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True


def material(name, color, metallic=0.0, roughness=0.22, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Strength"].default_value = emission
    return mat


dark = material("IronWallDeepSteel", (0.035, 0.09, 0.16), 0.94, 0.16, 0.32)
blue = material("IronWallBlueSteel", (0.12, 0.34, 0.62), 0.90, 0.15, 0.78)
silver = material("IronWallSilverEdge", (0.58, 0.75, 0.90), 0.91, 0.12, 1.35)
cyan = material("IronWallWardGlow", (0.14, 0.76, 1.0), 0.35, 0.10, 5.5)

root = bpy.data.objects.new("IronWallRoot", None)
bpy.context.collection.objects.link(root)


def bevel(obj, width=0.018, segments=2):
    mod = obj.modifiers.new("FortifiedBevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"


def cube(name, location, scale, mat, rotation=(0.0, 0.0, 0.0), width=0.015):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    bevel(obj, width, 3)
    obj.parent = root
    return obj


# Wide rigid torso: overlapping plates, never arm-shaped.
for row, (z, width) in enumerate(((0.20, 0.28), (0.10, 0.30), (0.00, 0.285), (-0.10, 0.26), (-0.20, 0.225))):
    cube(f"IronWall_ChestPlate_{row + 1}", (0.0, 0.0, z), (width, 0.060, 0.060),
         blue if row % 2 == 0 else dark, width=0.020)
    cube(f"IronWall_ChestEdge_{row + 1}", (0.0, -0.068, z + 0.052), (width * 0.94, 0.012, 0.010),
         silver, width=0.006)

# V gorget and compact layered pauldrons.
cube("IronWall_GorgetLeft", (-0.075, -0.005, 0.31), (0.105, 0.052, 0.030), silver,
     rotation=(0.0, 0.0, math.radians(-18)), width=0.012)
cube("IronWall_GorgetRight", (0.075, -0.005, 0.31), (0.105, 0.052, 0.030), silver,
     rotation=(0.0, 0.0, math.radians(18)), width=0.012)
for side, sign in (("Left", -1), ("Right", 1)):
    cube(f"IronWall_Pauldron{side}_Upper", (sign * 0.34, 0.0, 0.19), (0.15, 0.070, 0.070), silver,
         rotation=(0.0, 0.0, math.radians(sign * -13)), width=0.024)
    cube(f"IronWall_Pauldron{side}_Lower", (sign * 0.37, 0.0, 0.10), (0.115, 0.060, 0.052), blue,
         rotation=(0.0, 0.0, math.radians(sign * -10)), width=0.020)

# Central shield crest and cyan ward gem make the defensive purpose immediately legible.
bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.105, depth=0.032,
                                    location=(0.0, -0.083, 0.045), rotation=(math.pi / 2, 0.0, 0.0))
crest = bpy.context.object
crest.name = "IronWall_ShieldCrest"
crest.scale = (0.82, 1.0, 1.12)
crest.data.materials.append(silver)
crest.parent = root
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.048, location=(0.0, -0.108, 0.045))
gem = bpy.context.object
gem.name = "IronWall_WardGem"
gem.scale = (0.72, 0.45, 1.0)
gem.data.materials.append(cyan)
gem.parent = root

# A luminous protection ring belongs to the GLB and pulses with the armour.
bpy.ops.mesh.primitive_torus_add(major_radius=0.42, minor_radius=0.014, major_segments=32,
                                 minor_segments=6, location=(0.0, 0.0, 0.03), rotation=(math.pi / 2, 0.0, 0.0))
ring = bpy.context.object
ring.name = "IronWall_ProtectionRing"
ring.data.materials.append(cyan)
ring.parent = root

# Preview only.
bpy.ops.object.light_add(type="AREA", location=(2.2, -3.0, 2.8))
key = bpy.context.object
key.data.energy = 950
key.data.size = 3.0
bpy.ops.object.light_add(type="AREA", location=(-2.0, -1.2, 1.4))
fill = bpy.context.object
fill.data.energy = 600
fill.data.color = (0.15, 0.45, 1.0)
fill.data.size = 2.5
bpy.ops.object.camera_add(location=(0.0, -3.6, 0.25), rotation=(math.pi / 2, 0.0, 0.0))
camera = bpy.context.object
camera.rotation_euler = (math.pi / 2, 0.0, 0.0)
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

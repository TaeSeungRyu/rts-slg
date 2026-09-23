"""Create Regroup: a glass syringe with red medicine and a movable plunger."""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "effect-regroup.glb")
PREVIEW = os.path.join(ROOT, ".tmp-regroup-preview.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True


def material(name, color, metallic=0.0, roughness=0.3, emission=0.0, alpha=1.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (*color, alpha)
    mat.surface_render_method = "DITHERED" if alpha < 1.0 else "DITHERED"
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, alpha)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Alpha"].default_value = alpha
    bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Strength"].default_value = emission
    return mat


glass = material("RegroupGlass", (0.64, 0.82, 0.88), roughness=0.16, emission=0.35, alpha=0.36)
metal = material("RegroupNeedleSteel", (0.62, 0.70, 0.74), metallic=0.9, roughness=0.14, emission=0.4)
red = material("RegroupMedicineRed", (0.74, 0.01, 0.018), roughness=0.22, emission=1.55)
plunger_mat = material("RegroupPlungerIvory", (0.82, 0.84, 0.82), roughness=0.28, emission=0.55)

root = bpy.data.objects.new("RegroupSyringeRoot", None)
bpy.context.collection.objects.link(root)
root.rotation_euler[1] = math.radians(-18)


def cylinder(name, radius, depth, x, mat, parent=root):
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=radius, depth=depth,
                                        location=(x, 0.0, 0.0), rotation=(0, math.pi / 2, 0))
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    obj.parent = parent
    return obj


# Barrel and contained medicine run left-to-right toward the needle.
cylinder("Regroup_Barrel", 0.115, 0.72, 0.02, glass)
liquid = cylinder("Regroup_Liquid", 0.081, 0.46, 0.105, red)
cylinder("Regroup_NeedleHub", 0.068, 0.13, 0.445, metal)
cylinder("Regroup_Needle", 0.014, 0.46, 0.735, metal)

plunger = bpy.data.objects.new("Regroup_PlungerGroup", None)
bpy.context.collection.objects.link(plunger)
plunger.parent = root
cylinder("Regroup_PlungerRod", 0.027, 0.49, -0.47, plunger_mat, plunger)
cylinder("Regroup_PlungerSeal", 0.098, 0.055, -0.205, red, plunger)
cylinder("Regroup_PlungerThumb", 0.18, 0.045, -0.735, plunger_mat, plunger)

# Finger flanges make the silhouette immediately recognizable as a syringe.
for z in (-0.19, 0.19):
    bpy.ops.mesh.primitive_cube_add(location=(-0.34, 0.0, z))
    flange = bpy.context.object
    flange.name = f"Regroup_FingerFlange_{'Top' if z > 0 else 'Bottom'}"
    flange.scale = (0.045, 0.035, 0.16)
    flange.data.materials.append(plunger_mat)
    flange.parent = root

bpy.ops.object.light_add(type="AREA", location=(1.8, -3.0, 2.4))
bpy.context.object.data.energy = 850
bpy.context.object.data.size = 3.0
bpy.ops.object.camera_add(location=(0.0, -3.6, 0.0), rotation=(math.pi / 2, 0, 0))
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

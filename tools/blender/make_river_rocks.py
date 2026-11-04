import bpy
from pathlib import Path


root = Path(__file__).resolve().parents[2]
models = root / "SanguoSLG.Game" / "assets" / "models"
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(models / "water.glb"))
water = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.name.startswith("water"))

stone = bpy.data.materials.new("river-stone")
stone.diffuse_color = (0.48, 0.51, 0.49, 1)
stone.use_nodes = True
stone.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = stone.diffuse_color

for index, (x, y, width, height, angle) in enumerate([
    (-0.22, -0.11, 0.15, 0.11, 19),
    (0.21, 0.20, 0.12, 0.09, -28),
    (0.08, -0.28, 0.09, 0.07, 33),
]):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1, location=(x, y, 0.11))
    rock = bpy.context.object
    rock.name = f"river-rock-{index + 1}"
    rock.scale = (width, height, 0.055)
    rock.rotation_euler.z = angle * 0.01745329252
    rock.data.materials.append(stone)

bpy.ops.object.select_all(action="DESELECT")
water.select_set(True)
for obj in bpy.context.scene.objects:
    if obj.name.startswith("river-rock-"):
        obj.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(models / "river-rocky.glb"), export_format="GLB",
    use_selection=True, export_apply=True)

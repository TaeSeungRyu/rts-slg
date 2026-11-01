import bpy
import math
import random
from pathlib import Path

OUT = Path(__file__).resolve().parents[2] / "SanguoSLG.Game/assets/models"


def material(name, rgb):
    item = bpy.data.materials.new(name)
    item.diffuse_color = (*rgb, 1)
    item.use_nodes = True
    item.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1)
    return item


def cone(name, x, y, z, radius, height, mat, vertices=7):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=0.008,
                                    depth=height, location=(x, y, z + height / 2))
    bpy.context.object.name = name
    bpy.context.object.data.materials.append(mat)


for variant in (2, 3):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    grass = material("forest_ground", (0.19, 0.60, 0.37))
    side = material("forest_soil", (0.60, 0.44, 0.29))
    trunk = material("forest_trunk", (0.34, 0.25, 0.16))
    leaves = [material("forest_leaf_" + str(i), color) for i, color in enumerate(
        ((0.08, 0.37, 0.24), (0.11, 0.49, 0.29), (0.17, 0.55, 0.32)))]
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.5774, depth=0.2,
                                        location=(0, 0, 0.1))
    base = bpy.context.object
    base.name = "forest_hex_base"
    base.data.materials.append(side)
    base.data.materials.append(grass)
    for face in base.data.polygons:
        if face.normal.z > 0.5:
            face.material_index = 1

    rng = random.Random(variant * 117)
    positions = ((-.25, -.09), (.03, .22), (.25, -.12), (-.09, -.29), (.28, .20)) if variant == 2 else (
        (-.28, .08), (-.04, -.10), (.23, .20), (.24, -.22), (-.19, -.27), (.04, .31))
    for index, (x, y) in enumerate(positions):
        height = rng.uniform(.20, .32) * (1.18 if variant == 3 and index == 1 else 1)
        cone(f"trunk_{index}", x, y, .2, .027, height * .65, trunk, 6)
        crown = leaves[(index + variant) % len(leaves)]
        cone(f"lower_crown_{index}", x, y, .2 + height * .35, .11, height * .58, crown)
        cone(f"upper_crown_{index}", x, y, .2 + height * .65, .075, height * .45, crown)
    bpy.ops.object.select_all(action="SELECT")
    target = OUT / f"grass-forest-{variant}.glb"
    bpy.ops.export_scene.gltf(filepath=str(target), export_format="GLB", use_selection=True)
    print("EXPORTED", target)

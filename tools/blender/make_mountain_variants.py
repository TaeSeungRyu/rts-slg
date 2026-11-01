import bpy
import math
from pathlib import Path

OUT = Path(__file__).resolve().parents[2] / "SanguoSLG.Game/assets/models"


def mat(name, rgb):
    item = bpy.data.materials.new(name)
    item.diffuse_color = (*rgb, 1)
    item.use_nodes = True
    item.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1)
    return item


def peak(name, x, y, radius, height, material, bottom=.2):
    bpy.ops.mesh.primitive_cone_add(vertices=7, radius1=radius, radius2=0.015,
                                    depth=height, location=(x, y, bottom + height / 2))
    bpy.context.object.name = name
    bpy.context.object.data.materials.append(material)


for variant in (2, 3):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    grass = mat("mountain_grass", (.14, .62, .35))
    soil = mat("mountain_soil", (.62, .45, .30))
    slopes = [mat("mountain_slope_a", (.10, .47, .25)),
              mat("mountain_slope_b", (.13, .54, .29))]
    rock = mat("mountain_rock", (.39, .38, .35))
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=.5774, depth=.2, location=(0, 0, .1))
    base = bpy.context.object
    base.name = "mountain_hex_base"
    base.data.materials.append(soil)
    base.data.materials.append(grass)
    for face in base.data.polygons:
        if face.normal.z > .5:
            face.material_index = 1
    peaks = ((-.18, -.02, .29, .32), (.20, .12, .27, .46), (.12, -.28, .19, .22)) if variant == 2 else (
        (-.13, .14, .34, .45), (.23, -.18, .23, .30), (-.26, -.21, .18, .24))
    for index, (x, y, radius, height) in enumerate(peaks):
        peak(f"slope_{index}", x, y, radius, height, slopes[index % 2])
        peak(f"stone_cap_{index}", x, y, radius * .27, height * .27, rock,
             .2 + height * .73)
    if variant == 3:
        for index, (x, y) in enumerate(((.34, .17), (-.36, .06))):
            peak(f"foothill_tree_{index}", x, y, .055, .12, slopes[0])
    bpy.ops.object.select_all(action="SELECT")
    target = OUT / f"mountain-small-{variant}.glb"
    bpy.ops.export_scene.gltf(filepath=str(target), export_format="GLB", use_selection=True)
    print("EXPORTED", target)

import bpy
from pathlib import Path

OUT = Path(__file__).resolve().parents[2] / "SanguoSLG.Game/assets/models"


def mat(name, color):
    item = bpy.data.materials.new(name)
    item.diffuse_color = (*color, 1)
    item.use_nodes = True
    item.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*color, 1)
    return item


def rock(name, x, y, radius, height, material, vertices=6):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=.012,
                                    depth=height, location=(x, y, .2 + height / 2))
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    obj.rotation_euler.z = (x - y) * .4


for variant in (2, 3):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    soil = mat("rock_soil", (.60, .45, .30))
    ground = mat("rock_ground", (.40, .43, .39))
    slate = mat("slate", (.35, .36, .34))
    granite = mat("granite", (.48, .47, .43))
    shadow = mat("shadow_rock", (.28, .30, .29))
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=.5774, depth=.2, location=(0, 0, .1))
    base = bpy.context.object
    base.name = "rough_mountain_hex_base"
    base.data.materials.append(soil)
    base.data.materials.append(ground)
    for face in base.data.polygons:
        if face.normal.z > .5:
            face.material_index = 1
    formations = ((-.17, .03, .29, .48), (.19, -.13, .24, .36), (.18, .26, .16, .25)) if variant == 2 else (
        (-.23, -.11, .22, .33), (-.02, .14, .26, .51), (.25, -.12, .22, .39), (.26, .25, .13, .20))
    for index, (x, y, radius, height) in enumerate(formations):
        rock(f"dark_ridge_{index}", x, y, radius, height, slate if index % 2 else shadow)
        rock(f"light_facet_{index}", x - .035, y + .018, radius * .48,
             height * .72, granite, 5)
    bpy.ops.object.select_all(action="SELECT")
    target = OUT / f"stone-mountain-{variant}.glb"
    bpy.ops.export_scene.gltf(filepath=str(target), export_format="GLB", use_selection=True)
    print("EXPORTED", target)

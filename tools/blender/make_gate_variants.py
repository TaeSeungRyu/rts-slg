import math
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "SanguoSLG.Game" / "assets" / "models"


def material(name, rgb, metallic=0.0):
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*rgb, 1.0)
    result.use_nodes = True
    shader = result.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*rgb, 1.0)
    shader.inputs["Roughness"].default_value = 0.78
    shader.inputs["Metallic"].default_value = metallic
    return result


def cube(name, location, size, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    return obj


def prism(name, vertices, faces, mat):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(mat)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def roof(name, x, y, z, width, depth, height, mats):
    tile, rim, ridge = mats
    eave = width * 0.57
    d = depth * 0.58
    top = width * 0.11
    v = [
        (x-eave, y-d, z), (x+eave, y-d, z),
        (x+eave, y+d, z), (x-eave, y+d, z),
        (x-top, y-d*0.80, z+height), (x+top, y-d*0.80, z+height),
        (x+top, y+d*0.80, z+height), (x-top, y+d*0.80, z+height),
    ]
    prism(name+"_tiles", v,
          [(0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7), (4, 5, 6, 7)], tile)
    for side, yy in (("front", y-d), ("back", y+d)):
        cube(name+"_"+side+"_eave", (x, yy, z+0.004), (width*1.16, 0.025, 0.026), rim)
    for side, xx in (("left", x-eave), ("right", x+eave)):
        cube(name+"_"+side+"_eave", (xx, y, z+0.004), (0.025, depth*1.16, 0.026), rim)
    cube(name+"_ridge", (x, y, z+height+0.009), (width*0.28, 0.034, 0.026), ridge)
    for sx in (-1, 1):
        for sy in (-1, 1):
            cube(name+f"_corner_{sx}_{sy}",
                 (x+sx*eave, y+sy*d, z+0.014), (0.06, 0.06, 0.035), ridge)


def make_gate(size):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    medium = size == "medium"
    ground = material("warm_stone", (0.50, 0.47, 0.39))
    wall = material("plaster_wall", (0.68, 0.65, 0.55))
    shadow = material("stone_edges", (0.43, 0.43, 0.39))
    wood = material("dark_wood", (0.22, 0.15, 0.105))
    door = material("red_brown_gate", (0.40, 0.16, 0.11))
    roof_tile = material("blue_black_roof", (0.075, 0.13, 0.17))
    roof_rim = material("roof_wood_rim", (0.24, 0.17, 0.12))
    bronze = material("bronze_details", (0.66, 0.49, 0.22), 0.35)

    radius = 0.55
    vertices = [(radius*math.cos(math.pi*i/3), radius*math.sin(math.pi*i/3), 0.0)
                for i in range(6)]
    vertices += [(x, y, 0.065) for x, y, _ in vertices]
    faces = [(6, 7, 8, 9, 10, 11), (5, 4, 3, 2, 1, 0)]
    faces += [(i, (i+1)%6, (i+1)%6+6, i+6) for i in range(6)]
    prism("hex_foundation", vertices, faces, ground)
    cube("front_road", (0, -0.40, 0.069), (0.24, 0.27, 0.017), shadow)
    cube("rear_road", (0, 0.40, 0.069), (0.24, 0.27, 0.017), shadow)

    gap = 0.21 if medium else 0.18
    tower_x = 0.35 if medium else 0.32
    wall_height = 0.30 if medium else 0.25
    for sign, label in ((-1, "left"), (1, "right")):
        pillar_x = sign*(gap/2+0.045)
        cube(label+"_gate_pillar", (pillar_x, 0, 0.27),
             (0.095, 0.20, 0.41), wall)
        cube(label+"_pillar_trim", (pillar_x, -0.107, 0.27),
             (0.084, 0.018, 0.36), shadow)
        cube(label+"_wall", (sign*(tower_x+gap/2)/2, 0, 0.065+wall_height/2),
             (tower_x-gap/2-0.07, 0.16, wall_height), wall)
        cube(label+"_wall_cap", (sign*(tower_x+gap/2)/2, 0, 0.065+wall_height+0.018),
             (tower_x-gap/2-0.06, 0.19, 0.036), shadow)
        for y in (-0.085, 0.085):
            cube(label+f"_merlon_{y}", (sign*0.24, y, 0.065+wall_height+0.061),
                 (0.074, 0.058, 0.085), wall)

        cube(label+"_watchtower", (sign*tower_x, 0, 0.065+0.255),
             (0.22, 0.23, 0.51 if medium else 0.42), wall)
        cube(label+"_watchtower_band", (sign*tower_x, -0.123, 0.38),
             (0.225, 0.018, 0.034), wood)
        roof(label+"_tower_roof", sign*tower_x, 0,
             0.61 if medium else 0.52, 0.30, 0.33, 0.14, (roof_tile, roof_rim, bronze))

    for sign, label in ((-1, "left"), (1, "right")):
        cube(label+"_door_leaf", (sign*gap/4, -0.10, 0.20),
             (gap/2-0.008, 0.024, 0.25), door)
        cube(label+"_door_strap", (sign*gap/4, -0.119, 0.28),
             (gap/2-0.018, 0.012, 0.023), bronze)
        cube(label+"_door_stud", (sign*gap/4, -0.128, 0.18),
             (0.024, 0.012, 0.024), bronze)
    cube("gate_lintel", (0, 0, 0.48), (0.39 if medium else 0.35, 0.23, 0.085), wood)
    cube("gate_upper", (0, 0, 0.58), (0.35 if medium else 0.31, 0.23, 0.13), wall)
    cube("gate_name_panel", (0, -0.126, 0.565), (0.145, 0.016, 0.07), wood)
    roof("main_roof", 0, 0, 0.68 if medium else 0.665,
         0.49 if medium else 0.43, 0.37, 0.17 if medium else 0.14,
         (roof_tile, roof_rim, bronze))
    if medium:
        roof("upper_roof", 0, 0, 0.845, 0.27, 0.27, 0.11,
             (roof_tile, roof_rim, bronze))
        for x in (-0.33, 0.33):
            cube(f"signal_banner_pole_{x}", (x, 0.025, 0.83), (0.017, 0.017, 0.29), wood)
            cube(f"signal_banner_{x}", (x+0.035, 0.025, 0.88),
                 (0.065, 0.012, 0.10), door)

    OUTPUT.mkdir(parents=True, exist_ok=True)
    target = OUTPUT / f"gate-{size}.glb"
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=str(target), export_format="GLB", use_selection=True)
    print(f"GATE_ASSET={target} objects={len(bpy.data.objects)}")


if __name__ == "__main__":
    for variant in ("small", "medium"):
        make_gate(variant)

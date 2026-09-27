"""Create the persistent blue-gold barrier shown over a protected ruin."""
import bpy
import math
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "SanguoSLG.Game" / "assets" / "models" / "effect-ruin-protection.glb"

bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, color, emission, alpha):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.surface_render_method = "DITHERED"
    mat.diffuse_color = (*color, alpha)
    shader = mat.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (*color, alpha)
    shader.inputs["Emission Color"].default_value = (*color, 1.0)
    shader.inputs["Emission Strength"].default_value = emission
    shader.inputs["Alpha"].default_value = alpha
    shader.inputs["Roughness"].default_value = 0.18
    return mat


BLUE = material("BarrierBlue", (0.10, 0.48, 1.0), 4.0, 0.16)
CYAN = material("BarrierEdge", (0.28, 0.85, 1.0), 7.0, 0.62)
GOLD = material("BarrierSeal", (1.0, 0.67, 0.16), 5.0, 0.82)

root = bpy.data.objects.new("RuinProtectionRoot", None)
bpy.context.collection.objects.link(root)

# 반투명 구체를 바닥 아래로 내려 상단만 돔처럼 보이게 한다.
bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=0.64, location=(0, 0, 0.20))
dome = bpy.context.object
dome.name = "ProtectionDome"
dome.scale = (1.0, 1.0, 0.78)
dome.data.materials.append(BLUE)
dome.parent = root

for index, (radius, z, tilt) in enumerate(((0.55, 0.04, 0), (0.47, 0.30, 58), (0.47, 0.30, -58))):
    bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=0.012,
                                    major_segments=48, minor_segments=6,
                                    location=(0, 0, z), rotation=(math.radians(tilt), 0, 0))
    ring = bpy.context.object
    ring.name = f"ProtectionRing_{index + 1}"
    ring.data.materials.append(CYAN)
    ring.parent = root

# 바닥 봉인 문양: 육각 테두리와 여섯 광점.
bpy.ops.mesh.primitive_torus_add(major_radius=0.39, minor_radius=0.016,
                                major_segments=6, minor_segments=6, location=(0, 0, 0.035))
seal = bpy.context.object
seal.name = "ProtectionSeal"
seal.data.materials.append(GOLD)
seal.parent = root

for i in range(6):
    angle = math.tau * i / 6
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.027,
                                         location=(math.cos(angle) * 0.39,
                                                   math.sin(angle) * 0.39, 0.045))
    mote = bpy.context.object
    mote.name = f"SealMote_{i + 1}"
    mote.data.materials.append(GOLD)
    mote.parent = root

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=str(OUT), export_format="GLB", use_selection=True)
print(f"EXPORTED: {OUT}")

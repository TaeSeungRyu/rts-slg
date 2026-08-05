# 작은 주민(저폴리) 생성 → GLB 익스포트
# 실행: blender --background --python make_villager.py
#
# 마을 타일 생활감 연출용 초소형 인물: 도포(원뿔 몸) + 머리 + 삿갓.
# 키 ~0.05 (집 벽 높이 0.06의 0.8배). 몸 색은 Godot에서 팔레트로 덧입힌다(body 노드명 기준).
import bpy

OUT = r"D:\dev\window\slg\SanguoSLG.Game\assets\models\villager.glb"

bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, roughness=0.85):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return m


M_ROBE = make_mat("robe", (0.26, 0.32, 0.55))   # 기본 도포색(런타임 교체 대상)
M_SKIN = make_mat("skin", (0.85, 0.64, 0.48))
M_HAT = make_mat("hat", (0.74, 0.60, 0.28))     # 삿갓 짚색

# 도포(몸): 아래가 넓은 원뿔대
bpy.ops.mesh.primitive_cone_add(vertices=7, radius1=0.014, radius2=0.008, depth=0.034,
                                location=(0, 0, 0.017))
body = bpy.context.object
body.name = "body"
body.data.materials.append(M_ROBE)

# 머리
bpy.ops.mesh.primitive_uv_sphere_add(segments=7, ring_count=5, radius=0.0085,
                                     location=(0, 0, 0.040))
head = bpy.context.object
head.name = "head"
head.data.materials.append(M_SKIN)

# 삿갓
bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=0.0145, radius2=0.0015, depth=0.010,
                                location=(0, 0, 0.049))
hat = bpy.context.object
hat.name = "hat"
hat.data.materials.append(M_HAT)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True)
print("EXPORTED:", OUT)

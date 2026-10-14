"""Build the single wild phoenix token for Phase 20 in the current checkout."""
import bpy
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)
ic.MODEL_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "SanguoSLG.Game", "assets", "models"))

flame = ic.make_mat("phoenix_vermilion", (0.82, 0.13, 0.05))
crimson = ic.make_mat("phoenix_crimson", (0.43, 0.035, 0.025))
gold = ic.make_mat("phoenix_gold", (0.98, 0.62, 0.08), roughness=0.55)
ivory = ic.make_mat("phoenix_beak", (0.92, 0.79, 0.45))
eye_mat = ic.make_mat("phoenix_eye", (0.06, 0.025, 0.02))


def oval(name, loc, scale, material):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(material)
    ic.shade_smooth(obj)
    ic.bake_scale(obj)
    return obj


body = oval("body", (0, 0, 0.22), (0.085, 0.13, 0.10), flame)
chest = oval("chest", (0, -0.09, 0.215), (0.060, 0.060, 0.072), gold)
ic.parent_to(chest, body)
neck = oval("neck", (0, -0.08, 0.32), (0.040, 0.049, 0.09), flame)
ic.parent_to(neck, body)
head = oval("head", (0, -0.115, 0.40), (0.056, 0.051, 0.052), flame)
ic.parent_to(head, body)
beak = ic.cone("beak", 0.027, 0.003, 0.065, 0, -0.173, 0.395, ivory,
               verts=6, rot_x=math.radians(90))
ic.parent_to(beak, head)
for sign in (-1, 1):
    eye = oval(f"eye_{sign}", (sign * 0.047, -0.13, 0.411), (0.008, 0.007, 0.008), eye_mat)
    ic.parent_to(eye, head)
    for index in range(3):
        plume = ic.cone(f"crown_{sign}_{index}", 0.011, 0, 0.095 - index * 0.012,
                        sign * (0.014 + index * 0.008), -0.103 + index * 0.012,
                        0.475 + index * 0.004, gold if index == 0 else flame, verts=6,
                        rot_x=math.radians(-18))
        ic.parent_to(plume, head)

# Wings are the infantry arm pivots, so normal movement and attack visibly flap them.
for sign, label in ((-1, "l"), (1, "r")):
    wing = ic.box(f"arm_{label}", 0.035, 0.11, 0.032, sign * 0.105, -0.015, 0.305,
                  crimson, rot_y=sign * math.radians(25))
    ic.parent_to(wing, body)
    for index in range(7):
        reach = 0.125 + index * 0.033
        feather = oval(f"wing_{label}_feather_{index}",
                       (sign * reach, 0.015 + index * 0.012, 0.30 - index * 0.014),
                       (0.070 - index * 0.004, 0.016, 0.018),
                       gold if index % 3 == 0 else flame)
        feather.rotation_euler[1] = sign * math.radians(12)
        ic.parent_to(feather, wing)
    leg = ic.box(f"leg_{label}", 0.017, 0.017, 0.10, sign * 0.04, 0.025, 0.105,
                 ivory, origin_shift=(0, 0, -0.045))
    foot = ic.box(f"foot_{label}", 0.033, 0.047, 0.012, sign * 0.04, -0.006, 0.018, ivory)
    ic.parent_to(foot, leg)
    ic.parent_to(leg, body)

# Long split tail makes the silhouette recognizable even from the campaign camera.
for index in range(7):
    spread = (index - 3) * 0.034
    tail = oval(f"tail_feather_{index}",
                (spread, 0.20 + abs(index - 3) * 0.018, 0.155 - abs(index - 3) * 0.009),
                (0.018, 0.18 + (3 - abs(index - 3)) * 0.025, 0.014),
                gold if index % 2 == 0 else crimson)
    tail.rotation_euler[2] = math.radians((index - 3) * -7)
    ic.parent_to(tail, body)

ic.export("troop-phoenix.glb")

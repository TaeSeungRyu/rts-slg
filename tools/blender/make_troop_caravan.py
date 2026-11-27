# Low-poly merchant caravan. Blender -Y forward, ground Z=0.
# Run beside infantry_common.py using Blender's Python.
import bpy
import math
import os
import sys
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import infantry_common as ic

bpy.ops.wm.read_factory_settings(use_empty=True)
m = ic.Mats()
coat = ic.make_mat('merchant_horse_coat', (.36,.22,.13))
mane = ic.make_mat('merchant_mane', (.12,.09,.07))
wood = ic.make_mat('cart_wood', (.32,.19,.09))
trim = ic.make_mat('cart_trim', (.19,.11,.06))
robe = ic.make_mat('merchant_robe', (.58,.39,.16))
cloth = ic.make_mat('merchant_cloth', (.72,.61,.38))
hat = ic.make_mat('merchant_hat', (.23,.15,.07))
blue = ic.make_mat('trade_fabric', (.24,.36,.39))

# Root at ground center; all child transforms preserve world positions.
body = bpy.data.objects.new('body', None)
bpy.context.collection.objects.link(body)

def box(name, w,d,h,x,y,z,mat,parent=body,**kw):
    obj = ic.box(name,w,d,h,x,y,z,mat,**kw)
    ic.parent_to(obj,parent)
    return obj

def cone(name,r1,r2,h,x,y,z,mat,parent=body,**kw):
    obj=ic.cone(name,r1,r2,h,x,y,z,mat,**kw)
    ic.parent_to(obj,parent)
    return obj

def sphere(name,x,y,z,sx,sy,sz,mat,parent=body):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=10,ring_count=6,radius=1,location=(x,y,z))
    obj=bpy.context.object; obj.name=name; obj.scale=(sx,sy,sz)
    obj.data.materials.append(mat); ic.bake_scale(obj); ic.shade_smooth(obj)
    ic.parent_to(obj,parent)
    return obj

def pivot(name,x,y,z,parent=body):
    obj=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(obj)
    obj.location=(x,y,z); ic.parent_to(obj,parent); return obj

def rod(name,a,b,r,mat,parent=body):
    a,b=Vector(a),Vector(b); delta=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=r,depth=delta.length,location=(a+b)/2)
    obj=bpy.context.object; obj.name=name
    obj.rotation_euler=delta.to_track_quat('Z','Y').to_euler()
    obj.data.materials.append(mat); ic.parent_to(obj,parent); return obj

# Horse: compact draft horse, same proportions as cavalry.
X,Y=-.027,-.105
horse=sphere('horse_body',X,Y,.166,.052,.098,.046,coat)
for tag,dx,dy in [('fl',-.026,-.052),('fr',.026,-.052),('bl',-.026,.052),('br',.026,.052)]:
    x,y=X+dx,Y+dy
    leg=box('leg_'+tag,.019,.021,.151,x,y,.151,coat,horse,origin_shift=(0,0,-.0755))
    box('hoof_'+tag,.024,.026,.016,x,y,.008,mane,leg)
cone('horse_neck',.034,.024,.095,X,Y-.078,.202,coat,horse,rot_x=math.radians(38),smooth=True)
head=box('horse_head',.036,.069,.036,X,Y-.132,.244,coat,horse,rot_x=math.radians(20))
for i,dx in enumerate((-.012,.012)):
    box('ear_'+str(i),.009,.010,.018,X+dx,Y-.108,.270,coat,head)
box('mane',.015,.041,.080,X,Y-.071,.218,mane,horse,rot_x=math.radians(38))
box('tail',.018,.023,.079,X,Y+.097,.152,mane,horse,rot_x=math.radians(-30))
box('harness',.114,.023,.016,X,Y-.034,.205,trim,horse)

# Cart: two independent wheels with X-axis pivots.
CY=.184
cart=pivot('cart',X,CY,.105)
box('cart_floor',.132,.177,.018,X,CY,.105,wood,cart)
for dx in (-.061,.061):
    box('cart_side',.012,.177,.046,X+dx,CY,.132,wood,cart)
    rod('cart_shaft',(X+dx,CY-.078,.105),(X+dx,Y-.035,.173),.006,trim,cart)
for dy in (-.083,.083):
    box('cart_end',.132,.012,.041,X,CY+dy,.131,wood,cart)
rod('axle',(X-.085,CY,.063),(X+.085,CY,.063),.008,trim,cart)
for tag,dx in [('l',-.079),('r',.079)]:
    wx=X+dx; wheel=pivot('wheel_'+tag,wx,CY,.063,cart)
    # Twelve-sided rim and six spokes; rim is a true open ring.
    for i in range(12):
        a=2*math.pi*i/12; b=2*math.pi*(i+1)/12
        rod('rim_'+tag,(wx,CY+.055*math.cos(a),.063+.055*math.sin(a)),(wx,CY+.055*math.cos(b),.063+.055*math.sin(b)),.007,trim,wheel)
    for i in range(6):
        a=2*math.pi*i/6
        rod('spoke_'+tag,(wx,CY,.063),(wx,CY+.052*math.cos(a),.063+.052*math.sin(a)),.004,wood,wheel)
    rod('hub_'+tag,(wx-.012,CY,.063),(wx+.012,CY,.063),.012,trim,wheel)
# Cargo: crate, sacks and rolled fabric.
box('crate',.065,.065,.058,X-.023,CY-.037,.145,wood,cart)
for dx in (-.023,.023):
    box('crate_band',.009,.067,.060,X-.023+dx,CY-.037,.145,trim,cart)
for i,(dx,dy) in enumerate(((.033,-.038),(-.028,.044),(.027,.046))):
    sphere('sack_'+str(i),X+dx,CY+dy,.145,.025,.032,.030,cloth,cart)
    cone('sack_tie_'+str(i),.009,.012,.010,X+dx,CY+dy,.177,trim,cart,verts=6)
rod('fabric_roll',(X-.050,CY+.012,.184),(X+.050,CY+.012,.184),.015,blue,cart)

# Walking merchant alongside horse; height 0.275.
MX,MY=.087,-.055
merchant=pivot('merchant',MX,MY,0)
for tag,dx in [('l',-.017),('r',.017)]:
    leg=box('merchant_leg_'+tag,.023,.025,.100,MX+dx,MY,.110,robe,merchant,origin_shift=(0,0,-.050))
    box('merchant_shoe_'+tag,.025,.040,.018,MX+dx,MY-.008,.009,hat,leg)
torso=cone('merchant_torso',.039,.030,.104,MX,MY,.151,robe,merchant,verts=8)
box('merchant_belt',.069,.049,.011,MX,MY,.148,trim,torso)
box('merchant_head',.040,.038,.040,MX,MY,.222,m.skin,torso)
cone('merchant_hat_brim',.044,.044,.008,MX,MY,.246,hat,torso,verts=10)
cone('merchant_hat_crown',.033,.012,.025,MX,MY,.2625,hat,torso,verts=10)
for tag,dx,pitch in [('l',-.038,-22),('r',.038,8)]:
    arm=box('merchant_arm_'+tag,.024,.026,.072,MX+dx,MY,.190,robe,torso,rot_x=math.radians(pitch),origin_shift=(0,0,-.036))
    # Hand is attached to the sleeve, using the same pitch.
    p=math.radians(pitch)
    box('merchant_hand_'+tag,.020,.021,.020,MX+dx,MY+.074*math.sin(p),.190-.074*math.cos(p),m.skin,arm)
box('merchant_bag',.034,.024,.045,MX+.032,MY+.026,.128,cloth,torso)
# Ground origin and forward direction match cavalry.
# Additional cargo, without weapons.
for i in range(2):
    box('trade_chest_'+str(i), .052,.044,.037,X-.028+i*.056,CY+.060,.185,wood,cart)
# Bake only movement and stationary clips. Root remains stationary for Godot movement.
# Preserve all existing pivots for procedural animation if needed.
scene=bpy.context.scene
scene.render.fps=24
animated=[bpy.data.objects[n] for n in ('leg_fl','leg_fr','leg_bl','leg_br','merchant_leg_l','merchant_leg_r','merchant_arm_l','merchant_arm_r','wheel_l','wheel_r')]
base={o.name:o.rotation_euler.copy() for o in animated}
for clip in ('idle','move'):
    for o in animated:
        if o.animation_data is None: o.animation_data_create()
        for f in range(1,25,3):
            t=2*math.pi*(f-1)/24
            o.rotation_euler=base[o.name].copy()
            if clip=='move':
                if o.name.startswith('wheel_'): o.rotation_euler.x += t
                else:
                    sign=-1 if o.name in ('leg_fr','leg_bl','merchant_leg_r','merchant_arm_l') else 1
                    o.rotation_euler.x += sign*.35*math.sin(t)
            o.keyframe_insert(data_path='rotation_euler',frame=f,group=clip)
        o.rotation_euler=base[o.name].copy()
        if clip=='move' and o.name.startswith('wheel_'): o.rotation_euler.x += 2*math.pi
        o.keyframe_insert(data_path='rotation_euler',frame=25,group=clip)
        action=o.animation_data.action
        action.name=clip+'_'+o.name
        track=o.animation_data.nla_tracks.new(); track.name=clip
        track.strips.new(clip,1,action)
        o.animation_data.action=None
# Export NLA tracks of the same name as one clip per state.
scene.frame_set(1)
out=os.path.join(ic.MODEL_DIR,'troop-caravan.glb')
os.makedirs(ic.MODEL_DIR,exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=out,export_format='GLB',use_selection=True,export_animation_mode='NLA_TRACKS')
print('EXPORTED:',out)

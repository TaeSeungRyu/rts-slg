"""Blender background script: two modular low-poly mounted duelists.
Run: blender -b -t 2 --python make_duel_generals.py -- --out-dir PATH
Requires infantry_common.py beside this script. Front -Y, Z up; GLB +Z front.
"""
import argparse, math, sys
from pathlib import Path
import bpy
from mathutils import Vector
sys.path.insert(0, str(Path(__file__).resolve().parent))
from infantry_common import make_mat


def pivot(name, parent=None, pos=(0,0,0)):
    o=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(o)
    o.parent=parent; o.location=pos; return o


def mesh(name,parent,pos,scale,mat,kind='box',rot=(0,0,0)):
    if kind=='box': bpy.ops.mesh.primitive_cube_add(size=1)
    elif kind=='ball': bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1)
    else: bpy.ops.mesh.primitive_cone_add(vertices=8,radius1=1,radius2=.65,depth=1)
    o=bpy.context.object; o.name=name; o.parent=parent; o.location=pos
    o.scale=scale; o.rotation_euler=rot; o.data.materials.append(mat); return o


def rod(name,parent,a,b,r,mat):
    a,b=Vector(a),Vector(b); d=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=8,radius=r,depth=d.length)
    o=bpy.context.object; o.name=name; o.parent=parent; o.location=(a+b)/2
    o.rotation_euler=d.to_track_quat('Z','Y').to_euler(); o.data.materials.append(mat); return o


def blade(name,parent,points,thickness,mat):
    # Outline in local Y/Z plane, extruded along X.
    n=len(points); v=[(x,y,z) for x in (-thickness/2,thickness/2) for y,z in points]
    f=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    me=bpy.data.meshes.new(name); me.from_pydata(v,[],f); me.update()
    o=bpy.data.objects.new(name,me); bpy.context.collection.objects.link(o); o.parent=parent; me.materials.append(mat); return o


def build(guan):
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for a in list(bpy.data.actions):
        if a.users==0: bpy.data.actions.remove(a)
    m=lambda n,c:make_mat(n,c)
    cloth=m('cloth',(.12,.32,.16) if guan else (.45,.075,.055)); armor=m('armor',(.19,.23,.18) if guan else (.12,.12,.14))
    gold=m('gold',(.65,.42,.13)); skin=m('skin',(.68,.43,.27)); black=m('hair',(.035,.025,.02)); steel=make_mat('steel',(.65,.70,.73),.35,.6)
    horse=m('horse',(.31,.17,.09) if guan else (.43,.16,.09)); hoof=m('hoof',(.09,.065,.045)); wood=m('shaft',(.28,.16,.07))
    root=pivot('DuelRoot'); h=pivot('horse_body',root,(0,0,.175))
    mesh('barrel',h,(0,0,0),(.068,.145,.068),horse,'ball')
    mesh('chest',h,(0,-.09,.008),(.061,.066,.072),horse,'ball'); mesh('rump',h,(0,.09,.008),(.065,.066,.065),horse,'ball')
    neck=pivot('neck',h,(0,-.09,.035)); rod('neck_mesh',neck,(0,0,0),(0,-.058,.085),.045,horse)
    mesh('mane',neck,(0,.003,.049),(.026,.035,.075),black,'ball',(.55,0,0))
    head=pivot('horse_head',neck,(0,-.058,.085)); mesh('head',head,(0,-.025,0),(.035,.065,.038),horse,'ball')
    mesh('muzzle',head,(0,-.067,-.013),(.029,.032,.024),horse,'ball')
    for s in (-1,1):
        mesh('ear',head,(s*.022,0,.038),(.014,.018,.045),horse,'cone',(.15,0,s*.2)); mesh('eye',head,(s*.032,-.027,.012),(.008,.009,.009),black,'ball')
    tail=pivot('tail',h,(0,.13,.018)); rod('tail_mesh',tail,(0,0,0),(0,.065,-.085),.015,black)
    legs=[]
    for tag,x,y in [('fl',-.045,-.085),('fr',.045,-.085),('bl',-.045,.085),('br',.045,.085)]:
        upper=pivot('leg_'+tag,h,(x,y,-.025)); rod('upper',upper,(0,0,0),(0,0,-.072),.021,horse)
        lower=pivot('knee_'+tag,upper,(0,0,-.072)); rod('lower',lower,(0,0,0),(0,0,-.064),.013,horse)
        mesh('hoof',lower,(0,-.006,-.069),(.032,.041,.018),hoof); legs.append((upper,lower))
    mesh('saddlecloth',h,(0,.015,.054),(.14,.14,.025),cloth); mesh('saddle',h,(0,.015,.075),(.105,.085,.025),wood)
    rider=pivot('rider',h,(0,.015,.086)); torso=pivot('torso',rider,(0,0,.015))
    mesh('coat',torso,(0,0,.042),(.094,.065,.10),cloth,'cone'); mesh('breastplate',torso,(0,-.027,.053),(.092,.024,.073),armor)
    for z in (.027,.048,.069): mesh('gold_trim',torso,(0,-.041,z),(.085,.008,.005),gold)
    mesh('cape',torso,(0,.045,.014),(.10,.023,.13),cloth,rot=(-.25,0,0))
    mesh('face',torso,(0,0,.117),(.052,.049,.058),skin,'ball'); mesh('helmet',torso,(0,.002,.148),(.037,.034,.035),armor,'ball')
    mesh('helmet_band',torso,(0,-.025,.141),(.067,.012,.009),gold)
    if guan:
        mesh('beard',torso,(0,-.029,.083),(.025,.017,.075),black,'cone',(math.pi,0,0)); mesh('headcloth',torso,(0,0,.158),(.034,.033,.022),cloth,'ball')
    else:
        for s in (-1,1):
            rod('plume',torso,(s*.014,0,.16),(s*.043,.025,.22),.007,cloth); rod('plume_tip',torso,(s*.043,.025,.22),(s*.065,.065,.23),.005,cloth)
    for s in (-1,1):
        rod('thigh',rider,(s*.035,0,0),(s*.076,-.024,-.045),.022,armor); rod('boot',rider,(s*.076,-.024,-.045),(s*.077,.008,-.10),.016,armor)
    arms=[]
    for s in (-1,1):
        a=pivot('arm_l' if s<0 else 'arm_r',torso,(s*.055,0,.073)); mesh('pauldron',a,(0,0,0),(.037,.041,.035),gold,'ball')
        rod('upper_arm',a,(0,0,0),(0,0,-.045),.018,armor); e=pivot('elbow_l' if s<0 else 'elbow_r',a,(0,0,-.045)); rod('forearm',e,(0,0,0),(0,-.042,0),.014,cloth); mesh('hand',e,(0,-.047,0),(.025,.025,.025),skin,'ball'); arms.append((a,e))
    weapon=pivot('weapon',arms[1][1],(0,-.047,0)); rod('shaft',weapon,(0,0,-.12),(0,0,.17),.008,wood)
    if guan: blade('guandao',weapon,[(0,.15),(-.035,.17),(-.063,.23),(-.035,.28),(.008,.24),(.012,.17)],.009,steel)
    else:
        blade('spear_tip',weapon,[(0,.16),(-.018,.20),(0,.29),(.018,.20)],.009,steel)
        blade('crescent',weapon,[(0,.16),(-.055,.15),(-.072,.20),(-.047,.26),(-.026,.22),(-.038,.19),(0,.19)],.009,steel)
    # Local rigid-part animation; all clips use same node names and durations.
    animated=[h,neck,head,tail,rider,torso,weapon]+[o for pair in legs+arms for o in pair]
    base={o.name:o.location.copy() for o in animated}
    fps=30; bpy.context.scene.render.fps=fps
    for clip,end in [('idle',60),('run',24),('attack',30),('block',30),('hit',24),('fall',36)]:
        for o in animated: o.animation_data_create(); o.animation_data.action=None
        for frame in range(0,end+1,3):
            t=frame/end; wave=math.sin(t*math.tau); running=clip in ('run','attack','block','hit'); strike=math.sin(math.pi*t)**2
            for o in animated: o.location=base[o.name]; o.rotation_euler=(0,0,0)
            h.location.z+=.009*math.sin(t*math.tau*2) if running else .002*wave
            neck.rotation_euler.x=.10*wave; head.rotation_euler.x=-.06*wave; tail.rotation_euler.x=.16*wave
            for i,(u,k) in enumerate(legs):
                p=t*math.tau+(0 if i in (0,3) else math.pi)
                u.rotation_euler.x=.52*math.sin(p) if running else 0
                k.rotation_euler.x=.65*max(0,math.cos(p)) if running else 0
            torso.rotation_euler.x=.09*wave if running else .025*wave
            arms[0][0].rotation_euler.x=-.35; arms[1][0].rotation_euler.x=-.45
            weapon.rotation_euler.x=.45
            if clip=='attack':
                torso.rotation_euler.z=-.65*math.sin(t*math.tau); arms[1][0].rotation_euler.x=-.45-1.25*strike; arms[1][0].rotation_euler.z=-1.2*math.sin(t*math.tau); weapon.rotation_euler.x=.45+1.2*strike
            elif clip=='block': arms[1][0].rotation_euler.x=-.45-.9*strike; weapon.rotation_euler.y=1.25*strike
            elif clip=='hit': torso.rotation_euler.x+=.6*strike; torso.rotation_euler.y=.22*strike
            elif clip=='fall': rider.location.x+=.17*t; rider.location.z-=.18*t*t; rider.rotation_euler.y=1.45*t
            for o in animated:
                o.keyframe_insert('location',frame=frame); o.keyframe_insert('rotation_euler',frame=frame)
        for o in animated:
            ad=o.animation_data; action=ad.action; action.name=f'{clip}_{o.name}'; ad.action=None
            track=ad.nla_tracks.new(); track.name=clip; strip=track.strips.new(clip,0,action); strip.name=clip
    bpy.context.scene.frame_set(0)
    return root


def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--out-dir',required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    out=Path(args.out_dir).resolve(); out.mkdir(parents=True,exist_ok=True)
    for guan,name in [(True,'duel-guanyu'),(False,'duel-lubu')]:
        build(guan); bpy.ops.object.select_all(action='SELECT')
        bpy.ops.export_scene.gltf(filepath=str(out/(name+'.glb')),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_yup=True)
        print('EXPORTED:',out/(name+'.glb'))

if __name__=='__main__': main()

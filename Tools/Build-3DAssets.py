"""Run with Blender --background --factory-startup --python Tools/Build-3DAssets.py.

Original, editable low-poly meshes, rigid-weight armature rigs and animation takes.
Export staging is outside Assets so Unity never imports a half-written FBX.
"""
import bpy
import math
import random
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / '.utmp' / 'Blender3DOutput'
SOURCE = ROOT / 'ArtSource' / 'Blender'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
MATS = {}
PARTS = []

PALETTE = {
    'Fur': (.94,.94,.89,1), 'Ear': (.92,.81,.58,1), 'Pink': (.88,.48,.49,1),
    'Mint': (.29,.62,.49,1), 'Cream': (.91,.86,.68,1), 'Brown': (.27,.14,.075,1),
    'Dark': (.035,.055,.045,1), 'Eye': (.25,.58,.14,1), 'Gold': (.92,.61,.16,1),
    'Goblin': (.36,.47,.22,1), 'Orc': (.42,.49,.25,1), 'Leather': (.32,.23,.14,1),
    'Steel': (.62,.70,.73,1), 'Slime': (.24,.75,.56,1), 'Stone': (.65,.66,.58,1),
    'Roof': (.22,.46,.37,1), 'Apricot': (.94,.57,.29,1), 'Grass': (.41,.58,.29,1),
    'Leaf': (.25,.43,.24,1), 'LeafLight': (.49,.63,.29,1), 'Water': (.18,.51,.57,1),
    'Portal': (.27,.91,.71,1), 'Path': (.79,.73,.56,1),
}

def reset():
    global MATS, PARTS
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for action in list(bpy.data.actions): bpy.data.actions.remove(action)
    MATS = {}; PARTS = []
    bpy.context.scene.render.fps = 30
    for name,color in PALETTE.items():
        mat=bpy.data.materials.new(name); mat.diffuse_color=color; mat.use_nodes=True
        bsdf=mat.node_tree.nodes.get('Principled BSDF')
        bsdf.inputs['Base Color'].default_value=color
        bsdf.inputs['Roughness'].default_value=.72
        if name=='Portal':
            bsdf.inputs['Emission Color'].default_value=color; bsdf.inputs['Emission Strength'].default_value=.7
        MATS[name]=mat

def finish(obj, name, material, bone=None, smooth=False):
    obj.name=name; obj.data.materials.append(MATS[material])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if smooth:
        for face in obj.data.polygons: face.use_smooth=True
    if bone:
        group=obj.vertex_groups.new(name=bone); group.add(list(range(len(obj.data.vertices))),1,'REPLACE')
    PARTS.append(obj)
    return obj

def ball(name,pos,scale,mat,bone=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=10,location=pos)
    obj=bpy.context.object; obj.scale=scale
    return finish(obj,name,mat,bone,True)

def box(name,pos,scale,mat,bone=None,bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos)
    obj=bpy.context.object; obj.scale=scale
    finish(obj,name,mat,bone)
    if bevel:
        mod=obj.modifiers.new('Soft edges','BEVEL'); mod.width=bevel; mod.segments=2
        bpy.context.view_layer.objects.active=obj; bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj

def rod(name,a,b,radius,mat,bone=None,vertices=10):
    a,b=Vector(a),Vector(b); midpoint=(a+b)/2
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=(b-a).length,location=midpoint)
    obj=bpy.context.object; obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    return finish(obj,name,mat,bone)

def ear(name,x,z,mat,bone='Head',width=.19,height=.29):
    verts=[(x-width/2,-.02,z),(x+width/2,-.02,z),(x,-.01,z+height),
           (x-width/2,.12,z),(x+width/2,.12,z),(x,.06,z+height)]
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],[(0,1,2),(5,4,3),(0,3,4,1),(1,4,5,2),(2,5,3,0)])
    for vertex in mesh.vertices: vertex.co -= Vector((x,0,z))
    obj=bpy.data.objects.new(name,mesh); obj.location=(x,0,z); bpy.context.collection.objects.link(obj)
    return finish(obj,name,mat,bone)

def face(head_z,skin,cat=False):
    # Faces look along Blender -Y; the FBX axis conversion makes this Unity +Z.
    for x in (-.125,.125):
        ball('Eye white',(x,-.255,head_z+.04),(.091,.045,.105),'Fur','Head')
        ball('Iris',(x,-.292,head_z+.045),(.046,.018,.064),'Eye' if cat else 'Gold','Head')
        ball('Pupil',(x,-.307,head_z+.045),(.021,.008,.047),'Dark','Head')
        ball('Eye glint',(x-.012,-.315,head_z+.068),(.012,.006,.014),'Fur','Head')
    if cat:
        for x in (-.055,.055): ball('Muzzle',(x,-.265,head_z-.095),(.075,.06,.047),'Fur','Head')
        ball('Nose',(0,-.327,head_z-.065),(.039,.024,.027),'Pink','Head')
        rod('Mouth',(0,-.322,head_z-.09),(0,-.319,head_z-.12),.009,'Dark','Head',6)
        for sign in (-1,1):
            for dz in (-.03,.025):
                rod('Whisker',(sign*.10,-.28,head_z-.095),(sign*.26,-.27,head_z-.095+dz),.0045,'Cream','Head',5)
    else:
        ball('Nose',(0,-.29,head_z-.035),(.067,.053,.055),skin,'Head')
        box('Mouth',(0,-.257,head_z-.14),(.18,.035,.055),'Dark','Head',.01)
        for x in (-.067,.067):
            rod('Tusk',(x,-.28,head_z-.16),(x,-.295,head_z-.08),.024,'Cream','Head',6)

def build_character(kind):
    reset(); cat=kind=='CatJanitor'; slime=kind=='Slime'; orc=kind=='Orc'
    size=1.14 if orc else .9 if kind=='Goblin' else 1
    bones={'Root':((0,0,0),(0,0,.25),None)}
    if not slime:
        bones.update({'Spine':((0,0,.55),(0,0,.98),'Root'), 'Head':((0,0,1.01),(0,0,1.36),'Spine'),
            'Arm_L':((-.29,0,.93),(-.29,0,.49),'Spine'), 'Arm_R':((.29,0,.93),(.29,0,.49),'Spine'),
            'Leg_L':((-.135,0,.5),(-.135,0,.14),'Root'), 'Leg_R':((.135,0,.5),(.135,0,.14),'Root'),
            'Tail':((0,.15,.65),(0,.4,.75),'Root')})
    if slime:
        ball('Jelly',(0,0,.31),(.45,.39,.34),'Slime','Root')
        for x in (-.13,.13):
            ball('Slime eye',(x,-.348,.36),(.044,.025,.065),'Dark','Root')
            ball('Slime highlight',(x-.012,-.371,.385),(.012,.007,.017),'Fur','Root')
        ball('Jelly shine',(-.17,-.18,.54),(.095,.04,.035),'Fur','Root')
    else:
        skin='Fur' if cat else 'Orc' if orc else 'Goblin'
        torso='Mint' if cat else 'Leather'
        ball('Torso',(0,0,.74),(.25 if not orc else .31,.17,.30),torso,'Spine')
        if cat:
            ball('Shirt',(0,0,.93),(.245,.165,.15),'Cream','Spine')
            box('Bib',(0,-.164,.84),(.25,.036,.26),'Mint','Spine',.02)
            for x in (-.115,.115):
                box('Strap',(x,-.17,.975),(.045,.035,.2),'Mint','Spine',.008)
                ball('Brass button',(x,-.197,.92),(.023,.012,.023),'Gold','Spine')
            box('Pocket',(0,-.19,.76),(.12,.02,.07),'Roof','Spine',.008)
            box('Tool pouch',(.27,0,.65),(.13,.17,.18),'Cream','Spine',.02)
            rod('Belt brush',(.29,.02,.71),(.29,.02,.9),.019,'Brown','Spine')
        else:
            box('Belt',(0,-.02,.59),(.51,.35,.075),'Brown','Spine',.015)
            box('Buckle',(0,-.208,.59),(.10,.025,.09),'Gold','Spine',.008)
        ball('Head',(0,-.01,1.19),(.305,.255,.265),skin,'Head')
        face(1.19,skin,cat)
        if cat:
            for x in (-.21,.21):
                ear('Cream ear',x,1.37,'Ear')
                ear('Pink inner ear',x,1.40,'Pink',width=.10,height=.16)
            for i in range(5):
                t=i/4
                ball('Fluffy tail',(0,.24+t*.44,.67+.28*math.sin(t*math.pi/2)),(.105-t*.008,.12,.12),'Fur','Tail')
        else:
            for sign in (-1,1):
                obj=ear('Pointed ear',sign*.29,1.17,skin,width=.28,height=.24)
                obj.rotation_euler.y=sign*.85
            for i in range(5):
                ball('Hair',(0,.005+i*.025,1.43-i*.006),(.20-i*.017,.08,.07),'Dark','Head')
        for side,x in [('L',-.29),('R',.29)]:
            bone='Arm_'+side
            ball('Sleeve',(x,0,.87),(.12,.13,.14),'Cream' if cat else skin,bone)
            ball('Forearm',(x,0,.66),(.075 if cat else .10,.08,.17),skin,bone)
            ball('Paw',(x,-.015,.5),(.082,.082,.085),skin,bone)
            bx=-.135 if side=='L' else .135
            ball('Trouser',(bx,0,.33),(.12,.13,.23),'Mint' if cat else 'Brown','Leg_'+side)
            box('Boot',(bx,-.065,.115),(.23,.34,.19),'Brown','Leg_'+side,.045)
        if not cat:
            rod('Weapon handle',(.29,-.015,.56),(.29,-.015,.34),.035,'Brown','Arm_R')
            if orc:
                rod('Club',(.29,-.015,.35),(.29,-.015,.05),.10,'Brown','Arm_R',8)
                for zz in (.13,.24): ball('Club stud',(.29,-.11,zz),(.028,.025,.028),'Steel','Arm_R')
            else:
                box('Dagger guard',(.29,-.015,.36),(.18,.04,.035),'Gold','Arm_R')
                box('Dagger blade',(.29,-.015,.20),(.07,.025,.31),'Steel','Arm_R',.018)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in PARTS: obj.select_set(True)
    bpy.context.view_layer.objects.active=PARTS[0]; bpy.ops.object.join()
    mesh=bpy.context.object; mesh.name=kind+'Mesh'
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    arm=bpy.data.armatures.new(kind+'Skeleton'); rig=bpy.data.objects.new(kind+'Rig',arm)
    bpy.context.collection.objects.link(rig); bpy.context.view_layer.objects.active=rig
    bpy.ops.object.mode_set(mode='EDIT')
    for name,(head,tail,parent) in bones.items():
        bone=arm.edit_bones.new(name); bone.head=head; bone.tail=tail
        if parent: bone.parent=arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    mesh.parent=rig; mod=mesh.modifiers.new('Skeleton','ARMATURE'); mod.object=rig
    markers=[]
    for name,position in [('FaceOrigin',(0,0,0)),('FaceForward',(0,-1,0))]:
        marker=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(marker)
        marker.parent=rig;marker.location=position;markers.append(marker)
    if size!=1: rig.scale=(size,size,size)
    rig.animation_data_create()
    for action_name,length in [('Idle',40),('Walk',24),('Dash',16),('Attack',20),('Loot',24),('Defeated',12)]:
        action=bpy.data.actions.new(kind+'_'+action_name); action.use_fake_user=True
        rig.animation_data.action=action
        for frame in range(1,length+1):
            t=(frame-1)/(length-1); wave=math.sin(t*math.tau)
            for pb in rig.pose.bones:
                pb.rotation_mode='XYZ'; pb.rotation_euler=(0,0,0); pb.location=(0,0,0); pb.scale=(1,1,1)
            root=rig.pose.bones['Root']
            if slime:
                if action_name in ('Walk','Dash'):
                    root.location.y=abs(wave)*.13; root.scale=(1+.08*wave,1-.08*wave,1+.06*wave)
                elif action_name=='Attack':
                    root.location.y=math.sin(t*math.pi)*.19; root.scale=(1+.2*wave,1-.15*wave,1+.2*wave)
                elif action_name=='Defeated': root.scale=(1.3, .2, 1.3)
                else: root.scale=(1+wave*.02,1-wave*.02,1+wave*.02)
            elif action_name=='Walk':
                root.location.y=abs(wave)*.035
                for side,sign in [('L',1),('R',-1)]:
                    rig.pose.bones['Leg_'+side].rotation_euler.x=wave*.62*sign
                    rig.pose.bones['Arm_'+side].rotation_euler.x=-wave*.5*sign
                rig.pose.bones['Tail'].rotation_euler.z=wave*.18
            elif action_name=='Dash':
                lean=math.sin(t*math.pi)
                rig.pose.bones['Spine'].rotation_euler.x=.65*lean
                rig.pose.bones['Head'].rotation_euler.x=-.30*lean
                rig.pose.bones['Leg_L'].rotation_euler.x=.8*lean
                rig.pose.bones['Leg_R'].rotation_euler.x=-.7*lean
                rig.pose.bones['Arm_L'].rotation_euler.x=.65*lean
                rig.pose.bones['Arm_R'].rotation_euler.x=.65*lean
                root.location.y=.06*lean
            elif action_name in ('Attack','Loot'):
                swing=math.sin(t*math.pi)
                strike = 2.1 * (t/.25) if t < .25 else 2.1 - (t-.25)/.25*3.5 if t < .5 else -1.4*(1-(t-.5)/.5)
                rig.pose.bones['Arm_R'].rotation_euler.x=strike if action_name=='Attack' else -.8-abs(wave)*.5
                rig.pose.bones['Spine'].rotation_euler.x=.18*swing
                rig.pose.bones['Head'].rotation_euler.x=-.10*swing
            elif action_name=='Defeated':
                root.rotation_euler.x=math.pi/2*t; root.location.y=.28*t
                rig.pose.bones['Arm_L'].rotation_euler.z=.3*t
                rig.pose.bones['Arm_R'].rotation_euler.z=-.3*t
            else:
                rig.pose.bones['Spine'].scale=(1+wave*.012,1+wave*.02,1+wave*.012)
                rig.pose.bones['Tail'].rotation_euler.z=wave*.13
            for pb in rig.pose.bones:
                pb.keyframe_insert('rotation_euler',frame=frame)
                pb.keyframe_insert('location',frame=frame)
                pb.keyframe_insert('scale',frame=frame)
    rig.animation_data.action=bpy.data.actions.get(kind+'_Idle'); bpy.context.scene.frame_set(1)
    bpy.context.scene.frame_end=40
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(kind+'.blend')))
    bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); mesh.select_set(True)
    for marker in markers:marker.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/(kind+'.fbx')),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},
        add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,bake_anim_simplify_factor=0,apply_scale_options='FBX_SCALE_UNITS')
    print('MODEL_EXPORTED',kind,len(mesh.data.vertices),len(mesh.data.polygons))

def roof(x,y,z,w,d,mat):
    verts=[(x-w/2,y-d/2,z),(x+w/2,y-d/2,z),(x,y-d/2,z+.75),
           (x-w/2,y+d/2,z),(x+w/2,y+d/2,z),(x,y+d/2,z+.75)]
    faces=[(0,2,1),(3,4,5),(0,3,5,2),(2,5,4,1),(1,4,3,0)]
    mesh=bpy.data.meshes.new('Gable');mesh.from_pydata(verts,[],[tuple(reversed(face)) for face in faces])
    obj=bpy.data.objects.new('Gable roof',mesh);bpy.context.collection.objects.link(obj);finish(obj,'Gable roof',mat)

def house(x,y,w,d,warehouse=False):
    box('Ivory walls',(x,y,1.1),(w,d,2.2),'Cream',bevel=.08)
    roof(x,y,2.2,w+.32,d+.34,'Roof')
    box('Door',(x,y-d/2-.025,.68),(1.0 if warehouse else .7,.08,1.36),'Brown',bevel=.07)
    for sx in (-1,1):
        box('Timber post',(x+sx*(w/2-.10),y-d/2-.06,1.1),(.15,.15,2.25),'Brown',bevel=.02)
        box('Window frame',(x+sx*w*.31,y-d/2-.06,1.35),(.48,.1,.65),'Roof',bevel=.06)
        box('Warm window',(x+sx*w*.31,y-d/2-.12,1.35),(.35,.04,.51),'Gold',bevel=.04)
    box('Front step',(x,y-d/2-.26,.06),(1.3,.55,.12),'Stone',bevel=.03)
    box('Chimney',(x-.7,y+.22,2.85),(.35,.4,1.0),'Stone',bevel=.04)
    if warehouse:
        for sx in (-1,1):
            box('Crate',(x+sx*1.7,y-.5,.3),(.55,.55,.6),'Leather',bevel=.025)
            for dz in (-.16,.16): box('Crate band',(x+sx*1.7,y-.79,.3+dz),(.57,.035,.05),'Brown')
    else:
        rod('Mop handle',(x-.85,y-d/2-.28,.15),(x-.85,y-d/2-.12,1.38),.025,'Brown')
        box('Mop head',(x-.85,y-d/2-.27,.15),(.4,.16,.09),'Fur',bevel=.03)
        rod('Bucket',(x+.6,y-d/2-.30,.03),(x+.6,y-d/2-.30,.36),.15,'Mint',vertices=12)

def market(x,y):
    for xx in (-1.15,1.15):
        for yy in (-.6,.6): box('Stall post',(x+xx,y+yy,.9),(.11,.11,1.8),'Brown')
    box('Counter',(x,y-.2,.48),(2.45,1.35,.8),'Leather',bevel=.04)
    for i in range(8):
        obj=box('Canvas stripe',(x-1.23+i*.35,y-.12,1.90),(.35,1.8,.085),'Apricot' if i%2==0 else 'Cream')
        obj.rotation_euler.x=.12
    for i in range(7):
        xx=x-.9+i*.30
        rod('Supply bottle',(xx,y-.4,.9),(xx,y-.4,1.18),.08,'Mint' if i%2 else 'Slime')
        rod('Bottle stopper',(xx,y-.4,1.18),(xx,y-.4,1.24),.045,'Brown')
    rod('Sign post',(x+1.45,y,1.1),(x+1.45,y,2.0),.045,'Brown')
    ball('Trade coin',(x+1.45,y-.05,1.75),(.26,.06,.26),'Gold')

def portal(x=0,y=0):
    for sx in (-1,1):
        for h in range(5): box('Portal pier',(x+sx*.85,y,.2+h*.34),(.38,.5,.34),'Stone',bevel=.025)
    for i in range(11):
        angle=i/10*math.pi
        obj=box('Arch stone',(x+math.cos(angle)*.85,y,1.5+math.sin(angle)*.85),(.36,.5,.34),'Stone',bevel=.025)
        obj.rotation_euler.y=angle-math.pi/2
    bpy.ops.mesh.primitive_circle_add(vertices=32,radius=.68,fill_type='NGON',location=(x,y-.01,1.02),rotation=(math.pi/2,0,0))
    obj=bpy.context.object;obj.scale=(1,1.43,1);finish(obj,'Mint portal','Portal')
    for sx in (-1,1): ball('Gate crystal',(x+sx*.86,y-.26,.4),(.10,.09,.16),'Portal')

def static_export(name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in PARTS: obj.select_set(True)
    bpy.context.view_layer.objects.active=PARTS[0];bpy.ops.object.join()
    obj=bpy.context.object;obj.name=name;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')))
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',bake_anim=False,apply_scale_options='FBX_SCALE_UNITS')

for species in ('CatJanitor','Goblin','Slime','Orc'): build_character(species)
reset();portal();static_export('Portal')
reset();random.seed(20261016)
box('Island',(0,0,-.25),(23,17,.5),'Grass',bevel=.5)
box('Water',(0,0,-.62),(45,38,.2),'Water')
box('Central path',(0,0,.025),(15,1.8,.05),'Path',bevel=.2)
box('Cross path',(-.2,0,.03),(2.1,11,.06),'Path',bevel=.2)
house(-4.3,1.5,2.8,2.15);market(.2,2.3);house(-.4,-2.6,2.6,1.75,True);portal(4.8,0)
for i in range(26):
    x=random.uniform(-10.4,10.4); y=random.choice([-1,1])*random.uniform(5.2,7.4)
    rod('Tree trunk',(x,y,0),(x,y,1.1),.12,'Brown')
    for j in range(3):
        ball('Tree canopy',(x+(j-1)*.25,y,1.3+j*.30),(.7-j*.09,.60-j*.04,.62),'Leaf' if i%2 else 'LeafLight')
for i in range(32):
    x=random.uniform(-10.5,10.5);y=random.uniform(-7,7)
    if abs(y)<4.2 and abs(x)<7:continue
    ball('Boundary rock',(x,y,.12),(.25,.23,.23),'Stone')
static_export('Town')
print('ART_BUILD_COMPLETE',OUT)

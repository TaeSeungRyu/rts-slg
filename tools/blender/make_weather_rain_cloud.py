import bpy
import os


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "SanguoSLG.Game", "assets", "models", "weather-rain-cloud.glb")

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

material = bpy.data.materials.new("WeatherRainCloudGray")
material.diffuse_color = (0.64, 0.71, 0.77, 1.0)
material.use_nodes = True
material.use_backface_culling = True
shader = material.node_tree.nodes.get("Principled BSDF")
shader.inputs["Base Color"].default_value = (0.64, 0.71, 0.77, 1.0)
shader.inputs["Roughness"].default_value = 1.0

body = bpy.data.metaballs.new("RainCloudVolume")
body.resolution = 0.035
body.render_resolution = 0.035
cloud = bpy.data.objects.new("RainCloud", body)
bpy.context.collection.objects.link(cloud)

lobes = [
    (-0.35, 0.00, -0.04, 0.28, 0.17, 0.16),
    (-0.17, 0.03, 0.04, 0.30, 0.19, 0.22),
    (0.10, -0.05, -0.02, 0.36, 0.20, 0.20),
    (0.35, 0.00, -0.06, 0.27, 0.15, 0.16),
    (-0.04, 0.09, 0.15, 0.21, 0.16, 0.23),
    (0.25, 0.08, 0.13, 0.19, 0.14, 0.19),
    (-0.26, -0.12, -0.08, 0.22, 0.14, 0.12),
]
for x, y, z, rx, ry, rz in lobes:
    part = body.elements.new()
    part.type = "ELLIPSOID"
    part.co = (x, y, z)
    part.radius = 1.0
    part.size_x = rx
    part.size_y = ry
    part.size_z = rz

bpy.context.view_layer.objects.active = cloud
cloud.select_set(True)
bpy.ops.object.convert(target="MESH")
cloud = bpy.context.object
cloud.name = "WeatherRainCloudMesh"
cloud.data.materials.append(material)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_animations=False)
print(f"exported {OUT}")

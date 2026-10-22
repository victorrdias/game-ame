"""Vila Nova player. Same matte cottage blocks as the houses.

Blender Z-up, feet on the ground, face toward +Y. export_yup maps that to Godot +Z.
"""

from pathlib import Path

import bpy
import math
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
MODEL_DIR = ROOT / "Assets" / "Models"
PREVIEW_DIR = ROOT / "Assets" / "ArtPreviews"
GLB_PATH = MODEL_DIR / "vn_player.glb"
BLEND_PATH = MODEL_DIR / "vn_player.blend"


def rgba(hex_color):
    h = hex_color.lstrip("#")
    rgb = tuple(int(h[i : i + 2], 16) / 255.0 for i in (0, 2, 4))

    def linear(channel):
        if channel <= 0.04045:
            return channel / 12.92
        return ((channel + 0.055) / 1.055) ** 2.4

    return tuple(linear(c) for c in rgb) + (1.0,)


def make_material(name, hex_color):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = next(node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = rgba(hex_color)
    bsdf.inputs["Roughness"].default_value = 0.92
    bsdf.inputs["Metallic"].default_value = 0.0
    spec = bsdf.inputs.get("Specular IOR Level") or bsdf.inputs.get("Specular")
    if spec is not None:
        spec.default_value = 0.08
    return mat


def link(obj, coll):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    coll.objects.link(obj)


def add_box(verts, faces, mats, center, size, material_index, yaw=0.0):
    cx, cy, cz = center
    sx, sy, sz = size
    cos_y, sin_y = math.cos(yaw), math.sin(yaw)
    start = len(verts)
    for dx, dy, dz in (
        (-1, -1, -1),
        (1, -1, -1),
        (1, 1, -1),
        (-1, 1, -1),
        (-1, -1, 1),
        (1, -1, 1),
        (1, 1, 1),
        (-1, 1, 1),
    ):
        lx = dx * sx * 0.5
        ly = dy * sy * 0.5
        verts.append((cx + lx * cos_y - ly * sin_y, cy + lx * sin_y + ly * cos_y, cz + dz * sz * 0.5))
    for face in (
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (1, 5, 6, 2),
        (2, 6, 7, 3),
        (3, 7, 4, 0),
    ):
        faces.append(tuple(start + corner for corner in face))
        mats.append(material_index)


def make_part(coll, materials, name, pivot, boxes):
    """Mesh origin sits on the joint so Godot can swing the limb."""
    index = {mat.name: i for i, mat in enumerate(materials)}
    verts, faces, mats = [], [], []
    px, py, pz = pivot
    for center, size, material_name in boxes:
        cx, cy, cz = center
        add_box(verts, faces, mats, (cx - px, cy - py, cz - pz), size, index[material_name])
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    for material in materials:
        mesh.materials.append(material)
    for polygon, material_index in zip(mesh.polygons, mats):
        polygon.material_index = material_index
        polygon.use_smooth = False
    obj = bpy.data.objects.new(name, mesh)
    obj.location = Vector(pivot)
    link(obj, coll)
    return obj


def build(coll, materials, root):
    parts = {
        "Body": ((0.0, 0.0, 0.0), (
            ((0.0, 0.0, 0.94), (0.38, 0.22, 0.5), "tunic"),
            ((0.0, 0.09, 0.96), (0.3, 0.08, 0.34), "vest"),
            ((0.0, 0.1, 1.08), (0.16, 0.06, 0.08), "trim"),
            ((0.0, 0.0, 0.7), (0.4, 0.24, 0.07), "belt"),
            ((0.0, 0.125, 0.7), (0.06, 0.03, 0.06), "buckle"),
            ((0.0, 0.0, 1.22), (0.1, 0.1, 0.08), "skin"),
            ((0.0, 0.03, 1.4), (0.26, 0.24, 0.26), "skin"),
            ((0.0, -0.02, 1.52), (0.32, 0.3, 0.16), "hair"),
            ((0.0, -0.1, 1.38), (0.32, 0.14, 0.3), "hair"),
            ((0.0, 0.12, 1.54), (0.28, 0.1, 0.08), "hair"),
            ((-0.12, -0.02, 1.34), (0.08, 0.16, 0.22), "hair"),
            ((0.12, -0.02, 1.34), (0.08, 0.16, 0.22), "hair"),
            ((-0.07, 0.15, 1.42), (0.055, 0.02, 0.055), "eye"),
            ((0.07, 0.15, 1.42), (0.055, 0.02, 0.055), "eye"),
            ((-0.07, 0.15, 1.4), (0.045, 0.015, 0.02), "eye_light"),
            ((0.07, 0.15, 1.4), (0.045, 0.015, 0.02), "eye_light"),
            ((0.0, 0.15, 1.35), (0.07, 0.02, 0.025), "mouth"),
            ((-0.09, 0.14, 1.36), (0.04, 0.015, 0.03), "blush"),
            ((0.09, 0.14, 1.36), (0.04, 0.015, 0.03), "blush"),
            ((0.24, -0.02, 0.84), (0.09, 0.1, 0.16), "satchel"),
            ((0.24, 0.02, 0.92), (0.1, 0.08, 0.05), "vest"),
            ((0.2, 0.0, 1.02), (0.04, 0.04, 0.26), "satchel"),
        )),
        "LegL": ((-0.13, 0.0, 0.67), (
            ((-0.13, 0.03, 0.08), (0.12, 0.18, 0.16), "boot"),
            ((-0.13, 0.1, 0.045), (0.11, 0.07, 0.07), "boot"),
            ((-0.13, 0.0, 0.44), (0.11, 0.14, 0.46), "pants"),
        )),
        "LegR": ((0.13, 0.0, 0.67), (
            ((0.13, 0.03, 0.08), (0.12, 0.18, 0.16), "boot"),
            ((0.13, 0.1, 0.045), (0.11, 0.07, 0.07), "boot"),
            ((0.13, 0.0, 0.44), (0.11, 0.14, 0.46), "pants"),
        )),
        "ArmL": ((-0.25, 0.0, 1.17), (
            ((-0.25, 0.0, 1.05), (0.12, 0.13, 0.24), "tunic"),
            ((-0.27, 0.04, 0.84), (0.1, 0.11, 0.22), "tunic"),
            ((-0.27, 0.07, 0.7), (0.08, 0.08, 0.08), "skin"),
        )),
        "ArmR": ((0.25, 0.0, 1.17), (
            ((0.25, 0.0, 1.05), (0.12, 0.13, 0.24), "tunic"),
            ((0.27, 0.04, 0.84), (0.1, 0.11, 0.22), "tunic"),
            ((0.27, 0.07, 0.7), (0.08, 0.08, 0.08), "skin"),
        )),
    }
    nodes = {}
    for name, (pivot, boxes) in parts.items():
        node = make_part(coll, materials, name, pivot, boxes)
        node.parent = root
        nodes[name] = node
    return nodes


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def render(root):
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1100
    scene.render.image_settings.file_format = "PNG"
    for candidate in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "CYCLES"):
        try:
            scene.render.engine = candidate
            break
        except Exception:
            continue
    bpy.ops.object.light_add(type="SUN", location=(4, -3, 6))
    sun = bpy.context.active_object
    sun.data.energy = 3.2
    sun.data.color = (1.0, 0.94, 0.84)
    look_at(sun, (0, 0, 0.8))
    loc = (1.7, 1.7, 1.55)
    bpy.ops.object.camera_add(location=loc)
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.15
    look_at(cam, (0.0, 0.0, 0.78))
    scene.camera = cam
    scene.render.filepath = str(PREVIEW_DIR / "vn_player_iso.png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath)


def export_glb(root):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    kwargs = {
        "filepath": str(GLB_PATH),
        "export_format": "GLB",
        "use_selection": True,
        "export_apply": True,
        "export_materials": "EXPORT",
    }
    try:
        props = set(bpy.ops.export_scene.gltf.get_rna_type().properties.keys())
    except Exception:
        props = set()
    if "export_yup" in props:
        kwargs["export_yup"] = True
    for key in ("export_cameras", "export_lights"):
        if key in props:
            kwargs[key] = False
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(**kwargs)
    print(f"EXPORTED {GLB_PATH.name} ({GLB_PATH.stat().st_size} bytes)")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    world = bpy.data.worlds.new("VN_Player")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs[0].default_value = (0.62, 0.78, 0.9, 1.0)
    background.inputs[1].default_value = 0.9
    try:
        scene.view_settings.view_transform = "Standard"
    except Exception:
        pass

    coll = bpy.data.collections.new("Player")
    scene.collection.children.link(coll)
    materials = [
        make_material("boot", "#3f2818"),
        make_material("pants", "#6a4328"),
        make_material("tunic", "#f0e2c8"),
        make_material("vest", "#c4513a"),
        make_material("trim", "#f6efe2"),
        make_material("belt", "#3f2818"),
        make_material("buckle", "#d9cbb3"),
        make_material("skin", "#f0c8a8"),
        make_material("hair", "#5c3a28"),
        make_material("eye", "#3a2a24"),
        make_material("eye_light", "#f4f1ea"),
        make_material("mouth", "#c48a78"),
        make_material("blush", "#e7a090"),
        make_material("satchel", "#6e4a30"),
    ]
    root = bpy.data.objects.new("VNPlayer", None)
    link(root, coll)
    nodes = build(coll, materials, root)
    render(root)
    nodes["LegL"].rotation_euler.x = math.radians(-28)
    nodes["LegR"].rotation_euler.x = math.radians(28)
    nodes["ArmL"].rotation_euler.x = math.radians(20)
    nodes["ArmR"].rotation_euler.x = math.radians(-20)
    bpy.context.view_layer.update()
    scene.render.filepath = str(PREVIEW_DIR / "vn_player_step.png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath)
    for node in nodes.values():
        node.rotation_euler = (0.0, 0.0, 0.0)
    export_glb(root)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    import_path = BLEND_PATH.with_suffix(".blend.import")
    if not import_path.exists():
        import_path.write_text("[remap]\n\nimporter=\"keep\"\n\n[deps]\n\n[params]\n", encoding="utf-8")
    print("SUCCESS")


if __name__ == "__main__":
    main()

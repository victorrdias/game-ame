"""Visual valley surround for Vila Nova.

The 40m playable square stays in TerrainHeightField. This mesh only hides that
square: banks, forest, mountains, and a south road that vanishes between hills.
No collision. Houses stay out of the export.
"""

import json
import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
MODEL_DIR = ROOT / "Assets" / "Models"
PREVIEW_DIR = ROOT / "Assets" / "ArtPreviews"
GLB_PATH = MODEL_DIR / "vn_valley_surround.glb"
JSON_PATH = MODEL_DIR / "vn_valley_surround.json"
BLEND_PATH = MODEL_DIR / "vn_valley_surround.blend"

HALF = 210.0
STEP = 5.0
TOWN = 15.3


def smooth(a, b, value):
    if value <= a:
        return 0.0
    if value >= b:
        return 1.0
    t = (value - a) / (b - a)
    return t * t * (3.0 - 2.0 * t)


def square_edge(x, y):
    angle = math.atan2(y, x)
    wobble = 0.85 * math.sin(angle * 5.0) + 0.45 * math.sin(angle * 9.0 + 1.3)
    return max(abs(x), abs(y)) + wobble


def road_factor(x, y):
    if y > -8.0:
        return 0.0
    center = 1.2 + math.sin(y * 0.09) * 1.4
    width = 2.6 + max(0.0, -y - 18.0) * 0.09
    distance = abs(x - center)
    if distance >= width:
        return 0.0
    return 1.0 - distance / width


def hill_height(x, y):
    radius = math.hypot(x, y)
    rise = smooth(52.0, 84.0, radius) * 9.0 + smooth(80.0, 120.0, radius) * 16.0
    lobe = (
        1.4 * math.sin(x * 0.045 + 0.4) * math.cos(y * 0.04)
        + 0.8 * math.sin((x * 0.7 + y) * 0.055)
    ) * smooth(46.0, 70.0, radius)
    height = 0.32 + rise + lobe
    road = road_factor(x, y)
    if road > 0.0:
        along = smooth(8.0, 70.0, -y)
        road_height = 0.12 + along * 6.4
        height = height * (1.0 - road) + road_height * road
    edge = square_edge(x, y)
    if edge < 29.0 and road < 0.35:
        if edge < 18.2:
            bank = smooth(15.7, 17.8, edge)
            height = 0.12 + (2.25 - 0.12) * bank
        else:
            settle = smooth(18.2, 28.0, edge)
            height = 2.25 * (1.0 - settle) + height * settle
    return max(0.08, height)


def in_town_hole(x, y):
    return square_edge(x, y) < TOWN and road_factor(x, y) < 0.25


def make_material(name, color):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = next(node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    r, g, b = color
    bsdf.inputs["Base Color"].default_value = (r, g, b, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.94
    bsdf.inputs["Metallic"].default_value = 0.0
    return mat


def link(obj, coll):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    coll.objects.link(obj)


def build_ground(coll, materials):
    coords = []
    index_of = {}
    span = int(HALF / STEP)
    for iy in range(-span, span + 1):
        for ix in range(-span, span + 1):
            x = ix * STEP
            y = iy * STEP
            index_of[(ix, iy)] = len(coords)
            coords.append((x, y, hill_height(x, y)))

    verts = []
    faces = []
    mats = []
    for iy in range(-span, span):
        for ix in range(-span, span):
            corners = (
                coords[index_of[(ix, iy)]],
                coords[index_of[(ix + 1, iy)]],
                coords[index_of[(ix + 1, iy + 1)]],
                coords[index_of[(ix, iy + 1)]],
            )
            if all(in_town_hole(x, y) for x, y, _z in corners):
                continue
            start = len(verts)
            verts.extend(corners)
            faces.append((start, start + 1, start + 2, start + 3))
            mid_x = (corners[0][0] + corners[2][0]) * 0.5
            mid_y = (corners[0][1] + corners[2][1]) * 0.5
            mats.append(ground_material(mid_x, mid_y, materials))

    mesh = bpy.data.meshes.new("vn_surround_ground")
    mesh.from_pydata(verts, [], faces)
    for material in materials["ground"]:
        mesh.materials.append(material)
    for polygon, material_index in zip(mesh.polygons, mats):
        polygon.material_index = material_index
        polygon.use_smooth = False
    obj = bpy.data.objects.new("SurroundGround", mesh)
    link(obj, coll)
    return obj


def ground_material(x, y, materials):
    names = materials["ground_index"]
    if road_factor(x, y) > 0.45:
        return names["dirt"]
    radius = math.hypot(x, y)
    if radius > 78.0:
        return names["far"]
    patch = math.sin(x * 0.045 + 0.4) * math.cos(y * 0.038) + 0.35 * math.sin((x + y) * 0.03)
    if patch > 0.25:
        return names["grass"]
    if patch < -0.25:
        return names["grass_dark"]
    return names["grass"]


def add_cone(mesh_verts, mesh_faces, mesh_mats, origin, radius, height, sides, material_index):
    x, y, z = origin
    start = len(mesh_verts)
    mesh_verts.append((x, y, z + height))
    for i in range(sides):
        angle = math.tau * i / sides
        mesh_verts.append((x + math.cos(angle) * radius, y + math.sin(angle) * radius, z))
    center = len(mesh_verts)
    mesh_verts.append((x, y, z))
    for i in range(sides):
        nxt = (i + 1) % sides
        mesh_faces.append((start, start + 1 + i, start + 1 + nxt))
        mesh_mats.append(material_index)
        mesh_faces.append((center, start + 1 + nxt, start + 1 + i))
        mesh_mats.append(material_index)


def build_mountains(coll, materials):
    near = (
        (0, 46, 10, 15),
        (28, 36, 9, 13),
        (42, 16, 10, 16),
        (44, -14, 9, 14),
        (30, -32, 8, 12),
        (16, -46, 9, 15),
        (-16, -46, 10, 16),
        (-36, -28, 9, 13),
        (-46, -6, 10, 16),
        (-40, 22, 9, 14),
        (-22, 40, 10, 15),
        (18, 44, 8, 12),
        (-8, 50, 8, 13),
        (48, -36, 8, 12),
    )
    far = (
        (8, 96, 20, 38),
        (78, 62, 18, 34),
        (108, 6, 20, 36),
        (86, -58, 16, 32),
        (34, -102, 18, 36),
        (-32, -100, 18, 34),
        (-96, -48, 18, 34),
        (-108, 12, 20, 36),
        (-70, 78, 16, 32),
        (52, 100, 16, 30),
        (112, -40, 16, 30),
        (-112, -78, 18, 32),
    )
    verts, faces, mats = [], [], []
    for x, y, radius, height in near:
        ground = hill_height(x, y)
        add_cone(verts, faces, mats, (x, y, ground - 0.8), radius + 1.5, height + 8.0, 7, 0)
    for x, y, radius, height in far:
        ground = hill_height(x, y)
        add_cone(verts, faces, mats, (x, y, ground * 0.25), radius, height, 6, 1)
    mesh = bpy.data.meshes.new("vn_surround_mountains")
    mesh.from_pydata(verts, [], faces)
    for material in materials:
        mesh.materials.append(material)
    for polygon, material_index in zip(mesh.polygons, mats):
        polygon.material_index = material_index
        polygon.use_smooth = False
    obj = bpy.data.objects.new("SurroundMountains", mesh)
    link(obj, coll)
    return obj


def box_faces(bm_verts):
    return (
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (1, 5, 6, 2),
        (2, 6, 7, 3),
        (3, 7, 4, 0),
    )


def add_box(verts, faces, mats, center, size, yaw, material_index):
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
    for face in box_faces(None):
        faces.append(tuple(start + corner for corner in face))
        mats.append(material_index)


def hash_byte(ix, iy):
    return (ix * 73856093 ^ iy * 19349663) & 255


def build_forest(coll, materials):
    verts, faces, mats = [], [], []
    indexes = materials["forest_index"]
    for iy in range(-40, 41):
        for ix in range(-40, 41):
            grove = hash_byte(ix // 3, iy // 3)
            pick = hash_byte(ix, iy)
            x = ix * 3.6 + math.sin(iy * 1.7) * 1.5
            y = iy * 3.6 + math.cos(ix * 1.3) * 1.5
            edge = square_edge(x, y)
            if edge < 17.4 or edge > 96.0 or road_factor(x, y) > 0.22:
                continue
            if edge < 46.0:
                if grove > 230 or pick > 168:
                    continue
            elif grove > 150 or pick > 88:
                continue
            ground = hill_height(x, y)
            scale = 0.62 + (pick % 11) * 0.11
            yaw = (pick * 17 % 180) * math.pi / 180.0
            tall = pick % 5 == 0
            trunk_h = (1.7 if tall else 1.05) * scale
            trunk_w = (0.22 if tall else 0.30) * scale
            add_box(verts, faces, mats, (x, y, ground + trunk_h * 0.5), (trunk_w, trunk_w, trunk_h), yaw, indexes["trunk"])
            leaf = indexes["leaf"] if pick % 2 == 0 else indexes["leaf_dark"]
            canopy = (0.95 * scale, 0.85 * scale, 1.35 * scale) if tall else (1.55 * scale, 1.35 * scale, 1.05 * scale)
            add_box(
                verts,
                faces,
                mats,
                (x, y, ground + trunk_h + canopy[2] * 0.45),
                canopy,
                yaw,
                leaf,
            )
            if pick % 3 == 0:
                add_box(
                    verts,
                    faces,
                    mats,
                    (x + 0.35 * scale, y - 0.2 * scale, ground + trunk_h + 1.15 * scale),
                    (1.05 * scale, 0.95 * scale, 0.8 * scale),
                    yaw + 0.4,
                    indexes["leaf_light"],
                )
    mesh = bpy.data.meshes.new("vn_surround_forest")
    mesh.from_pydata(verts, [], faces)
    for material in materials["forest"]:
        mesh.materials.append(material)
    for polygon, material_index in zip(mesh.polygons, mats):
        polygon.material_index = material_index
        polygon.use_smooth = False
    obj = bpy.data.objects.new("SurroundForest", mesh)
    link(obj, coll)
    return obj


def build_gate_rocks(coll, material):
    verts, faces, mats = [], [], []
    rocks = (
        (-2.2, -15.8, 2.4, 1.6, 1.8),
        (1.2, -16.4, 2.6, 1.7, 2.2),
        (3.8, -15.5, 1.8, 1.3, 1.5),
        (0.1, -17.6, 2.0, 1.5, 1.4),
        (-4.0, -16.8, 1.6, 1.2, 1.3),
    )
    for index, (x, y, sx, sy, sz) in enumerate(rocks):
        add_box(verts, faces, mats, (x, y, hill_height(x, y) * 0.2 + sz * 0.45), (sx, sy, sz), index * 0.4, 0)
    mesh = bpy.data.meshes.new("vn_surround_gate")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(material)
    obj = bpy.data.objects.new("SurroundGate", mesh)
    link(obj, coll)
    return obj


def build_catch_plane(coll, material):
    bpy.ops.mesh.primitive_plane_add(size=420.0, location=(0.0, 0.0, -0.2))
    plane = bpy.context.active_object
    plane.name = "SurroundCatch"
    plane.data.materials.append(material)
    link(plane, coll)
    return plane


def build_preview_town(coll, material):
    bpy.ops.mesh.primitive_plane_add(size=38.0, location=(0, 0, 0.02))
    floor = bpy.context.active_object
    floor.name = "PreviewFloor"
    floor.data.materials.append(bpy.data.materials.get("grass") or material)
    link(floor, coll)
    for name, loc, size in (
        ("PreviewCottageA", (-1, -1, 1.3), (4.2, 3.6, 2.6)),
        ("PreviewCottageB", (-8, -2, 1.2), (3.4, 3.2, 2.4)),
        ("PreviewCottageC", (0, -6, 1.2), (3.4, 3.2, 2.4)),
    ):
        bpy.ops.mesh.primitive_cube_add(location=loc)
        house = bpy.context.active_object
        house.name = name
        house.scale = (size[0] * 0.5, size[1] * 0.5, size[2] * 0.5)
        house.data.materials.append(material)
        link(house, coll)


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_turns(root):
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 760
    for turn, yaw in enumerate((0, 90, 180, 270)):
        angle = math.radians(yaw)
        loc = (20.0 * math.cos(angle) - 20.0 * math.sin(angle), 20.0 * math.sin(angle) + 20.0 * math.cos(angle), 20.0)
        bpy.ops.object.camera_add(location=loc)
        cam = bpy.context.active_object
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = 64.0
        cam.data.clip_end = 800.0
        look_at(cam, (0.0, 0.0, 1.0))
        scene.camera = cam
        scene.render.filepath = str(PREVIEW_DIR / f"valley_surround_yaw{yaw}.png")
        bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(cam, do_unlink=True)
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


def write_meta():
    payload = {
        "id": "vn_valley_surround",
        "role": "visual_only",
        "collision": False,
        "playable_size_m": 40,
        "playable_half_m": 20,
        "walkable_inner_half_m": 16,
        "surround_half_m": HALF,
        "road": {
            "side": "south",
            "purpose": "visual continuation beyond the town",
            "gameplay": "blocked at the existing rim collision",
        },
        "notes": "Do not move construction, A* or occupancy outside TerrainHeightField.",
    }
    JSON_PATH.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    world = bpy.data.worlds.new("VN_Valley")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs[0].default_value = (0.62, 0.76, 0.88, 1.0)
    background.inputs[1].default_value = 0.9
    try:
        scene.view_settings.view_transform = "Standard"
    except Exception:
        pass
    scene.render.image_settings.file_format = "PNG"
    for candidate in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "CYCLES"):
        try:
            scene.render.engine = candidate
            break
        except Exception:
            continue
    eevee = getattr(scene, "eevee", None)
    if eevee is not None and hasattr(eevee, "taa_render_samples"):
        eevee.taa_render_samples = 8

    surround = bpy.data.collections.new("Surround")
    preview = bpy.data.collections.new("PreviewOnly")
    scene.collection.children.link(surround)
    scene.collection.children.link(preview)

    grass = make_material("grass", (0.49, 0.66, 0.40))
    grass_dark = make_material("grass_dark", (0.40, 0.56, 0.34))
    far = make_material("far_hill", (0.50, 0.62, 0.52))
    dirt = make_material("road", (0.62, 0.46, 0.31))
    mountain = make_material("mountain", (0.34, 0.48, 0.36))
    mountain_far = make_material("mountain_far", (0.58, 0.66, 0.62))
    trunk = make_material("trunk", (0.42, 0.30, 0.20))
    leaf = make_material("leaf", (0.36, 0.62, 0.38))
    leaf_dark = make_material("leaf_dark", (0.28, 0.50, 0.32))
    leaf_light = make_material("leaf_light", (0.55, 0.74, 0.46))
    rock = make_material("rock", (0.50, 0.48, 0.46))
    cream = make_material("preview_town", (0.86, 0.78, 0.62))
    materials = {
        "ground": [grass, grass_dark, far, dirt],
        "ground_index": {"grass": 0, "grass_dark": 1, "far": 2, "dirt": 3},
        "forest": [trunk, leaf, leaf_dark, leaf_light],
        "forest_index": {"trunk": 0, "leaf": 1, "leaf_dark": 2, "leaf_light": 3},
    }

    root = bpy.data.objects.new("ValleySurround", None)
    link(root, surround)
    for builder in (
        lambda: build_catch_plane(surround, grass),
        lambda: build_ground(surround, materials),
        lambda: build_mountains(surround, (mountain, mountain_far)),
        lambda: build_forest(surround, materials),
        lambda: build_gate_rocks(surround, rock),
    ):
        builder().parent = root

    build_preview_town(preview, cream)
    bpy.ops.object.light_add(type="SUN", location=(18, -12, 24))
    sun = bpy.context.active_object
    sun.data.energy = 3.0
    sun.data.color = (1.0, 0.94, 0.82)
    look_at(sun, (0, 0, 0))
    link(sun, preview)

    render_turns(root)
    export_glb(root)
    write_meta()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    import_path = BLEND_PATH.with_suffix(".blend.import")
    if not import_path.exists():
        import_path.write_text("[remap]\n\nimporter=\"keep\"\n\n[deps]\n\n[params]\n", encoding="utf-8")
    print("SUCCESS")


if __name__ == "__main__":
    main()

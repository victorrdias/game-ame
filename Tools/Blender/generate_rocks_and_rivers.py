"""Cottage-block rocks and rivers for the Vila Nova catalog.

Same matte stone courses as the houses. Blender Z-up, centered on the
placement footprint. export_yup maps X to Godot X and Y to Godot -Z.
Rivers stay open on the short ends so segments can continue.

blender --background --python Tools/Blender/generate_rocks_and_rivers.py
"""

from __future__ import annotations

import math
import sys
import traceback
from pathlib import Path

import bpy
import bmesh
from mathutils import Euler, Vector


ROOT = Path(__file__).resolve().parents[2]
MODEL_DIR = ROOT / "Assets" / "Models"
PREVIEW_DIR = ROOT / "Assets" / "ArtPreviews"
BLEND_PATH = MODEL_DIR / "vn_rocks_and_rivers.blend"
CELL = 2.0 / 3.0


def rgba(hex_color, alpha=1.0):
    h = hex_color.lstrip("#")
    rgb = tuple(int(h[i : i + 2], 16) / 255.0 for i in (0, 2, 4))

    def linear(channel):
        if channel <= 0.04045:
            return channel / 12.92
        return ((channel + 0.055) / 1.055) ** 2.4

    return tuple(linear(c) for c in rgb) + (alpha,)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def ensure_collection(name):
    coll = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(coll)
    return coll


def link_object(obj, coll):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    coll.objects.link(obj)


def make_material(name, hex_color, roughness=0.94):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = next(node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = rgba(hex_color)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = 0.0
    spec = bsdf.inputs.get("Specular IOR Level") or bsdf.inputs.get("Specular")
    if spec is not None:
        spec.default_value = 0.08
    return mat


class Bucket:
    def __init__(self, name, collection):
        self.name = name
        self.collection = collection
        self.bm = bmesh.new()
        self.materials = []
        self._index = {}

    def slot(self, mat):
        idx = self._index.get(mat.name)
        if idx is None:
            idx = len(self.materials)
            self._index[mat.name] = idx
            self.materials.append(mat)
        return idx

    def add_box(self, center, size, mat, yaw=0.0):
        sx, sy, sz = (float(v) for v in size)
        if sx <= 0.004 or sy <= 0.004 or sz <= 0.004:
            return
        geom = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = geom["verts"]
        bmesh.ops.scale(self.bm, verts=verts, vec=Vector((sx, sy, sz)))
        if abs(yaw) > 0.001:
            rotation = Euler((0.0, 0.0, yaw), "XYZ")
            bmesh.ops.rotate(
                self.bm,
                verts=verts,
                cent=Vector((0.0, 0.0, 0.0)),
                matrix=rotation.to_matrix(),
            )
        bmesh.ops.translate(self.bm, verts=verts, vec=Vector(center))
        idx = self.slot(mat)
        for face in {f for v in verts for f in v.link_faces}:
            face.material_index = idx
            face.smooth = False

    def finish(self, parent):
        if len(self.bm.verts) == 0:
            self.bm.free()
            return
        self.bm.normal_update()
        mesh = bpy.data.meshes.new(self.name + "_mesh")
        self.bm.to_mesh(mesh)
        self.bm.free()
        for mat in self.materials:
            mesh.materials.append(mat)
        for poly in mesh.polygons:
            poly.use_smooth = False
        obj = bpy.data.objects.new(self.name, mesh)
        link_object(obj, self.collection)
        obj.parent = parent


def make_materials():
    stones = [make_material(f"VN_Rock_stone_{i}", h) for i, h in enumerate(
        ("#8e8b86", "#a39e96", "#6f6d68", "#b7b2a8", "#7a7772")
    )]
    warm = [make_material(f"VN_Rock_warm_{i}", h) for i, h in enumerate(
        ("#c4b49a", "#d9cbb3", "#a89880")
    )]
    leaves = [make_material(f"VN_Rock_leaf_{i}", h, 0.9) for i, h in enumerate(
        ("#3f8f4e", "#5aaa62", "#2f6e3c")
    )]
    return {
        "stones": stones,
        "warm": warm,
        "moss": leaves[0],
        "moss_light": leaves[1],
        "moss_dark": leaves[2],
        "water": make_material("VN_Rock_water", "#6aa8b0", 0.55),
        "water_deep": make_material("VN_Rock_water_deep", "#4d8490", 0.62),
        "foam": make_material("VN_Rock_foam", "#e7f0ea", 0.72),
    }


def add_tuft(bucket, x, y, z, mats, scale=1.0):
    for i, (dx, dy) in enumerate(((0.035, 0.0), (-0.03, 0.02), (0.0, -0.03))):
        height = (0.18 + 0.04 * (i % 2)) * scale
        bucket.add_box(
            (x + dx * scale, y + dy * scale, z + height * 0.5),
            (0.035 * scale, 0.03 * scale, height),
            mats["moss"] if i != 1 else mats["moss_light"],
        )


def add_moss(bucket, x, y, z, sx, sy, mats, dark=False):
    bucket.add_box((x, y, z), (sx, sy, 0.045), mats["moss_dark"] if dark else mats["moss"])


def build_rock_small(root, coll, mats):
    stone = Bucket("rock_small", coll)
    s = mats["stones"]
    w = mats["warm"]
    stone.add_box((0.0, 0.0, 0.11), (0.88, 0.6, 0.22), s[1])
    stone.add_box((-0.14, 0.08, 0.28), (0.46, 0.32, 0.2), s[0], yaw=0.4)
    stone.add_box((0.18, -0.1, 0.26), (0.34, 0.28, 0.18), s[3], yaw=-0.45)
    stone.add_box((-0.02, 0.0, 0.44), (0.26, 0.2, 0.14), w[0], yaw=0.2)
    stone.add_box((0.32, 0.16, 0.08), (0.16, 0.14, 0.14), s[2], yaw=0.55)
    stone.add_box((-0.32, -0.16, 0.07), (0.16, 0.14, 0.12), w[1], yaw=-0.3)
    add_moss(stone, -0.02, 0.0, 0.53, 0.16, 0.12, mats)
    add_moss(stone, 0.16, -0.08, 0.36, 0.14, 0.1, mats, dark=True)
    add_tuft(stone, -0.28, 0.2, 0.22, mats, 0.75)
    add_tuft(stone, 0.24, -0.22, 0.2, mats, 0.6)
    stone.finish(root)


def build_rock_medium(root, coll, mats):
    stone = Bucket("rock_medium", coll)
    s = mats["stones"]
    w = mats["warm"]
    stone.add_box((0.0, -0.02, 0.15), (1.42, 1.02, 0.3), s[0])
    stone.add_box((-0.32, 0.12, 0.4), (0.7, 0.5, 0.32), s[3], yaw=0.18)
    stone.add_box((0.34, -0.16, 0.38), (0.58, 0.46, 0.28), w[0], yaw=-0.22)
    stone.add_box((-0.08, 0.04, 0.66), (0.46, 0.34, 0.28), s[1], yaw=0.12)
    stone.add_box((0.16, -0.06, 0.86), (0.28, 0.22, 0.16), s[2], yaw=-0.1)
    stone.add_box((0.62, 0.32, 0.11), (0.26, 0.2, 0.16), w[1], yaw=0.45)
    stone.add_box((-0.62, -0.34, 0.1), (0.24, 0.18, 0.14), s[4], yaw=-0.35)
    stone.add_box((0.22, 0.38, 0.1), (0.2, 0.16, 0.12), s[2], yaw=0.6)
    add_moss(stone, -0.08, 0.04, 0.82, 0.28, 0.16, mats)
    add_moss(stone, 0.34, -0.14, 0.54, 0.22, 0.14, mats, dark=True)
    add_tuft(stone, -0.52, 0.32, 0.3, mats, 0.9)
    add_tuft(stone, 0.5, -0.38, 0.3, mats, 0.7)
    stone.finish(root)


def build_rock_large(root, coll, mats):
    stone = Bucket("rock_large", coll)
    s = mats["stones"]
    w = mats["warm"]
    stone.add_box((0.04, -0.04, 0.18), (1.9, 1.28, 0.36), s[1])
    stone.add_box((-0.38, 0.12, 0.48), (1.05, 0.72, 0.46), s[0], yaw=0.08)
    stone.add_box((0.5, -0.14, 0.42), (0.86, 0.64, 0.38), s[3], yaw=-0.1)
    stone.add_box((-0.18, 0.04, 0.82), (0.7, 0.5, 0.42), s[2], yaw=0.06)
    stone.add_box((0.36, 0.0, 0.78), (0.52, 0.42, 0.36), w[0])
    stone.add_box((-0.04, 0.02, 1.12), (0.4, 0.3, 0.28), s[4])
    stone.add_box((0.02, 0.0, 1.34), (0.24, 0.18, 0.18), w[1])
    stone.add_box((0.08, 0.02, 0.7), (0.06, 0.55, 0.7), s[4])
    stone.add_box((0.82, 0.36, 0.14), (0.36, 0.28, 0.22), w[2], yaw=0.35)
    stone.add_box((-0.86, -0.38, 0.11), (0.34, 0.26, 0.18), s[2], yaw=-0.25)
    stone.add_box((0.15, -0.48, 0.1), (0.26, 0.2, 0.16), s[0], yaw=0.5)
    add_moss(stone, -0.18, 0.06, 1.05, 0.32, 0.18, mats)
    add_moss(stone, 0.36, -0.02, 0.98, 0.24, 0.14, mats, dark=True)
    add_moss(stone, 0.02, 0.0, 1.45, 0.16, 0.1, mats)
    add_tuft(stone, -0.72, 0.34, 0.36, mats, 1.0)
    add_tuft(stone, 0.7, -0.4, 0.36, mats, 0.8)
    add_tuft(stone, 0.2, 0.42, 0.36, mats, 0.7)
    stone.finish(root)


class RiverParts:
    """One mesh per edge cell so a neighbor can hide just that border."""

    def __init__(self, collection):
        self.collection = collection
        self.buckets = {}

    def bucket(self, name):
        found = self.buckets.get(name)
        if found is None:
            found = Bucket(name, self.collection)
            self.buckets[name] = found
        return found

    def finish(self, root):
        for bucket in self.buckets.values():
            bucket.finish(root)


def cell_index(coord, origin, count):
    index = int(math.floor((coord - origin) / CELL))
    return max(0, min(count - 1, index))


def x_index(x, cells_x, span_x):
    return cell_index(x, -span_x * 0.5, cells_x)


def z_index_from_blender_y(y, cells_y, span_y):
    # export_yup sends Blender +Y to Godot -Z. Index 0 is Godot's minimum Z.
    return cell_index(-y, -span_y * 0.5, cells_y)


def lay_bank(parts, x0, x1, y, z0, mats, salt, depth, edge, cells_x, span_x):
    widths = (0.34, 0.48, 0.3, 0.42, 0.26, 0.5, 0.36)
    heights = (0.22, 0.28, 0.18, 0.26, 0.2)
    stones = mats["stones"]
    warm = mats["warm"]
    x = x0
    i = 0
    while x < x1 - 0.05:
        width = widths[(i + salt) % len(widths)]
        if x + width > x1:
            width = x1 - x
        if width < 0.14:
            break
        height = heights[(i + salt) % len(heights)]
        mat = warm[(i + salt) % 2] if (i + salt) % 5 == 0 else stones[(i * 2 + salt) % len(stones)]
        center_x = x + width * 0.5
        bucket = parts.bucket(f"{edge}_{x_index(center_x, cells_x, span_x)}")
        bucket.add_box((center_x, y, z0 + height * 0.5), (width * 0.94, depth, height), mat)
        if (i + salt) % 2 == 0:
            cap_h = 0.1 + 0.03 * ((i + salt) % 3)
            bucket.add_box(
                (x + width * 0.48, y, z0 + height + cap_h * 0.5),
                (width * 0.58, depth * 0.72, cap_h),
                stones[(i + 3 + salt) % len(stones)],
                yaw=0.08 if i % 2 == 0 else -0.06,
            )
        if (i + salt) % 4 == 1:
            add_moss(
                bucket,
                center_x,
                y,
                z0 + height + 0.08,
                width * 0.4,
                depth * 0.45,
                mats,
                dark=(i % 2 == 0),
            )
        x += width * 0.9
        i += 1


def lay_end(parts, x, mats, salt, depth, edge, cells_y, span_y):
    """Stone cap across a short end. Hidden when another river continues past it."""
    heights = (0.24, 0.3, 0.2, 0.26, 0.22)
    stones = mats["stones"]
    warm = mats["warm"]
    for index in range(cells_y):
        godot_z = -span_y * 0.5 + (index + 0.5) * CELL
        y = -godot_z
        height = heights[(index + salt) % len(heights)]
        mat = warm[(index + salt) % 2] if (index + salt) % 4 == 0 else stones[(index + salt) % len(stones)]
        bucket = parts.bucket(f"{edge}_{index}")
        bucket.add_box((x, y, 0.07 + height * 0.5), (depth, CELL * 0.94, height), mat)
        if index % 2 == 0:
            cap_h = 0.1 + 0.02 * (index % 3)
            bucket.add_box(
                (x, y, 0.07 + height + cap_h * 0.5),
                (depth * 0.72, CELL * 0.55, cap_h),
                stones[(index + 2) % len(stones)],
            )
        if index % 3 == 1:
            add_moss(bucket, x, y, 0.07 + height + 0.04, depth * 0.55, CELL * 0.4, mats, dark=index % 2 == 0)


def add_foam(bucket, x, y, mats, scale=1.0):
    bucket.add_box((x, y, 0.145), (0.16 * scale, 0.07 * scale, 0.02), mats["foam"])
    bucket.add_box((x + 0.12 * scale, y + 0.03 * scale, 0.148), (0.08 * scale, 0.04 * scale, 0.015), mats["foam"])


def build_river(root, coll, mats, cells_x, cells_y, kind):
    sx = cells_x * CELL
    sy = cells_y * CELL
    parts = RiverParts(coll)
    # Water fills the footprint and overlaps the shared edge by a centimeter,
    # so two rivers meet with no grass or stone seam.
    pad = 0.03
    water = parts.bucket("Water")
    water.add_box((0.0, 0.0, 0.08), (sx + pad, sy + pad, 0.05), mats["water_deep"])
    water.add_box((0.0, 0.0, 0.105), (sx + pad, sy + pad, 0.03), mats["water"])

    bank = 0.42 if kind == "creek" else 0.5 if kind == "reach" else 0.62
    span_x = sx * 0.98
    span_y = sy * 0.98
    y_bank = (span_y * 0.5) - bank * 0.5
    x0 = -span_x * 0.5
    x1 = span_x * 0.5
    # Blender +Y becomes Godot -Z.
    lay_bank(parts, x0, x1, y_bank, 0.07, mats, 1, bank * 0.92, "EdgeNZ", cells_x, sx)
    lay_bank(parts, x0, x1, -y_bank, 0.07, mats, 4, bank * 0.92, "EdgePZ", cells_x, sx)
    end_depth = bank * 0.92
    x_cap = span_x * 0.5 - end_depth * 0.5
    lay_end(parts, -x_cap, mats, 2, end_depth, "EdgeNX", cells_y, sy)
    lay_end(parts, x_cap, mats, 5, end_depth, "EdgePX", cells_y, sy)

    center = parts.bucket("Center")
    north = lambda x: parts.bucket(f"EdgeNZ_{x_index(x, cells_x, sx)}")
    south = lambda x: parts.bucket(f"EdgePZ_{x_index(x, cells_x, sx)}")
    if kind == "creek":
        center.add_box((0.15, 0.0, 0.13), (0.22, 0.16, 0.1), mats["stones"][2])
        add_foam(center, -0.25, 0.0, mats, 0.8)
        add_tuft(north(0.0), 0.0, y_bank, 0.32, mats, 0.55)
        add_tuft(south(-0.35), -0.35, -y_bank, 0.3, mats, 0.5)
    elif kind == "reach":
        center.add_box((-0.55, 0.05, 0.14), (0.28, 0.2, 0.12), mats["stones"][4])
        center.add_box((0.85, -0.04, 0.13), (0.22, 0.16, 0.1), mats["warm"][0])
        add_foam(center, -1.15, 0.0, mats)
        add_foam(center, 0.15, 0.0, mats, 0.9)
        add_foam(center, 1.2, 0.0, mats, 0.7)
        add_tuft(north(-0.4), -0.4, y_bank, 0.36, mats, 0.75)
        add_tuft(south(0.9), 0.9, -y_bank, 0.34, mats, 0.7)
        add_tuft(north(0.2), 0.2, y_bank, 0.34, mats, 0.6)
    else:
        center.add_box((-0.35, 0.05, 0.16), (1.15, 0.55, 0.14), mats["warm"][2])
        center.add_box((-0.15, 0.08, 0.28), (0.48, 0.3, 0.14), mats["stones"][1])
        center.add_box((0.1, 0.0, 0.3), (0.28, 0.2, 0.1), mats["stones"][3], yaw=0.2)
        add_moss(center, -0.2, 0.08, 0.36, 0.28, 0.14, mats)
        add_tuft(center, -0.55, 0.12, 0.3, mats, 0.65)
        center.add_box((1.35, -0.08, 0.14), (0.32, 0.22, 0.12), mats["stones"][2])
        center.add_box((-1.7, 0.06, 0.13), (0.28, 0.2, 0.1), mats["warm"][1])
        for fx in (-2.4, -1.2, 0.35, 1.5, 2.3):
            add_foam(center, fx, 0.0, mats, 1.1 if abs(fx) < 1.5 else 0.85)
        add_tuft(north(-2.2), -2.2, y_bank, 0.4, mats, 0.9)
        add_tuft(south(1.8), 1.8, -y_bank, 0.38, mats, 0.85)
        add_tuft(north(0.4), 0.4, y_bank, 0.4, mats, 0.75)
        add_tuft(south(-0.8), -0.8, -y_bank, 0.36, mats, 0.7)

    parts.finish(root)


def world_bounds(root):
    xs, ys, zs = [], [], []
    for obj in root.children_recursive:
        if obj.type != "MESH":
            continue
        for vert in obj.data.vertices:
            point = obj.matrix_world @ vert.co
            xs.append(point.x)
            ys.append(point.y)
            zs.append(point.z)
    return min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)


def validate(root, cells_x, cells_y):
    x0, x1, y0, y1, z0, z1 = world_bounds(root)
    limit_x = cells_x * CELL * 0.5
    limit_y = cells_y * CELL * 0.5
    print(
        f"BOUNDS {root.name} x[{x0:.2f},{x1:.2f}] y[{y0:.2f},{y1:.2f}] "
        f"z[{z0:.2f},{z1:.2f}] limit[{limit_x:.2f},{limit_y:.2f}]"
    )
    if z0 < -0.02:
        raise RuntimeError(f"{root.name} sinks below ground ({z0:.3f})")
    if max(-x0, x1) > limit_x + 0.04 or max(-y0, y1) > limit_y + 0.04:
        raise RuntimeError(f"{root.name} leaves its footprint")


def export_glb(root, path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    kwargs = {
        "filepath": str(path),
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
    bpy.ops.export_scene.gltf(**kwargs)
    if not path.is_file() or path.stat().st_size < 512:
        raise RuntimeError(f"GLB export failed: {path}")
    print(f"EXPORTED {path.name} ({path.stat().st_size} bytes)")


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def setup_world():
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    world = bpy.data.worlds.new("VN_Rocks")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs[0].default_value = (0.62, 0.78, 0.9, 1.0)
    background.inputs[1].default_value = 0.9
    try:
        scene.view_settings.view_transform = "Standard"
    except Exception:
        pass
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.image_settings.file_format = "PNG"
    for candidate in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "CYCLES"):
        try:
            scene.render.engine = candidate
            break
        except Exception:
            continue


def hide_tree(root, hidden):
    root.hide_render = hidden
    for child in root.children_recursive:
        child.hide_render = hidden


def render_iso(root, stem, ortho, focus_z):
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    distance = max(ortho * 0.9, 2.2)
    bpy.ops.object.camera_add(location=(distance, -distance * 1.05, distance * 0.72))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ortho
    cam.data.clip_end = 80.0
    look_at(cam, (0.0, 0.0, focus_z))
    scene = bpy.context.scene
    scene.camera = cam
    scene.render.filepath = str(PREVIEW_DIR / f"{stem}_iso.png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath)
    bpy.data.objects.remove(cam, do_unlink=True)


def main():
    clear_scene()
    setup_world()
    ground_coll = ensure_collection("PreviewGround")
    bpy.ops.mesh.primitive_plane_add(size=40.0, location=(0.0, 0.0, -0.02))
    ground = bpy.context.active_object
    ground.data.materials.append(make_material("VN_Rock_ground", "#c6d46a", 0.95))
    link_object(ground, ground_coll)
    bpy.ops.object.light_add(type="SUN", location=(6.0, -8.0, 10.0))
    sun = bpy.context.active_object
    sun.data.energy = 3.2
    sun.data.color = (1.0, 0.94, 0.84)
    look_at(sun, (0.0, 0.0, 0.4))
    link_object(sun, ground_coll)

    mats = make_materials()
    specs = (
        ("vn_rock_small", build_rock_small, None, 2, 2, 2.2, 0.35),
        ("vn_rock_medium", build_rock_medium, None, 3, 3, 3.2, 0.55),
        ("vn_rock_large", build_rock_large, None, 4, 3, 4.2, 0.7),
        ("vn_river_3x3", build_river, "creek", 3, 3, 3.4, 0.2),
        ("vn_river_6x4", build_river, "reach", 6, 4, 5.6, 0.25),
        ("vn_river_10x8", build_river, "wide", 10, 8, 10.5, 0.3),
    )
    roots = []
    for index, (name, builder, kind, cells_x, cells_y, ortho, focus_z) in enumerate(specs):
        for prev in roots:
            hide_tree(prev, True)
        coll = ensure_collection(name)
        root = bpy.data.objects.new(name, None)
        link_object(root, coll)
        if kind is None:
            builder(root, coll, mats)
        else:
            builder(root, coll, mats, cells_x, cells_y, kind)
        bpy.context.view_layer.update()
        validate(root, cells_x, cells_y)
        export_glb(root, MODEL_DIR / f"{name}.glb")
        hide_tree(root, False)
        render_iso(root, name, ortho, focus_z)
        root.location.x = (index + 1) * 8.0
        bpy.context.view_layer.update()
        roots.append(root)

    for root in roots:
        hide_tree(root, False)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    keep = BLEND_PATH.with_suffix(".blend.import")
    if not keep.exists():
        keep.write_text("[remap]\n\nimporter=\"keep\"\n\n[deps]\n\n[params]\n", encoding="utf-8")
    print("SUCCESS")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        sys.exit(1)

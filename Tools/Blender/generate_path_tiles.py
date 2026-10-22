"""Path tiles for the Vila Nova catalog.

1x1 and 2x2 floors in the cottage palette. A stone rim sits on each side
and is named like the river banks (EdgeNX/PX/NZ/PZ) so same-type pieces
can hide the shared rim. The bed extends a little past the footprint so
the join has no grass seam.

blender --background --python Tools/Blender/generate_path_tiles.py
"""

from __future__ import annotations

import sys
import traceback
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
MODEL_DIR = ROOT / "Assets" / "Models"
PREVIEW_DIR = ROOT / "Assets" / "ArtPreviews"
BLEND_PATH = MODEL_DIR / "vn_path_tiles.blend"
CELL = 2.0 / 3.0
PAD = 0.016
RIM = 0.058


def rgba(hex_color):
    h = hex_color.lstrip("#")
    rgb = tuple(int(h[i : i + 2], 16) / 255.0 for i in (0, 2, 4))

    def linear(channel):
        if channel <= 0.04045:
            return channel / 12.92
        return ((channel + 0.055) / 1.055) ** 2.4

    return tuple(linear(c) for c in rgb) + (1.0,)


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

    def add_box(self, center, size, mat):
        sx, sy, sz = (float(v) for v in size)
        if sx <= 0.004 or sy <= 0.004 or sz <= 0.004:
            return
        geom = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = geom["verts"]
        bmesh.ops.scale(self.bm, verts=verts, vec=Vector((sx, sy, sz)))
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


class Parts:
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


def make_materials():
    stones = [make_material(f"VN_Path_stone_{i}", h) for i, h in enumerate(
        ("#8e8b86", "#a39e96", "#6f6d68", "#b7b2a8", "#7a7772")
    )]
    warm = [make_material(f"VN_Path_warm_{i}", h) for i, h in enumerate(("#c4b49a", "#d9cbb3", "#a89880"))]
    roofs = [make_material(f"VN_Path_roof_{i}", h, 0.9) for i, h in enumerate(
        ("#c4513a", "#d4654a", "#b84432", "#a33a2c", "#e07a55")
    )]
    return {
        "stones": stones,
        "warm": warm,
        "roofs": roofs,
        "soil": make_material("VN_Path_soil", "#6b4a32"),
        "grout": make_material("VN_Path_grout", "#8d3b2a", 0.9),
        "plaster": make_material("VN_Path_plaster", "#f0e2c8"),
        "rim": warm[0],
    }


def lay_cobble(bucket, reach, mats):
    bucket.add_box((0.0, 0.0, 0.02), (reach * 2.0, reach * 2.0, 0.032), mats["soil"])
    stones = mats["stones"] + mats["warm"]
    y = -reach + 0.07
    row = 0
    while y < reach - 0.04:
        x = -reach + (0.09 if row % 2 else 0.05)
        col = 0
        while x < reach - 0.04:
            width = 0.16 if (row + col) % 2 == 0 else 0.11
            if x + width > reach - 0.02:
                width = reach - 0.02 - x
            if width < 0.06:
                break
            bucket.add_box((x + width * 0.5, y, 0.046), (width * 0.92, 0.09, 0.03), stones[(row + col) % len(stones)])
            x += width + 0.018
            col += 1
        y += 0.115
        row += 1


def lay_terracotta(bucket, reach, mats):
    bucket.add_box((0.0, 0.0, 0.018), (reach * 2.0, reach * 2.0, 0.028), mats["grout"])
    colors = mats["roofs"]
    tile_w = 0.125
    tile_d = 0.086
    y = -reach + 0.06
    row = 0
    while y + tile_d * 0.5 < reach - 0.01:
        x = -reach + (0.08 if row % 2 else 0.03)
        col = 0
        while x + tile_w < reach - 0.008:
            bucket.add_box((x + tile_w * 0.5, y, 0.042), (tile_w, tile_d, 0.024), colors[(row + col) % len(colors)])
            x += 0.145
            col += 1
        y += 0.108
        row += 1


def lay_slab(bucket, reach, mats, cells):
    bucket.add_box((0.0, 0.0, 0.02), (reach * 2.0, reach * 2.0, 0.03), mats["stones"][2])
    count = cells * 2
    pitch = (reach * 2.0) / count
    for row in range(count):
        for col in range(count):
            shrink = 0.028 if (row + col) % 2 == 0 else 0.02
            bucket.add_box(
                (-reach + (col + 0.5) * pitch, -reach + (row + 0.5) * pitch, 0.046),
                (pitch - shrink, pitch - shrink * 0.8, 0.026),
                mats["warm"][(row + col) % 2] if (row + col) % 3 else mats["plaster"],
            )


def lay_rims(parts, cells, mats):
    span = cells * CELL
    half = span * 0.5
    lip = RIM
    for index in range(cells):
        along = -half + (index + 0.5) * CELL
        # Godot X is Blender X. Godot Z is -Blender Y.
        parts.bucket(f"EdgePX_{index}").add_box((half - lip * 0.5, -along, 0.07), (lip, CELL * 1.08, 0.05), mats["rim"])
        parts.bucket(f"EdgeNX_{index}").add_box((-half + lip * 0.5, -along, 0.07), (lip, CELL * 1.08, 0.05), mats["rim"])
        parts.bucket(f"EdgeNZ_{index}").add_box((along, half - lip * 0.5, 0.07), (CELL * 1.08, lip, 0.05), mats["rim"])
        parts.bucket(f"EdgePZ_{index}").add_box((along, -(half - lip * 0.5), 0.07), (CELL * 1.08, lip, 0.05), mats["rim"])


def build_path(root, coll, mats, cells, kind):
    span = cells * CELL
    half = span * 0.5
    reach = half + PAD
    parts = Parts(coll)
    bed = parts.bucket("Bed")
    if kind == "cobble":
        lay_cobble(bed, reach, mats)
    elif kind == "terracotta":
        lay_terracotta(bed, reach, mats)
    else:
        lay_slab(bed, reach, mats, cells)
    lay_rims(parts, cells, mats)
    parts.finish(root)


def bounds(root):
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


def validate(root, cells):
    limit = cells * CELL * 0.5
    x0, x1, y0, y1, z0, z1 = bounds(root)
    print(
        f"BOUNDS {root.name} x[{x0:.3f},{x1:.3f}] y[{y0:.3f},{y1:.3f}] z[{z0:.3f},{z1:.3f}]"
    )
    if z0 < -0.02:
        raise RuntimeError(f"{root.name} sinks ({z0:.3f})")
    if max(-x0, x1, -y0, y1) > limit + 0.03:
        raise RuntimeError(f"{root.name} leaves its footprint")
    names = {child.name for child in root.children}
    for side in ("EdgePX", "EdgeNX", "EdgeNZ", "EdgePZ"):
        for index in range(cells):
            expected = f"{side}_{index}"
            if expected not in names:
                raise RuntimeError(f"{root.name} missing {expected}")


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
    print(f"EXPORTED {path.name} ({path.stat().st_size} bytes)")


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def setup_world():
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    world = bpy.data.worlds.new("VN_Paths")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs[0].default_value = (0.62, 0.78, 0.9, 1.0)
    background.inputs[1].default_value = 0.9
    try:
        scene.view_settings.view_transform = "Standard"
    except Exception:
        pass
    scene.render.resolution_x = 800
    scene.render.resolution_y = 600
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


def hide_tree(root, hidden):
    root.hide_render = hidden
    for child in root.children_recursive:
        child.hide_render = hidden


def render_iso(root, stem, ortho):
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    distance = max(ortho * 1.1, 1.6)
    bpy.ops.object.camera_add(location=(distance, -distance, distance * 0.85))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ortho
    cam.data.clip_end = 40.0
    look_at(cam, (0.0, 0.0, 0.02))
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
    bpy.ops.mesh.primitive_plane_add(size=12.0, location=(0.0, 0.0, -0.02))
    ground = bpy.context.active_object
    ground.data.materials.append(make_material("VN_Path_ground", "#7ea36a", 0.95))
    link_object(ground, ground_coll)
    bpy.ops.object.light_add(type="SUN", location=(4.0, -5.0, 8.0))
    sun = bpy.context.active_object
    sun.data.energy = 3.0
    sun.data.color = (1.0, 0.95, 0.86)
    look_at(sun, (0.0, 0.0, 0.0))
    link_object(sun, ground_coll)

    mats = make_materials()
    specs = (
        ("vn_path_cobble", 1, "cobble", 1.5),
        ("vn_path_terracotta", 1, "terracotta", 1.5),
        ("vn_path_slab", 1, "slab", 1.5),
        ("vn_path_cobble_2x2", 2, "cobble", 2.4),
        ("vn_path_terracotta_2x2", 2, "terracotta", 2.4),
        ("vn_path_slab_2x2", 2, "slab", 2.4),
    )
    roots = []
    for name, cells, kind, ortho in specs:
        for prev in roots:
            hide_tree(prev, True)
        coll = ensure_collection(name)
        root = bpy.data.objects.new(name, None)
        link_object(root, coll)
        build_path(root, coll, mats, cells, kind)
        validate(root, cells)
        export_glb(root, MODEL_DIR / f"{name}.glb")
        for child in list(root.children):
            child.name = f"{name}__{child.name}"
        hide_tree(root, False)
        render_iso(root, name, ortho)
        root.location.x = (len(roots) + 1) * 4.0
        bpy.context.view_layer.update()
        roots.append(root)
    try:
        bpy.context.preferences.filepaths.save_version = 0
    except Exception:
        pass
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    keep = Path(str(BLEND_PATH) + ".import")
    if not keep.exists():
        keep.write_text('[remap]\n\nimporter="keep"\n\n[deps]\n\n[params]\n', encoding="utf-8")
    print("SUCCESS")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        sys.exit(1)

# Vila Nova — cottage-style decorations.
# Same matte palette as the reference cottage: terracotta, warm stone,
# dark timber, amber glass, chunky flowers. Props are 1x1 and stay
# inside a 2 m cell. Facade, when there is one, faces +Y.
#
# blender --background --python Tools/Blender/generate_village_decorations.py

from __future__ import annotations

import math
import sys
import traceback
from pathlib import Path

import bpy
import bmesh
from mathutils import Euler, Vector

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parent.parent
MODEL_DIR = PROJECT_ROOT / "Assets" / "Models"
PREVIEW_DIR = PROJECT_ROOT / "Assets" / "ArtPreviews"
BLEND_PATH = MODEL_DIR / "vn_village_decorations.blend"
FOOTPRINT = 0.98


def srgb_hex(value: str) -> tuple[float, float, float]:
    h = value.lstrip("#")
    rgb = tuple(int(h[i : i + 2], 16) / 255.0 for i in (0, 2, 4))

    def to_linear(channel: float) -> float:
        if channel <= 0.04045:
            return channel / 12.92
        return ((channel + 0.055) / 1.055) ** 2.4

    return tuple(to_linear(c) for c in rgb)  # type: ignore[return-value]


def rgba(value: str, alpha: float = 1.0) -> tuple[float, float, float, float]:
    r, g, b = srgb_hex(value)
    return (r, g, b, alpha)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.cameras):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def ensure_collection(name: str, parent: bpy.types.Collection | None = None) -> bpy.types.Collection:
    coll = bpy.data.collections.get(name)
    if coll is None:
        coll = bpy.data.collections.new(name)
    host = parent if parent is not None else bpy.context.scene.collection
    if coll.name not in {c.name for c in host.children}:
        host.children.link(coll)
    return coll


def link_object(obj: bpy.types.Object, coll: bpy.types.Collection) -> None:
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    coll.objects.link(obj)


def make_material(
    name: str,
    hex_color: str,
    roughness: float = 0.92,
    emission: str | None = None,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = rgba(hex_color)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = 0.0
    spec = bsdf.inputs.get("Specular IOR Level") or bsdf.inputs.get("Specular")
    if spec is not None:
        spec.default_value = 0.08
    if emission and emission_strength > 0.0:
        emit_socket = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
        if emit_socket is not None:
            emit_socket.default_value = rgba(emission)
        strength = bsdf.inputs.get("Emission Strength")
        if strength is not None:
            strength.default_value = emission_strength
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


class Bucket:
    def __init__(self, name: str, collection: bpy.types.Collection) -> None:
        self.name = name
        self.collection = collection
        self.bm = bmesh.new()
        self.materials: list[bpy.types.Material] = []
        self._index: dict[str, int] = {}

    def slot(self, mat: bpy.types.Material) -> int:
        idx = self._index.get(mat.name)
        if idx is None:
            idx = len(self.materials)
            self._index[mat.name] = idx
            self.materials.append(mat)
        return idx

    def add_box(self, center, size, mat, rotation: Euler | None = None) -> None:
        sx, sy, sz = float(size[0]), float(size[1]), float(size[2])
        if sx <= 0.004 or sy <= 0.004 or sz <= 0.004:
            return
        geom = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = geom["verts"]
        bmesh.ops.scale(self.bm, verts=verts, vec=Vector((sx, sy, sz)))
        if rotation is not None:
            bmesh.ops.rotate(self.bm, verts=verts, cent=Vector((0.0, 0.0, 0.0)), matrix=rotation.to_matrix())
        bmesh.ops.translate(self.bm, verts=verts, vec=Vector(center))
        idx = self.slot(mat)
        for face in {f for v in verts for f in v.link_faces}:
            face.material_index = idx
            face.smooth = False

    def add_cylinder(self, center, radius_bottom, radius_top, height, mat, segments=8, rotation: Euler | None = None) -> None:
        if height <= 0.004 or max(radius_bottom, radius_top) <= 0.004:
            return
        bm = self.bm
        idx = self.slot(mat)
        bottom = []
        top = []
        z0 = -height * 0.5
        z1 = height * 0.5
        for i in range(segments):
            angle = math.tau * i / segments
            c = math.cos(angle)
            s = math.sin(angle)
            bottom.append(bm.verts.new((c * radius_bottom, s * radius_bottom, z0)))
            top.append(bm.verts.new((c * radius_top, s * radius_top, z1)))
        verts = bottom + top
        if rotation is not None:
            bmesh.ops.rotate(bm, verts=verts, cent=Vector((0.0, 0.0, 0.0)), matrix=rotation.to_matrix())
        bmesh.ops.translate(bm, verts=verts, vec=Vector(center))
        bm.verts.ensure_lookup_table()
        for face in (
            bm.faces.new(list(reversed(bottom))),
            bm.faces.new(top),
        ):
            face.material_index = idx
            face.smooth = False
        for i in range(segments):
            j = (i + 1) % segments
            side = bm.faces.new((bottom[i], top[i], top[j], bottom[j]))
            side.material_index = idx
            side.smooth = False

    def finish(self, parent: bpy.types.Object) -> None:
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
        obj.matrix_parent_inverse = parent.matrix_world.inverted()


def add_flower(bucket: Bucket, x: float, y: float, z: float, color, mats: dict, scale: float = 1.0) -> None:
    stem = 0.12 * scale
    bucket.add_box((x, y, z + stem * 0.5), (0.025 * scale, 0.025 * scale, stem), mats["leaf_stem"])
    petal = 0.07 * scale
    petal_z = z + stem
    for dx, dy in ((petal, 0.0), (-petal, 0.0), (0.0, petal), (0.0, -petal)):
        bucket.add_box((x + dx, y + dy, petal_z), (0.065 * scale, 0.065 * scale, 0.035 * scale), color)
    bucket.add_box((x, y, petal_z + 0.02 * scale), (0.04 * scale, 0.04 * scale, 0.03 * scale), mats["flower_y"])


def gable_cap(bucket: Bucket, x: float, y: float, z: float, width: float, depth: float, mats: dict) -> None:
    """Two short terracotta slopes. Ridge runs along Y, same tile colors as the cottages."""
    pitch = math.radians(34.0)
    left = Euler((0.0, -pitch, 0.0), "XYZ")
    right = Euler((0.0, pitch, 0.0), "XYZ")
    half = width * 0.26
    span = width * 0.58
    bucket.add_box((x - half, y, z), (span, depth, 0.04), mats["roof_dark"], left)
    bucket.add_box((x + half, y, z), (span, depth, 0.04), mats["roof_dark"], right)
    for index, color in enumerate(mats["roofs"]):
        shift = (index - 1.5) * depth * 0.22
        bucket.add_box((x - half, y + shift, z + 0.025), (span * 0.92, depth * 0.22, 0.028), color, left)
        bucket.add_box((x + half, y + shift, z + 0.025), (span * 0.92, depth * 0.22, 0.028), color, right)


def build_bench(root, coll, mats) -> None:
    wood = Bucket("bench_wood", coll)
    stone = Bucket("bench_stone", coll)
    for x in (-0.52, 0.52):
        stone.add_box((x, 0.0, 0.07), (0.28, 0.36, 0.14), mats["stones"][1])
        stone.add_box((x, 0.0, 0.18), (0.18, 0.22, 0.1), mats["stones_warm"][0])
        wood.add_box((x, -0.02, 0.36), (0.08, 0.08, 0.28), mats["wood_dark"])
        wood.add_box((x, -0.14, 0.62), (0.07, 0.07, 0.42), mats["wood_dark"])
    for index, y in enumerate((-0.1, 0.02, 0.14)):
        wood.add_box((0.0, y, 0.5), (1.28, 0.1, 0.055), mats["wood"] if index % 2 == 0 else mats["door"])
    wood.add_box((0.0, -0.14, 0.66), (1.16, 0.045, 0.07), mats["wood"])
    wood.add_box((0.0, -0.14, 0.8), (1.16, 0.045, 0.06), mats["wood_dark"])
    add_flower(wood, 0.72, 0.28, 0.0, mats["flower_p"], mats, 0.75)
    add_flower(wood, -0.7, 0.22, 0.0, mats["flower_w"], mats, 0.65)
    wood.finish(root)
    stone.finish(root)


def build_lantern(root, coll, mats) -> None:
    wood = Bucket("lantern_wood", coll)
    glass = Bucket("lantern_glass", coll)
    for i, (w, h, z) in enumerate(((0.46, 0.14, 0.07), (0.34, 0.1, 0.18), (0.26, 0.08, 0.26))):
        wood.add_box((0.0, 0.0, z), (w, w, h), mats["stones"][i % 3])
    wood.add_cylinder((0.0, 0.0, 1.15), 0.07, 0.05, 1.55, mats["wood_dark"], 8)
    wood.add_box((0.18, 0.0, 1.88), (0.42, 0.07, 0.06), mats["wood"])
    wood.add_box((0.36, 0.0, 1.78), (0.05, 0.05, 0.16), mats["wood_dark"])
    glass.add_box((0.36, 0.0, 1.58), (0.18, 0.14, 0.26), mats["glass"])
    wood.add_box((0.36, 0.0, 1.74), (0.26, 0.2, 0.04), mats["wood_dark"])
    wood.add_box((0.36, 0.0, 1.42), (0.26, 0.2, 0.04), mats["wood"])
    for sx, sy in ((0.1, 0.08), (-0.1, 0.08), (0.1, -0.08), (-0.1, -0.08)):
        wood.add_box((0.36 + sx, sy, 1.58), (0.025, 0.025, 0.26), mats["wood_dark"])
    wood.finish(root)
    glass.finish(root)


def build_planter(root, coll, mats) -> None:
    wood = Bucket("planter_wood", coll)
    veg = Bucket("planter_veg", coll)
    wood.add_box((0.0, 0.0, 0.16), (1.15, 0.42, 0.28), mats["wood"])
    wood.add_box((0.0, 0.0, 0.28), (1.02, 0.3, 0.08), mats["wood_dark"])
    wood.add_box((0.0, 0.0, 0.3), (0.96, 0.24, 0.06), mats["soil"])
    for x in (-0.48, 0.48):
        wood.add_box((x, 0.0, 0.06), (0.12, 0.16, 0.12), mats["stones_warm"][1])
    colors = (mats["flower_p"], mats["flower_r"], mats["flower_w"], mats["flower_y"], mats["flower_p"])
    for i, color in enumerate(colors):
        add_flower(veg, -0.4 + i * 0.2, 0.02 * (1 if i % 2 == 0 else -1), 0.32, color, mats, 0.85)
    veg.add_box((-0.15, 0.08, 0.38), (0.22, 0.16, 0.12), mats["leaves"][1])
    veg.add_box((0.22, -0.06, 0.4), (0.18, 0.14, 0.1), mats["leaves"][0])
    wood.finish(root)
    veg.finish(root)


def build_well(root, coll, mats) -> None:
    stone = Bucket("well_stone", coll)
    wood = Bucket("well_wood", coll)
    stone.add_cylinder((0.0, 0.0, 0.36), 0.46, 0.42, 0.72, mats["stones"][0], 8)
    stone.add_cylinder((0.0, 0.0, 0.62), 0.26, 0.26, 0.05, mats["water"], 8)
    for i in range(8):
        angle = math.tau * i / 8
        stone.add_box(
            (math.cos(angle) * 0.36, math.sin(angle) * 0.36, 0.76),
            (0.2, 0.14, 0.1),
            mats["stones_warm"][i % 2],
            Euler((0.0, 0.0, angle), "XYZ"),
        )
    for x in (-0.16, 0.16):
        wood.add_box((x, 0.0, 1.08), (0.07, 0.07, 0.62), mats["wood_dark"])
    wood.add_box((0.0, 0.0, 1.36), (0.5, 0.08, 0.07), mats["wood"])
    wood.add_box((0.0, 0.0, 1.12), (0.03, 0.03, 0.42), mats["wood_dark"])
    wood.add_box((0.08, 0.0, 0.88), (0.14, 0.12, 0.12), mats["wood"])
    gable_cap(wood, 0.0, 0.0, 1.5, 0.62, 0.42, mats)
    wood.add_box((0.42, 0.22, 0.14), (0.22, 0.18, 0.28), mats["leaves"][2])
    add_flower(wood, 0.34, 0.36, 0.0, mats["flower_r"], mats, 0.65)
    stone.finish(root)
    wood.finish(root)


def build_cart(root, coll, mats) -> None:
    wood = Bucket("cart_wood", coll)
    veg = Bucket("cart_veg", coll)
    wheel_rot = Euler((0.0, math.pi * 0.5, 0.0), "XYZ")
    for x in (-0.42, 0.42):
        wood.add_cylinder((x, 0.0, 0.28), 0.28, 0.28, 0.08, mats["wood_dark"], 8, wheel_rot)
        wood.add_cylinder((x, 0.0, 0.28), 0.1, 0.1, 0.1, mats["wood"], 8, wheel_rot)
    wood.add_box((0.0, 0.0, 0.28), (0.9, 0.06, 0.06), mats["metal"])
    wood.add_box((0.0, 0.02, 0.48), (0.95, 0.7, 0.06), mats["door"])
    for y in (-0.32, 0.32):
        wood.add_box((0.0, y, 0.62), (0.95, 0.05, 0.22), mats["wood"])
    wood.add_box((-0.42, 0.0, 0.62), (0.05, 0.64, 0.22), mats["wood_dark"])
    wood.add_box((0.15, 0.55, 0.42), (0.08, 0.55, 0.08), mats["wood_dark"])
    wood.add_box((0.22, 0.05, 0.66), (0.28, 0.28, 0.28), mats["wood"])
    wood.add_box((-0.18, -0.08, 0.6), (0.22, 0.22, 0.16), mats["brick"])
    add_flower(veg, -0.05, 0.12, 0.7, mats["flower_p"], mats, 0.7)
    add_flower(veg, 0.28, -0.12, 0.78, mats["flower_w"], mats, 0.6)
    wood.finish(root)
    veg.finish(root)


def build_sign(root, coll, mats) -> None:
    wood = Bucket("sign_wood", coll)
    wood.add_box((0.0, 0.0, 0.08), (0.36, 0.28, 0.16), mats["stones"][2])
    wood.add_box((0.0, 0.0, 0.95), (0.08, 0.08, 1.6), mats["wood_dark"])
    wood.add_box((0.22, 0.0, 1.45), (0.08, 0.06, 0.5), mats["wood"])
    wood.add_box((0.48, 0.0, 1.42), (0.55, 0.05, 0.4), mats["wood_dark"])
    for face in (0.05, -0.05):
        wood.add_box((0.48, face, 1.42), (0.44, 0.03, 0.28), mats["plaster"])
        wood.add_box((0.4, face + (0.03 if face > 0 else -0.03), 1.36), (0.12, 0.02, 0.1), mats["wood"])
        wood.add_box((0.54, face + (0.03 if face > 0 else -0.03), 1.4), (0.1, 0.02, 0.07), mats["roof_dark"])
    gable_cap(wood, 0.48, 0.0, 1.66, 0.5, 0.22, mats)
    add_flower(wood, -0.16, 0.18, 0.0, mats["flower_y"], mats, 0.65)
    wood.finish(root)


def make_materials() -> dict:
    mats = {
        "plaster": make_material("VN_Deco_plaster", "#f0e2c8"),
        "wood": make_material("VN_Deco_wood", "#6e4a30"),
        "wood_dark": make_material("VN_Deco_wood_dark", "#3f2818"),
        "door": make_material("VN_Deco_door", "#6a4328"),
        "soil": make_material("VN_Deco_soil", "#6b4a32"),
        "glass": make_material("VN_Deco_glass", "#ffc56b", 0.32, "#ffb03a", 1.15),
        "metal": make_material("VN_Deco_metal", "#3a3532", 0.72),
        "brick": make_material("VN_Deco_brick", "#a34432"),
        "roof_dark": make_material("VN_Deco_roof_under", "#8d3b2a"),
        "water": make_material("VN_Deco_water", "#6aa8b0", 0.55),
        "flower_p": make_material("VN_Deco_flower_pink", "#e58cb0"),
        "flower_r": make_material("VN_Deco_flower_red", "#d24b4b"),
        "flower_w": make_material("VN_Deco_flower_white", "#f4f1ea"),
        "flower_y": make_material("VN_Deco_flower_yellow", "#f0c14a"),
        "leaf_stem": make_material("VN_Deco_leaf_stem", "#2f6e3c"),
    }
    mats["stones"] = [make_material(f"VN_Deco_stone_{i}", h) for i, h in enumerate(("#8e8b86", "#a39e96", "#6f6d68", "#b7b2a8"))]
    mats["stones_warm"] = [make_material(f"VN_Deco_stone_warm_{i}", h) for i, h in enumerate(("#c4b49a", "#d9cbb3"))]
    mats["roofs"] = [make_material(f"VN_Deco_roof_{i}", h, 0.9) for i, h in enumerate(("#c4513a", "#d4654a", "#b84432", "#e07a55"))]
    mats["leaves"] = [make_material(f"VN_Deco_leaf_{i}", h) for i, h in enumerate(("#3f8f4e", "#5aaa62", "#2f6e3c", "#6fba78"))]
    return mats


def new_root(name: str, coll: bpy.types.Collection) -> bpy.types.Object:
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.3
    link_object(root, coll)
    return root


def world_bounds(root: bpy.types.Object):
    xs, ys, zs = [], [], []
    for obj in root.children_recursive:
        if obj.type != "MESH":
            continue
        for vert in obj.data.vertices:
            w = obj.matrix_world @ vert.co
            xs.append(w.x)
            ys.append(w.y)
            zs.append(w.z)
    if not xs:
        raise RuntimeError(f"{root.name} has no vertices")
    return min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)


def validate(root: bpy.types.Object) -> None:
    x0, x1, y0, y1, z0, z1 = world_bounds(root)
    print(f"BOUNDS {root.name} x[{x0:.2f},{x1:.2f}] y[{y0:.2f},{y1:.2f}] z[{z0:.2f},{z1:.2f}]")
    if z0 < -0.03:
        raise RuntimeError(f"{root.name} sinks below ground ({z0:.3f})")
    if max(abs(x0), abs(x1), abs(y0), abs(y1)) > FOOTPRINT:
        raise RuntimeError(f"{root.name} exceeds the 1x1 cell")


def export_glb(root: bpy.types.Object, path: Path) -> None:
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
    if "export_cameras" in props:
        kwargs["export_cameras"] = False
    if "export_lights" in props:
        kwargs["export_lights"] = False
    bpy.ops.export_scene.gltf(**kwargs)
    if not path.is_file() or path.stat().st_size < 512:
        raise RuntimeError(f"GLB export failed: {path}")
    print(f"EXPORTED {path.name} ({path.stat().st_size} bytes)")


def look_at(obj: bpy.types.Object, target) -> None:
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def setup_render_world() -> None:
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    world = scene.world or bpy.data.worlds.new("VN_World")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg is None:
        bg = world.node_tree.nodes.new("ShaderNodeBackground")
        out = world.node_tree.nodes.get("World Output") or world.node_tree.nodes.new("ShaderNodeOutputWorld")
        world.node_tree.links.new(bg.outputs["Background"], out.inputs["Surface"])
    bg.inputs[0].default_value = (0.53, 0.72, 0.90, 1.0)
    bg.inputs[1].default_value = 0.85
    try:
        scene.view_settings.view_transform = "Standard"
    except Exception:
        pass
    scene.render.resolution_x = 900
    scene.render.resolution_y = 700
    scene.render.image_settings.file_format = "PNG"
    for candidate in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "CYCLES"):
        try:
            scene.render.engine = candidate
            break
        except Exception:
            continue
    eevee = getattr(scene, "eevee", None)
    if eevee is not None and hasattr(eevee, "taa_render_samples"):
        eevee.taa_render_samples = 16


def add_sun(coll: bpy.types.Collection) -> None:
    bpy.ops.mesh.primitive_plane_add(size=28.0, location=(8.0, 0.0, -0.02))
    ground = bpy.context.active_object
    ground.data.materials.append(make_material("VN_Deco_ground", "#7ea36a", 0.95))
    link_object(ground, coll)
    bpy.ops.object.light_add(type="SUN", location=(6.0, 8.0, 10.0))
    sun = bpy.context.active_object
    sun.data.energy = 3.2
    sun.data.color = (1.0, 0.94, 0.84)
    look_at(sun, (0.0, 0.0, 0.6))
    link_object(sun, coll)


def hide_tree(root: bpy.types.Object, hidden: bool) -> None:
    root.hide_render = hidden
    for child in root.children_recursive:
        child.hide_render = hidden


def render_iso(root: bpy.types.Object, stem: str, ortho: float, focus_z: float) -> None:
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.camera_add(location=(2.2, 2.6, 2.0))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ortho
    cam.data.clip_end = 40.0
    look_at(cam, (0.0, 0.0, focus_z))
    scene = bpy.context.scene
    scene.camera = cam
    scene.render.filepath = str(PREVIEW_DIR / f"{stem}_iso.png")
    bpy.ops.render.render(write_still=True)
    print(f"RENDERED {scene.render.filepath}")
    bpy.data.objects.remove(cam, do_unlink=True)


def main() -> None:
    clear_scene()
    setup_render_world()
    render_coll = ensure_collection("RenderOnly")
    add_sun(render_coll)
    mats = make_materials()
    specs = (
        ("vn_deco_rustic_bench", build_bench, 2.6, 0.45),
        ("vn_deco_cottage_lantern", build_lantern, 3.4, 1.0),
        ("vn_deco_cottage_planter", build_planter, 2.4, 0.35),
        ("vn_deco_stone_well", build_well, 3.2, 0.8),
        ("vn_deco_wood_cart", build_cart, 2.8, 0.5),
        ("vn_deco_village_sign", build_sign, 3.2, 0.9),
    )
    roots = []
    for index, (name, builder, ortho, focus_z) in enumerate(specs):
        for prev in roots:
            hide_tree(prev, True)
        coll = ensure_collection(name)
        root = new_root(name, coll)
        builder(root, coll, mats)
        validate(root)
        export_glb(root, MODEL_DIR / f"{name}.glb")
        hide_tree(root, False)
        render_iso(root, name, ortho, focus_z)
        root.location.x = (index + 1) * 3.2
        bpy.context.view_layer.update()
        roots.append(root)

    for root in roots:
        hide_tree(root, False)
    center = (len(roots) + 1) * 1.6
    bpy.ops.object.camera_add(location=(center + 6.0, -8.0, 5.0))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 16.0
    look_at(cam, (center, 0.0, 0.6))
    bpy.context.scene.camera = cam
    bpy.context.scene.render.filepath = str(PREVIEW_DIR / "vn_village_decorations_lineup.png")
    bpy.ops.render.render(write_still=True)
    try:
        bpy.context.preferences.filepaths.save_version = 0
    except Exception:
        pass
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    keep_import = Path(str(BLEND_PATH) + ".import")
    if not keep_import.exists():
        keep_import.write_text("[remap]\n\nimporter=\"keep\"\n\n[deps]\n\n[params]\n", encoding="utf-8")
    print("SUCCESS")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        sys.exit(1)

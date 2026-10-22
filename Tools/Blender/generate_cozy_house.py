# Vila Nova — procedural cozy house for Blender (bpy)
# Run: blender --background --python generate_cozy_house.py
# Axes: Z up, origin at footprint center on ground (XY plane). Front facade = +Y.

from __future__ import annotations

import math
import os
from pathlib import Path

import bpy
from mathutils import Vector

# ---------------------------------------------------------------------------
# Parameters
# ---------------------------------------------------------------------------

HOUSE_WIDTH_X = 3.4
HOUSE_DEPTH_Y = 3.3
WALL_HEIGHT = 2.05
FOUNDATION_HEIGHT = 0.16
PLINTH_HEIGHT = 0.06

WALL_BODY_W = 3.15
WALL_BODY_D = 2.95

ROOF_PITCH_DEG = 36.0
ROOF_OVERHANG = 0.0
RIDGE_EXTRA_HEIGHT = 0.56

TIMBER_W = 0.18
TIMBER_T = 0.12

DOOR_WIDTH = 0.78
DOOR_HEIGHT = 1.55
DOOR_FRAME_W = 1.05
DOOR_FRAME_H = 1.75

WINDOW_SIZE = 0.72
GLASS_SIZE = 0.52

CHIMNEY_ENABLED = True
FLOWER_BOX_ENABLED = True
STEPPING_STONES_ENABLED = True

# VillagePalette (sRGB hex)
COL_WALL = "#f3e8d4"
COL_TRIM = "#dcc9a8"
COL_WOOD = "#9c6b43"
COL_WOOD_DARK = "#6f4a2e"
COL_ROOF = "#c06840"
COL_ROOF_SHADOW = "#9a4f32"
COL_DOOR = "#7a4f2f"
COL_FRAME = "#f0e2cc"
COL_GLASS = "#8eb5c9"
COL_STONE = "#b8a898"
COL_CHIMNEY = "#a8654f"
COL_FLOWER_GREEN = "#6f9b5a"
COL_FLOWER_ACCENT = "#e8927c"

ROUGHNESS_MATTE = 0.92
ROUGHNESS_GLASS = 0.35

COLLECTION_ROOT = "VN_CozyHouse"
EXPORT_FILENAME = "vn_building_cozy_house.glb"

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parent.parent
EXPORT_PATH = PROJECT_ROOT / "Assets" / "Models" / EXPORT_FILENAME


# ---------------------------------------------------------------------------
# Utilities
# ---------------------------------------------------------------------------

def hex_to_rgba(hex_color: str, alpha: float = 1.0) -> tuple[float, float, float, float]:
    h = hex_color.lstrip("#")
    r = int(h[0:2], 16) / 255.0
    g = int(h[2:4], 16) / 255.0
    b = int(h[4:6], 16) / 255.0
    return (r, g, b, alpha)


def ensure_collection(name: str, parent: bpy.types.Collection | None = None) -> bpy.types.Collection:
    coll = bpy.data.collections.get(name)
    if coll is None:
        coll = bpy.data.collections.new(name)
        if parent is None:
            bpy.context.scene.collection.children.link(coll)
        else:
            parent.children.link(coll)
    return coll


def link_object(obj: bpy.types.Object, coll: bpy.types.Collection) -> None:
    if obj.name not in coll.objects:
        coll.objects.link(obj)
    for c in obj.users_collection:
        c.objects.unlink(obj)
    coll.objects.link(obj)


def get_or_create_material(
    name: str,
    hex_color: str,
    roughness: float = ROUGHNESS_MATTE,
    metallic: float = 0.0,
) -> bpy.types.Material:
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name=name)
        mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = hex_to_rgba(hex_color)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    spec = bsdf.inputs.get("Specular IOR Level") or bsdf.inputs.get("Specular")
    if spec is not None:
        spec.default_value = 0.15 if roughness < 0.5 else 0.05
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return mat


def assign_material(obj: bpy.types.Object, mat: bpy.types.Material) -> None:
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)


def add_box(
    name: str,
    center: Vector,
    size: Vector,
    material: bpy.types.Material,
    coll: bpy.types.Collection,
    rotation_euler: Vector | None = None,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.0))
    obj = bpy.context.active_object
    obj.name = name
    obj.dimensions = size
    obj.location = center
    if rotation_euler is not None:
        obj.rotation_euler = rotation_euler
    assign_material(obj, material)
    link_object(obj, coll)
    return obj


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for mat in bpy.data.materials:
        if mat.users == 0:
            bpy.data.materials.remove(mat)


def setup_scene_units() -> None:
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"


def create_root_empty(coll_root: bpy.types.Collection) -> bpy.types.Object:
    empty = bpy.data.objects.new("vn_building_cozy_house", None)
    empty.empty_display_size = 0.4
    link_object(empty, coll_root)
    return empty


def parent_keep_transform(child: bpy.types.Object, parent: bpy.types.Object) -> None:
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()


# ---------------------------------------------------------------------------
# House parts
# ---------------------------------------------------------------------------

def build_foundation(mats: dict, coll: bpy.types.Collection, root: bpy.types.Object) -> list[bpy.types.Object]:
    objs = []
    objs.append(
        add_box(
            "foundation_stone",
            Vector((0.0, 0.0, FOUNDATION_HEIGHT * 0.5)),
            Vector((HOUSE_WIDTH_X, HOUSE_DEPTH_Y, FOUNDATION_HEIGHT)),
            mats["stone"],
            coll,
        )
    )
    plinth_z = FOUNDATION_HEIGHT + PLINTH_HEIGHT * 0.5
    objs.append(
        add_box(
            "plinth_wall",
            Vector((0.0, 0.0, plinth_z)),
            Vector((HOUSE_WIDTH_X - 0.15, HOUSE_DEPTH_Y - 0.25, PLINTH_HEIGHT)),
            mats["wall"],
            coll,
        )
    )
    for o in objs:
        parent_keep_transform(o, root)
    return objs


def build_walls(mats: dict, coll: bpy.types.Collection, root: bpy.types.Object) -> list[bpy.types.Object]:
    objs = []
    wall_z = FOUNDATION_HEIGHT + PLINTH_HEIGHT + WALL_HEIGHT * 0.5
    objs.append(
        add_box(
            "walls_main",
            Vector((0.0, 0.0, wall_z)),
            Vector((WALL_BODY_W, WALL_BODY_D, WALL_HEIGHT)),
            mats["wall"],
            coll,
        )
    )

    half_w = WALL_BODY_W * 0.5 - TIMBER_W * 0.5
    half_d = WALL_BODY_D * 0.5 - TIMBER_W * 0.5
    trim_specs = [
        ("trim_corner_west", Vector((-half_w, 0.0, wall_z)), Vector((TIMBER_W, WALL_BODY_D, WALL_HEIGHT))),
        ("trim_corner_east", Vector((half_w, 0.0, wall_z)), Vector((TIMBER_W, WALL_BODY_D, WALL_HEIGHT))),
        ("trim_corner_north", Vector((0.0, -half_d, wall_z)), Vector((WALL_BODY_W, TIMBER_W, WALL_HEIGHT))),
        ("trim_corner_south", Vector((0.0, half_d, wall_z)), Vector((WALL_BODY_W, TIMBER_W, WALL_HEIGHT))),
    ]
    for name, loc, dim in trim_specs:
        objs.append(add_box(name, loc, dim, mats["trim"], coll))

    front_y = half_d + TIMBER_T * 0.5
    beam_z_low = FOUNDATION_HEIGHT + PLINTH_HEIGHT + 0.75
    beam_z_high = FOUNDATION_HEIGHT + PLINTH_HEIGHT + 1.65
    for i, z in enumerate((beam_z_low, beam_z_high)):
        objs.append(
            add_box(
                f"beam_front_{i}",
                Vector((0.0, front_y, z)),
                Vector((WALL_BODY_W, TIMBER_T, 0.12)),
                mats["wood"],
                coll,
            )
        )

    for o in objs:
        parent_keep_transform(o, root)
    return objs


def build_door(mats: dict, coll: bpy.types.Collection, root: bpy.types.Object) -> list[bpy.types.Object]:
    objs = []
    half_d = WALL_BODY_D * 0.5
    face_y = half_d + 0.04
    door_center_z = FOUNDATION_HEIGHT + PLINTH_HEIGHT + 0.92

    objs.append(
        add_box(
            "door_frame",
            Vector((0.0, face_y, door_center_z)),
            Vector((DOOR_FRAME_W, 0.1, DOOR_FRAME_H)),
            mats["frame"],
            coll,
        )
    )
    objs.append(
        add_box(
            "door_panel",
            Vector((0.0, face_y + 0.03, door_center_z - 0.03)),
            Vector((DOOR_WIDTH, 0.08, DOOR_HEIGHT)),
            mats["door"],
            coll,
        )
    )
    objs.append(
        add_box(
            "door_knob",
            Vector((0.22, face_y + 0.08, door_center_z)),
            Vector((0.07, 0.04, 0.07)),
            mats["wood_dark"],
            coll,
        )
    )
    objs.append(
        add_box(
            "door_step",
            Vector((0.0, face_y + 0.02, FOUNDATION_HEIGHT + PLINTH_HEIGHT + 0.28)),
            Vector((0.95, 0.12, 0.1)),
            mats["stone"],
            coll,
        )
    )

    for o in objs:
        parent_keep_transform(o, root)
    return objs


def build_window(
    name: str,
    center: Vector,
    face_axis: str,
    mats: dict,
    coll: bpy.types.Collection,
    root: bpy.types.Object,
) -> list[bpy.types.Object]:
    objs = []
    offset = 0.04 if face_axis == "y" else 0.04
    loc = center.copy()
    size_frame = Vector((WINDOW_SIZE, 0.1, WINDOW_SIZE))
    size_glass = Vector((GLASS_SIZE, 0.06, GLASS_SIZE))

    if face_axis == "y":
        loc.y += offset
    else:
        loc.x += offset
        size_frame = Vector((0.1, WINDOW_SIZE, WINDOW_SIZE))
        size_glass = Vector((0.06, GLASS_SIZE, GLASS_SIZE))

    objs.append(add_box(f"{name}_frame", loc, size_frame, mats["frame"], coll))

    glass_loc = loc.copy()
    if face_axis == "y":
        glass_loc.y += 0.03
    else:
        glass_loc.x += 0.03
    objs.append(add_box(f"{name}_glass", glass_loc, size_glass, mats["glass"], coll))

    sill_loc = loc.copy()
    sill_loc.z -= 0.42
    sill_size = Vector((0.78, 0.14, 0.08)) if face_axis == "y" else Vector((0.14, 0.78, 0.08))
    if face_axis == "x":
        sill_loc.x += 0.02
    else:
        sill_loc.y += 0.02
    objs.append(add_box(f"{name}_sill", sill_loc, sill_size, mats["wood"], coll))

    for o in objs:
        parent_keep_transform(o, root)
    return objs


def build_windows(mats: dict, coll: bpy.types.Collection, root: bpy.types.Object) -> list[bpy.types.Object]:
    objs = []
    base_z = FOUNDATION_HEIGHT + PLINTH_HEIGHT + 1.45
    half_d = WALL_BODY_D * 0.5
    half_w = WALL_BODY_W * 0.5
    face_y = half_d + 0.02
    face_x = half_w - 0.02

    for side, x in (("L", -0.95), ("R", 0.95)):
        objs.extend(build_window(f"window_front_{side}", Vector((x, face_y, base_z)), "y", mats, coll, root))

    objs.extend(
        build_window(
            "window_side_east",
            Vector((face_x, -0.35, base_z)),
            "x",
            mats,
            coll,
            root,
        )
    )
    return objs


def build_roof(mats: dict, coll: bpy.types.Collection, root: bpy.types.Object) -> list[bpy.types.Object]:
    objs = []
    wall_top = FOUNDATION_HEIGHT + PLINTH_HEIGHT + WALL_HEIGHT
    eave_z = wall_top + 0.07
    objs.append(
        add_box(
            "roof_eave_ring",
            Vector((0.0, 0.0, eave_z)),
            Vector((HOUSE_WIDTH_X + ROOF_OVERHANG, HOUSE_DEPTH_Y + ROOF_OVERHANG, 0.12)),
            mats["roof"],
            coll,
        )
    )

    pitch = math.radians(ROOF_PITCH_DEG)
    half_depth = (HOUSE_DEPTH_Y + ROOF_OVERHANG) * 0.5
    slope_len = half_depth / math.cos(pitch) * 0.98
    ridge_z = eave_z + math.sin(pitch) * half_depth + 0.18

    north_center_y = -half_depth * 0.45
    south_center_y = half_depth * 0.45
    roof_thickness = 0.14

    objs.append(
        add_box(
            "roof_slope_north",
            Vector((0.0, north_center_y, eave_z + math.sin(pitch) * half_depth * 0.5)),
            Vector((HOUSE_WIDTH_X + ROOF_OVERHANG * 2, slope_len, roof_thickness)),
            mats["roof_shadow"],
            coll,
            rotation_euler=Vector((pitch, 0.0, 0.0)),
        )
    )
    objs.append(
        add_box(
            "roof_slope_south",
            Vector((0.0, south_center_y, eave_z + math.sin(pitch) * half_depth * 0.5)),
            Vector((HOUSE_WIDTH_X + ROOF_OVERHANG * 2, slope_len, roof_thickness)),
            mats["roof"],
            coll,
            rotation_euler=Vector((-pitch, 0.0, 0.0)),
        )
    )
    objs.append(
        add_box(
            "roof_ridge_cap",
            Vector((0.0, 0.0, ridge_z)),
            Vector((HOUSE_WIDTH_X + ROOF_OVERHANG * 2.2, 0.22, 0.1)),
            mats["roof"],
            coll,
        )
    )

    for o in objs:
        parent_keep_transform(o, root)
    return objs


def build_chimney(mats: dict, coll: bpy.types.Collection, root: bpy.types.Object) -> list[bpy.types.Object]:
    if not CHIMNEY_ENABLED:
        return []
    objs = []
    base_z = FOUNDATION_HEIGHT + PLINTH_HEIGHT + WALL_HEIGHT + 0.35
    objs.append(
        add_box(
            "chimney_stack",
            Vector((-1.05, -0.55, base_z)),
            Vector((0.38, 0.38, 1.05)),
            mats["chimney"],
            coll,
        )
    )
    objs.append(
        add_box(
            "chimney_cap",
            Vector((-1.05, -0.55, base_z + 0.58)),
            Vector((0.46, 0.46, 0.1)),
            mats["stone"],
            coll,
        )
    )
    for o in objs:
        parent_keep_transform(o, root)
    return objs


def build_decor(mats: dict, coll: bpy.types.Collection, root: bpy.types.Object) -> list[bpy.types.Object]:
    objs = []
    if FLOWER_BOX_ENABLED:
        half_d = WALL_BODY_D * 0.5
        fy = half_d + 0.06
        fz = FOUNDATION_HEIGHT + PLINTH_HEIGHT + 0.95
        objs.append(add_box("flower_box", Vector((-0.95, fy, fz)), Vector((0.62, 0.18, 0.14)), mats["wood"], coll))
        objs.append(
            add_box("flower_soil", Vector((-0.95, fy + 0.02, fz + 0.07)), Vector((0.52, 0.12, 0.08)), mats["flower_green"], coll)
        )
        objs.append(
            add_box("flower_a", Vector((-0.82, fy + 0.05, fz + 0.11)), Vector((0.08, 0.06, 0.08)), mats["flower_accent"], coll)
        )
        objs.append(
            add_box("flower_b", Vector((-1.08, fy + 0.05, fz + 0.11)), Vector((0.08, 0.06, 0.08)), mats["flower_accent"], coll)
        )

    if STEPPING_STONES_ENABLED:
        objs.append(add_box("stone_a", Vector((1.35, 1.35, 0.025)), Vector((0.35, 0.35, 0.05)), mats["stone"], coll))
        objs.append(add_box("stone_b", Vector((1.55, 1.15, 0.025)), Vector((0.28, 0.28, 0.05)), mats["stone"], coll))

    for o in objs:
        parent_keep_transform(o, root)
    return objs


def validate_footprint(root: bpy.types.Object) -> None:
    """Warn if mesh AABB exceeds horizontal footprint (Z-up)."""
    max_x = HOUSE_WIDTH_X * 0.5 + 0.01
    max_y = HOUSE_DEPTH_Y * 0.5 + 0.01
    for obj in root.children_recursive:
        if obj.type != "MESH":
            continue
        for corner in obj.bound_box:
            world = obj.matrix_world @ Vector(corner)
            if abs(world.x) > max_x or abs(world.y) > max_y:
                print(
                    f"WARNING: {obj.name} extends outside footprint: "
                    f"x={world.x:.3f} y={world.y:.3f} (limit ±{max_x:.2f}/±{max_y:.2f})"
                )


def export_glb(root: bpy.types.Object, filepath: Path) -> None:
    filepath.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(filepath),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_materials="EXPORT",
        export_texcoords=True,
        export_normals=True,
    )


def main() -> None:
    clear_scene()
    setup_scene_units()

    coll_root = ensure_collection(COLLECTION_ROOT)
    sub = {
        "foundation": ensure_collection(f"{COLLECTION_ROOT}.Foundation", coll_root),
        "walls": ensure_collection(f"{COLLECTION_ROOT}.Walls", coll_root),
        "roof": ensure_collection(f"{COLLECTION_ROOT}.Roof", coll_root),
        "door": ensure_collection(f"{COLLECTION_ROOT}.Door", coll_root),
        "windows": ensure_collection(f"{COLLECTION_ROOT}.Windows", coll_root),
        "chimney": ensure_collection(f"{COLLECTION_ROOT}.Chimney", coll_root),
        "decor": ensure_collection(f"{COLLECTION_ROOT}.Decor", coll_root),
    }

    mats = {
        "wall": get_or_create_material("VN_WallCream", COL_WALL),
        "trim": get_or_create_material("VN_TrimPaint", COL_TRIM),
        "wood": get_or_create_material("VN_WoodWarm", COL_WOOD),
        "wood_dark": get_or_create_material("VN_WoodDark", COL_WOOD_DARK),
        "roof": get_or_create_material("VN_RoofTerracotta", COL_ROOF),
        "roof_shadow": get_or_create_material("VN_RoofShadow", COL_ROOF_SHADOW),
        "door": get_or_create_material("VN_DoorWood", COL_DOOR),
        "frame": get_or_create_material("VN_WindowFrame", COL_FRAME),
        "glass": get_or_create_material("VN_WindowGlass", COL_GLASS, ROUGHNESS_GLASS),
        "stone": get_or_create_material("VN_StoneFoundation", COL_STONE),
        "chimney": get_or_create_material("VN_ChimneyBrick", COL_CHIMNEY),
        "flower_green": get_or_create_material("VN_FlowerGreen", COL_FLOWER_GREEN),
        "flower_accent": get_or_create_material("VN_FlowerAccent", COL_FLOWER_ACCENT),
    }

    root = create_root_empty(coll_root)

    build_foundation(mats, sub["foundation"], root)
    build_walls(mats, sub["walls"], root)
    build_door(mats, sub["door"], root)
    build_windows(mats, sub["windows"], root)
    build_roof(mats, sub["roof"], root)
    build_chimney(mats, sub["chimney"], root)
    build_decor(mats, sub["decor"], root)

    validate_footprint(root)

    print(f"Exporting to: {EXPORT_PATH}")
    export_glb(root, EXPORT_PATH)

    if EXPORT_PATH.is_file() and EXPORT_PATH.stat().st_size > 1024:
        print(f"SUCCESS: GLB written ({EXPORT_PATH.stat().st_size} bytes)")
    else:
        raise RuntimeError(f"Export failed or file too small: {EXPORT_PATH}")


if __name__ == "__main__":
    main()

# Vila Nova — hero house (cozy 3D pixel / voxel blocks)
# Pixel look = chunky boxes + solid palette colors (no broken atlas UVs on cubes).
# blender --background --python Tools/Blender/generate_hero_house.py

from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Vector

# ---------------------------------------------------------------------------
# Parameters
# ---------------------------------------------------------------------------

VOX = 0.1
HOUSE_WIDTH_X = 3.6
HOUSE_DEPTH_Y = 3.4
FOOTPRINT_LIMIT_X = 4.0
FOOTPRINT_LIMIT_Y = 4.0

FOUNDATION_H = 0.22
PLINTH_H = 0.08
WALL_H = 2.15
WALL_W = 3.35
WALL_D = 3.05

ROOF_PITCH_DEG = 42.0
TIMBER_W = 0.2
TIMBER_T = 0.11

EXPORT_FILENAME = "vn_building_hero_house.glb"
ATLAS_FILENAME = "vn_hero_house_atlas.png"
COLLECTION_ROOT = "VN_HeroHouse"
ATLAS_SIZE = 128

PAL = {
    "wall": (0.953, 0.910, 0.831),
    "wall_dark": (0.898, 0.831, 0.737),
    "wood": (0.612, 0.420, 0.263),
    "wood_dark": (0.435, 0.290, 0.180),
    "roof": (0.753, 0.408, 0.251),
    "roof_dark": (0.604, 0.310, 0.196),
    "stone": (0.722, 0.659, 0.596),
    "stone_dark": (0.580, 0.525, 0.470),
    "frame": (0.941, 0.886, 0.800),
    "glass": (0.557, 0.710, 0.788),
    "glass_glow": (0.98, 0.82, 0.55),
    "door": (0.478, 0.310, 0.184),
    "chimney": (0.659, 0.396, 0.310),
    "leaf": (0.435, 0.608, 0.353),
    "flower": (0.910, 0.573, 0.486),
    "trim": (0.867, 0.788, 0.659),
}

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parent.parent
EXPORT_PATH = PROJECT_ROOT / "Assets" / "Models" / EXPORT_FILENAME
ATLAS_PATH = PROJECT_ROOT / "Assets" / "Textures" / ATLAS_FILENAME

# ---------------------------------------------------------------------------
# Atlas (reference PNG only — not applied to cube UVs)
# ---------------------------------------------------------------------------


def save_reference_atlas_png() -> None:
    """Palette reference for artists; gameplay mesh uses solid materials."""
    ATLAS_PATH.parent.mkdir(parents=True, exist_ok=True)
    buf = [0.0] * ATLAS_SIZE * ATLAS_SIZE * 4
    swatches = list(PAL.values())
    step = ATLAS_SIZE // max(len(swatches), 1)
    for i, rgb in enumerate(swatches):
        x0 = (i * step) % ATLAS_SIZE
        y0 = ((i * step) // ATLAS_SIZE) * 8
        for dy in range(8):
            for dx in range(min(step, 24)):
                x, y = x0 + dx, y0 + dy
                if x < ATLAS_SIZE and y < ATLAS_SIZE:
                    j = (y * ATLAS_SIZE + x) * 4
                    buf[j : j + 4] = [rgb[0], rgb[1], rgb[2], 1.0]
    img = bpy.data.images.new("VN_HeroPaletteRef", ATLAS_SIZE, ATLAS_SIZE, alpha=True)
    img.pixels = buf
    img.filepath_raw = str(ATLAS_PATH)
    img.file_format = "PNG"
    img.save()


# ---------------------------------------------------------------------------
# Scene helpers
# ---------------------------------------------------------------------------


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def ensure_collection(name: str, parent: bpy.types.Collection | None = None) -> bpy.types.Collection:
    coll = bpy.data.collections.get(name)
    if coll is None:
        coll = bpy.data.collections.new(name)
        (bpy.context.scene.collection if parent is None else parent).children.link(coll)
    return coll


def link_object(obj: bpy.types.Object, coll: bpy.types.Collection) -> None:
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    coll.objects.link(obj)


def create_matte(name: str, rgb: tuple[float, float, float], roughness: float = 0.94) -> bpy.types.Material:
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name=name)
        mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = 0.0
    spec = bsdf.inputs.get("Specular IOR Level") or bsdf.inputs.get("Specular")
    if spec:
        spec.default_value = 0.04 if roughness > 0.5 else 0.12
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def add_box(
    name: str,
    center: Vector,
    size: Vector,
    material: bpy.types.Material,
    coll: bpy.types.Collection,
    rotation: Vector | None = None,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.name = name
    obj.dimensions = size
    obj.location = center
    if rotation:
        obj.rotation_euler = rotation
    if obj.data.materials:
        obj.data.materials[0] = material
    else:
        obj.data.materials.append(material)
    link_object(obj, coll)
    return obj


def parent_all(root: bpy.types.Object, objs: list[bpy.types.Object]) -> None:
    for obj in objs:
        obj.parent = root
        obj.matrix_parent_inverse = root.matrix_world.inverted()


# ---------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------


def build_house(mats: dict, coll_root: bpy.types.Collection) -> bpy.types.Object:
    sub = {
        "base": ensure_collection(f"{COLLECTION_ROOT}.Base", coll_root),
        "walls": ensure_collection(f"{COLLECTION_ROOT}.Walls", coll_root),
        "roof": ensure_collection(f"{COLLECTION_ROOT}.Roof", coll_root),
        "details": ensure_collection(f"{COLLECTION_ROOT}.Details", coll_root),
    }
    root = bpy.data.objects.new("vn_building_hero_house", None)
    link_object(root, coll_root)
    made: list[bpy.types.Object] = []

    z0 = FOUNDATION_H * 0.5
    made.append(add_box("foundation", Vector((0, 0, z0)), Vector((HOUSE_WIDTH_X, HOUSE_DEPTH_Y, FOUNDATION_H)), mats["stone"], sub["base"]))

    plinth_z = FOUNDATION_H + PLINTH_H * 0.5
    made.append(
        add_box(
            "plinth",
            Vector((0, 0, plinth_z)),
            Vector((HOUSE_WIDTH_X - 0.12, HOUSE_DEPTH_Y - 0.18, PLINTH_H)),
            mats["wall"],
            sub["base"],
        )
    )

    wall_z = FOUNDATION_H + PLINTH_H + WALL_H * 0.5
    made.append(add_box("walls_core", Vector((0, 0, wall_z)), Vector((WALL_W, WALL_D, WALL_H)), mats["wall"], sub["walls"]))

    half_w = WALL_W * 0.5 - TIMBER_W * 0.5
    half_d = WALL_D * 0.5 - TIMBER_W * 0.5
    for name, loc, dim in (
        ("timber_west", Vector((-half_w, 0, wall_z)), Vector((TIMBER_W, WALL_D, WALL_H))),
        ("timber_east", Vector((half_w, 0, wall_z)), Vector((TIMBER_W, WALL_D, WALL_H))),
        ("timber_north", Vector((0, -half_d, wall_z)), Vector((WALL_W, TIMBER_W, WALL_H))),
        ("timber_south", Vector((0, half_d, wall_z)), Vector((WALL_W, TIMBER_W, WALL_H))),
    ):
        made.append(add_box(name, loc, dim, mats["wood"], sub["walls"]))

    front_y = half_d + TIMBER_T * 0.5
    for i, z in enumerate((0.78, 1.72)):
        made.append(
            add_box(
                f"beam_front_{i}",
                Vector((0, front_y, FOUNDATION_H + PLINTH_H + z)),
                Vector((WALL_W, TIMBER_T, 0.13)),
                mats["wood"],
                sub["walls"],
            )
        )

    # Pixel stone band on foundation (voxel strip)
    for i, ox in enumerate(range(-16, 17, 2)):
        made.append(
            add_box(
                f"foundation_voxel_{i}",
                Vector((ox * VOX * 0.5, half_d + 0.02, FOUNDATION_H * 0.55)),
                Vector((VOX * 0.95, VOX * 0.4, VOX * 0.95)),
                mats["stone"] if i % 2 == 0 else mats["stone_dark"],
                sub["base"],
            )
        )

    # Front plaster pixels (left/right of door)
    face_panel_y = half_d + VOX * 0.2
    base_z = FOUNDATION_H + PLINTH_H + VOX * 2
    for panel_x, tag in ((-1.1, "L"), (1.1, "R")):
        for row in range(10):
            for col in range(4):
                mat = mats["wall"] if (row + col) % 2 == 0 else mats["wall_dark"]
                made.append(
                    add_box(
                        f"facade_pixel_{tag}_{row}_{col}",
                        Vector((panel_x + col * VOX, face_panel_y, base_z + row * VOX)),
                        Vector((VOX * 0.9, VOX * 0.32, VOX * 0.9)),
                        mat,
                        sub["walls"],
                    )
                )

    # Door
    door_z = FOUNDATION_H + PLINTH_H + 0.95
    face_y = half_d + 0.05
    made.append(add_box("door_recess", Vector((0, face_y, door_z)), Vector((1.12, 0.18, 1.82)), mats["stone_dark"], sub["details"]))
    made.append(add_box("door_frame", Vector((0, face_y + 0.06, door_z)), Vector((1.08, 0.12, 1.78)), mats["frame"], sub["details"]))
    made.append(add_box("door_panel", Vector((0, face_y + 0.12, door_z - 0.02)), Vector((0.82, 0.1, 1.58)), mats["door"], sub["details"]))
    made.append(add_box("door_knob", Vector((0.26, face_y + 0.16, door_z)), Vector((VOX, VOX * 0.6, VOX)), mats["wood_dark"], sub["details"]))
    for step_i in range(3):
        made.append(
            add_box(
                f"door_step_{step_i}",
                Vector((0, face_y + 0.1 + step_i * 0.06, FOUNDATION_H + 0.08 + step_i * VOX)),
                Vector((1.0 - step_i * 0.08, 0.14, VOX * 0.85)),
                mats["stone"] if step_i % 2 == 0 else mats["stone_dark"],
                sub["details"],
            )
        )

    def add_window(tag: str, x: float, y: float, z: float, axis: str) -> None:
        if axis == "y":
            made.append(add_box(f"{tag}_frame", Vector((x, y, z)), Vector((0.78, 0.14, 0.78)), mats["frame"], sub["details"]))
            made.append(add_box(f"{tag}_recess", Vector((x, y - 0.06, z)), Vector((0.62, 0.12, 0.62)), mats["stone_dark"], sub["details"]))
            made.append(add_box(f"{tag}_glass", Vector((x, y + 0.07, z)), Vector((0.5, 0.08, 0.5)), mats["glass_glow"], sub["details"]))
            made.append(add_box(f"{tag}_sill", Vector((x, y + 0.03, z - 0.46)), Vector((0.82, 0.18, VOX * 0.9)), mats["wood"], sub["details"]))
            made.append(add_box(f"{tag}_mullion_v", Vector((x, y + 0.08, z)), Vector((VOX * 0.7, 0.08, 0.52)), mats["frame"], sub["details"]))
            made.append(add_box(f"{tag}_mullion_h", Vector((x, y + 0.08, z)), Vector((0.52, 0.08, VOX * 0.7)), mats["frame"], sub["details"]))
        else:
            made.append(add_box(f"{tag}_frame", Vector((x, y, z)), Vector((0.14, 0.78, 0.78)), mats["frame"], sub["details"]))
            made.append(add_box(f"{tag}_recess", Vector((x - 0.06, y, z)), Vector((0.12, 0.62, 0.62)), mats["stone_dark"], sub["details"]))
            made.append(add_box(f"{tag}_glass", Vector((x + 0.07, y, z)), Vector((0.08, 0.5, 0.5)), mats["glass_glow"], sub["details"]))

    win_z = FOUNDATION_H + PLINTH_H + 1.52
    add_window("win_front_l", -0.95, face_y, win_z, "y")
    add_window("win_front_r", 0.95, face_y, win_z, "y")
    add_window("win_side_e", half_w - 0.02, -0.35, win_z, "x")

    # Flower voxels
    fx, fy, fz = -0.95, face_y + 0.14, FOUNDATION_H + PLINTH_H + 0.92
    made.append(add_box("flower_box", Vector((fx, fy, fz)), Vector((0.68, 0.22, 0.18)), mats["wood"], sub["details"]))
    for i, (dx, dz, mkey) in enumerate(((0, 0, "leaf"), (0.12, 0.04, "flower"), (-0.1, 0.05, "flower"), (0.04, 0.08, "leaf"))):
        made.append(
            add_box(
                f"flower_vox_{i}",
                Vector((fx + dx, fy + 0.1, fz + 0.12 + dz)),
                Vector((VOX * 0.85, VOX * 0.7, VOX * 0.85)),
                mats[mkey],
                sub["details"],
            )
        )

    # Roof (same layout as cozy GLB — slopes parent tiles, no per-shingle world rotation)
    wall_top = FOUNDATION_H + PLINTH_H + WALL_H
    pitch = math.radians(ROOF_PITCH_DEG)
    half_depth = HOUSE_DEPTH_Y * 0.5
    eave_z = wall_top + 0.07
    made.append(
        add_box(
            "roof_eave_ring",
            Vector((0, 0, eave_z)),
            Vector((HOUSE_WIDTH_X, HOUSE_DEPTH_Y, 0.14)),
            mats["roof"],
            sub["roof"],
        )
    )

    slope_len = half_depth / math.cos(pitch) * 0.98
    rise = math.sin(pitch) * half_depth
    ridge_z = eave_z + rise + 0.18

    for side, sign, mat_key in (("south", 1, "roof"), ("north", -1, "roof_dark")):
        cy = sign * half_depth * 0.45
        cz = eave_z + rise * 0.5
        made.append(
            add_box(
                f"roof_slope_{side}",
                Vector((0, cy, cz)),
                Vector((HOUSE_WIDTH_X + 0.04, slope_len, 0.18)),
                mats[mat_key],
                sub["roof"],
                rotation=Vector((sign * pitch, 0, 0)),
            )
        )

    made.append(
        add_box(
            "roof_ridge_cap",
            Vector((0, 0, ridge_z)),
            Vector((HOUSE_WIDTH_X + 0.08, 0.22, 0.11)),
            mats["roof"],
            sub["roof"],
        )
    )

    cx, cy = -1.0, -0.55
    made.append(add_box("chimney_stack", Vector((cx, cy, ridge_z + 0.35)), Vector((0.4, 0.4, 1.0)), mats["chimney"], sub["details"]))
    made.append(add_box("chimney_cap", Vector((cx, cy, ridge_z + 0.92)), Vector((0.48, 0.48, 0.12)), mats["stone"], sub["details"]))

    parent_all(root, made)
    return root


def validate_footprint(root: bpy.types.Object) -> None:
    hx = FOOTPRINT_LIMIT_X * 0.5
    hy = FOOTPRINT_LIMIT_Y * 0.5
    for obj in root.children_recursive:
        if obj.type != "MESH":
            continue
        for corner in obj.bound_box:
            w = obj.matrix_world @ Vector(corner)
            if abs(w.x) > hx + 0.05 or abs(w.y) > hy + 0.05:
                print(f"WARNING footprint: {obj.name} x={w.x:.2f} y={w.y:.2f}")


def export_glb(root: bpy.types.Object, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for ch in root.children_recursive:
        ch.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_materials="EXPORT",
    )


def main() -> None:
    clear_scene()
    bpy.context.scene.unit_settings.system = "METRIC"
    save_reference_atlas_png()
    mats = {key: create_matte(f"VN_Hero_{key}", rgb, 0.35 if key == "glass_glow" else 0.94) for key, rgb in PAL.items()}
    coll = ensure_collection(COLLECTION_ROOT)
    root = build_house(mats, coll)
    validate_footprint(root)
    export_glb(root, EXPORT_PATH)
    if EXPORT_PATH.is_file() and EXPORT_PATH.stat().st_size > 2048:
        print(f"SUCCESS: {EXPORT_PATH.stat().st_size} bytes (solid voxel materials)")
    else:
        raise RuntimeError("Export failed")


if __name__ == "__main__":
    main()

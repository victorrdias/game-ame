# Vila Nova — town floor GLB: medieval cobbles, a worn slab street, grass tufts.
# Coordinates below are Godot XZ. Blender is Z-up; export flips Y into glTF Z.
# blender --background --python Tools/Blender/generate_town_ground.py

from __future__ import annotations

from pathlib import Path

import bpy
import bmesh


SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parent.parent
MODEL_DIR = PROJECT_ROOT / "Assets" / "Models"
TEXTURE_DIR = PROJECT_ROOT / "Assets" / "Textures"
GLB_PATH = MODEL_DIR / "vn_town_ground.glb"

CELL = 2.0
COUNT = 120
HALF = 120.0
FLOOR_Y = 0.025
PATH_Y = 0.04
FLOOR_UV = 4.0

STONES = (
    (198, 180, 156, 255),
    (176, 158, 136, 255),
    (154, 136, 118, 255),
    (186, 168, 148, 255),
    (142, 126, 110, 255),
    (168, 150, 130, 255),
)
MORTAR = (88, 78, 68, 255)
JOINT_DIRT = (132, 102, 72, 255)
MOSS = (86, 118, 74, 255)
SLABS = (
    (214, 190, 156, 255),
    (188, 156, 116, 255),
    (166, 138, 106, 255),
    (200, 174, 142, 255),
)
TUFT = (79, 145, 96, 255)
TUFT_TIP = (142, 196, 132, 255)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def new_image(name: str, size: int):
    image = bpy.data.images.new(name, size, size, alpha=True)
    image.colorspace_settings.name = "sRGB"
    image.pixels = [0.0] * (size * size * 4)
    return image


def put_pixel(image, x: int, y: int, rgba: tuple[int, int, int, int]) -> None:
    size = image.size[0]
    index = (y * size + x) * 4
    image.pixels[index : index + 4] = tuple(channel / 255.0 for channel in rgba)


def save_image(image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()


def shade(color: tuple[int, int, int, int], delta: int) -> tuple[int, int, int, int]:
    return tuple(max(0, min(255, channel + delta)) if index < 3 else channel for index, channel in enumerate(color))


def paint_setts(image, palette, row_height: int, min_width: int, width_span: int) -> None:
    size = image.size[0]
    mortar = [[True for _ in range(size)] for _ in range(size)]
    for y in range(size):
        for x in range(size):
            put_pixel(image, x, y, MORTAR)

    y = 0
    row = 0
    while y < size:
        x = -((row * 3) % (min_width + 1))
        stone = 0
        while x < size:
            width = min_width + ((row * 7 + stone * 3) % width_span)
            height = row_height - 1
            base = palette[(row * 3 + stone) % len(palette)]
            for yy in range(y, min(size, y + height)):
                for xx in range(max(0, x), min(size, x + width)):
                    corner = (xx == x or xx == x + width - 1) and (yy == y or yy == y + height - 1)
                    if corner and (stone + yy) % 3 == 0:
                        continue
                    if (xx * 3 + yy) % 11 == 0:
                        color = shade(base, -22)
                    elif (xx + yy * 2) % 13 == 0:
                        color = shade(base, 16)
                    else:
                        color = base
                    put_pixel(image, xx, yy, color)
                    mortar[yy][xx] = False
            x += width + 1
            stone += 1
        y += row_height
        row += 1

    for y in range(size):
        for x in range(size):
            if not mortar[y][x]:
                continue
            roll = (x * 5 + y * 9) % 23
            if roll == 0:
                put_pixel(image, x, y, JOINT_DIRT)
            elif roll == 11:
                put_pixel(image, x, y, MOSS)


def paint_floor(image) -> None:
    paint_setts(image, STONES, 5, 3, 3)


def paint_path(image) -> None:
    paint_setts(image, SLABS, 8, 6, 3)


def paint_tuft(image) -> None:
    size = image.size[0]
    for y in range(size):
        for x in range(size):
            put_pixel(image, x, y, (0, 0, 0, 0))
    for x, height in ((1, 3), (2, 5), (3, 7), (4, 6), (5, 5), (6, 3)):
        for y in range(1, height):
            put_pixel(image, x, y, TUFT_TIP if y == height - 1 else TUFT)


def make_material(name: str, image, clip: bool) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.use_backface_culling = not clip
    material.blend_method = "CLIP" if clip else "OPAQUE"
    if clip:
        material.alpha_threshold = 0.5
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Closest"
    bsdf.inputs["Roughness"].default_value = 1.0
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.0
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if clip:
        links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return material


def cell_is_floor(x: float, z: float, floor_cells: set[tuple[int, int]]) -> bool:
    cell_x = int((x + HALF) // CELL)
    cell_z = int((z + HALF) // CELL)
    return (cell_x, cell_z) in floor_cells


def level_zero_rects() -> tuple[list[tuple[float, float, float, float]], set[tuple[int, int]]]:
    kinds = [0] * (COUNT * COUNT)
    levels = [0] * (COUNT * COUNT)

    def set_cell(x: int, z: int, kind: int, level: int) -> None:
        if 0 <= x < COUNT and 0 <= z < COUNT:
            index = x + z * COUNT
            kinds[index] = kind
            levels[index] = level

    def fill(x0: int, x1: int, z0: int, z1: int, kind: int, level: int) -> None:
        for x in range(x0, x1 + 1):
            for z in range(z0, z1 + 1):
                set_cell(x, z, kind, level)

    last = COUNT - 1
    for x in range(COUNT):
        for z in range(COUNT):
            if x <= 1 or z <= 1 or x >= last - 1 or z >= last - 1:
                set_cell(x, z, 2, 4)

    fill(14, 44, 36, 78, 0, 1)
    fill(37, 37, 54, 62, 1, 0)
    fill(82, 104, 48, 78, 0, 1)
    fill(96, 110, 56, 74, 0, 2)
    fill(81, 81, 58, 66, 1, 0)
    fill(95, 95, 60, 66, 1, 1)
    fill(40, 80, 82, 94, 0, 1)
    fill(46, 76, 96, 110, 0, 2)
    fill(54, 66, 102, 108, 0, 3)
    fill(54, 64, 81, 81, 1, 0)
    fill(56, 64, 95, 95, 1, 1)
    fill(56, 62, 101, 101, 1, 2)
    fill(18, 38, 84, 100, 0, 1)
    fill(20, 34, 90, 106, 0, 2)
    fill(22, 30, 98, 106, 0, 3)
    fill(22, 30, 83, 83, 1, 0)
    fill(22, 30, 89, 89, 1, 1)
    fill(24, 28, 97, 97, 1, 2)
    fill(86, 112, 18, 42, 0, 1)
    fill(96, 110, 24, 36, 0, 2)
    fill(85, 85, 26, 32, 1, 0)
    fill(95, 95, 28, 32, 1, 1)
    fill(54, 72, 16, 34, 0, -1)
    fill(60, 66, 35, 35, 1, -1)
    fill(90, 104, 84, 98, 0, -1)
    fill(89, 89, 88, 94, 1, -1)

    rects = []
    cells = set()
    for x in range(COUNT):
        for z in range(COUNT):
            index = x + z * COUNT
            if kinds[index] == 0 and levels[index] == 0:
                x0 = -HALF + x * CELL
                z0 = -HALF + z * CELL
                rects.append((x0, x0 + CELL, z0, z0 + CELL))
                cells.add((x, z))
    return rects, cells


def blender_of(godot_x: float, godot_y: float, godot_z: float) -> tuple[float, float, float]:
    return (godot_x, -godot_z, godot_y)


def add_ground(name: str, rects, height: float, material, uv_meters: float) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    for x0, x1, z0, z1 in rects:
        corners = (
            (x0, z0, (x0 + HALF) / uv_meters, (z0 + HALF) / uv_meters),
            (x0, z1, (x0 + HALF) / uv_meters, (z1 + HALF) / uv_meters),
            (x1, z1, (x1 + HALF) / uv_meters, (z1 + HALF) / uv_meters),
            (x1, z0, (x1 + HALF) / uv_meters, (z0 + HALF) / uv_meters),
        )
        verts = [bm.verts.new(blender_of(x, height, z)) for x, z, _, _ in corners]
        face = bm.faces.new(verts)
        for loop, (_, _, u, v) in zip(face.loops, corners):
            loop[uv_layer].uv = (u, v)
    bm.normal_update()
    for face in bm.faces:
        if face.normal.z < 0.0:
            face.normal_flip()
    bm.to_mesh(mesh)
    bm.free()
    obj.data.materials.append(material)
    return obj


def add_tuft(name: str, x: float, z: float, material, scale: float) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    height = 0.95 * scale
    span = 0.48 * scale
    cards = (
        ((x - span, z, 0.0), (x + span, z, 0.0), (x + span, z, height), (x - span, z, height)),
        ((x, z - span, 0.0), (x, z + span, 0.0), (x, z + span, height), (x, z - span, height)),
    )
    uvs = ((0, 0), (1, 0), (1, 1), (0, 1))
    for card in cards:
        verts = [bm.verts.new(blender_of(px, py, pz)) for px, pz, py in card]
        face = bm.faces.new(verts)
        for loop, uv in zip(face.loops, uvs):
            loop[uv_layer].uv = uv
        face.normal_flip()
    bm.to_mesh(mesh)
    bm.free()
    obj.data.materials.append(material)
    return obj


def blocked(x: float, z: float) -> bool:
    keep_clear = (
        (-1.0, -1.0, 4.2),
        (-8.0, -2.0, 3.2),
        (0.0, -6.0, 3.2),
        (-5.0, -1.0, 1.8),
        (5.0, -3.0, 1.8),
        (-3.0, -9.0, 1.8),
        (-7.0, 5.0, 1.4),
        (-9.0, -5.0, 1.4),
        (4.0, 4.0, 1.6),
    )
    for cx, cz, radius in keep_clear:
        if (x - cx) ** 2 + (z - cz) ** 2 < radius * radius:
            return True
    on_south = -16.0 <= x <= 12.0 and -12.2 <= z <= -9.8
    on_east = 7.8 <= x <= 10.2 and -12.2 <= z <= 10.2
    return on_south or on_east


def tuft_points(floor_cells: set[tuple[int, int]]) -> list[tuple[float, float, float]]:
    hand = (
        (12.5, 4.0, 1.05),
        (14.0, -6.0, 0.85),
        (-16.0, 2.0, 1.1),
        (-14.0, 7.0, 0.8),
        (3.0, 12.0, 0.95),
        (-6.0, 14.0, 1.0),
        (16.0, 8.0, 0.75),
        (-18.0, -6.0, 1.15),
        (11.0, -15.0, 0.85),
        (-2.0, 16.0, 0.7),
        (18.0, -3.0, 0.9),
        (-12.0, 11.0, 0.8),
        (6.0, -16.0, 1.0),
        (-17.0, -12.0, 0.85),
        (20.0, 2.0, 0.7),
        (-8.0, 17.0, 0.95),
    )
    points = [point for point in hand if cell_is_floor(point[0], point[1], floor_cells) and not blocked(point[0], point[1])]
    for x in range(-56, 57, 8):
        for z in range(-56, 57, 8):
            if (x + z) % 16 != 0:
                continue
            if x * x + z * z < 20 * 20:
                continue
            if not cell_is_floor(x, z, floor_cells) or blocked(x, z):
                continue
            scale = 0.7 + ((x * 13 + z * 7) % 5) * 0.08
            points.append((float(x), float(z), scale))
    print(f"TUFTS {len(points)}")
    return points


def export_glb(root, path: Path) -> None:
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
    kwargs = {key: value for key, value in kwargs.items() if not props or key in props}
    if "export_yup" in props:
        kwargs["export_yup"] = True
    for key in ("export_cameras", "export_lights"):
        if key in props:
            kwargs[key] = False
    bpy.ops.export_scene.gltf(**kwargs)
    print(f"EXPORTED {path} ({path.stat().st_size} bytes)")


def main() -> None:
    clear_scene()
    floor_image = new_image("vn_town_floor", 64)
    path_image = new_image("vn_town_path", 32)
    tuft_image = new_image("vn_town_tuft", 8)
    paint_floor(floor_image)
    paint_path(path_image)
    paint_tuft(tuft_image)
    save_image(floor_image, TEXTURE_DIR / "vn_town_floor.png")
    save_image(path_image, TEXTURE_DIR / "vn_town_path.png")
    save_image(tuft_image, TEXTURE_DIR / "vn_town_tuft.png")

    floor_mat = make_material("TownFloor", floor_image, False)
    path_mat = make_material("TownPath", path_image, False)
    tuft_mat = make_material("TownTuft", tuft_image, True)

    rects, floor_cells = level_zero_rects()
    print(f"LEVEL0_QUADS {len(rects)}")
    objects = [add_ground("TownFloor", rects, FLOOR_Y, floor_mat, FLOOR_UV)]
    objects.append(add_ground("TownPath", [(-16.0, 12.0, -12.0, -10.0), (8.0, 10.0, -12.0, 10.0)], PATH_Y, path_mat, FLOOR_UV))
    for index, (x, z, scale) in enumerate(tuft_points(floor_cells)):
        objects.append(add_tuft(f"Tuft{index}", x, z, tuft_mat, scale))

    root = bpy.data.objects.new("TownGround", None)
    bpy.context.scene.collection.objects.link(root)
    for obj in objects:
        obj.parent = root
    export_glb(root, GLB_PATH)


if __name__ == "__main__":
    main()

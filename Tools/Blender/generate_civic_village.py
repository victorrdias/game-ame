# Vila Nova — school, hospital, town building, entrance arch, and cottage paths.
# Same matte palette as the reference cottage. Placement cells are 2/3 m.
# Blender Z-up, origin at the footprint center, facade +Y. GLB is Y-up.
#
# blender --background --python Tools/Blender/generate_civic_village.py

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
BLEND_PATH = MODEL_DIR / "vn_civic_village.blend"


def srgb_hex(value: str) -> tuple[float, float, float]:
    h = value.lstrip("#")
    rgb = tuple(int(h[i : i + 2], 16) / 255.0 for i in (0, 2, 4))

    def to_linear(channel: float) -> float:
        if channel <= 0.04045:
            return channel / 12.92
        return ((channel + 0.055) / 1.055) ** 2.4

    return tuple(to_linear(c) for c in rgb)  # type: ignore[return-value]


def rgba(value: str) -> tuple[float, float, float, float]:
    r, g, b = srgb_hex(value)
    return (r, g, b, 1.0)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.cameras):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def ensure_collection(name: str, parent=None):
    coll = bpy.data.collections.get(name)
    if coll is None:
        coll = bpy.data.collections.new(name)
    host = parent if parent is not None else bpy.context.scene.collection
    if coll.name not in {c.name for c in host.children}:
        host.children.link(coll)
    return coll


def link_object(obj, coll) -> None:
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    coll.objects.link(obj)


def make_material(name, hex_color, roughness=0.92, emission=None, emission_strength=0.0):
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
        socket = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
        if socket is not None:
            socket.default_value = rgba(emission)
        strength = bsdf.inputs.get("Emission Strength")
        if strength is not None:
            strength.default_value = emission_strength
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def subtract_intervals(u0, u1, holes):
    spans = [(u0, u1)]
    for h0, h1 in holes:
        nxt = []
        for a, b in spans:
            if h1 <= a or h0 >= b:
                nxt.append((a, b))
                continue
            if a < h0:
                nxt.append((a, min(b, h0)))
            if h1 < b:
                nxt.append((max(a, h1), b))
        spans = [(a, b) for a, b in nxt if b - a > 0.02]
    return spans


def rects_minus_holes(u0, u1, z0, z1, holes):
    zs = {z0, z1}
    clipped = []
    for hu0, hu1, hz0, hz1 in holes:
        za, zb = max(hz0, z0), min(hz1, z1)
        ua, ub = max(hu0, u0), min(hu1, u1)
        if zb - za > 0.02 and ub - ua > 0.02:
            clipped.append((ua, ub, za, zb))
            zs.add(za)
            zs.add(zb)
    ordered = sorted(zs)
    out = []
    for za, zb in zip(ordered, ordered[1:]):
        if zb - za < 0.02:
            continue
        mid = (za + zb) * 0.5
        blockers = [(h[0], h[1]) for h in clipped if h[2] < mid < h[3]]
        for ua, ub in subtract_intervals(u0, u1, blockers):
            out.append((ua, ub, za, zb))
    return out


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

    def add_box(self, center, size, mat, rotation=None):
        sx, sy, sz = (float(v) for v in size)
        if min(sx, sy, sz) <= 0.004:
            return
        geom = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = geom["verts"]
        bmesh.ops.scale(self.bm, verts=verts, vec=Vector((sx, sy, sz)))
        if rotation is not None:
            bmesh.ops.rotate(self.bm, verts=verts, cent=Vector((0, 0, 0)), matrix=rotation.to_matrix())
        bmesh.ops.translate(self.bm, verts=verts, vec=Vector(center))
        idx = self.slot(mat)
        for face in {f for v in verts for f in v.link_faces}:
            face.material_index = idx
            face.smooth = False

    def add_cylinder(self, center, radius, height, mat, segments=8):
        bm = self.bm
        idx = self.slot(mat)
        bottom, top = [], []
        z0, z1 = -height * 0.5, height * 0.5
        for i in range(segments):
            a = math.tau * i / segments
            c, s = math.cos(a), math.sin(a)
            bottom.append(bm.verts.new((c * radius, s * radius, z0)))
            top.append(bm.verts.new((c * radius, s * radius, z1)))
        bmesh.ops.translate(bm, verts=bottom + top, vec=Vector(center))
        for face in (bm.faces.new(list(reversed(bottom))), bm.faces.new(top)):
            face.material_index = idx
            face.smooth = False
        for i in range(segments):
            j = (i + 1) % segments
            side = bm.faces.new((bottom[i], top[i], top[j], bottom[j]))
            side.material_index = idx
            side.smooth = False

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
        obj.matrix_parent_inverse = parent.matrix_world.inverted()


def add_wall(bucket, axis, face, thickness, u0, u1, z0, z1, holes, mat):
    for ua, ub, za, zb in rects_minus_holes(u0, u1, z0, z1, holes):
        if axis == "y":
            bucket.add_box(((ua + ub) * 0.5, face, (za + zb) * 0.5), (ub - ua, thickness, zb - za), mat)
        else:
            bucket.add_box((face, (ua + ub) * 0.5, (za + zb) * 0.5), (thickness, ub - ua, zb - za), mat)


def fill_arch(bucket, axis, face, thickness, cx, z_spring, radius, mat, steps=5):
    for i in range(steps):
        za = z_spring + radius * i / steps
        zb = z_spring + radius * (i + 1) / steps
        dz = (za + zb) * 0.5 - z_spring
        half = math.sqrt(max(radius * radius - dz * dz, 0.0))
        for side in (-1.0, 1.0):
            outer = cx + side * radius
            inner = cx + side * half
            ua, ub = (inner, outer) if inner < outer else (outer, inner)
            if ub - ua < 0.02:
                continue
            if axis == "y":
                bucket.add_box(((ua + ub) * 0.5, face, (za + zb) * 0.5), (ub - ua, thickness, zb - za), mat)
            else:
                bucket.add_box((face, (ua + ub) * 0.5, (za + zb) * 0.5), (thickness, ub - ua, zb - za), mat)


def add_courses(bucket, axis, face, thickness, u0, u1, z0, z1, stones, holes=None):
    holes = holes or []
    z = z0
    row = 0
    while z < z1 - 0.03 and row < 30:
        h = min(0.22 if row % 2 == 0 else 0.16, z1 - z)
        u = u0 - (0.08 if row % 2 else 0.0)
        col = 0
        while u < u1 - 0.05 and col < 40:
            w = 0.42 if (row + col) % 3 else 0.28
            ua, ub = max(u, u0), min(u + w, u1)
            blocked = any(ua < h1 and ub > h0 and z < hz1 and z + h > hz0 for h0, h1, hz0, hz1 in holes)
            if ub - ua > 0.08 and not blocked:
                mat = stones[(row + col) % len(stones)]
                if axis == "y":
                    bucket.add_box(((ua + ub) * 0.5, face, z + h * 0.5), (ub - ua - 0.015, thickness, h - 0.012), mat)
                else:
                    bucket.add_box((face, (ua + ub) * 0.5, z + h * 0.5), (thickness, ub - ua - 0.015, h - 0.012), mat)
            u += w
            col += 1
        z += h
        row += 1


def add_flower(bucket, x, y, z, color, mats, scale=1.0):
    stem = 0.12 * scale
    bucket.add_box((x, y, z + stem * 0.5), (0.025 * scale, 0.025 * scale, stem), mats["leaf"])
    petal = 0.07 * scale
    pz = z + stem
    for dx, dy in ((petal, 0), (-petal, 0), (0, petal), (0, -petal)):
        bucket.add_box((x + dx, y + dy, pz), (0.065 * scale, 0.065 * scale, 0.035 * scale), color)
    bucket.add_box((x, y, pz + 0.02 * scale), (0.04 * scale, 0.04 * scale, 0.03 * scale), mats["flower_y"])


def add_window(bucket, x, y, z, w, h, mats, face="y"):
    if face == "y":
        bucket.add_box((x, y, z), (w * 0.72, 0.04, h * 0.62), mats["glass"])
        bucket.add_box((x, y + 0.03, z), (0.04, 0.05, h * 0.7), mats["frame"])
        bucket.add_box((x, y + 0.03, z), (w * 0.7, 0.05, 0.04), mats["frame"])
    else:
        bucket.add_box((y, x, z), (0.04, w * 0.72, h * 0.62), mats["glass"])
        bucket.add_box((y + 0.03, x, z), (0.05, 0.04, h * 0.7), mats["frame"])


def add_door(bucket, x, y, z, w, h, mats):
    plank = w / 4.0
    for i in range(4):
        bucket.add_box((x - w * 0.5 + plank * (i + 0.5), y, z + h * 0.46), (plank * 0.9, 0.06, h * 0.9), mats["door"] if i % 2 == 0 else mats["wood"])
    bucket.add_box((x + w * 0.28, y + 0.05, z + h * 0.48), (0.04, 0.03, 0.1), mats["metal"])
    bucket.add_box((x, y + 0.02, z + h * 0.92), (w * 0.55, 0.03, h * 0.14), mats["glass"])


def roof_x(tiles, wood, center_y, half, x0, x1, z_plate, pitch, oh, mats):
    """Ridge along X, slopes face ±Y. Big tiles so civic roofs stay light."""
    tan_p, sin_p, cos_p = math.tan(pitch), math.sin(pitch), math.cos(pitch)
    ridge = z_plate + half * tan_p
    colors = mats["roofs"]
    for sign in (-1.0, 1.0):
        span = half + oh
        slope = span / cos_p
        rot = Euler((pitch if sign < 0 else -pitch, 0, 0), "XYZ")
        normal = Vector((0, -sin_p if sign < 0 else sin_p, cos_p))
        eave_y = center_y + sign * span
        eave_z = z_plate - oh * tan_p
        mid = slope * 0.5
        mid_y = eave_y + mid * cos_p if sign < 0 else eave_y - mid * cos_p
        mid_z = eave_z + mid * sin_p
        tiles.add_box(((x0 + x1) * 0.5, mid_y, mid_z) , (x1 - x0, slope, 0.04), mats["roof_dark"], rot)
        dist = 0.04
        row = 0
        while dist < slope - 0.08 and row < 24:
            row_c = dist + 0.16
            y = eave_y + row_c * cos_p if sign < 0 else eave_y - row_c * cos_p
            z = eave_z + row_c * sin_p
            base = Vector((0, y, z)) + normal * 0.04
            x = x0 + (0.18 if row % 2 else 0.0)
            col = 0
            while x < x1 - 0.05 and col < 30:
                tiles.add_box((x + 0.2, base.y, base.z), (0.36, 0.3, 0.045), colors[(row + col) % len(colors)], rot)
                x += 0.38
                col += 1
            dist += 0.2
            row += 1
        wood.add_box(((x0 + x1) * 0.5, eave_y, eave_z), (x1 - x0, 0.08, 0.06), mats["wood_dark"])
    x = x0
    i = 0
    while x < x1:
        tiles.add_box((x + 0.18, center_y, ridge + 0.05), (0.34, 0.22, 0.06), colors[i % len(colors)])
        x += 0.34
        i += 1
    return ridge


def roof_y(tiles, wood, center_x, half, y0, y1, z_plate, pitch, oh, mats):
    """Ridge along Y, slopes face ±X."""
    tan_p, sin_p, cos_p = math.tan(pitch), math.sin(pitch), math.cos(pitch)
    ridge = z_plate + half * tan_p
    colors = mats["roofs"]
    for sign in (-1.0, 1.0):
        span = half + oh
        slope = span / cos_p
        rot = Euler((0, -pitch if sign < 0 else pitch, 0), "XYZ")
        normal = Vector((-sin_p if sign < 0 else sin_p, 0, cos_p))
        eave_x = center_x + sign * span
        eave_z = z_plate - oh * tan_p
        mid = slope * 0.5
        mid_x = eave_x + mid * cos_p if sign < 0 else eave_x - mid * cos_p
        mid_z = eave_z + mid * sin_p
        tiles.add_box((mid_x, (y0 + y1) * 0.5, mid_z), (slope, y1 - y0, 0.04), mats["roof_dark"], rot)
        dist = 0.04
        row = 0
        while dist < slope - 0.08 and row < 24:
            row_c = dist + 0.16
            x = eave_x + row_c * cos_p if sign < 0 else eave_x - row_c * cos_p
            z = eave_z + row_c * sin_p
            base = Vector((x, 0, z)) + normal * 0.04
            y = y0 + (0.16 if row % 2 else 0.0)
            col = 0
            while y < y1 - 0.05 and col < 30:
                tiles.add_box((base.x, y + 0.16, base.z), (0.3, 0.3, 0.045), colors[(row + col) % len(colors)], rot)
                y += 0.32
                col += 1
            dist += 0.2
            row += 1
    y = y0
    i = 0
    while y < y1:
        tiles.add_box((center_x, y + 0.16, ridge + 0.05), (0.22, 0.3, 0.06), colors[i % len(colors)])
        y += 0.3
        i += 1
    wood.add_box((center_x, (y0 + y1) * 0.5, ridge - 0.02), (0.1, y1 - y0, 0.07), mats["wood_dark"])
    return ridge


def fill_gable_x(bucket, x, y_half, z0, z1, thickness, mat, steps=6):
    span = z1 - z0
    if span < 0.05:
        return
    for i in range(steps):
        za = z0 + span * i / steps
        zb = z0 + span * (i + 1) / steps
        t = ((za + zb) * 0.5 - z0) / span
        half = y_half * max(0.05, 1.0 - t)
        bucket.add_box((x, 0, (za + zb) * 0.5), (thickness, half * 2, zb - za), mat)


def fill_gable_y(bucket, y, x_half, z0, z1, thickness, mat, steps=6):
    span = z1 - z0
    if span < 0.05:
        return
    for i in range(steps):
        za = z0 + span * i / steps
        zb = z0 + span * (i + 1) / steps
        t = ((za + zb) * 0.5 - z0) / span
        half = x_half * max(0.05, 1.0 - t)
        bucket.add_box((0, y, (za + zb) * 0.5), (half * 2, thickness, zb - za), mat)


def build_school(root, coll, mats):
    walls = Bucket("school_walls", coll)
    stone = Bucket("school_stone", coll)
    wood = Bucket("school_wood", coll)
    roof = Bucket("school_roof", coll)
    win = Bucket("school_win", coll)
    veg = Bucket("school_veg", coll)
    hx, hy = 3.45, 2.35
    found, plate = 0.34, 4.15
    door_w, door_h = 1.15, 1.9
    front_holes = [(-door_w * 0.5, door_w * 0.5, found, found + door_h + 0.45)]
    for x in (-2.35, -1.15, 1.15, 2.35):
        front_holes.append((x - 0.38, x + 0.38, found + 0.85, found + 1.85))
        front_holes.append((x - 0.34, x + 0.34, 2.55, 3.55))
    add_courses(stone, "y", hy + 0.02, 0.18, -hx, hx, 0, found + 1.15, mats["stones"], front_holes)
    add_courses(stone, "y", -hy - 0.02, 0.16, -hx, hx, 0, found, mats["stones_warm"])
    add_courses(stone, "x", hx + 0.02, 0.16, -hy, hy, 0, found, mats["stones"])
    add_courses(stone, "x", -hx - 0.02, 0.16, -hy, hy, 0, found, mats["stones_warm"])
    add_wall(walls, "y", hy, 0.16, -hx, hx, found, plate, front_holes, mats["plaster"])
    add_wall(walls, "y", -hy, 0.16, -hx, hx, found, plate, [(-0.4, 0.4, 2.4, 3.3)], mats["plaster2"])
    add_wall(walls, "x", hx, 0.16, -hy, hy, found, plate, [], mats["plaster"])
    add_wall(walls, "x", -hx, 0.16, -hy, hy, found, plate, [(0.0, 0.7, 2.3, 3.2)], mats["plaster2"])
    fill_arch(walls, "y", hy, 0.16, 0, found + door_h, 0.48, mats["plaster"])
    add_door(wood, 0, hy + 0.12, found, door_w * 0.9, door_h, mats)
    win.add_box((0, hy - 0.05, found + 1.15), (door_w, 0.25, door_h + 0.3), mats["interior"])
    for x in (-2.35, -1.15, 1.15, 2.35):
        add_window(win, x, hy + 0.12, found + 1.35, 0.7, 0.85, mats)
        add_window(win, x, hy + 0.12, 3.05, 0.62, 0.8, mats)
        wood.add_box((x, hy + 0.18, found + 0.78), (0.85, 0.12, 0.08), mats["wood"])
        for i, color in enumerate((mats["flower_p"], mats["flower_w"], mats["flower_r"])):
            add_flower(veg, x - 0.22 + i * 0.22, hy + 0.28, found + 0.82, color, mats, 0.55)
    add_window(win, 0.35, -hy - 0.1, 2.85, 0.7, 0.75, mats)
    add_window(win, 0.2, hx + 0.1, 2.75, 0.6, 0.7, mats, "x")
    # Timber band between floors.
    wood.add_box((0, hy + 0.1, 2.15), (hx * 2, 0.08, 0.1), mats["wood_dark"])
    wood.add_box((0, hy + 0.1, plate - 0.04), (hx * 2, 0.08, 0.1), mats["wood_dark"])
    ridge = roof_x(roof, wood, 0, hy, -hx - 0.15, hx + 0.15, plate, math.radians(36), 0.22, mats)
    fill_gable_x(walls, hx - 0.02, hy - 0.08, plate, ridge - 0.08, 0.14, mats["plaster"])
    fill_gable_x(walls, -hx + 0.02, hy - 0.08, plate, ridge - 0.08, 0.14, mats["plaster2"])
    # Bell cupola, off center so the school is not a plain hall.
    stone.add_box((1.15, 0, ridge + 0.35), (0.7, 0.7, 0.7), mats["plaster"])
    wood.add_box((1.15, 0.28, ridge + 0.35), (0.28, 0.08, 0.28), mats["metal"])
    roof.add_box((1.15, 0, ridge + 0.85), (0.95, 0.95, 0.12), mats["roof_dark"])
    for i, color in enumerate(mats["roofs"]):
        roof.add_box((1.15 - 0.28 + i * 0.2, 0, ridge + 0.95), (0.2, 0.85, 0.06), color)
    wood.add_box((1.15, 0, ridge + 1.15), (0.06, 0.06, 0.35), mats["wood_dark"])
    for color, y in ((mats["flower_p"], hy + 0.35), (mats["flower_y"], -hy - 0.3)):
        add_flower(veg, -hx + 0.3, y, 0, color, mats, 0.8)
    for part in (walls, stone, wood, roof, win, veg):
        part.finish(root)


def build_hospital(root, coll, mats):
    walls = Bucket("hospital_walls", coll)
    stone = Bucket("hospital_stone", coll)
    wood = Bucket("hospital_wood", coll)
    roof = Bucket("hospital_roof", coll)
    win = Bucket("hospital_win", coll)
    veg = Bucket("hospital_veg", coll)
    hx, hy = 2.45, 2.35
    found, plate = 0.32, 3.15
    door = (-0.15, 0.55)
    holes = [(door[0] - door[1], door[0] + door[1], found, found + 2.05)]
    for x in (-1.45, 1.25):
        holes.append((x - 0.4, x + 0.4, found + 0.9, found + 1.9))
    add_courses(stone, "y", hy + 0.02, 0.2, -hx, hx, 0, plate, mats["stones"], holes)
    add_courses(stone, "y", -hy - 0.02, 0.16, -hx, hx, 0, plate, mats["stones_warm"])
    add_courses(stone, "x", -hx - 0.02, 0.16, -hy, hy, 0, plate, mats["stones"])
    add_courses(stone, "x", hx + 0.02, 0.16, -hy, hy, 0, plate, mats["stones_warm"])
    add_wall(walls, "y", hy - 0.02, 0.12, -hx, hx, found, plate, holes, mats["plaster"])
    add_wall(walls, "y", -hy, 0.14, -hx, hx, found, plate, [(0.4, 1.2, found + 1.0, found + 1.9)], mats["plaster2"])
    add_wall(walls, "x", -hx, 0.14, -hy, hy, found, plate, [], mats["plaster"])
    add_wall(walls, "x", hx, 0.14, -hy, hy, found, plate, [(-0.3, 0.4, found + 0.9, found + 1.8)], mats["plaster2"])
    fill_arch(walls, "y", hy - 0.02, 0.14, door[0], found + 1.55, door[1], mats["plaster"])
    add_door(wood, door[0], hy + 0.14, found, door[1] * 1.7, 1.55, mats)
    win.add_box((door[0], hy - 0.08, found + 1.05), (door[1] * 2, 0.3, 2.0), mats["interior"])
    for x in (-1.45, 1.25):
        add_window(win, x, hy + 0.14, found + 1.4, 0.72, 0.85, mats)
    add_window(win, 0.8, -hy - 0.1, found + 1.45, 0.7, 0.75, mats)
    add_window(win, 0.05, hx + 0.12, found + 1.35, 0.6, 0.7, mats, "x")
    # Pale panel and red cross, the readable hospital mark.
    walls.add_box((door[0], hy + 0.16, found + 2.35), (1.15, 0.08, 1.15), mats["plaster"])
    wood.add_box((door[0], hy + 0.22, found + 2.35), (0.22, 0.06, 0.78), mats["cross"])
    wood.add_box((door[0], hy + 0.22, found + 2.35), (0.78, 0.06, 0.22), mats["cross"])
    ridge = roof_y(roof, wood, 0, hx, -hy - 0.1, hy + 0.18, plate, math.radians(42), 0.2, mats)
    fill_gable_y(walls, hy - 0.02, hx - 0.12, plate, ridge - 0.1, 0.16, mats["plaster"])
    fill_gable_y(walls, -hy + 0.02, hx - 0.12, plate, ridge - 0.1, 0.16, mats["plaster2"])
    wood.add_box((0.9, 0.2, plate + 0.9), (0.55, 0.55, 1.5), mats["stones"][0])
    wood.add_box((0.9, 0.2, plate + 1.7), (0.7, 0.7, 0.1), mats["stones_warm"][0])
    wood.add_box((0.9, 0.2, plate + 1.85), (0.4, 0.4, 0.18), mats["brick"])
    for i, color in enumerate((mats["flower_w"], mats["flower_p"], mats["flower_r"])):
        add_flower(veg, -hx + 0.25, hy + 0.25 + i * 0.02, 0.05 * i, color, mats, 0.7)
    for part in (walls, stone, wood, roof, win, veg):
        part.finish(root)


def build_townhouse(root, coll, mats):
    walls = Bucket("town_walls", coll)
    stone = Bucket("town_stone", coll)
    wood = Bucket("town_wood", coll)
    roof = Bucket("town_roof", coll)
    win = Bucket("town_win", coll)
    veg = Bucket("town_veg", coll)
    hx, hy = 1.72, 1.62
    floors = (0.28, 2.25, 4.2)
    plate = 6.05
    door = ( -0.35, 0.42)
    holes = [(door[0] - door[1], door[0] + door[1], floors[0], floors[0] + 1.7)]
    holes.append((0.35, 1.05, floors[0] + 0.7, floors[0] + 1.55))
    for z in floors[1:]:
        holes.append((-1.05, -0.35, z + 0.45, z + 1.35))
        holes.append((0.25, 0.95, z + 0.45, z + 1.35))
    add_courses(stone, "y", hy + 0.02, 0.16, -hx, hx, 0, floors[1], mats["stones"], holes)
    add_courses(stone, "y", -hy - 0.02, 0.14, -hx, hx, 0, 0.3, mats["stones_warm"])
    add_wall(walls, "y", hy, 0.16, -hx, hx, floors[0], plate, holes, mats["plaster"])
    add_wall(walls, "y", -hy, 0.16, -hx, hx, floors[0], plate, [(0.1, 0.8, floors[1] + 0.5, floors[1] + 1.35)], mats["plaster2"])
    add_wall(walls, "x", hx, 0.14, -hy, hy, floors[0], plate, [], mats["plaster"])
    add_wall(walls, "x", -hx, 0.14, -hy, hy, floors[0], plate, [( -0.2, 0.45, floors[2] + 0.45, floors[2] + 1.3)], mats["plaster2"])
    fill_arch(walls, "y", hy, 0.16, door[0], floors[0] + 1.25, door[1], mats["plaster"])
    add_door(wood, door[0], hy + 0.12, floors[0], door[1] * 1.6, 1.25, mats)
    win.add_box((door[0], hy - 0.06, floors[0] + 0.85), (0.8, 0.2, 1.5), mats["interior"])
    add_window(win, 0.7, hy + 0.12, floors[0] + 1.15, 0.6, 0.7, mats)
    for z in floors[1:]:
        for x in (-0.7, 0.6):
            add_window(win, x, hy + 0.12, z + 0.9, 0.62, 0.75, mats)
            wood.add_box((x, hy + 0.16, z + 0.38), (0.75, 0.1, 0.06), mats["wood"])
    for z in (floors[1], floors[2], plate):
        wood.add_box((0, hy + 0.1, z), (hx * 2.05, 0.07, 0.08), mats["wood_dark"])
    # Corner posts so the stack reads as a building, not three cottages.
    for x, sx in ((-hx, -1), (hx, 1)):
        wood.add_box((x, hy + 0.08, (floors[0] + plate) * 0.5), (0.1, 0.08, plate - floors[0]), mats["wood_dark"])
    ridge = roof_y(roof, wood, 0, hx, -hy - 0.08, hy + 0.16, plate, math.radians(28), 0.16, mats)
    fill_gable_y(walls, hy - 0.02, hx - 0.08, plate, ridge - 0.06, 0.14, mats["plaster"])
    fill_gable_y(walls, -hy + 0.02, hx - 0.08, plate, ridge - 0.06, 0.14, mats["plaster2"])
    wood.add_box((-0.55, -0.15, ridge + 0.15), (0.4, 0.4, 0.7), mats["brick"])
    wood.add_box((-0.55, -0.15, ridge + 0.55), (0.52, 0.52, 0.08), mats["stones_warm"][0])
    add_flower(veg, hx - 0.15, hy + 0.25, 0, mats["flower_r"], mats, 0.7)
    add_flower(veg, door[0] - 0.7, hy + 0.3, 0, mats["flower_w"], mats, 0.65)
    for part in (walls, stone, wood, roof, win, veg):
        part.finish(root)


def build_gate(root, coll, mats):
    stone = Bucket("gate_stone", coll)
    wood = Bucket("gate_wood", coll)
    roof = Bucket("gate_roof", coll)
    win = Bucket("gate_win", coll)
    opening = 1.2
    pier_outer = 2.7
    hy = 0.72
    spring, radius = 2.15, opening
    # Piers.
    for sign in (-1.0, 1.0):
        x = sign * (opening + (pier_outer - opening) * 0.5)
        w = pier_outer - opening
        add_courses(stone, "y", 0, w * 0.92, x - w * 0.45, x + w * 0.45, 0, 3.35, mats["stones"])
        stone.add_box((x, 0, 3.5), (w + 0.08, hy * 2.1, 0.28), mats["stones_warm"][0])
        # Amber lantern on the street face.
        win.add_box((x, hy + 0.08, 2.15), (0.22, 0.1, 0.32), mats["glass"])
        wood.add_box((x, hy + 0.12, 2.35), (0.32, 0.08, 0.06), mats["wood_dark"])
        wood.add_box((x, hy + 0.12, 1.95), (0.32, 0.08, 0.06), mats["wood"])
    # Arch ring across the opening, in front and back.
    for y in (hy * 0.35, -hy * 0.35):
        for i in range(7):
            a = math.pi * (i + 0.5) / 7
            stone.add_box((math.cos(a) * (radius + 0.02), y, spring + math.sin(a) * radius), (0.28, hy * 0.7, 0.18), mats["stones_warm"][i % 2])
        wood.add_box((0, y, spring + radius + 0.28), (opening * 2.3, 0.12, 0.16), mats["wood_dark"])
    # Spandrel infill so the opening reads as an arch, not a slot.
    fill_arch(stone, "y", 0, hy * 1.3, 0, spring, radius, mats["stones"][2], 4)
    # The fill_arch fills corners of the bounding square; the pier courses already
    # stop at the opening, so this closes the upper corners.
    ridge = roof_x(roof, wood, 0, hy, -pier_outer, pier_outer, 3.72, math.radians(28), 0.08, mats)
    wood.add_box((0, hy + 0.08, ridge + 0.4), (0.05, 0.05, 0.7), mats["wood_dark"])
    for i in range(3):
        wood.add_box((0.16 + i * 0.14, hy + 0.2, ridge + 0.35 - i * 0.05), (0.16, 0.02, 0.22), mats["cross"] if i % 2 == 0 else mats["brick"])
    for part in (stone, wood, roof, win):
        part.finish(root)


def build_cobble(root, coll, mats):
    b = Bucket("path_cobble", coll)
    b.add_box((0, 0, 0.02), (0.6, 0.6, 0.035), mats["soil"])
    stones = mats["stones"] + mats["stones_warm"]
    i = 0
    y = -0.24
    while y < 0.22:
        x = -0.26 + (0.06 if i % 2 else 0)
        while x < 0.24:
            w = 0.16 if (i + int(x * 10)) % 2 == 0 else 0.11
            b.add_box((x + w * 0.4, y, 0.045), (w, 0.1, 0.03), stones[(i + int((x + 2) * 5)) % len(stones)])
            x += w + 0.02
        y += 0.12
        i += 1
    b.finish(root)


def build_terracotta(root, coll, mats):
    b = Bucket("path_tile", coll)
    b.add_box((0, 0, 0.018), (0.6, 0.6, 0.03), mats["roof_dark"])
    colors = mats["roofs"]
    row = 0
    y = -0.24
    while y < 0.22:
        x = -0.26 + (0.08 if row % 2 else 0.0)
        col = 0
        while x < 0.22:
            b.add_box((x + 0.07, y, 0.042), (0.13, 0.09, 0.025), colors[(row + col) % len(colors)])
            x += 0.15
            col += 1
        y += 0.11
        row += 1
    b.finish(root)


def build_slab(root, coll, mats):
    b = Bucket("path_slab", coll)
    b.add_box((0, 0, 0.02), (0.6, 0.6, 0.03), mats["stones"][2])
    slabs = ((-0.12, -0.1, 0.28, 0.26), (0.14, -0.08, 0.22, 0.3), (-0.08, 0.16, 0.32, 0.18), (0.16, 0.16, 0.18, 0.16))
    for i, (x, y, w, d) in enumerate(slabs):
        b.add_box((x, y, 0.045), (w, d, 0.028), mats["stones_warm"][i % 2] if i % 2 == 0 else mats["plaster"])
    b.finish(root)


def make_materials():
    mats = {
        "plaster": make_material("VN_Civ_plaster", "#f0e2c8"),
        "plaster2": make_material("VN_Civ_plaster2", "#e4d3b6"),
        "wood": make_material("VN_Civ_wood", "#6e4a30"),
        "wood_dark": make_material("VN_Civ_wood_dark", "#3f2818"),
        "door": make_material("VN_Civ_door", "#6a4328"),
        "frame": make_material("VN_Civ_frame", "#f3e6cf"),
        "glass": make_material("VN_Civ_glass", "#ffc56b", 0.32, "#ffb03a", 1.1),
        "interior": make_material("VN_Civ_interior", "#2a1c14"),
        "metal": make_material("VN_Civ_metal", "#3a3532", 0.72),
        "roof_dark": make_material("VN_Civ_roof_dark", "#8d3b2a"),
        "brick": make_material("VN_Civ_brick", "#a34432"),
        "cross": make_material("VN_Civ_cross", "#c62828"),
        "soil": make_material("VN_Civ_soil", "#6b4a32"),
        "leaf": make_material("VN_Civ_leaf", "#2f6e3c"),
        "flower_p": make_material("VN_Civ_flower_p", "#e58cb0"),
        "flower_r": make_material("VN_Civ_flower_r", "#d24b4b"),
        "flower_w": make_material("VN_Civ_flower_w", "#f4f1ea"),
        "flower_y": make_material("VN_Civ_flower_y", "#f0c14a"),
    }
    mats["stones"] = [make_material(f"VN_Civ_stone_{i}", h) for i, h in enumerate(("#8e8b86", "#a39e96", "#6f6d68", "#b7b2a8"))]
    mats["stones_warm"] = [make_material(f"VN_Civ_warm_{i}", h) for i, h in enumerate(("#c4b49a", "#d9cbb3", "#a89880"))]
    mats["roofs"] = [make_material(f"VN_Civ_roof_{i}", h, 0.9) for i, h in enumerate(("#c4513a", "#d4654a", "#b84432", "#a33a2c", "#e07a55"))]
    return mats


def new_root(name, coll):
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.4
    link_object(root, coll)
    return root


def bounds(root):
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
        raise RuntimeError(root.name + " empty")
    return min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)


def validate(root, limit_x, limit_y):
    x0, x1, y0, y1, z0, z1 = bounds(root)
    print(f"BOUNDS {root.name} x[{x0:.2f},{x1:.2f}] y[{y0:.2f},{y1:.2f}] z[{z0:.2f},{z1:.2f}]")
    if z0 < -0.03:
        raise RuntimeError(f"{root.name} sinks {z0:.3f}")
    if max(abs(x0), abs(x1)) > limit_x or max(abs(y0), abs(y1)) > limit_y:
        raise RuntimeError(f"{root.name} exceeds footprint")


def export_glb(root, path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    kwargs = {"filepath": str(path), "export_format": "GLB", "use_selection": True, "export_apply": True, "export_materials": "EXPORT"}
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
    scene.unit_settings.scale_length = 1.0
    world = scene.world or bpy.data.worlds.new("VN_World")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg is None:
        bg = world.node_tree.nodes.new("ShaderNodeBackground")
        out = world.node_tree.nodes.get("World Output") or world.node_tree.nodes.new("ShaderNodeOutputWorld")
        world.node_tree.links.new(bg.outputs["Background"], out.inputs["Surface"])
    bg.inputs[0].default_value = (0.53, 0.72, 0.90, 1)
    bg.inputs[1].default_value = 0.85
    try:
        scene.view_settings.view_transform = "Standard"
    except Exception:
        pass
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 750
    scene.render.image_settings.file_format = "PNG"
    for candidate in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "CYCLES"):
        try:
            scene.render.engine = candidate
            break
        except Exception:
            continue
    eevee = getattr(scene, "eevee", None)
    if eevee is not None and hasattr(eevee, "taa_render_samples"):
        eevee.taa_render_samples = 12


def add_sun(coll):
    bpy.ops.mesh.primitive_plane_add(size=40, location=(10, 0, -0.03))
    ground = bpy.context.active_object
    ground.data.materials.append(make_material("VN_Civ_ground", "#7ea36a", 0.95))
    link_object(ground, coll)
    bpy.ops.object.light_add(type="SUN", location=(8, 12, 14))
    sun = bpy.context.active_object
    sun.data.energy = 3.2
    sun.data.color = (1, 0.94, 0.84)
    look_at(sun, (0, 0, 2))
    link_object(sun, coll)


def hide(root, hidden):
    root.hide_render = hidden
    for child in root.children_recursive:
        child.hide_render = hidden


def render_views(stem, kind):
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    if kind == "building":
        shots = {"front": ((0.2, 14, 3.2), (0, 0, 2.4), 12), "iso": ((10, 10, 8), (0, 0, 2), 16)}
    elif kind == "gate":
        shots = {"front": ((0, 10, 2.4), (0, 0, 2), 8), "iso": ((7, 7, 5), (0, 0, 1.8), 10)}
    else:
        shots = {"iso": ((1.3, 1.3, 1.1), (0, 0, 0), 1.5)}
    for name, (loc, target, ortho) in shots.items():
        bpy.ops.object.camera_add(location=loc)
        cam = bpy.context.active_object
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho
        cam.data.clip_end = 80
        look_at(cam, target)
        scene.camera = cam
        scene.render.filepath = str(PREVIEW_DIR / f"{stem}_{name}.png")
        bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(cam, do_unlink=True)
        print("RENDERED", scene.render.filepath)


def main():
    clear_scene()
    setup_world()
    add_sun(ensure_collection("RenderOnly"))
    mats = make_materials()
    specs = (
        ("vn_building_school", build_school, "building", 3.95, 2.95),
        ("vn_building_hospital", build_hospital, "building", 2.95, 2.95),
        ("vn_building_townhouse", build_townhouse, "building", 2.05, 2.05),
        ("vn_building_town_gate", build_gate, "gate", 3.08, 1.05),
        ("vn_path_cobble", build_cobble, "path", 0.34, 0.34),
        ("vn_path_terracotta", build_terracotta, "path", 0.34, 0.34),
        ("vn_path_slab", build_slab, "path", 0.34, 0.34),
    )
    roots = []
    for index, (name, builder, kind, limx, limy) in enumerate(specs):
        for prev in roots:
            hide(prev, True)
        coll = ensure_collection(name)
        root = new_root(name, coll)
        builder(root, coll, mats)
        validate(root, limx, limy)
        export_glb(root, MODEL_DIR / f"{name}.glb")
        hide(root, False)
        render_views(name, kind)
        root.location.x = (index + 1) * 12
        bpy.context.view_layer.update()
        roots.append(root)
    try:
        bpy.context.preferences.filepaths.save_version = 0
    except Exception:
        pass
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    keep = Path(str(BLEND_PATH) + ".import")
    if not keep.exists():
        keep.write_text("[remap]\n\nimporter=\"keep\"\n\n[deps]\n\n[params]\n", encoding="utf-8")
    print("SUCCESS")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        sys.exit(1)

# Vila Nova — two 2x2 cottages plus trees and bushes.
# Same matte language as vn_building_reference_cottage: terracotta tiles,
# stone, cream plaster, dark timber, warm amber glass, chunky foliage.
#
# blender --background --python Tools/Blender/generate_village_expansion.py
#
# Blender Z-up, origin at the footprint center on the ground, facade = +Y.
# GLB export is Y-up. Houses stay inside a 4x4 m (2x2 cells) footprint.
# Trees and bushes are 1x1 props and may overhang the cell the way CozyTree does.

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
BLEND_PATH = MODEL_DIR / "vn_village_expansion.blend"

HOUSE_LIMIT = 1.96
NATURE_LIMIT = 1.45


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
    specular: float = 0.08,
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
        spec.default_value = specular
    if emission and emission_strength > 0.0:
        emit_socket = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
        if emit_socket is not None:
            emit_socket.default_value = rgba(emission)
        strength = bsdf.inputs.get("Emission Strength")
        if strength is not None:
            strength.default_value = emission_strength
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def subtract_intervals(u0: float, u1: float, holes: list[tuple[float, float]]) -> list[tuple[float, float]]:
    spans = [(u0, u1)]
    for h0, h1 in holes:
        nxt: list[tuple[float, float]] = []
        for a, b in spans:
            if h1 <= a or h0 >= b:
                nxt.append((a, b))
                continue
            if a < h0:
                nxt.append((a, min(b, h0)))
            if h1 < b:
                nxt.append((max(a, h1), b))
        spans = [(a, b) for a, b in nxt if b - a > 0.015]
    return spans


def rects_minus_holes(
    u0: float,
    u1: float,
    z0: float,
    z1: float,
    holes: list[tuple[float, float, float, float]],
) -> list[tuple[float, float, float, float]]:
    clipped: list[tuple[float, float, float, float]] = []
    zs = {z0, z1}
    for hu0, hu1, hz0, hz1 in holes:
        za, zb = max(hz0, z0), min(hz1, z1)
        ua, ub = max(hu0, u0), min(hu1, u1)
        if zb - za > 0.012 and ub - ua > 0.012:
            clipped.append((ua, ub, za, zb))
            zs.add(za)
            zs.add(zb)
    ordered = sorted(zs)
    out: list[tuple[float, float, float, float]] = []
    for za, zb in zip(ordered, ordered[1:]):
        if zb - za < 0.012:
            continue
        mid = (za + zb) * 0.5
        blockers = [(h[0], h[1]) for h in clipped if h[2] - 1.0e-6 < mid < h[3] + 1.0e-6]
        for ua, ub in subtract_intervals(u0, u1, blockers):
            out.append((ua, ub, za, zb))
    return out


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

    def add_box(
        self,
        center: tuple[float, float, float] | Vector,
        size: tuple[float, float, float] | Vector,
        mat: bpy.types.Material,
        rotation: Euler | None = None,
    ) -> None:
        sx, sy, sz = float(size[0]), float(size[1]), float(size[2])
        if sx <= 0.004 or sy <= 0.004 or sz <= 0.004:
            return
        geom = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = geom["verts"]
        bmesh.ops.scale(self.bm, verts=verts, vec=Vector((sx, sy, sz)))
        if rotation is not None:
            bmesh.ops.rotate(
                self.bm,
                verts=verts,
                cent=Vector((0.0, 0.0, 0.0)),
                matrix=rotation.to_matrix(),
            )
        bmesh.ops.translate(self.bm, verts=verts, vec=Vector(center))
        idx = self.slot(mat)
        faces = {f for v in verts for f in v.link_faces}
        for face in faces:
            face.material_index = idx
            face.smooth = False

    def add_cylinder(
        self,
        center: tuple[float, float, float],
        radius_bottom: float,
        radius_top: float,
        height: float,
        mat: bpy.types.Material,
        segments: int = 8,
    ) -> None:
        if height <= 0.004 or radius_bottom <= 0.004 or radius_top <= 0.004:
            return
        bm = self.bm
        idx = self.slot(mat)
        cx, cy, cz = center
        z0 = cz - height * 0.5
        z1 = cz + height * 0.5
        bottom = []
        top = []
        for i in range(segments):
            angle = math.tau * i / segments
            c = math.cos(angle)
            s = math.sin(angle)
            bottom.append(bm.verts.new((cx + c * radius_bottom, cy + s * radius_bottom, z0)))
            top.append(bm.verts.new((cx + c * radius_top, cy + s * radius_top, z1)))
        bm.verts.ensure_lookup_table()
        cap_b = bm.faces.new(list(reversed(bottom)))
        cap_t = bm.faces.new(top)
        cap_b.material_index = idx
        cap_t.material_index = idx
        cap_b.smooth = False
        cap_t.smooth = False
        for i in range(segments):
            j = (i + 1) % segments
            side = bm.faces.new((bottom[i], top[i], top[j], bottom[j]))
            side.material_index = idx
            side.smooth = False

    def add_prism_y(self, pts_xz: list[tuple[float, float]], y0: float, y1: float, mat: bpy.types.Material) -> None:
        self._prism(pts_xz, y0, y1, mat, "y")

    def add_prism_x(self, pts_yz: list[tuple[float, float]], x0: float, x1: float, mat: bpy.types.Material) -> None:
        self._prism(pts_yz, x0, x1, mat, "x")

    def _prism(self, pts: list[tuple[float, float]], c0: float, c1: float, mat: bpy.types.Material, axis: str) -> None:
        if len(pts) < 3 or abs(c1 - c0) < 0.004:
            return
        ordered = list(pts)
        area = 0.0
        for i, (x1, y1) in enumerate(ordered):
            x2, y2 = ordered[(i + 1) % len(ordered)]
            area += x1 * y2 - x2 * y1
        if abs(area) < 1.0e-5:
            return
        if area < 0.0:
            ordered.reverse()
        lo, hi = (c0, c1) if c0 < c1 else (c1, c0)
        idx = self.slot(mat)
        bm = self.bm

        def coord(u: float, v: float, along: float) -> tuple[float, float, float]:
            if axis == "y":
                return (u, along, v)
            return (along, u, v)

        front = [bm.verts.new(coord(u, v, hi)) for u, v in ordered]
        back = [bm.verts.new(coord(u, v, lo)) for u, v in ordered]
        bm.verts.ensure_lookup_table()
        face_hi = bm.faces.new(front)
        face_lo = bm.faces.new(list(reversed(back)))
        face_hi.material_index = idx
        face_lo.material_index = idx
        face_hi.smooth = False
        face_lo.smooth = False
        n = len(ordered)
        for i in range(n):
            j = (i + 1) % n
            side = bm.faces.new((front[i], back[i], back[j], front[j]))
            side.material_index = idx
            side.smooth = False

    def finish(self, parent: bpy.types.Object) -> bpy.types.Object | None:
        if len(self.bm.verts) == 0:
            self.bm.free()
            return None
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
        return obj


def add_wall(
    bucket: Bucket,
    face_axis: str,
    face: float,
    thickness: float,
    u0: float,
    u1: float,
    z0: float,
    z1: float,
    holes: list[tuple[float, float, float, float]],
    mat: bpy.types.Material,
) -> None:
    for ua, ub, za, zb in rects_minus_holes(u0, u1, z0, z1, holes):
        if face_axis == "y":
            bucket.add_box(((ua + ub) * 0.5, face, (za + zb) * 0.5), (ub - ua, thickness, zb - za), mat)
        else:
            bucket.add_box((face, (ua + ub) * 0.5, (za + zb) * 0.5), (thickness, ub - ua, zb - za), mat)


def fill_arch_spandrels(
    bucket: Bucket,
    face_axis: str,
    face: float,
    thickness: float,
    cx: float,
    z_spring: float,
    radius: float,
    mat: bpy.types.Material,
    steps: int = 6,
) -> None:
    """Fill the corners of an arch's bounding square so the opening reads as a curve."""
    for i in range(steps):
        z_a = z_spring + radius * i / steps
        z_b = z_spring + radius * (i + 1) / steps
        z_mid = (z_a + z_b) * 0.5
        dz = z_mid - z_spring
        half = math.sqrt(max(radius * radius - dz * dz, 0.0))
        for side in (-1.0, 1.0):
            outer = cx + side * radius
            inner = cx + side * half
            ua, ub = (inner, outer) if inner < outer else (outer, inner)
            if ub - ua < 0.012:
                continue
            if face_axis == "y":
                bucket.add_box(((ua + ub) * 0.5, face, (z_a + z_b) * 0.5), (ub - ua, thickness, z_b - z_a), mat)
            else:
                bucket.add_box((face, (ua + ub) * 0.5, (z_a + z_b) * 0.5), (thickness, ub - ua, z_b - z_a), mat)


def add_arch_trim(
    bucket: Bucket,
    face_axis: str,
    face: float,
    cx: float,
    z_spring: float,
    radius: float,
    mat: bpy.types.Material,
    segments: int = 7,
) -> None:
    for i in range(segments):
        a0 = math.pi * i / segments
        a1 = math.pi * (i + 1) / segments
        arc = (a0 + a1) * 0.5
        along = math.cos(arc) * radius
        z = z_spring + math.sin(arc) * radius
        if face_axis == "y":
            rot = Euler((0.0, -arc - math.pi * 0.5, 0.0), "XYZ")
            bucket.add_box((cx + along, face, z), (0.18, 0.06, 0.1), mat, rot)
        else:
            rot = Euler((arc + math.pi * 0.5, 0.0, 0.0), "XYZ")
            bucket.add_box((face, cx + along, z), (0.06, 0.18, 0.1), mat, rot)


def _overlaps_hole(ua: float, ub: float, za: float, zb: float, holes: list[tuple[float, float, float, float]]) -> bool:
    for hu0, hu1, hz0, hz1 in holes:
        if ua < hu1 - 0.02 and ub > hu0 + 0.02 and za < hz1 - 0.02 and zb > hz0 + 0.02:
            return True
    return False


def add_courses(
    bucket: Bucket,
    face_axis: str,
    face: float,
    thickness: float,
    u0: float,
    u1: float,
    z0: float,
    z1: float,
    stones: list[bpy.types.Material],
    course: float = 0.22,
    holes: list[tuple[float, float, float, float]] | None = None,
) -> None:
    holes = holes or []
    z = z0
    i = 0
    while z < z1 - 0.02 and i < 24:
        h = min(course if i % 2 == 0 else course * 0.78, z1 - z)
        stagger = 0.08 if i % 2 else 0.0
        u = u0 - stagger
        col = 0
        while u < u1 - 0.04 and col < 16:
            w = 0.34 if (i + col) % 3 else 0.22
            if (i + col) % 5 == 0:
                w = 0.46
            ua = max(u, u0)
            ub = min(u + w, u1)
            if ub - ua > 0.06 and not _overlaps_hole(ua, ub, z, z + h, holes):
                mat = stones[(i * 3 + col) % len(stones)]
                if face_axis == "y":
                    bucket.add_box(((ua + ub) * 0.5, face, z + h * 0.5), (ub - ua - 0.012, thickness, h - 0.012), mat)
                else:
                    bucket.add_box((face, (ua + ub) * 0.5, z + h * 0.5), (thickness, ub - ua - 0.012, h - 0.012), mat)
            u += w
            col += 1
        z += h
        i += 1


def add_quoins(bucket: Bucket, x: float, y: float, z0: float, z1: float, sx: float, sy: float, stones: list) -> None:
    z = z0
    i = 0
    while z < z1 - 0.04 and i < 28:
        h = 0.24 if i % 2 == 0 else 0.16
        long_x = 0.36 if i % 2 == 0 else 0.22
        long_y = 0.22 if i % 2 == 0 else 0.36
        if z + h > z1:
            h = z1 - z
        bucket.add_box((x + sx * 0.02, y + sy * 0.02, z + h * 0.5), (long_x, long_y, h - 0.01), stones[i % len(stones)])
        z += h
        i += 1


def add_flower(bucket: Bucket, x: float, y: float, z: float, color: bpy.types.Material, mats: dict, scale: float = 1.0) -> None:
    stem = 0.12 * scale
    bucket.add_box((x, y, z + stem * 0.5), (0.025 * scale, 0.025 * scale, stem), mats["leaf_stem"])
    petal_z = z + stem
    petal = 0.075 * scale
    for dx, dy in ((petal, 0.0), (-petal, 0.0), (0.0, petal), (0.0, -petal)):
        bucket.add_box((x + dx, y + dy, petal_z), (0.07 * scale, 0.07 * scale, 0.04 * scale), color)
    bucket.add_box((x, y, petal_z + 0.02 * scale), (0.045 * scale, 0.045 * scale, 0.035 * scale), mats["flower_y"])


def add_ivy(bucket: Bucket, x: float, y: float, z0: float, z1: float, nx: float, ny: float, mats: dict) -> None:
    leaves = mats["leaves"]
    length = math.hypot(nx, ny) or 1.0
    nx, ny = nx / length, ny / length
    tx, ty = -ny, nx
    z = z0
    i = 0
    while z < z1 and i < 22:
        wave = math.sin(i * 0.9) * 0.14
        px = x + nx * 0.06 + tx * wave
        py = y + ny * 0.06 + ty * wave
        for k, (along, out, up, size) in enumerate(
            ((0.0, 0.0, 0.0, 0.14), (0.06, 0.025, 0.035, 0.1), (-0.05, 0.02, 0.03, 0.09))
        ):
            bucket.add_box(
                (px + tx * along + nx * out, py + ty * along + ny * out, z + up),
                (size, size * 0.7, size * 0.5),
                leaves[(i + k) % len(leaves)],
                Euler((0.35, 0.2 * k, i * 0.4), "XYZ"),
            )
        z += 0.18
        i += 1


def add_leaf_clump(
    bucket: Bucket,
    x: float,
    y: float,
    z: float,
    radius: float,
    mats: dict,
    seed: int,
    puff: int = 5,
) -> None:
    leaves = mats["leaves"]
    offsets = (
        (0.0, 0.0, 0.05, 1.0),
        (0.55, 0.15, 0.12, 0.72),
        (-0.48, 0.2, 0.02, 0.68),
        (0.1, -0.5, 0.16, 0.64),
        (-0.15, 0.42, 0.28, 0.58),
        (0.35, 0.35, -0.08, 0.5),
        (-0.4, -0.28, 0.22, 0.48),
    )
    for i, (dx, dy, dz, s) in enumerate(offsets[:puff]):
        jitter = (((seed * 17 + i * 11) % 5) - 2) * 0.03
        bucket.add_box(
            (x + dx * radius, y + dy * radius, z + dz * radius + jitter),
            (radius * s * 1.15, radius * s, radius * s * 0.85),
            leaves[(seed + i) % len(leaves)],
            Euler((0.08 * ((i % 3) - 1), 0.15 * (i % 2), 0.2 * ((seed + i) % 3)), "XYZ"),
        )


def add_roof_ridge_y(
    tiles: Bucket,
    wood: Bucket,
    *,
    center_x: float,
    half: float,
    y0: float,
    y1: float,
    z_plate: float,
    pitch: float,
    oh_left: float,
    oh_right: float,
    mats: dict,
    chimneys: list[tuple[float, float, float]] | None = None,
) -> float:
    """Slopes face ±X. Returns ridge height."""
    tan_p = math.tan(pitch)
    sin_p = math.sin(pitch)
    cos_p = math.cos(pitch)
    ridge_z = z_plate + half * tan_p
    chimneys = chimneys or []
    roof_colors = mats["roofs"]
    sides = ((-1.0, half + oh_left, oh_left), (1.0, half + oh_right, oh_right))
    tile_len = 0.22
    tile_step = 0.13
    tile_w = 0.28
    for sign, span, oh in sides:
        slope_len = span / cos_p
        rot = Euler((0.0, -pitch if sign < 0.0 else pitch, 0.0), "XYZ")
        normal = Vector((-sin_p, 0.0, cos_p) if sign < 0.0 else (sin_p, 0.0, cos_p))
        eave_x = center_x + sign * (half + oh)
        eave_z = z_plate - oh * tan_p
        mid = slope_len * 0.5
        mid_x = eave_x + mid * cos_p if sign < 0.0 else eave_x - mid * cos_p
        mid_z = eave_z + mid * sin_p
        under = Vector((mid_x, (y0 + y1) * 0.5, mid_z)) - normal * 0.02
        tiles.add_box(under, (slope_len + 0.02, (y1 - y0) + 0.04, 0.035), mats["roof_dark"], rot)
        row = 0
        dist = 0.02
        while dist < slope_len - 0.04 and row < 40:
            row_c = dist + tile_len * 0.5
            x = eave_x + row_c * cos_p if sign < 0.0 else eave_x - row_c * cos_p
            z = eave_z + row_c * sin_p
            base = Vector((x, 0.0, z)) + normal * 0.035
            stagger = tile_w * 0.5 if row % 2 else 0.0
            y = y0 + stagger
            col = 0
            while y < y1 - 0.02 and col < 40:
                cy = y + tile_w * 0.5
                blocked = any(abs(base.x - cx) < rad and abs(cy - cy0) < rad for cx, cy0, rad in chimneys)
                if not blocked and y0 - 0.05 <= cy <= y1 + 0.05:
                    mat = roof_colors[(row * 3 + col * 5 + (0 if sign < 0 else 1)) % len(roof_colors)]
                    tiles.add_box((base.x, cy, base.z), (tile_len, tile_w * 0.9, 0.04), mat, rot)
                y += tile_w
                col += 1
            dist += tile_step
            row += 1
        y = y0 + 0.15
        index = 0
        while y < y1 - 0.08:
            tail = Vector((eave_x, y, eave_z)) - normal * 0.015
            wood.add_box(tail, (0.28, 0.06, 0.05), mats["wood_dark"] if index % 2 else mats["wood"], rot)
            y += 0.48
            index += 1
    y = y0
    cap_i = 0
    while y < y1 - 0.02:
        tiles.add_box((center_x, y + 0.14, ridge_z + 0.04), (0.18, 0.26, 0.055), roof_colors[cap_i % len(roof_colors)])
        y += 0.26
        cap_i += 1
    wood.add_box((center_x, (y0 + y1) * 0.5, ridge_z - 0.02), (0.1, y1 - y0, 0.07), mats["wood_dark"])
    for y_edge, y_sign in ((y1, 1.0), (y0, -1.0)):
        for sign, span, oh in sides:
            slope_len = span / cos_p
            rot = Euler((0.0, -pitch if sign < 0.0 else pitch, 0.0), "XYZ")
            eave_x = center_x + sign * (half + oh)
            eave_z = z_plate - oh * tan_p
            mid = slope_len * 0.5
            mx = eave_x + mid * cos_p if sign < 0.0 else eave_x - mid * cos_p
            mz = eave_z + mid * sin_p
            wood.add_box((mx, y_edge + y_sign * 0.03, mz), (slope_len, 0.06, 0.08), mats["wood"], rot)
    return ridge_z


def add_roof_ridge_x(
    tiles: Bucket,
    wood: Bucket,
    *,
    center_y: float,
    half: float,
    x0: float,
    x1: float,
    z_plate: float,
    pitch: float,
    oh_neg: float,
    oh_pos: float,
    mats: dict,
    chimneys: list[tuple[float, float, float]] | None = None,
) -> float:
    """Slopes face ±Y. Chimney tuples are (y, x, radius). Returns ridge height."""
    tan_p = math.tan(pitch)
    sin_p = math.sin(pitch)
    cos_p = math.cos(pitch)
    ridge_z = z_plate + half * tan_p
    chimneys = chimneys or []
    roof_colors = mats["roofs"]
    sides = ((-1.0, half + oh_neg, oh_neg), (1.0, half + oh_pos, oh_pos))
    tile_len = 0.22
    tile_step = 0.13
    tile_w = 0.28
    for sign, span, oh in sides:
        slope_len = span / cos_p
        # Local +Z aligns with the outward roof normal. See generator notes.
        rot = Euler((pitch if sign < 0.0 else -pitch, 0.0, 0.0), "XYZ")
        normal = Vector((0.0, -sin_p, cos_p) if sign < 0.0 else (0.0, sin_p, cos_p))
        eave_y = center_y + sign * (half + oh)
        eave_z = z_plate - oh * tan_p
        mid = slope_len * 0.5
        mid_y = eave_y + mid * cos_p if sign < 0.0 else eave_y - mid * cos_p
        mid_z = eave_z + mid * sin_p
        under = Vector(((x0 + x1) * 0.5, mid_y, mid_z)) - normal * 0.02
        tiles.add_box(under, ((x1 - x0) + 0.04, slope_len + 0.02, 0.035), mats["roof_dark"], rot)
        row = 0
        dist = 0.02
        while dist < slope_len - 0.04 and row < 40:
            row_c = dist + tile_len * 0.5
            y = eave_y + row_c * cos_p if sign < 0.0 else eave_y - row_c * cos_p
            z = eave_z + row_c * sin_p
            base = Vector((0.0, y, z)) + normal * 0.035
            stagger = tile_w * 0.5 if row % 2 else 0.0
            x = x0 + stagger
            col = 0
            while x < x1 - 0.02 and col < 40:
                cx = x + tile_w * 0.5
                blocked = any(abs(base.y - cy) < rad and abs(cx - cxx) < rad for cy, cxx, rad in chimneys)
                if not blocked and x0 - 0.05 <= cx <= x1 + 0.05:
                    mat = roof_colors[(row * 3 + col * 5 + (0 if sign < 0 else 2)) % len(roof_colors)]
                    tiles.add_box((cx, base.y, base.z), (tile_w * 0.9, tile_len, 0.04), mat, rot)
                x += tile_w
                col += 1
            dist += tile_step
            row += 1
        x = x0 + 0.16
        index = 0
        while x < x1 - 0.08:
            tail = Vector((x, eave_y, eave_z)) - normal * 0.015
            wood.add_box(tail, (0.06, 0.28, 0.05), mats["wood_dark"] if index % 2 else mats["wood"], rot)
            x += 0.48
            index += 1
    x = x0
    cap_i = 0
    while x < x1 - 0.02:
        tiles.add_box((x + 0.14, center_y, ridge_z + 0.04), (0.26, 0.18, 0.055), roof_colors[cap_i % len(roof_colors)])
        x += 0.26
        cap_i += 1
    wood.add_box(((x0 + x1) * 0.5, center_y, ridge_z - 0.02), (x1 - x0, 0.1, 0.07), mats["wood_dark"])
    for x_edge, x_sign in ((x1, 1.0), (x0, -1.0)):
        for sign, span, oh in sides:
            slope_len = span / cos_p
            rot = Euler((pitch if sign < 0.0 else -pitch, 0.0, 0.0), "XYZ")
            eave_y = center_y + sign * (half + oh)
            eave_z = z_plate - oh * tan_p
            mid = slope_len * 0.5
            my = eave_y + mid * cos_p if sign < 0.0 else eave_y - mid * cos_p
            mz = eave_z + mid * sin_p
            wood.add_box((x_edge + x_sign * 0.03, my, mz), (0.06, slope_len, 0.08), mats["wood"], rot)
    return ridge_z


def build_chimney(bucket: Bucket, x: float, y: float, z_base: float, height: float, mats: dict) -> None:
    stones = mats["stones"]
    courses = max(4, int(height / 0.16))
    course_h = height / courses
    for i in range(courses):
        brick = i >= courses - 3
        w = 0.42 if brick else 0.5
        mat = (mats["brick"] if i % 2 == 0 else mats["brick_dark"]) if brick else stones[(i * 2) % len(stones)]
        bucket.add_box((x, y, z_base + course_h * (i + 0.5)), (w, w, course_h - 0.01), mat)
    cap_z = z_base + height + 0.035
    bucket.add_box((x, y, cap_z), (0.62, 0.62, 0.07), mats["stone_trim"])
    bucket.add_box((x, y, cap_z + 0.08), (0.34, 0.34, 0.1), mats["brick"])


def add_flag(bucket: Bucket, x: float, y: float, z: float, mats: dict) -> None:
    bucket.add_box((x, y, z + 0.42), (0.035, 0.035, 0.88), mats["wood_dark"])
    for i in range(4):
        bucket.add_box(
            (x + 0.16 + i * 0.12, y + 0.03, z + 0.72 - i * 0.055),
            (0.16, 0.02, 0.28 - i * 0.03),
            mats["flag"] if i % 2 == 0 else mats["flag_dark"],
            Euler((0.15, 0.0, -0.18 - i * 0.05), "XYZ"),
        )


def add_door(doors: Bucket, windows: Bucket, x: float, y: float, z: float, width: float, height: float, mats: dict, face: str = "y") -> None:
    plank_w = width / 4.0
    for i in range(4):
        px = -width * 0.5 + plank_w * (i + 0.5)
        mat = mats["door"] if i % 2 == 0 else mats["wood"]
        if face == "y":
            doors.add_box((x + px, y, z + height * 0.46), (plank_w * 0.94, 0.05, height * 0.92), mat)
        else:
            doors.add_box((y, x + px, z + height * 0.46), (0.05, plank_w * 0.94, height * 0.92), mat)
    if face == "y":
        doors.add_box((x + width * 0.28, y + 0.04, z + height * 0.48), (0.04, 0.03, 0.1), mats["metal"])
        windows.add_box((x, y - 0.02, z + height * 0.92), (width * 0.72, 0.03, height * 0.16), mats["glass"])
    else:
        doors.add_box((y + 0.04, x + width * 0.28, z + height * 0.48), (0.03, 0.04, 0.1), mats["metal"])
        windows.add_box((y - 0.02, x, z + height * 0.92), (0.03, width * 0.72, height * 0.16), mats["glass"])


def add_window(windows: Bucket, x: float, y: float, z: float, width: float, height: float, mats: dict, face: str = "y") -> None:
    if face == "y":
        windows.add_box((x, y, z), (width * 0.78, 0.03, height * 0.62), mats["glass"])
        windows.add_box((x, y + 0.02, z), (0.035, 0.04, height * 0.7), mats["frame"])
        windows.add_box((x, y + 0.02, z), (width * 0.72, 0.04, 0.035), mats["frame"])
        windows.add_box((x, y + 0.025, z - height * 0.42), (width * 0.9, 0.06, 0.05), mats["stone_trim"])
    else:
        windows.add_box((y, x, z), (0.03, width * 0.78, height * 0.62), mats["glass"])
        windows.add_box((y + 0.02, x, z), (0.04, 0.035, height * 0.7), mats["frame"])
        windows.add_box((y + 0.02, x, z), (0.04, width * 0.72, 0.035), mats["frame"])
        windows.add_box((y + 0.025, x, z - height * 0.42), (0.06, width * 0.9, 0.05), mats["stone_trim"])


def add_planter(deco: Bucket, veg: Bucket, x: float, y: float, z: float, length: float, mats: dict, along_x: bool = True) -> None:
    colors = (mats["flower_p"], mats["flower_w"], mats["flower_r"])
    if along_x:
        deco.add_box((x, y, z), (length, 0.14, 0.1), mats["wood"])
        deco.add_box((x, y, z + 0.02), (length - 0.05, 0.1, 0.05), mats["soil"])
        for i, color in enumerate(colors):
            add_flower(veg, x - length * 0.28 + i * length * 0.28, y, z + 0.04, color, mats, 0.75)
    else:
        deco.add_box((x, y, z), (0.14, length, 0.1), mats["wood"])
        deco.add_box((x, y, z + 0.02), (0.1, length - 0.05, 0.05), mats["soil"])
        for i, color in enumerate(colors):
            add_flower(veg, x, y - length * 0.28 + i * length * 0.28, z + 0.04, color, mats, 0.75)


def add_porch(
    deco: Bucket,
    tiles: Bucket,
    x: float,
    y_wall: float,
    z: float,
    width: float,
    depth: float,
    mats: dict,
) -> None:
    y_post = y_wall + depth * 0.72
    post_base = 0.24
    post_h = max(0.35, z - post_base)
    for sx in (-width * 0.42, width * 0.42):
        deco.add_box((x + sx, y_post, post_base + post_h * 0.5), (0.08, 0.08, post_h), mats["wood_dark"])
    deco.add_box((x, y_post, z), (width, 0.08, 0.08), mats["wood"])
    # Shed tiles falling toward +Y.
    pitch = math.radians(28.0)
    rot = Euler((-pitch, 0.0, 0.0), "XYZ")
    span = depth + 0.18
    tiles.add_box(
        (x, y_wall + span * 0.35, z + 0.08),
        (width + 0.08, span, 0.04),
        mats["roof_dark"],
        rot,
    )
    colors = mats["roofs"]
    col = 0
    u = x - width * 0.5
    while u < x + width * 0.45 and col < 12:
        tiles.add_box((u + 0.12, y_wall + span * 0.35, z + 0.12), (0.22, span * 0.92, 0.035), colors[col % len(colors)], rot)
        u += 0.2
        col += 1


def add_steps(bucket: Bucket, x: float, y: float, mats: dict) -> None:
    stones = mats["stones_warm"]
    bucket.add_box((x, y + 0.1, 0.07), (0.78, 0.36, 0.14), stones[0])
    bucket.add_box((x, y + 0.28, 0.16), (0.64, 0.22, 0.08), stones[1])


def timber_frame(bucket: Bucket, x0: float, x1: float, y: float, z0: float, z1: float, mats: dict, posts: int = 3) -> None:
    t = 0.07
    bucket.add_box(((x0 + x1) * 0.5, y, z0), (x1 - x0, t, 0.08), mats["wood_dark"])
    bucket.add_box(((x0 + x1) * 0.5, y, z1), (x1 - x0, t, 0.08), mats["wood_dark"])
    bucket.add_box(((x0 + x1) * 0.5, y, (z0 + z1) * 0.5), (x1 - x0, t * 0.8, 0.06), mats["wood"])
    for i in range(posts):
        x = x0 + (x1 - x0) * i / (posts - 1)
        bucket.add_box((x, y, (z0 + z1) * 0.5), (0.07, t, z1 - z0), mats["wood_dark"])
    panel = (x1 - x0) / (posts - 1)
    for i in range(posts - 1):
        xa = x0 + panel * i + 0.08
        xb = x0 + panel * (i + 1) - 0.08
        # Diagonal brace as a rotated beam.
        dx = xb - xa
        dz = (z1 - z0) * 0.45
        angle = math.atan2(dz, dx) * (1.0 if i % 2 == 0 else -1.0)
        length = math.hypot(dx, dz)
        z = z0 + 0.18 + (0.15 if i % 2 else (z1 - z0) * 0.35)
        bucket.add_box(((xa + xb) * 0.5, y, z), (length * 0.92, 0.05, 0.055), mats["wood"], Euler((0.0, angle, 0.0)))


def build_timber_cottage(root: bpy.types.Object, cols: dict[str, bpy.types.Collection], mats: dict) -> None:
    """Front-gable half-timber cottage. Jetty, one chimney, porch, flag."""
    walls = Bucket("timber_walls", cols["Structure"])
    stone = Bucket("timber_stone", cols["Structure"])
    timber = Bucket("timber_frame", cols["Structure"])
    roof_tiles = Bucket("timber_roof", cols["Roof"])
    roof_wood = Bucket("timber_roof_wood", cols["Roof"])
    doors = Bucket("timber_doors", cols["Doors"])
    windows = Bucket("timber_windows", cols["Windows"])
    chimneys = Bucket("timber_chimney", cols["Chimneys"])
    deco = Bucket("timber_deco", cols["Decor"])
    veg = Bucket("timber_veg", cols["Vegetation"])

    hx = 1.26
    y0, y1 = -1.08, 1.02
    jetty = 1.18
    found = 0.28
    ground_top = 1.70
    plate = 3.02
    pitch = math.radians(48.0)
    door_cx, door_r = -0.18, 0.34
    door_spring = found + 1.05
    win_cx, win_r = 0.62, 0.26
    win_spring = found + 0.72
    upper_wins = ((-0.48, 0.22), (0.46, 0.22))

    def front_holes(z_lo: float, z_hi: float, kind: str) -> list[tuple[float, float, float, float]]:
        holes = []
        if kind == "ground":
            holes.append((door_cx - door_r, door_cx + door_r, found, door_spring + door_r))
            holes.append((win_cx - win_r, win_cx + win_r, win_spring - 0.02, win_spring + win_r))
        else:
            for cx, r in upper_wins:
                holes.append((cx - r, cx + r, plate - 0.95, plate - 0.95 + r * 2.05))
        return [h for h in holes if h[3] > z_lo and h[2] < z_hi]

    # Foundation courses and plaster ground floor.
    add_courses(stone, "y", y1 + 0.02, 0.16, -hx, hx, 0.0, found, mats["stones_warm"], 0.14)
    add_courses(stone, "y", y0 - 0.02, 0.16, -hx, hx, 0.0, found, mats["stones"], 0.14)
    add_courses(stone, "x", -hx - 0.02, 0.16, y0, y1, 0.0, found, mats["stones"], 0.14)
    add_courses(stone, "x", hx + 0.02, 0.16, y0, y1, 0.0, found, mats["stones_warm"], 0.14)

    add_wall(walls, "y", y1, 0.16, -hx, hx, found, ground_top, front_holes(found, ground_top, "ground"), mats["plaster"])
    add_wall(walls, "y", jetty, 0.16, -hx, hx, ground_top, plate, front_holes(ground_top, plate, "upper"), mats["plaster2"])
    add_wall(walls, "y", y0, 0.16, -hx, hx, found, plate, [(0.15, 0.7, found + 0.85, found + 1.45)], mats["plaster"])
    add_wall(walls, "x", -hx, 0.16, y0, jetty, found, plate, [(-0.15, 0.45, found + 0.7, found + 1.28)], mats["plaster2"])
    add_wall(walls, "x", hx, 0.16, y0, y1, found, plate, [], mats["plaster"])

    # Arch spandrels and trims.
    fill_arch_spandrels(walls, "y", y1, 0.16, door_cx, door_spring, door_r, mats["plaster"])
    fill_arch_spandrels(walls, "y", y1, 0.16, win_cx, win_spring, win_r, mats["plaster"])
    add_arch_trim(stone, "y", y1 + 0.1, door_cx, door_spring, door_r + 0.02, mats["stone_trim"])
    add_arch_trim(stone, "y", y1 + 0.1, win_cx, win_spring, win_r + 0.01, mats["stone_trim"])
    for cx, r in upper_wins:
        spring = plate - 0.95
        fill_arch_spandrels(walls, "y", jetty, 0.16, cx, spring, r, mats["plaster2"])
        add_arch_trim(timber, "y", jetty + 0.08, cx, spring, r + 0.01, mats["wood"])
        add_window(windows, cx, jetty + 0.1, spring + r * 0.45, r * 1.7, r * 1.5, mats)

    add_door(doors, windows, door_cx, y1 + 0.1, found, door_r * 1.85, door_spring - found, mats)
    windows.add_box((door_cx, y1 - 0.22, found + 0.85), (door_r * 2.15, 0.2, 1.75), mats["interior"])
    add_window(windows, win_cx, y1 + 0.1, win_spring + win_r * 0.4, win_r * 1.7, win_r * 1.5, mats)
    # Side and back windows.
    add_window(windows, 0.15, -hx - 0.1, found + 0.95, 0.5, 0.48, mats, face="x")
    add_window(windows, 0.42, y0 - 0.1, found + 1.12, 0.46, 0.5, mats)

    for x, y, sx, sy in ((-hx, y0, -1, -1), (-hx, jetty, -1, 1), (hx, y0, 1, -1), (hx, y1, 1, 1)):
        add_quoins(stone, x, y, 0.0, ground_top, sx, sy, mats["stones_warm"])

    timber_frame(timber, -hx + 0.08, hx - 0.08, jetty + 0.1, ground_top + 0.02, plate - 0.02, mats, 4)
    timber_frame_side = Bucket("timber_side_frame", cols["Structure"])
    # Side timber as vertical posts only, so the jetty reads in profile.
    for y in (y0 + 0.15, 0.05, jetty - 0.2):
        timber_frame_side.add_box((-hx - 0.1, y, (ground_top + plate) * 0.5), (0.06, 0.06, plate - ground_top), mats["wood_dark"])
    timber_frame_side.add_box((-hx - 0.1, (y0 + jetty) * 0.5, plate - 0.04), (0.06, jetty - y0, 0.07), mats["wood"])
    timber_frame_side.add_box((-hx - 0.1, (y0 + jetty) * 0.5, ground_top + 0.04), (0.06, jetty - y0, 0.08), mats["wood_dark"])

    # Jetty beam ends.
    x = -hx + 0.2
    while x < hx - 0.1:
        deco.add_box((x, (y1 + jetty) * 0.5, ground_top - 0.02), (0.1, jetty - y1 + 0.08, 0.1), mats["wood"])
        x += 0.38

    gable_y0 = y0 - 0.16
    gable_y1 = jetty + 0.2
    chimney = (0.58, 0.05, 0.34)
    ridge = add_roof_ridge_y(
        roof_tiles,
        roof_wood,
        center_x=0.0,
        half=hx,
        y0=gable_y0,
        y1=gable_y1,
        z_plate=plate,
        pitch=pitch,
        oh_left=0.18,
        oh_right=0.2,
        mats=mats,
        chimneys=[chimney],
    )
    # Gable plaster above the plate, front face.
    walls.add_prism_y(
        [(-hx + 0.02, plate - 0.02), (hx - 0.02, plate - 0.02), (0.0, ridge - 0.08)],
        jetty - 0.02,
        jetty + 0.06,
        mats["plaster2"],
    )
    # King post and collar in the gable.
    timber.add_box((0.0, jetty + 0.1, (plate + ridge) * 0.5), (0.07, 0.06, ridge - plate), mats["wood_dark"])
    timber.add_box((0.0, jetty + 0.1, plate + 0.28), (hx * 1.15, 0.05, 0.06), mats["wood"])

    roof_at_chimney = plate + (hx - abs(chimney[0])) * math.tan(pitch)
    build_chimney(chimneys, chimney[0], chimney[1], roof_at_chimney - 0.15, 1.15, mats)
    add_flag(deco, 0.02, jetty + 0.28, ridge - 0.15, mats)
    add_porch(deco, roof_tiles, door_cx, y1, found + 1.15, 1.05, 0.48, mats)
    add_steps(stone, door_cx, y1, mats)
    add_planter(deco, veg, win_cx, y1 + 0.16, found + 0.42, 0.7, mats)
    add_ivy(veg, -hx + 0.02, y1, found, ground_top * 0.95, 0.2, 1.0, mats)
    add_ivy(veg, hx - 0.15, y0, found * 0.5, found + 1.1, -0.2, -1.0, mats)
    # Base flowers.
    for i, color in enumerate((mats["flower_p"], mats["flower_w"], mats["flower_r"], mats["flower_y"])):
        add_flower(veg, -0.85 + i * 0.22, y1 + 0.28, 0.0, color, mats, 0.7)
    veg.add_box((hx - 0.35, y0 - 0.28, 0.16), (0.42, 0.36, 0.28), mats["leaves"][2])
    veg.add_box((hx - 0.22, y0 - 0.18, 0.28), (0.3, 0.28, 0.22), mats["leaves"][1])

    for bucket in (walls, stone, timber, timber_frame_side, roof_tiles, roof_wood, doors, windows, chimneys, deco, veg):
        bucket.finish(root)


def build_stone_cottage(root: bpy.types.Object, cols: dict[str, bpy.types.Collection], mats: dict) -> None:
    """Low side-gable stone cottage. Eave faces the street. Plaster only in the gables."""
    walls = Bucket("stone_walls", cols["Structure"])
    stone = Bucket("stone_blocks", cols["Structure"])
    timber = Bucket("stone_timber", cols["Structure"])
    roof_tiles = Bucket("stone_roof", cols["Roof"])
    roof_wood = Bucket("stone_roof_wood", cols["Roof"])
    doors = Bucket("stone_doors", cols["Doors"])
    windows = Bucket("stone_windows", cols["Windows"])
    chimneys = Bucket("stone_chimney", cols["Chimneys"])
    deco = Bucket("stone_deco", cols["Decor"])
    veg = Bucket("stone_veg", cols["Vegetation"])

    hx = 1.46
    hy = 1.00
    found = 0.26
    plate = 2.18
    pitch = math.radians(42.0)
    door_cx, door_r = 0.0, 0.36
    door_spring = found + 1.02
    wins = ((-0.78, 0.28), (0.78, 0.28))

    holes = [(door_cx - door_r, door_cx + door_r, found, door_spring + door_r)]
    for cx, r in wins:
        holes.append((cx - r, cx + r, found + 0.62, found + 0.62 + r * 2.15))
    back_holes = [(0.35, 0.95, found + 0.7, found + 1.35)]
    side_holes = [(-0.2, 0.35, found + 0.65, found + 1.25)]

    add_courses(stone, "y", hy + 0.01, 0.2, -hx, hx, 0.0, plate, mats["stones"], 0.2, holes)
    add_courses(stone, "y", -hy - 0.01, 0.18, -hx, hx, 0.0, plate, mats["stones_warm"], 0.2, back_holes)
    add_courses(stone, "x", hx + 0.01, 0.18, -hy, hy, 0.0, plate, mats["stones"], 0.2, side_holes)
    add_courses(stone, "x", -hx - 0.01, 0.18, -hy, hy, 0.0, plate, mats["stones_warm"], 0.2)
    # Plaster behind the stone so mortar gaps stay warm, and to close the courses.
    add_wall(walls, "y", hy - 0.02, 0.1, -hx, hx, found, plate, holes, mats["plaster"])
    add_wall(walls, "y", -hy + 0.02, 0.1, -hx, hx, found, plate, [(0.35, 0.95, found + 0.7, found + 1.35)], mats["plaster2"])
    add_wall(walls, "x", -hx + 0.02, 0.1, -hy, hy, found, plate, [], mats["plaster"])
    add_wall(walls, "x", hx - 0.02, 0.1, -hy, hy, found, plate, [(-0.2, 0.35, found + 0.65, found + 1.25)], mats["plaster2"])

    fill_arch_spandrels(walls, "y", hy - 0.02, 0.12, door_cx, door_spring, door_r, mats["plaster"])
    fill_arch_spandrels(stone, "y", hy + 0.01, 0.2, door_cx, door_spring, door_r, mats["plaster"])
    add_arch_trim(stone, "y", hy + 0.14, door_cx, door_spring, door_r + 0.02, mats["stone_trim"])
    add_door(doors, windows, door_cx, hy + 0.16, found, door_r * 1.9, door_spring - found, mats)
    windows.add_box((door_cx, hy - 0.08, (found + door_spring + door_r) * 0.5), (door_r * 2.2, 0.45, door_spring + door_r - found), mats["interior"])
    for cx, r in wins:
        spring = found + 0.62
        fill_arch_spandrels(walls, "y", hy - 0.02, 0.12, cx, spring, r, mats["plaster"])
        fill_arch_spandrels(stone, "y", hy + 0.01, 0.2, cx, spring, r, mats["plaster"])
        add_arch_trim(stone, "y", hy + 0.14, cx, spring, r + 0.01, mats["stone_trim"])
        add_window(windows, cx, hy + 0.16, spring + r * 0.55, r * 1.7, r * 1.6, mats)
    add_window(windows, 0.65, -hy - 0.12, found + 1.0, 0.5, 0.55, mats)
    add_window(windows, 0.08, hx + 0.12, found + 0.92, 0.46, 0.5, mats, face="x")

    # Cut stone visually in front of openings by leaving the plaster holes; courses are solid.
    # A dark recess sells the depth in front of the stone skin.

    chimney = (0.15, 0.85, 0.36)  # (y, x, rad) for the X-ridge roof
    ridge = add_roof_ridge_x(
        roof_tiles,
        roof_wood,
        center_y=0.0,
        half=hy,
        x0=-hx - 0.16,
        x1=hx + 0.18,
        z_plate=plate,
        pitch=pitch,
        oh_neg=0.2,
        oh_pos=0.28,
        mats=mats,
        chimneys=[chimney],
    )
    for x_face, x0, x1 in ((-hx, -hx - 0.04, -hx + 0.08), (hx, hx - 0.08, hx + 0.04)):
        walls.add_prism_x(
            [(-hy + 0.04, plate - 0.02), (hy - 0.04, plate - 0.02), (0.0, ridge - 0.05)],
            x0,
            x1,
            mats["plaster"],
        )
    # Round vent in the right gable, as a stone ring plus dark center.
    vent_z = plate + (ridge - plate) * 0.48
    deco.add_box((hx + 0.06, 0.0, vent_z), (0.06, 0.28, 0.28), mats["stone_trim"])
    windows.add_box((hx + 0.1, 0.0, vent_z), (0.04, 0.16, 0.16), mats["interior"])

    roof_y = chimney[0]
    roof_z = plate + (hy - abs(roof_y)) * math.tan(pitch)
    build_chimney(chimneys, chimney[1], chimney[0], roof_z - 0.1, 0.95, mats)

    add_porch(deco, roof_tiles, door_cx, hy, found + 1.08, 1.15, 0.5, mats)
    add_steps(stone, door_cx, hy, mats)
    add_planter(deco, veg, wins[0][0], hy + 0.2, found + 0.48, 0.62, mats)
    add_planter(deco, veg, wins[1][0], hy + 0.2, found + 0.48, 0.62, mats)
    add_ivy(veg, -hx + 0.15, hy, found, plate * 0.85, 0.15, 1.0, mats)
    for i, color in enumerate((mats["flower_r"], mats["flower_p"], mats["flower_w"])):
        add_flower(veg, -hx + 0.25, hy + 0.22 + i * 0.02, found + 0.15 * i, color, mats, 0.65)
    veg.add_box((-hx + 0.45, -hy - 0.22, 0.18), (0.5, 0.4, 0.32), mats["leaves"][0])
    veg.add_box((-hx + 0.25, -hy - 0.1, 0.3), (0.36, 0.3, 0.24), mats["leaves"][3])
    # Wood lintel over the door, in front of the stone.
    timber.add_box((door_cx, hy + 0.14, door_spring + door_r + 0.06), (door_r * 2.5, 0.08, 0.08), mats["wood_dark"])

    for bucket in (walls, stone, timber, roof_tiles, roof_wood, doors, windows, chimneys, deco, veg):
        bucket.finish(root)


def build_oak(root: bpy.types.Object, cols: dict[str, bpy.types.Collection], mats: dict) -> None:
    trunk = Bucket("oak_trunk", cols["Trunk"])
    leaves = Bucket("oak_leaves", cols["Foliage"])
    trunk.add_cylinder((0.0, 0.0, 0.85), 0.22, 0.13, 1.7, mats["trunk"], 8)
    trunk.add_cylinder((0.0, 0.0, 0.12), 0.3, 0.24, 0.24, mats["trunk_dark"], 8)
    trunk.add_box((0.22, 0.04, 1.95), (0.16, 0.14, 0.72), mats["trunk"], Euler((0.2, 0.15, 0.55), "XYZ"))
    trunk.add_box((-0.18, 0.06, 2.05), (0.14, 0.14, 0.62), mats["trunk"], Euler((-0.15, -0.2, -0.65), "XYZ"))
    add_leaf_clump(leaves, 0.0, 0.05, 2.55, 0.85, mats, 2, 7)
    add_leaf_clump(leaves, 0.45, -0.2, 2.15, 0.55, mats, 5, 5)
    add_leaf_clump(leaves, -0.4, 0.25, 2.25, 0.5, mats, 9, 5)
    add_leaf_clump(leaves, 0.1, 0.15, 3.05, 0.42, mats, 3, 4)
    for i, color in enumerate((mats["flower_w"], mats["flower_p"])):
        add_flower(leaves, -0.35 + i * 0.55, 0.55, 0.0, color, mats, 0.8)
    trunk.finish(root)
    leaves.finish(root)


def build_poplar(root: bpy.types.Object, cols: dict[str, bpy.types.Collection], mats: dict) -> None:
    trunk = Bucket("poplar_trunk", cols["Trunk"])
    leaves = Bucket("poplar_leaves", cols["Foliage"])
    trunk.add_cylinder((0.0, 0.0, 1.35), 0.13, 0.07, 2.7, mats["bark_light"], 8)
    trunk.add_cylinder((0.0, 0.0, 0.1), 0.18, 0.14, 0.2, mats["trunk_dark"], 8)
    for i, z in enumerate((0.45, 0.9, 1.4, 1.9, 2.35)):
        trunk.add_box((0.11, 0.0, z), (0.03, 0.1, 0.12), mats["trunk_dark"], Euler((0.0, 0.0, 0.3), "XYZ"))
        trunk.add_box((-0.08, 0.06, z + 0.22), (0.08, 0.025, 0.1), mats["trunk"])
    for i, z in enumerate((2.15, 2.55, 2.95, 3.35, 3.7)):
        radius = 0.62 - i * 0.08
        add_leaf_clump(leaves, 0.0, 0.0, z, radius, mats, 11 + i, 4)
    leaves.finish(root)
    trunk.finish(root)


def build_flower_bush(root: bpy.types.Object, cols: dict[str, bpy.types.Collection], mats: dict) -> None:
    leaves = Bucket("flower_bush_leaves", cols["Foliage"])
    add_leaf_clump(leaves, 0.0, 0.0, 0.18, 0.48, mats, 1, 6)
    add_leaf_clump(leaves, 0.22, -0.1, 0.16, 0.32, mats, 4, 4)
    add_leaf_clump(leaves, -0.18, 0.12, 0.14, 0.28, mats, 7, 3)
    for i, color in enumerate((mats["flower_p"], mats["flower_w"], mats["flower_r"], mats["flower_y"], mats["flower_p"])):
        angle = i * 1.25
        add_flower(leaves, math.cos(angle) * 0.28, math.sin(angle) * 0.24, 0.36 + (i % 2) * 0.1, color, mats, 0.9)
    leaves.finish(root)


def build_berry_bush(root: bpy.types.Object, cols: dict[str, bpy.types.Collection], mats: dict) -> None:
    leaves = Bucket("berry_bush_leaves", cols["Foliage"])
    add_leaf_clump(leaves, 0.0, 0.02, 0.3, 0.55, mats, 8, 6)
    add_leaf_clump(leaves, -0.22, 0.14, 0.34, 0.34, mats, 6, 4)
    add_leaf_clump(leaves, 0.2, -0.12, 0.24, 0.3, mats, 2, 3)
    berries = (mats["flower_r"], mats["brick"], mats["flower_r"])
    for i in range(11):
        angle = i * 0.62
        radius = 0.16 + (i % 3) * 0.12
        leaves.add_box(
            (math.cos(angle) * radius, math.sin(angle) * radius * 0.85, 0.34 + (i % 4) * 0.1),
            (0.08, 0.08, 0.08),
            berries[i % 3],
        )
    leaves.finish(root)


def make_materials() -> dict:
    stone_hex = ("#8e8b86", "#a39e96", "#6f6d68", "#b7b2a8", "#7a7772")
    warm_hex = ("#c4b49a", "#d9cbb3", "#a89880")
    roof_hex = ("#c4513a", "#d4654a", "#b84432", "#a33a2c", "#e07a55")
    leaf_hex = ("#3f8f4e", "#5aaa62", "#2f6e3c", "#6fba78", "#4f9160")
    mats: dict = {
        "plaster": make_material("VN_Exp_plaster", "#f0e2c8"),
        "plaster2": make_material("VN_Exp_plaster_shade", "#e4d3b6"),
        "wood": make_material("VN_Exp_wood", "#6e4a30", 0.9),
        "wood_dark": make_material("VN_Exp_wood_dark", "#3f2818", 0.88),
        "door": make_material("VN_Exp_door", "#6a4328", 0.88),
        "frame": make_material("VN_Exp_frame", "#f3e6cf", 0.9),
        "stone_trim": make_material("VN_Exp_stone_trim", "#b7b1a6", 0.93),
        "glass": make_material("VN_Exp_glass", "#ffc56b", 0.32, "#ffb03a", 1.15, 0.25),
        "interior": make_material("VN_Exp_interior", "#2a1c14", 0.95),
        "metal": make_material("VN_Exp_metal", "#3a3532", 0.72, specular=0.2),
        "soil": make_material("VN_Exp_soil", "#6b4a32", 0.95),
        "flag": make_material("VN_Exp_flag", "#c62828", 0.86),
        "flag_dark": make_material("VN_Exp_flag_dark", "#8e1c1c", 0.88),
        "brick": make_material("VN_Exp_brick", "#a34432", 0.9),
        "brick_dark": make_material("VN_Exp_brick_dark", "#7d3428", 0.9),
        "roof_dark": make_material("VN_Exp_roof_under", "#8d3b2a", 0.94),
        "flower_p": make_material("VN_Exp_flower_pink", "#e58cb0", 0.88),
        "flower_r": make_material("VN_Exp_flower_red", "#d24b4b", 0.88),
        "flower_w": make_material("VN_Exp_flower_white", "#f4f1ea", 0.9),
        "flower_y": make_material("VN_Exp_flower_yellow", "#f0c14a", 0.88),
        "leaf_stem": make_material("VN_Exp_leaf_stem", "#2f6e3c", 0.9),
        "trunk": make_material("VN_Exp_trunk", "#7a5230", 0.92),
        "trunk_dark": make_material("VN_Exp_trunk_dark", "#5c3d24", 0.93),
        "bark_light": make_material("VN_Exp_bark_light", "#d9cbb3", 0.9),
    }
    mats["stones"] = [make_material(f"VN_Exp_stone_{i}", h, 0.94) for i, h in enumerate(stone_hex)]
    mats["stones_warm"] = [make_material(f"VN_Exp_stone_warm_{i}", h, 0.94) for i, h in enumerate(warm_hex)]
    mats["roofs"] = [make_material(f"VN_Exp_roof_{i}", h, 0.9) for i, h in enumerate(roof_hex)]
    mats["leaves"] = [make_material(f"VN_Exp_leaf_{i}", h, 0.9) for i, h in enumerate(leaf_hex)]
    return mats


def new_root(name: str, collection: bpy.types.Collection) -> bpy.types.Object:
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.4
    link_object(root, collection)
    return root


def world_bounds(root: bpy.types.Object) -> tuple[float, float, float, float, float, float]:
    xs: list[float] = []
    ys: list[float] = []
    zs: list[float] = []
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


def validate(root: bpy.types.Object, limit: float) -> None:
    x0, x1, y0, y1, z0, z1 = world_bounds(root)
    print(f"BOUNDS {root.name} x[{x0:.2f},{x1:.2f}] y[{y0:.2f},{y1:.2f}] z[{z0:.2f},{z1:.2f}] size {(x1-x0):.2f}x{(y1-y0):.2f}x{(z1-z0):.2f}")
    if z0 < -0.03:
        raise RuntimeError(f"{root.name} sinks below ground ({z0:.3f})")
    if max(abs(x0), abs(x1), abs(y0), abs(y1)) > limit:
        raise RuntimeError(f"{root.name} exceeds footprint {limit:.2f} m")


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
    if not path.is_file() or path.stat().st_size < 1024:
        raise RuntimeError(f"GLB export failed: {path}")
    print(f"EXPORTED {path.name} ({path.stat().st_size} bytes)")


def look_at(obj: bpy.types.Object, target: tuple[float, float, float]) -> None:
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
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 825
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    for candidate in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "CYCLES"):
        try:
            scene.render.engine = candidate
            break
        except Exception:
            continue
    print(f"RENDER ENGINE {scene.render.engine}")
    if scene.render.engine == "CYCLES":
        scene.cycles.samples = 24
    eevee = getattr(scene, "eevee", None)
    if eevee is not None and hasattr(eevee, "taa_render_samples"):
        eevee.taa_render_samples = 16


def add_sun(coll: bpy.types.Collection) -> None:
    bpy.ops.mesh.primitive_plane_add(size=40.0, location=(14.0, 0.0, -0.03))
    ground = bpy.context.active_object
    ground.name = "render_ground"
    mat = make_material("VN_Exp_ground", "#7ea36a", 0.95)
    ground.data.materials.append(mat)
    link_object(ground, coll)

    bpy.ops.object.light_add(type="SUN", location=(8.0, 10.0, 14.0))
    sun = bpy.context.active_object
    sun.data.energy = 3.2
    sun.data.color = (1.0, 0.94, 0.84)
    look_at(sun, (0.0, 0.0, 1.5))
    link_object(sun, coll)

    bpy.ops.object.light_add(type="AREA", location=(-6.0, 4.0, 5.0))
    fill = bpy.context.active_object
    fill.data.energy = 180
    fill.data.size = 5.0
    fill.data.color = (0.85, 0.9, 1.0)
    look_at(fill, (0.0, 0.0, 1.2))
    link_object(fill, coll)


def set_render_visible(root: bpy.types.Object, visible: bool) -> None:
    root.hide_render = not visible
    for child in root.children_recursive:
        child.hide_render = not visible


def render_asset(root: bpy.types.Object, kind: str, stem: str) -> None:
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    if kind == "house":
        shots = {
            "front": ((0.1, 8.8, 2.3), (0.0, 0.0, 1.7), 48.0, False, 0.0),
            "iso": ((6.4, 6.4, 5.4), (0.0, 0.0, 1.4), 50.0, True, 7.2),
        }
    elif kind == "tree":
        shots = {"iso": ((4.8, 4.8, 3.6), (0.0, 0.0, 1.7), 50.0, True, 6.2)}
    else:
        shots = {"iso": ((2.8, 2.8, 1.8), (0.0, 0.0, 0.35), 50.0, True, 2.8)}
    for name, (loc, target, lens, ortho, ortho_scale) in shots.items():
        bpy.ops.object.camera_add(location=loc)
        cam = bpy.context.active_object
        cam.data.lens = lens
        cam.data.clip_end = 80.0
        if ortho:
            cam.data.type = "ORTHO"
            cam.data.ortho_scale = ortho_scale
        look_at(cam, target)
        scene.camera = cam
        scene.render.filepath = str(PREVIEW_DIR / f"{stem}_{name}.png")
        bpy.ops.render.render(write_still=True)
        print(f"RENDERED {scene.render.filepath}")
        bpy.data.objects.remove(cam, do_unlink=True)


def asset_cols(master_name: str) -> dict[str, bpy.types.Collection]:
    master = ensure_collection(master_name)
    names = ("Structure", "Roof", "Doors", "Windows", "Chimneys", "Decor", "Vegetation", "Trunk", "Foliage")
    return {name: ensure_collection(f"{master_name}_{name}", master) for name in names}


def main() -> None:
    clear_scene()
    setup_render_world()
    render_coll = ensure_collection("RenderOnly")
    add_sun(render_coll)
    mats = make_materials()

    specs = (
        ("vn_building_timber_cottage", "house", HOUSE_LIMIT, build_timber_cottage),
        ("vn_building_stone_cottage", "house", HOUSE_LIMIT, build_stone_cottage),
        ("vn_nature_oak_tree", "tree", NATURE_LIMIT, build_oak),
        ("vn_nature_poplar_tree", "tree", NATURE_LIMIT, build_poplar),
        ("vn_nature_flower_bush", "bush", NATURE_LIMIT, build_flower_bush),
        ("vn_nature_berry_bush", "bush", NATURE_LIMIT, build_berry_bush),
    )
    roots: list[bpy.types.Object] = []
    for index, (name, kind, limit, builder) in enumerate(specs):
        cols = asset_cols(name)
        root = new_root(name, cols["Structure"])
        for prev in roots:
            set_render_visible(prev, False)
        builder(root, cols, mats)
        validate(root, limit)
        export_glb(root, MODEL_DIR / f"{name}.glb")
        set_render_visible(root, True)
        render_asset(root, kind, name)
        root.location.x = (index + 1) * 8.0
        bpy.context.view_layer.update()
        roots.append(root)

    for prev in roots:
        set_render_visible(prev, True)
    center_x = (len(roots) + 1) * 4.0
    bpy.ops.object.camera_add(location=(center_x + 10.0, -16.0, 9.0))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 42.0
    look_at(cam, (center_x, 0.0, 1.0))
    bpy.context.scene.camera = cam
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    bpy.context.scene.render.filepath = str(PREVIEW_DIR / "vn_village_expansion_lineup.png")
    bpy.ops.render.render(write_still=True)
    print(f"RENDERED {bpy.context.scene.render.filepath}")

    BLEND_PATH.parent.mkdir(parents=True, exist_ok=True)
    try:
        bpy.context.preferences.filepaths.save_version = 0
    except Exception:
        pass
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    keep_import = Path(str(BLEND_PATH) + ".import")
    if not keep_import.exists():
        keep_import.write_text("[remap]\n\nimporter=\"keep\"\n\n[deps]\n\n[params]\n", encoding="utf-8")
    print(f"BLEND {BLEND_PATH}")
    print("SUCCESS")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        sys.exit(1)

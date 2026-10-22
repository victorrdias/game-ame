# Vila Nova — reference cottage (stylized voxel-inspired fantasy house)
# blender --background --python Tools/Blender/generate_reference_cottage.py
#
# Blender Z-up, origin at the center of the base, front facade = +Y.
# Footprint target: 3x3 cells = 6x6 m. GLB export is Y-up for Godot.
#
# The attached reference image was not present in the workspace. Massing below
# follows the architectural inventory in the sprint brief, composed so the
# silhouette stays specific: steep front gable, off-center porch, left stone /
# right timber split, right-side balcony under a longer eave, left chimney
# with a brick neck, rear chimney, red pennant on the ridge.

from __future__ import annotations

import math
import sys
import traceback
from pathlib import Path

import bpy
import bmesh
from mathutils import Euler, Vector

# ---------------------------------------------------------------------------
# Massing (meters)
# ---------------------------------------------------------------------------

SEED = 707

WALL_HALF_X = 2.15
WALL_Y0 = -2.00
WALL_Y1 = 1.82
WALL_T = 0.26
FOUND_H = 0.38
GROUND_H = 2.22
BAND_H = 0.12
UPPER_H = 1.95

Z_FLOOR1 = FOUND_H + GROUND_H
Z_UPPER0 = Z_FLOOR1 + BAND_H
Z_PLATE = Z_UPPER0 + UPPER_H

PITCH = math.radians(46.0)
TAN_P = math.tan(PITCH)
COS_P = math.cos(PITCH)
SIN_P = math.sin(PITCH)

OH_LEFT = 0.28
OH_RIGHT = 0.50
OH_BACK = 0.26
OH_FRONT = 0.30

RIDGE_X = 0.0
RIDGE_Z = Z_PLATE + WALL_HALF_X * TAN_P

DOOR_U = -0.48
DOOR_HW = 0.50
DOOR_RECT = 1.52

FOOTPRINT_LIMIT = 3.05

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parent.parent
EXPORT_PATH = PROJECT_ROOT / "Assets" / "Models" / "vn_building_reference_cottage.glb"
BLEND_PATH = PROJECT_ROOT / "Assets" / "Models" / "vn_building_reference_cottage.blend"
PREVIEW_DIR = PROJECT_ROOT / "Assets" / "ArtPreviews"

COLLECTION_NAMES = (
    "House_Structure",
    "House_Roof",
    "House_Doors",
    "House_Windows",
    "House_Chimneys",
    "House_Balcony",
    "House_Decorations",
    "House_Vegetation",
)


# ---------------------------------------------------------------------------
# Color
# ---------------------------------------------------------------------------


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


# ---------------------------------------------------------------------------
# Scene / materials / mesh bucket
# ---------------------------------------------------------------------------


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    for mat in list(bpy.data.materials):
        if mat.users == 0:
            bpy.data.materials.remove(mat)


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
    if hasattr(mat, "use_backface_culling"):
        mat.use_backface_culling = False
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
        emit = rgba(emission)
        emit_socket = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
        if emit_socket is not None:
            emit_socket.default_value = emit
        strength = bsdf.inputs.get("Emission Strength")
        if strength is not None:
            strength.default_value = emission_strength
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def signed_area(pts: list[tuple[float, float]]) -> float:
    area = 0.0
    n = len(pts)
    for i in range(n):
        x1, y1 = pts[i]
        x2, y2 = pts[(i + 1) % n]
        area += x1 * y2 - x2 * y1
    return area * 0.5


class Bucket:
    """Accumulates boxes and prisms into one mesh, then links it to a collection."""

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
            bmesh.ops.rotate(self.bm, verts=verts, cent=Vector((0.0, 0.0, 0.0)), matrix=rotation.to_matrix())
        bmesh.ops.translate(self.bm, verts=verts, vec=Vector(center))
        idx = self.slot(mat)
        faces = {f for v in verts for f in v.link_faces}
        for face in faces:
            face.material_index = idx
            face.smooth = False

    def _prism(self, pts: list[tuple[float, float]], c0: float, c1: float, mat: bpy.types.Material, axis: str) -> None:
        if len(pts) < 3 or abs(signed_area(pts)) < 1.0e-5 or abs(c1 - c0) < 0.004:
            return
        ordered = list(pts)
        if signed_area(ordered) < 0.0:
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

    def add_prism_y(self, pts_xz: list[tuple[float, float]], y0: float, y1: float, mat: bpy.types.Material) -> None:
        self._prism(pts_xz, y0, y1, mat, "y")

    def add_prism_x(self, pts_yz: list[tuple[float, float]], x0: float, x1: float, mat: bpy.types.Material) -> None:
        self._prism(pts_yz, x0, x1, mat, "x")

    def finish(self, parent: bpy.types.Object, bevel: float = 0.0) -> bpy.types.Object | None:
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
        if bevel > 0.0:
            try:
                mod = obj.modifiers.new("Bevel", "BEVEL")
                mod.width = bevel
                mod.segments = 1
                mod.limit_method = "ANGLE"
                if hasattr(mod, "angle_limit"):
                    mod.angle_limit = math.radians(42.0)
            except Exception as exc:  # pragma: no cover - blender version drift
                print(f"bevel skipped on {self.name}: {exc}")
        obj.parent = parent
        obj.matrix_parent_inverse = parent.matrix_world.inverted()
        return obj


def axis_loc(u: float, n: float, z: float, axis: str) -> tuple[float, float, float]:
    if axis == "y":
        return (u, n, z)
    return (n, u, z)


def axis_size(su: float, sn: float, sz: float, axis: str) -> tuple[float, float, float]:
    if axis == "y":
        return (su, sn, sz)
    return (sn, su, sz)


# ---------------------------------------------------------------------------
# Reusable components
# ---------------------------------------------------------------------------


def add_wall_box(
    bucket: Bucket,
    u0: float,
    u1: float,
    z0: float,
    z1: float,
    face: float,
    normal: float,
    thickness: float,
    axis: str,
    mat: bpy.types.Material,
    proud: float = 0.0,
) -> None:
    if u1 - u0 < 0.02 or z1 - z0 < 0.02 or thickness <= 0.0:
        return
    um = (u0 + u1) * 0.5
    zm = (z0 + z1) * 0.5
    n = face - normal * (thickness * 0.5) + normal * proud
    bucket.add_box(axis_loc(um, n, zm, axis), axis_size(u1 - u0, thickness, z1 - z0, axis), mat)


def fill_stone(
    bucket: Bucket,
    u0: float,
    u1: float,
    z0: float,
    z1: float,
    face: float,
    normal: float,
    axis: str,
    mats: dict,
    salt: int,
) -> None:
    """Irregular stone courses over a mortar backing. `u` runs along the wall."""
    if u1 - u0 < 0.08 or z1 - z0 < 0.06:
        return
    add_wall_box(bucket, u0, u1, z0, z1, face, normal, WALL_T, axis, mats["mortar"], 0.0)
    heights = (0.16, 0.22, 0.18, 0.26)
    widths = (0.30, 0.42, 0.26, 0.50, 0.36)
    stones = mats["stones"]
    z = z0
    row = 0
    while z < z1 - 0.04 and row < 60:
        h = heights[(row + salt) % len(heights)]
        if z + h > z1:
            h = z1 - z
        if h < 0.05:
            break
        u = u0 + (0.07 if row % 2 else 0.0)
        if u > u1 - 0.1:
            u = u0
        col = 0
        while u < u1 - 0.06 and col < 48:
            w = widths[(row * 3 + col + salt) % len(widths)]
            if u + w > u1:
                w = u1 - u
            if w < 0.10:
                break
            jut = (((row * 5 + col * 3 + salt) % 5) - 2) * 0.006
            mat = stones[(row * 2 + col + salt) % len(stones)]
            add_wall_box(
                bucket,
                u + 0.008,
                u + w - 0.008,
                z + 0.006,
                z + h - 0.006,
                face,
                normal,
                0.055,
                axis,
                mat,
                proud=0.04 + jut,
            )
            u += w
            col += 1
        z += h
        row += 1


def add_beam(
    bucket: Bucket,
    center: tuple[float, float, float],
    size: tuple[float, float, float],
    mat: bpy.types.Material,
    rotation: Euler | None = None,
) -> None:
    bucket.add_box(center, size, mat, rotation)


def add_diag_xz(bucket: Bucket, x0: float, z0: float, x1: float, z1: float, y: float, mat: bpy.types.Material) -> None:
    dx, dz = x1 - x0, z1 - z0
    length = math.hypot(dx, dz)
    if length < 0.05:
        return
    ang = math.atan2(dz, dx)
    add_beam(bucket, ((x0 + x1) * 0.5, y, (z0 + z1) * 0.5), (length, 0.075, 0.09), mat, Euler((0.0, -ang, 0.0), "XYZ"))


def add_diag_yz(bucket: Bucket, y0: float, z0: float, y1: float, z1: float, x: float, mat: bpy.types.Material) -> None:
    dy, dz = y1 - y0, z1 - z0
    length = math.hypot(dy, dz)
    if length < 0.05:
        return
    ang = math.atan2(dz, dy)
    add_beam(bucket, (x, (y0 + y1) * 0.5, (z0 + z1) * 0.5), (0.09, length, 0.075), mat, Euler((ang, 0.0, 0.0), "XYZ"))


def add_diag_xz_at_x(bucket: Bucket, x0: float, z0: float, x1: float, z1: float, y: float, mat: bpy.types.Material) -> None:
    add_diag_xz(bucket, x0, z0, x1, z1, y, mat)


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
        spans = [(a, b) for a, b in nxt if b - a > 0.02]
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
        if zb - za > 0.015 and ub - ua > 0.015:
            clipped.append((ua, ub, za, zb))
            zs.add(za)
            zs.add(zb)
    ordered = sorted(zs)
    out: list[tuple[float, float, float, float]] = []
    for za, zb in zip(ordered, ordered[1:]):
        if zb - za < 0.015:
            continue
        mid = (za + zb) * 0.5
        blockers = [(h[0], h[1]) for h in clipped if h[2] - 1.0e-6 < mid < h[3] + 1.0e-6]
        for ua, ub in subtract_intervals(u0, u1, blockers):
            out.append((ua, ub, za, zb))
    return out


def clip_bands(
    u0: float,
    u1: float,
    bands: list[tuple[float, float, str]],
) -> list[tuple[float, float, str]]:
    pieces = []
    for b0, b1, kind in bands:
        a, b = max(u0, b0), min(u1, b1)
        if b - a > 0.02:
            pieces.append((a, b, kind))
    return pieces


def fill_region(
    bucket: Bucket,
    u0: float,
    u1: float,
    z0: float,
    z1: float,
    face: float,
    normal: float,
    axis: str,
    bands: list[tuple[float, float, str]],
    holes: list[tuple[float, float, float, float]],
    mats: dict,
    salt: int,
) -> None:
    for ua, ub, za, zb in rects_minus_holes(u0, u1, z0, z1, holes):
        for a, b, kind in clip_bands(ua, ub, bands):
            if kind == "stone":
                fill_stone(bucket, a, b, za, zb, face, normal, axis, mats, salt + int(a * 4))
            else:
                add_wall_box(bucket, a, b, za, zb, face, normal, WALL_T, axis, mats["plaster"], 0.0)


def arch_pts(cx: float, z_spring: float, radius: float, a0: float, a1: float, r_outer: float, r_inner: float) -> list[tuple[float, float]]:
    def p(a: float, r: float) -> tuple[float, float]:
        return (cx + math.cos(a) * r, z_spring + math.sin(a) * r)

    return [p(a0, r_outer), p(a1, r_outer), p(a1, r_inner), p(a0, r_inner)]


def add_arch_ring(
    bucket: Bucket,
    cx: float,
    z_spring: float,
    r_outer: float,
    r_inner: float,
    n0: float,
    n1: float,
    axis: str,
    mat: bpy.types.Material,
    segments: int = 7,
) -> None:
    for i in range(segments):
        a0 = math.pi * i / segments
        a1 = math.pi * (i + 1) / segments
        pts = arch_pts(cx, z_spring, r_outer, a0, a1, r_outer, r_inner)
        if axis == "y":
            bucket.add_prism_y(pts, n0, n1, mat)
        else:
            bucket.add_prism_x(pts, n0, n1, mat)


def add_arch_fill(
    bucket: Bucket,
    cx: float,
    z_spring: float,
    radius: float,
    n0: float,
    n1: float,
    axis: str,
    mat: bpy.types.Material,
    segments: int = 7,
) -> None:
    for i in range(segments):
        a0 = math.pi * i / segments
        a1 = math.pi * (i + 1) / segments
        p0 = (cx + math.cos(a0) * radius, z_spring + math.sin(a0) * radius)
        p1 = (cx + math.cos(a1) * radius, z_spring + math.sin(a1) * radius)
        pts = [(cx, z_spring), p0, p1]
        if axis == "y":
            bucket.add_prism_y(pts, n0, n1, mat)
        else:
            bucket.add_prism_x(pts, n0, n1, mat)


def spandrel_pts(cx: float, z_spring: float, radius: float, side: int, segments: int = 6) -> list[tuple[float, float]]:
    pts: list[tuple[float, float]] = []
    if side < 0:
        pts.append((cx - radius, z_spring))
        pts.append((cx - radius, z_spring + radius))
        pts.append((cx, z_spring + radius))
        for i in range(1, segments):
            a = math.pi * 0.5 + (math.pi * 0.5) * i / segments
            pts.append((cx + math.cos(a) * radius, z_spring + math.sin(a) * radius))
    else:
        pts.append((cx + radius, z_spring))
        pts.append((cx + radius, z_spring + radius))
        pts.append((cx, z_spring + radius))
        for i in range(1, segments):
            a = math.pi * 0.5 - (math.pi * 0.5) * i / segments
            pts.append((cx + math.cos(a) * radius, z_spring + math.sin(a) * radius))
    return pts


def add_spandrels(
    bucket: Bucket,
    cx: float,
    z_spring: float,
    radius: float,
    face: float,
    normal: float,
    axis: str,
    mat_left: bpy.types.Material,
    mat_right: bpy.types.Material,
) -> None:
    n_inner = face - normal * WALL_T
    for side, mat in ((-1, mat_left), (1, mat_right)):
        pts = spandrel_pts(cx, z_spring, radius, side)
        if axis == "y":
            bucket.add_prism_y(pts, n_inner, face, mat)
        else:
            bucket.add_prism_x(pts, n_inner, face, mat)


def add_arched_opening(
    frame: Bucket,
    cx: float,
    z_sill: float,
    hw: float,
    rect_h: float,
    face: float,
    normal: float,
    axis: str,
    mats: dict,
    kind: str,
) -> None:
    """Frame, depth, and leaf/glass for a door or window. Arch radius == half width."""
    z_spring = z_sill + rect_h
    radius = hw
    frame_t = 0.09
    n_proud = face + normal * 0.05
    n_back = face - normal * 0.11
    n_glass = face - normal * 0.07
    n_recess = face - normal * (WALL_T - 0.04)
    frame_mat = mats["frame"]
    depth_n = abs(n_proud - n_back)

    def put(u: float, n: float, z: float, su: float, sn: float, sz: float, mat: bpy.types.Material) -> None:
        frame.add_box(axis_loc(u, n, z, axis), axis_size(su, sn, sz, axis), mat)

    # Jambs and rectangular frame
    for side in (-1.0, 1.0):
        put(cx + side * (hw + frame_t * 0.35), (n_proud + n_back) * 0.5, z_sill + rect_h * 0.5, frame_t, depth_n, rect_h, frame_mat)
    add_arch_ring(frame, cx, z_spring, hw + frame_t, hw - 0.015, n_back, n_proud, axis, frame_mat, 7)

    # Sill / threshold
    sill_z = z_sill - 0.015
    sill_mat = mats["wood"] if kind != "door" else mats["stone_trim"]
    put(cx, face + normal * 0.04, sill_z, hw * 2.0 + 0.22, 0.16, 0.07, sill_mat)

    # Small keystone so the arch stays readable. A full lintel was flattening the curve.
    put(cx, face + normal * 0.06, z_spring + radius + 0.035, 0.12, 0.07, 0.08, mats["stone_trim"])

    if kind == "door":
        leaf_n = face + normal * 0.012
        leaf_t = 0.045
        put(cx, leaf_n, z_sill + rect_h * 0.5, hw * 2.0 - 0.08, leaf_t, rect_h - 0.04, mats["door"])
        add_arch_fill(frame, cx, z_spring, hw - 0.05, leaf_n - leaf_t * 0.5, leaf_n + leaf_t * 0.5, axis, mats["door"], 7)
        # Planks, rails, hinges, handle
        put(cx, leaf_n + normal * 0.02, z_sill + rect_h * 0.5, 0.035, 0.02, rect_h - 0.12, mats["wood_dark"])
        for rz in (0.22, 0.62):
            put(cx, leaf_n + normal * 0.02, z_sill + rect_h * rz, hw * 2.0 - 0.16, 0.018, 0.06, mats["wood_mid"])
        hinge_u = cx - hw + 0.08
        for hz in (0.28, 0.78):
            put(hinge_u, leaf_n + normal * 0.03, z_sill + rect_h * hz, 0.05, 0.02, 0.14, mats["metal"])
        put(cx + hw - 0.14, leaf_n + normal * 0.035, z_sill + rect_h * 0.48, 0.045, 0.03, 0.045, mats["metal"])
        return

    # Recess back so the opening has real depth even with opaque glass
    put(cx, n_recess, z_sill + rect_h * 0.5, hw * 2.0 - 0.04, 0.03, rect_h - 0.02, mats["recess"])
    add_arch_fill(frame, cx, z_spring, hw - 0.02, n_recess - 0.015, n_recess + 0.015, axis, mats["recess"], 6)

    glass_top = rect_h
    glass_bottom = 0.04
    if kind == "glazed_door":
        put(cx, n_glass, z_sill + rect_h * 0.28, hw * 2.0 - 0.10, 0.04, rect_h * 0.52, mats["door"])
        put(cx + hw * 0.55, n_glass + normal * 0.03, z_sill + rect_h * 0.32, 0.04, 0.025, 0.04, mats["metal"])
        glass_bottom = rect_h * 0.56
    g0 = z_sill + glass_bottom
    g1 = z_sill + glass_top - 0.03
    if g1 > g0:
        put(cx, n_glass, (g0 + g1) * 0.5, hw * 2.0 - 0.12, 0.025, g1 - g0, mats["glass"])
    add_arch_fill(frame, cx, z_spring, hw - 0.06, n_glass - 0.012, n_glass + 0.012, axis, mats["glass"], 6)

    # Mullions
    mullion_n = n_glass + normal * 0.02
    if g1 > g0:
        put(cx, mullion_n, (g0 + g1) * 0.5, 0.035, 0.02, g1 - g0, mats["frame"])
        if kind != "glazed_door":
            put(cx, mullion_n, (g0 + g1) * 0.5, hw * 2.0 - 0.16, 0.02, 0.03, mats["frame"])


def opening_holes(op: dict) -> list[tuple[float, float, float, float]]:
    z_spring = op["z"] + op["rect"]
    radius = op["hw"]
    return [
        (op["u"] - op["hw"], op["u"] + op["hw"], op["z"], z_spring),
        (op["u"] - radius, op["u"] + radius, z_spring, z_spring + radius),
    ]


def place_openings(
    walls: Bucket,
    frames: Bucket,
    openings: list[dict],
    face: float,
    normal: float,
    axis: str,
    mats: dict,
) -> list[tuple[float, float, float, float]]:
    holes: list[tuple[float, float, float, float]] = []
    for op in openings:
        holes.extend(opening_holes(op))
        z_spring = op["z"] + op["rect"]
        left_name, right_name = op.get("spandrel", ("plaster", "plaster"))
        add_spandrels(
            walls,
            op["u"],
            z_spring,
            op["hw"],
            face,
            normal,
            axis,
            mats[left_name],
            mats[right_name],
        )
        add_arched_opening(frames, op["u"], op["z"], op["hw"], op["rect"], face, normal, axis, mats, op["kind"])
    return holes


def add_stairs(
    bucket: Bucket,
    cx: float,
    y_wall: float,
    z_top: float,
    width: float,
    mats: dict,
    steps: int = 3,
) -> None:
    tread = 0.30
    rise = z_top / steps
    stones = mats["stones"]
    for i in range(steps):
        h = rise
        z = rise * i
        y_center = y_wall + tread * (steps - i) - tread * 0.5
        w = width + (steps - 1 - i) * 0.10
        pieces = 4
        pw = w / pieces
        for p in range(pieces):
            u = cx - w * 0.5 + pw * (p + 0.5)
            mat = stones[(i + p) % len(stones)]
            bucket.add_box((u, y_center, z + h * 0.5), (pw - 0.012, tread - 0.01, h), mat)


def add_railing_segment(
    bucket: Bucket,
    a: Vector,
    b: Vector,
    z: float,
    mat_wood: bpy.types.Material,
    mat_post: bpy.types.Material,
    height: float = 0.46,
) -> None:
    delta = b - a
    length = delta.length
    if length < 0.08:
        return
    direction = delta / length
    mid = (a + b) * 0.5
    angle = math.atan2(direction.y, direction.x)
    rot = Euler((0.0, 0.0, angle), "XYZ")
    # Rails are authored along local X then yawed in the XY plane.
    bucket.add_box((mid.x, mid.y, z + 0.04), (length, 0.04, 0.04), mat_wood, rot)
    bucket.add_box((mid.x, mid.y, z + height), (length, 0.045, 0.045), mat_wood, rot)
    count = max(2, int(length / 0.13))
    for i in range(count + 1):
        t = i / count
        p = a.lerp(b, t)
        post_mat = mat_post if i in (0, count) else mat_wood
        size_xy = 0.055 if i in (0, count) else 0.032
        bucket.add_box((p.x, p.y, z + height * 0.5), (size_xy, size_xy, height), post_mat)


def add_pitched_roof(
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
    tile_len: float = 0.24,
    tile_step: float = 0.125,
    tile_w: float = 0.27,
) -> None:
    """Two slopes, ridge parallel to Y. Overhangs continue the pitch past the plate."""
    tan_p = math.tan(pitch)
    sin_p = math.sin(pitch)
    cos_p = math.cos(pitch)
    ridge_z = z_plate + half * tan_p
    sides = (
        ("L", -1.0, half + oh_left, oh_left),
        ("R", 1.0, half + oh_right, oh_right),
    )
    chimneys = chimneys or []
    roof_colors = mats["roofs"]

    for _name, sign, span, oh in sides:
        slope_len = span / cos_p
        rot = Euler((0.0, -pitch if sign < 0.0 else pitch, 0.0), "XYZ")
        normal = Vector((-sin_p, 0.0, cos_p) if sign < 0.0 else (sin_p, 0.0, cos_p))
        eave_x = center_x + sign * (half + oh)
        eave_z = z_plate - oh * tan_p
        mid = slope_len * 0.5
        # Underlayment sits just under the tiles.
        if sign < 0.0:
            mid_x = eave_x + mid * cos_p
        else:
            mid_x = eave_x - mid * cos_p
        mid_z = eave_z + mid * sin_p
        under = Vector((mid_x, (y0 + y1) * 0.5, mid_z)) - normal * 0.02
        tiles.add_box(under, (slope_len + 0.02, (y1 - y0) + 0.02, 0.03), mats["roof_dark"], rot)

        row = 0
        dist = 0.02
        while dist < slope_len - 0.05 and row < 80:
            row_c = dist + tile_len * 0.5
            if sign < 0.0:
                x = eave_x + row_c * cos_p
            else:
                x = eave_x - row_c * cos_p
            z = eave_z + row_c * sin_p
            base = Vector((x, 0.0, z)) + normal * 0.04
            stagger = tile_w * 0.5 if row % 2 else 0.0
            y = y0 + stagger
            col = 0
            while y < y1 - 0.02 and col < 80:
                cy = y + tile_w * 0.5
                blocked = False
                for cx, cy0, rad in chimneys:
                    if abs(base.x - cx) < rad and abs(cy - cy0) < rad:
                        blocked = True
                        break
                if not blocked and y0 - 0.02 <= cy <= y1 + 0.02:
                    mat = roof_colors[(row * 3 + col * 5 + (0 if sign < 0 else 1)) % len(roof_colors)]
                    jitter = (((row * 13 + col * 7) % 5) - 2) * 0.004
                    tiles.add_box(
                        (base.x, cy, base.z + jitter),
                        (tile_len, tile_w * 0.90, 0.045),
                        mat,
                        rot,
                    )
                y += tile_w
                col += 1
            dist += tile_step
            row += 1

        # Rafter tails under the eave, every ~0.46 m
        y = y0 + 0.12
        index = 0
        while y < y1 - 0.05:
            tail = Vector((eave_x, y, eave_z)) - normal * 0.02
            wood.add_box(tail, (0.34, 0.07, 0.06), mats["wood_dark"] if index % 2 else mats["wood"], rot)
            y += 0.46
            index += 1

    # Ridge caps
    y = y0
    cap_i = 0
    while y < y1 - 0.02:
        mat = roof_colors[(cap_i * 2) % len(roof_colors)]
        tiles.add_box((center_x, y + 0.16, ridge_z + 0.045), (0.20, 0.30, 0.06), mat)
        y += 0.28
        cap_i += 1
    wood.add_box((center_x, (y0 + y1) * 0.5, ridge_z - 0.02), (0.12, y1 - y0, 0.08), mats["wood_dark"])

    # Barge boards along both gable edges
    for y_edge, y_sign in ((y1, 1.0), (y0, -1.0)):
        for sign, span, oh in ((-1.0, half + oh_left, oh_left), (1.0, half + oh_right, oh_right)):
            slope_len = span / cos_p
            rot = Euler((0.0, -pitch if sign < 0.0 else pitch, 0.0), "XYZ")
            eave_x = center_x + sign * (half + oh)
            eave_z = z_plate - oh * tan_p
            mid = slope_len * 0.5
            if sign < 0.0:
                mx = eave_x + mid * cos_p
            else:
                mx = eave_x - mid * cos_p
            mz = eave_z + mid * math.sin(pitch)
            wood.add_box((mx, y_edge + y_sign * 0.02, mz), (slope_len, 0.07, 0.09), mats["wood"], rot)


def add_corner_quoins(
    bucket: Bucket,
    x: float,
    y: float,
    z0: float,
    z1: float,
    sx: float,
    sy: float,
    mats: dict,
) -> None:
    stones = mats["stones_warm"]
    z = z0
    i = 0
    while z < z1 - 0.05 and i < 40:
        h = 0.28 if i % 2 == 0 else 0.18
        long_x = 0.48 if i % 2 == 0 else 0.30
        long_y = 0.30 if i % 2 == 0 else 0.48
        if z + h > z1:
            h = z1 - z
        bucket.add_box(
            (x + sx * 0.02, y + sy * 0.02, z + h * 0.5),
            (long_x, long_y, h - 0.012),
            stones[i % len(stones)],
        )
        z += h
        i += 1


def add_flower(bucket: Bucket, x: float, y: float, z: float, color: bpy.types.Material, mats: dict, scale: float = 1.0) -> None:
    stem_h = 0.11 * scale
    bucket.add_box((x, y, z + stem_h * 0.5), (0.02 * scale, 0.02 * scale, stem_h), mats["leaf2"])
    petal_z = z + stem_h
    petal = 0.07 * scale
    for dx, dy in ((petal, 0.0), (-petal, 0.0), (0.0, petal), (0.0, -petal), (petal * 0.7, petal * 0.7)):
        bucket.add_box((x + dx, y + dy, petal_z), (0.07 * scale, 0.07 * scale, 0.04 * scale), color)
    bucket.add_box((x, y, petal_z + 0.02 * scale), (0.045 * scale, 0.045 * scale, 0.035 * scale), mats["flower_y"])


def add_bush(bucket: Bucket, x: float, y: float, mats: dict, scale: float = 1.0, z: float = 0.0) -> None:
    puffs = (
        (0.0, 0.0, 0.16, 0.28),
        (0.12, 0.04, 0.22, 0.20),
        (-0.10, 0.06, 0.18, 0.18),
        (0.02, -0.10, 0.20, 0.16),
        (-0.02, 0.02, 0.30, 0.14),
    )
    leaves = mats["leaves"]
    for i, (dx, dy, dz, s) in enumerate(puffs):
        bucket.add_box(
            (x + dx * scale, y + dy * scale, z + dz * scale),
            (s * scale, s * 0.9 * scale, s * 0.75 * scale),
            leaves[i % len(leaves)],
        )


def add_ivy(
    bucket: Bucket,
    x: float,
    y: float,
    z0: float,
    z1: float,
    nx: float,
    ny: float,
    mats: dict,
) -> None:
    """Climbing vine: leaves spread along the wall, not a single stack of cubes."""
    leaves = mats["leaves"]
    length = math.hypot(nx, ny) or 1.0
    nx, ny = nx / length, ny / length
    tx, ty = -ny, nx
    z = z0
    i = 0
    while z < z1 and i < 28:
        wave = math.sin(i * 0.95) * 0.18
        px = x + nx * 0.07 + tx * wave
        py = y + ny * 0.07 + ty * wave
        for k, (along, out, up, size) in enumerate(
            ((0.0, 0.0, 0.0, 0.16), (0.07, 0.03, 0.04, 0.11), (-0.06, 0.02, 0.03, 0.10))
        ):
            bucket.add_box(
                (px + tx * along + nx * out, py + ty * along + ny * out, z + up),
                (size, size * 0.72, size * 0.55),
                leaves[(i + k) % len(leaves)],
                Euler((0.4, 0.25 * k, i * 0.35), "XYZ"),
            )
        z += 0.20
        i += 1


def add_planter(
    deco: Bucket,
    veg: Bucket,
    x: float,
    y: float,
    z: float,
    length: float,
    axis: str,
    mats: dict,
    colors: list[bpy.types.Material],
) -> None:
    if axis == "x":
        deco.add_box((x, y, z), (length, 0.16, 0.12), mats["wood"])
        deco.add_box((x, y, z + 0.02), (length - 0.06, 0.12, 0.06), mats["soil"])
        for i, color in enumerate(colors):
            add_flower(veg, x - length * 0.3 + i * (length * 0.3), y, z + 0.05, color, mats, 0.85)
    else:
        deco.add_box((x, y, z), (0.16, length, 0.12), mats["wood"])
        deco.add_box((x, y, z + 0.02), (0.12, length - 0.06, 0.06), mats["soil"])
        for i, color in enumerate(colors):
            add_flower(veg, x, y - length * 0.3 + i * (length * 0.3), z + 0.05, color, mats, 0.85)


def add_flag(bucket: Bucket, x: float, y: float, z: float, mats: dict) -> None:
    bucket.add_box((x, y, z + 0.52), (0.04, 0.04, 1.08), mats["wood_dark"])
    bucket.add_box((x, y, z + 1.08), (0.09, 0.09, 0.09), mats["flag"])
    for i in range(4):
        bucket.add_box(
            (x + 0.22 + i * 0.16, y + 0.04, z + 0.92 - i * 0.07),
            (0.20, 0.025, 0.36 - i * 0.04),
            mats["flag"] if i % 2 == 0 else mats["flag_dark"],
            Euler((0.18, 0.0, -0.22 - i * 0.07), "XYZ"),
        )


def build_chimney(bucket: Bucket, veg: Bucket, x: float, y: float, z_base: float, height: float, mats: dict, with_plant: bool) -> None:
    stones = mats["stones"]
    courses = max(4, int(height / 0.18))
    course_h = height / courses
    for i in range(courses):
        shrink = 0.0 if i < courses - 2 else 0.0
        w = 0.64 - shrink
        mat = stones[(i * 2) % len(stones)]
        if i >= courses - 3:
            mat = mats["brick"] if i % 2 == 0 else mats["brick_dark"]
            w = 0.56
        bucket.add_box((x, y, z_base + course_h * (i + 0.5)), (w - 0.02, w - 0.02, course_h - 0.012), mat)
    cap_z = z_base + height + 0.04
    bucket.add_box((x, y, cap_z), (0.78, 0.78, 0.09), mats["stone_trim"])
    bucket.add_box((x, y, cap_z + 0.09), (0.46, 0.46, 0.12), mats["brick"])
    if with_plant:
        add_ivy(veg, x + 0.22, y, z_base + height * 0.15, z_base + height * 0.55, 1.0, 0.0, mats)
        add_bush(veg, x - 0.05, y + 0.22, mats, 0.35, z_base + height * 0.15)


# ---------------------------------------------------------------------------
# House
# ---------------------------------------------------------------------------


def build_house(parent: bpy.types.Object, cols: dict[str, bpy.types.Collection], mats: dict) -> None:
    walls = Bucket("structure_walls", cols["House_Structure"])
    timber = Bucket("structure_timber", cols["House_Structure"])
    roof_tiles = Bucket("roof_tiles", cols["House_Roof"])
    roof_wood = Bucket("roof_timber", cols["House_Roof"])
    doors = Bucket("doors", cols["House_Doors"])
    windows = Bucket("windows", cols["House_Windows"])
    chimney_bucket = Bucket("chimneys", cols["House_Chimneys"])
    balcony = Bucket("balcony", cols["House_Balcony"])
    deco = Bucket("decorations", cols["House_Decorations"])
    veg = Bucket("vegetation", cols["House_Vegetation"])

    x0, x1 = -WALL_HALF_X, WALL_HALF_X
    inset = 0.24

    walls.add_box((0.0, (WALL_Y0 + WALL_Y1) * 0.5, FOUND_H * 0.5), (4.58, 4.14, FOUND_H), mats["stone_trim"])
    fill_stone(walls, x0 - 0.10, x1 + 0.10, 0.02, FOUND_H, WALL_Y1 + 0.13, 1.0, "y", mats, 1)
    fill_stone(walls, x0 - 0.10, x1 + 0.10, 0.02, FOUND_H, WALL_Y0 - 0.13, -1.0, "y", mats, 2)
    fill_stone(walls, WALL_Y0 - 0.08, WALL_Y1 + 0.12, 0.02, FOUND_H, x1 + 0.13, 1.0, "x", mats, 3)
    fill_stone(walls, WALL_Y0 - 0.08, WALL_Y1 + 0.12, 0.02, FOUND_H, x0 - 0.13, -1.0, "x", mats, 4)

    walls.add_box(
        (0.0, (WALL_Y0 + WALL_Y1) * 0.5, (FOUND_H + Z_PLATE) * 0.5),
        (WALL_HALF_X * 2.0 - 0.82, (WALL_Y1 - WALL_Y0) - 0.82, Z_PLATE - FOUND_H - 0.25),
        mats["interior"],
    )

    def assemble(
        openings: list[dict],
        frame_bucket: Bucket,
        u0: float,
        u1: float,
        z0: float,
        z1: float,
        face: float,
        normal: float,
        axis: str,
        bands: list[tuple[float, float, str]],
        salt: int,
    ) -> None:
        holes = place_openings(walls, frame_bucket, openings, face, normal, axis, mats)
        fill_region(walls, u0, u1, z0, z1, face, normal, axis, bands, holes, mats, salt)

    front_door = [
        {"u": DOOR_U, "z": FOUND_H, "hw": DOOR_HW, "rect": DOOR_RECT, "kind": "door", "spandrel": ("stone_trim", "plaster")},
    ]
    front_windows_g = [
        {"u": 1.20, "z": 1.08, "hw": 0.36, "rect": 0.78, "kind": "window"},
    ]
    front_windows_u = [
        {"u": -1.58, "z": 3.32, "hw": 0.26, "rect": 0.55, "kind": "window"},
        {"u": 1.22, "z": 3.34, "hw": 0.30, "rect": 0.58, "kind": "window"},
    ]
    bands_front_g = [
        (x0 + inset, DOOR_U - DOOR_HW - 0.02, "stone"),
        (DOOR_U + DOOR_HW + 0.02, x1 - inset, "plaster"),
    ]
    bands_plaster = [(x0 + inset, x1 - inset, "plaster")]
    bands_upper = [(x0, x1, "plaster")]
    bands_stone = [(x0 + inset, x1 - inset, "stone")]

    # Front — door and windows are separate meshes/collections but share the wall holes.
    door_holes = place_openings(walls, doors, front_door, WALL_Y1, 1.0, "y", mats)
    win_holes = place_openings(walls, windows, front_windows_g, WALL_Y1, 1.0, "y", mats)
    fill_region(walls, x0 + inset, x1 - inset, FOUND_H, Z_FLOOR1, WALL_Y1, 1.0, "y", bands_front_g, door_holes + win_holes, mats, 11)
    upper_holes = place_openings(walls, windows, front_windows_u, WALL_Y1, 1.0, "y", mats)
    fill_region(walls, x0, x1, Z_UPPER0, Z_PLATE, WALL_Y1, 1.0, "y", bands_upper, upper_holes, mats, 12)
    # Floor band
    add_wall_box(walls, x0, x1, Z_FLOOR1, Z_UPPER0, WALL_Y1, 1.0, WALL_T, "y", mats["wood_mid"], 0.01)

    # Left
    left_g = [{"u": -0.20, "z": 1.18, "hw": 0.24, "rect": 0.64, "kind": "window", "spandrel": ("stone_trim", "stone_trim")}]
    left_u = [{"u": -0.05, "z": 3.38, "hw": 0.22, "rect": 0.46, "kind": "window"}]
    assemble(left_g, windows, WALL_Y0 + inset, WALL_Y1 - inset, FOUND_H, Z_FLOOR1, x0, -1.0, "x", [(WALL_Y0 + inset, WALL_Y1 - inset, "stone")], 21)
    assemble(left_u, windows, WALL_Y0, WALL_Y1, Z_UPPER0, Z_PLATE, x0, -1.0, "x", [(WALL_Y0, WALL_Y1, "plaster")], 22)
    add_wall_box(walls, WALL_Y0, WALL_Y1, Z_FLOOR1, Z_UPPER0, x0, -1.0, WALL_T, "x", mats["wood_mid"], 0.01)

    # Right
    right_g = [{"u": -0.55, "z": 1.12, "hw": 0.28, "rect": 0.68, "kind": "window"}]
    right_door = [{"u": 0.72, "z": Z_UPPER0 + 0.06, "hw": 0.32, "rect": 0.92, "kind": "glazed_door"}]
    assemble(right_g, windows, WALL_Y0 + inset, WALL_Y1 - inset, FOUND_H, Z_FLOOR1, x1, 1.0, "x", [(WALL_Y0 + inset, WALL_Y1 - inset, "plaster")], 31)
    assemble(right_door, balcony, WALL_Y0, WALL_Y1, Z_UPPER0, Z_PLATE, x1, 1.0, "x", [(WALL_Y0, WALL_Y1, "plaster")], 32)
    add_wall_box(walls, WALL_Y0, WALL_Y1, Z_FLOOR1, Z_UPPER0, x1, 1.0, WALL_T, "x", mats["wood_mid"], 0.01)

    # Back: stone wainscot, plaster above
    back_g = [{"u": 0.40, "z": 1.12, "hw": 0.30, "rect": 0.70, "kind": "window"}]
    back_u = [{"u": -0.50, "z": 3.36, "hw": 0.24, "rect": 0.50, "kind": "window"}]
    stone_top = FOUND_H + 1.05
    assemble(back_g, windows, x0 + inset, x1 - inset, FOUND_H, stone_top, WALL_Y0, -1.0, "y", bands_stone, 41)
    fill_region(walls, x0 + inset, x1 - inset, stone_top, Z_FLOOR1, WALL_Y0, -1.0, "y", bands_plaster, opening_holes(back_g[0]), mats, 42)
    assemble(back_u, windows, x0, x1, Z_UPPER0, Z_PLATE, WALL_Y0, -1.0, "y", bands_upper, 43)
    add_wall_box(walls, x0, x1, Z_FLOOR1, Z_UPPER0, WALL_Y0, -1.0, WALL_T, "y", mats["wood_mid"], 0.01)

    # Corner quoins, ground floor
    for cx, cy, sx, sy in (
        (x0, WALL_Y1, -1.0, 1.0),
        (x1, WALL_Y1, 1.0, 1.0),
        (x0, WALL_Y0, -1.0, -1.0),
        (x1, WALL_Y0, 1.0, -1.0),
    ):
        add_corner_quoins(walls, cx, cy, FOUND_H, Z_FLOOR1, sx, sy, mats)

    # Gables
    def gable_clean(face: float, normal: float, window_u: float) -> None:
        def half(z: float) -> float:
            return max(0.02, (RIDGE_Z - z) / TAN_P)

        n0 = face - normal * WALL_T
        hw = 0.26
        z_sill = Z_PLATE + 0.40
        rect = 0.42
        z_spring = z_sill + rect
        z_crown = z_spring + hw
        walls.add_prism_y(
            [(-half(Z_PLATE), Z_PLATE), (half(Z_PLATE), Z_PLATE), (half(z_sill), z_sill), (-half(z_sill), z_sill)],
            n0,
            face,
            mats["plaster"],
        )
        walls.add_prism_y(
            [(-half(z_sill), z_sill), (window_u - hw, z_sill), (window_u - hw, z_spring), (-half(z_spring), z_spring)],
            n0,
            face,
            mats["plaster"],
        )
        walls.add_prism_y(
            [(window_u + hw, z_sill), (half(z_sill), z_sill), (half(z_spring), z_spring), (window_u + hw, z_spring)],
            n0,
            face,
            mats["plaster"],
        )
        walls.add_prism_y(
            [(-half(z_spring), z_spring), (window_u - hw, z_spring), (window_u - hw, z_crown), (-half(z_crown), z_crown)],
            n0,
            face,
            mats["plaster"],
        )
        walls.add_prism_y(
            [(window_u + hw, z_spring), (half(z_spring), z_spring), (half(z_crown), z_crown), (window_u + hw, z_crown)],
            n0,
            face,
            mats["plaster"],
        )
        add_spandrels(walls, window_u, z_spring, hw, face, normal, "y", mats["plaster"], mats["plaster"])
        walls.add_prism_y(
            [(-half(z_crown), z_crown), (half(z_crown), z_crown), (RIDGE_X, RIDGE_Z)],
            n0,
            face,
            mats["plaster"],
        )
        add_arched_opening(windows, window_u, z_sill, hw, rect, face, normal, "y", mats, "window")
        post_h = RIDGE_Z - z_crown - 0.04
        if post_h > 0.12:
            timber.add_box((window_u, face + normal * 0.045, z_crown + post_h * 0.5), (0.08, 0.07, post_h), mats["wood_dark"])

    gable_clean(WALL_Y1, 1.0, 0.10)
    gable_clean(WALL_Y0, -1.0, -0.15)

    # Timber frame — upper floor and the plastered ground-floor bays
    def post_y(x: float, z0: float, z1: float, y: float = WALL_Y1 + 0.04) -> None:
        timber.add_box((x, y, (z0 + z1) * 0.5), (0.10, 0.07, z1 - z0), mats["wood_dark"])

    def post_x(y: float, z0: float, z1: float, x: float, normal: float) -> None:
        timber.add_box((x + normal * 0.04, y, (z0 + z1) * 0.5), (0.07, 0.10, z1 - z0), mats["wood_dark"])

    for x in (-2.02, -1.15, -0.20, 0.78, 1.62, 2.02):
        post_y(x, Z_UPPER0, Z_PLATE)
    for x in (0.20, 0.78, 1.62, 2.02):
        post_y(x, FOUND_H + 0.04, Z_FLOOR1)
    for z, thick in ((Z_UPPER0 + 0.06, 0.11), (3.72, 0.08), (Z_PLATE - 0.02, 0.12)):
        timber.add_box((0.0, WALL_Y1 + 0.045, z), (WALL_HALF_X * 2.0 - 0.08, 0.07, thick), mats["wood"])
    add_diag_xz(timber, -2.02, Z_UPPER0 + 0.08, -1.15, Z_PLATE - 0.08, WALL_Y1 + 0.05, mats["wood"])
    add_diag_xz(timber, 0.78, Z_PLATE - 0.08, 1.62, Z_UPPER0 + 0.08, WALL_Y1 + 0.05, mats["wood_dark"])

    # Side and back plates
    for x, normal in ((x0, -1.0), (x1, 1.0)):
        for y in (WALL_Y0 + 0.12, -0.2, WALL_Y1 - 0.2):
            post_x(y, Z_UPPER0, Z_PLATE, x, normal)
        timber.add_box((x + normal * 0.04, (WALL_Y0 + WALL_Y1) * 0.5, Z_PLATE - 0.02), (0.07, WALL_Y1 - WALL_Y0 - 0.2, 0.11), mats["wood"])
        timber.add_box((x + normal * 0.04, (WALL_Y0 + WALL_Y1) * 0.5, Z_UPPER0 + 0.06), (0.07, WALL_Y1 - WALL_Y0 - 0.2, 0.10), mats["wood"])
    add_diag_yz(timber, 0.15, Z_UPPER0 + 0.1, 1.15, Z_PLATE - 0.1, x1 + 0.045, mats["wood"])
    for x in (-2.02, 2.02, 0.2):
        timber.add_box((x, WALL_Y0 - 0.04, (Z_UPPER0 + Z_PLATE) * 0.5), (0.10, 0.07, Z_PLATE - Z_UPPER0), mats["wood_dark"])
    timber.add_box((0.0, WALL_Y0 - 0.045, Z_PLATE - 0.02), (WALL_HALF_X * 2.0 - 0.1, 0.07, 0.11), mats["wood"])
    add_diag_xz(timber, 0.40, Z_UPPER0 + 0.12, 1.50, Z_PLATE - 0.12, WALL_Y0 - 0.05, mats["wood"])

    # Joist heads on the front-right, under the floor band
    x = 0.55
    while x < 2.05:
        timber.add_box((x, WALL_Y1 + 0.08, Z_FLOOR1 + BAND_H * 0.5), (0.09, 0.16, 0.09), mats["wood_mid"])
        x += 0.32

    # Entrance steps + porch
    add_stairs(doors, DOOR_U, WALL_Y1 - 0.02, FOUND_H, 1.28, mats, 3)
    porch_half = 0.86
    porch_cx = DOOR_U
    porch_eave_z = FOUND_H + DOOR_RECT + DOOR_HW + 0.06
    porch_y0 = WALL_Y1 - 0.04
    porch_y1 = WALL_Y1 + 0.98
    post_xs = (porch_cx - porch_half + 0.08, porch_cx + porch_half - 0.08)
    for px in post_xs:
        doors.add_box((px, porch_y1 - 0.12, 0.06), (0.28, 0.28, 0.12), mats["stone_trim"])
        timber.add_box((px, porch_y1 - 0.12, porch_eave_z * 0.5 + 0.04), (0.12, 0.12, porch_eave_z - 0.08), mats["wood_dark"])
    timber.add_box((porch_cx, porch_y1 - 0.12, porch_eave_z), (porch_half * 2.0, 0.12, 0.10), mats["wood"])
    timber.add_box((porch_cx, (porch_y0 + porch_y1) * 0.5, porch_eave_z), (0.10, porch_y1 - porch_y0, 0.08), mats["wood_mid"])

    add_pitched_roof(
        roof_tiles,
        roof_wood,
        center_x=porch_cx,
        half=porch_half,
        y0=porch_y0,
        y1=porch_y1 + 0.06,
        z_plate=porch_eave_z,
        pitch=math.radians(38.0),
        oh_left=0.06,
        oh_right=0.06,
        mats=mats,
        tile_len=0.20,
        tile_step=0.11,
        tile_w=0.22,
    )

    # Main roof
    # Forward on each slope so the shafts clear the gable in elevation.
    chimney_a = (-1.15, 1.15, 0.52)
    chimney_b = (1.05, -1.25, 0.48)
    y_roof0 = WALL_Y0 - OH_BACK
    y_roof1 = WALL_Y1 + OH_FRONT
    add_pitched_roof(
        roof_tiles,
        roof_wood,
        center_x=0.0,
        half=WALL_HALF_X,
        y0=y_roof0,
        y1=y_roof1,
        z_plate=Z_PLATE,
        pitch=PITCH,
        oh_left=OH_LEFT,
        oh_right=OH_RIGHT,
        mats=mats,
        chimneys=[chimney_a, chimney_b],
    )

    def roof_surface(x: float) -> float:
        return Z_PLATE + (WALL_HALF_X - abs(x)) * TAN_P

    build_chimney(chimney_bucket, veg, chimney_a[0], chimney_a[1], roof_surface(chimney_a[0]) - 0.08, 1.55, mats, True)
    build_chimney(chimney_bucket, veg, chimney_b[0], chimney_b[1], roof_surface(chimney_b[0]) - 0.08, 1.25, mats, False)

    add_flag(deco, 0.55, WALL_Y1 + OH_FRONT - 0.08, RIDGE_Z, mats)

    # Balcony on +X, second floor, under the longer eave
    deck_x0 = x1 - 0.04
    deck_x1 = x1 + 0.56
    deck_y0 = 0.08
    deck_y1 = 1.42
    deck_z = Z_FLOOR1 + 0.04
    plank_y = deck_y0
    i = 0
    while plank_y < deck_y1 - 0.02:
        balcony.add_box(
            ((deck_x0 + deck_x1) * 0.5, plank_y + 0.055, deck_z),
            (deck_x1 - deck_x0, 0.10, 0.055),
            mats["wood"] if i % 2 == 0 else mats["wood_mid"],
        )
        plank_y += 0.125
        i += 1
    for jx in (deck_x0 + 0.08, (deck_x0 + deck_x1) * 0.5, deck_x1 - 0.08):
        balcony.add_box((jx, (deck_y0 + deck_y1) * 0.5, deck_z - 0.07), (0.07, deck_y1 - deck_y0, 0.07), mats["wood_dark"])
    for by in (deck_y0 + 0.15, (deck_y0 + deck_y1) * 0.5, deck_y1 - 0.15):
        add_diag_xz(balcony, x1 - 0.02, deck_z - 0.48, deck_x1 - 0.04, deck_z - 0.05, by, mats["wood_dark"])
    z_rail = deck_z + 0.03
    add_railing_segment(balcony, Vector((deck_x1, deck_y0, 0)), Vector((deck_x1, deck_y1, 0)), z_rail, mats["wood"], mats["wood_dark"])
    add_railing_segment(balcony, Vector((deck_x0, deck_y0, 0)), Vector((deck_x1, deck_y0, 0)), z_rail, mats["wood"], mats["wood_dark"])
    add_railing_segment(balcony, Vector((deck_x0, deck_y1, 0)), Vector((deck_x1, deck_y1, 0)), z_rail, mats["wood"], mats["wood_dark"])
    add_planter(
        deco,
        veg,
        deck_x1 - 0.02,
        (deck_y0 + deck_y1) * 0.5,
        z_rail + 0.50,
        0.72,
        "y",
        mats,
        [mats["flower_p"], mats["flower_w"], mats["flower_r"]],
    )

    # Planters on the facade
    add_planter(deco, veg, 1.20, WALL_Y1 + 0.16, 0.98, 0.78, "x", mats, [mats["flower_r"], mats["flower_p"], mats["flower_w"]])
    add_planter(deco, veg, DOOR_U - 0.95, WALL_Y1 + 0.18, 0.42, 0.55, "x", mats, [mats["flower_w"], mats["flower_p"], mats["flower_r"]])

    # Ivy on the stone corner, the left porch post, and a run up the left wall
    add_ivy(veg, x0 + 0.02, WALL_Y1 - 0.02, 0.35, 3.35, -0.6, 0.8, mats)
    add_ivy(veg, post_xs[0], porch_y1 - 0.12, 0.20, porch_eave_z * 0.8, 0.2, 1.0, mats)
    add_ivy(veg, x0, -0.4, 0.40, 2.4, -1.0, 0.0, mats)

    # Base planting — denser in front, present on every side for the 360° read
    for bx, by, sc in (
        (-1.85, 2.15, 1.05),
        (1.85, 2.05, 0.9),
        (2.45, 0.2, 0.8),
        (-2.45, -0.4, 0.85),
        (-1.7, -2.25, 0.75),
        (1.9, -2.15, 0.7),
    ):
        add_bush(veg, bx, by, mats, sc)
    flower_spots = (
        (-1.55, 2.35, "flower_p"),
        (-1.25, 2.15, "flower_w"),
        (-2.05, 1.55, "flower_r"),
        (1.55, 2.30, "flower_r"),
        (2.05, 1.85, "flower_p"),
        (2.35, 1.15, "flower_w"),
        (2.55, -0.35, "flower_p"),
        (-2.35, 0.85, "flower_w"),
        (-2.15, -1.35, "flower_r"),
        (0.9, -2.30, "flower_p"),
        (-0.8, -2.28, "flower_w"),
        (DOOR_U + 0.85, 2.45, "flower_r"),
    )
    for fx, fy, key in flower_spots:
        add_flower(veg, fx, fy, 0.0, mats[key], mats, 1.0)
        add_flower(veg, fx + 0.10, fy + 0.05, 0.0, mats[key], mats, 0.75)

    # Moss along the left eave and beside the main chimney
    moss_y = y_roof0 + 0.3
    k = 0
    while moss_y < 0.8:
        eave_x = -(WALL_HALF_X + OH_LEFT) + 0.18
        eave_z = Z_PLATE - OH_LEFT * TAN_P + 0.12
        veg.add_box((eave_x, moss_y, eave_z), (0.16, 0.10, 0.05), mats["leaves"][k % 4], Euler((0.0, -PITCH, 0.0), "XYZ"))
        moss_y += 0.42
        k += 1

    made = [
        walls.finish(parent, 0.0),
        timber.finish(parent, 0.007),
        roof_tiles.finish(parent, 0.0),
        roof_wood.finish(parent, 0.006),
        doors.finish(parent, 0.006),
        windows.finish(parent, 0.005),
        chimney_bucket.finish(parent, 0.0),
        balcony.finish(parent, 0.005),
        deco.finish(parent, 0.0),
        veg.finish(parent, 0.0),
    ]
    print(f"objects: {sum(1 for m in made if m is not None)}")


# ---------------------------------------------------------------------------
# Validate / export / render
# ---------------------------------------------------------------------------


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
        raise RuntimeError("House has no vertices")
    return min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)


def validate(root: bpy.types.Object) -> None:
    x0, x1, y0, y1, z0, z1 = world_bounds(root)
    print(f"BOUNDS x[{x0:.2f},{x1:.2f}] y[{y0:.2f},{y1:.2f}] z[{z0:.2f},{z1:.2f}]")
    print(f"SIZE {x1 - x0:.2f} x {y1 - y0:.2f} x {z1 - z0:.2f} m")
    if z0 < -0.02:
        print(f"WARNING base below ground: {z0:.3f}")
    if max(abs(x0), abs(x1), abs(y0), abs(y1)) > FOOTPRINT_LIMIT:
        print("WARNING footprint exceeds 6 m cell limit")
    if z1 < 5.0:
        print("WARNING house is shorter than the two-storey target")


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
    scene.view_settings.exposure = 0.0
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1050
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    engine = "BLENDER_EEVEE_NEXT"
    for candidate in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "CYCLES"):
        try:
            scene.render.engine = candidate
            engine = candidate
            break
        except Exception:
            continue
    print(f"RENDER ENGINE {scene.render.engine}")
    if scene.render.engine == "CYCLES":
        scene.cycles.samples = 32
        scene.cycles.use_denoising = True
    eevee = getattr(scene, "eevee", None)
    if eevee is not None:
        if hasattr(eevee, "taa_render_samples"):
            eevee.taa_render_samples = 24
        if hasattr(eevee, "use_shadows"):
            eevee.use_shadows = True
    return None


def add_render_rig(coll: bpy.types.Collection) -> dict[str, bpy.types.Object]:
    bpy.ops.mesh.primitive_plane_add(size=18.0, location=(0.0, 0.0, -0.02))
    ground = bpy.context.active_object
    ground.name = "render_ground"
    mat = make_material("VN_Ref_ground", "#7ea36a", 0.95)
    ground.data.materials.append(mat)
    link_object(ground, coll)

    bpy.ops.object.light_add(type="SUN", location=(8.0, 10.0, 14.0))
    sun = bpy.context.active_object
    sun.name = "render_sun"
    sun.data.energy = 3.4
    sun.data.color = (1.0, 0.94, 0.84)
    if hasattr(sun.data, "angle"):
        sun.data.angle = math.radians(6.0)
    if hasattr(sun.data, "use_shadow"):
        sun.data.use_shadow = True
    look_at(sun, (0.0, 0.2, 2.5))
    link_object(sun, coll)

    bpy.ops.object.light_add(type="AREA", location=(-7.0, 3.0, 6.0))
    fill = bpy.context.active_object
    fill.name = "render_fill"
    fill.data.energy = 250
    fill.data.size = 6.0
    fill.data.color = (0.85, 0.90, 1.0)
    look_at(fill, (0.0, 0.0, 2.0))
    link_object(fill, coll)

    cameras: dict[str, bpy.types.Object] = {}
    specs = {
        "front": ((0.15, 12.2, 3.5), (0.0, 0.2, 3.1), 50.0, False, 0.0),
        "back": ((-0.2, -12.2, 3.6), (0.0, 0.0, 3.0), 50.0, False, 0.0),
        "side": ((12.4, 0.15, 3.4), (0.0, 0.15, 2.9), 50.0, False, 0.0),
        "iso": ((9.6, 9.6, 8.2), (0.0, 0.15, 2.5), 50.0, True, 11.2),
    }
    for name, (loc, target, lens, ortho, ortho_scale) in specs.items():
        bpy.ops.object.camera_add(location=loc)
        cam = bpy.context.active_object
        cam.name = f"cam_{name}"
        cam.data.lens = lens
        cam.data.clip_end = 80.0
        if ortho:
            cam.data.type = "ORTHO"
            cam.data.ortho_scale = ortho_scale
        look_at(cam, target)
        link_object(cam, coll)
        cameras[name] = cam
    return cameras


def render_views(cameras: dict[str, bpy.types.Object]) -> None:
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    for name, cam in cameras.items():
        scene.camera = cam
        scene.render.filepath = str(PREVIEW_DIR / f"vn_reference_cottage_{name}.png")
        bpy.ops.render.render(write_still=True)
        print(f"RENDERED {scene.render.filepath}")


def make_materials() -> dict:
    stone_hex = ("#8e8b86", "#a39e96", "#6f6d68", "#b7b2a8", "#7a7772")
    warm_hex = ("#c4b49a", "#d9cbb3", "#a89880")
    roof_hex = ("#c4513a", "#d4654a", "#b84432", "#a33a2c", "#e07a55")
    leaf_hex = ("#3f8f4e", "#5aaa62", "#2f6e3c", "#6fba78")
    mats: dict = {
        "plaster": make_material("VN_Ref_plaster", "#f0e2c8"),
        "plaster2": make_material("VN_Ref_plaster_shade", "#e4d3b6"),
        "mortar": make_material("VN_Ref_mortar", "#6a6560", 0.95),
        "wood": make_material("VN_Ref_wood", "#6e4a30", 0.9),
        "wood_mid": make_material("VN_Ref_wood_mid", "#5c3a24", 0.9),
        "wood_dark": make_material("VN_Ref_wood_dark", "#3f2818", 0.88),
        "door": make_material("VN_Ref_door", "#6a4328", 0.88),
        "frame": make_material("VN_Ref_frame", "#f3e6cf", 0.9),
        "stone_trim": make_material("VN_Ref_stone_trim", "#b7b1a6", 0.93),
        "glass": make_material("VN_Ref_glass", "#ffc56b", 0.32, "#ffb03a", 1.35, 0.28),
        "recess": make_material("VN_Ref_recess", "#4a301c", 0.9, "#ffb15a", 0.18, 0.05),
        "interior": make_material("VN_Ref_interior", "#2a1c14", 0.95),
        "metal": make_material("VN_Ref_metal", "#3a3532", 0.72, specular=0.2),
        "soil": make_material("VN_Ref_soil", "#6b4a32", 0.95),
        "flag": make_material("VN_Ref_flag", "#c62828", 0.86),
        "flag_dark": make_material("VN_Ref_flag_dark", "#8e1c1c", 0.88),
        "brick": make_material("VN_Ref_brick", "#a34432", 0.9),
        "brick_dark": make_material("VN_Ref_brick_dark", "#7d3428", 0.9),
        "roof_dark": make_material("VN_Ref_roof_under", "#8d3b2a", 0.94),
        "flower_p": make_material("VN_Ref_flower_pink", "#e58cb0", 0.88),
        "flower_r": make_material("VN_Ref_flower_red", "#d24b4b", 0.88),
        "flower_w": make_material("VN_Ref_flower_white", "#f4f1ea", 0.9),
        "flower_y": make_material("VN_Ref_flower_yellow", "#f0c14a", 0.88),
        "leaf2": make_material("VN_Ref_leaf_stem", "#2f6e3c", 0.9),
    }
    mats["stones"] = [make_material(f"VN_Ref_stone_{i}", h, 0.94) for i, h in enumerate(stone_hex)]
    mats["stones_warm"] = [make_material(f"VN_Ref_stone_warm_{i}", h, 0.94) for i, h in enumerate(warm_hex)]
    mats["roofs"] = [make_material(f"VN_Ref_roof_{i}", h, 0.9) for i, h in enumerate(roof_hex)]
    mats["leaves"] = [make_material(f"VN_Ref_leaf_{i}", h, 0.9) for i, h in enumerate(leaf_hex)]
    return mats


def main() -> None:
    clear_scene()
    setup_render_world()
    master = ensure_collection("ReferenceCottage")
    cols = {name: ensure_collection(name, master) for name in COLLECTION_NAMES}
    render_coll = ensure_collection("RenderOnly")

    root = bpy.data.objects.new("vn_building_reference_cottage", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.5
    link_object(root, master)

    mats = make_materials()
    print(
        f"MASSING plate={Z_PLATE:.2f} ridge={RIDGE_Z:.2f} "
        f"right_eave_z={Z_PLATE - OH_RIGHT * TAN_P:.2f}"
    )
    build_house(root, cols, mats)
    validate(root)

    try:
        bpy.ops.preferences.addon_enable(module="io_scene_gltf2")
    except Exception:
        pass
    export_glb(root, EXPORT_PATH)
    cameras = add_render_rig(render_coll)
    BLEND_PATH.parent.mkdir(parents=True, exist_ok=True)
    try:
        bpy.context.preferences.filepaths.save_version = 0
    except Exception:
        pass
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    render_views(cameras)
    # Save again so the blend keeps the render cameras.
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))

    if not EXPORT_PATH.is_file() or EXPORT_PATH.stat().st_size < 4096:
        raise RuntimeError(f"GLB export failed: {EXPORT_PATH}")
    print(f"SUCCESS {EXPORT_PATH} ({EXPORT_PATH.stat().st_size} bytes)")
    print(f"BLEND {BLEND_PATH}")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        sys.exit(1)

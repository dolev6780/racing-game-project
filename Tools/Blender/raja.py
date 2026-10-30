"""
Raja, the hero of Cloudborne, built in Blender from code.

Based on the concept portrait: an orange tiger with green eyes, a pink nose, a white cheek ruff
and a wicker basket on his back. The body is a signed-distance sculpture (ellipsoids and round
cones blended together) turned into a mesh, then UV-unwrapped, painted, rigged and exported.

Run it
  In Blender (4.2 or newer): Scripting tab > Open > this file > Run Script. Takes about a minute.
  From a terminal: blender --background --python Tools/Blender/raja.py -- [--render] [--out DIR]
What it makes
  A "Raja" collection: armature "Raja" with the body, eyes, whiskers, basket, straps and bamboo staff.
  Raja.fbx and the textures go to Assets/Art/Raja when the script sits in this repository,
  otherwise to a "Raja" folder next to the script. --render also writes preview images there.

Everything is in metres: +X is Raja's left, -Y is his front, +Z is up. Running it again rebuilds
the collection from scratch, so change the numbers here rather than the generated mesh.
"""

import math
import os
import sys
import time

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Matrix, Vector
from mathutils.bvhtree import BVHTree

# ---------------------------------------------------------------- settings

STEP = 0.007          # voxel size of the body sculpture; smaller is finer and slower
BODY_TRIS = 22000     # triangles in the final body mesh
TEX = 2048            # body texture size
SEED = 7


def hexc(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)], np.float32)


COL = {
    "orange": hexc("#F07A1A"), "deep": hexc("#CC4E0E"), "light": hexc("#F7A040"),
    "cream": hexc("#FBF3E4"), "shade": hexc("#E8D6BC"), "black": hexc("#1E1512"),
    "nose": hexc("#E8928A"), "nose_dk": hexc("#9C4A45"), "ear_in": hexc("#F3D3C2"),
}

# ---------------------------------------------------------------- anatomy

def P3(*a):
    return np.array(a, np.float32)


HEAD = P3(0, -0.03, 1.93)
EYE_R = 0.056


def eye_c(s):
    return HEAD + P3(s * 0.094, -0.166, 0.004)


J = {
    "hips": P3(0, 0.01, 0.90), "spine": P3(0, 0.01, 1.02), "chest": P3(0, 0.0, 1.22),
    "neck": P3(0, 0.0, 1.46), "head": P3(0, -0.025, 1.68), "head_end": P3(0, -0.025, 2.12),
}
for _s, _l in ((1, "L"), (-1, "R")):
    J["clav" + _l] = P3(_s * 0.04, 0.0, 1.43)
    J["sh" + _l] = P3(_s * 0.245, 0.0, 1.44)
    J["el" + _l] = P3(_s * 0.355, 0.005, 1.195)
    J["wr" + _l] = P3(_s * 0.46, -0.02, 0.96)
    J["tip" + _l] = P3(_s * 0.505, -0.045, 0.83)
    J["hip" + _l] = P3(_s * 0.115, 0.0, 0.90)
    J["kn" + _l] = P3(_s * 0.13, -0.025, 0.49)
    J["an" + _l] = P3(_s * 0.13, 0.025, 0.115)
    J["ball" + _l] = P3(_s * 0.13, -0.11, 0.04)
    J["toe" + _l] = P3(_s * 0.13, -0.2, 0.035)
    J["ear" + _l] = HEAD + P3(_s * 0.165, 0.035, 0.2)

TAIL = [P3(0, 0.14, 0.92), P3(0, 0.3, 0.84), P3(0, 0.44, 0.72), P3(0, 0.57, 0.61),
        P3(0, 0.70, 0.55), P3(0, 0.82, 0.57), P3(0, 0.92, 0.65)]
TAIL_R = [0.058, 0.05, 0.047, 0.045, 0.043, 0.042, 0.042]

# The basket sits on his back; the staff rests in it, leaning over his right shoulder.
BASKET_BASE = P3(0, 0.315, 1.0)
BASKET_H = 0.5
STAFF_LEN = 2.1
STAFF_GRIP = 0.35     # the grip (object origin) is this far below the middle, as in the game


def rot(x=0.0, y=0.0, z=0.0):
    """3x3 rotation (XYZ Euler, radians) as numpy."""
    return np.array(Euler((x, y, z), "XYZ").to_matrix(), np.float32)


def ear_rot(s):
    return rot(0, s * 0.42, s * 0.3)


def lerp(a, b, t):
    return a + (b - a) * t


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------- signed distance sculpture

def smin(a, b, k):
    h = np.maximum(k - np.abs(a - b), 0.0) / k
    return np.minimum(a, b) - h * h * k * 0.25


class Ell:
    """Ellipsoid with radii r, optionally turned by a 3x3 rotation R (columns are its local axes)."""

    def __init__(self, c, r, R=None):
        self.c, self.r, self.R = np.asarray(c, np.float32), np.asarray(r, np.float32), R

    def bounds(self):
        m = float(self.r.max())
        return self.c - m, self.c + m

    def dist(self, X, Y, Z):
        x, y, z = X - self.c[0], Y - self.c[1], Z - self.c[2]
        if self.R is not None:
            R = self.R
            x, y, z = (R[0, 0] * x + R[1, 0] * y + R[2, 0] * z, R[0, 1] * x + R[1, 1] * y + R[2, 1] * z,
                       R[0, 2] * x + R[1, 2] * y + R[2, 2] * z)
        r = self.r
        k0 = np.sqrt((x / r[0]) ** 2 + (y / r[1]) ** 2 + (z / r[2]) ** 2)
        k1 = np.sqrt((x / r[0] ** 2) ** 2 + (y / r[1] ** 2) ** 2 + (z / r[2] ** 2) ** 2)
        return k0 * (k0 - 1) / np.maximum(k1, 1e-9)


class Cone:
    """Round cone: a capsule from a (radius ra) to b (radius rb)."""

    def __init__(self, a, b, ra, rb):
        self.a, self.b = np.asarray(a, np.float32), np.asarray(b, np.float32)
        self.ra, self.rb = ra, rb

    def bounds(self):
        return np.minimum(self.a - self.ra, self.b - self.rb), np.maximum(self.a + self.ra, self.b + self.rb)

    def dist(self, X, Y, Z):
        ba = self.b - self.a
        l2 = float(ba @ ba)
        rr = self.ra - self.rb
        a2 = l2 - rr * rr
        il2 = 1.0 / l2
        px, py, pz = X - self.a[0], Y - self.a[1], Z - self.a[2]
        y = px * ba[0] + py * ba[1] + pz * ba[2]
        z = y - l2
        x2 = (px * l2 - ba[0] * y) ** 2 + (py * l2 - ba[1] * y) ** 2 + (pz * l2 - ba[2] * y) ** 2
        y2 = y * y * l2
        z2 = z * z * l2
        k = math.copysign(1, rr) * rr * rr * x2
        d_b = np.sqrt(x2 + z2) * il2 - self.rb
        d_a = np.sqrt(x2 + y2) * il2 - self.ra
        d_m = (np.sqrt(np.maximum(x2 * a2 * il2, 0)) + y * rr) * il2 - self.ra
        return np.where(np.sign(z) * a2 * z2 > k, d_b, np.where(np.sign(y) * a2 * y2 < k, d_a, d_m))


class Below:
    """Everything below the height z (used to flatten the soles)."""

    def __init__(self, z, lo, hi):
        self.z, self.lo, self.hi = z, np.asarray(lo, np.float32), np.asarray(hi, np.float32)

    def bounds(self):
        return self.lo, self.hi

    def dist(self, X, Y, Z):
        return np.broadcast_to(Z - self.z, np.broadcast_shapes(X.shape, Y.shape, Z.shape))


class Sculpt:
    """A distance field on a grid. Shapes are blended in with add (smooth union) and carved with cut."""

    def __init__(self, lo, hi, step):
        self.lo, self.step = np.asarray(lo, np.float32), step
        self.n = (np.ceil((np.asarray(hi, np.float32) - self.lo) / step) + 1).astype(int)
        self.F = np.full(tuple(self.n), 1.0, np.float32)

    def _region(self, shape, pad):
        lo, hi = shape.bounds()
        i0 = np.clip(np.floor((lo - pad - self.lo) / self.step).astype(int), 0, self.n)
        i1 = np.clip(np.ceil((hi + pad - self.lo) / self.step).astype(int) + 1, 0, self.n)
        sl = tuple(slice(int(a), int(b)) for a, b in zip(i0, i1))
        ax = [self.lo[i] + np.arange(i0[i], i1[i], dtype=np.float32) * self.step for i in range(3)]
        return sl, ax[0][:, None, None], ax[1][None, :, None], ax[2][None, None, :]

    def add(self, shape, k=0.0):
        sl, X, Y, Z = self._region(shape, k + 3 * self.step)
        d = shape.dist(X, Y, Z)
        self.F[sl] = smin(self.F[sl], d, k) if k > 0 else np.minimum(self.F[sl], d)

    def cut(self, shape, k=0.0):
        sl, X, Y, Z = self._region(shape, k + 3 * self.step)
        d = -shape.dist(X, Y, Z)
        self.F[sl] = -smin(-self.F[sl], -d, k) if k > 0 else np.maximum(self.F[sl], d)


def axis_frame(direction, side_hint):
    """Rotation whose local Z follows direction and local X leans towards side_hint."""
    z = direction / np.linalg.norm(direction)
    x = side_hint - z * float(side_hint @ z)
    x /= np.linalg.norm(x)
    y = np.cross(z, x)
    return np.stack([x, y, z], 1).astype(np.float32)


def sculpt_body():
    S = Sculpt((-0.64, -0.46, -0.02), (0.64, 1.02, 2.32), STEP)

    # torso: pelvis, belly, chest, pecs, lats, shoulders, neck
    S.add(Ell((0, 0.02, 0.94), (0.2, 0.16, 0.14)))
    S.add(Ell((0, -0.02, 1.10), (0.205, 0.17, 0.17)), 0.07)
    S.add(Ell((0, 0.0, 1.32), (0.265, 0.19, 0.2)), 0.07)
    for s in (1, -1):
        S.add(Ell((s * 0.1, -0.08, 1.35), (0.13, 0.1, 0.11)), 0.05)
        S.add(Ell((s * 0.12, 0.06, 1.30), (0.13, 0.11, 0.16)), 0.05)
        S.add(Ell((s * 0.11, 0.03, 1.48), (0.12, 0.09, 0.07)), 0.05)
    S.add(Cone((0, 0.0, 1.45), (0, -0.03, 1.74), 0.155, 0.145), 0.06)

    for s, L in ((1, "L"), (-1, "R")):
        # leg: thigh, calf, paw with four toes; the sole is flattened later
        S.add(Cone(J["hip" + L], J["kn" + L], 0.14, 0.092), 0.06)
        calf = lerp(J["kn" + L], J["an" + L], 0.33) + P3(0, 0.03, 0)
        S.add(Cone(J["kn" + L], calf, 0.092, 0.092), 0.03)
        S.add(Cone(calf, J["an" + L], 0.092, 0.066), 0.03)
        S.add(Ell((s * 0.13, -0.055, 0.07), (0.095, 0.15, 0.072)), 0.04)
        for dx in (-0.066, -0.022, 0.022, 0.066):
            S.add(Ell((s * 0.13 + dx, -0.18 + abs(dx) * 0.25, 0.048), (0.034, 0.04, 0.038)), 0.015)

        # arm: deltoid, upper arm, forearm, paw with four finger pads and a thumb
        S.add(Ell(J["sh" + L] + P3(s * 0.012, 0, 0.005), (0.115, 0.112, 0.118)), 0.05)
        S.add(Cone(J["sh" + L], J["el" + L], 0.1, 0.076), 0.04)
        fore = lerp(J["el" + L], J["wr" + L], 0.35)
        S.add(Cone(J["el" + L], fore, 0.077, 0.088), 0.03)
        S.add(Cone(fore, J["wr" + L], 0.088, 0.064), 0.03)
        R = axis_frame(J["tip" + L] - J["wr" + L], P3(s, 0, 0))
        pc = lerp(J["wr" + L], J["tip" + L], 0.42)
        S.add(Ell(pc, (0.066, 0.074, 0.088), R), 0.03)
        for i, dy in enumerate((-0.048, -0.016, 0.016, 0.048)):
            f = pc + R @ P3(0.005, dy, 0.078 - abs(dy) * 0.35)
            S.add(Ell(f, (0.031, 0.027, 0.034), R), 0.012)
        S.add(Ell(pc + R @ P3(-0.034, -0.062, 0.0), (0.027, 0.027, 0.038), R), 0.015)

    for i in range(len(TAIL) - 1):
        S.add(Cone(TAIL[i], TAIL[i + 1], TAIL_R[i], TAIL_R[i + 1]), 0.05 if i == 0 else 0.02)

    # head: cranium, brow, cheeks, jowls, muzzle pads, nose bridge, chin, nose
    H = HEAD
    S.add(Ell(H + P3(0, 0.03, 0.03), (0.235, 0.22, 0.215)), 0.06)
    S.add(Ell(H + P3(0, -0.12, 0.05), (0.17, 0.1, 0.11)), 0.05)
    for s in (1, -1):
        S.add(Ell(H + P3(s * 0.15, -0.08, -0.1), (0.16, 0.14, 0.13)), 0.06)
        S.add(Ell(H + P3(s * 0.1, -0.12, -0.16), (0.12, 0.1, 0.08)), 0.05)
        S.add(Ell(H + P3(s * 0.085, -0.15, 0.072), (0.075, 0.05, 0.04)), 0.04)
        S.add(Ell(H + P3(s * 0.054, -0.21, -0.11), (0.078, 0.07, 0.06)), 0.03)
        S.add(Ell(H + P3(s * 0.17, -0.05, -0.2), (0.13, 0.13, 0.1)), 0.05)
        # cheek ruff tufts
        for a, b, ra in (((0.27, -0.04, -0.06), (0.345, -0.02, -0.1), 0.05),
                         ((0.27, -0.04, -0.14), (0.34, -0.01, -0.2), 0.05),
                         ((0.24, -0.05, -0.21), (0.3, -0.02, -0.28), 0.048),
                         ((0.17, -0.08, -0.25), (0.2, -0.06, -0.32), 0.042)):
            S.add(Cone(H + P3(s * a[0], a[1], a[2]), H + P3(s * b[0], b[1], b[2]), ra, 0.01), 0.03)
        # ear, hollowed at the front
        R = ear_rot(s)
        S.add(Ell(J["ear" + ("L" if s > 0 else "R")], (0.085, 0.036, 0.09), R), 0.03)
    S.add(Ell(H + P3(0, -0.19, -0.025), (0.055, 0.08, 0.06)), 0.04)
    S.add(Ell(H + P3(0, -0.17, -0.185), (0.065, 0.06, 0.04)), 0.04)
    S.add(Ell(H + P3(0, -0.268, -0.055), (0.052, 0.03, 0.026)), 0.015)
    S.add(Ell(H + P3(0, -0.268, -0.078), (0.024, 0.026, 0.02)), 0.015)

    for s in (1, -1):
        S.cut(Ell(eye_c(s) + P3(0, -0.004, -0.012), (EYE_R + 0.002,) * 3), 0.012)
        R = ear_rot(s)
        S.cut(Ell(J["ear" + ("L" if s > 0 else "R")] + R @ P3(0, -0.026, -0.006), (0.06, 0.03, 0.064), R), 0.012)
    S.cut(Below(0.0, (-0.4, -0.4, -0.05), (0.4, 0.3, 0.08)), 0.01)
    return S


def surface_nets(F, lo, step):
    """Mesh the zero level of F: one vertex per crossing cell, one quad per crossing edge."""
    S = F < 0
    nx, ny, nz = F.shape
    cnt = np.zeros((nx - 1, ny - 1, nz - 1), np.uint8)
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                cnt += S[dx:nx - 1 + dx, dy:ny - 1 + dy, dz:nz - 1 + dz]
    ci = np.argwhere((cnt > 0) & (cnt < 8))
    del cnt
    idx = np.full((nx - 1, ny - 1, nz - 1), -1, np.int32)
    idx[tuple(ci.T)] = np.arange(len(ci), dtype=np.int32)

    corners = np.array([[0, 0, 0], [1, 0, 0], [0, 1, 0], [1, 1, 0], [0, 0, 1], [1, 0, 1], [0, 1, 1], [1, 1, 1]])
    edges = [(0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7)]
    vals = np.stack([F[ci[:, 0] + c[0], ci[:, 1] + c[1], ci[:, 2] + c[2]] for c in corners], 1)
    acc = np.zeros((len(ci), 3), np.float32)
    num = np.zeros(len(ci), np.float32)
    for a, b in edges:
        fa, fb = vals[:, a], vals[:, b]
        m = (fa < 0) != (fb < 0)
        t = np.where(m, fa / np.where(m, fa - fb, 1), 0)
        acc += (corners[a] + t[:, None] * (corners[b] - corners[a])) * m[:, None]
        num += m
    verts = lo + (ci + acc / num[:, None]) * step

    quads = []
    for ax in range(3):
        b, c = (ax + 1) % 3, (ax + 2) % 3
        s0 = S[tuple(slice(0, -1) if i == ax else slice(None) for i in range(3))]
        s1 = S[tuple(slice(1, None) if i == ax else slice(None) for i in range(3))]
        e = np.argwhere(s0 != s1)
        ok = (e[:, b] >= 1) & (e[:, c] >= 1) & (e[:, b] <= F.shape[b] - 2) & (e[:, c] <= F.shape[c] - 2)
        e = e[ok]
        inside_low = s0[tuple(e.T)]

        def cell(db, dc):
            q = e.copy()
            q[:, b] += db
            q[:, c] += dc
            return idx[tuple(q.T)]

        q = np.stack([cell(-1, -1), cell(0, -1), cell(0, 0), cell(-1, 0)], 1)
        q[~inside_low] = q[~inside_low][:, ::-1]
        quads.append(q)
    return verts.astype(np.float32), np.concatenate(quads)


def taubin(V, quads, rounds=4):
    """Smooths voxel steps away without shrinking the shape."""
    e = np.concatenate([quads[:, [0, 1]], quads[:, [1, 2]], quads[:, [2, 3]], quads[:, [3, 0]]])
    a, b = e[:, 0], e[:, 1]
    n = len(V)
    cnt = np.bincount(a, minlength=n) + np.bincount(b, minlength=n)
    for _ in range(rounds):
        for lam in (0.5, -0.53):
            avg = np.stack([np.bincount(a, V[b, i], n) + np.bincount(b, V[a, i], n) for i in range(3)], 1)
            V = V + lam * (avg / np.maximum(cnt, 1)[:, None] - V)
    return V.astype(np.float32)


# ---------------------------------------------------------------- blender helpers

def link(obj, coll):
    coll.objects.link(obj)
    return obj


def mesh_object(name, verts, faces, coll, uvs=None):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], [tuple(f) for f in faces])
    if uvs is not None:
        layer = me.uv_layers.new(name="UVMap")
        layer.data.foreach_set("uv", np.asarray(uvs, np.float32).ravel())
    me.validate()
    smooth(me)
    return link(bpy.data.objects.new(name, me), coll)


def smooth(me):
    try:
        me.shade_smooth()
    except AttributeError:
        me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))


def activate(obj):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def set_in(node, names, value):
    for n in names:
        if n in node.inputs:
            node.inputs[n].default_value = value
            return


def image(name, rgb, alpha=None):
    h, w = rgb.shape[:2]
    old = bpy.data.images.get(name)
    if old:
        bpy.data.images.remove(old)
    img = bpy.data.images.new(name, w, h, alpha=alpha is not None)
    px = np.ones((h, w, 4), np.float32)
    px[..., :3] = np.clip(rgb, 0, 1)
    if alpha is not None:
        px[..., 3] = alpha
    img.pixels.foreach_set(px.ravel())
    img.pack()
    return img


def material(name, img=None, color=None, rough=0.75, sheen=0.0, coat=0.0, emit=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    if m.node_tree is None:
        m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (-300, 0)
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    if img is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        tex.location = (-650, 0)
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    elif color is not None:
        bsdf.inputs["Base Color"].default_value = (*color, 1)
    set_in(bsdf, ["Roughness"], rough)
    set_in(bsdf, ["Specular IOR Level", "Specular"], 0.35)
    set_in(bsdf, ["Sheen Weight", "Sheen"], sheen)
    set_in(bsdf, ["Coat Weight", "Clearcoat"], coat)
    if emit is not None:
        set_in(bsdf, ["Emission Color", "Emission"], (*emit, 1))
        set_in(bsdf, ["Emission Strength"], 1.0)
    return m


def tube(path, radii, sides=8, closed_ends=True, v0=0.8, v1=1.0, u_scale=1.0):
    """A tube along path (N,3) with per-point radii. Returns verts, faces, per-loop uvs."""
    path = np.asarray(path, np.float32)
    n = len(path)
    radii = np.broadcast_to(np.asarray(radii, np.float32), (n,))
    tang = np.gradient(path, axis=0)
    tang /= np.linalg.norm(tang, axis=1, keepdims=True)
    ref = np.array([0, 0, 1], np.float32) if abs(tang[0][2]) < 0.9 else np.array([1, 0, 0], np.float32)
    nrm = np.cross(tang[0], ref)
    nrm /= np.linalg.norm(nrm)
    verts, rings = [], []
    for i in range(n):
        if i:  # parallel transport keeps the tube from twisting
            nrm = nrm - tang[i] * float(nrm @ tang[i])
            nrm /= np.linalg.norm(nrm)
        bin_ = np.cross(tang[i], nrm)
        ring = []
        for k in range(sides):
            a = 2 * math.pi * k / sides
            ring.append(len(verts))
            verts.append(path[i] + radii[i] * (math.cos(a) * nrm + math.sin(a) * bin_))
        rings.append(ring)
    length = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(path, axis=0), axis=1))])
    faces, uvs = [], []
    for i in range(n - 1):
        for k in range(sides):
            k2 = (k + 1) % sides
            faces.append((rings[i][k], rings[i + 1][k], rings[i + 1][k2], rings[i][k2]))
            for ii, kk in ((i, k), (i + 1, k), (i + 1, k + 1), (i, k + 1)):
                uvs.append((length[ii] * u_scale, v0 + (v1 - v0) * kk / sides))
    if closed_ends:
        for end, ring in ((0, rings[0]), (n - 1, rings[-1])):
            c = len(verts)
            verts.append(path[end] + (tang[end] * radii[end] * 0.6 * (1 if end else -1)))
            for k in range(sides):
                k2 = (k + 1) % sides
                faces.append((ring[k], ring[k2], c) if end else (ring[k2], ring[k], c))
                uvs += [(length[end] * u_scale, v0)] * 3
    return np.array(verts, np.float32), faces, uvs


# ---------------------------------------------------------------- painting

def vnoise(P, freq, seed=0):
    """Smooth 3D value noise in -1..1."""
    p = P * freq + seed * 17.13
    i = np.floor(p).astype(np.int64)
    f = p - i
    u = f * f * (3 - 2 * f)

    def h(dx, dy, dz):
        n = (i[:, 0] + dx) * 73856093 ^ (i[:, 1] + dy) * 19349663 ^ (i[:, 2] + dz) * 83492791 ^ (seed * 2654435761)
        n = (n ^ (n >> 13)) * 1274126177
        return ((n ^ (n >> 16)) & 0xFFFF) / 32767.5 - 1.0

    out = 0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (u[:, 0] if dx else 1 - u[:, 0]) * (u[:, 1] if dy else 1 - u[:, 1]) * (u[:, 2] if dz else 1 - u[:, 2])
                out = out + w * h(dx, dy, dz)
    return out


def seg_dist(P, a, b):
    ab = b - a
    t = np.clip(((P - a) @ ab) / max(float(ab @ ab), 1e-12), 0, 1)
    d = np.linalg.norm(P - (a + t[:, None] * ab), axis=1)
    return d, t


def ell_mask(P, c, r, soft=0.12, jag=None):
    q = np.linalg.norm((P - c) / r, axis=1)
    if jag is not None:
        q = q + jag
    return 1 - smoothstep(1 - soft, 1 + soft, q)


def stroke_mask(D, pts, w0, w1, edge=0.35):
    """Tapered brush stroke on the unit sphere: D are unit directions, pts (theta, phi) pairs."""
    pts = np.asarray(pts, np.float32)
    dirs = np.stack([np.sin(pts[:, 0]) * np.cos(pts[:, 1]), -np.cos(pts[:, 0]) * np.cos(pts[:, 1]), np.sin(pts[:, 1])], 1)
    seg = np.linalg.norm(np.diff(dirs, axis=0), axis=1)
    cum = np.concatenate([[0], np.cumsum(seg)]) / max(seg.sum(), 1e-6)
    best_d = np.full(len(D), 9.0, np.float32)
    best_u = np.zeros(len(D), np.float32)
    for i in range(len(dirs) - 1):
        d, t = seg_dist(D, dirs[i], dirs[i + 1])
        u = cum[i] + t * (cum[i + 1] - cum[i])
        better = d < best_d
        best_d = np.where(better, d, best_d)
        best_u = np.where(better, u, best_u)
    w = w0 + (w1 - w0) * best_u
    return smoothstep(w, w * (1 - edge), best_d)


def limb_stripes(P, a, b, outer, rings, width, reach=-0.25):
    """Stripes wrapping a limb from its outer side; rings are positions along a..b."""
    d, t = seg_dist(P, a, b)
    axis = (b - a) / np.linalg.norm(b - a)
    rv = P - (a + np.outer(t, b - a))
    rv /= np.maximum(np.linalg.norm(rv, axis=1, keepdims=True), 1e-6)
    o = outer - axis * float(outer @ axis)
    o /= np.linalg.norm(o)
    cosang = rv @ o
    side = np.clip((cosang - reach) / (1 - reach), 0, 1)
    length = float(np.linalg.norm(b - a))
    out = np.zeros(len(P), np.float32)
    wob = vnoise(P, 14, 3) * 0.012
    for k, tk in enumerate(rings):
        center = tk * length + 0.035 * cosang + wob
        w = width * np.sqrt(side) * (0.8 + 0.4 * ((k * 7919) % 5) / 4) + 1e-6
        out = np.maximum(out, smoothstep(w, w * 0.6, np.abs(t * length - center)) * (side > 0))
    return out, cosang, t


def paint_body(P, N):
    """Colour and stripe mask for points P with normals N on the body surface."""
    rng = np.random.default_rng(SEED)
    n = len(P)

    parts = [("torso", J["hips"] + P3(0, 0, -0.04), J["neck"] + P3(0, 0, 0.05), 0.2),
             ("neck", J["neck"], J["head"] + P3(0, 0, 0.03), 0.12),
             ("head", HEAD + P3(0, -0.02, -0.08), HEAD + P3(0, 0, 0.05), 0.24),
             ("tail", TAIL[0] + (TAIL[1] - TAIL[0]) * 0.4, TAIL[-1], 0.05)]
    for L in "LR":
        parts += [("ear" + L, J["ear" + L], J["ear" + L], 0.075),
                  ("uarm" + L, J["sh" + L], J["el" + L], 0.08), ("farm" + L, J["el" + L], J["wr" + L], 0.068),
                  ("hand" + L, J["wr" + L], J["tip" + L], 0.06),
                  ("thigh" + L, J["hip" + L], J["kn" + L], 0.11), ("shin" + L, J["kn" + L], J["an" + L], 0.07),
                  ("foot" + L, J["an" + L] + P3(0, -0.06, -0.05), J["toe" + L] + P3(0, 0.02, 0), 0.06)]
    # the tail is a chain: measure it segment by segment
    D = []
    for name, a, b, r in parts:
        if name == "tail":
            d = np.min([seg_dist(P, TAIL[i], TAIL[i + 1])[0] for i in range(1, len(TAIL) - 1)], 0)
        else:
            d = seg_dist(P, a, b)[0]
        D.append(d - r)
    lab = np.argmin(np.stack(D, 1), 1)
    names = [p[0] for p in parts]

    def is_(*want):
        return np.isin(lab, [names.index(w) for w in want])

    # base orange: deeper on the back and top, lighter low on the sides
    shade = np.clip(0.5 + 0.35 * N[:, 2] + 0.35 * N[:, 1], 0, 1)
    shade = np.clip(shade + vnoise(P, 6, 1) * 0.12, 0, 1)
    col = COL["light"] + (COL["orange"] - COL["light"]) * smoothstep(0.1, 0.5, shade)[:, None]
    col = col + (COL["deep"] - col) * smoothstep(0.55, 0.95, shade)[:, None]

    white = np.zeros(n, np.float32)
    stripe = np.zeros(n, np.float32)
    jag = vnoise(P, 22, 5) * 0.12

    # ---- torso and neck
    tn = is_("torso", "neck")
    belly = ell_mask(P, P3(0, -0.25, 1.22), P3(0.13, 0.16, 0.36), 0.1, jag)
    throat = ell_mask(P, P3(0, -0.2, 1.62), P3(0.1, 0.13, 0.18), 0.1, jag)
    white = np.where(tn, np.maximum(belly, throat), white)
    ang = np.arctan2(P[:, 0], P[:, 1])            # 0 at the spine, +-pi at the belly
    aa = np.abs(ang)
    zz = P[:, 2] + vnoise(P, 9, 2) * 0.014
    zs = np.arange(0.88, 1.66, 0.072)
    for side in (1, -1):
        on_side = (np.sign(P[:, 0]) == side) | (np.abs(P[:, 0]) < 1e-4)
        for k, z0 in enumerate(zs):
            z0 = z0 + rng.uniform(-0.012, 0.012)
            amax = rng.uniform(1.45, 2.25)
            w = rng.uniform(0.014, 0.024)
            drop = rng.uniform(0.04, 0.08)
            gap = 0.16 if rng.random() < 0.35 else 0.0
            u = aa / amax
            zc = z0 - drop * u + 0.01 * np.sin(aa * 6 + k)
            wid = w * np.clip(1 - u, 0, 1) ** 0.6 * (smoothstep(gap, gap + 0.15, aa) if gap else 1) + 1e-6
            m = smoothstep(wid, wid * 0.55, np.abs(zz - zc)) * (u < 1)
            # a shorter branch under some stripes
            if rng.random() < 0.45:
                b0 = rng.uniform(0.7, 1.1)
                ub = np.clip((aa - b0) / (amax * 0.95 - b0), 0, 1)
                wb = w * 0.8 * np.sin(np.pi * ub) + 1e-6
                m = np.maximum(m, smoothstep(wb, wb * 0.55, np.abs(zz - (zc - 0.03 - 0.02 * ub))) * (aa > b0))
            stripe = np.where(tn & on_side, np.maximum(stripe, m), stripe)
    stripe = np.where(tn, stripe * (1 - 0.9 * belly) * (1 - throat), stripe)

    # ---- arms and legs
    for s, L in ((1, "L"), (-1, "R")):
        st, cosang, _ = limb_stripes(P, J["sh" + L], J["el" + L], P3(s, 0.3, 0), (0.28, 0.55, 0.8), 0.02)
        sel = is_("uarm" + L)
        stripe = np.where(sel, np.maximum(stripe, st), stripe)
        white = np.where(sel, smoothstep(0.35, 0.85, -cosang) * 0.8, white)
        st, cosang, _ = limb_stripes(P, J["el" + L], J["wr" + L], P3(s, 0.5, 0), (0.22, 0.48, 0.72), 0.019)
        sel = is_("farm" + L)
        stripe = np.where(sel, np.maximum(stripe, st), stripe)
        white = np.where(sel, smoothstep(0.2, 0.8, -cosang) * 0.85, white)
        _, t = seg_dist(P, J["wr" + L], J["tip" + L])
        white = np.where(is_("hand" + L), smoothstep(0.55, 0.85, t) * 0.75, white)

        st, cosang, _ = limb_stripes(P, J["hip" + L], J["kn" + L], P3(s, 0.6, 0), (0.18, 0.4, 0.62, 0.83), 0.022, -0.35)
        sel = is_("thigh" + L)
        stripe = np.where(sel, np.maximum(stripe, st), stripe)
        inner = P[:, 0] * s < 0.09
        white = np.where(sel, np.maximum(smoothstep(0.45, 0.9, -cosang), inner * 0.0) * 0.85, white)
        st, cosang, _ = limb_stripes(P, J["kn" + L], J["an" + L], P3(s, 0.8, 0), (0.25, 0.5, 0.72), 0.02, -0.3)
        sel = is_("shin" + L)
        stripe = np.where(sel, np.maximum(stripe, st), stripe)
        white = np.where(sel, smoothstep(0.5, 0.9, -cosang) * 0.7, white)
        toes = smoothstep(-0.11, -0.15, P[:, 1]) * 0.8
        white = np.where(is_("foot" + L), toes, white)

    # ---- tail: rings, lighter underneath, black tip
    sel = is_("tail")
    seg_len = [float(np.linalg.norm(TAIL[i + 1] - TAIL[i])) for i in range(len(TAIL) - 1)]
    best, along = np.full(n, 9.0, np.float32), np.zeros(n, np.float32)
    for i in range(len(TAIL) - 1):
        d, t = seg_dist(P, TAIL[i], TAIL[i + 1])
        better = d < best
        best = np.where(better, d, best)
        along = np.where(better, sum(seg_len[:i]) + t * seg_len[i], along)
    total = sum(seg_len)
    ring = np.zeros(n, np.float32)
    for k, c in enumerate(np.arange(0.2, total - 0.12, 0.105)):
        ring = np.maximum(ring, smoothstep(0.026, 0.016, np.abs(along - c - vnoise(P, 12, 9) * 0.01)))
    ring = np.maximum(ring, smoothstep(total - 0.14, total - 0.12, along))
    stripe = np.where(sel, np.maximum(stripe, ring), stripe)
    white = np.where(sel, smoothstep(-0.2, -0.8, N[:, 2]) * 0.6 * (along < total - 0.14), white)

    # ---- head
    hd = is_("head")
    H = HEAD
    Q = P - H
    Dn = Q / np.maximum(np.linalg.norm(Q, axis=1, keepdims=True), 1e-6)
    hw = np.zeros(n, np.float32)
    hw = np.maximum(hw, ell_mask(P, H + P3(0, -0.21, -0.12), P3(0.12, 0.11, 0.1), 0.15, jag))
    hw = np.maximum(hw, ell_mask(P, H + P3(0, -0.12, -0.25), P3(0.15, 0.17, 0.11), 0.12, jag))
    for s in (1, -1):
        hw = np.maximum(hw, ell_mask(P, H + P3(s * 0.2, -0.08, -0.17), P3(0.19, 0.19, 0.11), 0.12, jag))
        hw = np.maximum(hw, ell_mask(P, eye_c(s) + P3(-s * 0.012, -0.02, 0.074), P3(0.042, 0.06, 0.028), 0.2))
        hw = np.maximum(hw, ell_mask(P, eye_c(s) + P3(s * 0.012, -0.02, -0.066), P3(0.07, 0.07, 0.034), 0.2, jag))
    white = np.where(hd, hw, white)

    hs = np.zeros(n, np.float32)
    strokes = [
        ([(0.16, 0.47), (0.3, 0.44), (0.46, 0.37), (0.62, 0.29)], 0.05, 0.012),        # worried brow
        ([(0.11, 0.62), (0.3, 0.62), (0.5, 0.57), (0.68, 0.49)], 0.036, 0.01),
        ([(0.13, 0.77), (0.3, 0.78), (0.5, 0.73)], 0.03, 0.008),
        ([(0.07, 0.9), (0.25, 0.97), (0.45, 1.0)], 0.028, 0.008),
        ([(0.8, 0.46), (0.93, 0.27), (1.03, 0.1)], 0.036, 0.01),
        ([(1.48, 0.08), (1.22, 0.04), (0.98, 0.01)], 0.048, 0.008),                  # cheeks
        ([(1.52, -0.13), (1.27, -0.16), (1.04, -0.15)], 0.048, 0.008),
        ([(1.46, -0.33), (1.27, -0.35), (1.12, -0.31)], 0.042, 0.006),
        ([(0.62, 0.04), (0.82, 0.0)], 0.02, 0.005),                                    # eye corner
        ([(1.75, 0.62), (2.3, 0.55), (3.14, 0.5)], 0.042, 0.032),                      # back of head
        ([(1.8, 0.32), (2.4, 0.26), (3.14, 0.2)], 0.042, 0.03),
        ([(1.75, 0.02), (2.4, -0.04), (3.14, -0.08)], 0.04, 0.03),
    ]
    for pts, w0, w1 in strokes:
        for s in (1, -1):
            hs = np.maximum(hs, stroke_mask(Dn, [(s * a, b) for a, b in pts], w0, w1))
    hs = np.maximum(hs, stroke_mask(Dn, [(0.0, 0.53), (0.0, 0.7)], 0.024, 0.01))
    hs = np.maximum(hs, stroke_mask(Dn, [(-0.05, 0.75), (0.05, 0.75)], 0.02, 0.02))
    hs *= 1 - ell_mask(P, H + P3(0, -0.21, -0.12), P3(0.12, 0.11, 0.1), 0.15)   # keep the muzzle clean
    stripe = np.where(hd, hs, stripe)

    # ---- ears: black backs with a white spot, pale insides
    for s, L in ((1, "L"), (-1, "R")):
        sel = is_("ear" + L)
        q = (P - J["ear" + L]) @ ear_rot(s)   # world to ear-local
        back = smoothstep(-0.006, 0.008, q[:, 1])
        spot = ell_mask(q, P3(0, 0, -0.012), P3(0.03, 1.0, 0.038), 0.25)
        inner = ell_mask(q, P3(0, 0, -0.006), P3(0.058, 1.0, 0.064), 0.15) * (1 - back)
        stripe = np.where(sel, back * (1 - spot), stripe)
        white = np.where(sel, np.maximum(back * spot, inner), white)
        col = np.where((sel & (inner > 0.5))[:, None], COL["ear_in"], col)

    # compose
    cream = COL["cream"] + (COL["shade"] - COL["cream"]) * smoothstep(0.2, -0.6, N[:, 2])[:, None] * 0.6
    col = col + (cream - col) * np.clip(white, 0, 1)[:, None]
    col = col + (COL["black"] - col) * np.clip(stripe, 0, 1)[:, None]
    mask = stripe.copy()

    # ---- nose, mouth, whisker dots, eye liner (head only)
    nose_q = np.linalg.norm((P - (H + P3(0, -0.27, -0.062))) / P3(0.058, 0.04, 0.036), axis=1)
    nose = (1 - smoothstep(0.95, 1.08, nose_q)) * hd
    rim = smoothstep(0.72, 0.9, nose_q) * nose
    nose_col = COL["nose"] + (COL["nose_dk"] - COL["nose"]) * np.clip(rim + smoothstep(-0.07, -0.085, Q[:, 2]), 0, 1)[:, None]
    col = col + (nose_col - col) * nose[:, None]

    front = (Q[:, 1] < -0.17) & hd
    xz = np.stack([Q[:, 0], np.zeros(n, np.float32), Q[:, 2]], 1)
    line = np.full(n, 9.0, np.float32)
    mouth = [((0, 0, -0.088), (0, 0, -0.124))]
    for s in (1, -1):
        mouth += [((0, 0, -0.124), (s * 0.024, 0, -0.14)), ((s * 0.024, 0, -0.14), (s * 0.052, 0, -0.137)),
                  ((s * 0.052, 0, -0.137), (s * 0.074, 0, -0.124))]
    for a, b in mouth:
        line = np.minimum(line, seg_dist(xz, P3(*a), P3(*b))[0])
    dots = np.full(n, 9.0, np.float32)
    for s in (1, -1):
        for i, j in ((0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1), (1, 2)):
            c = P3(s * (0.03 + 0.017 * i + 0.006 * j), 0, -0.102 - 0.014 * j)
            dots = np.minimum(dots, np.linalg.norm(xz - c, axis=1))
    ink = np.maximum(smoothstep(0.0045, 0.0025, line), smoothstep(0.0048, 0.0032, dots) * (Q[:, 1] < -0.2))
    liner = np.zeros(n, np.float32)
    for s in (1, -1):
        de = np.linalg.norm(P - eye_c(s), axis=1)
        top = (P[:, 2] - eye_c(s)[2]) > 0.0
        liner = np.maximum(liner, smoothstep(EYE_R + 0.011 + 0.004 * top, EYE_R + 0.005 + 0.004 * top, de))
    ink = np.maximum(ink * front, liner * hd)
    col = col + (COL["black"] - col) * ink[:, None]

    col *= (1 + 0.045 * vnoise(P, 45, 11))[:, None]
    return np.clip(col, 0, 1), np.clip(mask, 0, 1)


def bake(obj, fn, size):
    """Paints a texture by evaluating fn at the surface point under every texel of obj's UV layout."""
    me = obj.data
    me.calc_loop_triangles()
    nt = len(me.loop_triangles)
    tri_loops = np.zeros(nt * 3, np.int32)
    me.loop_triangles.foreach_get("loops", tri_loops)
    tri_loops = tri_loops.reshape(-1, 3)
    loop_v = np.zeros(len(me.loops), np.int32)
    me.loops.foreach_get("vertex_index", loop_v)
    uv = np.zeros(len(me.loops) * 2, np.float32)
    me.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    co = np.zeros(len(me.vertices) * 3, np.float32)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    no = np.zeros(len(me.vertices) * 3, np.float32)
    try:
        me.vertex_normals.foreach_get("vector", no)
    except AttributeError:
        me.vertices.foreach_get("normal", no)
    no = no.reshape(-1, 3)

    pix, pos, nrm = [], [], []
    T = uv[tri_loops] * size - 0.5
    V = co[loop_v[tri_loops]]
    Nv = no[loop_v[tri_loops]]
    for t in range(nt):
        a, b, c = T[t]
        x0, y0 = np.floor(np.minimum(np.minimum(a, b), c)).astype(int)
        x1, y1 = np.ceil(np.maximum(np.maximum(a, b), c)).astype(int)
        x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, size - 1), min(y1, size - 1)
        if x1 < x0 or y1 < y0:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        gx, gy = gx.ravel(), gy.ravel()
        den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(den) < 1e-12:
            continue
        w0 = ((b[1] - c[1]) * (gx - c[0]) + (c[0] - b[0]) * (gy - c[1])) / den
        w1 = ((c[1] - a[1]) * (gx - c[0]) + (a[0] - c[0]) * (gy - c[1])) / den
        w2 = 1 - w0 - w1
        inside = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
        if not inside.any():
            continue
        W = np.stack([w0, w1, w2], 1)[inside]
        pix.append(gy[inside] * size + gx[inside])
        pos.append(W @ V[t])
        nrm.append(W @ Nv[t])
    pix = np.concatenate(pix)
    P = np.concatenate(pos).astype(np.float32)
    N = np.concatenate(nrm).astype(np.float32)
    N /= np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-6)
    outs = fn(P, N)
    filled = np.zeros(size * size, bool)
    filled[pix] = True
    results = []
    for o in outs:
        ch = 1 if o.ndim == 1 else o.shape[1]
        img = np.zeros((size * size, ch), np.float32)
        img[pix] = o.reshape(len(pix), ch)
        results.append(dilate(img.reshape(size, size, ch), filled.reshape(size, size), 8))
    return results


def dilate(img, filled, steps):
    """Grows painted islands outwards so texture filtering never reaches the empty gaps."""
    img, filled = img.copy(), filled.copy()
    for _ in range(steps):
        acc = np.zeros_like(img)
        cnt = np.zeros(filled.shape, np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            f = np.roll(filled, (dy, dx), (0, 1))
            acc += np.roll(img, (dy, dx), (0, 1)) * f[..., None]
            cnt += f
        grow = (~filled) & (cnt > 0)
        img[grow] = acc[grow] / cnt[grow][:, None]
        filled |= grow
    return img


# ---------------------------------------------------------------- textures made directly in UV space

def eye_texture(size=256):
    v, u = (np.mgrid[0:size, 0:size] + 0.5) / size
    x, y = u - 0.5, v - 0.5
    r = np.sqrt(x * x + y * y)
    a = np.arctan2(y, x)
    sclera = hexc("#FBF8F0")[None, None] * (1 - 0.12 * smoothstep(0.3, 0.5, r))[..., None]
    streak = 0.5 + 0.5 * np.sin(a * 23 + np.sin(a * 7) * 2)
    iris = hexc("#3F8F2C") + (hexc("#A8DA64") - hexc("#3F8F2C")) * smoothstep(0.36, 0.14, r)[..., None]
    iris = iris * (0.9 + 0.12 * streak)[..., None]
    iris = iris + (hexc("#163A10") - iris) * smoothstep(0.32, 0.365, r)[..., None]
    col = sclera + (iris - sclera) * smoothstep(0.375, 0.362, r)[..., None]
    col = col + (hexc("#0D0907") - col) * smoothstep(0.155, 0.145, r)[..., None]
    for cx, cy, rad in ((-0.1, 0.11, 0.065), (0.085, -0.07, 0.026)):
        hl = smoothstep(rad, rad * 0.75, np.sqrt((x - cx) ** 2 + (y - cy) ** 2))
        col = col + (1 - col) * hl[..., None]
    return col


def weave_texture(w=1024, h=1024):
    """Wicker: strands over and under the stakes (v < 0.78), twisted rope above."""
    v, u = (np.mgrid[0:h, 0:w] + 0.5) / np.array([h, w])[:, None, None]
    base, light, dark = hexc("#A8733F"), hexc("#DDB070"), hexc("#3E2614")
    stakes, rows = 24, 26
    su, sv = u * stakes, v / 0.78 * rows
    col_i, row_i = np.floor(su), np.floor(sv)
    fu, fv = su - col_i, sv - row_i
    over = ((col_i + row_i) % 2) == 0
    rnd = np.sin(row_i * 12.9898 + 4.1) * 43758.5453
    tint = 0.85 + 0.25 * (rnd - np.floor(rnd))
    strand = np.sqrt(np.clip(1 - (2 * fv - 1) ** 2, 0, 1))
    stake = np.sqrt(np.clip(1 - ((2 * fu - 1) / 0.6) ** 2, 0, 1))
    shade = np.where(over, strand * (0.75 + 0.25 * np.sin(np.pi * fu)), np.maximum(stake * 0.9, strand * 0.35))
    gap = np.where(over, smoothstep(0.0, 0.12, fv) * smoothstep(1.0, 0.88, fv), np.maximum(stake > 0.05, 0.2))
    c = base + (light - base) * (shade * tint)[..., None]
    c = c * (0.35 + 0.65 * gap)[..., None]
    c = c + (dark - c) * ((1 - gap) * 0.5)[..., None]
    # rope band: diagonal twists
    rv = (v - 0.8) / 0.2
    tw = (u * 16 + rv * 2.0) % 1.0
    rope = np.sqrt(np.clip(1 - (2 * tw - 1) ** 2, 0, 1))
    rope_c = base + (light - base) * (rope * 0.9)[..., None]
    rope_c = rope_c * (0.45 + 0.55 * rope)[..., None]
    return np.where((v >= 0.8)[..., None], rope_c, np.where((v < 0.78)[..., None], c, base * 0.6))


def staff_texture(w=128, h=1024):
    """Bamboo along v (0 = bottom, 1 = top) with nodes and red rope wraps."""
    v, u = (np.mgrid[0:h, 0:w] + 0.5) / np.array([h, w])[:, None, None]
    along = v * STAFF_LEN - (STAFF_LEN / 2 - STAFF_GRIP)          # metres from the grip
    green, pale, node = hexc("#86B84E"), hexc("#B5D774"), hexc("#3F6B22")
    fib = 0.5 + 0.5 * np.sin(u * 2 * np.pi * 9 + np.sin(v * 40) * 0.6)
    c = green + (pale - green) * (0.25 + 0.2 * fib)[..., None]
    center = along + STAFF_GRIP                                    # metres from the middle
    nd = np.abs(((center + 0.15) % 0.3) - 0.15)
    c = c + (node - c) * smoothstep(0.012, 0.004, nd)[..., None]
    c = c + (pale - c) * (smoothstep(0.02, 0.012, nd) * smoothstep(0.004, 0.012, nd) * 0.6)[..., None]
    for y in (-0.42, -0.28, 0.9):
        band = smoothstep(0.037, 0.03, np.abs(center - y))
        tw = (u * 3 + center * 20) % 1.0
        rope = hexc("#C8323C") * (0.6 + 0.5 * np.sqrt(np.clip(1 - (2 * tw - 1) ** 2, 0, 1)))[..., None]
        c = c + (rope - c) * band[..., None]
    return c


# ---------------------------------------------------------------- parts

def build_body(coll):
    t = time.time()
    S = sculpt_body()
    verts, quads = surface_nets(S.F, S.lo, S.step)
    del S
    verts = taubin(verts, quads)
    print(f"  sculpture: {len(verts)} vertices in {time.time() - t:.1f}s")
    obj = mesh_object("Raja_Body", verts, quads, coll)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(obj.data)
    bm.free()

    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    dec = obj.modifiers.new("decimate", "DECIMATE")
    dec.ratio = min(1.0, BODY_TRIS / tris)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    old = obj.data
    obj.modifiers.clear()
    obj.data = me
    me.name = "Raja_Body"
    bpy.data.meshes.remove(old)
    smooth(me)

    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")

    t = time.time()
    col, mask = bake(obj, paint_body, TEX)
    print(f"  painted {TEX}px textures in {time.time() - t:.1f}s")
    albedo = image("Raja_Albedo", col)
    image("Raja_StripeMask", np.repeat(mask, 3, 2))
    obj.data.materials.append(material("Raja_Fur", albedo, rough=0.8, sheen=0.4))
    return obj


def build_eyes(coll):
    img = image("Raja_Eye", eye_texture())
    mat = material("Raja_Eye", img, rough=0.12, coat=0.6)
    objs = []
    for s, L in ((1, "L"), (-1, "R")):
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=28, v_segments=16, radius=EYE_R)
        fwd = Vector((s * 0.1, -1, 0.02)).normalized()
        q = Vector((0, 0, 1)).rotation_difference(fwd)
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=q.to_matrix())
        right = fwd.cross(Vector((0, 0, 1))).normalized() * -1
        up = right.cross(fwd).normalized() * -1
        uvl = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            for lp in f.loops:
                p = lp.vert.co
                lp[uvl].uv = (0.5 + p.dot(right) / (2 * EYE_R), 0.5 + p.dot(up) / (2 * EYE_R))
        me = bpy.data.meshes.new("Raja_Eye" + L)
        bm.to_mesh(me)
        bm.free()
        smooth(me)
        me.materials.append(mat)
        o = link(bpy.data.objects.new("Raja_Eye" + L, me), coll)
        o.location = Vector(eye_c(s).tolist())
        objs.append(o)
    return objs


def build_whiskers(coll):
    verts, faces = [], []
    for s in (1, -1):
        for i in range(4):
            start = HEAD + P3(s * (0.06 + 0.012 * i), -0.245 + 0.01 * i, -0.1 - 0.014 * i)
            d = P3(s * 1.0, -0.15 + 0.08 * i, -0.08 - 0.12 * i)
            d /= np.linalg.norm(d)
            length = 0.27 - 0.02 * abs(i - 1.5)
            path = [start + d * length * t + P3(0, 0, -0.06 * t * t) for t in np.linspace(0, 1, 7)]
            v, f, _ = tube(path, np.linspace(0.0032, 0.0008, 7), 5)
            faces += [tuple(x + len(verts) for x in face) for face in f]
            verts += list(v)
    obj = mesh_object("Raja_Whiskers", verts, faces, coll)
    obj.data.materials.append(material("Raja_Whisker", color=(0.95, 0.94, 0.9), rough=0.4))
    return obj


def basket_radius(z):
    t = z / BASKET_H
    return 0.15 + 0.06 * t ** 0.8 + 0.015 * math.sin(math.pi * t)


def build_basket(coll, body):
    """Wicker basket on Raja's back, with two vine straps over his shoulders."""
    seg, rows, depth = 32, 14, 0.78
    verts, faces, uvs = [], [], []

    def ring(z, r, inner=False):
        start = len(verts)
        for k in range(seg + 1):
            a = 2 * math.pi * k / seg
            verts.append((math.cos(a) * r, math.sin(a) * r * depth, z))
        return start

    outer = [ring(BASKET_H * i / rows, basket_radius(BASKET_H * i / rows)) for i in range(rows + 1)]
    inner = [ring(0.02 + (BASKET_H - 0.02) * i / rows, basket_radius(BASKET_H * i / rows) - 0.016) for i in range(rows + 1)]
    for rings, flip in ((outer, False), (inner, True)):
        for i in range(rows):
            for k in range(seg):
                a, b = rings[i] + k, rings[i + 1] + k
                f = (a, a + 1, b + 1, b)
                faces.append(f[::-1] if flip else f)
                for vi in (f[::-1] if flip else f):
                    kk = (vi - rings[0]) % (seg + 1)
                    ii = (vi - rings[0]) // (seg + 1)
                    uvs.append((kk / seg, 0.78 * ii / rows))
    for rings, z, flip in ((outer, 0.0, True), (inner, 0.02, False)):
        c = len(verts)
        verts.append((0, 0, z))
        for k in range(seg):
            f = (rings[0] + k, rings[0] + k + 1, c)
            faces.append(f[::-1] if flip else f)
            uvs += [(0.5, 0.79)] * 3
    v_list = [list(map(float, v)) for v in verts]

    # rim: a rope ring over the top edge
    rim_path = []
    for k in range(seg * 2 + 1):
        a = 2 * math.pi * k / (seg * 2)
        r = basket_radius(BASKET_H) - 0.008
        rim_path.append((math.cos(a) * r, math.sin(a) * r * depth, BASKET_H))
    rv, rf, ru = tube(rim_path, 0.022, 8, closed_ends=False, u_scale=1.2)
    off = len(v_list)
    v_list += [list(map(float, v)) for v in rv]
    faces += [tuple(x + off for x in f) for f in rf]
    uvs += ru

    obj = mesh_object("Raja_Basket", v_list, faces, coll, uvs)
    tilt = Matrix.Rotation(math.radians(-4), 4, "X")
    obj.matrix_world = Matrix.Translation(Vector(BASKET_BASE.tolist())) @ tilt
    img = image("Raja_Basket", weave_texture())
    mat = material("Raja_Basket", img, rough=0.85)
    obj.data.materials.append(mat)

    # straps: rough paths snapped onto the body surface
    bvh = BVHTree.FromObject(body, bpy.context.evaluated_depsgraph_get())
    sv, sf, su = [], [], []
    for s in (1, -1):
        ctrl = [(0.1, 0.2, 1.49), (0.14, 0.1, 1.6), (0.17, -0.06, 1.55), (0.165, -0.18, 1.38),
                (0.21, -0.14, 1.22), (0.26, 0.0, 1.19), (0.22, 0.14, 1.15), (0.13, 0.22, 1.1)]
        ctrl = [Vector((s * x, y, z)) for x, y, z in ctrl]
        path = []
        for i in range(len(ctrl) - 1):
            p0, p1, p2, p3 = ctrl[max(i - 1, 0)], ctrl[i], ctrl[i + 1], ctrl[min(i + 2, len(ctrl) - 1)]
            for t in np.linspace(0, 1, 7)[:-1]:
                t2, t3 = t * t, t * t * t
                path.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
        path.append(ctrl[-1])
        snapped = []
        for i, p in enumerate(path):
            hit, nrm, _, dist = bvh.find_nearest(p)
            if hit is not None and dist < 0.09 and 2 < i < len(path) - 3:
                p = hit + nrm * 0.022
            snapped.append(np.array(p, np.float32))
        snapped = np.array(snapped)
        for _ in range(3):
            snapped[1:-1] = snapped[1:-1] * 0.5 + (snapped[:-2] + snapped[2:]) * 0.25
        v, f, u = tube(snapped, 0.017, 6, u_scale=3.0)
        faces_off = len(sv)
        sv += list(v)
        sf += [tuple(x + faces_off for x in face) for face in f]
        su += u
    straps = mesh_object("Raja_Straps", sv, sf, coll, su)
    straps.data.materials.append(mat)
    return obj, straps


def build_staff(coll):
    """Bamboo staff; its origin is the grip, its local +Z runs to the long end."""
    seg, n = 12, 43
    verts, faces, uvs = [], [], []
    lo = -(STAFF_LEN / 2 - STAFF_GRIP)
    for i in range(n):
        v = i / (n - 1)
        z = lo + v * STAFF_LEN
        center = z + STAFF_GRIP
        bump = 0.006 * max(0.0, 1 - abs(((center + 0.15) % 0.3) - 0.15) / 0.02)
        r = 0.05 * (1 - 0.08 * v) + bump
        for k in range(seg + 1):
            a = 2 * math.pi * k / seg
            verts.append((math.cos(a) * r, math.sin(a) * r, z))
    for i in range(n - 1):
        for k in range(seg):
            a, b = i * (seg + 1) + k, (i + 1) * (seg + 1) + k
            faces.append((a, a + 1, b + 1, b))
            uvs += [(k / seg, i / (n - 1)), ((k + 1) / seg, i / (n - 1)), ((k + 1) / seg, (i + 1) / (n - 1)), (k / seg, (i + 1) / (n - 1))]
    for end in (0, n - 1):
        c = len(verts)
        verts.append((0, 0, lo + (end / (n - 1)) * STAFF_LEN))
        for k in range(seg):
            a = end * (seg + 1) + k
            faces.append((a + 1, a, c) if end == 0 else (a, a + 1, c))
            uvs += [(0.5, 0.004 if end == 0 else 0.996)] * 3
    obj = mesh_object("Raja_Staff", verts, faces, coll, uvs)
    obj.data.materials.append(material("Raja_Staff", image("Raja_Staff", staff_texture()), rough=0.45))
    # resting in the basket, leaning over his right shoulder
    bottom = Vector(BASKET_BASE.tolist()) + Vector((0.03, 0.0, 0.04))
    direction = Vector((-0.55, 0.14, 1.0)).normalized()
    q = Vector((0, 0, 1)).rotation_difference(direction)
    obj.matrix_world = Matrix.Translation(bottom - direction * lo) @ q.to_matrix().to_4x4()
    return obj


# ---------------------------------------------------------------- rig

BONES = [  # name, head, tail, parent
    ("Hips", "hips", "spine", None), ("Spine", "spine", "chest", "Hips"), ("Chest", "chest", "neck", "Spine"),
    ("Neck", "neck", "head", "Chest"), ("Head", "head", "head_end", "Neck"),
]
for _l in "LR":
    BONES += [("Shoulder." + _l, "clav" + _l, "sh" + _l, "Chest"), ("UpperArm." + _l, "sh" + _l, "el" + _l, "Shoulder." + _l),
              ("LowerArm." + _l, "el" + _l, "wr" + _l, "UpperArm." + _l), ("Hand." + _l, "wr" + _l, "tip" + _l, "LowerArm." + _l),
              ("UpperLeg." + _l, "hip" + _l, "kn" + _l, "Hips"), ("LowerLeg." + _l, "kn" + _l, "an" + _l, "UpperLeg." + _l),
              ("Foot." + _l, "an" + _l, "ball" + _l, "LowerLeg." + _l), ("Toes." + _l, "ball" + _l, "toe" + _l, "Foot." + _l)]


def build_rig(coll):
    data = bpy.data.armatures.new("Raja_Rig")
    arm = link(bpy.data.objects.new("Raja", data), coll)
    try:
        data.display_type = "STICK"
    except TypeError:
        pass
    arm.show_in_front = True
    activate(arm)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = data.edit_bones
    for name, h, t, parent in BONES:
        b = eb.new(name)
        b.head, b.tail = Vector(J[h].tolist()), Vector(J[t].tolist())
        if parent:
            b.parent = eb[parent]
            b.use_connect = (b.head - eb[parent].tail).length < 1e-4
    for s, L in ((1, "L"), (-1, "R")):
        b = eb.new("Ear." + L)
        b.head = Vector((J["ear" + L] - ear_rot(s) @ P3(0, 0, 0.06)).tolist())
        b.tail = Vector((J["ear" + L] + ear_rot(s) @ P3(0, 0, 0.06)).tolist())
        b.parent = eb["Head"]
    parent = eb["Hips"]
    for i in range(len(TAIL) - 1):
        b = eb.new(f"Tail{i + 1}")
        b.head, b.tail = Vector(TAIL[i].tolist()), Vector(TAIL[i + 1].tolist())
        b.parent = parent
        b.use_connect = i > 0
        parent = b
    for b in eb:  # Blender's convention: rolls that keep each bone's X pointing sideways
        b.roll = 0.0
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def skin(body, arm):
    """Automatic (bone heat) weights; any vertex left out gets distance-based weights instead."""
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    body.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    try:
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    except RuntimeError as e:
        print("  automatic weights failed:", e)
    if body.parent != arm:
        body.parent = arm
        body.modifiers.new("Armature", "ARMATURE").object = arm

    me = body.data
    co = np.zeros(len(me.vertices) * 3, np.float32)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    weighted = np.array([any(g.weight > 1e-3 for g in v.groups) for v in me.vertices])
    missing = np.nonzero(~weighted)[0]
    if len(missing):
        print(f"  {len(missing)} vertices had no weights; filling by distance")
        bones = [b for b in arm.data.bones if b.use_deform]
        d = np.stack([seg_dist(co[missing], np.array(b.head_local), np.array(b.tail_local))[0] for b in bones], 1)
        w = np.exp(-((d - d.min(1, keepdims=True)) / 0.03) ** 2)
        w /= w.sum(1, keepdims=True)
        for j, b in enumerate(bones):
            g = body.vertex_groups.get(b.name) or body.vertex_groups.new(name=b.name)
            for vi, wv in zip(missing, w[:, j]):
                if wv > 0.02:
                    g.add([int(vi)], float(wv), "REPLACE")


def attach(obj, arm, bone):
    """Parents a rigid part to a bone without moving it."""
    mw = obj.matrix_world.copy()
    obj.parent = arm
    obj.parent_type = "BONE"
    obj.parent_bone = bone
    bpy.context.view_layer.update()
    obj.matrix_world = mw


# ---------------------------------------------------------------- preview and export

def preview_scene(coll):
    scene = bpy.context.scene
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    if world.node_tree is None:
        world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.42, 0.55, 0.38, 1)
        bg.inputs[1].default_value = 0.9

    def light(name, kind, loc, rotation, energy, color, size=1.0):
        data = bpy.data.lights.new(name, kind)
        data.energy, data.color = energy, color
        if kind == "AREA":
            data.size = size
        o = link(bpy.data.objects.new(name, data), coll)
        o.location, o.rotation_euler = loc, rotation
        return o

    light("Raja_Key", "SUN", (-2, -3, 4), (math.radians(50), 0, math.radians(-35)), 2.6, (1.0, 0.94, 0.84))
    light("Raja_Rim", "SUN", (2, 3, 3), (math.radians(-55), 0, math.radians(150)), 1.6, (0.75, 0.9, 1.0))
    light("Raja_Fill", "AREA", (1.8, -2.5, 1.4), (math.radians(70), 0, math.radians(35)), 90, (0.85, 0.95, 1.0), 2.5)
    ground = bpy.data.meshes.new("Raja_Ground")
    ground.from_pydata([(-3, -3, 0), (3, -3, 0), (3, 3, 0), (-3, 3, 0)], [], [(0, 1, 2, 3)])
    g = link(bpy.data.objects.new("Raja_Ground", ground), coll)
    g.data.materials.append(material("Raja_Ground", color=(0.32, 0.45, 0.24), rough=0.95))
    cam = link(bpy.data.objects.new("Raja_Camera", bpy.data.cameras.new("Raja_Camera")), coll)
    scene.camera = cam
    return cam


def aim(cam, eye, target, lens=50):
    cam.location = eye
    cam.rotation_euler = (Vector(target) - Vector(eye)).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens


def render_previews(cam, arm, out_dir):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    try:
        scene.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    scene.render.film_transparent = False
    shots = []

    def shot(name, eye, target, lens, w, h):
        scene.render.resolution_x, scene.render.resolution_y = w, h
        aim(cam, eye, target, lens)
        scene.render.filepath = os.path.join(out_dir, f"preview_{name}.png")
        bpy.ops.render.render(write_still=True)
        shots.append(scene.render.filepath)

    shot("portrait", (-1.25, -2.6, 2.15), (0.0, 0.0, 1.82), 85, 900, 900)
    for name, a in (("front", 0), ("three_quarter", -40), ("side", -90), ("back", 180)):
        r = math.radians(a)
        shot(name, (math.sin(r) * 6.2, -math.cos(r) * 6.2, 1.35), (0, 0, 1.12), 50, 600, 900)
    pose(arm, True)
    shot("stance", (-3.5, -5.2, 1.7), (0, 0, 1.08), 50, 700, 900)
    pose(arm, False)
    return shots


def pose(arm, on):
    """A fighting stance to check the skinning; off returns to the rest pose."""
    angles = {
        "Spine": (0.1, 0, 0.1), "Chest": (0.05, 0, 0.15), "Head": (-0.05, 0, -0.15),
        "UpperArm.L": (-0.9, 0, 0.2), "LowerArm.L": (-1.5, 0, 0), "UpperArm.R": (-0.4, 0, -0.2), "LowerArm.R": (-1.6, 0, 0),
        "UpperLeg.L": (-0.45, 0, 0.1), "LowerLeg.L": (0.6, 0, 0), "UpperLeg.R": (0.35, 0, -0.1), "LowerLeg.R": (0.4, 0, 0),
        "Tail1": (0.3, 0, 0.3), "Tail3": (0.3, 0, 0.3), "Tail5": (-0.4, 0, 0.3), "Ear.L": (0.2, 0, 0),
    }
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = angles.get(pb.name, (0, 0, 0)) if on else (0, 0, 0)
    bpy.context.view_layer.update()


def export(coll, arm, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    for img in bpy.data.images:
        if img.name.startswith("Raja_"):
            img.filepath_raw = os.path.join(out_dir, img.name + ".png")
            img.file_format = "PNG"
            img.save()
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    arm.select_set(True)
    for o in arm.children:
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    path = os.path.join(out_dir, "Raja.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
                             add_leaf_bones=False, bake_anim=False, apply_scale_options="FBX_SCALE_ALL",
                             mesh_smooth_type="FACE", path_mode="STRIP")
    return path


def default_out_dir():
    here = None
    f = globals().get("__file__", "")
    if f and os.path.isfile(f):
        here = os.path.dirname(os.path.abspath(f))
    else:
        for t in bpy.data.texts:
            if t.filepath and os.path.basename(t.filepath) == "raja.py":
                here = os.path.dirname(bpy.path.abspath(t.filepath))
    if here:
        repo = os.path.abspath(os.path.join(here, "..", ".."))
        if os.path.isdir(os.path.join(repo, "Assets")):
            return os.path.join(repo, "Assets", "Art", "Raja")
        return os.path.join(here, "Raja")
    return os.path.join(os.path.expanduser("~"), "Raja")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_dir = argv[argv.index("--out") + 1] if "--out" in argv else default_out_dir()
    t0 = time.time()
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    cube = bpy.data.objects.get("Cube")   # Blender's untouched start-up cube would sit inside Raja
    if cube and cube.type == "MESH" and len(cube.data.vertices) == 8 and cube.location.length < 1e-6:
        bpy.data.objects.remove(cube, do_unlink=True)
    for name in ("Raja", "Raja Preview"):
        old = bpy.data.collections.get(name)
        if old:
            for o in list(old.objects):
                bpy.data.objects.remove(o, do_unlink=True)
            bpy.data.collections.remove(old)
    coll = bpy.data.collections.new("Raja")
    bpy.context.scene.collection.children.link(coll)
    extras = bpy.data.collections.new("Raja Preview")
    bpy.context.scene.collection.children.link(extras)

    print("Building Raja")
    body = build_body(coll)
    eyes = build_eyes(coll)
    whiskers = build_whiskers(coll)
    basket, straps = build_basket(coll, body)
    staff = build_staff(coll)
    arm = build_rig(coll)
    skin(body, arm)
    for o in eyes + [whiskers]:
        attach(o, arm, "Head")
    for o in (basket, straps, staff):
        attach(o, arm, "Chest")
    cam = preview_scene(extras)
    aim(cam, (-1.25, -2.6, 2.15), (0.0, 0.0, 1.82), 85)

    path = export(coll, arm, out_dir)
    print(f"Exported {path}")
    for area in (bpy.context.screen.areas if bpy.context.screen else []):
        if area.type == "VIEW_3D":
            area.spaces.active.shading.type = "MATERIAL"
    if "--render" in argv:
        for p in render_previews(cam, arm, out_dir):
            print("Rendered", p)
    print(f"Done in {time.time() - t0:.0f}s")


if __name__ == "__main__":
    main()

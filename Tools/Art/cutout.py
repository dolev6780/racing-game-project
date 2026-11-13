"""
Cuts a Canva painting out of its plain white background (the critters: Resources/Cloudborne/Jungle/Critters). The
white joined to the picture's border is flooded away, so white inside the painting (spots on a wing) stays; the
edge is softened a pixel, and the result is cropped to the painting with a little margin.

    python3 cutout.py IN.png OUT.png [THRESHOLD] [--even]

THRESHOLD (default 232) is how light a pixel must be, in every channel, to count as background. --even crops it
evenly about its middle (the painting's weight), for a creature split down the middle (the butterfly's wings).
"""

import sys
from collections import deque

from PIL import Image, ImageFilter

args = [a for a in sys.argv[1:] if not a.startswith("--")]
even = "--even" in sys.argv
src, out = args[0], args[1]
light = int(args[2]) if len(args) > 2 else 232

im = Image.open(src).convert("RGBA")
w, h = im.size
px = im.load()
bg = [[False] * w for _ in range(h)]
todo = deque()
for x in range(w):
    todo.append((x, 0)); todo.append((x, h - 1))
for y in range(h):
    todo.append((0, y)); todo.append((w - 1, y))
while todo:
    x, y = todo.popleft()
    if x < 0 or y < 0 or x >= w or y >= h or bg[y][x]:
        continue
    r, g, b, _ = px[x, y]
    if min(r, g, b) < light:
        continue
    bg[y][x] = True
    todo.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))

mask = Image.new("L", (w, h), 0)
mp = mask.load()
for y in range(h):
    for x in range(w):
        mp[x, y] = 0 if bg[y][x] else 255
# soften the cut a little (the paintings are outlined in dark ink, so no white fringe is left at the edge)
mask = mask.filter(ImageFilter.GaussianBlur(0.6))
im.putalpha(mask)
box = mask.point(lambda a: 255 if a > 8 else 0).getbbox()
pad = 3
box = (max(0, box[0] - pad), max(0, box[1] - pad), min(w, box[2] + pad), min(h, box[3] + pad))
if even:
    # the middle: where the painting's weight balances left and right
    cols = [sum(1 for y in range(h) if not bg[y][x]) for x in range(w)]
    mid = round(sum(x * c for x, c in enumerate(cols)) / max(1, sum(cols)))
    half = min(max(mid - box[0], box[2] - mid), mid, w - mid)
    box = (mid - half, box[1], mid + half, box[3])
im.crop(box).save(out)
print(f"wrote {out}: {box[2] - box[0]}x{box[3] - box[1]}")

# 把 Artifacts 里的五张阶段截图拼成一行预览图。用法: python3 montage.py <截图目录> <输出png>
import glob
import os
import sys

from PIL import Image, ImageDraw

source, output = sys.argv[1], sys.argv[2]
files = sorted(glob.glob(os.path.join(source, "0[1-5]-*.png")))
width, height, header = 576, 360, 28
canvas = Image.new("RGB", (width * len(files), height + header), (8, 10, 20))
draw = ImageDraw.Draw(canvas)
for index, path in enumerate(files):
    frame = Image.open(path).convert("RGB").resize((width, height), Image.LANCZOS)
    canvas.paste(frame, (index * width, header))
    draw.text((index * width + 10, 8), os.path.basename(path)[:-4], fill=(230, 220, 190))
canvas.save(output)
print(output)

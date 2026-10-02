"""Copy an unchanged generated atlas and write full-silhouette crop metadata."""
from pathlib import Path
from PIL import Image
from scipy import ndimage
import numpy as np
import json, shutil, sys

source, name, columns, rows = sys.argv[1:]
columns, rows = int(columns), int(rows)
im = Image.open(source).convert('RGBA')
labels, _ = ndimage.label(np.asarray(im)[:, :, 3] > 100)
sizes = np.bincount(labels.ravel())
parts = []
for i, slices in enumerate(ndimage.find_objects(labels), 1):
    if slices and sizes[i] > 1500:
        y, x = slices
        parts.append([x.start, y.start, x.stop-x.start, y.stop-y.start])
assert len(parts) == columns * rows, (name, len(parts))
parts.sort(key=lambda b: b[1]+b[3]/2)
boxes = []
for row in range(rows):
    for x,y,w,h in sorted(parts[row*columns:(row+1)*columns], key=lambda b:b[0]):
        l,t = max(0,x-2),max(0,y-2)
        r,b = min(im.width,x+w+2),min(im.height,y+h+2)
        boxes.append([l,t,r-l,b-t])
shutil.copyfile(source,Path('Assets')/(name+'.png'))
Path('Assets',name+'-frames.json').write_text(json.dumps(boxes))
print(name,im.size,len(boxes))

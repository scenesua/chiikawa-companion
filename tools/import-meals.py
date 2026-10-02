"""Import unchanged generated meal atlases; write crop metadata only."""
from pathlib import Path
from PIL import Image
from scipy import ndimage
import numpy as np
import json
import shutil

for entry in json.loads(Path('Assets/meal-generation.json').read_text(encoding='utf-8')):
    image = Image.open(entry['path']).convert('RGBA')
    labels, _ = ndimage.label(np.asarray(image)[:, :, 3] > 100)
    sizes = np.bincount(labels.ravel())
    parts = []
    for i, slices in enumerate(ndimage.find_objects(labels), 1):
        if slices and sizes[i] > 3000:
            y, x = slices
            parts.append([x.start, y.start, x.stop - x.start, y.stop - y.start])
    assert len(parts) == 12, (entry['id'], len(parts))
    parts.sort(key=lambda b: b[1] + b[3] / 2)
    boxes = []
    for row in range(3):
        for x, y, w, h in sorted(parts[row * 4:row * 4 + 4], key=lambda b: b[0]):
            left, top = max(0, x - 2), max(0, y - 2)
            right, bottom = min(image.width, x + w + 2), min(image.height, y + h + 2)
            boxes.append([left, top, right - left, bottom - top])
    target = Path('Assets') / (entry['id'] + '-meals.png')
    shutil.copyfile(entry['path'], target)
    target.with_name(entry['id'] + '-meal-frames.json').write_text(json.dumps(boxes))
    print(entry['id'], image.size, len(boxes))

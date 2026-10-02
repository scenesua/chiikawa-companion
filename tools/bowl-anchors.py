"""Measure shell width/floor for identical bowl geometry across food states."""
from pathlib import Path
from PIL import Image
import numpy as np,json
result={}
for image,metadata in [('meal-bowls','meal-bowl-frames'),('meal-bowls-eating','meal-bowl-eating-frames')]:
    alpha=np.array(Image.open(Path('Assets',image+'.png')).convert('RGBA'))[:,:,3]>100
    frames=[]
    for x,y,w,h in json.loads(Path('Assets',metadata+'.json').read_text()):
        yy,xx=np.where(alpha[y+h//2:y+h,x:x+w]);frames.append([int(xx.min()),int(xx.max()+1),int(yy.max()+1+h//2)])
    result[image]=frames
Path('Assets/bowl-anchors.json').write_text(json.dumps(result))

"""Inspect crop edges and neighboring silhouettes; write diagnostic contact sheets."""
import json
from pathlib import Path
from PIL import Image, ImageDraw
import numpy as np
from scipy import ndimage

out = Path('obj/sprite-audit'); out.mkdir(parents=True, exist_ok=True)
report = []
for meta in sorted(Path('Assets').glob('*frames.json')):
    name = meta.stem
    candidates = [name.replace('-atlas-frames','-atlas'), name.replace('-meal-frames','-meals'), name.replace('-snack-frames','-meals'), name.replace('-interaction-frames','-interactions'), name.replace('-scene-actor-frames','-scene-actors'), name.replace('-food-actions-frames','-food-actions'), name.replace('-affection-frames','-affection'), name.replace('-bowl-eating-frames','-bowls-eating'), name.replace('-bowl-frames','-bowls'), name.replace('-frames','')]
    if name=='momonga-frames': candidates.insert(0,'momonga-sprites')
    source = next((Path('Assets', s+'.png') for s in candidates if Path('Assets',s+'.png').exists()), None)
    if source is None: continue
    boxes = json.loads(meta.read_text()); im=Image.open(source).convert('RGBA')
    alpha=np.array(im)[:,:,3]>100
    labels,n=ndimage.label(alpha); sizes=np.bincount(labels.ravel())
    sheets=Image.new('RGB',(8*160,((len(boxes)+7)//8)*175),'#eeeeee'); draw=ImageDraw.Draw(sheets)
    for i,(x,y,w,h) in enumerate(boxes):
        crop=im.crop((x,y,x+w,y+h)); crop.thumbnail((150,150))
        sx=i%8*160+(160-crop.width)//2; sy=i//8*175+20+(150-crop.height)//2
        sheets.paste(crop,(sx,sy),crop); draw.text((i%8*160+5,i//8*175+3),str(i),fill='black')
        part=alpha[y:y+h,x:x+w]
        if part.shape!=(h,w) or not part.any(): report.append([str(meta),i,'outside/empty']); continue
        touching=int(part[0].sum()+part[-1].sum()+part[:,0].sum()+part[:,-1].sum())
        found=np.unique(labels[y:y+h,x:x+w]); found=[j for j in found if j and sizes[j]>250]
        intersect=[]
        for j in found:
            yy,xx=np.where(labels==j); inside=((xx>=x)&(xx<x+w)&(yy>=y)&(yy<y+h)).sum()
            if inside<sizes[j]*.985: intersect.append([int(j),int(inside),int(sizes[j])])
        if touching>5 or intersect: report.append([str(meta),i,'edge',touching,intersect])
    sheets.save(out/(name+'.jpg'))
(out/'report.json').write_text(json.dumps(report,indent=2))
print('suspect frames',len(report))
for p in sorted(set(r[0] for r in report)): print(p,sum(r[0]==p for r in report))

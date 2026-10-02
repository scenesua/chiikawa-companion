"""Repair metadata only: keep the complete main silhouette and remove neighboring poses."""
import json
from pathlib import Path
from PIL import Image
from scipy import ndimage
import numpy as np

changes=[]
for meta in sorted(Path('Assets').glob('*frames.json')):
    name=meta.stem
    if not ('-atlas-frames' in name or name in ['meal-bowl-frames','momonga-interaction-frames','momonga-affection-frames','momonga-food-actions-frames','kuromi-meal-frames','kuromi-meals-frames']): continue
    source=name.replace('-atlas-frames','-atlas').replace('-meal-frames','-meals').replace('-meals-frames','-meals').replace('meal-bowl-frames','meal-bowls').replace('-interaction-frames','-interactions').replace('-affection-frames','-affection').replace('-food-actions-frames','-food-actions')
    im=Image.open(Path('Assets',source+'.png')).convert('RGBA')
    labels,_=ndimage.label(np.array(im)[:,:,3]>100); sizes=np.bincount(labels.ravel()); slices=ndimage.find_objects(labels)
    mains=[j for j in range(1,len(sizes)) if sizes[j]>3000]
    centers={j:((slices[j-1][1].start+slices[j-1][1].stop)/2,(slices[j-1][0].start+slices[j-1][0].stop)/2) for j in mains}
    boxes=json.loads(meta.read_text()); result=[]
    for i,(x,y,w,h) in enumerate(boxes):
        ids,counts=np.unique(labels[y:y+h,x:x+w],return_counts=True)
        main=max(((c,j) for j,c in zip(ids,counts) if j and sizes[j]>1000),default=None)
        if main is None: raise ValueError((meta,i,'missing silhouette'))
        j=main[1]; sy,sx=slices[j-1]; left,top,right,bottom=sx.start,sy.start,sx.stop,sy.stop
        # Held utensils/food can be disconnected from the outline. Keep nearby parts belonging to this pose.
        for k,part in enumerate(slices,1):
            if k==j or part is None or sizes[k]<20 or sizes[k]>.20*sizes[j]: continue
            py,px=part
            cx,cy=(px.start+px.stop)/2,(py.start+py.stop)/2
            if min(mains,key=lambda m:(cx-centers[m][0])**2+(cy-centers[m][1])**2)!=j: continue
            if px.start>=left-12 and px.stop<=right+12 and py.start>=top-12 and py.stop<=bottom+12:
                left=min(left,px.start); right=max(right,px.stop); top=min(top,py.start); bottom=max(bottom,py.stop)
        left=max(0,left-2); top=max(0,top-2); right=min(im.width,right+2); bottom=min(im.height,bottom+2)
        b=[left,top,right-left,bottom-top]; result.append(b)
        if b!=[x,y,w,h]: changes.append([str(meta),i,[x,y,w,h],b])
    meta.write_text(json.dumps(result))
Path('obj/sprite-audit/repairs.json').write_text(json.dumps(changes,indent=2))
print('repaired crop rectangles',len(changes),'files',len(set(c[0] for c in changes)))

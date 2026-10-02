"""Record exact foreign-component scanline rectangles; images remain unchanged."""
import json
from pathlib import Path
from PIL import Image
from scipy import ndimage
import numpy as np

exclusions={}
for meta in sorted(Path('Assets').glob('*frames.json')):
    name=meta.stem
    source=name.replace('-atlas-frames','-atlas').replace('-meal-frames','-meals').replace('-snack-frames','-meals').replace('-meals-frames','-meals').replace('-interaction-frames','-interactions').replace('-scene-actor-frames','-scene-actors').replace('-food-actions-frames','-food-actions').replace('-affection-frames','-affection').replace('meal-bowl-eating-frames','meal-bowls-eating').replace('meal-bowl-frames','meal-bowls').replace('-frames','')
    if name=='momonga-frames':source='momonga-sprites'
    path=Path('Assets',source+'.png')
    if not path.exists() or name in ['momonga-scene-actor-frames','momonga-idle-frames','hand-cursors-frames']:continue
    image=np.array(Image.open(path).convert('RGBA'));labels,_=ndimage.label(image[:,:,3]>100);sizes=np.bincount(labels.ravel());slices=ndimage.find_objects(labels)
    mains=[j for j in range(1,len(sizes)) if sizes[j]>3000]
    if not mains:continue
    centers={j:((slices[j-1][1].start+slices[j-1][1].stop)/2,(slices[j-1][0].start+slices[j-1][0].stop)/2) for j in mains}
    owners={}
    for j,s in enumerate(slices,1):
        if s is None:continue
        sy,sx=s;cx,cy=(sx.start+sx.stop)/2,(sy.start+sy.stop)/2
        owners[j]=j if j in mains else min(mains,key=lambda m:(cx-centers[m][0])**2+(cy-centers[m][1])**2)
    frames=[]
    for x,y,w,h in json.loads(meta.read_text()):
        part=labels[y:y+h,x:x+w];ids,counts=np.unique(part,return_counts=True)
        main=max(((count,j) for j,count in zip(ids,counts) if j in mains),default=None)
        if main is None:frames.append([]);continue
        my, mx=slices[main[1]-1]
        def outside(j):
            sy,sx=slices[j-1]
            return sizes[j]<.2*sizes[main[1]] and (sx.stop<=mx.start or sx.start>=mx.stop or sy.stop<=my.start or sy.start>=my.stop)
        unwanted=[j for j in ids if j and (owners[j]!=main[1] or outside(j))]
        foreign=np.isin(part,unwanted)
        owned=np.isin(part,[j for j in ids if j and j not in unwanted])
        foreign=ndimage.binary_dilation(foreign,iterations=2) & ~ndimage.binary_dilation(owned,iterations=1)
        # Do not trim the silhouette: erase only alpha belonging to a different pose.
        runs=[];active={}
        for row in range(h):
            padded=np.pad(foreign[row].astype(int),(1,1));edges=np.diff(padded);starts=np.where(edges==1)[0];ends=np.where(edges==-1)[0]
            current={}
            for left,right in zip(starts,ends):
                key=(int(left),int(right));r=active.get(key)
                if r is not None:r[3]+=1
                else:r=[int(left),row,int(right-left),1];runs.append(r)
                current[key]=r
            active=current
        frames.append(runs)
    if any(frames):exclusions[name]=frames
# Two tiny connected remnants at the left edge overlap the main pose's bounding box.
for index, rectangle in [(34,[0,0,6,45]),(54,[0,0,5,65])]:
    exclusions['kuromi-atlas-frames'][index].append(rectangle)
Path('Assets/sprite-exclusions.json').write_text(json.dumps(exclusions,separators=(',',':')))
print('cleaned frames',sum(bool(f) for v in exclusions.values() for f in v),'sheets',len(exclusions))

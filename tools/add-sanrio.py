"""Add the two requested characters and their app-specific Korean dialogue."""
from pathlib import Path
import json

def write(path,data):
    Path(path).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')

base=json.loads(Path('Content/hachiware.json').read_text(encoding='utf-8'))
lines=json.loads(Path('Content/hachiware-dialogue.json').read_text(encoding='utf-8'))
profiles={
 'mymelody': ('마이멜로디','분홍 두건을 쓴 다정하고 밝은 토끼','#D28DA8','#FBE2ED',.4,.28,.66,.7),
 'kuromi': ('쿠로미','검은 두건 속에 소녀다운 마음을 감춘 장난꾸러기','#796395','#EDE1F5',.58,.38,.8,.4)
}
voices={
 'mymelody': {
  'Greeting':['안녕! 멜로디랑 같이 있자.','오늘도 만나서 기뻐.'],
  'ReturnGreeting':['돌아왔구나. 기다리고 있었어.'],
  'IdleTalk':['오늘은 어떤 일이 있었어?','멜로디랑 잠깐 쉬어 갈래?','쿠키를 같이 구우면 좋겠다.'],
  'Hungry':['배가 고파졌어. 밥 먹어도 될까?'], 'Thirsty':['목이 말라. 물을 부탁해도 돼?'],
  'SnackRequest':['달콤한 간식이 먹고 싶어.'], 'Attention':['멜로디랑 같이 있어 줄래?'],
  'Happy':['헤헤, 고마워!'], 'Annoyed':['그러면 아파… 살살 해 줘.'],
  'Sleepy':['졸려… 조금만 쉬고 올게.'], 'PetReaction':['포근해… 기분 좋아.'],
  'FlickReaction':['아야… 살살 해 줘.'], 'Meal':['잘 먹었어. 고마워!'],
  'Snack':['달콤해서 행복해.'], 'Snack:dessert':['케이크 맛있다! 같이 먹을래?'],
  'WaterReaction':['휴… 시원해.'], 'PraiseReaction':['헤헤, 정말? 고마워.'],
  'SootheReaction':['응, 이제 괜찮아.'], 'RefuseReaction':['그럼 다음에 같이 먹자.'],
  'WakeReaction':['음… 벌써 일어날 시간이야?']
 },
 'kuromi': {
  'Greeting':['쿠로미 님 등장!','왔어? 오늘도 잘 부탁해!'],
  'ReturnGreeting':['너무 오래 기다리게 하지 마!'],
  'IdleTalk':['오늘 일기는 뭘 적을까…','내 검은 두건, 멋지지?','흥, 심심해서 말 건 거야.'],
  'Hungry':['배고파! 밥 먹고 다시 놀자.'], 'Thirsty':['물 좀 채워 줘. 목말라!'],
  'SnackRequest':['간식 있지? 조금만 나눠 줘.'], 'Attention':['심심해! 같이 놀자!'],
  'Happy':['헤헷, 제법인데?'], 'Annoyed':['야! 너무하는 거 아니야?'],
  'Sleepy':['잠깐 쉴 거야. 깨우지 마!'], 'PetReaction':['흥… 나쁘진 않네.'],
  'FlickReaction':['아야! 가만 안 둬!'], 'Meal':['잘 먹었어! 다음에도 부탁해.'],
  'Snack':['헤헷, 달콤한 것도 좋네.'], 'Snack:pudding':['푸딩… 맛있잖아!'],
  'WaterReaction':['휴, 살 것 같네!'], 'PraiseReaction':['당연하지! …고마워.'],
  'SootheReaction':['알았어. 이번엔 봐줄게.'], 'RefuseReaction':['뭐야! 다음엔 꼭 줘.'],
  'WakeReaction':['으… 조금만 더 자면 안 돼?']
 }
}
for id,(name,desc,accent,soft,talk,wander,play,sleep) in profiles.items():
    data=json.loads(json.dumps(base))
    for key in ['CharacterId','AssetSet','DialogueSetId','AnimationSetId']: data[key]=id
    data.update(DisplayName=name,Category='마이멜로디',Description=desc,TalkFrequency=talk,WanderFrequency=wander,Playfulness=play,Sleepiness=sleep,
                Mischief=.2 if id=='mymelody' else .85,Patience=.8 if id=='mymelody' else .4,
                FavoriteFoods=['nuts'] if id=='mymelody' else ['furikake-rice'],FavoriteSnacks=['dessert','cookie'] if id=='mymelody' else ['pudding'],
                FavoriteToys=['doll'] if id=='mymelody' else ['ball'],PreferredBeds=['beanbag'] if id=='mymelody' else ['nest'],
                ItemPreferences={'dessert':1.7,'cookie':1.5,'nuts':1.3,'beer':.1} if id=='mymelody' else {'pudding':1.5,'furikake-rice':1.2,'beer':.3})
    data['Theme'].update(Name=name,Accent=accent,Soft=soft,Border=soft,Background='#FFF8FB' if id=='mymelody' else '#FAF7FD',Ink='#56414B' if id=='mymelody' else '#3E324E')
    write('Content/'+id+'.json',data)
    # Do not inherit another character's specific food lines.
    write('Content/'+id+'-dialogue.json',voices[id])
    path=Path('Assets/'+id+'-atlas-frames.json')
    frames=json.loads(path.read_text())
    for row in range(3):
        ordered=[frames[row*8+i] for i in [2,1,0,7,6,4,5,3]]
        if id=='kuromi' and row==0: ordered[0]=frames[10]
        frames[row*8:row*8+8]=ordered
    write(path,frames)
items=json.loads(Path('Content/items.json').read_text(encoding='utf-8'))
for item in items:
    if item['Id']=='meal': item['Utensil']='Chopsticks'
    if item['Id']=='dessert': item['Utensil']='Spoon'
    if item['Id']=='beanbag': item['Price']=4000
    if item['Id']=='nest': item['Price']=6500
write('Content/items.json',items)

# Momonga sprites

momonga-interactions.png and momonga-interaction-frames.json: 16-frame AI-generated sheet. Row 1: cushion sitting left/right and two asleep-on-cushion integrated scenes. Row 2: two eating and two drinking integrated scenes. Row 3: food full/half/empty, water full. Row 4: water half/empty, flick impact/recovery. Original sheet kept unchanged; WPF crops metadata at runtime.
Prompt: one transparent 4 by 4 sprite atlas matching existing Momonga and pastel furniture; single combined scenes physically sitting/sleeping inside cushion, muzzle in food or plain water bowl; full, half and empty standalone bowls; forehead flick impact and rubbing forehead reactions. No labels, text, grids, or background.

water-bowl.png: AI-generated transparent water bowl furniture. References: ui-icons.png food bowl and earlier water bowl. Prompt: compact chubby pastel lavender bowl matching the food bowl, dark purple outline, pale blue water and white glints. Final edit removed only the paw emblem, preserving the body and transparency.

Built-in `image_gen`으로 생성 및 수정했습니다. AI 생성물이며 공식 원본 아트는 아닙니다.
`momonga-sprites.png`: RGBA 1774 × 887, 32개 포즈.
`momonga-frames.json`: 순서대로 [x, y, width, height], 212 × 212 프레임.
AI 시트의 간격이 불규칙해 각각의 포즈를 감싸는 좌표를 지정했습니다.
크기와 발 기준선을 맞추고 PNG의 원본 알파를 유지합니다. 파일 자체의 그림은 후처리하지 않았습니다.

프레임 0–7: E, SE, S, SW, W, NW, N, NE 대기.
프레임 8–15: 같은 방향의 걷기 A. 16–23: 걷기 B.
프레임 24–31: Happy, Eat, Sleep, Annoyed, Sulk, Startled, Dragged, Play.
다른 시트로 교체할 때는 PNG와 좌표 JSON을 함께 변경합니다.

## Idle sprites

`momonga-idle.png`: RGBA 1774 × 887, 대기 8프레임.
`momonga-idle-frames.json`: 400 × 400 프레임 좌표, 발 기준 정렬.
순서: 앉기 / 눈 깜빡임 / 하품 / 졸기 / 몸단장 A / 몸단장 B / 기지개 / 기지개 끝.
기존 시트를 캐릭터 외형 참고로 사용해 built-in `image_gen`으로 생성했습니다.
원본 그림의 알파를 유지하며, WPF에서 좌표에 따라 잘라 표시합니다.

### Idle generation prompt

Use case: stylized-concept. Create a NEW transparent 4 columns x 2 rows sprite atlas of 8 idle animation frames for Momonga from Chiikawa, matching the reference atlas character exactly: white body, huge shiny black eyes, pink cheeks and ear interiors, blue-gray big fluffy tail, fine dark outlines, flat 2D anime art. The reference image is character/style reference only; do not reproduce the old sheet. All eight poses face front or slightly three-quarter. EXACTLY eight separate full-body sprites, consistent size and scale, centered in equal cells with generous transparent padding, tail fully in cell, soles aligned. First row left to right: 1 sitting calmly eyes open paws resting, 2 same sitting pose eyes fully closed blinking, 3 sleepy sitting yawning one paw at mouth, 4 same sleepy sitting with eyes closed head lowered. Second row left to right: 5 grooming paw raised to cheek, 6 grooming paw rubbing cheek (small change matching frame5), 7 stretching arms up eyes shut, 8 relaxing after stretch arms lowered. No props, text, sound effect symbols, hearts, floor, shadows, grid, decorative effects or background. True alpha transparency. Keep Momonga identity/proportions exactly as reference. Ideally 1024x512 canvas, strict regular 4x2 layout.

## Generation prompt

Use case: stylized-concept. Asset type: transparent PNG sprite atlas for Windows desktop virtual pet. Create Momonga from Chiikawa in the recognizable original anime/manga appearance: white small flying squirrel, pale blue-gray back and large fluffy blue-gray tail, round ears with pink inner ears, huge round black eyes with tiny white highlights, tiny mouth, short limbs, pink cheeks, shameless cute expressive face. NOT purple, NOT realistic photography. Clean crisp 2D hand-drawn anime sprites, consistent proportions, dark fine outlines, flat colors. Output a perfectly regular 8 columns by 4 rows sheet, ideally 2048 x 1024 pixels. EXACTLY 32 full-body separate sprites, one per cell, evenly centered, identical character scale, entire tail inside cell, plenty of transparent padding. No labels, no text, no grid lines, no background, no floor/shadows. Columns left to right are directions E, SE, S (front), SW, W, NW, N (back), NE. Row 1 is standing idle in each of the eight directions. Row 2 is walking step A in each direction. Row 3 is walking step B with alternate foot in each direction. Row 4 eight front-facing actions left to right: happy arms up, eating a small cookie, sleeping curled with tail, annoyed squint arms crossed, sulking turned partly away, startled ears up, gently held/dragged with dangling feet, playful crouch. Prioritize recognizable authentic Momonga design and very strict 8x4 sprite grid. True alpha transparency.

## Final edit prompt

Edit this transparent Momonga sprite atlas. Keep the same recognizable Chiikawa Momonga design, all 32 sprites and EXACT regular 8 columns x 4 rows layout and existing canvas aspect ratio 2:1. Keep rows 1-3 columns 1-7 essentially unchanged. Correct rows 1-3 column 8: this is NE, rear three-quarter facing toward the RIGHT; mirror the rear-left view in column 6 so the visible face is on the RIGHT and large blue-gray tail on the LEFT. This must be visually distinct from NW column 6. Remove the hand entirely from row 4 column 7: just Momonga held in midair pose, legs dangling, no external hand. Remove ALL decorative effects sparks flames hearts streaks from all sprites especially row4 col6. Repair any rough white fringes outside outlines, clean antialiased outline against real transparent alpha. Each sprite stays fully inside its own cell with transparent margin, including tail. Row4 poses remain happy, cookie eating, curled sleeping, annoyed, sulking, startled, dangling held pose, playful crouch. No text, grids, floor shadows or background. STRICT clean 8x4 atlas for automated equal-cell cropping.

## UI icons

ui-icons.png: AI-generated transparent atlas, 5 columns by 4 rows. Runtime crops preserve original pixels.
Prompt: Twenty custom pastel pictogram icons, lavender outlines and mint/blue fills, no text or emoji. Rows: heart, bowl, ball, fingertip, speech; quiet home, more, sliders, shop, box; utensils, water, energy, social, fun; AP coin, bed, debug, close, back. Transparent background, evenly spaced grid.

## 2026-10-02 interaction resources

Generated with the built-in imagegen tool. Original PNGs copied unchanged; JSON files describe runtime crops. Human hands are cursor resources only; Momonga sprites contain no external hands.

- `C:/Chiikawa Companion/Assets/momonga-affection.png`: 4×3 atlas, 12 frames. Prompt: Same reference Momonga, front-facing, consistent body and cheek shape. Row 1 neutral, head-petted delighted, chin-scratched delighted, proud. Row 2 mild surprised, moderate annoyed, strong teary angry, soothed. Row 3 play A/B with lavender ball, poke startled/annoyed. No human hands, text or props other than the play ball. Transparent background. Runtime expression overlays preserve the neutral body; only the grabbed cheek is deformed by drag input.
- `C:/Chiikawa Companion/Assets/hand-cursors.png`: 2×2 atlas. Prompt: Four isolated cartoon hands, palm-down head strokes, palm-up chin scratches, thumb/index pinch, prepared finger flick. Pale skin, dark lavender outlines, no sleeves/text, equal cells and transparent background.
- `C:/Chiikawa Companion/Assets/momonga-food-actions.png`: 4×6 atlas, 12 foods with two frames each. Prompt: Reference Momonga holding/eating cookie, strawberry cake, star candy, dorayaki, pudding with spoon, beer mug, furikake rice with chopsticks, curry rice with spoon, plain rice with chopsticks, jiro ramen with chopsticks, nuts, fruit. No external human hands or floor furniture. Equal cells, transparent background.
- `C:/Chiikawa Companion/Assets/bowl-contents.png`: 3×2 atlas. Prompt: Six isolated food heaps viewed from gentle elevated front angle: white rice, furikake rice, curry rice, jiro ramen with pork/cabbage/sprouts, mixed nuts, strawberry/apple pieces. Cute black outlines, no bowl/plate/hands/character/text, transparent background.

Each atlas has a companion `*-frames.json`. Furniture anchors are normalized scene rectangles in `momonga-furniture-anchors.json`; furniture world position and scale remain fixed when a scene changes.

### Corrected flick cursor

`C:/Chiikawa Companion/Assets/flick-cursor.png` replaces the fourth atlas hand at runtime. Generated using the built-in imagegen tool; original PNG preserved, tight runtime crop in `flick-cursor-frames.json`.

Final prompt (reference-guided replacement): Turn the user's supplied line drawing into a clean colored cartoon hand cursor. Preserve the reference pose, finger arrangement, contours and orientation exactly: wrist enters from left, long index finger extends toward upper right, middle finger bends down sharply and its tip meets the thumb below, the two remaining fingers extend behind the index as depicted. Trace the supplied drawing rather than inventing another pose. Keep the large open space between index and thumb and the bent middle finger inside that space. Change only visual finish: warm pale peach fill, minimal shading, crisp dark lavender outline. Transparent background; no white rectangle, glow, shadow, motion marks, text, sleeve or extra fingers. Built-in imagegen, using the user's reference image. Runtime cursor hotspot is (22, 19), at the thumb/middle-finger contact.

## 2026-10-02 canonical anime revision

## Additional character atlases

`{id}-atlas.png` originals are copied unchanged from built-in imagegen outputs; all selected paths and final prompts are in `character-generation.json`. JSON frame rectangles preserve complete connected silhouettes. Directional/reaction/idle/affection/furniture-actor/snack frames share a 240×240 runtime canvas with one uniform scale per character; NE uses native mirroring of NW. Missing happy cells reuse that character's proud pose, and generated happy/eating order is corrected in metadata.

Design references (original manga or official flat illustration, no dolls):
- Chiikawa/Hachiware/Usagi/Momonga/Kurimanju/Shisa/Kani/Rakko: https://www.anime-chiikawa.jp/chara.html and https://www.anime-chiikawa.jp/images/characters/img_charapage.png (`obj/official-anime-models.png`).
- Rilakkuma/Korilakkuma: https://www.san-x.co.jp/ja/characters/rilakkuma/ (`obj/official-rilakkuma.webp`, `obj/official-korilakkuma.webp`).
- Anoko: original Nagano manga https://x.com/ngnchiikawa/status/1439183389412638734 ; reference `obj/original-anoko-night.jpg`, especially the full-body front flying panel and chest fur outline. The chest is organic scalloped fur, not a geometric heart badge.
- Dekatsuyo: original manga media https://pbs.twimg.com/media/EgXSKAmVAAA2_Ba?format=jpg&name=medium (`obj/official-monster-manga.jpg`).
- Goblin: original Nagano manga https://x.com/ngnchiikawa/status/1455109916276887553 ; unaltered reproduction `obj/original-goblin-soup.webp` obtained from https://anna-movies.com/chiikawa/character/goblins/ . Green pointed-eared character, not the yellow cyclops in the same chapter.
- Chiikabu: original Nagano manga https://x.com/ngnchiikawa/status/1371754158785654785 ; unaltered reproduction `obj/original-beetle.webp` obtained from https://anna-movies.com/chiikawa/character/chiikabu/ .
- Ode: original Nagano manga https://x.com/ngnchiikawa/status/1453745521164570625 ; unaltered reproduction `obj/original-goblin.webp` obtained from the Goblin chapter archive above. Original giant body proportions retained.

AI outputs are game artwork, not official model sheets. Rejected posters, the compact Ode version, pink Chimera, and geometric-heart Anoko variants are excluded from the selected assets. Character monster transformations and armor knights are not included.

Built-in imagegen; selected PNG originals copied unchanged. Runtime JSON crops, uniform scaling and native WPF mirroring preserve the source artwork. Toy/plush references and unused generation variants are excluded. Original cushion icon retained with its natural proportions; it shares the bowls' nominal width. The interaction actor is rendered separately above the fixed furniture, avoiding furniture replacement or distortion.

References used: https://www.anime-chiikawa.jp/chara.html ; https://www.anime-chiikawa.jp/images/bg_top_momonga_01.png ; https://www.anime-chiikawa.jp/images/characters/img_charapage.png ; https://www.chiikawaofficial.com/characters ; user-supplied anime tail-hug still. Additional original manga inspection: https://x.com/ngnchiikawa/status/1996547718450438232 (reproduction linked by https://chiikawapark.com/comics/12815/). No physical dolls or figures are canonical design references.

Selected assets / prompt specifications:
- C:/Chiikawa Companion/Assets/momonga-sprites.png: 32 poses, 8x4. Same original anime model as references; white compact pear body, blue nose and inner ears, pink cheek hatches, sparse brown ink, long blue tail. Idle/walk A/walk B in eight directions; happy/eat without bowl/sleep/annoyed/sulk/startled/carried without hands/playful crouch. Consistent head scale and baseline, alpha transparency. Direction metadata remaps generated columns; SE and NE mirror their opposite diagonal views at runtime.
- C:/Chiikawa Companion/Assets/momonga-idle.png: 12 poses, 4x3. Neutral/blink, seated sleepy/closed eyes, cheek/belly grooming, two stretches, four tail-hug poses matching the supplied anime still. Original anime only, no human hands, consistent scale and baseline, transparent gaps.
- C:/Chiikawa Companion/Assets/momonga-affection.png: 12 poses, 4x3. Neutral, head-petted, chin-rubbed, proud; light/medium/strong cheek expressions, soothed; play A/B, poke and blush. Resting cheek outline preserved; deformation is driven by the pointer at runtime. No drawn human hands.
- C:/Chiikawa Companion/Assets/momonga-scene-actors.png: 8 actor-only poses, 4x2. Sitting front/aside, lying asleep A/B; own paw scooping rice into mouth with a lavender spoon/lowering spoon toward the placed bowl; drinking/licking. Absolutely no bowls, plates, cushions or other furniture in the actor atlas. The game composes the actor with the actual placed furniture. Measured white body widths normalize character scale across the poses.
- C:/Chiikawa Companion/Assets/momonga-food-actions.png: 12 snack-only poses, 6x2. Cookie, strawberry dessert, special candy, dorayaki, pudding with spoon, beer; two frames per snack. Removed all handheld meal poses and background; alpha transparent, unchanged snack silhouettes and colors.
- C:/Chiikawa Companion/Assets/meal-bowls.png: 16 sprites, 4x4. Exact lavender bear-face bowl with little lavender spoon from the interaction reference. Full/half white rice, furikake rice, curry rice, jiro ramen, nuts and fruit. Last row empty bear/spoon bowl and matching plain water bowl full/half/empty, no paw print. Identical shell placement, natural proportions, amounts change through distinct sprites rather than stretching food.
- C:/Chiikawa Companion/Assets/momonga-interactions.png: reference/legacy reactions, 16 cells. Actual placed furniture is composed with the actor-only atlas; handheld meal frames are never used for bowl eating.
- C:/Chiikawa Companion/Assets/furniture.png: unchanged original UI atlas; one crop selects the cushion, preserving its natural aspect ratio.
- C:/Chiikawa Companion/Assets/wood-quiet-sign.png: final prompt: Replace this framed twolegged sign with a REAL SIMPLE WOODEN PLANK ON ONE WOODEN STICK. One rough horizontal warmbrownwoodplank board, irregular cut corners slightwoodgrain thinbrownanimeoutline, EXACT black handpainted Korean '방해금지' directly on the wood. A single vertical narrow wooden stake attached at the center underneath. NOTHING ELSE: NO frame NO cream inset NO decorativeborder NOmoon NOtwolegs NO freestandingbase NO feet. Literal rusticwoodplankandonewoodstick like a handmade garden sign. Frontview mildperspective, plankwidth80%image, boardheight25%image, centralstickextendingdown35%image, alpha transparentoutsideobject NOglowshadowsbackground. Cute simple flat gameart, naturalwoodbrown texturebutnotphoto. Keep Koreantextreadable onwood. Entirepoleandboardfitwithtransparentmargin.

Crop repair: momonga-frames.json uses complete alpha-component bounds rather than assumed uniform cells. Runtime pads each complete pose to a shared 240x240 canvas and foot baseline; no pose is stretched to fill its crop. Main-frame checks reject opaque crop edges.

Character themes: native WPF rendering recolors the lavender shell/cushion/toy palette using the selected character's Soft color; food, water, wood and geometry stay intact. Accent silhouettes come from unchanged `Assets/official-anime-models.png` crops for the eight anime cast members, with generated portraits for other characters. Menu headers show the same emblem. The official strip is the original source linked above, not an AI model sheet.

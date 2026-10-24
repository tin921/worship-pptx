"""Prepare the 240 individually prompted, original background images."""
from pathlib import Path
import json

root=Path(__file__).resolve().parents[1]
scenes=json.loads((root/'docs/research/chinese-worship-corpus/master-scenes.json').read_text(encoding='utf-8'))
styles={
 'photo':'Photorealistic premium landscape or still-life photography, believable materials, natural light, excellent composition. For symbolic or heavenly scenes use tasteful cinematic realism. No text on books or objects.',
 'illustration':'Sophisticated hand-painted editorial illustration, subtle watercolor and gouache textures, layered depth, graceful organic shapes. Clearly illustrated rather than a photograph. Restrained contemporary church presentation artwork.',
 'abstract':'Elegant abstract visual interpretation using layered translucent shapes, softly illuminated sculptural forms and flowing gradients. Retain a subtle recognizable visual reference to the theme but avoid a literal photograph. Refined, spacious, understated.'
}
plan=[]
for scene in scenes:
 for style,description in styles.items():
  prompt=f'''Create ONE finished background image, landscape 16:9, intended output 1280 x 720 pixels, for a Chinese Christian worship presentation.
Theme: {scene['label']}. Meaning and visual direction: {scene['description']}.
Style: {description}
Composition: keep the upper 50 percent extremely quiet and low-detail, reserved for lyric text. Place the main subject, detail and visual interest in the lower half. Leave crop room around edges for a slow 3 percent zoom and gentle pan. Choose an appealing scene-specific palette with calm, contemplative, hopeful lighting. The result should look beautifully finished at full screen, with neither busy ornament nor flat unfinished emptiness in the lower half.
Constraints: NO words, lettering, typography, lyrics, watermarks, publisher logos, borders, contact sheets or collage. One full-bleed image only. No frightening or graphic imagery; for sacrifice/redemption themes keep symbolism peaceful and non-graphic. No distracting high-contrast details in the upper lyric area. Original art, not an existing album cover or worship video still.'''
  if style=='illustration':
   prompt=f'''Use case: illustration-story. Create ONE clearly hand-drawn gouache editorial illustration, a 16:9 worship background intended for 1280x720. Subject: {scene['label']} ({scene['query']}). The entire image is flat stylized painted art: broad simplified shapes, visible brush strokes and paper grain, no detailed realistic textures. A refined illustrated storybook aesthetic, restrained colors appropriate to this theme. Upper 50 percent is quiet low-detail sky or a smooth painted backdrop. Main illustrated subjects only in lower half, with crop room for 3 percent zoom. No photograph, no photorealism, no cinematic camera lighting, no realistic surface detail. No text, lettering, logos, collage or borders. Peaceful and hopeful, never graphic or frightening.'''
  if style=='abstract':
   prompt=f'''Use case: stylized-concept. Create ONE strictly abstract digital artwork, a 16:9 worship background intended for 1280x720. Only geometric forms, translucent paper-cut shapes, softly glowing curved ribbons and flowing gradients. Suggest the concept of {scene['label']} ({scene['query']}) through symbolic geometry and color, NEVER through realistic objects or a photographed landscape. Refined deep colors and restrained glowing accents suited to the theme. Entire upper 50 percent is a smooth quiet gradient. Arrange the abstract shapes in lower half with crop room for slow 3 percent zoom. Elegant computer-generated art, clearly non-photographic. No realistic terrain, no detailed trees, no rocks, no camera or sunrise photograph. No words, letters, logos, collage, or borders. One full-bleed finished abstract image.'''
  if scene['id']=='mountain_path' and style=='abstract':
   prompt='Use case: stylized-concept. Create a 16:9 landscape abstract worship background, 1280x720 intended. Strict abstract digital art: overlapping translucent origami triangles and softly glowing curved ribbons suggest mountains, valleys and a winding path. Use a refined deep teal, dusty blue and warm gold palette. Entire upper half is a smooth quiet dark teal gradient; arrange the geometric forms only in the lower half. Sharp elegant paper-cut silhouettes, curved translucent layers, soft graphic gradients. Clearly non-photographic. No realistic terrain, no trees, no rocks, no camera, no sunrise photograph. No words or logos. One full-bleed finished abstract image, no collage.'
  if scene['id']=='mountain_path' and style=='illustration':
   prompt='Use case: illustration-story. Create ONE clearly hand-drawn gouache editorial illustration, a 16:9 worship background intended for 1280x720. A winding mountain path through layered valleys. The entire image is flat stylized painted art: broad simplified shapes, visible brush strokes and paper grain, no detailed realistic textures. Rounded sage-green and dusty blue hills, a pale peach path, pale apricot sky. Illustration quality like a refined illustrated storybook landscape. Upper half is quiet pale sky with a few simple soft cloud shapes. Main illustrated path and simplified plants only in lower half. No photograph, no photorealism, no cinematic camera lighting, no realistic rocks. No text, lettering, logos, collage, or borders.'
  plan.append({'id':scene['id']+'-'+style,'scene':scene['id'],'title':scene['label'],'core':scene['in_selected_60'],'style':style,'prompt':prompt,'destination':f"assets/media/themes/{scene['id']}/{style}.jpg"})
(root/'assets/media').mkdir(parents=True,exist_ok=True)
(root/'assets/media/generation-prompts.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'{len(scenes)} themes; {len(plan)} separately prompted images')

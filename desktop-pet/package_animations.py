"""Encode the native renderer's transparent PNG frames for review."""
from pathlib import Path
from PIL import Image
import json
import sys

v4 = '--v4' in sys.argv
v3 = '--v3' in sys.argv or v4
out = Path(__file__).resolve().parent.parent / 'output' / ('桌宠动画-v4' if v4 else '桌宠动画-v3' if v3 else '桌宠动画-v2')
groups = {
    'greeting': ('打招呼', 50, True),
    'left': ('左脚单脚-独立新动画', 33, True),
    'scatter': ('散落', 50, False),
    'reassemble': ('重新组装', 50, False),
}
if v3:
    groups = {
        'greeting': ('打招呼', 33, True),
        'left-loop': ('左脚-荒诞持续挣扎', 33, True),
        'scatter-to-normal': ('零件组装-双脚站稳', 33, True),
        'scatter-to-left': ('零件组装-左脚持续挣扎', 33, True),
        'scatter-to-right': ('零件组装-右脚持续挣扎', 33, True),
    }
if v4:
    groups['left-loop'] = ('左脚-保持比例的持续挣扎',33,True)
for group, (name, delay, loop) in groups.items():
    frames = [Image.open(p).convert('RGBA') for p in sorted((out/'frames'/group).glob('*.png'))]
    frames[0].save(out/f'{name}.apng', save_all=True, append_images=frames[1:], duration=delay, loop=0 if loop else 1, disposal=0, blend=0)
    gif = []
    for rgba in frames:
        pal = rgba.convert('RGB').quantize(colors=255)
        palette = pal.getpalette() or []
        pal.putpalette((palette+[0]*768)[:765]+[0,0,0])
        pal.paste(255, mask=rgba.getchannel('A').point(lambda a:255 if a<128 else 0))
        pal.info['transparency']=255
        gif.append(pal)
    gif[0].save(out/f'{name}.gif', save_all=True, append_images=gif[1:], duration=delay, loop=0 if loop else 1, disposal=2, transparency=255, optimize=False)
    check=Image.open(out/f'{name}.apng')
    total=0
    for i in range(check.n_frames):
        check.seek(i)
        total+=check.info.get('duration',0)
    assert abs(total-len(frames)*delay)<2, (group,total,len(frames)*delay)
manifest={
 'normal': 'User supplied original PNG, unchanged bytes; rendered uniformly at 60% scale.',
 'left': 'New imagegen reference-conditioned 12-pose sheet. No mirroring. Cup stays on viewer right. Planted foot registered at (195,563).',
 'greeting': 'Original pixels; only viewer-left arm rotates about its original attachment.',
 'scatter': 'Original flat design pixels separated and simulated independently.',
 'right': 'Existing experimental R animation retained; not regenerated or promoted to final.',
 'generator': 'Built-in image_gen tool',
}
if v3:
    manifest = {
        'normal': 'Original user image is displayed directly after assembly, unchanged.',
        'left': '96-frame independently authored original-design cutout animation, 3.2 second periodic tracks. Body is always tilted and right foot is always raised. Eye whites, pupils, mouth, bow, cap and limbs have separate delayed tracks. No neutral beat or double-support pose.',
        'transitions': 'Three interruptible staggered spring assembly routes: scatter to normal, scatter directly to left struggle, scatter directly to existing right struggle. Current poses are retained when input changes.',
        'right': 'Existing experimental R action retained; only the assembly route is new.',
        'generator': 'Native WPF layer and rubber-hose animation renderer; source design pixels plus code-authored motion. No new AI-generated raster character.',
        'validation': '96 non-neutral left frames, raised free foot, periodic seam equality, three assembly destinations, finite transforms, interruption continuity. See frames/result.txt and left-motion-evidence.json.',
    }
if v4:
    manifest.update({
        'left': '96-frame constant-proportion animation: body, eyes, pupils and accessories all have unit scale. Bounded position shifts and rotations express imbalance. Limb attachment points slide along the body boundary. Free leg stays lifted. No neutral pose in the loop.',
        'mouth': 'Fixed face anchor; smile-to-tense expression changes inside a compact local area, no translation or stretching.',
        'collar': 'User supplied 191x58 transparent white two-lobed collar, rigidly drawn in the body coordinate system. Bow moves only a few pixels at the neckline.',
        'validation': 'All 96 frames verify unit scales, fixed mouth anchor, continuous imbalance, raised free foot, seamless wrap; all three assembly routes exported and interrupt continuity tested.',
    })
(out/'animation-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('Encoded four animation groups with alpha; verified total playback durations (identical frames may merge).')

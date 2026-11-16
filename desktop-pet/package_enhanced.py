from pathlib import Path
from PIL import Image

out=Path(__file__).resolve().parent.parent/'output'/'桌宠增强版接入'
frames=[]
for p in sorted((out/'frames/normal-idle').glob('*.png')):
    rgba=Image.open(p).convert('RGBA')
    pal=rgba.convert('RGB').quantize(colors=255)
    palette=pal.getpalette() or []
    pal.putpalette((palette+[0]*768)[:765]+[0,0,0])
    pal.paste(255,mask=rgba.getchannel('A').point(lambda a:255 if a<128 else 0))
    frames.append(pal)
frames[0].save(out/'双键站稳-眨眼晃杯.gif',save_all=True,append_images=frames[1:],duration=[[30,30,40][i%3] for i in range(len(frames))],loop=0,disposal=2,transparency=255,optimize=False)
print('Encoded idle blink, cup sway and greeting preview at the renderer timing.')

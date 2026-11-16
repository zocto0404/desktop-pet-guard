from pathlib import Path
from PIL import Image

out=Path(__file__).resolve().parent.parent/'output'/'桌宠眨眼预览'
frames=[]
for p in sorted((out/'frames').glob('frame-*.png')):
    rgba=Image.open(p).convert('RGBA')
    pal=rgba.convert('RGB').quantize(colors=255)
    palette=pal.getpalette() or []
    pal.putpalette((palette+[0]*768)[:765]+[0,0,0])
    pal.paste(255,mask=rgba.getchannel('A').point(lambda a:255 if a<128 else 0))
    frames.append(pal)
frames[0].save(out/'三种小人-错峰眨眼.gif',save_all=True,append_images=frames[1:],duration=33,loop=0,disposal=2,transparency=255,optimize=False)
print('Encoded transparent close-up preview from the desktop renderer.')

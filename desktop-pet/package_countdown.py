from pathlib import Path
from PIL import Image

out=Path(__file__).resolve().parent.parent/'output'/'桌宠倒计时接入'
frames=[]
for p in sorted((out/'frames').glob('frame-*.png'))[::2]:
    rgba=Image.open(p).convert('RGBA').resize((640,360),Image.Resampling.LANCZOS)
    pal=rgba.convert('RGB').quantize(colors=255)
    palette=pal.getpalette() or []
    pal.putpalette((palette+[0]*768)[:765]+[0,0,0])
    pal.paste(255,mask=rgba.getchannel('A').point(lambda a:255 if a<128 else 0))
    pal.info['transparency']=255
    frames.append(pal)
frames[0].save(out/'透明倒计时预览.gif',save_all=True,append_images=frames[1:],duration=100,loop=0,disposal=2,transparency=255,optimize=False)
assert Image.open(out/'frames/frame-000.png').getchannel('A').getbbox() is None
assert Image.open(sorted((out/'frames').glob('frame-*.png'))[-1]).getchannel('A').getbbox() is None
assert Image.open(out/'frames/frame-040.png').getchannel('A').getpixel((0,0))==0
print('PASS: native frames have transparent background; initial and completed frames are empty; transparent preview encoded.')

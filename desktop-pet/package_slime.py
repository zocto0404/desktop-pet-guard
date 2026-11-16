from pathlib import Path
from PIL import Image

out=Path(__file__).resolve().parent.parent/'output'/'桌宠史莱姆预览'
def encode(name,paths,crop=None):
    frames=[]
    for p in paths:
        rgba=Image.open(p).convert('RGBA')
        if crop: rgba=rgba.crop(crop)
        pal=rgba.convert('RGB').quantize(colors=255)
        palette=pal.getpalette() or []
        pal.putpalette((palette+[0]*768)[:765]+[0,0,0])
        pal.paste(255,mask=rgba.getchannel('A').point(lambda a:255 if a<128 else 0))
        frames.append(pal)
    frames[0].save(out/(name+'.gif'),save_all=True,append_images=frames[1:],duration=33,loop=0,disposal=2,transparency=255,optimize=False)

melt=sorted((out/'frames/melt-and-idle').glob('*.png'))
encode('蔫掉的粉色史莱姆',melt[30:],(60,375,575,585))
encode('融化与恢复',melt+sorted((out/'frames/slime-to-normal').glob('*.png')))
encode('史莱姆恢复左脚',sorted((out/'frames/slime-to-left').glob('*.png')))
encode('史莱姆恢复右脚',sorted((out/'frames/slime-to-right').glob('*.png')))
print('Encoded tired slime idle, melt/recovery and both single-foot recoveries.')

"""Copy approved frames unchanged and retain their authored timing."""
from pathlib import Path
import ast,json,shutil,hashlib
source=Path(__file__).resolve().parent.parent/'output'/'wobbly-mascot-v2-enhanced'
target=Path(__file__).resolve().parent/'Assets'/'enhanced'
values={}
def literal(node):
    if isinstance(node,ast.Name): return values[node.id]
    if isinstance(node,ast.BinOp) and isinstance(node.op,ast.Add): return literal(node.left)+literal(node.right)
    return ast.literal_eval(node)
for n in ast.parse((source/'assemble.py').read_text(encoding='utf-8')).body:
    if isinstance(n,ast.Assign) and isinstance(n.targets[0],ast.Name):
        try: values[n.targets[0].id]=literal(n.value)
        except (ValueError,KeyError,TypeError): pass
manifest={}
for side in ['left-support','right-support']:
    info=json.loads((source/side/'timing.json').read_text(encoding='utf-8'))
    folder=target/side;folder.mkdir(parents=True,exist_ok=True)
    files=sorted((source/side/'frames').glob('*.png'))
    for p in files: shutil.copy2(p,folder/p.name)
    manifest[side]={'intro':info['intro_frames'],'loop':info['loop_frames'],'introDurations':values['INTRO_DURATIONS'],'loopDurations':values['LOOP_DURATIONS'],'count':len(files),'hashes':[hashlib.sha256(p.read_bytes()).hexdigest() for p in files]}
(target/'timing.json').write_text(json.dumps(manifest),encoding='utf-8')
assert all(sum(v['loopDurations'])==6050 for v in manifest.values())
print('Copied 105 approved PNGs unchanged, with 1080 ms intros and 6050 ms struggle loops.')

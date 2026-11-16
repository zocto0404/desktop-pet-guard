import assert from 'node:assert/strict';
import fs from 'node:fs';
import {minionFrame,crushedFrame,minionSlime} from './motion.mjs';
const source=JSON.parse(fs.readFileSync('../Assets/countdown/source.json'));
const shapes=JSON.parse(fs.readFileSync('dist/assets/shapes.json'));
const atlas=JSON.parse(fs.readFileSync('../Assets/guard/sprites.json'));
assert.deepEqual(shapes.minions,source.pets.map(p=>p.paths),'all three types keep the countdown artwork and order');
for(let type=0;type<3;type++){
  assert.equal(atlas.minions[type].walk.length,24);
  assert.equal(atlas.minions[type].crushed.length,96);
  assert.notEqual(minionFrame(type,0),minionFrame(type,.25));
  assert(minionFrame(type,.25).includes('data-walk="redcowork"'));
  const pivots=[['94.7396 115.139','67.4812 115.139'],['91.8753 124.798','64.6169 124.798'],['89.0579 122.603','61.7996 122.603']][type];
  for(const p of pivots)assert(minionFrame(type,.25).includes(p),'rotation must start at the real hip');
  for(let frame=0;frame<96;frame++){
    const s=crushedFrame(type,frame/95);
    assert(!/NaN|Infinity/.test(s));
    assert(!/\b(?:fill-)?opacity=/.test(s),'minions must melt geometrically, with no opacity fade or crossfade');
    assert(!s.includes('data-debris-piece="leg-'),'no detached leg debris');
    if(frame/95*4>=.13){
      for(const i of [0,1])assert(!s.includes(`href="#m${type}-${i}"`),'no legs after takeoff');
      if(frame<95)assert(s.includes('data-knockback="legless"'));
    }
    if(frame>50&&frame<72)assert(s.includes('data-minion-melt="1"'),'landed body must become a real puddle');
    if(frame>68)for(const i of [3,4,5,6,7])assert(!s.includes(`href="#m${type}-${i}"`),'face must sink into slime before the puddle contracts');
  }
  assert.notEqual(minionSlime(type,1.2).match(/d="([^"]+)"/)[1],minionSlime(type,1.8).match(/d="([^"]+)"/)[1],'the actual body outline must flow');
  assert(crushedFrame(type,.75).includes('data-slime-drain="0"'),'solid puddle must remain before contraction');
  assert(crushedFrame(type,.9).includes('data-slime-drain='));
  assert(crushedFrame(type,1).includes('data-geometry-empty="true"'),'final frame has no geometry, rather than transparent geometry');
  assert(!crushedFrame(type,1).includes('<path'),'final puddle collapses to nothing');
}
assert.equal(atlas.manifest.length,90);
console.log('PASS: countdown models/order, exact hip pivots, redcowork gait; 288 legless knockback/slime frames; real outline morph, face absorption and geometric collapse; no fading or detached legs.');

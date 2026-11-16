import assert from 'node:assert/strict';
import fs from 'node:fs';
import {PetGame,PARTS} from '../dist/engine.js';
let seed=123456;const random=()=>{seed=(seed*1664525+1013904223)>>>0;return seed/2**32};
const events=[],g=new PetGame({random,onEvent:e=>events.push(e)});g.start();
function step(seconds){for(let i=0;i<Math.round(seconds*60);i++)g.tick(1/60)}
step(3.05);assert.equal(g.state,'playing');
const t=g.elapsed;g.togglePause(true);step(5);assert.equal(g.elapsed,t);g.togglePause(false);
g.move('left',-10000,-10000);assert.equal(g.feet.left.x,38);assert.equal(g.feet.left.y,312);
const first={id:200,type:0,x:200,y:400,job:-1,mode:'walk',side:1,phase:0,speed:80,dead:false};g.actors.push(first);g.aim('left',200,400);g.press('left');assert.equal(g.score.hits,0,'foot must fall before contact');step(.15);assert.equal(g.score.hits,1);const steps=g.score.steps;g.press('left');assert.equal(g.score.steps,steps,'holding is one stomp');g.release('left');step(.3);
const j=g.jobs[0];j.status='carry';j.members=[201];g.missing.add(0);g.actors.push({...first,id:201,dead:false,job:0,x:280});g.aim('left',280,400);g.press('left');step(.15);assert.equal(g.score.rescued,1);assert(!g.missing.has(0));g.release('left');
g.reset();g.start();step(3.05);step(59.9);assert.equal(g.state,'playing');step(.15);assert.equal(g.state,'ending');assert.equal(g.elapsed,60);const counts=g.score.spawned;assert(Math.max(...counts)-Math.min(...counts)<=1);step(6.6);assert.equal(g.state,'result');assert.equal(g.missing.size,0);assert.equal(g.actors.length,0);assert(g.cursor>=12,`idle play should expose staged detachment; reached ${g.cursor}`);const detached=events.filter(e=>e.type==='detached').map(e=>e.part);assert.equal(PARTS.length,19);console.log(JSON.stringify({result:'PASS',spawned:counts,partsDetachedInIdleGame:g.cursor,events:events.length}));
// A hat crew must wait for its second member; interrupting any carrier returns the whole part once.
g.reset();g.state='playing';g.cursor=1;g.spawnIn=999;const hat=g.jobs[1];hat.members=[1];const a={...first,id:1,job:1,dead:false,x:739,y:414};g.actors=[a];step(2);assert.equal(g.cursor,1);hat.members.push(2);g.actors.push({...a,id:2,speed:110});step(2);assert.equal(g.cursor,2);assert(g.missing.has(1));const crewOffset=g.actors[0].x-g.actors[1].x;step(1);assert(Math.abs(g.actors[0].x-g.actors[1].x-crewOffset)<.001,'carriers stay together');g.hit(a);assert(!g.missing.has(1));assert.equal(g.score.rescued,1);
const manifest=JSON.parse(fs.readFileSync('dist/assets/animation-manifest.json','utf8'));assert.equal(manifest.animations.length,90);for(const a of manifest.animations)assert(fs.existsSync('dist/'+a.path));const atlas=JSON.parse(fs.readFileSync('dist/assets/sprites.json','utf8'));assert(atlas.feet.down[0].y<-80&&atlas.feet.down.at(-1).y===0);assert(atlas.feet.down.length>=18&&atlas.feet.down[4].rx>=90,'stomp must keep its long exaggerated squash');assert(atlas.feet.up.at(-1).y<-60);assert.equal(atlas.dust,undefined,'pastel dust clouds must be absent');assert(!manifest.animations.some(a=>a.name==='stomp-dust'));assert(atlas.pet.idle[0][0]!==atlas.pet.idle[0][9]);console.log('PASS: group cooperation, interception, falling impact, pause, timing, restoration and recorded animation frames.');
g.reset();for(let i=0;i<500;i++){g.aim(i%2?'left':'right',i%3?1200:0,400);assert(g.feet.left.x+148<=g.feet.right.x,'feet cannot cross under mouse/hardware/keyboard movement');}
g.reset();g.start();step(3.05);step(10);assert(g.score.spawned.reduce((a,b)=>a+b,0)>=18,'early waves must be dense');
const {petFrame}=await import('./motion.mjs');for(const u of [0,.1,.3,.6,.85])assert(petFrame(0,'idle',u).startsWith('<g transform="translate(0 0) rotate(0 151 398)'),'intact pet stays upright');assert.equal(atlas.pet.idle[0].length,168);console.log('PASS: non-crossing feet, denser early waves and stable intact idle.');
g.reset();g.aim('left',1200,400);g.aim('right',0,400);assert.equal(g.feet.left.x,526);assert.equal(g.feet.right.x,674);assert(g.feet.left.x+72<600&&g.feet.right.x-72>600,'entire ellipse must stay on its side');
assert(PARTS[2].paths.includes(0)&&PARTS[3].paths.includes(1),'legs are pickups 3 and 4');g.reset();g.start();step(3.05);step(14);assert(g.collapsed,'second leg removal triggers collapse early');const {debrisPose}=await import('./motion.mjs');assert(debrisPose(0,1).x>950&&debrisPose(6,1).x< -950,'stomped minion bodies land farther in both directions');assert(Math.abs(debrisPose(0,2).x-debrisPose(0,1).x)>30,'landed minions keep rolling');const ground=debrisPose(0,.82).y;assert(debrisPose(0,1).y<ground-20,'body must rebound after first impact');assert(Math.abs(debrisPose(0,1.35).y-ground)<3,'body must contact the ground again');assert(debrisPose(0,1.45).y<ground-5,'body must make a smaller second bounce');assert(Math.abs(debrisPose(0,2).y-ground)<1,'body must eventually settle');console.log('PASS: midline bounds, early leg removal, farther body landing and diminishing rebounds.');
const {BODY_INDICES,SCATTER}=await import('../dist/engine.js');assert.equal(BODY_INDICES.length,4);assert(BODY_INDICES.every(i=>PARTS[i].people===1&&PARTS[i].style==='chunk'));let staged;const pickups=[];staged=new PetGame({random,onEvent:e=>{if(e.type==='detached'&&BODY_INDICES.includes(e.part))pickups.push({part:e.part,time:staged.elapsed})}});staged.start();for(let i=0;i<64*60;i++)staged.tick(1/60);assert.equal(pickups.length,4);for(let i=0;i<4;i++){assert(pickups[i].time>=PARTS[BODY_INDICES[i]].notBefore);if(i)assert(pickups[i].time-pickups[i-1].time>=5.95)}assert(SCATTER.slice(4).every(q=>Math.abs(q.x)<100),'tight central mound');assert(Math.abs(SCATTER[11].x-SCATTER[15].x)<50,'face remains together');const {crushedFrame}=await import('./motion.mjs');for(let type=0;type<3;type++){const frame=crushedFrame(type,.4);assert.equal((frame.match(new RegExp(`href="#m${type}-2"`,'g'))||[]).length,1);assert(!frame.includes('clipPath'),'minion body stays whole')}console.log('PASS: four staggered body pickups, compact mound/face and intact minion body.');
const {softBodyMarkup,liquidBodyMarkup,fallFrame,mouthMarkup,squintEyeMarkup}=await import('./motion.mjs');
assert.notEqual(softBodyMarkup(0,0).match(/d="([^"]+)"/)[1],softBodyMarkup(0,1).match(/d="([^"]+)"/)[1],'actual outline must morph');
assert.notEqual(liquidBodyMarkup(.38,'air'),liquidBodyMarkup(.57,'air'),'the whole airborne torso must lag and stretch');
assert.notEqual(liquidBodyMarkup(.2,'land'),liquidBodyMarkup(.5,'land'),'the single landed torso must keep recoiling');
assert(fallFrame(.24).includes('somersault'));
assert(fallFrame(.24).includes('data-liquid-body="air"'));
assert(fallFrame(.42).includes('data-liquid-body="mound"'));
assert(fallFrame(.42).includes('data-splash'));
assert.equal(atlas.pet.after[3].length,72);
assert(squintEyeMarkup(10).includes('data-eye-expression="squint"'));
assert(fallFrame(.2).includes('data-mouth-expression="small"'));
assert(fallFrame(.8).includes('data-mouth-expression="tinySmile"'));
const allowedMouths=new Set(['smile','flat','crooked','small','tight','tinySmile','wince','open','wide']);
let auditedPetFrames=0,squintFrames=0;
for(const clips of Object.values(atlas.pet))for(const clip of clips)for(const frame of clip){
 const matches=[...frame.matchAll(/<g data-mouth-expression="([^"]+)">([\s\S]*?)<\/g>/g)];
 assert.equal(matches.length,1,'each recorded pet frame must have one deliberate mouth pose');
 const [,name,markup]=matches[0];assert(allowedMouths.has(name),`unexpected mouth pose: ${name}`);
 assert(!markup.includes('fill="#FFF9EF"'),'mouth must never have white teeth');
 assert(!/<path[^>]*d="[^"]*Z"[^>]*fill="none"/.test(markup),'mouth must never be a hollow closed O');
 assert(!frame.includes('M132 186C130 168 157 166 158 185'),'old round O path must not occur');
 if(frame.includes('data-eye-expression="squint"'))squintFrames++;
 auditedPetFrames++;
}
assert(squintFrames>30,'squint must appear in recorded idle and reaction clips');
for(const frame of atlas.pet.after[3]){
 const [,name,markup]=frame.match(/<g data-mouth-expression="([^"]+)">([\s\S]*?)<\/g>/)||[];
 assert(['flat','small','crooked','tinySmile'].includes(name),`fall frame must use a simple curved line mouth, got ${name}`);
 assert(!markup.includes('fill="#282747"')&&!markup.includes('fill="#F47098"'),'fall mouth must never contain a dark opening or tongue that resembles teeth');
}
let auditedStompFrames=0;
for(let type=0;type<3;type++)for(const [index,f] of atlas.minions[type].crushed.entries()){assert.equal((f.match(/data-debris-group=/g)||[]).length,1);assert.equal((f.match(/data-leg-join=/g)||[]).length,2);for(let i=0;i<8;i++)assert.equal((f.match(new RegExp(`href="#m${type}-${i}"`,'g'))||[]).length,1,`part ${i} must appear once during stomp`);if(index>6){assert(f.includes('data-debris-group="split"'),'legs should separate shortly after launch');assert.equal((f.match(/data-debris-piece="leg-/g)||[]).length,2,'both legs become independent debris');assert(f.includes('data-debris-piece="body"'));assert(f.indexOf(`href="#m${type}-7"`)<f.indexOf('data-debris-piece="leg-0"'),'face stays on the body');}auditedStompFrames++}
for(let type=0;type<3;type++){const f=atlas.minions[type].crushed[24],x0=+f.match(/data-debris-piece="leg-0" transform="translate\(([-\d.]+)/)[1],x1=+f.match(/data-debris-piece="leg-1" transform="translate\(([-\d.]+)/)[1];assert(x0>=220&&x1<=-220,'both legs must land farther from the impact point')}
const legPose=(i,t)=>{const f=crushedFrame(0,(t+.13)/4),m=f.match(new RegExp(`data-debris-piece="leg-${i}" transform="translate\\(([-\\d.]+) ([-\\d.]+)\\) rotate\\(([-\\d.]+)`));return{x:+m[1],y:+m[2],angle:+m[3]}};
assert(legPose(0,.9).y>legPose(0,.97).y,'first leg must rebound after landing');
assert(legPose(1,.78).y<legPose(0,.78).y,'legs must have different flight and bounce timing');
assert(legPose(0,.9).angle>legPose(0,.97).angle,'first leg must reverse its spin at impact');
assert(legPose(1,1.05).angle<legPose(1,1.12).angle,'second leg must reverse its spin at a different impact');
assert.equal(legPose(0,2.5).angle,legPose(0,3).angle,'first leg must stop rolling');
console.log(`PASS: audited ${auditedPetFrames} pet frames: zero teeth, zero hollow O mouths, ${squintFrames} squint frames; ${auditedStompFrames} stomp frames split off two legs while keeping the face on the body.`);
assert.equal(softBodyMarkup(0,0),softBodyMarkup(0,Math.PI*2),'soft body loop must join without a shape jump');

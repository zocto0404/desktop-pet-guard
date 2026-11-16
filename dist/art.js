import {PARTS,PET,SCATTER,clamp,partGeometry,BODY_INDICES} from './engine.js';
let atlas;export function setAtlas(a){atlas=a}
const fixed=n=>Math.round(n*100)/100;
const cycle=(frames,t)=>frames[((Math.floor(t*atlas.fps)%frames.length)+frames.length)%frames.length];
const once=(frames,t)=>frames[clamp(Math.floor(t*atlas.fps),0,frames.length-1)];
export function partAt(i,x,y,scale=PET.scale,angle=0,shape){const b=PARTS[i].box;return `<g transform="translate(${fixed(x)} ${fixed(y)}) rotate(${fixed(angle)}) scale(${scale}) translate(${-b[0]-b[2]/2} ${-b[1]-b[3]/2})">${shape||partGeometry(i)}</g>`}
export function petMarkup(game){
 const j=game.jobs[game.cursor],r=game.reaction;let stage=game.cursor,frame;
 if(game.missing.size===0&&j?.status!=='pull')stage=0;
 if(game.state==='ending'&&game.endRestore)frame=cycle(atlas.pet.idle[0],game.clock);
 else if(!game.collapsed&&stage>=4)frame=cycle(atlas.pet.idle[3],game.clock);
 else if(r.kind==='detach'&&r.age<atlas.pet.after[r.part].length/atlas.fps)frame=once(atlas.pet.after[r.part],r.age);
 else if(j?.status==='pull'){const phase=j.pull<.65?'prepare':'pull',t=j.pull<.65?j.pull/.65:j.pull/1-.65;frame=once(atlas.pet[phase][j.part],t/.99*(20/24));}
 else frame=cycle(atlas.pet.idle[Math.min(PARTS.length,stage)],game.clock);
 const hidden=new Set([...game.missing,...game.returns.filter(r=>r.age<r.duration*.98).map(r=>r.part)]);
 let mask=[...hidden].map(i=>`#pet .p${i}{display:none}`).join('');
 mask+=BODY_INDICES.some(i=>hidden.has(i))?'#pet .body-whole{display:none}':'#pet .assembled-body-pieces{display:none}';
 let transform='';if(game.state==='countdown'){const u=game.countdown%1;transform=`translate(0 ${Math.sin(u*Math.PI)*13}) scale(1 ${1-Math.sin(u*Math.PI)*.035})`}
 if(game.state==='result'){transform=`rotate(${Math.sin(Math.min(1,r.age/1.2)*Math.PI)*-10} 240 480)`}
 return `<style>${mask}</style><g id="pet" transform="translate(${PET.x-(206+75)*PET.scale} ${PET.y-(398+115)*PET.scale}) scale(${PET.scale})"><g transform="${transform}">${frame}</g></g>`;
}
function actor(a,g){if(a.dead)return '';const j=g.jobs[a.job],p=j&&PARTS[j.part],i=j?.members.indexOf(a.id);let y=a.y,x=a.x;
 if(p?.style==='stack'&&i===1&&a.mode!=='walk')y-=46;
 if(j?.status==='pull'){const q=j.pull/1.65;x+=Math.sin(q*Math.PI*4)*4;if([1,5,9,10,13,14,15].includes(j.part))y-=Math.sin(q*Math.PI)*18;}
 let frame=cycle(atlas.minions[a.type][a.mode==='carry'?'carry':'walk'],g.clock+(a.phase||0));return `<g transform="translate(${fixed(x)} ${fixed(y)}) scale(.35) translate(-115 -225)">${frame}</g>`;
}
function cargo(j,g){const members=j.members.map(id=>g.actors.find(a=>a.id===id)).filter(a=>a&&!a.dead);if(!members.length)return '';const p=PARTS[j.part],a=members[0],avg=members.reduce((r,a)=>({x:r.x+a.x/members.length,y:r.y+a.y/members.length}),{x:0,y:0});let y=avg.y-60,scale=.52,rotation=0,x=avg.x;
 if(p.style==='stack'){y-=48;scale=.47}if(p.style==='chest'){y+=25;scale=.7}if(p.style==='wide'){y-=7;scale=.52;rotation=80}if(p.style==='body'){y-=48;scale=.5;rotation=80}if(p.style==='drag'){y+=38;x-=a.side*33;rotation=82;scale=.57}
 const frame=g.clock*24%24;const lift=atlas.lift[Math.floor(frame)];return partAt(j.part,x,y+lift.y,scale,rotation+lift.angle,p.style==='chunk'?cycle(atlas.bodyCarry[p.chunk],g.clock):undefined);
}
export function footPose(foot,t){if(foot.age<.75||foot.down)return atlas.feet.down[Math.min(atlas.feet.down.length-1,Math.floor(foot.age*24))];const rise=foot.age-Math.max(.75,foot.releaseAt||0);if(rise<.5)return atlas.feet.up[Math.min(atlas.feet.up.length-1,Math.floor(rise*24))];return atlas.feet.hover[Math.floor(t*24)%atlas.feet.hover.length]}
function foot(name,f,t){const p=footPose(f,t);return `<ellipse data-foot="${name}" cx="${fixed(f.x)}" cy="${fixed(f.y+p.y)}" rx="${p.rx}" ry="${p.ry}" fill="${name==='left'?'#7c7dde':'#eab07c'}" fill-opacity=".5" stroke="none"/>`}
function impact(r){if(r.age>=.48)return '';const t=r.age/.48,points=Array.from({length:32},(_,i)=>{const a=i/32*Math.PI*2;return`${fixed(r.x+Math.cos(a)*(i%2?68:103)*(1+t*.8))},${fixed(r.y+Math.sin(a)*(i%2?22:39)*(1+t*.8))}`}).join(' ');return `<polygon points="${points}" fill="none" stroke="${r.color}" stroke-width="2.8" opacity="${fixed((1-t)**2)}"/>`}
export function renderScene(g){let out=`<defs>${atlas.defs}</defs>`;const back=g.actors.filter(a=>a.y<425),front=g.actors.filter(a=>a.y>=425);for(const a of back)out+=actor(a,g);out+=petMarkup(g);for(const j of g.jobs)if(j.status==='carry')out+=cargo(j,g);for(const a of front)out+=actor(a,g);
 for(const d of g.debris){out+=`<g transform="translate(${d.x} ${d.y}) scale(${d.direction<0?'-.35':'.35'} .35) translate(-1300 -1200)">${once(atlas.minions[d.type].crushed,d.age)}</g>`;}
 for(const r of g.returns){const b=PARTS[r.part].box,q=g.collapsed?SCATTER[r.part]:null,t=1-(1-clamp(r.age/r.duration,0,1))**3,tx=q?PET.x+q.x:PET.x+(b[0]+b[2]/2-206)*PET.scale,ty=q?PET.y+q.y:PET.y+(b[1]+b[3]/2-398)*PET.scale;out+=partAt(r.part,r.x+(tx-r.x)*t,r.y+(ty-r.y)*t-Math.sin(t*Math.PI)*120,.35+t*(PET.scale-.35),(1-t)*230+(q?.angle||0)*t);}
 out+=foot('left',g.feet.left,g.clock)+foot('right',g.feet.right,g.clock);for(const r of g.rings)out+=impact(r);
 if(g.state==='result'){const age=g.reaction.age;if(age<2.5)for(let i=0;i<3;i++)out+=`<path d="M0 4C-16-9-23 8 0 24C23 8 16-9 0 4" transform="translate(${PET.x-65+i*50} ${PET.y-270-age*28-i*10}) scale(.42)" fill="#df6596" opacity="${Math.max(0,1-age/2.5)}"/>`;}
 return out;
}

import {PetGame,clamp,PARTS} from './engine.js';
import {setAtlas,renderScene} from './art.js';
const stage=document.getElementById('stage');let game,last=performance.now(),resultAt=0,selected='left',pointerFoot,drag;
const held=new Set(),directions={KeyW:['left',0,-1],KeyA:['left',-1,0],KeyS:['left',0,1],KeyD:['left',1,0],KeyI:['right',0,-1],KeyJ:['right',-1,0],KeyK:['right',0,1],KeyL:['right',1,0]},stomps={KeyF:'left',KeyH:'right'};
const pedals={left:{x:0,y:0,down:false},right:{x:0,y:0,down:false}};
function begin(){if(!game)return;held.clear();game.start();resultAt=0}
function wake(){if(['idle','result'].includes(game.state))begin()}
function clear(){held.clear();game?.releaseAll();pointerFoot=null}
function point(e){const r=stage.getBoundingClientRect(),s=Math.min(r.width/1200,r.height/600);return{x:(e.clientX-r.x-(r.width-1200*s)/2)/s,y:(e.clientY-r.y-(r.height-600*s)/2)/s}}
function overPet(p){return p.x>425&&p.x<780&&p.y>100&&p.y<460}
function frame(now){const dt=clamp((now-last)/1000,0,.05);last=now;if(game){const v={left:[0,0],right:[0,0]};for(const code of held){const d=directions[code];if(d){v[d[0]][0]+=d[1];v[d[0]][1]+=d[2]}}for(const foot of ['left','right']){const [x,y]=v[foot],n=Math.hypot(x,y)||1;game.move(foot,(x/n+pedals[foot].x)*290,(y/n+pedals[foot].y)*190,dt)}game.tick(dt);if(game.state==='result'){resultAt+=dt;if(resultAt>3.2){game.reset();resultAt=0}}stage.innerHTML=renderScene(game);}requestAnimationFrame(frame)}
document.addEventListener('keydown',e=>{if(!game)return;if(e.code==='Escape'){game.togglePause();return}if(e.code==='Enter'){e.preventDefault();if(!e.repeat)begin();return}if(directions[e.code]){e.preventDefault();held.add(e.code)}if(stomps[e.code]){e.preventDefault();if(!e.repeat){wake();game.press(stomps[e.code])}}});document.addEventListener('keyup',e=>{held.delete(e.code);if(stomps[e.code])game?.release(stomps[e.code])});
stage.addEventListener('contextmenu',e=>{e.preventDefault();if(window.desktopPet&&overPet(point(e)))window.desktopPet.menu()});
stage.addEventListener('pointermove',e=>{if(!game)return;if(drag&&window.desktopPet){window.desktopPet.move(e.screenX-drag.x,e.screenY-drag.y);drag={x:e.screenX,y:e.screenY};return}const p=point(e);game.aim(pointerFoot||selected,p.x,p.y);if(window.desktopPet)window.desktopPet.hitTest(overPet(p)||p.y>260&&p.y<530)});
stage.addEventListener('pointerdown',e=>{if(!game)return;const p=point(e);if(window.desktopPet&&e.button===2&&overPet(p))return;if(e.shiftKey&&window.desktopPet){drag={x:e.screenX,y:e.screenY};stage.setPointerCapture(e.pointerId);return}e.preventDefault();wake();pointerFoot=e.button===2?'right':e.pointerType==='touch'?selected:'left';selected=pointerFoot;game.aim(pointerFoot,p.x,p.y);game.press(pointerFoot);stage.setPointerCapture(e.pointerId)});
window.addEventListener('pointerup',()=>{if(pointerFoot)game.release(pointerFoot);pointerFoot=null;drag=null});window.addEventListener('pointercancel',clear);
document.addEventListener('visibilitychange',()=>{if(document.hidden){clear();if(!window.desktopPet&&game&&!game.paused&&['playing','countdown','ending'].includes(game.state))game.togglePause(true)}});window.addEventListener('blur',clear);
try{const r=await fetch('assets/sprites.json');if(!r.ok)throw Error('Unable to load animation frames');setAtlas(await r.json());game=new PetGame({onEvent:e=>{if(e.type==='result')resultAt=0;window.dispatchEvent(new CustomEvent('deskpet:state',{detail:e}))}});game.feet.left.x=400;game.feet.right.x=800;
 window.deskPet={version:'2.6.2',start:begin,pause:()=>game.togglePause(true),resume:()=>game.togglePause(false),reset:()=>game.reset(),getState:()=>({state:game.state,paused:game.paused,elapsed:game.elapsed,integrity:Math.round((PARTS.length-game.missing.size)/PARTS.length*100),score:structuredClone(game.score)}),setPedal(foot,{tiltX=0,tiltY=0,pressure=0}={}){if(!pedals[foot]||![tiltX,tiltY,pressure].every(Number.isFinite))return;const p=pedals[foot];p.x=clamp(tiltX,-1,1);p.y=clamp(tiltY,-1,1);const down=pressure>=(p.down?.4:.6);if(down&&!p.down){wake();game.press(foot)}if(!down&&p.down)game.release(foot);p.down=down;}};
 window.addEventListener('deskpet:pedal',e=>{if(e.detail)window.deskPet.setPedal(e.detail.foot,e.detail)});
 window.desktopPet?.onCommand(command=>{if(command==='start')begin();if(command==='pause')game.togglePause();if(command==='reset')game.reset()});
 if(new URLSearchParams(location.search).get('qa')==='1')window.__petQA={game,begin,render:()=>stage.innerHTML=renderScene(game)};
 if(window.desktopPet)window.desktopPet.ready();requestAnimationFrame(frame);
}catch(e){console.error(e)}

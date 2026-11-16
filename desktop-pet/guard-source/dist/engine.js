// Input-independent simulation. Coordinates are in a 1200 × 530 ground plane.
export const PARTS = [
 {name:'杯子',paths:[15,16,17,18],people:1,box:[335,207,80,75],style:'overhead'},
 {name:'贝雷帽与帽沿',paths:[5,6],people:2,box:[130,0,178,110],style:'stack'},
 {name:'蝴蝶结',paths:[19],people:1,box:[111,208,57,35],style:'chest'},
 {name:'左侧外套片',paths:[4],people:2,box:[48,173,70,140],style:'wide'},
 {name:'右侧外套片',paths:[3],people:2,box:[166,173,120,141],style:'wide'},
 {name:'白色衣领',paths:[7],people:1,box:[88,195,103,34],style:'overhead'},
 {name:'右侧手臂',paths:[8],people:1,box:[270,201,69,55],style:'overhead'},
 {name:'左侧腿脚',paths:[0],people:1,box:[94,281,29,117],style:'drag'},
 {name:'左侧手臂',paths:[9],people:1,box:[0,201,61,55],style:'overhead'},
 {name:'右侧蓝色眼珠',paths:[13],people:1,box:[130,127,36,36],style:'overhead'},
 {name:'右侧白色眼球',paths:[12],people:1,box:[126,108,69,71],style:'overhead'},
 {name:'右侧腿脚',paths:[1],people:1,box:[164,281,29,117],style:'drag'},
 {name:'粉色主体',paths:[2],people:4,box:[45,39,242,267],style:'body'},
 {name:'左侧蓝色眼珠',paths:[11],people:1,box:[92,128,35,34],style:'overhead'},
 {name:'左侧白色眼球',paths:[10],people:1,box:[74,108,70,71],style:'overhead'},
 {name:'嘴巴',paths:[14],people:1,box:[127,177,33,21],style:'overhead'},
];
// Updated pickup order: both legs are removed immediately after cup and hat.
export const LEGACY_ORDER=[0,1,7,11,2,3,4,5,6,8,9,10,12,13,14,15];
const originalParts=PARTS.slice();PARTS.splice(0,16,...LEGACY_ORDER.map(i=>originalParts[i]));
export const BODY_CHUNKS=[
 {box:[44,38,128,140],clip:'M40 30H166L159 116L170 167L40 180Z'},
 {box:[159,38,129,142],clip:'M166 30H295V180L170 167L159 116Z'},
 {box:[45,167,129,140],clip:'M40 180L170 167L160 235L174 315H40Z'},
 {box:[160,167,128,140],clip:'M170 167L295 180V315H174L160 235Z'}
];
const base=PARTS.slice();
const chunk=k=>({...base[12],name:`粉色身体块${k+1}`,box:BODY_CHUNKS[k].box,people:1,style:'chunk',chunk:k,symbol:`pet-chunk-${k}`,notBefore:[28,35,42,48][k]});
PARTS.splice(0,16,...[...base.slice(0,9),chunk(0),base[9],base[10],chunk(1),base[11],chunk(2),base[13],chunk(3),base[14],base[15]]);
export const BODY_INDICES=[9,12,14,16];
export const SCATTER=[{x:-70,y:7,angle:12},{x:42,y:-25,angle:-12},{x:-38,y:35,angle:82},{x:38,y:38,angle:-80},{x:-30,y:23,angle:-12},{x:-89,y:4,angle:39,scale:.7},{x:82,y:10,angle:-42,scale:.7},{x:30,y:26,angle:9},{x:69,y:36,angle:-18},{x:-30,y:-26,angle:-12,scale:.87,flatten:.7},{x:-65,y:35,angle:15},{x:21,y:4,angle:0,scale:.86},{x:30,y:-30,angle:12,scale:.87,flatten:.7},{x:21,y:4,angle:0,scale:.86},{x:-43,y:10,angle:-7,scale:.87,flatten:.7},{x:-21,y:4,angle:0,scale:.86},{x:43,y:10,angle:8,scale:.87,flatten:.7},{x:-21,y:4,angle:0,scale:.86},{x:0,y:27,angle:0}];
export const partGeometry=i=>PARTS[i].symbol?`<use href="#${PARTS[i].symbol}"/>`:PARTS[i].paths.map(n=>`<use href="#pet-${n}"/>`).join('');
export const PET={x:600,y:440,scale:.72};
export const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
export class PetGame {
 constructor({random=Math.random,onEvent=()=>{}}={}){this.random=random;this.onEvent=onEvent;this.reset()}
 emit(type,data={}){this.onEvent({type,...data})}
 reset(){this.state='idle';this.paused=false;this.elapsed=0;this.clock=0;this.countdown=3;this.endTime=0;this.cursor=0;this.collapsed=false;this.missing=new Set();this.actors=[];this.jobs=PARTS.map((p,i)=>({part:i,members:[],status:'waiting',pull:0}));this.debris=[];this.rings=[];this.returns=[];this.score={hits:0,steps:0,rescued:0,escaped:0,spawned:[0,0,0]};this.spawnIn=.12;this.nextActor=0;this.reaction={kind:'idle',age:99};this.feet={left:{x:400,y:450,down:false,age:9,cooldown:0},right:{x:800,y:450,down:false,age:9,cooldown:0}};this.finalIntegrity=100;this.endRestore=false;this.emit('reset')}
 start(){this.reset();this.state='countdown';this.emit('countdown',{value:3})}
 togglePause(force){if(!['playing','countdown','ending'].includes(this.state))return;this.paused=force??!this.paused;this.releaseAll();this.emit('pause',{paused:this.paused})}
 releaseAll(){for(const f of Object.values(this.feet)){f.down=false;f.releaseAt=f.age;f.pending=false}}
 move(foot,dx,dy,dt=1){const f=this.feet[foot];if(!f||this.paused)return;const low=foot==='right'?PET.x+74:38;const high=foot==='left'?PET.x-74:1162;f.x=clamp(f.x+dx*dt,low,high);f.y=clamp(f.y+dy*dt,312,500)}
 aim(foot,x,y){if(!this.feet[foot])return;this.move(foot,x-this.feet[foot].x,y-this.feet[foot].y)}
 press(foot){const f=this.feet[foot];if(!f||f.down||this.paused||f.cooldown>0)return;f.down=true;f.age=0;f.releaseAt=0;f.cooldown=.25;f.pending=true;this.emit('step',{foot});if(this.state==='playing')this.score.steps++}
 impact(foot){const f=this.feet[foot];f.pending=false;this.rings.push({x:f.x,y:f.y,age:0,color:foot==='left'?'#7778df':'#e8a363'});if(this.state!=='playing')return;const victims=this.actors.filter(a=>!a.dead&&Math.hypot((a.x-f.x)/1.2,a.y-f.y)<63);for(const a of victims)this.hit(a);if(victims.length)this.emit('hit',{count:victims.length,total:this.score.hits})}
 release(foot){if(this.feet[foot]){this.feet[foot].down=false;this.feet[foot].releaseAt=this.feet[foot].age}}
 hit(a){if(a.dead)return;a.dead=true;a.deathAge=0;this.score.hits++;this.debris.push({type:a.type,x:a.x,y:a.y,age:0,direction:a.x<PET.x?-1:1,seed:a.id*1.7});const j=this.jobs[a.job];if(!j)return;if(j.status==='carry'){this.rescue(j,a.x,a.y)}else{j.members=j.members.filter(id=>id!==a.id);j.pull=0;j.status='waiting';}}
 rescue(j,x,y){if(j.status!=='carry')return;this.missing.delete(j.part);j.status='recovered';this.score.rescued++;this.returns.push({part:j.part,x,y:y-45,age:0,duration:1});for(const id of j.members){const a=this.actors.find(a=>a.id===id);if(a&&!a.dead){a.job=-1;a.mode='retreat';a.side=a.x<PET.x?-1:1}}this.reaction={kind:'happy',age:0};this.emit('rescued',{part:j.part,name:PARTS[j.part].name})}
 spawn(){let j=this.jobs.find(j=>j.part>=this.cursor&&j.status==='waiting'&&j.members.length<PARTS[j.part].people);const side=this.random()<.5?-1:1;const type=this.nextActor%3;const a={id:this.nextActor++,type,side,x:side<0?-38:1238,y:385+this.random()*90,speed:110+this.random()*30,phase:this.random()*Math.PI*2,job:j?.part??-1,mode:'walk',dead:false,deathAge:0};if(j)j.members.push(a.id);this.actors.push(a);this.score.spawned[type]++;}
 target(a){const j=this.jobs[a.job];if(!j)return{x:PET.x+(a.side*135),y:434};const p=PARTS[j.part],i=j.members.indexOf(a.id),b=p.box;if(a.job>this.cursor+1)return{x:PET.x+a.side*(160+a.id%4*45),y:475+a.id%3*10};if(this.collapsed&&SCATTER[j.part]){const q=SCATTER[j.part];return{x:PET.x+q.x+(i-(p.people-1)/2)*42,y:PET.y+q.y+10+(i%2)*10}}let x=PET.x+(b[0]+b[2]/2-206)*PET.scale;let y=421;if(p.style==='wide'){x=PET.x+(j.part===5?-75:63)+(i-.5)*58;y=414+i*16}else if(p.style==='stack'){x=PET.x+9;y=414}else if(p.style==='body'){x=PET.x-38+(i%2)*65;y=397+Math.floor(i/2)*29}else{x=clamp(x,PET.x-115,PET.x+100);y=414+(a.id%3)*12}return{x,y}}
 detach(j){j.status='carry';j.pull=0;this.missing.add(j.part);if(this.missing.has(2)&&this.missing.has(3))this.collapsed=true;this.cursor++;this.reaction={kind:'detach',age:0,part:j.part};let members=j.members.map(id=>this.actors.find(a=>a.id===id)).filter(Boolean);let side=members[0]?.side??1;const crewSpeed=Math.min(...members.map(a=>a.speed));for(const a of members){a.mode='carry';a.side=side;a.speed=crewSpeed;}this.emit('detached',{part:j.part,name:PARTS[j.part].name,stage:this.cursor});}
 tick(dt){dt=clamp(dt,0,.05);if(this.paused)return;this.clock+=dt;this.reaction.age+=dt;for(const [name,f] of Object.entries(this.feet)){f.age+=dt;f.cooldown=Math.max(0,f.cooldown-dt);if(f.pending&&f.age>=.125)this.impact(name)}for(const list of [this.rings,this.debris,this.returns])for(const p of list)p.age+=dt;this.rings=this.rings.filter(p=>p.age<.48);this.debris=this.debris.filter(p=>p.age<4);this.returns=this.returns.filter(p=>p.age<p.duration);if(this.state==='idle'||this.state==='result')return;
 if(this.state==='countdown'){let before=Math.ceil(this.countdown);this.countdown-=dt;if(this.countdown<=0){this.state='playing';this.emit('go')}else if(before!==Math.ceil(this.countdown))this.emit('countdown',{value:Math.ceil(this.countdown)});return;}
 if(this.state==='playing'){this.elapsed=Math.min(60,this.elapsed+dt);if(this.elapsed>=60){this.state='ending';this.finalIntegrity=Math.round((PARTS.length-this.missing.size)/PARTS.length*100);for(const a of this.actors){if(!a.dead){a.mode='retreat';a.side=a.x<PET.x?-1:1}}this.emit('ending');return;}
 this.spawnIn-=dt;const fade=clamp((56-this.elapsed)/8,0,1);if(this.elapsed<56&&this.spawnIn<=0&&this.actors.filter(a=>!a.dead).length<48){this.spawn();this.spawnIn=(.38+this.random()*.18)/Math.max(.13,fade)}
 for(const a of this.actors){if(a.dead){a.deathAge+=dt;continue}if(a.mode==='carry'||a.mode==='retreat'){a.x+=a.side*a.speed*(a.mode==='retreat'?1.9:.72)*dt;continue}const target=this.target(a),dx=target.x-a.x,dy=target.y-a.y,dist=Math.hypot(dx,dy);if(dist>2){a.x+=dx/dist*Math.min(dist,a.speed*dt);a.y+=dy/dist*Math.min(dist,a.speed*dt);a.mode='walk'}else{a.mode='wait';if(a.job<0){a.mode='retreat'}}}
 const j=this.jobs[this.cursor];if(j){const p=PARTS[j.part];const members=j.members.map(id=>this.actors.find(a=>a.id===id)).filter(a=>a&&!a.dead);const ready=members.length===p.people&&members.every(a=>Math.hypot(a.x-this.target(a).x,a.y-this.target(a).y)<5);if(ready&&this.elapsed>=(p.notBefore||0)&&!(this.reaction.kind==='detach'&&this.reaction.part===3&&this.reaction.age<3)){j.status='pull';j.pull+=dt;if(j.pull>1.65)this.detach(j)}}
 for(const j of this.jobs){if(j.status!=='carry')continue;const members=j.members.map(id=>this.actors.find(a=>a.id===id)).filter(a=>a&&!a.dead);if(members.length&&members.every(a=>a.x<-85||a.x>1285)){j.status='escaped';this.score.escaped++;}}
 }else if(this.state==='ending'){this.endTime+=dt;for(const a of this.actors){if(!a.dead)a.x+=a.side*a.speed*2*dt;else a.deathAge+=dt}if(this.endTime>2&&!this.endRestore){this.endRestore=true;this.collapsed=false;this.endParts=[...this.missing].reverse();this.restoreIn=0;}if(this.endRestore){this.restoreIn-=dt;if(this.restoreIn<=0&&this.endParts.length){const part=this.endParts.shift();this.missing.delete(part);this.returns.push({part,x:PARTS[part].style==='body'?-140:1300,y:260+this.random()*160,age:0,duration:1.3});this.restoreIn=.13;this.emit('reassemble',{part})}}if(this.endTime>6.5){this.state='result';this.missing.clear();this.actors=[];this.reaction={kind:'happy',age:0};this.emit('result',{score:{...this.score},integrity:this.finalIntegrity})}}
 this.actors=this.actors.filter(a=>(!a.dead||a.deathAge<.14)&&a.x>-160&&a.x<1360);
 }
}

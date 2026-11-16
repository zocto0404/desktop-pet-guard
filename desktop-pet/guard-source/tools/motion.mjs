// Authoring only: record full vector poses at 24 fps. The player selects frames.
import {PARTS,PET,SCATTER,LEGACY_ORDER,BODY_CHUNKS,BODY_INDICES,partGeometry} from '../dist/engine.js';
import fs from 'node:fs';
export const FPS=24;
const rnd=n=>Math.round(n*100)/100;
const sn=(p,n=1)=>Math.sin(p*Math.PI*2*n),cs=(p,n=1)=>Math.cos(p*Math.PI*2*n);
const rotate=(a,x,y)=>`rotate(${rnd(a)} ${x} ${y})`;
const translate=(x,y)=>`translate(${rnd(x)} ${rnd(y)})`;
const scaleAt=(x,y,cx,cy)=>`translate(${cx} ${cy}) scale(${rnd(x)} ${rnd(y)}) translate(${-cx} ${-cy})`;
export const definitions=shapes=>shapes.pet.map((p,i)=>`<g id="pet-${i}">${p}</g>`).join('')+BODY_CHUNKS.map((c,k)=>`<g id="pet-chunk-${k}">${softBodyMarkup(k,0)}</g>`).join('')+shapes.minions.map((p,t)=>p.map((v,i)=>`<g id="m${t}-${i}">${v}</g>`).join('')).join('');
export function softBodyMarkup(k,t,pulse=0,pull=0){const b=BODY_CHUNKS[k].box,cx=b[0]+b[2]/2,cy=b[1]+b[3]/2,points=[];
 for(let i=0;i<8;i++){const a=i/8*Math.PI*2,wiggle=1+Math.sin(t*2+a*3+k)*.095+Math.cos(t-a*2)*.04;points.push([cx+Math.cos(a)*b[2]*.49*wiggle*(1+pulse*.23)+Math.sin(a*2+t)*5,cy+Math.sin(a)*b[3]*.47*wiggle*(1-pulse*.3)-Math.max(0,-Math.sin(a))*pull*32]);}
 let d=`M${points[0].map(rnd).join(' ')}`;for(let i=0;i<8;i++){const a=points[(i+7)%8],b=points[i],c=points[(i+1)%8],e=points[(i+2)%8];d+=`C${[b[0]+(c[0]-a[0])/6,b[1]+(c[1]-a[1])/6,c[0]-(e[0]-b[0])/6,c[1]-(e[1]-b[1])/6,c[0],c[1]].map(rnd).join(' ')}`;}
 return `<path data-soft-body="${k}" d="${d}Z" fill="${['#df588d','#e36b9a','#ce4b81','#e06598'][k]}"/>`;
}
// The torso stays one continuous surface through the fall. Its upper edge follows
// the center a fraction of a beat later, like the liquid motion in reference 2.
export function liquidBodyMarkup(t,mode='air'){
 if(mode==='air'){
  const p=Math.max(0,Math.min(1,t/1.05)),whip=Math.sin(Math.PI*p)*Math.sin(Math.PI*p),lag=whip*(24+10*Math.sin(p*Math.PI*2-.8)),tail=whip*20;
  return `<path data-liquid-body="air" d="M${rnd(142-lag*.35)} ${rnd(41-lag*.55)}C${rnd(106-lag*.4)} ${rnd(37-lag*.9)} ${rnd(63-lag*.3)} 68 ${rnd(50-lag*.24)} 120C39 166 60 193 56 231C${rnd(51-tail*.4)} 267 65 283 111 296C152 309 225 286 262 263C271 229 ${rnd(292+tail*.25)} 183 280 125C${rnd(270+lag*.6)} 68 ${rnd(216+lag)} ${rnd(38-lag*.4)} ${rnd(142-lag*.35)} ${rnd(41-lag*.55)}Z" fill="#DE588B"/>`;
 }
 const impact=Math.max(0,t),recoil=Math.exp(-impact*3.2)*Math.cos(impact*14),delayed=impact<.11?0:Math.exp(-(impact-.11)*2.6)*Math.sin((impact-.11)*12),idle=mode==='land'?0:Math.sin(t*Math.PI*2)*1.5;
 const left=105-19*recoil-8*delayed,right=307+19*recoil-12*delayed,top=314+22*recoil-18*delayed+idle,mid=335+13*recoil-8*delayed,crest=Math.max(0,delayed)*28;
 return `<path data-liquid-body="mound" d="M${rnd(left)} 445C${rnd(left-19)} 420 ${rnd(left-7)} ${rnd(mid+23)} ${rnd(left+20)} ${rnd(mid+9)}C${rnd(left+33)} ${rnd(top+5)} ${rnd(163-8*delayed)} ${rnd(top-9)} ${rnd(176-8*delayed)} ${rnd(top+3)}C${rnd(182-8*delayed)} ${rnd(top-7-crest)} ${rnd(191-6*delayed)} ${rnd(top-15-crest)} ${rnd(200-5*delayed)} ${rnd(top-2)}C${rnd(208-4*delayed)} ${rnd(top+7)} ${rnd(216-3*delayed)} ${rnd(top-11-crest*.7)} ${rnd(226-3*delayed)} ${rnd(top-6-crest*.7)}C${rnd(248-9*delayed)} ${rnd(top-13)} ${rnd(right-16)} ${rnd(mid-8)} ${rnd(right-8)} ${rnd(mid+17)}C${rnd(right+13)} 390 ${rnd(right+15)} 427 ${rnd(right)} 445C270 ${rnd(450+3*recoil)} 143 ${rnd(450+3*recoil)} ${rnd(left)} 445Z" fill="#DE588B"/>`;
}
// Mouths stay curved or asymmetric. Open poses are solid crescents, never
// toothy or hollow rings.
export function mouthMarkup(kind='smile'){
 const line=d=>`<path d="${d}" fill="none" stroke="#FFF9EF" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/>`;
 const open=`<path d="M124 183C133 177 155 178 165 183C164 193 155 201 144 200C133 199 127 193 124 183Z" fill="#282747"/><path d="M140 198C147 193 155 194 160 197C153 202 146 203 140 198Z" fill="#F47098"/>`;
 const wide=`<path d="M121 181C134 173 158 176 168 182C166 197 156 207 143 205C130 203 123 193 121 181Z" fill="#282747"/><path d="M138 202C146 195 158 195 164 200C156 206 146 208 138 202Z" fill="#F47098"/>`;
 const shapes={smile:line('M132 189Q142 197 155 184'),flat:line('M132 188L155 186'),crooked:line('M128 187Q137 198 148 188L159 183'),small:line('M140 188Q147 184 153 187'),tight:line('M129 189L136 185L143 190L151 184L160 186'),tinySmile:line('M136 188Q145 195 155 186'),wince:line('M129 189Q137 180 144 188Q152 194 160 184'),open,wide};
 return `<g data-mouth-expression="${kind}">${shapes[kind]||shapes.smile}</g>`;
}
export function squintEyeMarkup(n){
 const left=n===10||n===11,white=n===10||n===12,x=left?78:146,w=left?58:52,c=left?107:168;
 const d=white?`M${x} 137L${x+w-5} 137Q${x+w} 137 ${x+w} 143L${x+w} 150Q${x+w-1} 158 ${x+w-9} 158L${x+8} 158Q${x} 157 ${x} 150Z`:`M${c-13} 137L${c+13} 137L${c+13} 146Q${c+12} 157 ${c} 158Q${c-12} 157 ${c-13} 146Z`;
 return `<g data-eye-expression="squint"><path d="${d}" fill="${white?'#FFF9EF':'#2D83C5'}"/></g>`;
}
export function petFrame(stage,phase,u){
 if(stage===3&&phase==='after')return fallFrame(u);
 if(stage>=4)return scatteredFrame(stage,phase,u);
 const loop=phase==='idle',s=sn(u),wave=sn(u,2),env=loop?1:Math.sin(u*Math.PI),release=phase==='after',pull=phase==='pull',prep=phase==='prepare',part=LEGACY_ORDER[stage];
 let angle=s*(stage<2?14:8),dx=s*3,dy=-Math.max(0,wave)*12,sx=1+s*.045,sy=1-s*.065;
 let left=-22+s*40,right=10+sn(u-.16)*22,legLeft=Math.max(0,s)*-42,legRight=Math.min(0,s)*20,lookX=s*3,lookY=0,eyeX=1,eyeY=1,mouth='smile',hat=-s*7;
 if(stage>=1){right=-22+wave*9;lookX=5;lookY=7;mouth='flat'}
 if(stage>=2){left=-120+sn(u)*15;eyeX=1.09;eyeY=1.13;angle=sn(u)*11;hat=0}
 if(stage>=3){left=-45+s*15;lookY=8;mouth='open';sx=1+wave*.05}
 if(stage>=4){angle=8+s*12;left=-40+sn(u-.2)*20}
 if(stage>=5){left=30+s*22;right=-75+sn(u-.2)*20;angle=s*17;mouth='crooked'}
 if(stage>=6){angle=-13+s*13;legLeft=sn(u,2)*24;legRight=-sn(u,2)*24;eyeY=.83;mouth='crooked'}
 if(stage>=7){angle=-s*16;left=-95+sn(u-.25)*22;mouth='wince';lookX=8}
 if(stage>=8){dy=-Math.abs(s)*37;angle=-12+s*18;legRight=-20+Math.abs(s)*30;left=sn(u-.25)*45;lookY=11}
 if(stage>=9){angle=-8+s*22;dy=-Math.abs(s)*33;mouth='tight';eyeY=.9}
 if(stage>=10){lookX=8;mouth='crooked';dy-=Math.max(0,wave)*10}
 if(stage>=11){eyeX=1.17;eyeY=1.2;lookX=sn(u,2)*10;mouth='wince'}
 if(stage>=12){angle=-57+s*10;dy=84-Math.abs(s)*8;dx=-35;sx=1.05+wave*.04;sy=.88-wave*.04;lookY=12;mouth='wince'}
 if(stage>=13){angle=0;dx=0;dy=0;sx=sy=1;lookX=10+s*5;lookY=5;mouth='wince'}
 if(stage>=14){eyeX=1+wave*.18;eyeY=.65-wave*.15;mouth='small'}
 if(stage>=15){mouth='tinySmile';}
 if(stage>=16){angle=dx=dy=0;}
 if(loop&&stage>0&&stage<4){angle=s*(1+stage*1.4);dx=s*.5;dy=-Math.max(0,wave)*stage;sx=1+s*.008*stage;sy=1-s*.01*stage;legLeft=0;legRight=0;}
 if(loop&&stage===0){const gesture=(a,b)=>u>a&&u<b?Math.sin((u-a)/(b-a)*Math.PI)**2:0;const glance=gesture(.28,.43),waveHello=gesture(.78,.9);angle=0;dx=0;dy=0;sx=1;sy=1+sn(u)*.003;left=waveHello*38;right=-glance*9;legLeft=legRight=hat=0;lookX=glance*8;lookY=glance*5;eyeX=eyeY=1;mouth='smile';}
 if(loop&&stage===3){angle=-9+s*17;dy=-Math.abs(s)*35;legRight=-12+Math.abs(s)*25;left=-s*30;lookY=10;}
 if(loop&&stage>=3&&stage<13){const beat=u%1;mouth=beat<.2?'open':beat<.45?'wince':beat<.7?'crooked':'tight';eyeY*=1+sn(u,2)*.09;lookX+=sn(u,2)*3;}
 if(!loop){
   const response=release?Math.sin(u*Math.PI*5)*(1-u):env;
   angle=(release?response*17:sn(u)*4);dy=release?-Math.abs(response)*13:-env*9;sx=1+response*.08;sy=1-response*.09;lookX=0;lookY=0;
   switch(part){
   case 0:lookX=12;lookY=10;right=prep?-25-env*18:pull?-45+u*70:-18+response*10;mouth=release?'flat':'small';break;
   case 1:lookY=-12;eyeY=release?1.28:.82;left=release?-135+response*18:-35;dy=prep?env*17:pull?-env*33:Math.abs(response)*16;hat=pull?-env*23:0;break;
   case 2:lookY=15;left=-55;right=-50;angle=response*20;mouth=release?'open':'small';break;
   case 3:angle=release?response*25:env*19;lookX=-8;lookY=11;left=release?-35:-5;break;
   case 4:angle=release?response*33:-env*19;lookX=9;lookY=11;left=release?-70+response*25:10;right=release?-65-response*25:10;break;
   case 5:lookY=13;eyeY=.65;mouth='small';angle=release?-15+response*18:env*16;dy=-env*26;legLeft=sn(u,3)*20;legRight=-sn(u,3)*20;break;
   case 6:lookX=13;right=prep?-45:pull?u*65:0;angle=release?-response*24:env*20;left=release?-85:15;mouth='wince';break;
   case 7:legLeft=sn(u,2)*25;legRight=-sn(u,2)*25;dy=-Math.abs(sn(u,2))*25;angle=release?-12+response*18:-env*17;lookY=15;break;
   case 8:left=prep?-100:pull?u*40:0;dy=-Math.abs(sn(u,2))*24;angle=release?response*30:-env*25;mouth='tight';break;
   case 9:lookX=prep?-8:12;lookY=9;mouth='crooked';dy=-Math.abs(sn(u,2))*24;break;
   case 10:lookX=12;angle=release?response*24:-env*22;eyeX=1.2;eyeY=1.18;mouth='wince';dy=-Math.abs(sn(u,2))*23;break;
   case 11:dy=release?88+response*20:-Math.abs(sn(u,2))*40;angle=release?-57+response*26:sn(u)*20;sx=release?1.12+response*.14:1;sy=release?.83-response*.14:1;lookY=14;mouth='wince';break;
   case 12:angle=release?0:-55+sn(u,2)*5;dy=release?0:80-env*25;lookX=sn(u,3)*12;mouth='crooked';break;
   case 13:lookX=sn(u,3)*12;eyeX=pull?1+env*.6:1;eyeY=release?.6:1;mouth=release?'small':'wince';angle=0;dy=0;break;
   case 14:lookX=sn(u,2)*10;mouth=release?'tinySmile':'wide';angle=0;dy=0;break;
   case 15:mouth=pull?'flat':'small';angle=0;dy=0;break;
   }
 }
 const squint=loop?(stage===0?u>.29&&u<.43:stage<4&&u>.31&&u<.48):stage>=1&&stage<4&&env>.75;
 if(squint&&loop&&stage>0)mouth='wince';
 const effective=release?Math.min(16,stage+1):stage,faceDown=effective>=13;
 if(faceDown){angle=0;dy=0;dx=0;sx=sy=1;}
 const root=`${translate(dx,dy)} ${rotate(angle,effective>=8?188:151,398)} ${scaleAt(sx,sy,160,300)}`;
 let nodes='';
 for(let n=0;n<20;n++){
  if(n===2){nodes+=`<g class="body-whole"><use href="#pet-2"/></g><g class="assembled-body-pieces">${BODY_INDICES.map(i=>`<g class="p${i}">${partGeometry(i)}</g>`).join('')}</g>`;continue;}
  const pi=PARTS.findIndex(p=>p.paths.includes(n));let tr='',replace='';
  if(n===0)tr=rotate(legLeft,117,285);if(n===1)tr=rotate(legRight,187,285);
  if(n===5||n===6)tr=`${translate(-s*3,-Math.max(0,wave)*4)} ${rotate(hat,205,68)}`;
  if(n===8||n===9){const isLeft=n===9,turn=isLeft?left:right;tr=rotate(turn,isLeft?54:274,212);let d='';const poseStage=release?stage+1:stage;
   if(isLeft&&poseStage===2)d=`M54 212Q${rnd(-16+s*12)} 110 ${rnd(144+s*5)} 43`;
   if(isLeft&&(poseStage===3||!loop&&part===2))d=`M54 212Q${rnd(70+s*8)} 267 132 224`;
   if(isLeft&&poseStage===4)d=`M54 212Q${rnd(12+s*9)} 274 110 263`;
   if(poseStage===5)d=isLeft?`M54 212Q123 ${rnd(280+s*8)} 199 247`:`M274 212Q198 ${rnd(286-s*8)} 120 248`;
   if(isLeft&&poseStage===7)d=`M54 212Q146 ${rnd(281+s*10)} 271 213`;
   if(isLeft&&poseStage===8)d=`M54 212Q${rnd(2+s*16)} 143 ${rnd(17+s*25)} 105`;
   if(d){tr=rotate(s*3,isLeft?54:274,212);replace=`<path d="${d}" stroke="#393ac8" stroke-width="7" stroke-linecap="round" fill="none"/>`;}
  }
  if([10,11,12,13].includes(n)){const pupil=n===11||n===13,cx=n<12?109:160;tr=squint?(pupil?translate(lookX*.35,lookY*.12):''):`${scaleAt(eyeX,eyeY,cx,144)} ${pupil?translate(lookX,lookY):''}`;const blink=stage===0?(u>.12&&u<.14||u>.69&&u<.71):(u>.82&&u<.89);if(loop&&blink&&!squint)tr+=` ${scaleAt(1,.08,cx,144)}`;if(squint)replace=squintEyeMarkup(n);}
  if(n===19){tr=rotate(-angle*.4+wave*8,140,222);if(pull&&part===2)tr+=` ${scaleAt(1+env*.85,1,140,222)}`;}
  if(n>=15&&n<=18){tr=rotate(right,274,212);}
  if(pull&&pi===stage){const b=PARTS[stage].box;const dir=[3,4,7,8].includes(part)?(part===3||part===8?-1:1):1;tr+=` ${translate(env*dir*([3,4,6,8].includes(part)?25:9),-env*([1,5,9,10,12,14,15].includes(part)?35:3))} ${rotate(env*(part===3?-25:part===4?25:part===10?-35:0),b[0],b[1])}`;if([9,13].includes(part))tr+=` ${scaleAt(1+env*.65,1-env*.25,b[0],b[1])}`;}
  if(n===14)replace=mouthMarkup(mouth);
  if(faceDown&&[10,11,12,13,14].includes(n)){const eye=n!==14;let fall=1;if(release&&stage===12){const t=Math.max(0,Math.min(1,(u-(eye?0:.09))*2.5));fall=t<.7?(t/.7)**2:1-Math.abs(Math.sin((t-.7)/.3*Math.PI*2))*.12*(1-t)/.3;}tr=`${translate(((eye?5:-3)+s*(eye?5:9))*fall,(eye?251-Math.abs(wave)*8:230-Math.abs(wave)*4)*fall)} ${rotate(release&&stage===12?sn(u)*18*(1-u):0,eye?109:144,144)} ${tr}`;}
  nodes+=`<g class="p${pi}" transform="${tr}">${replace||`<use href="#pet-${n}"/>`}</g>`;
 }
 return `<g transform="${root}">${nodes}</g>`;
}
export function minionFrame(type,u,carry=false){
 // Same model order, rigid body, ±32° stride and 4° sway as redcowork.
 // Pivots come from the actual path starts, not a guessed width/height ratio.
 const height=[146,156,153][type],width=[161,147,157][type],s=sn(u),bob=-Math.abs(s)*11.43;
 const hips=[[[94.7396,115.139],[67.4812,115.139]],[[91.8753,124.798],[64.6169,124.798]],[[89.0579,122.603],[61.7996,122.603]]][type];
 let out=[0,1].map(i=>`<g data-walk-leg="${i}" transform="${rotate(s*(i===0?32:-32),...hips[i])}"><use href="#m${type}-${i}"/></g>`).join('');
 out+=minionBody(type);
 if(carry)out+=`<path d="M8 80Q-28 56 -16 20M${width-6} 78Q${width+29} 49 ${width+12} 18" stroke="#4a4939" stroke-width="6" stroke-linecap="round" fill="none"/>`;
 return `<g data-walk="redcowork" transform="${translate(0,bob)} ${rotate(s*4,0,-height/2)} ${translate(-width/2,-height)}">${out}</g>`;
}
const minionOrigin=type=>translate(-[161,147,157][type]/2,-[146,156,153][type]);
const minionBody=type=>Array.from({length:6},(_,i)=>`<use href="#m${type}-${i+2}"/>`).join('');
const sourceMinions=JSON.parse(fs.readFileSync(new URL('../dist/assets/shapes.json',import.meta.url),'utf8')).minions;
const smooth=t=>{t=Math.max(0,Math.min(1,t));return t*t*(3-2*t)};
// Sample each original closed outline in radial order. All following frames morph
// that same opaque surface; there is no crossfade to a replacement silhouette.
const minionContours=sourceMinions.map((parts,type)=>{
 const d=parts[2].match(/\bd="([^"]+)"/)[1],tokens=d.match(/[MLCZ]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?/g);
 const polygon=[];let at=0,current,start;
 const point=()=>[+tokens[at++],+tokens[at++]];
 while(at<tokens.length){const command=tokens[at++];
  if(command==='M'){current=point();start=current;polygon.push(current)}
  else if(command==='L'){current=point();polygon.push(current)}
  else if(command==='C'){const a=current,b=point(),c=point(),end=point();for(let j=1;j<=32;j++){const t=j/32,s=1-t;polygon.push([s*s*s*a[0]+3*s*s*t*b[0]+3*s*t*t*c[0]+t*t*t*end[0],s*s*s*a[1]+3*s*s*t*b[1]+3*s*t*t*c[1]+t*t*t*end[1]])}current=end}
  else if(command==='Z')polygon.push(start);
  else throw Error(`Unsupported minion contour command ${command}`);
 }
 const w=[161,147,157][type],h=[111,118,118][type],height=[146,156,153][type],cx=w/2,cy=h/2;
 return Array.from({length:48},(_,i)=>{const angle=i/48*Math.PI*2,dx=Math.cos(angle),dy=Math.sin(angle);let distance=0;
  for(let j=1;j<polygon.length;j++){const a=polygon[j-1],b=polygon[j],ex=b[0]-a[0],ey=b[1]-a[1],ax=a[0]-cx,ay=a[1]-cy,denom=dx*ey-dy*ex;if(Math.abs(denom)<1e-8)continue;const r=(ax*ey-ay*ex)/denom,v=(ax*dy-ay*dx)/denom;if(r>=0&&v>=0&&v<=1)distance=Math.max(distance,r)}
  if(distance===0)throw Error('Minion outline is not closed');
  return [cx+dx*distance-w/2,cy+dy*distance-height];
 });
});
function closedCurve(points){
 let d=`M${points[0].map(rnd).join(' ')}`;
 for(let i=0;i<points.length;i++){const a=points[(i+points.length-1)%points.length],b=points[i],c=points[(i+1)%points.length],e=points[(i+2)%points.length];d+=`C${[b[0]+(c[0]-a[0])/6,b[1]+(c[1]-a[1])/6,c[0]-(e[0]-b[0])/6,c[1]-(e[1]-b[1])/6,c[0],c[1]].map(rnd).join(' ')}`}
 return d+'Z';
}
export function minionSlime(type,t){
 const landed=Math.max(0,t-.95),melt=smooth(landed/1.05),drain=smooth((t-3.05)/.95),color=['#F04438','#B7E85B','#FFAC52'][type];
 if(t>=4)return '<g data-debris-group="melted" data-geometry-empty="true"/>';
 const lag=Math.sin(landed*10)*Math.exp(-landed*2.6)*melt*17;
 const points=minionContours[type].map((p,i)=>{const a=i/48*Math.PI*2,upper=Math.max(0,-Math.sin(a)),ripple=Math.sin(a*3+landed*8)*upper*4*Math.exp(-landed*1.4);
  const targetX=Math.cos(a)*(109+type*3)*(1+Math.sin(landed*10)*Math.exp(-landed*3)*.16)+upper*lag;
  const targetY=-19+Math.sin(a)*19+ripple;
  const x=p[0]+(targetX-p[0])*melt,y=p[1]+(targetY-p[1])*melt;
  return [x*(1-drain*.985),y*(1-drain)**2];
 });
 const surface=`<path data-minion-melt="${rnd(melt)}" data-slime-drain="${rnd(drain)}" fill="${color}" d="${closedCurve(points)}"/>`;
 const absorbed=smooth((t-2.15)/.7),face=absorbed>=1?'':`<g data-face-absorb="${rnd(absorbed)}" transform="${translate(0,melt*60+absorbed*12)} ${scaleAt(1+melt*.12,1-melt*.72,0,-75)} ${scaleAt(1-absorbed,1-absorbed,0,-75)}"><g transform="${minionOrigin(type)}">${[3,4,5,6,7].map(i=>`<use href="#m${type}-${i}"/>`).join('')}</g></g>`;
 // Solid droplets shrink back into the puddle; neither droplets nor torso fade.
 let drops='';if(landed>0&&landed<.58)for(let i=0;i<2;i++){const q=landed/.58,r=(4+i)*Math.max(0,1-smooth((q-.45)/.55));if(r<.1)continue;const x=(i?1:-1)*(65+q*78),y=-Math.sin(q*Math.PI)*(32+i*14);drops+=`<path data-slime-drop="${i}" fill="${color}" transform="${translate(x,y)}" d="M${-r} 0C${-r} ${-r*1.7} ${r} ${-r*1.7} ${r} 0C${r} ${r} ${-r} ${r} ${-r} 0Z"/>`}
 return `<g data-debris-group="slime">${surface}${face}${drops}</g>`;
}
// Sample a small fixed-step rigid-body simulation while recording frames.
// Each collision reduces vertical energy and applies contact friction to spin.
function landingMotion(kind,type,index,t){
 const leg=kind==='leg',side=leg?(index===0?1:-1):(Math.cos(index/12*Math.PI*2+.19)<0?-1:1),gravity=leg?1650:1280;
 let x=0,y=0,angle=0,vx=side*(leg?145+type*22+index*28:310+type*25),vy=-(leg?265+index*90+type*20:335),omega=leg?side*(420+index*95+type*30):vx/48*180/Math.PI*.78,contacts=0,resting=false;
 for(let elapsed=0;elapsed<t;){const dt=Math.min(1/240,t-elapsed);elapsed+=dt;
  x+=vx*dt;angle+=omega*dt;
  if(!resting){y+=vy*dt;vy+=gravity*dt;vx*=Math.exp(-(leg?.35:.25)*dt);omega*=Math.exp(-(leg?.22:.16)*dt);
   if(y>=0&&vy>0){y=0;const hitSpeed=vy,restitution=(leg?.52:.48)*.72**contacts;contacts++;vx*=leg?.78:.82;
    if(hitSpeed>90&&contacts<5){vy=-hitSpeed*restitution;omega=leg?-omega*(.28+index*.1)+side*hitSpeed*.16:omega*.67+Math.sign(vx)*hitSpeed*.18;}
    else{vy=0;omega*=.45;resting=true;}
   }
  }else{const target=vx/(leg?19:44)*180/Math.PI*(leg?.35:.7);omega+=(target-omega)*Math.min(1,dt*(leg?8:5));vx*=Math.exp(-(leg?5:2.8)*dt);if(Math.abs(vx)<1&&Math.abs(omega)<2)vx=omega=0;}
 }
 return{x,y,angle};
}
export function debrisPose(i,t){const a=i/12*Math.PI*2+.19,vx=Math.cos(a)*(1140+(i%3)*170),land=.82+(i%4)*.085,flight=Math.min(t,land),p=flight/land,ground=Math.sin(a)*190,airAngle=(i%2?1:-1)*flight*(300+i*35);if(t<=land)return{x:vx*flight,y:ground*p-Math.sin(Math.PI*p)*(310+(i%3)*75),angle:airAngle,land};const b=landingMotion('body',i%3,i,t-land);return{x:vx*land+b.x,y:ground+b.y,angle:airAngle+b.angle,land};}
export function crushedFrame(type,u){
 const t=Math.max(0,Math.min(1,u))*4;
 if(t>=4)return minionSlime(type,t);
 if(t<.13){const attached=`<g transform="${minionOrigin(type)}">${[0,1].map(i=>`<use href="#m${type}-${i}"/>`).join('')}${minionBody(type)}</g>`;return `<g data-debris-group="pressed" transform="scale(${rnd(1+t*2)} ${rnd(1-t*5.8)})">${attached}</g>`}
 // No legs after takeoff. Landing unwinds into a horizontal, opaque slime puddle.
 const elapsed=t-.13,flight=Math.min(.82,elapsed),q=debrisPose(0,flight),landed=Math.max(0,t-.95),settle=smooth(landed/.32);
 const angle=q.angle+(360-q.angle)*settle,release=smooth(elapsed/.18),stretch=Math.sin(Math.min(1,elapsed/.82)*Math.PI)*.16;
 const sx=1.26-.26*release-stretch,sy=.246+.754*release+stretch;
 const x=q.x+(1-Math.exp(-landed*4))*45,y=q.y-Math.sin(Math.min(1,landed/.45)*Math.PI)*Math.exp(-landed*3)*26;
 return `<g data-debris-piece="body" data-knockback="legless" transform="${translate(x,y)} ${rotate(angle,0,0)} ${scaleAt(sx+(1-sx)*settle,sy+(1-sy)*settle,0,0)}">${minionSlime(type,t)}</g>`;
}
export function fallFrame(u){const t=u*3;
 if(t<1.05){const p=Math.max(0,Math.min(1,(t-.18)/.87)),anticipation=t<.18?Math.sin(t/.18*Math.PI):0,angle=-360*p,y=110*p-Math.sin(p*Math.PI)*180+anticipation*15,sx=1+anticipation*.18-Math.sin(p*Math.PI)*.12,sy=1-anticipation*.22+Math.sin(p*Math.PI)*.18;let parts='';
 for(let n=0;n<20;n++){const i=PARTS.findIndex(p=>p.paths.includes(n));let shape=n===2?liquidBodyMarkup(t,'air'):`<use href="#pet-${n}"/>`,tr='';if([10,11,12,13].includes(n)){const cx=n<12?109:160,pupil=n===11||n===13,blink=p<.14||p>.88?1:1.18;if(p<.14||p>.88){shape=squintEyeMarkup(n);tr=pupil?translate(Math.sin(p*Math.PI*2)*2,2):''}else tr=`${scaleAt(1.08,blink,cx,144)} ${pupil?translate(Math.sin(p*Math.PI*2)*5,8):''}`;}if(n===14)shape=mouthMarkup(p<.16?'flat':p<.67?'small':'crooked');parts+=`<g class="${n===2?'body-whole':'p'+i}" transform="${tr}">${shape}</g>`;}
 return `<g data-fall="somersault" transform="${translate(0,y)} ${rotate(angle,160,230)} ${scaleAt(sx,sy,160,230)} skewX(${rnd(Math.sin(p*Math.PI*2)*9)})">${parts}</g>`;
 }
 const landed=t-1.05,pulse=Math.exp(-landed*3.4)*Math.cos(landed*15);let out=`<g data-fall="impact" transform="translate(206 445) scale(${rnd(1+pulse*.36)} ${rnd(1-pulse*.53)}) skewX(${rnd(Math.sin(landed*13)*Math.exp(-landed*3)*9)}) translate(-206 -445)">${scatteredFrame(4,'land',landed)}</g>`;
 if(landed<.62){for(let i=0;i<3;i++){const p=Math.min(1,landed/(.46+i*.05)),x=181+i*23+(i-1)*p*24,y=324-Math.sin(p*Math.PI)*(25+i*9)+p*9,r=2.3+i*.7,fade=Math.max(0,1-landed/.62);out+=`<path data-splash="${i}" d="M0 ${-r*1.5}C${r} ${-r*.7} ${r*1.3} ${r*.8} 0 ${r}C${-r*1.3} ${r*.8} ${-r} ${-r*.7} 0 ${-r*1.5}Z" transform="${translate(x,y)} ${rotate((i-1)*p*45,0,0)}" fill="#DE588B" opacity="${rnd(fade)}"/>`;}}
 return out;
}

export function scatteredFrame(stage,phase,u){let out='';const landed=phase==='land',pulse=landed?Math.exp(-u*3.4)*Math.cos(u*15):sn(u)*.17,collapse=false,drop=1,progress=1;
 let bodyOpen=false;for(const i of [0,1,2,3,5,6,7,8,10,...BODY_INDICES,4,13,11,17,15,18]){if(i===BODY_INDICES[0]){out+=`<g class="body-whole">${liquidBodyMarkup(u,landed?'land':'idle')}</g><g class="assembled-body-pieces">`;bodyOpen=true;}if(i===4&&bodyOpen){out+='</g>';bodyOpen=false;}const p=PARTS[i],q=SCATTER[i],b=p.box,cx=b[0]+b[2]/2,cy=b[1]+b[3]/2,face=[11,13,15,17,18].includes(i),pupil=i===11||i===15;let x=cx+(206+q.x/PET.scale-cx)*progress,y=cy+(398+q.y/PET.scale-cy)*progress;const wobble=sn(u)*(face?.6:1.2);let angle=q.angle*progress+wobble;if(phase==='pull'&&stage===i){y-=Math.sin(u*Math.PI)*30;x+=Math.sin(u*Math.PI)*8;}if(collapse)y-=Math.sin(drop*Math.PI)*(face?18:30);if(pupil){x+=sn(u)*3;y+=cs(u)*1.2;}const blink=face&&i!==18&&phase==='idle'&&u>.68&&u<.77;const size=1+((q.scale||1)-1)*progress;
 let shape=partGeometry(i);if(p.style==='chunk')shape=softBodyMarkup(p.chunk,u*Math.PI*2,pulse,phase==='pull'&&stage===i?Math.sin(u*Math.PI):0);const squint=face&&i!==18&&(landed?u>.42&&u<.72:phase==='idle'&&u>.28&&u<.46);if(squint)shape=squintEyeMarkup(p.paths[0]);
 if(face){y+=landed?Math.exp(-u*4)*Math.sin(u*15)*5:Math.sin(u*Math.PI*2)*2;if(pupil)x+=Math.sin(u*Math.PI*2)*2;}
 if(i===18){const expression=landed?(u<.17?'flat':u<.53?'small':u<1.15?'crooked':'tinySmile'):(u<.2?'small':u<.42?'smile':u<.65?'wince':u<.83?'tight':'tinySmile');shape=mouthMarkup(expression);}
 out+=`<g class="p${i}" transform="${translate(x,y)} scale(${rnd(size)} ${rnd(size*(1+((q.flatten||1)-1)*progress)*(blink?.1:1))}) ${rotate(angle,0,0)} translate(${-cx} ${-cy})">${shape}</g>`;}
 return out;
}

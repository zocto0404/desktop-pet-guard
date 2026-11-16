import fs from 'node:fs';
import path from 'node:path';
import {definitions,mouthMarkup,squintEyeMarkup} from './motion.mjs';

const shapes=JSON.parse(fs.readFileSync('dist/assets/shapes.json','utf8'));
const expressions=[
 {mouth:'smile',pupilX:0,pupilY:0,eyeY:1},
 {mouth:'small',pupilX:8,pupilY:2,eyeY:.92},
 {mouth:'crooked',pupilX:-5,pupilY:3,eyeY:.9},
 {mouth:'smile',pupilX:0,pupilY:0,squint:true},
 {mouth:'wince',pupilX:0,pupilY:0,squint:true},
 {mouth:'wide',pupilX:-2,pupilY:-3,eyeY:1.15}
];
const poses=expressions.map((e,index)=>{
 const x=index%3*450+20,y=Math.floor(index/3)*430+16;
 const parts=Array.from({length:20},(_,n)=>{
  if(n===14)return mouthMarkup(e.mouth);
  if(e.squint&&[10,11,12,13].includes(n))return squintEyeMarkup(n);
  const pupil=n===11||n===13,white=n===10||n===12,cx=n<12?109:160;
  const tr=pupil?`translate(${e.pupilX} ${e.pupilY})`:white?`translate(0 144) scale(1 ${e.eyeY}) translate(0 -144)`:'';
  return `<g transform="${tr}"><use href="#pet-${n}"/></g>`;
 }).join('');
 return `<g transform="translate(${x} ${y})">${parts}</g>`;
}).join('');
const svg=`<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="1400" height="880" viewBox="0 0 1400 880"><defs>${definitions(shapes)}</defs>${poses}</svg>`;
const output=path.resolve('../../outputs/桌宠表情姿态预览.svg');
fs.writeFileSync(output,svg);
console.log(output);

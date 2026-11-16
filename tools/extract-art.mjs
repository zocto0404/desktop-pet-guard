import fs from 'node:fs';
const extract = file => [...fs.readFileSync(file,'utf8').matchAll(/<path\b[^>]*\/>/g)].map(m=>/\bfill=/.test(m[0])?m[0]:m[0].replace('<path','<path fill="none"'));
const data={pet:extract('dist/assets/pet.svg'),minions:[1,2,3].map(n=>extract(`dist/assets/minion${n}.svg`))};
fs.writeFileSync('dist/assets/shapes.json',JSON.stringify(data));
console.log('Original SVG paths extracted:',data.pet.length,data.minions.map(a=>a.length));

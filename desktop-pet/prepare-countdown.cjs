// Preserve the final source conversation's geometry and double-row formations.
const fs=require('fs'),path=require('path'),vm=require('vm');
const source=path.resolve(__dirname,'../output/pet-countdown');
const pets=JSON.parse(fs.readFileSync(path.join(source,'assets.json'),'utf8'));
const code=fs.readFileSync(path.join(source,'engine.js'),'utf8').replace('/*ASSETS*/',JSON.stringify(pets));
const context={module:{exports:{}}};vm.runInNewContext(code,context);
const target=path.join(__dirname,'Assets/countdown');fs.mkdirSync(target,{recursive:true});
fs.writeFileSync(path.join(target,'source.json'),JSON.stringify({pets,formations:context.module.exports.formations}));
if(pets.length!==3||context.module.exports.formations.some(f=>f.length!==60))throw Error('Unexpected source assets');
console.log('Preserved three SVG pets and all five final 60-actor formations.');

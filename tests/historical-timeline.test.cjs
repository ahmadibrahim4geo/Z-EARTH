const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const {normalize,closestIndex,trustedTemplate}=require('../src/ZEarth.Globe/Assets/historical-timeline.js');
const base='https://wayback.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/WMTS/1.0.0/default028mm/MapServer/tile/';
test('securely validate Wayback basemap tile origin',()=>{
 assert.equal(trustedTemplate(base+'56102/{z}/{y}/{x}'),true);
 assert.equal(trustedTemplate('https://evil.test/World_Imagery/tile/1/{z}/{y}/{x}'),false);
 assert.equal(trustedTemplate('https://wayback.maptiles.arcgis.com.evil.test/World_Imagery/tile/1/{z}/{y}/{x}'),false);
});
test('sort and dedupe release dates for chronological navigation',()=>{
 const r=normalize([{releaseId:'9',date:'2023-12-07',tileUrl:base+'9/{z}/{y}/{x}'},
 {releaseId:'2',date:'2014-02-20',tileUrl:base+'2/{z}/{y}/{x}'},
 {releaseId:'9',date:'2023-12-07',tileUrl:base+'9/{z}/{y}/{x}'}]);
 assert.deepEqual(r.map(x=>x.releaseId),['2','9']);
 assert.equal(closestIndex(r,'2019-01-01'),0);
 assert.equal(closestIndex(r,'2024-02-01'),1);
 assert.equal(closestIndex(r,'2013-01-01'),0);
});
test('invalid catalog records are rejected',()=>{
 assert.deepEqual(normalize(null),[]);
 assert.deepEqual(normalize([{releaseId:'a',date:'2023-01-01',tileUrl:base+'a/{z}/{y}/{x}'}]),[]);
});
test('viewport contains the inline controls and ordered scripts',()=>{
 const html=fs.readFileSync(path.join(__dirname,'../src/ZEarth.Globe/Assets/index.html'),'utf8');
 const app=fs.readFileSync(path.join(__dirname,'../src/ZEarth.Globe/Assets/cesium-engine.js'),'utf8');
 for(const id of ['history-timeline','history-range','history-prev','history-next','history-date','history-year','history-close'])assert.ok(html.includes('id="'+id+'"'));
 assert.ok(html.indexOf('historical-timeline.js')<html.indexOf('cesium-engine.js'));
 assert.ok(app.includes("case 'historical.show':"));
});

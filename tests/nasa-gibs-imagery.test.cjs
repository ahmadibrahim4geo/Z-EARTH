const test=require('node:test');const assert=require('node:assert/strict');
const vm=require('node:vm');const fs=require('node:fs');const path=require('node:path');
const source=fs.readFileSync(path.join(__dirname,'../src/ZEarth.Globe/Assets/nasa-gibs-imagery.js'),'utf8');
const context={window:{}};vm.runInNewContext(source,context);
const events=[];const providers=[];const removed=[];
const Cesium={WebMapServiceImageryProvider:class{constructor(cfg){this.config=cfg;this.errorEvent={addEventListener(){}};providers.push(cfg);}}};
const viewer={imageryLayers:{addImageryProvider(p){return p;},remove(p,destroy){removed.push({p,destroy});}},scene:{requestRender(){}}};
const controller=context.window.ZEarthNasaGibs.create({Cesium,viewer,onStatus:v=>events.push(v)});
test('GIBS allows only known NASA layers and valid historical dates',()=>{
 assert.equal(controller.set('arbitrary','2012-07-09').success,false);
 assert.equal(controller.set('MODIS_Terra_CorrectedReflectance_TrueColor','2026-13-08').success,false);
 assert.equal(controller.set('MODIS_Aqua_CorrectedReflectance_TrueColor','2001-01-01').success,false);
 assert.equal(providers.length,0);
});
test('GIBS replaces one overlay, keeps source date and never claims tiles verified',()=>{
 const terra='MODIS_Terra_CorrectedReflectance_TrueColor';
 assert.equal(controller.set(terra,'2012-07-09').success,true);
 assert.equal(providers[0].parameters.time,'2012-07-09');
 assert.equal(providers[0].layers,terra);
 assert.equal(providers[0].url,'https://gibs.earthdata.nasa.gov/wms/epsg3857/best/wms.cgi');
 assert.equal(events[0].tilesVerified,false);
 assert.equal(controller.set(terra,'2012-07-10').success,true);
 assert.equal(removed.length,1);
 assert.equal(controller.clear().configured,false);
 assert.equal(removed.length,2);
});

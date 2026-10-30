const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../src/ZEarth.Globe/Assets/flight-simulator.js'), 'utf8');
const viewModelSource = fs.readFileSync(path.join(__dirname, '../src/ZEarth.App/ViewModels/MainViewModel.cs'), 'utf8');
const raf = new Map();
let nextId = 1;
const handlers = {};
let cameraView = null;
let createdHud = null;
const node = { textContent:'', style:{}, setAttribute(){}, remove(){ this.removed = true; } };
const scene = {
  mode: 3,
  screenSpaceCameraController:{ enableRotate:true, enableTranslate:true, enableZoom:true, enableTilt:true, enableLook:false },
  globe:{ getHeight() { return 100; } },
  requestRender() {}
};
const camera = { position: { x:1, y:2, z:3 }, positionCartographic:{ longitude:31, latitude:30 }, heading:0.2,pitch:-0.5,roll:0,
  setView(v) { cameraView = v; } };
const Cesium = { SceneMode:{ SCENE3D:3 }, Math:{ toDegrees(x){return x;} },
  Cartesian3:{ fromDegrees(lon,lat,h){return {lon,lat,h};},clone(p){return {...p};} },
  Cartographic:{fromDegrees(lon,lat){return {lon,lat};}} };
const context = { window: {
  requestAnimationFrame(fn) {const id=nextId++;raf.set(id,fn);return id;},
  cancelAnimationFrame(id){raf.delete(id);},
  addEventListener(name,fn){handlers[name]=fn;}
}, document:{
  createElement(){createdHud={...node,style:{}};return createdHud;},
  getElementById(){return {appendChild(){}};},
  addEventListener(name,fn){handlers[name]=fn;},
  body:{appendChild(){}}
} };
vm.runInNewContext(source,context);
const flight = context.window.ZEarthFlightSimulator.create({Cesium,viewer:{camera,scene}});
assert.equal(flight.getState().enabled,false);
scene.mode=2;
assert.equal(flight.start().enabled,false);
scene.mode=3;
assert.equal(flight.start().enabled,true);
assert.equal(flight.start().alreadyActive,true);
assert.equal(scene.screenSpaceCameraController.enableRotate,false);
assert.match(createdHud.textContent,/CAMERA MODE \/ NOT AIRCRAFT SIMULATION/);
assert.match(createdHud.textContent,/Bank/);
assert.match(createdHud.textContent,/VS/);
assert.match(createdHud.textContent,/AGL/);
const baseLon=flight.getState().longitude;
for(const [id,t] of [...raf]){raf.delete(id);t(1000);}
for(const [id,t] of [...raf]){raf.delete(id);t(1100);}
assert.ok(cameraView);
assert.equal(flight.getState().enabled,true);
assert.ok(flight.getState().latitude !== 30 || flight.getState().longitude !== baseLon);
handlers.keydown({key:'PageUp',preventDefault(){}});
assert.ok(flight.getState().throttle > 0.55);
handlers.keydown({key:'d',preventDefault(){}});
for(const [id,t] of [...raf]){raf.delete(id);t(1200);}
assert.ok(flight.getState().bankDegrees > 0);
handlers.keyup({key:'d'});
assert.equal(flight.getState().terrainSampleAvailable,true);
assert.ok(Number.isFinite(flight.getState().aglMeters));
assert.ok(Number.isFinite(flight.getState().verticalSpeedMetersPerSecond));
handlers.keydown({key:'P',preventDefault(){}});
assert.equal(flight.getState().paused,true);
handlers.keydown({key:'Escape',preventDefault(){}});
assert.equal(flight.getState().enabled,false);
assert.equal(scene.screenSpaceCameraController.enableRotate,true);
assert.equal(flight.setActive(false).enabled,false);
assert.match(viewModelSource, /FlightModeChanged \+= \(s, enabled\) => RunOnUi\(\(\) =>[\s\S]*StatusMessage = GetFlightModeStatus\(enabled\)/);
assert.match(viewModelSource, /Flight navigation OFF; previous camera and regular mouse controls restored\./);
assert.match(viewModelSource, /تم إيقاف التنقل بالطيران واستعادة الكاميرا/);
console.log('Flight simulator source controller PASS (mock; no GPU/aircraft dynamics)');

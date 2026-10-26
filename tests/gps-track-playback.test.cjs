const test=require('node:test'),assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path');
const code=fs.readFileSync(path.join(__dirname,'../src/ZEarth.Globe/Assets/gps-track-playback.js'),'utf8');
const callbacks=new Map();let frameId=0;
const root={requestAnimationFrame(fn){const id=++frameId;callbacks.set(id,fn);return id;},cancelAnimationFrame(id){callbacks.delete(id);}};
vm.runInNewContext(code,{window:root,Date,Math});
const players=root.ZEarthGpsPlayback;
test('GPS interpolation crosses antimeridian by shortest route',()=>{
 const fixes=[{ms:1000,lon:179,lat:0},{ms:2000,lon:-179,lat:0}];
 const midpoint=players.interpolate(fixes,1500);
 assert.ok(Math.abs(midpoint.lon)>=179.9);
});
test('GPS playback uses source times, rejects reversed times, and stops without extra entities',()=>{
 let entities=[],removed=0;
 const viewer={entities:{add(x){entities.push(x);return x;},remove(x){removed++;}},scene:{requestRender(){}}};
 const Cesium={Cartesian3:{fromDegrees(lon,lat){return {lon,lat};}},Cartesian2:class{constructor(x,y){}},
   Color:{YELLOW:{}},HeightReference:{CLAMP_TO_GROUND:1}};
 const events=[];
 const player=players.create({Cesium,viewer,onChange:e=>events.push(e)});
 const data=[{longitude:31,latitude:30,timeUtc:'2020-01-01T00:00:00Z'},
 {longitude:31.1,latitude:30.1,timeUtc:'2020-01-01T00:01:00Z'}];
 assert.equal(player.start([...data].reverse()).enabled,false);
 assert.equal(player.start(data,10).enabled,true);
 assert.equal(entities.length,1);
 for(const [id,fn] of [...callbacks]){callbacks.delete(id);fn(1000);}
 for(const [id,fn] of [...callbacks]){callbacks.delete(id);fn(1100);}
 assert.ok(player.getState().elapsed>0);
 assert.equal(player.setPaused(true).paused,true);
 assert.equal(player.stop().enabled,false);
 assert.equal(removed,1);
 assert.equal(events.at(-1).enabled,false);
});

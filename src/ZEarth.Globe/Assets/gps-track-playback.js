// ZEarth standalone replay of time-tagged GPS fixes. No live tracking/network.
(function (root) {
  'use strict';
  const interpolate = (fixes, targetMs) => {
    if (!fixes || fixes.length < 2) return null;
    let i = 0;
    while (i < fixes.length - 2 && fixes[i + 1].ms < targetMs) i++;
    const a = fixes[i], b = fixes[i + 1];
    const fraction = Math.max(0, Math.min(1, (targetMs - a.ms) / (b.ms - a.ms)));
    let delta = ((b.lon - a.lon + 540) % 360) - 180;
    const lon = ((a.lon + delta * fraction + 540) % 360) - 180;
    return { lon, lat: a.lat + (b.lat - a.lat) * fraction };
  };
  function create({Cesium,viewer,onChange=()=>{}}) {
    if (!Cesium?.Cartesian3 || !viewer?.entities) throw Error('Cesium entities required for GPS playback.');
    let active=false, paused=false, marker=null, fixes=null, elapsed=0, last=0, frame=0, durationMs=30000;
    const host = typeof root.requestAnimationFrame === 'function' ? root : null;
    function report(extra={}) {
      onChange({enabled:active,paused,...extra});
    }
    function stop() {
      if (frame && host) host.cancelAnimationFrame?.(frame);
      frame=0;last=0;elapsed=0;active=false;paused=false;fixes=null;
      if(marker){viewer.entities.remove(marker);marker=null;}
      viewer.scene?.requestRender?.();
      report();
      return {enabled:false};
    }
    function render() {
      if(!fixes || !marker) return;
      const t=fixes[0].ms+(fixes[fixes.length-1].ms-fixes[0].ms)*Math.min(1,elapsed/durationMs);
      const coord=interpolate(fixes,t);
      marker.position=Cesium.Cartesian3.fromDegrees(coord.lon,coord.lat);
      if(marker.label) marker.label.text='GPS UTC '+new Date(t).toISOString().replace('T',' ').slice(0,19);
      viewer.scene?.requestRender?.();
      if(elapsed>=durationMs) stop();
    }
    function tick(now) {
      if(!active) return;
      if(!paused && last) elapsed += Math.min(200,Math.max(0,now-last));
      last=now;
      render();
      if(active && host) frame=host.requestAnimationFrame(tick);
    }
    function start(items,durationSeconds=30) {
      if(!Array.isArray(items) || items.length<2 || items.length>2000 || !host)
        return {enabled:false,error:'Require 2–2000 timed GPX points and animation support.'};
      const parsed=[];
      for(const p of items) {
        const ms=Date.parse(p?.timeUtc);
        if(typeof p?.timeUtc!=='string' ||
            !/^\d{4}-\d{2}-\d{2}T/.test(p.timeUtc) ||
            !Number.isFinite(ms) ||
            !Number.isFinite(p.longitude) || !Number.isFinite(p.latitude) ||
            p.longitude < -180 || p.longitude > 180 || p.latitude < -90 || p.latitude > 90 ||
            (parsed.length>0 && ms<=parsed[parsed.length-1].ms)) {
          return {enabled:false,error:'GPS fixes must have strictly increasing valid UTC times and geographic coordinates.'};
        }
        parsed.push({ms,lon:p.longitude,lat:p.latitude});
      }
      if(!Number.isFinite(durationSeconds) || durationSeconds<5 || durationSeconds>3600)
        return {enabled:false,error:'GPS preview duration must be between 5 and 3600 seconds.'};
      stop();
      fixes=parsed;durationMs=durationSeconds*1000;elapsed=0;last=0;
      marker=viewer.entities.add({
        position:Cesium.Cartesian3.fromDegrees(parsed[0].lon,parsed[0].lat),
        point:{pixelSize:13,color:Cesium.Color?.YELLOW,heightReference:Cesium.HeightReference?.CLAMP_TO_GROUND},
        label:{text:'GPS UTC '+new Date(parsed[0].ms).toISOString().slice(0,19),
          font:'bold 11px Segoe UI',pixelOffset:new Cesium.Cartesian2(0,-24),
          heightReference:Cesium.HeightReference?.CLAMP_TO_GROUND}
      });
      active=true;report({startUtc:new Date(parsed[0].ms).toISOString(),endUtc:new Date(parsed.at(-1).ms).toISOString(),durationSeconds});
      frame=host.requestAnimationFrame(tick);
      return {enabled:true,fixes:fixes.length,durationSeconds};
    }
    function setPaused(value) { if(!active) return {enabled:false};
      paused=!!value;last=0;report();return {enabled:true,paused}; }
    return {start,stop,setPaused,getState:()=>({enabled:active,paused,elapsed,durationMs}),interpolate};
  }
  root.ZEarthGpsPlayback=Object.freeze({create,interpolate});
})(typeof window!=='undefined'?window:globalThis);

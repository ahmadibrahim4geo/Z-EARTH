// ZEarth independent basic flight-navigation mode. No Google aircraft assets,
// simulator source code, proprietary flight models or restricted services used.
(function (root) {
  'use strict';

  function create({ Cesium, viewer, onChange = () => {} }) {
    if (!Cesium?.Math || !Cesium?.Cartesian3 || !viewer?.camera || !viewer?.scene) {
      throw new Error('Flight mode requires a Cesium globe camera and scene.');
    }
    const camera = viewer.camera;
    const scene = viewer.scene;
    const controller = scene.screenSpaceCameraController;
    const keys = new Set();
    const rad = Math.PI / 180;
    let active = false, paused = false, rafId = 0, lastFrame = 0;
    let throttle = 0.55, heading = 0, pitch = 0, bank = 0, verticalSpeed = 0;
    let lon = 0, lat = 0, height = 1600, speed = 0;
    let priorCamera = null, priorController = null, hud = null;

    const bound = (value, min, max) => Math.min(max, Math.max(min, value));
    function is3D() { return scene.mode === Cesium.SceneMode.SCENE3D; }
    function groundHeight() {
      if (typeof scene.globe?.getHeight !== 'function' || !Cesium.Cartographic?.fromDegrees) return null;
      try {
        const value = scene.globe.getHeight(Cesium.Cartographic.fromDegrees(lon, lat));
        return Number.isFinite(value) ? value : null;
      } catch { return null; }
    }
    function updateHud() {
      if (!hud) return;
      const terrain = groundHeight();
      const agl = terrain === null ? null : Math.max(0, height - terrain);
      const altitude = agl === null
        ? `${Math.round(height)} m ellipsoid · TERRAIN N/A`
        : `${Math.round(height)} m ellipsoid · ${Math.round(agl)} m AGL`;
      hud.textContent = `Z EARTH Flight Navigation · ${paused ? 'PAUSED' : 'ACTIVE'} · CAMERA MODE / NOT AIRCRAFT SIMULATION\n` +
        `${Math.round(speed * 3.6)} km/h · ${altitude} · THR ${Math.round(throttle * 100)}%\n` +
        `Heading ${Math.round((heading / rad + 360) % 360)}° · Pitch ${Math.round(pitch / rad)}° · Bank ${Math.round(bank / rad)}° · VS ${Math.round(verticalSpeed)} m/s\n` +
        'Arrows or W/A/S/D: steer · Page Up/Down or +/-: throttle · P/Space: pause · Esc: exit';
    }
    function ensureHud() {
      if (hud) return;
      hud = document.createElement('div');
      hud.id = 'zearth-flight-hud';
      hud.setAttribute('role', 'status');
      hud.setAttribute('aria-live', 'off');
      Object.assign(hud.style, {
        position: 'absolute', top: '16px', left: '50%', transform: 'translateX(-50%)',
        maxWidth: '94%', whiteSpace: 'pre-line', pointerEvents: 'none',
        background: 'rgba(10,20,30,.82)', color: '#e6f3fa',
        font: '12px Consolas, monospace', padding: '9px 14px', zIndex: '9998',
        border: '1px solid rgba(180,205,222,.45)', borderRadius: '6px',
        textAlign: 'center'
      });
      (document.getElementById('cesium-container') || document.body).appendChild(hud);
    }
    function restoreCamera() {
      if (!priorCamera) return;
      camera.setView({
        destination: priorCamera.position,
        orientation: { heading: priorCamera.heading, pitch: priorCamera.pitch, roll: priorCamera.roll }
      });
    }
    function stop(restore = true) {
      if (!active) return { enabled: false };
      active = false; paused = false;
      verticalSpeed = 0;
      keys.clear();
      if (rafId) root.cancelAnimationFrame?.(rafId);
      rafId = 0; lastFrame = 0;
      if (priorController && controller) {
        for (const [k, v] of Object.entries(priorController)) controller[k] = v;
      }
      if (restore && is3D()) restoreCamera();
      if (hud) { hud.remove(); hud = null; }
      priorController = null; priorCamera = null;
      scene.requestRender?.();
      onChange({ enabled: false });
      return { enabled: false };
    }
    function renderCamera() {
      camera.setView({
        destination: Cesium.Cartesian3.fromDegrees(lon, lat, height),
        orientation: { heading, pitch, roll: bank }
      });
      scene.requestRender?.();
    }
    function step(timestamp) {
      if (!active) return;
      const dt = lastFrame === 0 ? 0 : bound((timestamp - lastFrame) / 1000, 0, 0.10);
      lastFrame = timestamp;
      if (!is3D()) { stop(false); return; }
      if (!paused && dt > 0) {
        const rollInput = Number(keys.has('ArrowRight') || keys.has('d')) -
          Number(keys.has('ArrowLeft') || keys.has('a'));
        const pitchInput = Number(keys.has('ArrowDown') || keys.has('s')) -
          Number(keys.has('ArrowUp') || keys.has('w'));
        bank += (rollInput * 30 * rad - bank) * Math.min(1, dt * 3);
        pitch = bound(pitch + pitchInput * dt * 0.24, -28 * rad, 28 * rad);
        heading = ((heading + bank * dt * (0.35 + throttle) + Math.PI * 4) % (Math.PI * 2));
        speed = 25 + throttle * 145; // Approximate camera travel; not an aircraft flight model.
        const distance = speed * Math.cos(pitch) * dt;
        const nextLat = bound(lat + Math.cos(heading) * distance / 111132, -85, 85);
        const denom = 111320 * Math.max(0.10, Math.cos(nextLat * rad));
        lon = ((lon + Math.sin(heading) * distance / denom + 540) % 360) - 180;
        lat = nextLat;
        const previousHeight = height;
        height += speed * Math.sin(pitch) * dt;
        const ground = groundHeight();
        height = bound(Math.max(height, (ground ?? 0) + 100), 100, 50000);
        verticalSpeed = (height - previousHeight) / dt;
        renderCamera();
      } else if (paused) {
        verticalSpeed = 0;
      }
      updateHud();
      rafId = root.requestAnimationFrame(step);
    }
    function start() {
      if (active) return { enabled: true, alreadyActive: true };
      if (!is3D()) return { enabled: false, error: 'Switch to 3D before starting flight mode.' };
      const c = camera.positionCartographic;
      if (!c) return { enabled: false, error: 'Camera location unavailable.' };
      lon = Cesium.Math.toDegrees(c.longitude);
      lat = Cesium.Math.toDegrees(c.latitude);
      if (!Number.isFinite(lon) || !Number.isFinite(lat) || Math.abs(lat) > 85) {
        return { enabled: false, error: 'Fly from a non-polar 3D location.' };
      }
      priorCamera = { position: Cesium.Cartesian3.clone(camera.position), heading: camera.heading, pitch: camera.pitch, roll: camera.roll };
      priorController = {};
      for (const k of ['enableRotate','enableTranslate','enableZoom','enableTilt','enableLook']) {
        priorController[k] = controller[k];
        controller[k] = false;
      }
      heading = camera.heading;
      pitch = 0; bank = 0; verticalSpeed = 0;
      throttle = 0.55; speed = 25 + throttle * 145;
      height = Math.max(1600, (groundHeight() ?? 0) + 600);
      active = true; paused = false; lastFrame = 0;
      ensureHud(); renderCamera(); updateHud();
      rafId = root.requestAnimationFrame(step);
      onChange({ enabled: true });
      return { enabled: true, mode: 'basic-navigation', liveTerrainCollision: groundHeight() !== null };
    }
    function onKeyDown(event) {
      if (!active || event.target?.matches?.('input,textarea,select,[contenteditable="true"]')) return;
      const k = event.key.length === 1 ? event.key.toLowerCase() : event.key;
      if (['ArrowUp','ArrowDown','ArrowLeft','ArrowRight','PageUp','PageDown','Escape',' ',
           'w','a','s','d','+','=','-','_'].includes(k)) {
        event.preventDefault?.();
      }
      if (k === 'Escape') { stop(); return; }
      if (k === 'p' || k === ' ') { if (!event.repeat) paused = !paused; updateHud(); return; }
      if (k === 'PageUp' || k === '+' || k === '=') { throttle = bound(throttle + 0.08, 0, 1); updateHud(); }
      if (k === 'PageDown' || k === '-' || k === '_') { throttle = bound(throttle - 0.08, 0, 1); updateHud(); }
      if (k.startsWith('Arrow') || ['w','a','s','d'].includes(k)) keys.add(k);
    }
    function onKeyUp(event) {
      const k = event.key.length === 1 ? event.key.toLowerCase() : event.key;
      keys.delete(k);
    }
    document.addEventListener('keydown', onKeyDown, true);
    document.addEventListener('keyup', onKeyUp, true);
    root.addEventListener?.('blur', () => { keys.clear(); if (active) { paused = true; updateHud(); } });
    if (scene.morphStart?.addEventListener) scene.morphStart.addEventListener(() => { if (active) stop(false); });
    return {
      start,
      stop,
      setActive(requested) { return requested === true ? start() : stop(); },
      getState() {
        const terrain = groundHeight();
        return {
          enabled: active,
          paused,
          throttle,
          speedMetersPerSecond: speed,
          verticalSpeedMetersPerSecond: verticalSpeed,
          headingDegrees: (heading / rad + 360) % 360,
          pitchDegrees: pitch / rad,
          bankDegrees: bank / rad,
          longitude: lon,
          latitude: lat,
          height,
          terrainSampleAvailable: terrain !== null,
          aglMeters: terrain === null ? null : Math.max(0, height - terrain)
        };
      }
    };
  }

  root.ZEarthFlightSimulator = Object.freeze({ create });
})(typeof window !== 'undefined' ? window : globalThis);

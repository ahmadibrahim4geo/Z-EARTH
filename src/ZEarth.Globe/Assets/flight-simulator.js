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
    let mouseSteering = false;
    const mousePos = { x: 0, y: 0, active: false };

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
      const kmh = Math.round(speed * 3.6);
      const mach = (speed / 340.29).toFixed(1);
      const speedDisplay = kmh >= 10000 ? `${(kmh / 1000).toFixed(1)}k km/h (M${mach})` : `${kmh.toLocaleString()} km/h (M${mach})`;
      hud.textContent = `Z EARTH Flight Navigation · ${paused ? 'PAUSED' : 'ACTIVE'} · CAMERA MODE / NOT AIRCRAFT SIMULATION\n` +
        `${speedDisplay} · ${altitude} · THR ${Math.round(throttle * 100)}% · Mouse: ${mouseSteering ? 'ON' : 'OFF'} [M]\n` +
        `Heading ${Math.round((heading / rad + 360) % 360)}° · Pitch ${Math.round(pitch / rad)}° · Bank ${Math.round(bank / rad)}° · VS ${Math.round(verticalSpeed)} m/s\n` +
        'W/↑: climb · S/↓: dive · A/←: left · D/→: right · +/-/E/Q: speed · Shift: turbo · M: mouse · Space/P: pause · Esc: exit';
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
        background: 'rgba(11, 18, 32, 0.88)', color: '#FACC15',
        font: 'bold 12px "Consolas", monospace', padding: '10px 18px', zIndex: '9998',
        border: '1.5px solid #FACC15', borderRadius: '8px',
        boxShadow: '0 4px 20px rgba(0,0,0,0.6)',
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
      mouseSteering = false;
      mousePos.active = false;
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
        let rollInput = Number(keys.has('ArrowRight') || keys.has('d')) -
          Number(keys.has('ArrowLeft') || keys.has('a'));
        // User specification: Up Arrow & W raise camera UP (+1); Down Arrow & S lower camera DOWN (-1)
        let pitchInput = Number(keys.has('ArrowUp') || keys.has('w')) -
          Number(keys.has('ArrowDown') || keys.has('s'));

        if (mouseSteering && mousePos.active) {
          const dist = Math.hypot(mousePos.x, mousePos.y);
          const deadzone = 0.05; // 5% center deadzone
          if (dist > deadzone) {
            const scale = (dist - deadzone) / (1 - deadzone);
            const mRoll = (mousePos.x / dist) * scale;
            // Moving mouse forward (negative mousePos.y) raises camera UP
            // Moving mouse backward (positive mousePos.y) lowers camera DOWN
            const mPitch = -(mousePos.y / dist) * scale;
            rollInput = bound(rollInput + mRoll, -1, 1);
            pitchInput = bound(pitchInput + mPitch, -1, 1);
          }
        }

        bank += (rollInput * 45 * rad - bank) * Math.min(1, dt * 4.5);
        pitch = bound(pitch + pitchInput * dt * 0.95, -75 * rad, 75 * rad);
        heading = ((heading + bank * dt * (0.85 + 1.15 * Math.min(throttle, 2.5)) + rollInput * dt * 0.35 + Math.PI * 4) % (Math.PI * 2));

        // Fast, scalable speed with altitude scaling and optional Shift boost
        const altFactor = Math.max(1.0, Math.pow(height / 1000.0, 0.70));
        const boost = keys.has('turbo_boost') ? 2.5 : 1.0;
        speed = (60 + throttle * 340) * altFactor * boost;

        const distance = speed * Math.cos(pitch) * dt;
        const nextLat = bound(lat + Math.cos(heading) * distance / 111132, -85, 85);
        const denom = 111320 * Math.max(0.10, Math.cos(nextLat * rad));
        lon = ((lon + Math.sin(heading) * distance / denom + 540) % 360) - 180;
        lat = nextLat;
        const previousHeight = height;
        height += speed * Math.sin(pitch) * dt;
        const ground = groundHeight();
        height = bound(Math.max(height, (ground ?? 0) + 50), 50, 250000);
        verticalSpeed = (height - previousHeight) / dt;
        renderCamera();
      } else if (paused) {
        verticalSpeed = 0;
      }
      updateHud();
      rafId = root.requestAnimationFrame(step);
    }
    function start(options = {}) {
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
      throttle = 1.0;
      height = Math.max(1600, (groundHeight() ?? 0) + 600);
      mouseSteering = options?.mouseSteering === true;
      mousePos.active = false;
      active = true; paused = false; lastFrame = 0;
      ensureHud(); renderCamera(); updateHud();
      try { viewer.canvas?.focus?.(); } catch {}
      rafId = root.requestAnimationFrame(step);
      onChange({ enabled: true });
      return { enabled: true, mode: 'basic-navigation', liveTerrainCollision: groundHeight() !== null, mouseSteering };
    }
    function normalizeCode(event) {
      const code = event?.code || '';
      const rawKey = event?.key || '';
      const keyCode = event?.keyCode || event?.which || 0;
      const key = rawKey.length === 1 ? rawKey.toLowerCase() : rawKey;

      // Pitch UP: W or ArrowUp (Arabic W is 'ص')
      if (code === 'KeyW' || key === 'w' || key === 'ص' || keyCode === 87) return 'w';
      if (code === 'ArrowUp' || key === 'arrowup' || key === 'ArrowUp' || keyCode === 38) return 'ArrowUp';

      // Pitch DOWN: S or ArrowDown (Arabic S is 'س')
      if (code === 'KeyS' || key === 's' || key === 'س' || keyCode === 83) return 's';
      if (code === 'ArrowDown' || key === 'arrowdown' || key === 'ArrowDown' || keyCode === 40) return 'ArrowDown';

      // Turn LEFT: A or ArrowLeft (Arabic A is 'ش')
      if (code === 'KeyA' || key === 'a' || key === 'ش' || keyCode === 65) return 'a';
      if (code === 'ArrowLeft' || key === 'arrowleft' || key === 'ArrowLeft' || keyCode === 37) return 'ArrowLeft';

      // Turn RIGHT: D or ArrowRight (Arabic D is 'ي')
      if (code === 'KeyD' || key === 'd' || key === 'ي' || keyCode === 68) return 'd';
      if (code === 'ArrowRight' || key === 'arrowright' || key === 'ArrowRight' || keyCode === 39) return 'ArrowRight';

      // Speed UP / Throttle: E, +, =, PageUp (Arabic E is 'ث')
      if (code === 'KeyE' || key === 'e' || key === 'ث' || keyCode === 69 ||
          code === 'Equal' || code === 'NumpadAdd' || key === '+' || key === '=' ||
          code === 'PageUp' || key === 'PageUp' || keyCode === 33) return 'speed_up';

      // Speed DOWN / Decelerate: Q, -, PageDown (Arabic Q is 'ض')
      if (code === 'KeyQ' || key === 'q' || key === 'ض' || keyCode === 81 ||
          code === 'Minus' || code === 'NumpadSubtract' || key === '-' || key === '_' ||
          code === 'PageDown' || key === 'PageDown' || keyCode === 34) return 'speed_down';

      // Turbo Boost: Shift
      if (code === 'ShiftLeft' || code === 'ShiftRight' || key === 'Shift' || keyCode === 16) return 'turbo_boost';

      // Pause: Space, P (Arabic P is 'ح')
      if (code === 'Space' || code === 'KeyP' || key === ' ' || key === 'p' || key === 'P' || key === 'ح' || keyCode === 32 || keyCode === 80) return 'pause';

      // Toggle Mouse: M (Arabic M is 'ة')
      if (code === 'KeyM' || key === 'm' || key === 'M' || key === 'ة' || keyCode === 77) return 'toggle_mouse';

      if (code === 'Escape' || key === 'Escape' || keyCode === 27) return 'escape';
      return null;
    }
    function onKeyDown(event) {
      if (!active || event.target?.matches?.('input,textarea,select,[contenteditable="true"]')) return;
      const action = normalizeCode(event);
      if (action) event.preventDefault?.();
      if (action === 'escape') { stop(); return; }
      if (action === 'pause') { if (!event.repeat) paused = !paused; updateHud(); return; }
      if (action === 'speed_up') { throttle = Math.max(0, throttle + 0.15); updateHud(); return; }
      if (action === 'speed_down') { throttle = Math.max(0.05, throttle - 0.15); updateHud(); return; }
      if (action === 'toggle_mouse') {
        if (!event.repeat) {
          mouseSteering = !mouseSteering;
          mousePos.active = false;
          updateHud();
        }
        return;
      }
      if (action) keys.add(action);
    }
    function onKeyUp(event) {
      const action = normalizeCode(event);
      if (action) keys.delete(action);
      if (event?.key) {
        const k = event.key.length === 1 ? event.key.toLowerCase() : event.key;
        keys.delete(k);
      }
    }
    function onMouseMove(event) {
      if (!active || !mouseSteering || paused) return;
      const canvas = viewer.canvas || (typeof document !== 'undefined' ? document.body : null);
      if (!canvas) return;
      const rect = typeof canvas.getBoundingClientRect === 'function'
        ? canvas.getBoundingClientRect()
        : { left: 0, top: 0, width: 1200, height: 800 };
      const cx = (rect.left || 0) + (rect.width || 1200) / 2;
      const cy = (rect.top || 0) + (rect.height || 800) / 2;
      const halfW = Math.max(100, (rect.width || 1200) / 2);
      const halfH = Math.max(100, (rect.height || 800) / 2);
      mousePos.x = bound((event.clientX - cx) / halfW, -1, 1);
      mousePos.y = bound((event.clientY - cy) / halfH, -1, 1);
      mousePos.active = true;
    }
    document.addEventListener('keydown', onKeyDown, true);
    document.addEventListener('keyup', onKeyUp, true);
    document.addEventListener('mousemove', onMouseMove, true);
    root.addEventListener?.('blur', () => { keys.clear(); if (active) { paused = true; updateHud(); } });
    if (scene.morphStart?.addEventListener) scene.morphStart.addEventListener(() => { if (active) stop(false); });
    return {
      start,
      stop,
      setActive(requested, options) { return requested === true ? start(options) : stop(); },
      setMouseSteering(requested) {
        mouseSteering = requested === true;
        mousePos.active = false;
        updateHud();
        return mouseSteering;
      },
      getState() {
        const terrain = groundHeight();
        return {
          enabled: active,
          paused,
          throttle,
          mouseSteering,
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

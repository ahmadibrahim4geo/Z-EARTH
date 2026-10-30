// Independent, frame-based zoom scheduler; the caller owns Cesium camera math.
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.ZEarthCameraZoom = factory();
})(typeof window !== 'undefined' ? window : globalThis, function () {
  'use strict';

  function createZoomAnimator({ requestFrame, cancelFrame, step, duration = 420 }) {
    let active = null;

    function cancel() {
      if (!active) return;
      const old = active;
      active = null;
      if (old.frame !== null) cancelFrame(old.frame);
      old.onCancel?.();
    }

    function start(inside, position, factor, onComplete, onCancel, onFailure) {
      cancel();
      if (!Number.isFinite(factor) || factor <= 0 || factor >= 1) {
        onFailure?.();
        return false;
      }
      const job = { frame: null, startTime: null, progress: 0, onCancel };
      active = job;
      const tick = time => {
        if (active !== job) return;
        if (job.startTime === null) job.startTime = time;
        const t = Math.max(0, Math.min(1, (time - job.startTime) / duration));
        const eased = 1 - (1 - t) * (1 - t);
        const delta = eased - job.progress;
        if (delta > 0) {
          // Multiplicative steps compose to the exact requested overall factor.
          const frameFactor = 1 - Math.pow(1 - factor, delta);
          try {
            if (step(inside, position, frameFactor) === false) {
              active = null;
              onFailure?.();
              return;
            }
          } catch (error) {
            active = null;
            onFailure?.(error);
            return;
          }
          job.progress = eased;
        }
        if (t >= 1) {
          active = null;
          onComplete?.();
        } else {
          job.frame = requestFrame(tick);
        }
      };
      job.frame = requestFrame(tick);
      return true;
    }
    return { start, cancel };
  }
  return { createZoomAnimator };
});

const test = require('node:test');
const assert = require('node:assert/strict');
const { createZoomAnimator } = require('../src/ZEarth.Globe/Assets/camera-zoom.js');

function fixture() {
  let nextId = 0;
  const frames = new Map();
  const steps = [];
  const animator = createZoomAnimator({
    requestFrame: callback => { const id = ++nextId; frames.set(id, callback); return id; },
    cancelFrame: id => frames.delete(id),
    step: (inside, position, factor) => { steps.push({ inside, position, factor }); return true; },
    duration: 400
  });
  const tick = time => { const batch = [...frames.values()]; frames.clear(); batch.forEach(callback => callback(time)); };
  return { animator, tick, frames, steps };
}

test('zoom is interpolated over frames while retaining the clicked position', () => {
  const f = fixture();
  const position = { x: 100, y: 200 };
  let complete = 0;
  f.animator.start(true, position, 0.48, () => complete++);
  assert.equal(f.steps.length, 0);
  f.tick(0); f.tick(100); f.tick(250); f.tick(400);
  assert.equal(complete, 1);
  assert.ok(f.steps.length >= 3);
  assert.ok(f.steps.every(s => s.position === position && s.inside && s.factor > 0 && s.factor < 0.48));
  const remaining = f.steps.reduce((product, s) => product * (1 - s.factor), 1);
  assert.ok(Math.abs(remaining - 0.52) < 1e-10);
  assert.equal(f.frames.size, 0);
});

test('superseding a zoom cancels the old request and only completes the new one', () => {
  const f = fixture();
  let cancelled = 0, completed = 0;
  f.animator.start(true, {}, 0.28, () => assert.fail('old zoom completed'), () => cancelled++);
  f.tick(0); f.tick(100);
  f.animator.start(false, {}, 0.28, () => completed++);
  assert.equal(cancelled, 1);
  f.tick(150); f.tick(550);
  assert.equal(completed, 1);
  assert.equal(f.frames.size, 0);
});

test('manual cancellation stops the animation exactly once', () => {
  const f = fixture();
  let cancelled = 0;
  f.animator.start(true, {}, 0.28, null, () => cancelled++);
  f.tick(0); f.animator.cancel(); f.animator.cancel(); f.tick(1000);
  assert.equal(cancelled, 1);
  assert.equal(f.steps.length, 0);
});

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

test('feature click displays imported attributes as text and ignores clicks while drawing', () => {
  const source = fs.readFileSync('src/ZEarth.Globe/Assets/cesium-engine.js', 'utf8');
  const block = source.split('  // Use only text nodes for imported attributes: KML/GeoJSON data is untrusted.')[1]
    .split('  function pickGroundAtScreenPosition')[0];
  let callback, picked;
  function element(tag) { return {tag, children: [], style: {}, offsetWidth: 250, offsetHeight: 140,
    setAttribute() {}, addEventListener() {}, append(...items) {this.children.push(...items);},
    appendChild(item) {this.children.push(item);}, replaceChildren() {this.children = [];} }; }
  const container = element('container');
  const document = {createElement: element, getElementById: () => container};
  const DrawingManager = {activeMode: null}, MeasurementManager = {activeMode: null};
  const viewer = {canvas: {clientWidth: 600, clientHeight: 400}, scene: {pick: () => picked}};
  const Cesium = {ScreenSpaceEventHandler: class {setInputAction(fn) {callback=fn;}}, ScreenSpaceEventType: {LEFT_CLICK: 1}};
  vm.runInNewContext(block, {document, viewer, Cesium, DrawingManager, MeasurementManager});
  const panel = container.children[0];
  picked = {id: {_zearthFeatureInfo: {name: 'محطة', type: 'polygon', attributes: {name:'<img src=x onerror=alert(1)>'}}}};
  callback({position: {x: 50, y: 80}});
  assert.equal(panel.hidden, false);
  assert.equal(panel.children[0].children[0].textContent, 'محطة');
  assert.equal(panel.children[1].children[0].children[1].children[1].textContent, '<img src=x onerror=alert(1)>');
  DrawingManager.activeMode = 'path';
  picked = null;
  callback({position: {x: 50, y: 80}});
  assert.equal(panel.hidden, false, 'drawing clicks must not change the panel');
  DrawingManager.activeMode = null;
  callback({position: {x: 50, y: 80}});
  assert.equal(panel.hidden, true);
});

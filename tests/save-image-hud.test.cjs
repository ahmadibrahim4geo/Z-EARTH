const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const root = path.join(__dirname, '..');
const html = fs.readFileSync(path.join(root, 'src/ZEarth.Globe/Assets/index.html'), 'utf8');
const css = fs.readFileSync(path.join(root, 'src/ZEarth.Globe/Assets/globe.css'), 'utf8');
const engine = fs.readFileSync(path.join(root, 'src/ZEarth.Globe/Assets/cesium-engine.js'), 'utf8');
const bridge = fs.readFileSync(path.join(root, 'src/ZEarth.Globe/GlobeBridge.cs'), 'utf8');
const vm = fs.readFileSync(path.join(root, 'src/ZEarth.App/ViewModels/MainViewModel.cs'), 'utf8');
const xaml = fs.readFileSync(path.join(root, 'src/ZEarth.App/Views/MainWindow.xaml'), 'utf8');

test('Google Earth Pro Save Image HUD and Overlays DOM structure', () => {
  assert.ok(html.includes('id="save-image-hud"'));
  assert.ok(html.includes('id="save-image-title-card"'));
  assert.ok(html.includes('id="save-image-legend-card"'));
  assert.ok(html.includes('id="save-image-compass-card"'));
  assert.ok(html.includes('id="save-image-scale-card"'));
  assert.ok(html.includes('id="btn-save-image-export"'));
  assert.ok(html.includes('id="btn-save-image-close"'));
});

test('Sunlight Controller draggable HUD and controls DOM structure', () => {
  assert.ok(html.includes('id="sunlight-hud"'));
  assert.ok(html.includes('id="sunlight-drag-handle"'));
  assert.ok(html.includes('id="sunlight-date-picker"'));
  assert.ok(html.includes('id="btn-sunlight-prev-day"'));
  assert.ok(html.includes('id="btn-sunlight-next-day"'));
  assert.ok(html.includes('id="sunlight-phase-pill"'));
});

test('Cesium engine SaveImageManager and draggable HUD contracts', () => {
  assert.ok(engine.includes('SaveImageManager'));
  assert.ok(engine.includes('saveImage.triggerExport'));
  assert.ok(engine.includes("case 'saveImage.toggle':"));
  assert.ok(engine.includes("case 'saveImage.show':"));
  assert.ok(engine.includes("case 'saveImage.hide':"));
  assert.ok(engine.includes('enableMovableSunlightHud'));
});

test('GlobeBridge SaveImage contract and events', () => {
  assert.ok(bridge.includes('event EventHandler<EventArgs>? SaveImageExportRequested;'));
  assert.ok(bridge.includes('ToggleSaveImageAsync'));
  assert.ok(bridge.includes('ShowSaveImageAsync'));
  assert.ok(bridge.includes('HideSaveImageAsync'));
});

test('WPF Ribbon buttons active state bindings for Clouds, Atmosphere, Sunlight, and Save Image', () => {
  assert.ok(vm.includes('CloudsButtonBackground'));
  assert.ok(vm.includes('CloudsButtonBorderBrush'));
  assert.ok(vm.includes('AtmosphereButtonBackground'));
  assert.ok(vm.includes('AtmosphereButtonBorderBrush'));
  assert.ok(vm.includes('SunlightButtonBackground'));
  assert.ok(vm.includes('SunlightButtonBorderBrush'));
  assert.ok(vm.includes('SaveImageButtonBackground'));
  assert.ok(vm.includes('SaveImageButtonBorderBrush'));

  assert.ok(xaml.includes('Background="{Binding CloudsButtonBackground}"'));
  assert.ok(xaml.includes('Background="{Binding AtmosphereButtonBackground}"'));
  assert.ok(xaml.includes('Background="{Binding SunlightButtonBackground}"'));
  assert.ok(xaml.includes('Background="{Binding SaveImageButtonBackground}"'));
});

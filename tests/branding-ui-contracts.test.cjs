const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.join(__dirname, '..');
const read = f => fs.readFileSync(path.join(root, f));

test('the public branding folder copies the exact EXE icon and approved PNG', () => {
  assert.deepEqual(read('assets/branding/zearth_icon.ico'), read('src/ZEarth.App/Assets/zearth_icon.ico'));
  assert.deepEqual(read('assets/branding/zearth_approved_icon.png'), read('src/ZEarth.App/Assets/zearth_approved_icon.png'));
});

test('Grid Options targets map grid configuration; format selector is a menu', () => {
  const xaml = read('src/ZEarth.App/Views/MainWindow.xaml').toString('utf8');
  const vm = read('src/ZEarth.App/ViewModels/MainViewModel.cs').toString('utf8');
  assert.match(xaml, /Command="{Binding GridOptionsCommand}"/);
  assert.equal((xaml.match(/Click="OnCoordinateFormatMenuClick"/g) || []).length, 2);
  assert.match(vm, /GridOptionsCommand = new RelayCommand/);
  assert.match(xaml, /Foreground="{Binding TerrainIconBrush}"/);
  assert.match(vm, /TerrainFallbackOccurred/);
});
test('both XAML themes use an orientation-aware scrollbar instead of 8-pixel vertical Track', () => {
  for (const theme of ['DarkTheme', 'LightTheme']) {
    const xaml = read('src/ZEarth.App/Themes/' + theme + '.xaml').toString('utf8');
    const scope = xaml.split('<!-- Restored WPF')[1].split('</Style>')[0];
    assert.match(scope, /MinWidth" Value="15"/);
    assert.match(scope, /MinHeight" Value="15"/);
    assert.doesNotMatch(scope, /<Track/);
  }
});
test('time-of-day and grid label format are bound end to end', () => {
  const bridge = read('src/ZEarth.Globe/GlobeBridge.cs').toString('utf8');
  const view = read('src/ZEarth.App/Views/OptionsDialog.xaml').toString('utf8');
  const engine = read('src/ZEarth.Globe/Assets/cesium-engine.js').toString('utf8');
  assert.match(bridge, /string\? dateUtc = null/);
  assert.match(view, /GridLabelFormatCombo/);
  assert.match(engine, /this\.config\.gridLabelFormat/);
  assert.match(engine, /candidate\.toISOString\(\)/);
});

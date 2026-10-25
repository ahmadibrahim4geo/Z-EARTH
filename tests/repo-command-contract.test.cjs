const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

const from = path => fs.readFileSync(path, 'utf8');
const vm = from('src/ZEarth.App/ViewModels/MainViewModel.cs');
const bridge = from('src/ZEarth.Globe/GlobeBridge.cs');
const js = from('src/ZEarth.Globe/Assets/cesium-engine.js');
const xaml = from('src/ZEarth.App/Views/MainWindow.xaml');
const host = from('src/ZEarth.App/Views/MainWindow.xaml.cs');
const config = from('src/ZEarth.App/Views/OptionsDialog.xaml.cs');
const settings = from('src/ZEarth.App/Services/SettingsService.cs');

test('every literal GlobeBridge request is supported by Cesium command receiver', () => {
  const sent = [...bridge.matchAll(/SendRequestAsync\("([^"]+)"/g)].map(m => m[1]);
  const received = new Set([...js.matchAll(/case ['"]([^'"]+)['"]:/g)].map(m => m[1]));
  assert.ok(sent.length > 40, 'failed to inspect bridge actions');
  const missing = [...new Set(sent)].filter(action => !received.has(action));
  assert.deepEqual(missing, [], 'bridge actions without Cesium handlers');
});

test('native direct command origins do not bypass the Cesium contract', () => {
  const sent = [...vm.matchAll(/action\s*=\s*"([^"]+)"/g)].map(m => m[1]);
  const received = new Set([...js.matchAll(/case ['"]([^'"]+)['"]:/g)].map(m => m[1]));
  assert.deepEqual(sent.filter(action => !received.has(action)), [],
    'native direct message has no Cesium receiver');
});

test('WPF ribbon commands declared on MainViewModel except attached template commands', () => {
  const bindings = [...xaml.matchAll(/Command="\{Binding (\w+)\b/g)].map(m => m[1]);
  const missing = [...new Set(bindings)].filter(name =>
    !['PlacementTarget', 'DataContext'].includes(name) &&
    !new RegExp('\\b' + name + '\\b').test(vm));
  assert.deepEqual(missing, []);
});

test('bridge host checks the source before parsing messages and never navigates away', () => {
  const handler = host.indexOf('WebMessageReceived += ');
  assert.ok(handler > 0);
  const check = host.indexOf('WebViewTrustPolicy.IsTrustedGlobeDocument(args.Source)', handler);
  const dispatch = host.indexOf('HandleWebMessage(msg)', handler);
  assert.ok(check > handler && dispatch > check);
  assert.match(host, /NavigationStarting \+=/);
  assert.match(host, /NewWindowRequested \+=/);
});

test('option-save applies updated values to domain before persistence', () => {
  assert.ok(config.indexOf('SettingsService.Instance.ApplyToDomain()') <
            config.indexOf('SettingsService.Instance.Save()'));
  const saveMethod = settings.split('public void SaveSettings()')[1].split('public void ApplyToDomain()')[0];
  assert.doesNotMatch(saveMethod, /SyncFromDomain\(/);
  assert.match(vm, /ApplyGlobeThemeFromOptionsAsync/);
});

test('all failing mutations return actual failed bridge responses', () => {
  for (const action of ['layers.add', 'layers.setVisibility', 'layers.setOpacity', 'layers.reorder', 'layers.setBorders']) {
    assert.match(js, new RegExp("case '" + action.replace('.', '\\.') + "':"));
  }
  assert.match(js, /if \(!ok\) respond\('failed'/);
  assert.match(js, /if \(res\?\.success === false\) respond\('failed'/);
});

test('saved scenes await globe mode and basemap acknowledgements', () => {
  assert.match(vm, /if \(!await _globeBridge\.SetSceneModeAsync\(sm\)\)/);
  assert.match(vm, /if \(!await _globeBridge\.SetBasemapAsync\(bmapId\)\)/);
});

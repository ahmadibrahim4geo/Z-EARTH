const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

test('missing terrain samples do not become a chart of unproven vertex zeroes', () => {
  const vm = fs.readFileSync('src/ZEarth.App/ViewModels/MainViewModel.cs', 'utf8');
  const start = vm.indexOf('public async Task ExecuteElevationProfileAsync()');
  const end = vm.indexOf('public void ExecuteShowOptions()', start);
  const method = vm.slice(start, end);
  assert.ok(start >= 0 && end > start);
  assert.match(method, /SampleTerrainHeightsAsync/);
  assert.match(method, /No elevation profile was generated/);
  assert.doesNotMatch(method, /new Views\.ElevationProfileDialog\(vertices\)/);
  const dialog = fs.readFileSync('src/ZEarth.App/Views/ElevationProfileDialog.xaml.cs', 'utf8');
  const noData = dialog.slice(dialog.indexOf('if (_samples.Count < 2)'), dialog.indexOf('double totalDist'));
  assert.match(noData, /StatMinElevText\.Text = StatMaxElevText\.Text = "—"/);
  assert.match(noData, /StatDeltaZText\.Text = StatAvgSlopeText\.Text = StatMaxSlopeText\.Text = "—"/);
});

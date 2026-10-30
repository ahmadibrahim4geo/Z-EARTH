const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.join(__dirname, '..');
const xaml = fs.readFileSync(path.join(root, 'src/ZEarth.App/Views/MainWindow.xaml'), 'utf8');
const viewModel = fs.readFileSync(path.join(root, 'src/ZEarth.App/ViewModels/MainViewModel.cs'), 'utf8');
const windowCode = fs.readFileSync(path.join(root, 'src/ZEarth.App/Views/MainWindow.xaml.cs'), 'utf8');

assert.ok(xaml.includes('Command="{Binding TakeScreenshotCommand}"'));
assert.ok(xaml.includes('ToolTip="{Binding ScreenshotTooltip}"'));
assert.ok(xaml.includes('Text="{Binding ScreenshotLabel}"'));
assert.ok(!xaml.includes('Export high-resolution map snapshot to file'));
assert.ok(viewModel.includes('current on-screen resolution'));
assert.ok(viewModel.includes('بدقة العرض الحالية'));
assert.ok(windowCode.includes('CoreWebView2.CapturePreviewAsync'));
assert.ok(windowCode.includes('The captured PNG is empty.'));

console.log('Screenshot source contract PASS (current viewport PNG; no high-resolution claim)');

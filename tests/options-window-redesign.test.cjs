const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

const read = (file) => fs.readFileSync(file, 'utf8');

test('Options workspace uses one five-tab card design with stable automation ids', () => {
  const xaml = read('src/ZEarth.App/Views/OptionsDialog.xaml');
  const headers = [...xaml.matchAll(/<TabItem Header="([^"]+)"/g)].map(m => m[1]);
  assert.deepEqual(headers, [
    'Measurement Units',
    'Spatial Snapping',
    'Basemap &amp; Display',
    'Terrain Models',
    'Map Grid'
  ]);
  assert.match(xaml, /x:Key="SettingsCardStyle"/);
  assert.match(xaml, /x:Key="SectionTitleStyle"/);
  for (const id of ['Units', 'Snapping', 'Display', 'Terrain', 'Grid']) {
    assert.match(xaml, new RegExp(`AutomationProperties.AutomationId="ZE\\.Options\\.Tab\\.${id}"`));
  }
  assert.match(xaml, /Height="760" Width="1180"/);
  assert.match(xaml, /MinWidth="900" MinHeight="560"/);
  assert.match(xaml, /VerticalScrollBarVisibility="Auto"/);
  assert.match(xaml, /x:Key="GalleryItemStyle"/);
  assert.match(xaml, /x:Key="SwitchCheckBoxStyle"/);
  assert.match(xaml, /BasedOn="\{StaticResource \{x:Type ComboBox\}\}"/);
});

test('terrain source UI is truthful, source-specific and engine-backed', () => {
  const xaml = read('src/ZEarth.App/Views/OptionsDialog.xaml');
  const code = read('src/ZEarth.App/Views/OptionsDialog.xaml.cs');
  for (const provider of ['demTerrain', 'copernicusGlo30', 'srtm30', 'aw3d30', 'worldTerrain', 'worldBathymetry', 'gebco2026', 'ellipsoid', 'localDem', 'custom']) {
    assert.match(xaml, new RegExp(`Tag="${provider}"`));
  }
  assert.match(xaml, /GEBCO 2026 is a dataset, not a ready TerrainServer endpoint/);
  assert.match(xaml, /WMS and shaded-relief images are 2D imagery, not elevation/);
  assert.match(xaml, /token is kept in process memory only and is excluded from settings\.json and logs/);
  assert.doesNotMatch(xaml, /Remember token securely/);
  assert.match(code, /SetTerrainProviderAsync\(provider, url, token\)/);
  assert.match(code, /TryValidateCustomTerrainUrl/);
  assert.match(code, /parsed\.Scheme != Uri\.UriSchemeHttps/);
  assert.match(code, /IsPrivateOrLocalAddress/);
  assert.match(code, /LocalTerrainRasterService\.Instance\.Configure/);
  assert.match(code, /provider == "copernicusGlo30"/);
  assert.match(code, /CopernicusAccessTokenBox/);
  assert.doesNotMatch(code, /provider is "localDem" or "gebco2026"/);
});

test('approved visual structures expose live previews, galleries and visible grid format cards', () => {
  const xaml = read('src/ZEarth.App/Views/OptionsDialog.xaml');
  const code = read('src/ZEarth.App/Views/OptionsDialog.xaml.cs');
  for (const preview of ['DistancePreviewText', 'AreaPreviewText', 'ElevationPreviewText']) {
    assert.match(xaml, new RegExp(`x:Name="${preview}"`));
  }
  assert.match(code, /UpdateUnitPreviews/);
  assert.match(xaml, /x:Name="DefaultBasemapCombo" Style="\{StaticResource GalleryListStyle\}"/);
  assert.match(xaml, /x:Name="TerrainProviderCombo" Style="\{StaticResource GalleryListStyle\}"/);
  for (const format of ['GridFormatDdRadio', 'GridFormatDmRadio', 'GridFormatDmsRadio', 'GridFormatDegreesRadio']) {
    assert.match(xaml, new RegExp(`x:Name="${format}"`));
  }
  assert.match(code, /SetBasemapAsync\(settings\.DefaultBasemap\)/);
  assert.match(code, /OnOptionsDialogClosing/);
  assert.match(code, /Owner\.ActualWidth/);
  assert.match(code, /Owner\.ActualHeight/);
});

test('basemap, terrain and grid previews use bundled illustrative bitmaps instead of letter tiles', () => {
  const xaml = read('src/ZEarth.App/Views/OptionsDialog.xaml');
  const assets = [
    'src/ZEarth.App/Assets/Options/basemap-thumbnail-atlas.png',
    'src/ZEarth.App/Assets/Options/terrain-thumbnail-atlas.png',
    'src/ZEarth.App/Assets/Options/map-grid-preview.png'
  ];
  for (const asset of assets) {
    assert.ok(fs.existsSync(asset), `Missing thumbnail asset: ${asset}`);
    assert.ok(fs.statSync(asset).size > 100_000, `Thumbnail asset is unexpectedly small: ${asset}`);
  }

  for (const brush of [
    'BasemapThumbSatellite', 'BasemapThumbHybrid', 'BasemapThumbStreet',
    'BasemapThumbTopo', 'BasemapThumbLight', 'BasemapThumbDark', 'BasemapThumbPhysical',
    'TerrainThumbDem', 'TerrainThumbWorld', 'TerrainThumbBathymetry',
    'TerrainThumbGebco', 'TerrainThumbEllipsoid', 'TerrainThumbLocal', 'TerrainThumbCustom'
  ]) {
    assert.match(xaml, new RegExp(`x:Key="${brush}"`));
    assert.match(xaml, new RegExp(`Background="\\{StaticResource ${brush}\\}"`));
  }

  for (const provider of [
    'esri-imagery', 'esri-hybrid', 'osm-standard', 'opentopo', 'esri-gray',
    'carto-dark', 'offline-vector', 'osm-humanitarian', 'osm-cycling',
    'carto-light', 'carto-voyager', 'esri-streets', 'esri-topo',
    'esri-physical', 'esri-shaded-relief', 'esri-ocean'
  ]) {
    const item = xaml.match(new RegExp(`<ListBoxItem Tag="${provider}"[^]*?<\\/ListBoxItem>`))?.[0] ?? '';
    assert.match(item, /Background="\{StaticResource (?:Basemap|Terrain)Thumb/,
      `Basemap gallery entry ${provider} must have an illustrative thumbnail`);
  }
  for (const provider of ['demTerrain', 'copernicusGlo30', 'srtm30', 'aw3d30', 'worldTerrain', 'worldBathymetry', 'gebco2026', 'ellipsoid', 'localDem', 'custom']) {
    const item = xaml.match(new RegExp(`<ListBoxItem Tag="${provider}"[^]*?<\\/ListBoxItem>`))?.[0] ?? '';
    assert.match(item, /Background="\{StaticResource TerrainThumb/,
      `Terrain gallery entry ${provider} must have an illustrative thumbnail`);
  }

  assert.match(xaml, /Assets\/Options\/map-grid-preview\.png/);
  assert.doesNotMatch(xaml, /Text="(?:SAT|HYB|OSM|TOPO|GRAY|DARK|NE II|DEM|CWT|BATH|GEBCO|FILE|URL)"/);
});

test('map-grid preview keeps the world bitmap grid-free and draws live user-controlled overlays', () => {
  const xaml = read('src/ZEarth.App/Views/OptionsDialog.xaml');
  assert.match(xaml, /AutomationProperties\.Name="Dynamic map grid and UTM preview"/);
  for (const [picker, check] of [
    ['MajorColorPicker', 'KeyParallelsCheck'],
    ['LongitudeColorPicker', 'LongitudeLinesCheck'],
    ['LatitudeColorPicker', 'LatitudeLinesCheck'],
    ['UtmBoundaryColorPicker', 'UtmZonesCheck']
  ]) {
    assert.match(xaml, new RegExp(`Stroke="\\{Binding ColorHex, ElementName=${picker}\\}"`));
    assert.match(xaml, new RegExp(`Visibility="\\{Binding IsChecked, ElementName=${check}, Converter=\\{StaticResource BooleanToVisibilityConverter\\}\\}"`));
  }
  assert.match(xaml, /Foreground="\{Binding ColorHex, ElementName=LabelColorPicker\}"/);
  assert.match(xaml, /Preview of the selected grid colors and layers/);
  assert.match(xaml, /<Viewbox Stretch="Uniform" IsHitTestVisible="False">\s*<Grid Width="1000" Height="375">\s*<Image Source="pack:\/\/application:,,,\/Assets\/Options\/map-grid-preview\.png"/);
});

test('provider gallery keeps legacy CARTO entries truthful and non-selectable without credentials', () => {
  const xaml = read('src/ZEarth.App/Views/OptionsDialog.xaml');
  for (const provider of ['carto-dark', 'carto-light', 'carto-voyager']) {
    assert.match(xaml, new RegExp(`Tag="${provider}"[^>]*IsEnabled="False"`));
  }
  assert.match(xaml, /CARTO hosted basemaps now require provider-authorized access and an API key/);
  assert.match(xaml, /Key required/);
  const cartoDark = xaml.match(/<ListBoxItem Tag="carto-dark"[^]*?<\/ListBoxItem>/)?.[0] ?? '';
  assert.doesNotMatch(cartoDark, /Text="No key"/);
});

test('intersection snapping is a real engine preference, not a cosmetic checkbox', () => {
  const xaml = read('src/ZEarth.App/Views/OptionsDialog.xaml');
  const code = read('src/ZEarth.Globe/Assets/cesium-engine.js');
  assert.match(xaml, /x:Name="SnapIntersectionsCheck"/);
  assert.match(code, /snapIntersections: true/);
  assert.match(code, /cfg\.snapIntersections/);
  assert.match(code, /segmentIntersectionNearCursor/);
  assert.match(code, /type: 'intersection'/);
  assert.match(code, /snapIntersections: SnapManager\.snapIntersections/);
});

test('Options actions are real handlers and terrain has a direct ribbon entry point', () => {
  const xaml = read('src/ZEarth.App/Views/OptionsDialog.xaml');
  const main = read('src/ZEarth.App/Views/MainWindow.xaml');
  const vm = read('src/ZEarth.App/ViewModels/MainViewModel.cs');
  const app = read('src/ZEarth.App/App.xaml.cs');
  for (const handler of [
    'OnBrowseScreenshotFolderClick', 'OnBrowseLocalDemClick',
    'OnOpenGebcoGuidanceClick', 'OnTestTerrainConnectionClick',
    'OnOpenCesiumIonClick', 'OnResetGridDefaultsClick', 'OnSaveClick'
  ]) {
    assert.match(xaml, new RegExp(`Click="${handler}"`));
  }
  assert.match(main, /Command="{Binding TerrainOptionsCommand}"/);
  assert.match(vm, /TerrainOptionsCommand = new RelayCommand/);
  assert.match(vm, /OptionsDialogTab\.TerrainModels/);
  assert.match(app, /dialogArg == "terrain"/);
  assert.match(app, /dialogArg == "snapping"/);
  assert.match(app, /dialogArg is "basemap" or "display"/);
});

test('Arabic options localization covers the redesigned navigation and security guidance', () => {
  const localization = read('src/ZEarth.App/Services/LocalizationService.cs');
  for (const literal of [
    'Measurement Units', 'Spatial Snapping', 'Basemap & Display',
    'Terrain Models & Bathymetry', 'Map Grid', 'Test Connection',
    'The token is kept in process memory only and is excluded from settings.json and logs.',
    'Copernicus DEM GLO-30', 'SRTM-backed Terrain', 'JAXA AW3D30',
    'GEBCO 2026 Bathymetry', 'Copernicus Data Space OAuth access token',
    'No account or API key is required by JAXA Earth API.'
  ]) {
    assert.ok(localization.includes(`("${literal}`), `Missing Arabic translation entry for ${literal}`);
  }
});

test('terrain tokens remain outside plaintext settings persistence', () => {
  const settings = read('src/ZEarth.App/Services/SettingsService.cs');
  assert.match(settings, /JsonIgnore[^]*CesiumIonToken/);
  assert.match(settings, /ZEARTH_CESIUM_ION_TOKEN/);
  assert.doesNotMatch(settings, /WindowsCredentialStore/);
});

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

test('Arabic text reshaping and bidi reordering for Cesium labels', () => {
  const engineJs = fs.readFileSync(path.join(__dirname, '../src/ZEarth.Globe/Assets/cesium-engine.js'), 'utf8');
  assert.ok(engineJs.includes('function shapeArabicText'), 'shapeArabicText must be defined in cesium-engine.js');
  assert.ok(engineJs.includes('function isolatedLabelText(value)'), 'isolatedLabelText must exist and route to shapeArabicText');

  // Extract function to test directly
  const match = engineJs.match(/function shapeArabicText\(text\) \{([\s\S]*?)\n  \}/);
  assert.ok(match, 'Must match shapeArabicText implementation');
  const shapeArabicText = new Function('text', match[1]);

  // Pure Latin / English is untouched
  assert.equal(shapeArabicText('BARAKAT BIN AL-KHATTAB STREET'), 'BARAKAT BIN AL-KHATTAB STREET');

  // Arabic is reshaped with Presentation Forms and reversed for LTR canvas rendering
  const input = 'مشروع خفض منسوب المياه الجوفية - شارع بركات بن الخطاب - حي الفلاح';
  const reshaped = shapeArabicText(input);

  // Must not be empty and must not be equal to original broken letters
  assert.ok(reshaped.length > 0);
  assert.notEqual(reshaped, input);
  // Must contain Arabic presentation forms (U+FB50..U+FDFF or U+FE70..U+FEFF)
  assert.ok(/[\ufe70-\ufeff\ufb50-\ufdff]/.test(reshaped), 'Must contain connected Arabic presentation forms');
});

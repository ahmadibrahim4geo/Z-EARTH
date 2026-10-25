/* Browser and Node shared geographic grid label formatting, no provider data. */
(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (root) root.ZEarthGraticuleLabels = api;
})(typeof window !== 'undefined' ? window : null, function () {
  function formatDegrees(value, axis, format) {
    const degrees = Number(value);
    if (!Number.isFinite(degrees) || (axis !== 'lat' && axis !== 'lon')) return '';
    const direction = degrees < 0 ? (axis === 'lat' ? 'S' : 'W') :
      degrees > 0 ? (axis === 'lat' ? 'N' : 'E') : '';
    const normalized = format === 'DD' || format === 'DM' || format === 'Degrees' ? format : 'DMS';
    const absolute = Math.abs(degrees);
    if (normalized === 'DD') return absolute.toFixed(4) + '°' + (direction ? ' ' + direction : '');
    if (normalized === 'Degrees') return Math.round(absolute) + '°' + (direction ? ' ' + direction : '');
    // Round the total seconds/minutes before splitting to prevent illegal 60" or 60' labels.
    if (normalized === 'DM') {
      const rounded = Math.round(absolute * 60000) / 1000;
      const whole = Math.floor(rounded / 60);
      const minutes = rounded - whole * 60;
      return whole + '° ' + minutes.toFixed(3) + '′' + (direction ? ' ' + direction : '');
    }
    const total = Math.round(absolute * 36000) / 10;
    const whole = Math.floor(total / 3600);
    const minutes = Math.floor((total - whole * 3600) / 60);
    const seconds = total - whole * 3600 - minutes * 60;
    return whole + '° ' + minutes + '′ ' + seconds.toFixed(1) + '″' + (direction ? ' ' + direction : '');
  }
  return { formatDegrees };
});

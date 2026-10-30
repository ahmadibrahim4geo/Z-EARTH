/* Browser and Node shared geographic grid label formatting, no provider data.
 *
 * This is the single formatter for every rendered coordinate-grid label.
 * Rules:
 *  - whole values never show zero minutes/seconds or trailing decimal zeros;
 *  - rounding happens on the total minutes/seconds before splitting so labels
 *    such as 9°59′60″ are impossible;
 *  - ±180° and 0° do not get a contradictory hemisphere letter;
 *  - the status bar uses its own formatter and must stay independent.
 */
(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (root) root.ZEarthGraticuleLabels = api;
})(typeof window !== 'undefined' ? window : null, function () {
  const FORMATS = ['Degrees', 'DM', 'DMS', 'DD'];

  function normalizeFormat(format) {
    return FORMATS.includes(format) ? format : 'DD';
  }

  function trimFixed(value, decimals) {
    let text = value.toFixed(decimals);
    if (text.indexOf('.') >= 0) text = text.replace(/0+$/, '').replace(/\.$/, '');
    return text;
  }

  function hemisphere(degrees, axis) {
    // 0 and ±180 are cardinal boundaries; a letter would contradict the value.
    if (!Number.isFinite(degrees) || degrees === 0 || Math.abs(degrees) === 180) return '';
    return degrees < 0 ? (axis === 'lat' ? 'S' : 'W') : (axis === 'lat' ? 'N' : 'E');
  }

  function withDirection(text, direction) {
    return direction ? text + ' ' + direction : text;
  }

  function formatDegrees(value, axis, format, intervalDegrees) {
    const degrees = Number(value);
    if (!Number.isFinite(degrees) || (axis !== 'lat' && axis !== 'lon')) return '';
    const normalized = normalizeFormat(format);
    const absolute = Math.abs(degrees);
    const direction = hemisphere(degrees, axis);
    const decimalsFor = (unitScale, minimum, maximum) => {
      if (!Number.isFinite(intervalDegrees) || intervalDegrees <= 0) return minimum;
      return Math.max(minimum, Math.min(maximum,
        Math.ceil(-Math.log10(intervalDegrees * unitScale))));
    };

    if (normalized === 'Degrees') {
      return withDirection(Math.round(absolute) + '°', direction);
    }

    if (normalized === 'DD') {
      // 4 decimal degrees ≈ 11 m; trim the meaningless zeros for clean labels.
      return withDirection(trimFixed(absolute, decimalsFor(1, 4, 7)) + '°', direction);
    }

    if (normalized === 'DM') {
      // Round the whole minute count first, then split.
      const precision = decimalsFor(60, 3, 6);
      const factor = 10 ** precision;
      const totalMinutes = Math.round(absolute * 60 * factor) / factor;
      const wholeDegrees = Math.floor(totalMinutes / 60);
      const minutes = totalMinutes - wholeDegrees * 60;
      const text = minutes === 0
        ? wholeDegrees + '°'
        : wholeDegrees + '° ' + trimFixed(minutes, precision) + '′';
      return withDirection(text, direction);
    }

    // DMS: round the total seconds first so 59.999″ becomes the next minute.
    const precision = decimalsFor(3600, 1, 5);
    const factor = 10 ** precision;
    let totalSeconds = Math.round(absolute * 3600 * factor) / factor;
    let wholeDegrees = Math.floor(totalSeconds / 3600);
    let remainder = totalSeconds - wholeDegrees * 3600;
    let minutes = Math.floor(remainder / 60);
    let seconds = remainder - minutes * 60;
    if (seconds >= 60 - 0.5 / factor) {
      seconds = 0;
      minutes += 1;
    }
    if (minutes >= 60) {
      minutes = 0;
      wholeDegrees += 1;
    }
    let text = wholeDegrees + '°';
    if (minutes > 0 || seconds > 0) text += ' ' + minutes + '′';
    if (seconds > 0) text += ' ' + trimFixed(seconds, precision) + '″';
    return withDirection(text, direction);
  }

  return { formatDegrees, normalizeFormat, formats: FORMATS.slice() };
});

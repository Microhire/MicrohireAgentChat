(function (global) {
  'use strict';

  function formatIsoToDdMmYyyy(iso) {
    if (!iso || typeof iso !== 'string') return '';
    var m = iso.trim().match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (!m) return '';
    return m[3] + '-' + m[2] + '-' + m[1];
  }

  function parseDdMmYyyyToIso(text) {
    if (!text || typeof text !== 'string') return '';
    var t = text.trim().replace(/\//g, '-');
    var m = t.match(/^(\d{1,2})-(\d{1,2})-(\d{4})$/);
    if (!m) return '';
    var d = parseInt(m[1], 10);
    var mo = parseInt(m[2], 10);
    var y = parseInt(m[3], 10);
    if (mo < 1 || mo > 12 || d < 1 || d > 31) return '';
    var dt = new Date(Date.UTC(y, mo - 1, d));
    if (dt.getUTCFullYear() !== y || dt.getUTCMonth() !== mo - 1 || dt.getUTCDate() !== d) return '';
    return y.toString().padStart(4, '0') + '-' + mo.toString().padStart(2, '0') + '-' + d.toString().padStart(2, '0');
  }

  function compareIso(a, b) {
    if (!a || !b) return 0;
    return a < b ? -1 : a > b ? 1 : 0;
  }

  global.__islaDateFormat = {
    formatIsoToDdMmYyyy: formatIsoToDdMmYyyy,
    parseDdMmYyyyToIso: parseDdMmYyyyToIso,
    compareIso: compareIso
  };
})(typeof window !== 'undefined' ? window : this);

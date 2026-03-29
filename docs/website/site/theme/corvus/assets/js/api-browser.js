/**
 * API Browser - inline incremental search for the API Reference landing page.
 *
 * Lazy-loads the shared /search-index.json on first interaction and performs
 * dot-segment-aware prefix matching on entry titles.
 *
 * Matching rules:
 *   - The query is matched as a case-insensitive prefix against each "tail"
 *     of the title (split at dots). E.g. title "JsonElement.Parse" has tails
 *     "JsonElement.Parse" and "Parse".
 *   - After matching the prefix, if the next character in the title is a dot,
 *     the match is rejected. This prevents "JsonElement" from matching all of
 *     its members - the user must type past the dot ("JsonElement.P") to see
 *     members.
 *   - Types (class/struct/interface/enum/delegate) are always listed first.
 *
 * When the input is empty the default namespace card grid is shown.
 */
(function () {
  'use strict';

  var input = document.getElementById('api-browser-input');
  var statusEl = document.getElementById('api-browser-status');
  var resultsEl = document.getElementById('api-browser-results');
  var defaultContent = document.getElementById('api-browser-default');

  if (!input || !resultsEl || !defaultContent) return;

  var apiEntries = null;
  var indexLoading = false;

  // -- Index loading ----------------------------------------------------------

  async function ensureIndex() {
    if (apiEntries || indexLoading) return;
    indexLoading = true;
    try {
      var searchIndexUrl = input ? input.getAttribute('data-search-index') : null;
      if (!searchIndexUrl) searchIndexUrl = '/search-index.json';
      var resp = await fetch(searchIndexUrl);
      if (!resp.ok) throw new Error('HTTP ' + resp.status);
      var data = await resp.json();
      apiEntries = data
        .map(function (d) {
          return {
            url: d.url || d.Url || '',
            title: d.title || d.Title || '',
            description: d.description || d.Description || '',
            keywords: d.keywords || d.Keywords || '',
          };
        })
        .filter(function (d) { return d.url && d.url.startsWith('/api/'); });
    } catch (e) {
      console.warn('[api-browser] Failed to load search index:', e);
    } finally {
      indexLoading = false;
    }
  }

  // -- Helpers ----------------------------------------------------------------

  function escapeHtml(str) {
    var el = document.createElement('span');
    el.textContent = str;
    return el.innerHTML;
  }

  function extractNamespace(keywords) {
    if (!keywords) return '';
    var parts = keywords.split(' ');
    for (var i = parts.length - 1; i >= 0; i--) {
      if (parts[i].indexOf('.') !== -1) return parts[i];
    }
    return '';
  }

  function extractKind(keywords) {
    if (!keywords) return '';
    var kinds = ['class', 'struct', 'interface', 'enum', 'delegate'];
    var lower = keywords.toLowerCase();
    for (var k = 0; k < kinds.length; k++) {
      if (lower.indexOf(' ' + kinds[k] + ' ') !== -1 || lower.endsWith(' ' + kinds[k])) return kinds[k];
    }
    return '';
  }

  function isTypePage(entry) {
    return /\b(class|struct|interface|enum|delegate)\b/.test((entry.keywords || '').toLowerCase());
  }

  function truncate(str, max) {
    if (!str || str.length <= max) return str || '';
    return str.slice(0, max) + '\u2026';
  }

  // -- Dot-segment-aware prefix matching --------------------------------------

  /**
   * Returns true if the query matches the title under the dot-boundary rule.
   *
   * The query is tested as a case-insensitive prefix against every "tail" of
   * the title (starting at each dot boundary). A prefix match is rejected if
   * the character immediately after the matched prefix in the title is a dot.
   * This prevents "JsonElement" from matching "JsonElement.Parse" while still
   * allowing "JsonElement.P" to match it.
   */
  function matchesTitle(title, lowerQuery) {
    var lowerTitle = title.toLowerCase();
    var queryParts = lowerQuery.split('.');
    var titleParts = lowerTitle.split('.');

    // Try matching query segments against consecutive title segments
    // starting at each possible title segment position
    for (var start = 0; start <= titleParts.length - queryParts.length; start++) {
      var match = true;
      for (var j = 0; j < queryParts.length; j++) {
        if (!titleParts[start + j].startsWith(queryParts[j])) {
          match = false;
          break;
        }
      }
      if (match) {
        var lastQueryPart = queryParts[queryParts.length - 1];
        var lastTitlePart = titleParts[start + queryParts.length - 1];
        // Dot-boundary rule: if the last query segment exactly matches
        // a full title segment and there are more segments after, reject
        // (user must type past the dot to see members)
        if (lastQueryPart.length < lastTitlePart.length) {
          return true;
        }
        if (start + queryParts.length >= titleParts.length) {
          return true;
        }
      }
    }

    return false;
  }

  // -- Search -----------------------------------------------------------------

  function performSearch(query) {
    if (!apiEntries) return;

    var lowerQuery = query.toLowerCase();
    var maxResults = 50;
    var hits = [];

    for (var i = 0; i < apiEntries.length && hits.length < maxResults; i++) {
      var entry = apiEntries[i];
      if (matchesTitle(entry.title || '', lowerQuery)) {
        hits.push(entry);
      }
    }

    if (hits.length === 0) {
      statusEl.textContent = 'No results for \u201c' + query + '\u201d';
      resultsEl.innerHTML = '';
      return;
    }

    statusEl.textContent = 'Showing ' + hits.length + ' result' + (hits.length !== 1 ? 's' : '') + ' for \u201c' + query + '\u201d';

    // Sort: types first, then alphabetical by title
    hits.sort(function (a, b) {
      var aType = isTypePage(a) ? 0 : 1;
      var bType = isTypePage(b) ? 0 : 1;
      if (aType !== bType) return aType - bType;
      return (a.title || '').localeCompare(b.title || '');
    });

    // Group by namespace
    var groups = new Map();
    hits.forEach(function (m) {
      var ns = extractNamespace(m.keywords) || 'Other';
      if (!groups.has(ns)) groups.set(ns, []);
      groups.get(ns).push(m);
    });

    var html = '';
    groups.forEach(function (items, ns) {
      html += '<div class="api-browser__group">';
      html += '<h3 class="api-browser__group-title">' + escapeHtml(ns) + '</h3>';
      html += '<ul class="api-browser__list">';

      items.forEach(function (item) {
        var kind = extractKind(item.keywords);
        var badgeClass = kind ? ' api__badge--' + kind : '';
        var badgeLabel = kind ? kind.charAt(0).toUpperCase() + kind.slice(1) : '';
        var desc = truncate(item.description, 120);

        html += '<li class="api-browser__item">';
        html += '<a class="api-browser__item-link" href="' + escapeHtml(item.url) + '">';
        html += '<span class="api-browser__item-name">' + escapeHtml(item.title) + '</span>';
        if (badgeLabel) {
          html += ' <span class="api__badge' + badgeClass + '">' + escapeHtml(badgeLabel) + '</span>';
        }
        if (desc) {
          html += '<span class="api-browser__item-desc">' + escapeHtml(desc) + '</span>';
        }
        html += '</a></li>';
      });

      html += '</ul></div>';
    });

    resultsEl.innerHTML = html;
  }

  // -- Event wiring -----------------------------------------------------------

  var debounceTimer;
  input.addEventListener('input', function () {
    clearTimeout(debounceTimer);
    var q = input.value.trim();

    if (!q) {
      defaultContent.hidden = false;
      resultsEl.hidden = true;
      statusEl.textContent = '';
      resultsEl.innerHTML = '';
      return;
    }

    ensureIndex().then(function () {
      debounceTimer = setTimeout(function () {
        defaultContent.hidden = true;
        resultsEl.hidden = false;
        performSearch(q);
      }, 150);
    });
  });

  input.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
      input.value = '';
      input.dispatchEvent(new Event('input'));
      input.blur();
    }
  });
})();
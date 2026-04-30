/**
 * Sidebar Search — dropdown search for API documentation pages.
 *
 * Reuses the same dot-segment-aware prefix matching as api-browser.js.
 * Results appear in a dropdown overlay anchored to the search input at
 * the top of the sidebar, matching the .NET reference documentation UX.
 */
(function () {
  'use strict';

  var input = document.getElementById('sidebar-search-input');
  if (!input) return;

  var dropdown = document.getElementById('sidebar-search-dropdown');
  if (!dropdown) return;

  var apiEntries = null;
  var indexLoading = false;
  var selectedIdx = -1;

  // -- Index loading ----------------------------------------------------------

  async function ensureIndex() {
    if (apiEntries || indexLoading) return;
    indexLoading = true;
    try {
      // Determine versioned search index from current URL path
      var searchUrl = '/search-index.json';
      var loc = window.location.pathname;
      if (loc.indexOf('/api/v5/') === 0) searchUrl = '/api/v5/search-index.json';
      else if (loc.indexOf('/api/v4/') === 0) searchUrl = '/api/v4/search-index.json';
      var resp = await fetch(searchUrl);
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
      console.warn('[sidebar-search] Failed to load search index:', e);
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

  // -- Dot-segment-aware prefix matching --------------------------------------

  function matchesTitle(title, lowerQuery) {
    var lowerTitle = title.toLowerCase();
    var queryParts = lowerQuery.split('.');
    var titleParts = lowerTitle.split('.');

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

  // -- Search + render --------------------------------------------------------

  function performSearch(query) {
    if (!apiEntries) return;

    var lowerQuery = query.toLowerCase();
    var maxResults = 20;
    var hits = [];

    for (var i = 0; i < apiEntries.length && hits.length < maxResults; i++) {
      var entry = apiEntries[i];
      if (matchesTitle(entry.title || '', lowerQuery)) {
        hits.push(entry);
      }
    }

    if (hits.length === 0) {
      dropdown.innerHTML = '<div class="sidebar-search__empty">No results</div>';
      showDropdown();
      return;
    }

    // Sort: types first, then alphabetical
    hits.sort(function (a, b) {
      var aType = isTypePage(a) ? 0 : 1;
      var bType = isTypePage(b) ? 0 : 1;
      if (aType !== bType) return aType - bType;
      return (a.title || '').localeCompare(b.title || '');
    });

    var html = '<ul class="sidebar-search__list" role="listbox">';
    hits.forEach(function (item, idx) {
      var kind = extractKind(item.keywords);
      var badgeHtml = kind
        ? ' <span class="sidebar-search__badge sidebar-search__badge--' + kind + '">' + kind.charAt(0).toUpperCase() + '</span>'
        : '';
      html += '<li class="sidebar-search__item" role="option" data-idx="' + idx + '">';
      html += '<a class="sidebar-search__link" href="' + escapeHtml(item.url) + '">';
      html += '<span class="sidebar-search__name">' + escapeHtml(item.title) + '</span>';
      html += badgeHtml;
      html += '</a></li>';
    });
    html += '</ul>';

    dropdown.innerHTML = html;
    selectedIdx = -1;
    showDropdown();
  }

  // -- Dropdown visibility ----------------------------------------------------

  function showDropdown() {
    dropdown.hidden = false;
    dropdown.classList.add('is-visible');
  }

  function hideDropdown() {
    dropdown.hidden = true;
    dropdown.classList.remove('is-visible');
    selectedIdx = -1;
  }

  // -- Keyboard navigation ----------------------------------------------------

  function getItems() {
    return dropdown.querySelectorAll('.sidebar-search__item');
  }

  function highlightItem(idx) {
    var items = getItems();
    items.forEach(function (el) { el.classList.remove('is-highlighted'); });
    if (idx >= 0 && idx < items.length) {
      items[idx].classList.add('is-highlighted');
      items[idx].scrollIntoView({ block: 'nearest' });
    }
    selectedIdx = idx;
  }

  // -- Event wiring -----------------------------------------------------------

  var debounceTimer;

  input.addEventListener('input', function () {
    clearTimeout(debounceTimer);
    var q = input.value.trim();

    if (!q) {
      hideDropdown();
      dropdown.innerHTML = '';
      return;
    }

    ensureIndex().then(function () {
      debounceTimer = setTimeout(function () {
        performSearch(q);
      }, 120);
    });
  });

  input.addEventListener('keydown', function (e) {
    var items = getItems();
    var count = items.length;

    if (e.key === 'ArrowDown') {
      e.preventDefault();
      highlightItem(selectedIdx < count - 1 ? selectedIdx + 1 : 0);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      highlightItem(selectedIdx > 0 ? selectedIdx - 1 : count - 1);
    } else if (e.key === 'Enter') {
      e.preventDefault();
      if (selectedIdx >= 0 && selectedIdx < count) {
        var link = items[selectedIdx].querySelector('a');
        if (link) window.location.href = link.href;
      }
    } else if (e.key === 'Escape') {
      input.value = '';
      hideDropdown();
      input.blur();
    }
  });

  input.addEventListener('focus', function () {
    if (input.value.trim() && dropdown.innerHTML) {
      showDropdown();
    }
  });

  // Close dropdown when clicking outside
  document.addEventListener('click', function (e) {
    if (!e.target.closest('.sidebar-search')) {
      hideDropdown();
    }
  });
})();

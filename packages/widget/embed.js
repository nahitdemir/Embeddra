(function (window, document) {
  'use strict';

  var STYLE_ID = 'embeddra-widget-style';
  var DEFAULTS = {
    apiBaseUrl: window.EmbeddraSearchUrl || 'http://localhost:5222',
    apiKey: '',
    tenantId: '',
    container: null,
    placeholder: 'Search products',
    minChars: 2,
    debounceMs: 250,
    maxResults: 8,
    idleText: 'Type to search',
    emptyText: 'No results',
    loadingText: 'Searching...',
    errorText: 'Search failed',
    onSelect: null,
    trackClicks: true
  };

  function init(options) {
    var opts = merge(DEFAULTS, options || {});
    var host = resolveContainer(opts.container);
    if (!host) {
      console.error('[EmbeddraWidget] container not found');
      return null;
    }

    host.innerHTML = '';
    ensureStyles();

    var root = document.createElement('div');
    root.className = 'embeddra-widget';
    root.setAttribute('data-embeddra-widget', 'true');

    var label = document.createElement('label');
    label.className = 'embeddra-label';
    label.textContent = 'Search';

    var input = document.createElement('input');
    var inputId = uniqueId('embeddra-input');
    input.id = inputId;
    input.type = 'search';
    input.autocomplete = 'off';
    input.className = 'embeddra-input';
    input.placeholder = opts.placeholder;
    label.setAttribute('for', inputId);

    var status = document.createElement('div');
    status.className = 'embeddra-status';

    var list = document.createElement('ul');
    list.className = 'embeddra-results hidden';
    var listId = uniqueId('embeddra-list');
    list.id = listId;
    list.setAttribute('role', 'listbox');
    list.setAttribute('aria-live', 'polite');

    input.setAttribute('aria-controls', listId);

    root.appendChild(label);
    root.appendChild(input);
    root.appendChild(status);
    root.appendChild(list);
    host.appendChild(root);

    var debounceTimer = null;
    var activeController = null;
    var requestId = 0;
    var lastSearchId = null;

    setStatus(status, opts.idleText, 'idle');

    input.addEventListener('input', function () {
      var query = input.value.trim();
      if (debounceTimer) {
        window.clearTimeout(debounceTimer);
      }

      if (query.length < opts.minChars) {
        cancelActiveRequest();
        hideResults(list);
        setStatus(status, opts.idleText, 'idle');
        return;
      }

      debounceTimer = window.setTimeout(function () {
        runSearch(query);
      }, opts.debounceMs);
    });

    function runSearch(query) {
      if (!opts.apiBaseUrl) {
        setStatus(status, 'Missing apiBaseUrl', 'error');
        return;
      }

      if (!opts.tenantId) {
        setStatus(status, 'Missing tenantId', 'error');
        return;
      }

      var currentId = ++requestId;
      cancelActiveRequest();
      activeController = new window.AbortController();

      setStatus(status, opts.loadingText, 'loading');
      hideResults(list);

      var headers = {
        'Content-Type': 'application/json'
      };
      if (opts.apiKey) {
        headers['X-Api-Key'] = opts.apiKey;
      }
      headers['X-Tenant-Id'] = opts.tenantId;

      var payload = JSON.stringify({
        query: query,
        size: opts.maxResults
      });

      window.fetch(trimTrailingSlash(opts.apiBaseUrl) + '/search', {
        method: 'POST',
        headers: headers,
        body: payload,
        signal: activeController.signal
      })
        .then(function (response) {
          if (!response.ok) {
            throw new Error('Request failed with status ' + response.status);
          }
          return response.json();
        })
        .then(function (data) {
          if (currentId !== requestId) {
            return;
          }
          lastSearchId = data && (data.searchId || data.search_id) ? (data.searchId || data.search_id) : null;
          var items = extractResults(data);
          renderResults(list, items, opts, lastSearchId);
          if (items.length === 0) {
            setStatus(status, opts.emptyText, 'empty');
          } else {
            setStatus(status, '', 'ready');
          }
        })
        .catch(function (error) {
          if (error && error.name === 'AbortError') {
            return;
          }
          setStatus(status, opts.errorText, 'error');
        });
    }

    function cancelActiveRequest() {
      if (activeController) {
        activeController.abort();
        activeController = null;
      }
    }

    return {
      destroy: function () {
        cancelActiveRequest();
        host.innerHTML = '';
      },
      search: function (query) {
        input.value = query || '';
        runSearch(input.value.trim());
      }
    };
  }

  function extractResults(payload) {
    if (!payload) {
      return [];
    }

    if (Array.isArray(payload.results)) {
      return payload.results;
    }

    if (payload.hits && Array.isArray(payload.hits)) {
      return payload.hits;
    }

    if (payload.items && Array.isArray(payload.items)) {
      return payload.items;
    }

    return [];
  }

  function renderResults(list, items, opts, searchId) {
    list.innerHTML = '';
    if (!items || items.length === 0) {
      hideResults(list);
      return;
    }

    list.classList.remove('hidden');
    for (var i = 0; i < items.length; i++) {
      var item = items[i];
      var li = document.createElement('li');
      li.style.animationDelay = (i * 40) + 'ms';

      var button = document.createElement('button');
      button.type = 'button';
      button.className = 'embeddra-item';
      button.setAttribute('role', 'option');

      var text = getDisplayText(item);
      var meta = getMetaText(item);

      var title = document.createElement('div');
      title.className = 'embeddra-title';
      title.textContent = text;

      var metaLine = document.createElement('div');
      metaLine.className = 'embeddra-meta';
      metaLine.textContent = meta;
      if (!meta) {
        metaLine.style.display = 'none';
      }

      button.appendChild(title);
      button.appendChild(metaLine);
      button.addEventListener('click', function (selected) {
        return function () {
          var productId = getProductId(selected);
          if (opts.trackClicks && searchId && productId) {
            sendClick(opts, searchId, productId);
          }
          if (typeof opts.onSelect === 'function') {
            opts.onSelect(selected);
          }
        };
      }(item));

      li.appendChild(button);
      list.appendChild(li);
    }
  }

  function getDisplayText(item) {
    var source = item && (item.source || item._source) ? (item.source || item._source) : item || {};
    var text = source.name || source.title || source.product_id || source.id || item.id || item._id;
    if (!text) {
      return 'Untitled';
    }
    return String(text);
  }

  function getMetaText(item) {
    var source = item && (item.source || item._source) ? (item.source || item._source) : item || {};
    var parts = [];
    if (source.brand) {
      parts.push(source.brand);
    }
    if (source.category) {
      parts.push(source.category);
    }
    if (typeof source.price === 'number') {
      parts.push('$' + source.price);
    }
    return parts.join(' | ');
  }

  function getProductId(item) {
    var source = item && (item.source || item._source) ? (item.source || item._source) : item || {};
    return source.product_id || source.id || item.id || item._id || null;
  }

  function sendClick(opts, searchId, productId) {
    if (!opts.apiBaseUrl || !opts.tenantId) {
      return;
    }

    var headers = {
      'Content-Type': 'application/json'
    };
    if (opts.apiKey) {
      headers['X-Api-Key'] = opts.apiKey;
    }
    headers['X-Tenant-Id'] = opts.tenantId;

    var payload = JSON.stringify({
      searchId: searchId,
      productId: productId
    });

    window.fetch(trimTrailingSlash(opts.apiBaseUrl) + '/search:click', {
      method: 'POST',
      headers: headers,
      body: payload
    }).catch(function () {
      return;
    });
  }

  function setStatus(target, text, state) {
    target.textContent = text || '';
    target.setAttribute('data-state', state || 'idle');
    if (text) {
      target.classList.remove('hidden');
    } else {
      target.classList.add('hidden');
    }
  }

  function hideResults(list) {
    list.classList.add('hidden');
    list.innerHTML = '';
  }

  function trimTrailingSlash(url) {
    return url.replace(/\/+$/, '');
  }

  function resolveContainer(container) {
    if (!container) {
      return null;
    }
    if (typeof container === 'string') {
      return document.querySelector(container);
    }
    if (container && container.nodeType === 1) {
      return container;
    }
    return null;
  }

  function ensureStyles() {
    if (document.getElementById(STYLE_ID)) {
      return;
    }
    var style = document.createElement('style');
    style.id = STYLE_ID;
    style.type = 'text/css';
    style.textContent = [
      '.embeddra-widget{',
      '  --ew-bg:#f6f1e8;',
      '  --ew-surface:#ffffff;',
      '  --ew-border:#e2d6c3;',
      '  --ew-text:#1d1a16;',
      '  --ew-muted:#6e6150;',
      '  --ew-accent:#1a7a5b;',
      '  font-family:"Space Grotesk","Segoe UI","Helvetica Neue",sans-serif;',
      '  background:linear-gradient(180deg,var(--ew-bg),#ffffff);',
      '  border:1px solid var(--ew-border);',
      '  border-radius:14px;',
      '  padding:12px;',
      '  max-width:360px;',
      '  box-shadow:0 10px 28px rgba(33,24,12,0.12);',
      '  position:relative;',
      '}',
      '.embeddra-label{',
      '  display:block;',
      '  font-size:12px;',
      '  letter-spacing:0.08em;',
      '  text-transform:uppercase;',
      '  color:var(--ew-muted);',
      '  margin-bottom:6px;',
      '}',
      '.embeddra-input{',
      '  width:100%;',
      '  border:1px solid var(--ew-border);',
      '  border-radius:10px;',
      '  padding:10px 12px;',
      '  font-size:14px;',
      '  color:var(--ew-text);',
      '  background:var(--ew-surface);',
      '  outline:none;',
      '  transition:border-color 150ms ease;',
      '}',
      '.embeddra-input:focus{',
      '  border-color:var(--ew-accent);',
      '}',
      '.embeddra-status{',
      '  margin-top:8px;',
      '  font-size:12px;',
      '  color:var(--ew-muted);',
      '}',
      '.embeddra-status[data-state="loading"]{',
      '  color:var(--ew-accent);',
      '}',
      '.embeddra-status[data-state="error"]{',
      '  color:#b63b2e;',
      '}',
      '.embeddra-results{',
      '  list-style:none;',
      '  margin:10px 0 0;',
      '  padding:6px;',
      '  border:1px solid var(--ew-border);',
      '  border-radius:12px;',
      '  background:var(--ew-surface);',
      '  box-shadow:0 16px 30px rgba(24,18,12,0.14);',
      '  max-height:280px;',
      '  overflow-y:auto;',
      '}',
      '.embeddra-results.hidden{',
      '  display:none;',
      '}',
      '.embeddra-results li{',
      '  animation:embeddra-fade-in 180ms ease-out both;',
      '}',
      '.embeddra-item{',
      '  width:100%;',
      '  border:none;',
      '  background:transparent;',
      '  text-align:left;',
      '  padding:8px 10px;',
      '  border-radius:8px;',
      '  cursor:pointer;',
      '  color:var(--ew-text);',
      '}',
      '.embeddra-item:hover{',
      '  background:rgba(26,122,91,0.08);',
      '}',
      '.embeddra-title{',
      '  font-size:14px;',
      '  font-weight:600;',
      '}',
      '.embeddra-meta{',
      '  font-size:12px;',
      '  color:var(--ew-muted);',
      '  margin-top:2px;',
      '}',
      '.hidden{',
      '  display:none;',
      '}',
      '@keyframes embeddra-fade-in{',
      '  from{opacity:0;transform:translateY(6px);}',
      '  to{opacity:1;transform:translateY(0);}',
      '}'
    ].join('\n');
    document.head.appendChild(style);
  }

  function uniqueId(prefix) {
    return prefix + '-' + Math.random().toString(36).slice(2, 10);
  }

  function merge(base, override) {
    var result = {};
    var key;
    for (key in base) {
      if (Object.prototype.hasOwnProperty.call(base, key)) {
        result[key] = base[key];
      }
    }
    for (key in override) {
      if (Object.prototype.hasOwnProperty.call(override, key)) {
        result[key] = override[key];
      }
    }
    return result;
  }

  window.EmbeddraWidget = {
    init: init
  };
}(window, document));

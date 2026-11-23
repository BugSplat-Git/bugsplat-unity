(function () {
  if (typeof Module === 'undefined' || Module['BugSplatWebGL']) {
    return;
  }

  var maxLogLines = 100;
  var maxLogLineLength = 500;
  var keepaliveLimit = 60000;
  var config = null;
  var disabled = false;
  var pending = null;

  function format(value) {
    if (typeof value === 'string') {
      return value;
    }
    if (value instanceof Error) {
      return value.stack || String(value);
    }
    if (value !== null && typeof value === 'object') {
      var name = Object.prototype.toString.call(value).slice(8, -1);
      return typeof value.length === 'number' ? name + '(' + value.length + ')' : '[' + name + ']';
    }
    return String(value);
  }

  function pushLog(logs, level, args) {
    var parts = [];
    var budget = maxLogLineLength;
    for (var i = 0; i < args.length && budget > 0; i++) {
      var part;
      try {
        part = format(args[i]);
      } catch (e) {
        part = '[unreadable]';
      }
      if (part.length > budget) {
        part = part.substring(0, budget) + '...';
      }
      budget -= part.length + 1;
      parts.push(part);
    }
    logs.push(new Date().toISOString() + ' [' + level + '] ' + parts.join(' ').replace(/\s+$/, ''));
    if (logs.length > maxLogLines) {
      logs.shift();
    }
  }

  function hookConsole(logs) {
    if (typeof console === 'undefined') {
      return;
    }
    ['log', 'info', 'warn', 'error', 'debug'].forEach(function (level) {
      var original = console[level];
      if (typeof original !== 'function') {
        return;
      }
      console[level] = function () {
        try {
          pushLog(logs, level, arguments);
        } catch (e) {}
        return original.apply(console, arguments);
      };
    });
  }

  function describe(error, fallbackMessage) {
    var message = '';
    var stack = '';
    if (error && typeof error === 'object') {
      message = error.name && error.message ? error.name + ': ' + error.message : String(error);
      stack = error.stack ? String(error.stack) : '';
    } else if (error !== undefined && error !== null) {
      message = String(error);
    }
    if (!message) {
      message = fallbackMessage || 'Unknown error';
    }
    return stack && stack.indexOf(message) === 0 ? stack : (stack ? message + '\n' + stack : message);
  }

  function isWasmFailure(source, error, text) {
    if (source === 'abort') {
      return true;
    }
    if (typeof WebAssembly !== 'undefined' && WebAssembly.RuntimeError && error instanceof WebAssembly.RuntimeError) {
      return true;
    }
    return /wasm-function\[|wasm:\/\/|^RuntimeError|^abort\(|^Aborted\(/.test(text);
  }

  // One console hook, one pair of window listeners and one report per page, however many players
  // the page creates. A failure goes to the player whose wasm is in the stack, else the oldest.
  var page = window['__BugSplatWebGL'];
  if (!page) {
    page = window['__BugSplatWebGL'] = { 'logs': [], 'players': [], 'reported': false };
    hookConsole(page['logs']);
    var dispatch = function (source, error, fallbackMessage) {
      var text = describe(error, fallbackMessage);
      if (!page['players'].length || !isWasmFailure(source, error, text)) {
        return;
      }
      var owner = page['players'][0];
      for (var i = 0; i < page['players'].length; i++) {
        if (page['players'][i]['codeUrl'] && text.indexOf(page['players'][i]['codeUrl']) >= 0) {
          owner = page['players'][i];
          break;
        }
      }
      owner['capture'](source, text);
    };
    window.addEventListener('error', function (e) {
      dispatch('error', e.error, e.message);
    }, true);
    window.addEventListener('unhandledrejection', function (e) {
      dispatch('unhandledrejection', e.reason);
    }, true);
  }

  function heapBytes() {
    try {
      if (typeof HEAPU8 !== 'undefined' && HEAPU8) {
        return HEAPU8.length;
      }
    } catch (e) {}
    return 0;
  }

  function capture(source, text) {
    if (disabled || page['reported'] || pending) {
      return;
    }

    var details = {
      webglErrorSource: source,
      webglHeapBytes: String(heapBytes()),
      userAgent: typeof navigator !== 'undefined' ? navigator.userAgent : ''
    };
    if (typeof performance !== 'undefined' && performance.memory) {
      details.jsHeapUsedBytes = String(performance.memory.usedJSHeapSize);
    }
    pending = { callstack: text, details: details, log: page['logs'].join('\n') };
    flush();
  }

  function flush() {
    var current = config || Module['bugsplat'];
    if (!pending || page['reported'] || disabled || !current || !current['database']) {
      return;
    }
    page['reported'] = true;
    var report = pending;
    pending = null;

    var attributes = {};
    var configured = current['attributes'] || {};
    for (var name in configured) {
      if (Object.prototype.hasOwnProperty.call(configured, name)) {
        attributes[name] = configured[name];
      }
    }
    for (var detail in report.details) {
      attributes[detail] = report.details[detail];
    }

    var fields = [
      ['database', current['database']],
      ['appName', current['application'] || ''],
      ['appVersion', current['version'] || ''],
      ['appKey', current['key'] || ''],
      ['user', current['user'] || ''],
      ['email', current['email'] || ''],
      ['description', current['description'] || ''],
      ['notes', current['notes'] || ''],
      ['callstack', report.callstack],
      ['crashTypeId', '39'],
      ['attributes', JSON.stringify(attributes)]
    ];
    var log = new Blob([report.log], { type: 'text/plain' });
    var body = new FormData();
    var bytes = log.size;
    for (var i = 0; i < fields.length; i++) {
      body.append(fields[i][0], fields[i][1]);
      bytes += new Blob([fields[i][1]]).size;
    }
    body.append('BugSplatWebGL.log', log, 'BugSplatWebGL.log');

    var url = 'https://' + current['database'] + '.bugsplat.com/post/dotnetstandard/';
    var keepalive = bytes < keepaliveLimit;
    try {
      fetch(url, { method: 'POST', body: body, keepalive: keepalive })
        .catch(function (e) {
          if (!keepalive) {
            throw e;
          }
          return fetch(url, { method: 'POST', body: body });
        })
        .then(function (response) { return response.text().then(function (text) { return response.status + ' ' + text; }); })
        .then(function (text) { console.log('BugSplat: WebAssembly crash posted ' + text); })
        .catch(function (e) { console.error('BugSplat: WebAssembly crash post failed ' + e); });
    } catch (e) {}
  }

  var abortHandler = Module['abortHandler'];
  Module['abortHandler'] = function (message) {
    capture('abort', String(message));
    return abortHandler ? abortHandler.apply(this, arguments) : false;
  };

  Module['BugSplatWebGL'] = {
    configure: function (value) {
      config = value;
      disabled = false;
      setTimeout(flush, 0);
    },
    disable: function () {
      disabled = true;
      pending = null;
    }
  };

  var player = { 'codeUrl': '', 'capture': capture };
  try {
    if (Module['codeUrl']) {
      player['codeUrl'] = new URL(Module['codeUrl'], document.baseURI).href;
    }
  } catch (e) {}
  page['players'].push(player);
  if (Module['deinitializers'] && typeof Module['deinitializers'].push === 'function') {
    Module['deinitializers'].push(function () {
      disabled = true;
      pending = null;
      var index = page['players'].indexOf(player);
      if (index >= 0) {
        page['players'].splice(index, 1);
      }
    });
  }

  flush();
})();

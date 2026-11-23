(function () {
  if (typeof Module === 'undefined' || Module['BugSplatWebGL']) {
    return;
  }

  var maxLogLines = 100;
  var maxLogLineLength = 500;
  var keepaliveLimit = 60000;
  var logs = [];
  var config = null;
  var disabled = false;
  var pending = null;
  var reported = false;

  function pushLog(level, args) {
    var parts = [];
    for (var i = 0; i < args.length; i++) {
      var arg = args[i];
      try {
        parts.push(typeof arg === 'string' ? arg : (arg instanceof Error ? (arg.stack || String(arg)) : JSON.stringify(arg)));
      } catch (e) {
        parts.push(String(arg));
      }
    }
    var line = new Date().toISOString() + ' [' + level + '] ' + parts.join(' ').replace(/\s+$/, '');
    logs.push(line.length > maxLogLineLength ? line.substring(0, maxLogLineLength) + '...' : line);
    if (logs.length > maxLogLines) {
      logs.shift();
    }
  }

  if (typeof console !== 'undefined') {
    ['log', 'info', 'warn', 'error', 'debug'].forEach(function (level) {
      var original = console[level];
      if (typeof original !== 'function') {
        return;
      }
      console[level] = function () {
        try {
          pushLog(level, arguments);
        } catch (e) {}
        return original.apply(console, arguments);
      };
    });
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

  function heapBytes() {
    try {
      if (typeof HEAPU8 !== 'undefined' && HEAPU8) {
        return HEAPU8.length;
      }
    } catch (e) {}
    return 0;
  }

  function capture(source, error, fallbackMessage) {
    if (disabled || reported || pending) {
      return;
    }
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
    var text = stack && stack.indexOf(message) === 0 ? stack : (stack ? message + '\n' + stack : message);
    if (!isWasmFailure(source, error, text)) {
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
    pending = { callstack: text, details: details, log: logs.join('\n') };
    flush();
  }

  function flush() {
    var current = config || Module['bugsplat'];
    if (!pending || reported || disabled || !current || !current['database']) {
      return;
    }
    reported = true;
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

  window.addEventListener('error', function (e) {
    capture('error', e.error, e.message);
  }, true);
  window.addEventListener('unhandledrejection', function (e) {
    capture('unhandledrejection', e.reason);
  }, true);

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

  flush();
})();

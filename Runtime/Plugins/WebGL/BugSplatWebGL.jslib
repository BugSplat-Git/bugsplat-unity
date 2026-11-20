var BugSplatWebGLLibrary = {
  $BugSplatWebGL: {
    config: null,
    logs: [],
    maxLogLines: 100,
    maxLogLineLength: 500,
    reported: false,
    installed: false,
    consoleWrapped: false,

    pushLog: function (level, args) {
      var parts = [];
      for (var i = 0; i < args.length; i++) {
        var arg = args[i];
        try {
          parts.push(typeof arg === 'string' ? arg : (arg instanceof Error ? (arg.stack || String(arg)) : JSON.stringify(arg)));
        } catch (e) {
          parts.push(String(arg));
        }
      }
      var line = new Date().toISOString() + ' [' + level + '] ' + parts.join(' ');
      BugSplatWebGL.logs.push(line.length > BugSplatWebGL.maxLogLineLength ? line.substring(0, BugSplatWebGL.maxLogLineLength) + '...' : line);
      if (BugSplatWebGL.logs.length > BugSplatWebGL.maxLogLines) {
        BugSplatWebGL.logs.shift();
      }
    },

    wrapConsole: function () {
      if (BugSplatWebGL.consoleWrapped || typeof console === 'undefined') {
        return;
      }
      BugSplatWebGL.consoleWrapped = true;
      ['log', 'info', 'warn', 'error', 'debug'].forEach(function (level) {
        var original = console[level];
        if (typeof original !== 'function') {
          return;
        }
        console[level] = function () {
          BugSplatWebGL.pushLog(level, arguments);
          return original.apply(console, arguments);
        };
      });
    },

    isWasmFailure: function (text) {
      return /wasm-function\[|wasm:\/\/|RuntimeError|^abort\(|Aborted\(|memory access out of bounds|unreachable|function signature mismatch|Maximum call stack size exceeded|out of memory|Cannot enlarge memory/i.test(text);
    },

    heapBytes: function () {
      try {
        if (typeof HEAPU8 !== 'undefined' && HEAPU8) {
          return HEAPU8.length;
        }
      } catch (e) {}
      return 0;
    },

    capture: function (source, error, fallbackMessage) {
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
      if (!BugSplatWebGL.isWasmFailure(text)) {
        return;
      }
      BugSplatWebGL.report(source, text);
    },

    report: function (source, callstack) {
      var config = BugSplatWebGL.config;
      if (!config || BugSplatWebGL.reported) {
        return;
      }
      BugSplatWebGL.reported = true;

      var attributes = {};
      try {
        attributes = JSON.parse(config['attributes'] || '{}');
      } catch (e) {}
      attributes['webglAbortSource'] = source;
      attributes['webglHeapBytes'] = String(BugSplatWebGL.heapBytes());
      attributes['userAgent'] = navigator.userAgent;
      if (performance && performance.memory) {
        attributes['jsHeapUsedBytes'] = String(performance.memory.usedJSHeapSize);
      }

      var log = BugSplatWebGL.logs.join('\n');
      var body = new FormData();
      body.append('database', config['database']);
      body.append('appName', config['appName']);
      body.append('appVersion', config['appVersion']);
      body.append('appKey', config['appKey'] || '');
      body.append('user', config['user'] || '');
      body.append('email', config['email'] || '');
      body.append('description', config['description'] || '');
      body.append('callstack', callstack);
      body.append('crashTypeId', config['crashTypeId'] || '24');
      body.append('attributes', JSON.stringify(attributes));
      body.append('BugSplatWebGL.log', new Blob([log], { type: 'text/plain' }), 'BugSplatWebGL.log');

      var url = 'https://' + config['database'] + '.bugsplat.com/post/dotnetstandard/';
      var keepalive = callstack.length + log.length < 60000;
      fetch(url, { method: 'POST', body: body, keepalive: keepalive })
        .then(function (response) { return response.text(); })
        .then(function (text) { console.log('BugSplat: WebAssembly crash posted ' + text); })
        .catch(function (e) { console.error('BugSplat: WebAssembly crash post failed ' + e); });
    }
  },

  BugSplatWebGL_Install__deps: ['$BugSplatWebGL'],
  BugSplatWebGL_Install: function (configJson) {
    BugSplatWebGL.config = JSON.parse(UTF8ToString(configJson));
    BugSplatWebGL.wrapConsole();

    if (BugSplatWebGL.installed) {
      return;
    }
    BugSplatWebGL.installed = true;

    window.addEventListener('error', function (e) {
      BugSplatWebGL.capture('error', e.error, e.message);
    }, true);
    window.addEventListener('unhandledrejection', function (e) {
      BugSplatWebGL.capture('unhandledrejection', e.reason);
    }, true);

    var abortHandler = Module['abortHandler'];
    Module['abortHandler'] = function (message) {
      BugSplatWebGL.capture('abort', String(message));
      return abortHandler ? abortHandler.apply(this, arguments) : false;
    };
  }
};

autoAddDeps(BugSplatWebGLLibrary, '$BugSplatWebGL');
mergeInto(LibraryManager.library, BugSplatWebGLLibrary);

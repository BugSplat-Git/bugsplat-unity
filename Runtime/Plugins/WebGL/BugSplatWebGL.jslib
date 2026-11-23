var BugSplatWebGLLibrary = {
  BugSplatWebGL_Configure: function (configJson, attributesJson) {
    var api = Module['BugSplatWebGL'];
    if (!api) {
      return;
    }
    var config = JSON.parse(UTF8ToString(configJson));
    config['attributes'] = JSON.parse(UTF8ToString(attributesJson));
    api['configure'](config);
  },

  BugSplatWebGL_Disable: function () {
    var api = Module['BugSplatWebGL'];
    if (api) {
      api['disable']();
    }
  }
};

mergeInto(LibraryManager.library, BugSplatWebGLLibrary);

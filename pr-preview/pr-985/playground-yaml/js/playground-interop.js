// ── Theme management ──
window.getSystemTheme = function () {
    return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
};

window.getInitialTheme = function () {
    return window.__playgroundResolvedTheme || 'dark';
};

window.onSystemThemeChange = function (dotNetHelper) {
    var mq = window.matchMedia('(prefers-color-scheme: light)');
    mq.addEventListener('change', function (e) {
        dotNetHelper.invokeMethodAsync('OnSystemThemeChanged', e.matches ? 'light' : 'dark');
    });
};

window.applyTheme = function (theme) {
    document.documentElement.setAttribute('data-theme', theme);
    window.__playgroundResolvedTheme = theme;
    var monacoTheme = theme === 'light' ? 'vs' : 'vs-dark';
    if (typeof monaco !== 'undefined' && monaco.editor) {
        monaco.editor.setTheme(monacoTheme);
    } else {
        var attempts = 0;
        var interval = setInterval(function () {
            if (typeof monaco !== 'undefined' && monaco.editor) {
                monaco.editor.setTheme(monacoTheme);
                clearInterval(interval);
            } else if (++attempts > 50) {
                clearInterval(interval);
            }
        }, 100);
    }
};

// ── Keyboard shortcuts ──
window.registerPlaygroundShortcuts = function (dotNetHelper) {
    document.addEventListener('keydown', function (e) {
        if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
            e.preventDefault();
            dotNetHelper.invokeMethodAsync('OnConvertShortcut');
        }
    });
};

// ── Select reset (after dirty-cancel) ──
window.resetSampleSelect = function (value) {
    var sel = document.querySelector('.toolbar-select');
    if (sel) { sel.value = value; }
};

// ── Monaco editor markers (error squiggles) ──
window.setEditorMarkers = function (editorId, message, startLine, startCol, endLine, endCol) {
    window.monacoReady.then(function () {
        var models = monaco.editor.getModels();
        for (var i = 0; i < models.length; i++) {
            var editors = monaco.editor.getEditors();
            for (var j = 0; j < editors.length; j++) {
                var container = editors[j].getContainerDomNode();
                if (container && container.id === editorId) {
                    var model = editors[j].getModel();
                    if (model) {
                        monaco.editor.setModelMarkers(model, 'yaml-playground', [{
                            severity: monaco.MarkerSeverity.Error,
                            message: message,
                            startLineNumber: startLine,
                            startColumn: startCol,
                            endLineNumber: endLine,
                            endColumn: endCol
                        }]);
                    }
                    return;
                }
            }
        }
    });
};

window.clearEditorMarkers = function (editorId) {
    if (typeof monaco === 'undefined' || !monaco.editor) return;
    var editors = monaco.editor.getEditors();
    for (var j = 0; j < editors.length; j++) {
        var container = editors[j].getContainerDomNode();
        if (container && container.id === editorId) {
            var model = editors[j].getModel();
            if (model) {
                monaco.editor.setModelMarkers(model, 'yaml-playground', []);
            }
            return;
        }
    }
};

// ── Editor language switching ──
window.setEditorLanguage = function (editorId, language) {
    if (typeof monaco === 'undefined' || !monaco.editor) return;
    var editors = monaco.editor.getEditors();
    for (var j = 0; j < editors.length; j++) {
        var container = editors[j].getContainerDomNode();
        if (container && container.id === editorId) {
            var model = editors[j].getModel();
            if (model) {
                monaco.editor.setModelLanguage(model, language);
            }
            return;
        }
    }
};

// ── Theme management ──

// Returns the current system color scheme preference ("dark" or "light").
window.getSystemTheme = function () {
    return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
};

// Returns the theme resolved by the anti-flash inline script.
window.getInitialTheme = function () {
    return window.__playgroundResolvedTheme || 'dark';
};

// Listens for system color scheme changes and invokes the .NET callback.
window.onSystemThemeChange = function (dotNetHelper) {
    var mq = window.matchMedia('(prefers-color-scheme: light)');
    mq.addEventListener('change', function (e) {
        dotNetHelper.invokeMethodAsync('OnSystemThemeChanged', e.matches ? 'light' : 'dark');
    });
};

// Applies the theme to the document and Monaco editors.
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
        // Ctrl+Enter or Cmd+Enter to evaluate
        if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
            e.preventDefault();
            dotNetHelper.invokeMethodAsync('OnEvaluateShortcut');
        }
    });
};

// ── Select reset (after dirty-cancel) ──

window.resetSampleSelect = function (value) {
    var sel = document.querySelector('.toolbar-select');
    if (sel) { sel.value = value; }
};

// ── File operations ──

window.downloadFileFromBytes = function (filename, contentType, bytes) {
    var blob = new Blob([bytes], { type: contentType });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};
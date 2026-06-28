// Interop for the TypeScript playground. Minimal for the foundation increment (theme apply + a value reader
// for automated tests); the esbuild transpile-and-run bridge and Monaco TS lib wiring land in later increments.
window.playgroundInterop = {
    applyTheme: function (theme) {
        document.documentElement.setAttribute('data-theme', theme);
        try {
            if (window.monaco) {
                monaco.editor.setTheme(theme === 'light' ? 'vs' : 'vs-dark');
            }
        } catch (e) { /* monaco not ready yet */ }
    },

    // Read a Monaco editor's current model value by element id (used by the Playwright smoke test).
    getEditorValue: function (id) {
        try {
            var editors = (window.monaco && monaco.editor.getEditors) ? monaco.editor.getEditors() : [];
            for (var i = 0; i < editors.length; i++) {
                var node = editors[i].getDomNode();
                if (node && node.closest && node.closest('#' + id)) {
                    return editors[i].getValue();
                }
            }
        } catch (e) { /* ignore */ }
        return null;
    }
};

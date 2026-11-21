// Sets the indeterminate property on checkboxes with the 'indeterminate' CSS class.
// HTML doesn't support indeterminate via attribute; it must be set via JS.
window.updateIndeterminateCheckboxes = function () {
    document.querySelectorAll('input[type="checkbox"].indeterminate').forEach(function (cb) {
        cb.indeterminate = true;
    });
    document.querySelectorAll('input[type="checkbox"]:not(.indeterminate)').forEach(function (cb) {
        cb.indeterminate = false;
    });
};

// Registers global keyboard shortcuts for the playground.
// F5 triggers Run via .NET callback.
window.registerPlaygroundShortcuts = function (dotNetHelper) {
    document.addEventListener('keydown', function (e) {
        if (e.key === 'F5') {
            e.preventDefault();
            dotNetHelper.invokeMethodAsync('OnF5Pressed');
        }
    });
};

// Triggers a browser file download from a byte array.
window.downloadFileFromBytes = function (filename, contentType, bytes) {
    const blob = new Blob([bytes], { type: contentType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

// Opens a file picker and returns the selected file as a byte array.
window.pickFileAsBytes = function (accept) {
    return new Promise(function (resolve) {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = accept || '.zip';
        input.style.display = 'none';
        input.onchange = function () {
            if (input.files && input.files.length > 0) {
                const reader = new FileReader();
                reader.onload = function () {
                    resolve(new Uint8Array(reader.result));
                };
                reader.readAsArrayBuffer(input.files[0]);
            } else {
                resolve(null);
            }
            document.body.removeChild(input);
        };
        // Handle cancel — listen for focus returning without a file selection
        input.oncancel = function () {
            resolve(null);
            document.body.removeChild(input);
        };
        document.body.appendChild(input);
        input.click();
    });
};

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
    const mq = window.matchMedia('(prefers-color-scheme: light)');
    mq.addEventListener('change', function (e) {
        dotNetHelper.invokeMethodAsync('OnSystemThemeChanged', e.matches ? 'light' : 'dark');
    });
};

// Applies the theme to the document and Monaco editors.
// Retries Monaco theme if the editor hasn't loaded yet.
window.applyTheme = function (theme) {
    document.documentElement.setAttribute('data-theme', theme);
    window.__playgroundResolvedTheme = theme;
    var monacoTheme = theme === 'light' ? 'vs' : 'vs-dark';
    if (typeof monaco !== 'undefined' && monaco.editor) {
        monaco.editor.setTheme(monacoTheme);
    } else {
        // Monaco may still be loading; poll until available
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

window.registerCSharpProviders = function (dotNetRef) {
    // Wait for Monaco to be available
    if (typeof monaco === 'undefined') {
        setTimeout(() => window.registerCSharpProviders(dotNetRef), 500);
        return;
    }

    // Register completion item provider for C#
    monaco.languages.registerCompletionItemProvider('csharp', {
        triggerCharacters: ['.', ' '],
        provideCompletionItems: async function (model, position) {
            try {
                const code = model.getValue();
                const items = await dotNetRef.invokeMethodAsync(
                    'GetCompletionsForJs', code, position.lineNumber, position.column);

                return {
                    suggestions: items.map(item => ({
                        label: item.label,
                        kind: item.kind,
                        detail: item.detail,
                        insertText: item.insertText,
                        sortText: item.sortText,
                        filterText: item.filterText,
                        range: undefined
                    }))
                };
            } catch {
                return { suggestions: [] };
            }
        }
    });

    // Register signature help provider for C#
    monaco.languages.registerSignatureHelpProvider('csharp', {
        signatureHelpTriggerCharacters: ['(', ','],
        signatureHelpRetriggerCharacters: [','],
        provideSignatureHelp: async function (model, position) {
            try {
                const code = model.getValue();
                const result = await dotNetRef.invokeMethodAsync(
                    'GetSignatureHelpForJs', code, position.lineNumber, position.column);

                if (!result) return null;

                return {
                    value: {
                        signatures: result.signatures.map(sig => ({
                            label: sig.label,
                            documentation: sig.documentation,
                            parameters: sig.parameters.map(p => ({
                                label: p.label,
                                documentation: p.documentation
                            }))
                        })),
                        activeSignature: result.activeSignature,
                        activeParameter: result.activeParameter
                    },
                    dispose: function () { }
                };
            } catch {
                return null;
            }
        }
    });
};

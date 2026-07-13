// Registers a Roslyn-backed completion provider for the C# Monaco editor.
// The dotNetHelper calls back into IntelliSenseService via [JSInvokable].
window.registerCSharpCompletionProvider = function (dotNetHelper) {
    monaco.languages.registerCompletionItemProvider('csharp', {
        triggerCharacters: ['.'],
        provideCompletionItems: async function (model, position) {
            const code = model.getValue();
            const line = position.lineNumber;
            const column = position.column;

            try {
                const completions = await dotNetHelper.invokeMethodAsync(
                    'GetCompletionsForJs',
                    code,
                    line,
                    column
                );

                if (!completions || completions.length === 0) {
                    return { suggestions: [] };
                }

                // Compute the word at the cursor for the replacement range
                const word = model.getWordUntilPosition(position);
                const range = {
                    startLineNumber: position.lineNumber,
                    startColumn: word.startColumn,
                    endLineNumber: position.lineNumber,
                    endColumn: word.endColumn
                };

                return {
                    suggestions: completions.map(function (item) {
                        return {
                            label: item.label,
                            kind: item.kind,
                            detail: item.detail,
                            insertText: item.insertText,
                            sortText: item.sortText,
                            filterText: item.filterText,
                            range: range
                        };
                    })
                };
            } catch (e) {
                console.error('Completion provider error:', e);
                return { suggestions: [] };
            }
        }
    });
};

// Registers a Roslyn-backed signature help provider for the C# Monaco editor.
// Triggers on '(' and ',' to show method parameter info.
window.registerCSharpSignatureHelpProvider = function (dotNetHelper) {
    monaco.languages.registerSignatureHelpProvider('csharp', {
        signatureHelpTriggerCharacters: ['(', ','],
        signatureHelpRetriggerCharacters: [','],
        provideSignatureHelp: async function (model, position) {
            const code = model.getValue();
            const line = position.lineNumber;
            const column = position.column;

            try {
                const result = await dotNetHelper.invokeMethodAsync(
                    'GetSignatureHelpForJs',
                    code,
                    line,
                    column
                );

                if (!result || !result.signatures || result.signatures.length === 0) {
                    return null;
                }

                return {
                    value: {
                        signatures: result.signatures.map(function (sig) {
                            return {
                                label: sig.label,
                                documentation: sig.documentation
                                    ? { value: sig.documentation, isTrusted: true }
                                    : undefined,
                                parameters: (sig.parameters || []).map(function (p) {
                                    return {
                                        label: p.label,
                                        documentation: p.documentation
                                            ? { value: p.documentation, isTrusted: true }
                                            : undefined
                                    };
                                })
                            };
                        }),
                        activeSignature: result.activeSignature || 0,
                        activeParameter: result.activeParameter || 0
                    },
                    dispose: function () { }
                };
            } catch (e) {
                console.error('Signature help provider error:', e);
                return null;
            }
        }
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

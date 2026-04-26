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
            dotNetHelper.invokeMethodAsync('OnEvaluateShortcut');
        }
    });
};

// ── Select reset (after dirty-cancel) ──
window.resetSampleSelect = function (value) {
    var sel = document.querySelector('.toolbar-select');
    if (sel) { sel.value = value; }
};

// ── Monaco editor markers (for Roslyn compilation errors) ──
window.setMonacoMarkers = function (editorId, markersJson) {
    if (typeof monaco === 'undefined' || !monaco.editor) return;
    var markers;
    try {
        markers = JSON.parse(markersJson);
    } catch (e) {
        return;
    }

    // Find the C# model (operators editor is the only one)
    var models = monaco.editor.getModels();
    var model = null;
    for (var i = 0; i < models.length; i++) {
        if (models[i].getLanguageId() === 'csharp') {
            model = models[i];
            break;
        }
    }

    if (!model) return;

    var monacoMarkers = markers.map(function (m) {
        return {
            startLineNumber: m.startLineNumber,
            startColumn: m.startColumn,
            endLineNumber: m.endLineNumber,
            endColumn: m.endColumn,
            message: m.message,
            severity: m.severity, // 8 = Error, 4 = Warning
        };
    });

    monaco.editor.setModelMarkers(model, 'roslyn', monacoMarkers);
};

// ── Roslyn-backed IntelliSense providers ──

// Registers a completion provider for the C# Monaco editor.
window.registerCSharpCompletionProvider = function (dotNetHelper) {
    monaco.languages.registerCompletionItemProvider('csharp', {
        triggerCharacters: ['.'],
        provideCompletionItems: async function (model, position) {
            var code = model.getValue();
            var line = position.lineNumber;
            var column = position.column;

            try {
                var completions = await dotNetHelper.invokeMethodAsync(
                    'GetCompletionsForJs', code, line, column);

                if (!completions || completions.length === 0) {
                    return { suggestions: [] };
                }

                var word = model.getWordUntilPosition(position);
                var range = {
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

// Registers a signature help provider for the C# Monaco editor.
window.registerCSharpSignatureHelpProvider = function (dotNetHelper) {
    monaco.languages.registerSignatureHelpProvider('csharp', {
        signatureHelpTriggerCharacters: ['(', ','],
        signatureHelpRetriggerCharacters: [','],
        provideSignatureHelp: async function (model, position) {
            var code = model.getValue();
            var line = position.lineNumber;
            var column = position.column;

            try {
                var result = await dotNetHelper.invokeMethodAsync(
                    'GetSignatureHelpForJs', code, line, column);

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

// Interop for the TypeScript playground.
//   - theme apply + a Monaco value reader (used by the automated smoke test);
//   - the in-browser RUN bridge: esbuild-wasm transpiles + bundles the user's TypeScript together with the
//     generated module and the pre-bundled shared runtime into one ES module, which is then imported and
//     executed in the page's own JS engine, with console output captured. This is the playground's
//     differentiator: the emitted validators/mutators actually run, in the browser, against live input.
window.playgroundInterop = (function () {
    let esbuildPromise = null;     // resolves to the initialized esbuild-wasm module
    let runtimeSourcePromise = null; // resolves to the pre-bundled corvus-runtime.js source text

    async function ensureEsbuild() {
        if (!esbuildPromise) {
            esbuildPromise = (async () => {
                const esbuild = await import('/lib/esbuild/browser.min.js');
                // worker:false runs esbuild on the main thread. A Web Worker's postMessage callbacks are not
                // pumped while Blazor's JS interop call is awaiting, so a worker-backed build hangs; the
                // main-thread build resolves inline and is reliable under interop.
                await esbuild.initialize({ wasmURL: '/lib/esbuild/esbuild.wasm', worker: false });
                return esbuild;
            })();
        }
        return esbuildPromise;
    }

    function ensureRuntimeSource() {
        if (!runtimeSourcePromise) {
            runtimeSourcePromise = fetch('/corvus-runtime.js').then(function (r) {
                if (!r.ok) { throw new Error('failed to load corvus-runtime.js (' + r.status + ')'); }
                return r.text();
            });
        }
        return runtimeSourcePromise;
    }

    function fmt(x) {
        if (typeof x === 'string') { return x; }
        if (x instanceof Uint8Array) { try { return new TextDecoder().decode(x); } catch (e) { return String(x); } }
        try { return JSON.stringify(x); } catch (e) { return String(x); }
    }

    return {
        applyTheme: function (theme) {
            document.documentElement.setAttribute('data-theme', theme);
            try { if (window.monaco) { monaco.editor.setTheme(theme === 'light' ? 'vs' : 'vs-dark'); } } catch (e) { /* not ready */ }
        },

        // The persisted theme preference ('auto' | 'light' | 'dark').
        getThemePref: function () {
            try { return localStorage.getItem('playground-theme') || 'auto'; } catch (e) { return 'auto'; }
        },

        // Persist + apply a theme preference; resolves 'auto' against the OS setting. Returns the resolved
        // theme ('light' | 'dark') so the caller can sync its Monaco theme.
        setTheme: function (pref) {
            var resolved = pref;
            if (pref === 'auto') {
                resolved = (window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches) ? 'light' : 'dark';
            }
            try { localStorage.setItem('playground-theme', pref); } catch (e) { /* ignore */ }
            this.applyTheme(resolved);
            return resolved;
        },

        getEditorValue: function (id) {
            try {
                var editors = (window.monaco && monaco.editor.getEditors) ? monaco.editor.getEditors() : [];
                for (var i = 0; i < editors.length; i++) {
                    var node = editors[i].getDomNode();
                    if (node && node.closest && node.closest('#' + id)) { return editors[i].getValue(); }
                }
            } catch (e) { /* ignore */ }
            return null;
        },

        // Warm up esbuild + the runtime source so the first Run is fast.
        warmUp: async function () {
            try { await Promise.all([ensureEsbuild(), ensureRuntimeSource()]); return true; } catch (e) { return false; }
        },

        // Transpile + bundle [userCode + generatedCode + runtime] into one module, run it, capture console.
        // Returns { ok, output: [{ kind, text }], error }.
        transpileAndRun: async function (generatedCode, userCode) {
            let esbuild, runtimeSource;
            try {
                esbuild = await ensureEsbuild();
                runtimeSource = await ensureRuntimeSource();
            } catch (e) {
                return { ok: false, output: [], error: 'Toolchain load failed: ' + (e.message || String(e)) };
            }

            let built;
            try {
                built = await esbuild.build({
                    stdin: { contents: userCode, loader: 'ts', resolveDir: '/', sourcefile: 'user.ts' },
                    bundle: true,
                    // iife so the bundle is a self-contained expression we can run synchronously via new
                    // Function — no dynamic import() of a Blob (which also hangs under Blazor's interop await).
                    format: 'iife',
                    write: false,
                    target: 'es2022',
                    plugins: [{
                        name: 'playground-vfs',
                        setup: function (build) {
                            build.onResolve({ filter: /.*/ }, function (args) {
                                if (args.path === './generated.js' || args.path === './generated' || args.path === './generated.ts') {
                                    return { path: 'generated', namespace: 'pg' };
                                }
                                if (args.path === './corvus-runtime.js' || args.path === '../corvus-runtime.js' || args.path === '@endjin/corvus-json-runtime') {
                                    return { path: 'runtime', namespace: 'pg' };
                                }
                                // Anything else (a stray import) is left external so the bundle still builds.
                                return { path: args.path, external: true };
                            });
                            build.onLoad({ filter: /.*/, namespace: 'pg' }, function (args) {
                                if (args.path === 'generated') { return { contents: generatedCode, loader: 'ts' }; }
                                if (args.path === 'runtime') { return { contents: runtimeSource, loader: 'js' }; }
                                return null;
                            });
                        }
                    }]
                });
            } catch (e) {
                var msg = (e && e.errors && e.errors.length) ? e.errors.map(function (x) { return x.text; }).join('\n') : (e.message || String(e));
                return { ok: false, output: [], error: 'Build error:\n' + msg };
            }

            var js = built.outputFiles[0].text;

            var lines = [];
            var orig = { log: console.log, error: console.error, warn: console.warn, info: console.info };
            function cap(kind) { return function () { lines.push({ kind: kind, text: Array.prototype.map.call(arguments, fmt).join(' ') }); }; }
            console.log = cap('log'); console.error = cap('error'); console.warn = cap('warn'); console.info = cap('info');

            var runError = null;
            try {
                // The bundle is a self-contained IIFE; run it synchronously. The user's top-level console
                // output is captured here. (Output emitted from async callbacks after this returns is not.)
                (new Function(js))();
            } catch (e) {
                runError = (e && e.message) || String(e);
            } finally {
                console.log = orig.log; console.error = orig.error; console.warn = orig.warn; console.info = orig.info;
            }

            return { ok: !runError, output: lines, error: runError };
        }
    };
})();

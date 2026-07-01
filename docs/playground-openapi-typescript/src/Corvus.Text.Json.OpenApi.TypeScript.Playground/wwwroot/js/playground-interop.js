// Interop for the OpenAPI TypeScript playground.
//   - the operation-tree indeterminate-checkbox helper + F5 shortcut + theme helpers (shared with the
//     C# OpenAPI playground shape);
//   - the in-browser RUN bridge: esbuild-wasm transpiles + bundles the user's TypeScript together with
//     the generated client + model modules and the two pre-bundled shared runtimes into one ES module,
//     which is then run in the page's own JS engine with console output captured. A stubbed
//     globalThis.fetch supplies canned responses so the generated client's FetchApiTransport runs offline.
window.playgroundInterop = (function () {
    let esbuildPromise = null;           // resolves to the initialized esbuild-wasm module
    let modelRuntimePromise = null;      // resolves to the vendored model runtime (corvus-runtime.js) source
    let clientRuntimePromise = null;     // resolves to the vendored client runtime (corvus-client-runtime.js) source

    async function ensureEsbuild() {
        if (!esbuildPromise) {
            esbuildPromise = (async () => {
                // Resolve against the document base (the <base href>), not the site root, so esbuild loads
                // whether the app is served at "/", at a subpath, or under a GitHub Pages base path.
                const esbuildJsUrl = new URL('lib/esbuild/browser.min.js', document.baseURI).href;
                const esbuildWasmUrl = new URL('lib/esbuild/esbuild.wasm', document.baseURI).href;
                const esbuild = await import(esbuildJsUrl);
                // worker:false runs esbuild on the main thread. A Web Worker's postMessage callbacks are not
                // pumped while Blazor's JS interop call is awaiting, so a worker-backed build hangs; the
                // main-thread build resolves inline and is reliable under interop.
                await esbuild.initialize({ wasmURL: esbuildWasmUrl, worker: false });
                return esbuild;
            })();
        }
        return esbuildPromise;
    }

    function fetchText(name) {
        return fetch(new URL(name, document.baseURI).href).then(function (r) {
            if (!r.ok) { throw new Error('failed to load ' + name + ' (' + r.status + ')'); }
            return r.text();
        });
    }

    function ensureModelRuntime() {
        if (!modelRuntimePromise) { modelRuntimePromise = fetchText('corvus-runtime.js'); }
        return modelRuntimePromise;
    }

    function ensureClientRuntime() {
        if (!clientRuntimePromise) { clientRuntimePromise = fetchText('corvus-client-runtime.js'); }
        return clientRuntimePromise;
    }

    function fmt(x) {
        if (typeof x === 'string') { return x; }
        if (x instanceof Uint8Array) { try { return new TextDecoder().decode(x); } catch (e) { return String(x); } }
        try { return JSON.stringify(x); } catch (e) { return String(x); }
    }

    // Normalizes an import specifier to a candidate generated-file key: strips a leading "./", collapses
    // "../" segments, and swaps a trailing ".js" for ".ts" (the generated files are .ts in the map).
    function normalizeToKey(spec) {
        let p = spec;
        while (p.startsWith('./')) { p = p.slice(2); }
        // Collapse any parent-directory hops (the model barrel is imported from ./models, its siblings via ../).
        const parts = [];
        for (const seg of p.split('/')) {
            if (seg === '..') { parts.pop(); }
            else if (seg !== '.' && seg !== '') { parts.push(seg); }
        }
        p = parts.join('/');
        if (p.endsWith('.js')) { p = p.slice(0, -3) + '.ts'; }
        return p;
    }

    // Looks up a generated file by a normalized key, trying the key itself, its basename, and a models/ prefix.
    function lookupGenerated(files, key) {
        if (Object.prototype.hasOwnProperty.call(files, key)) { return files[key]; }
        const base = key.split('/').pop();
        if (Object.prototype.hasOwnProperty.call(files, base)) { return files[base]; }
        const modelsKey = 'models/' + base;
        if (Object.prototype.hasOwnProperty.call(files, modelsKey)) { return files[modelsKey]; }
        return null;
    }

    // The mock transport: a canned fetch installed for the duration of a run. It returns a 201 for a POST
    // to /pets (echoing the posted body), a 200 single pet for GET /pets/{id}, a 200 list for GET /pets,
    // and a benign 200 {} otherwise — so the generated client's FetchApiTransport produces typed responses
    // without a live server. Mirrors the old PlaygroundHttpMessageHandler cases (list/create/show pet).
    function makeMockFetch() {
        const enc = new TextEncoder();
        function jsonResponse(status, obj, headers) {
            const body = enc.encode(JSON.stringify(obj));
            const h = new Headers({ 'Content-Type': 'application/json' });
            if (headers) { for (const k of Object.keys(headers)) { h.set(k, headers[k]); } }
            return new Response(body, { status: status, headers: h });
        }
        const samplePets = [
            { id: 1, name: 'Fido', tag: 'dog', breed: 'Labrador', status: 'available', tags: ['good-boy'] },
            { id: 2, name: 'Whiskers', tag: 'cat', breed: 'Tabby', status: 'pending', tags: [] }
        ];
        return async function (input, init) {
            const url = typeof input === 'string' ? input : (input && input.url) || String(input);
            const method = ((init && init.method) || (input && input.method) || 'GET').toUpperCase();
            let path = url;
            try { path = new URL(url).pathname; } catch (e) { /* keep raw */ }

            if (method === 'POST' && /\/pets$/.test(path)) {
                let posted = {};
                try { if (init && init.body) { posted = JSON.parse(new TextDecoder().decode(init.body)); } } catch (e) { /* ignore */ }
                const created = Object.assign({ id: 99, status: 'available' }, posted);
                return jsonResponse(201, created);
            }
            if (method === 'GET' && /\/pets\/[^/]+$/.test(path)) {
                const id = path.split('/').pop();
                return jsonResponse(200, { id: 42, name: 'Rex', tag: 'dog', breed: 'Labrador', status: 'available', tags: ['good-boy'], _requestedId: id });
            }
            if (method === 'GET' && /\/pets$/.test(path)) {
                return jsonResponse(200, samplePets, { 'x-next': '/pets?page=2', 'x-total-count': '2' });
            }
            return jsonResponse(200, {});
        };
    }

    return {
        // ── operation-tree + shortcuts ──

        // Sets the indeterminate property on checkboxes with the 'indeterminate' CSS class (HTML has no
        // indeterminate attribute; it must be set via JS).
        updateIndeterminateCheckboxes: function () {
            document.querySelectorAll('input[type="checkbox"].indeterminate').forEach(function (cb) { cb.indeterminate = true; });
            document.querySelectorAll('input[type="checkbox"]:not(.indeterminate)').forEach(function (cb) { cb.indeterminate = false; });
        },

        // ── theme ──

        applyTheme: function (theme) {
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
        },

        getSystemTheme: function () {
            return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
        },

        getInitialTheme: function () {
            return window.__playgroundResolvedTheme || 'dark';
        },

        // Warm up esbuild + both runtimes so the first Run is fast.
        warmUp: async function () {
            try { await Promise.all([ensureEsbuild(), ensureModelRuntime(), ensureClientRuntime()]); return true; } catch (e) { return false; }
        },

        // Transpile + bundle [userCode + generated client/model files + both runtimes] into one esm module,
        // run it (wrapped in an async fn) with a stubbed fetch, and capture console output. `generatedFiles`
        // is a { filename: content } map (client .ts at the root, model files under models/). Returns
        // { ok, output:[{kind,text}], error }.
        transpileAndRun: async function (generatedFiles, userCode) {
            let esbuild, modelRuntime, clientRuntime;
            try {
                esbuild = await ensureEsbuild();
                modelRuntime = await ensureModelRuntime();
                clientRuntime = await ensureClientRuntime();
            } catch (e) {
                return { ok: false, output: [], error: 'Toolchain load failed: ' + (e.message || String(e)) };
            }

            const files = generatedFiles || {};

            let built;
            try {
                built = await esbuild.build({
                    stdin: { contents: userCode, loader: 'ts', resolveDir: '/', sourcefile: 'user.ts' },
                    bundle: true,
                    // esm: the OpenAPI client is async, so the user code uses top-level await; only esm
                    // supports TLA. A bundled esm module has every import inlined (no bare import survives),
                    // so its whole body can be wrapped in an async function and run via new Function — no
                    // dynamic import() of a Blob (which hangs under Blazor's interop await).
                    format: 'esm',
                    write: false,
                    target: 'es2022',
                    plugins: [{
                        name: 'playground-vfs',
                        setup: function (build) {
                            build.onResolve({ filter: /.*/ }, function (args) {
                                const spec = args.path;
                                // The two shared runtimes — by package name or by the relative specifier the
                                // generated modules import them under.
                                if (spec === '@endjin/corvus-json-client-runtime' || /corvus-client-runtime(\.js|\.ts)?$/.test(spec)) {
                                    return { path: 'client-runtime', namespace: 'pg' };
                                }
                                if (spec === '@endjin/corvus-json-runtime' || /corvus-runtime(\.js|\.ts)?$/.test(spec)) {
                                    return { path: 'model-runtime', namespace: 'pg' };
                                }
                                // A generated file (client .ts or the model barrel). Resolve to its map key.
                                const key = normalizeToKey(spec);
                                if (lookupGenerated(files, key) !== null) {
                                    return { path: key, namespace: 'pg' };
                                }
                                // Unknown relative import that we cannot serve — leave external so the build
                                // still completes (it will simply be undefined at run time).
                                if (spec.startsWith('.') || spec.startsWith('/')) {
                                    return { path: spec, external: true };
                                }
                                return { path: spec, external: true };
                            });
                            build.onLoad({ filter: /.*/, namespace: 'pg' }, function (args) {
                                if (args.path === 'client-runtime') { return { contents: clientRuntime, loader: 'js' }; }
                                if (args.path === 'model-runtime') { return { contents: modelRuntime, loader: 'js' }; }
                                const content = lookupGenerated(files, args.path);
                                if (content !== null) { return { contents: content, loader: 'ts', resolveDir: '/' }; }
                                return null;
                            });
                        }
                    }]
                });
            } catch (e) {
                var msg = (e && e.errors && e.errors.length) ? e.errors.map(function (x) { return x.text; }).join('\n') : (e.message || String(e));
                return { ok: false, output: [], error: 'Build error:\n' + msg };
            }

            // Strip any top-level `export` keywords the bundled body might carry (our user code does not
            // export, but be defensive) so the body is valid inside a plain async function.
            var js = built.outputFiles[0].text.replace(/^export\s+/gm, '');

            var lines = [];
            var orig = { log: console.log, error: console.error, warn: console.warn, info: console.info };
            function cap(kind) { return function () { lines.push({ kind: kind, text: Array.prototype.map.call(arguments, fmt).join(' ') }); }; }
            console.log = cap('log'); console.error = cap('error'); console.warn = cap('warn'); console.info = cap('info');

            var savedFetch = globalThis.fetch;
            globalThis.fetch = makeMockFetch();
            var runError = null;
            try {
                // Wrap the bundled esm body in an async function and await it, so top-level await in the
                // user code (the async client calls) is supported and its output is captured here.
                var runner = new Function('return (async () => {\n' + js + '\n})();');
                await runner();
            } catch (e) {
                runError = (e && e.message) || String(e);
            } finally {
                globalThis.fetch = savedFetch;
                console.log = orig.log; console.error = orig.error; console.warn = orig.warn; console.info = orig.info;
            }

            return { ok: !runError, output: lines, error: runError };
        }
    };
})();

// ── globals the C#-shape components expect (OperationTreePanel / MainLayout call these by bare name) ──

// Operation-tree indeterminate checkboxes (bare-name call from OperationTreePanel).
window.updateIndeterminateCheckboxes = function () {
    window.playgroundInterop.updateIndeterminateCheckboxes();
};

// F5 triggers Run via the .NET callback.
window.registerPlaygroundShortcuts = function (dotNetHelper) {
    document.addEventListener('keydown', function (e) {
        if (e.key === 'F5') {
            e.preventDefault();
            dotNetHelper.invokeMethodAsync('OnF5Pressed');
        }
    });
};

// Theme helpers (bare-name calls from MainLayout).
window.getSystemTheme = function () { return window.playgroundInterop.getSystemTheme(); };
window.getInitialTheme = function () { return window.playgroundInterop.getInitialTheme(); };
window.applyTheme = function (theme) { window.playgroundInterop.applyTheme(theme); };
window.onSystemThemeChange = function (dotNetHelper) {
    var mq = window.matchMedia('(prefers-color-scheme: light)');
    mq.addEventListener('change', function (e) {
        dotNetHelper.invokeMethodAsync('OnSystemThemeChanged', e.matches ? 'light' : 'dark');
    });
};
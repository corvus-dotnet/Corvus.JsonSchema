import * as esbuild from 'esbuild';

await esbuild.build({
    entryPoints: ['entry.js'],
    bundle: true,
    format: 'iife',
    globalName: 'MonacoJsonata',
    outfile: 'src/Corvus.Text.Json.Jsonata.Playground/wwwroot/js/monaco-jsonata-bundle.js',
    // Redirect monaco-editor imports to our shim that uses the global
    alias: {
        'monaco-editor': './monaco-editor-shim.js',
    },
    platform: 'browser',
    target: 'es2020',
    minify: true,
    define: {
        'process.env.NODE_ENV': '"production"',
    },
});

console.log('monaco-jsonata bundle created successfully.');
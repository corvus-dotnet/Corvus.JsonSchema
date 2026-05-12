import * as esbuild from 'esbuild';

await esbuild.build({
    entryPoints: ['jsonpath-language.js'],
    bundle: true,
    format: 'iife',
    globalName: 'MonacoJsonPath',
    outfile: 'src/Corvus.Text.Json.JsonPath.Playground/wwwroot/js/monaco-jsonpath-bundle.js',
    platform: 'browser',
    target: 'es2020',
    minify: true,
    define: {
        'process.env.NODE_ENV': '"production"',
    },
});

console.log('monaco-jsonpath bundle created successfully.');

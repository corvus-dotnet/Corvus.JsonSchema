import * as esbuild from 'esbuild';

await esbuild.build({
    entryPoints: ['jmespath-language.js'],
    bundle: true,
    format: 'iife',
    globalName: 'MonacoJMESPath',
    outfile: 'src/Corvus.Text.Json.JMESPath.Playground/wwwroot/js/monaco-jmespath-bundle.js',
    platform: 'browser',
    target: 'es2020',
    minify: true,
    define: {
        'process.env.NODE_ENV': '"production"',
    },
});

console.log('monaco-jmespath bundle created successfully.');

// Entry point for esbuild bundling.
// Re-exports monaco-jsonata so the IIFE exposes registerJsonataLanguage.
export { registerJsonataLanguage } from 'monaco-jsonata';
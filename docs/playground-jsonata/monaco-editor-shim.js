// Shim for monaco-editor: returns the global `monaco` object loaded by BlazorMonaco.
module.exports = (typeof monaco !== 'undefined') ? monaco : {};
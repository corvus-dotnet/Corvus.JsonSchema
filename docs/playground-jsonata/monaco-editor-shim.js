/**
 * Shim for monaco-editor: returns the global `monaco` object loaded by BlazorMonaco.
 * @module monaco-editor-shim
 * @returns {object} The monaco editor instance or an empty object if not available.
 */
module.exports = (typeof monaco !== 'undefined') ? monaco : {};
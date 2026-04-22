// blockly-jsonlogic.js — Custom Blockly blocks and JSON Logic conversion
// for the Corvus.JsonSchema JSON Logic Playground.
//
// Architecture:
//   - JSON Logic is expression-based (everything returns a value).
//   - All operator blocks have `output` connections (reporter/boolean shape).
//   - The `if` block is a C-shaped reporter: has `output` AND `input_statement`.
//   - C-block mouths use `jsonlogic_yield` (statement) to wrap value expressions,
//     analogous to Scratch's "say" block.
//   - Conversion between JSON Logic ↔ Blockly workspace is handled entirely in JS.

(function () {
    'use strict';

    // ── Scratch-style colours ──
    const COLOUR_CONTROL  = '#FFAB19';   // orange — if
    const COLOUR_LOGIC    = '#59C059';   // green — and, or, not, comparisons
    const COLOUR_DATA     = '#5CB1D6';   // blue — var
    const COLOUR_PATH     = '#3E8FAB';   // darker blue — path segments (property, array_element)
    const COLOUR_MATH     = '#4C97FF';   // purple-blue — arithmetic
    const COLOUR_TEXT     = '#9966FF';   // purple — string ops
    const COLOUR_YIELD    = '#9966FF';   // purple — result/yield in mouths
    const COLOUR_ARRAY    = '#0FBD8C';   // teal — array ops

    // ── Custom Blockly themes (dark / light) ──
    // Zelos renderer + our colour constants; componentStyles control the
    // toolbox, flyout, workspace background, and scrollbar appearance.
    var darkTheme = Blockly.Theme.defineTheme('jsonlogic_dark', {
        base: 'zelos',
        componentStyles: {
            workspaceBackgroundColour: '#1e1e1e',
            toolboxBackgroundColour:   '#252526',
            toolboxForegroundColour:   '#cccccc',
            flyoutBackgroundColour:    '#2d2d2d',
            flyoutForegroundColour:    '#cccccc',
            flyoutOpacity:             0.95,
            scrollbarColour:           '#555555',
            scrollbarOpacity:          0.6,
            insertionMarkerColour:     '#ffffff',
        },
        fontStyle: {
            family: "'Segoe UI', 'Helvetica Neue', Arial, sans-serif",
            weight: 'bold',
            size: 11,
        },
    });

    var lightTheme = Blockly.Theme.defineTheme('jsonlogic_light', {
        base: 'zelos',
        componentStyles: {
            workspaceBackgroundColour: '#f5f5f5',
            toolboxBackgroundColour:   '#ffffff',
            toolboxForegroundColour:   '#333333',
            flyoutBackgroundColour:    '#e8e8e8',
            flyoutForegroundColour:    '#333333',
            flyoutOpacity:             0.95,
            scrollbarColour:           '#aaaaaa',
            scrollbarOpacity:          0.6,
            insertionMarkerColour:     '#000000',
        },
        fontStyle: {
            family: "'Segoe UI', 'Helvetica Neue', Arial, sans-serif",
            weight: 'bold',
            size: 11,
        },
    });

    // Type system: all value-producing blocks output 'Value'.
    // Value inputs accept this list, which excludes 'PathSegment'.
    // NOTE: 'Boolean' is deliberately excluded — Zelos renders any input
    // whose check includes 'Boolean' as a hexagonal <> socket.  By using
    // only 'Value' for outputs, every connector stays round ().
    const VALUE_TYPES = ['Number', 'String', 'Array', 'Value'];

    // ══════════════════════════════════════════════════════════════════
    // DATA-AWARE DYNAMIC DROPDOWN
    // ══════════════════════════════════════════════════════════════════

    /**
     * Create a FieldDropdown with a dynamic options generator that queries
     * currentSampleData for suggestions.
     *
     * Uses the documented dynamic-dropdown pattern:
     *   https://developers.google.com/blockly/guides/create-custom-blocks/fields/built-in-fields/dropdown#dynamic_dropdowns
     *
     * The generator function runs every time the dropdown is clicked, so
     * options are always up-to-date with the current sample data.
     *
     * FieldDropdown.doClassValidation_ rejects values not in getOptions().
     * We wrap setValue() to temporarily inject the incoming value into the
     * generator's results so that setFieldValue() during block loading
     * always succeeds — even if the value isn't a current data key.
     *
     * @param {string} defaultValue - Initial/fallback value.
     * @param {function(Blockly.Block):string[]} suggestionsProvider -
     *        Given the source block (may be null), return suggestion strings.
     * @returns {Blockly.FieldDropdown}
     */
    /** Refresh every data-dropdown field in the workspace so the ⚠ prefix
     *  appears or disappears based on the current data context.
     *  Uses Blockly.Events.disable() to prevent deferred events from
     *  creating an infinite refresh loop. */
    var _refreshTimeout = null;
    function refreshDataDropdowns() {
        if (!workspace) return;
        // Disable events entirely — Blockly fires them asynchronously via
        // setTimeout(0), so a synchronous flag would be cleared before they
        // reach the listener.
        Blockly.Events.disable();
        try {
            workspace.getAllBlocks(false).forEach(function (block) {
                block.inputList.forEach(function (input) {
                    input.fieldRow.forEach(function (field) {
                        if (field.isDataDropdown_) {
                            field.generatedOptions = null;
                            var val = field.getValue();
                            if (val != null) {
                                field.setValue(val);
                            }
                        }
                    });
                });
            });
        } finally {
            Blockly.Events.enable();
        }
    }

    function createDataDropdown(defaultValue, suggestionsProvider) {
        var pendingSetValue = null;

        // Dynamic generator — called by Blockly with `this` = the field.
        // Despite what the Blockly docs say ("runs every time the dropdown is
        // clicked"), getOptions(true) actually returns a CACHED result.  We
        // clear generatedOptions in setValue (for programmatic loads) and in
        // showEditor_ (for user clicks) so the generator always re-runs.
        var field = new Blockly.FieldDropdown(function () {
            var block = null;
            try { block = this.getSourceBlock(); } catch (e) { }

            var suggestions = [];
            try { suggestions = suggestionsProvider(block) || []; } catch (e) { }

            var options = suggestions.map(function (s) { return [s, s]; });

            // Always include the field's current value — prefixed with ⚠
            // when it doesn't appear in the data-derived suggestions.
            var current = null;
            try { current = this.getValue(); } catch (e) { }
            if (current && suggestions.indexOf(current) === -1) {
                options.unshift(['\u26a0 ' + current, current]);
            }

            // Include the value about to be set (injected by our setValue wrapper)
            if (pendingSetValue != null && pendingSetValue !== current
                && suggestions.indexOf(pendingSetValue) === -1) {
                options.unshift(['\u26a0 ' + pendingSetValue, String(pendingSetValue)]);
            }

            // FieldDropdown requires at least one option
            if (options.length === 0) {
                options = [[defaultValue, defaultValue]];
            }

            return options;
        });

        // Wrap setValue: clear the cached options so getOptions(true) in
        // doClassValidation_ will re-call our generator (which sees pendingSetValue).
        // Also bypass Blockly's short-circuit (value_ === newValue) because
        // the display LABEL may change even when the raw value hasn't (the ⚠
        // prefix depends on data context, not the value itself).
        var origSetValue = field.setValue;
        field.setValue = function (newValue) {
            pendingSetValue = newValue;
            this.generatedOptions = null;
            if (this.value_ === newValue) {
                this.value_ = undefined;
            }
            try {
                return origSetValue.call(this, newValue);
            } finally {
                pendingSetValue = null;
            }
        };

        // Wrap showEditor_: clear the cache so clicking the dropdown always
        // regenerates options from the current sample data.
        var origShowEditor = field.showEditor_;
        field.showEditor_ = function (e) {
            this.generatedOptions = null;
            return origShowEditor.call(this, e);
        };

        // Tag so setData can find and refresh all data dropdowns.
        field.isDataDropdown_ = true;

        field.setValue(defaultValue);
        return field;
    }

    // ══════════════════════════════════════════════════════════════════
    // BLOCK DEFINITIONS
    // ══════════════════════════════════════════════════════════════════

    // ── Values (use Blockly built-ins: math_number, text, logic_boolean) ──
    // Override logic_boolean so its output type includes 'Value' (allowing
    // it to connect to VALUE_TYPES inputs) while keeping 'Boolean' first
    // so Zelos still renders it as a hexagonal block.
    Blockly.Blocks['logic_boolean'] = {
        init: function () {
            this.appendDummyInput()
                .appendField(new Blockly.FieldDropdown([
                    ['true', 'TRUE'],
                    ['false', 'FALSE'],
                ]), 'BOOL');
            this.setOutput(true, ['Boolean', 'Value']);
            this.setColour(COLOUR_LOGIC);
            this.setTooltip('A boolean true or false value');
        }
    };

    // Override built-in text block to use our purple colour
    Blockly.Blocks['text'] = {
        init: function () {
            this.appendDummyInput()
                .appendField('"')
                .appendField(new Blockly.FieldTextInput(''), 'TEXT')
                .appendField('"');
            this.setOutput(true, 'String');
            this.setColour(COLOUR_TEXT);
            this.setTooltip('A text string value');
        }
    };

    // ── jsonlogic_var: data access ──
    // The PATH socket accepts only PathSegment-typed blocks.
    Blockly.Blocks['jsonlogic_var'] = {
        init: function () {
            this.appendDummyInput()
                .appendField('var');
            this.appendValueInput('PATH')
                .setCheck('PathSegment');
            this.setOutput(true, 'Value');
            this.setColour(COLOUR_DATA);
            this.setInputsInline(true);
            this.setTooltip('Get a value from the data by path');
        }
    };

    // ── jsonlogic_var_current: current element in map/filter/reduce ──
    Blockly.Blocks['jsonlogic_var_current'] = {
        init: function () {
            this.appendDummyInput()
                .appendField('current item');
            this.setOutput(true, 'Value');
            this.setColour(COLOUR_DATA);
            this.setTooltip('The current element — emits var "" in map/filter, var "current" in reduce');
        }
    };

    // ── jsonlogic_var_reduce_accumulator: accumulator inside reduce ──
    Blockly.Blocks['jsonlogic_var_reduce_accumulator'] = {
        init: function () {
            this.appendDummyInput()
                .appendField('accumulator');
            this.setOutput(true, 'Value');
            this.setColour(COLOUR_DATA);
            this.setTooltip('The running accumulator inside a reduce expression (var "accumulator")');
        }
    };

    // ── jsonlogic_var_default: value with fallback ──
    // PATH socket accepts only PathSegment-typed blocks.
    Blockly.Blocks['jsonlogic_var_default'] = {
        init: function () {
            this.appendDummyInput()
                .appendField('var');
            this.appendValueInput('PATH')
                .setCheck('PathSegment');
            this.appendValueInput('DEFAULT')
                .setCheck(VALUE_TYPES)
                .appendField('if null');
            this.setOutput(true, 'Value');
            this.setColour(COLOUR_DATA);
            this.setInputsInline(true);
            this.setTooltip('Get a value from data by path, with a fallback if null/missing');
        }
    };

    // ── jsonlogic_array_element: path segment for array index (chainable) ──
    Blockly.Blocks['jsonlogic_array_element'] = {
        init: function () {
            this.appendDummyInput()
                .appendField('[')
                .appendField(new Blockly.FieldNumber(0, 0), 'INDEX')
                .appendField(']');
            this.appendValueInput('NEXT')
                .setCheck('PathSegment')
                .appendField('.');
            this.setOutput(true, 'PathSegment');
            this.setColour(COLOUR_PATH);
            this.setInputsInline(true);
            this.setTooltip('Access an array element by index, with further path segments');
        }
    };

    // ── jsonlogic_array_element_end: terminal array index (no NEXT) ──
    Blockly.Blocks['jsonlogic_array_element_end'] = {
        init: function () {
            this.appendDummyInput()
                .appendField('[')
                .appendField(new Blockly.FieldNumber(0, 0), 'INDEX')
                .appendField(']');
            this.setOutput(true, 'PathSegment');
            this.setColour(COLOUR_PATH);
            this.setInputsInline(true);
            this.setTooltip('Access an array element by index');
        }
    };

    // ── jsonlogic_property: path segment for named property (chainable) ──
    Blockly.Blocks['jsonlogic_property'] = {
        init: function () {
            this.appendDummyInput()
                .appendField(createDataDropdown('name', function (block) {
                    var ctx = getPropertyContext(block);
                    if (ctx && typeof ctx === 'object' && !Array.isArray(ctx)) {
                        return Object.keys(ctx);
                    }
                    return [];
                }), 'PROP');
            this.appendValueInput('NEXT')
                .setCheck('PathSegment')
                .appendField('.');
            this.setOutput(true, 'PathSegment');
            this.setColour(COLOUR_PATH);
            this.setInputsInline(true);
            this.setTooltip('Access a named property, with further path segments');
        }
    };

    // ── jsonlogic_property_end: terminal named property (no NEXT) ──
    Blockly.Blocks['jsonlogic_property_end'] = {
        init: function () {
            this.appendDummyInput()
                .appendField(createDataDropdown('name', function (block) {
                    var ctx = getPropertyContext(block);
                    if (ctx && typeof ctx === 'object' && !Array.isArray(ctx)) {
                        return Object.keys(ctx);
                    }
                    return [];
                }), 'PROP');
            this.setOutput(true, 'PathSegment');
            this.setColour(COLOUR_PATH);
            this.setInputsInline(true);
            this.setTooltip('Access a named property');
        }
    };

    // ── jsonlogic_path_text: freeform text path segment (chainable) ──
    Blockly.Blocks['jsonlogic_path_text'] = {
        init: function () {
            this.appendDummyInput()
                .appendField(new Blockly.FieldTextInput('key'), 'SEGMENT');
            this.appendValueInput('NEXT')
                .setCheck('PathSegment')
                .appendField('.');
            this.setOutput(true, 'PathSegment');
            this.setColour(COLOUR_PATH);
            this.setInputsInline(true);
            this.setTooltip('Type a path segment name');
        }
    };

    // ── jsonlogic_path_text_end: freeform text path segment (terminal) ──
    Blockly.Blocks['jsonlogic_path_text_end'] = {
        init: function () {
            this.appendDummyInput()
                .appendField(new Blockly.FieldTextInput('key'), 'SEGMENT');
            this.setOutput(true, 'PathSegment');
            this.setColour(COLOUR_PATH);
            this.setInputsInline(true);
            this.setTooltip('Type a path segment name');
        }
    };

    // ── jsonlogic_if: C-shaped statement block (like Scratch) ──
    Blockly.Blocks['jsonlogic_if'] = {
        init: function () {
            this.appendValueInput('CONDITION')
                .setCheck(VALUE_TYPES)
                .appendField('if');
            this.appendDummyInput()
                .appendField('then');
            this.appendStatementInput('THEN');
            this.appendDummyInput()
                .appendField('else');
            this.appendStatementInput('ELSE');
            this.setPreviousStatement(true);
            this.setColour(COLOUR_CONTROL);
            this.setTooltip('If condition, produce then-value; otherwise produce else-value');
            this.setInputsInline(false);
        }
    };

    // ── jsonlogic_yield: "result [value]" statement wrapper ──
    Blockly.Blocks['jsonlogic_yield'] = {
        init: function () {
            this.appendValueInput('VALUE')
                .setCheck(VALUE_TYPES)
                .appendField('result');
            this.setPreviousStatement(true);
            this.setColour(COLOUR_YIELD);
            this.setTooltip('The value produced by this branch');
        }
    };

    // ── Comparison: == === != !== < <= > >= ──
    Blockly.Blocks['jsonlogic_comparison'] = {
        init: function () {
            this.appendValueInput('LEFT')
                .setCheck(VALUE_TYPES);
            this.appendDummyInput()
                .appendField(new Blockly.FieldDropdown([
                    ['=', '=='],
                    ['≡', '==='],
                    ['≠', '!='],
                    ['≢', '!=='],
                    ['<', '<'],
                    ['≤', '<='],
                    ['>', '>'],
                    ['≥', '>='],
                ]), 'OP');
            this.appendValueInput('RIGHT')
                .setCheck(VALUE_TYPES);
            this.setOutput(true, ['Boolean', 'Value']);
            this.setColour(COLOUR_LOGIC);
            this.setInputsInline(true);
            this.setTooltip('Compare two values');
        }
    };

    // ── Between: low < val < high  or  low <= val <= high ──
    Blockly.Blocks['jsonlogic_between'] = {
        init: function () {
            this.appendValueInput('LOW')
                .setCheck(VALUE_TYPES);
            this.appendDummyInput()
                .appendField(new Blockly.FieldDropdown([
                    ['<', '<'],
                    ['≤', '<='],
                ]), 'OP');
            this.appendValueInput('VALUE')
                .setCheck(VALUE_TYPES);
            this.appendDummyInput()
                .appendField(new Blockly.FieldDropdown([
                    ['<', '<'],
                    ['≤', '<='],
                ]), 'OP2');
            this.appendValueInput('HIGH')
                .setCheck(VALUE_TYPES);
            this.setOutput(true, ['Boolean', 'Value']);
            this.setColour(COLOUR_LOGIC);
            this.setInputsInline(true);
            this.setTooltip('Check if a value is between two bounds');
        }
    };

    // ── Logical AND / OR ──
    Blockly.Blocks['jsonlogic_and'] = {
        init: function () {
            this.appendValueInput('A')
                .setCheck(VALUE_TYPES);
            this.appendDummyInput().appendField('and');
            this.appendValueInput('B')
                .setCheck(VALUE_TYPES);
            this.setOutput(true, ['Boolean', 'Value']);
            this.setColour(COLOUR_LOGIC);
            this.setInputsInline(true);
            this.setTooltip('Logical AND — returns last truthy value or first falsy');
        }
    };

    Blockly.Blocks['jsonlogic_or'] = {
        init: function () {
            this.appendValueInput('A')
                .setCheck(VALUE_TYPES);
            this.appendDummyInput().appendField('or');
            this.appendValueInput('B')
                .setCheck(VALUE_TYPES);
            this.setOutput(true, ['Boolean', 'Value']);
            this.setColour(COLOUR_LOGIC);
            this.setInputsInline(true);
            this.setTooltip('Logical OR — returns first truthy value or last falsy');
        }
    };

    Blockly.Blocks['jsonlogic_not'] = {
        init: function () {
            this.appendValueInput('VALUE')
                .setCheck(VALUE_TYPES)
                .appendField('not');
            this.setOutput(true, ['Boolean', 'Value']);
            this.setColour(COLOUR_LOGIC);
            this.setInputsInline(true);
            this.setTooltip('Logical NOT');
        }
    };

    // ── Truthy (!! double negation) ──
    Blockly.Blocks['jsonlogic_truthy'] = {
        init: function () {
            this.appendValueInput('VALUE')
                .setCheck(VALUE_TYPES)
                .appendField('truthy');
            this.setOutput(true, ['Boolean', 'Value']);
            this.setColour(COLOUR_LOGIC);
            this.setInputsInline(true);
            this.setTooltip('Convert a value to its boolean truthiness (!! operator)');
        }
    };

    // ── Arithmetic ──
    Blockly.Blocks['jsonlogic_arithmetic'] = {
        init: function () {
            this.appendValueInput('LEFT')
                .setCheck(VALUE_TYPES);
            this.appendDummyInput()
                .appendField(new Blockly.FieldDropdown([
                    ['+', '+'],
                    ['−', '-'],
                    ['×', '*'],
                    ['÷', '/'],
                    ['mod', '%'],
                ]), 'OP');
            this.appendValueInput('RIGHT')
                .setCheck(VALUE_TYPES);
            this.setOutput(true, 'Number');
            this.setColour(COLOUR_MATH);
            this.setInputsInline(true);
            this.setTooltip('Arithmetic operation');
        }
    };

    Blockly.Blocks['jsonlogic_min'] = {
        init: function () {
            this.appendValueInput('A').setCheck(VALUE_TYPES).appendField('min(');
            this.appendValueInput('B').setCheck(VALUE_TYPES).appendField(',');
            this.appendDummyInput().appendField(')');
            this.setOutput(true, 'Number');
            this.setColour(COLOUR_MATH);
            this.setInputsInline(true);
        }
    };

    Blockly.Blocks['jsonlogic_max'] = {
        init: function () {
            this.appendValueInput('A').setCheck(VALUE_TYPES).appendField('max(');
            this.appendValueInput('B').setCheck(VALUE_TYPES).appendField(',');
            this.appendDummyInput().appendField(')');
            this.setOutput(true, 'Number');
            this.setColour(COLOUR_MATH);
            this.setInputsInline(true);
        }
    };

    // ── String ──
    Blockly.Blocks['jsonlogic_cat'] = {
        init: function () {
            this.appendValueInput('A').setCheck(VALUE_TYPES).appendField('join');
            this.appendValueInput('B').setCheck(VALUE_TYPES);
            this.setOutput(true, 'String');
            this.setColour(COLOUR_TEXT);
            this.setInputsInline(true);
            this.setTooltip('Concatenate strings');
        }
    };

    Blockly.Blocks['jsonlogic_substr'] = {
        init: function () {
            this.appendValueInput('STR').setCheck(VALUE_TYPES).appendField('substr(');
            this.appendValueInput('START').setCheck(VALUE_TYPES).appendField(',');
            this.appendValueInput('LEN').setCheck(VALUE_TYPES).appendField(',');
            this.appendDummyInput().appendField(')');
            this.setOutput(true, 'String');
            this.setColour(COLOUR_TEXT);
            this.setInputsInline(true);
        }
    };

    // ── Array / membership ──
    Blockly.Blocks['jsonlogic_in'] = {
        init: function () {
            this.appendValueInput('VALUE')
                .setCheck(VALUE_TYPES);
            this.appendValueInput('ARRAY')
                .setCheck(VALUE_TYPES)
                .appendField('in');
            this.setOutput(true, ['Boolean', 'Value']);
            this.setColour(COLOUR_LOGIC);
            this.setInputsInline(true);
            this.setTooltip('Check if value is in array/string');
        }
    };

    Blockly.Blocks['jsonlogic_merge'] = {
        init: function () {
            this.appendValueInput('A').setCheck(VALUE_TYPES).appendField('merge(');
            this.appendValueInput('B').setCheck(VALUE_TYPES).appendField(',');
            this.appendDummyInput().appendField(')');
            this.setOutput(true, 'Array');
            this.setColour(COLOUR_ARRAY);
            this.setInputsInline(true);
        }
    };

    // ── Map / Filter / Reduce ──
    Blockly.Blocks['jsonlogic_map'] = {
        init: function () {
            this.appendValueInput('DATA').setCheck(VALUE_TYPES).appendField('map');
            this.appendValueInput('EXPR').setCheck(VALUE_TYPES).appendField('→');
            this.setOutput(true, 'Array');
            this.setColour(COLOUR_ARRAY);
            this.setInputsInline(true);
        }
    };

    Blockly.Blocks['jsonlogic_filter'] = {
        init: function () {
            this.appendValueInput('DATA').setCheck(VALUE_TYPES).appendField('filter');
            this.appendValueInput('EXPR').setCheck(VALUE_TYPES).appendField('→');
            this.setOutput(true, 'Array');
            this.setColour(COLOUR_ARRAY);
            this.setInputsInline(true);
        }
    };

    Blockly.Blocks['jsonlogic_reduce'] = {
        init: function () {
            this.appendValueInput('DATA').setCheck(VALUE_TYPES).appendField('reduce');
            this.appendValueInput('EXPR').setCheck(VALUE_TYPES).appendField('→');
            this.appendValueInput('INIT').setCheck(VALUE_TYPES).appendField('init');
            this.setOutput(true, 'Value');
            this.setColour(COLOUR_ARRAY);
            this.setInputsInline(true);
        }
    };

    // ── Null literal ──
    Blockly.Blocks['jsonlogic_null'] = {
        init: function () {
            this.appendDummyInput().appendField('null');
            this.setOutput(true, 'Value');
            this.setColour('#888888');
        }
    };

    // ── SVG icons for +/− buttons (inline data URIs) ──
    var ICON_PLUS = 'data:image/svg+xml,' + encodeURIComponent(
        '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">' +
        '<circle cx="12" cy="12" r="11" fill="#fff" opacity="0.3"/>' +
        '<path d="M12 6v12M6 12h12" stroke="#fff" stroke-width="2.5" stroke-linecap="round"/>' +
        '</svg>');
    var ICON_MINUS = 'data:image/svg+xml,' + encodeURIComponent(
        '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">' +
        '<circle cx="12" cy="12" r="11" fill="#fff" opacity="0.3"/>' +
        '<path d="M6 12h12" stroke="#fff" stroke-width="2.5" stroke-linecap="round"/>' +
        '</svg>');

    // ── Array literal (expandable) ──
    Blockly.Blocks['jsonlogic_array'] = {
        init: function () {
            this.itemCount_ = 2;
            this.appendDummyInput('HEADER').appendField('[');
            for (var i = 0; i < this.itemCount_; i++) {
                this.appendValueInput('ITEM_' + i).setCheck(VALUE_TYPES);
            }
            this.appendDummyInput('BUTTONS')
                .appendField(']  ')
                .appendField(new Blockly.FieldImage(ICON_PLUS, 20, 20, '+', function (field) {
                    field.getSourceBlock().addItem_();
                }))
                .appendField(new Blockly.FieldImage(ICON_MINUS, 20, 20, '−', function (field) {
                    field.getSourceBlock().removeItem_();
                }));
            this.setOutput(true, 'Array');
            this.setColour(COLOUR_ARRAY);
            this.setTooltip('A literal array of values');
        },

        addItem_: function () {
            var i = this.itemCount_;
            this.itemCount_++;
            this.appendValueInput('ITEM_' + i).setCheck(VALUE_TYPES);
            this.moveInputBefore('BUTTONS', null);
        },

        removeItem_: function () {
            if (this.itemCount_ <= 0) return;
            this.itemCount_--;
            this.removeInput('ITEM_' + this.itemCount_);
        },

        saveExtraState: function () {
            return { itemCount: this.itemCount_ };
        },

        loadExtraState: function (state) {
            var target = state.itemCount || 0;
            while (this.itemCount_ < target) this.addItem_();
            while (this.itemCount_ > target) this.removeItem_();
        },
    };

    // ── Quantifiers: all, some, none ──
    Blockly.Blocks['jsonlogic_all'] = {
        init: function () {
            this.appendValueInput('DATA').setCheck(VALUE_TYPES).appendField('all');
            this.appendValueInput('EXPR').setCheck(['Boolean']).appendField('→');
            this.setOutput(true, ['Boolean', 'Value']);
            this.setColour(COLOUR_ARRAY);
            this.setInputsInline(true);
            this.setTooltip('True if all elements satisfy the condition');
        }
    };

    Blockly.Blocks['jsonlogic_some'] = {
        init: function () {
            this.appendValueInput('DATA').setCheck(VALUE_TYPES).appendField('some');
            this.appendValueInput('EXPR').setCheck(['Boolean']).appendField('→');
            this.setOutput(true, ['Boolean', 'Value']);
            this.setColour(COLOUR_ARRAY);
            this.setInputsInline(true);
            this.setTooltip('True if at least one element satisfies the condition');
        }
    };

    Blockly.Blocks['jsonlogic_none'] = {
        init: function () {
            this.appendValueInput('DATA').setCheck(VALUE_TYPES).appendField('none');
            this.appendValueInput('EXPR').setCheck(['Boolean']).appendField('→');
            this.setOutput(true, ['Boolean', 'Value']);
            this.setColour(COLOUR_ARRAY);
            this.setInputsInline(true);
            this.setTooltip('True if no elements satisfy the condition');
        }
    };

    // ── Missing (expandable key list) ──
    Blockly.Blocks['jsonlogic_missing'] = {
        init: function () {
            this.itemCount_ = 2;
            this.appendDummyInput('HEADER').appendField('missing');
            for (var i = 0; i < this.itemCount_; i++) {
                this.appendValueInput('ITEM_' + i).setCheck(['String', 'Value']);
            }
            this.appendDummyInput('BUTTONS')
                .appendField(new Blockly.FieldImage(ICON_PLUS, 20, 20, '+', function (field) {
                    field.getSourceBlock().addItem_();
                }))
                .appendField(new Blockly.FieldImage(ICON_MINUS, 20, 20, '−', function (field) {
                    field.getSourceBlock().removeItem_();
                }));
            this.setOutput(true, 'Array');
            this.setColour(COLOUR_DATA);
            this.setTooltip('Returns an array of keys that are missing from the data');
        },

        addItem_: function () {
            var i = this.itemCount_;
            this.itemCount_++;
            this.appendValueInput('ITEM_' + i).setCheck(['String', 'Value']);
            this.moveInputBefore('BUTTONS', null);
        },

        removeItem_: function () {
            if (this.itemCount_ <= 0) return;
            this.itemCount_--;
            this.removeInput('ITEM_' + this.itemCount_);
        },

        saveExtraState: function () {
            return { itemCount: this.itemCount_ };
        },

        loadExtraState: function (state) {
            var target = state.itemCount || 0;
            while (this.itemCount_ < target) this.addItem_();
            while (this.itemCount_ > target) this.removeItem_();
        },
    };

    // ── Missing Some (expandable key list with minimum count) ──
    Blockly.Blocks['jsonlogic_missing_some'] = {
        init: function () {
            this.itemCount_ = 2;
            this.appendDummyInput('HEADER')
                .appendField('require at least')
                .appendField(new Blockly.FieldNumber(1, 0), 'MIN')
                .appendField('of');
            for (var i = 0; i < this.itemCount_; i++) {
                this.appendValueInput('ITEM_' + i).setCheck(['String', 'Value']);
            }
            this.appendDummyInput('BUTTONS')
                .appendField(new Blockly.FieldImage(ICON_PLUS, 20, 20, '+', function (field) {
                    field.getSourceBlock().addItem_();
                }))
                .appendField(new Blockly.FieldImage(ICON_MINUS, 20, 20, '−', function (field) {
                    field.getSourceBlock().removeItem_();
                }));
            this.setOutput(true, 'Array');
            this.setColour(COLOUR_DATA);
            this.setTooltip('Require at least N of these keys to be present in the data. Returns the missing keys if the requirement is not met, or an empty array if it is.');
        },

        addItem_: function () {
            var i = this.itemCount_;
            this.itemCount_++;
            this.appendValueInput('ITEM_' + i).setCheck(['String', 'Value']);
            this.moveInputBefore('BUTTONS', null);
        },

        removeItem_: function () {
            if (this.itemCount_ <= 0) return;
            this.itemCount_--;
            this.removeInput('ITEM_' + this.itemCount_);
        },

        saveExtraState: function () {
            return { itemCount: this.itemCount_ };
        },

        loadExtraState: function (state) {
            var target = state.itemCount || 0;
            while (this.itemCount_ < target) this.addItem_();
            while (this.itemCount_ > target) this.removeItem_();
        },
    };

    // ── Type Conversion ──
    const COLOUR_CONVERT = '#CF63CF'; // pink-purple — type casts

    Blockly.Blocks['jsonlogic_cast'] = {
        init: function () {
            this.appendValueInput('VALUE')
                .setCheck(VALUE_TYPES)
                .appendField(new Blockly.FieldDropdown([
                    ['asDouble', 'asDouble'],
                    ['asLong', 'asLong'],
                    ['asBigNumber', 'asBigNumber'],
                    ['asBigInteger', 'asBigInteger'],
                ]), 'OP');
            this.setOutput(true, 'Number');
            this.setColour(COLOUR_CONVERT);
            this.setInputsInline(true);
            this.setTooltip('Convert a value to a numeric type');
        }
    };

    // ══════════════════════════════════════════════════════════════════
    // TOOLBOX (categories matching Scratch style)
    // ══════════════════════════════════════════════════════════════════

    const TOOLBOX = {
        kind: 'categoryToolbox',
        contents: [
            {
                kind: 'category', name: 'Control', colour: COLOUR_CONTROL,
                contents: [
                    { kind: 'block', type: 'jsonlogic_if' },
                    { kind: 'block', type: 'jsonlogic_yield' },
                ]
            },
            {
                kind: 'category', name: 'Logic', colour: COLOUR_LOGIC,
                contents: [
                    { kind: 'block', type: 'jsonlogic_comparison' },
                    { kind: 'block', type: 'jsonlogic_between' },
                    { kind: 'block', type: 'jsonlogic_and' },
                    { kind: 'block', type: 'jsonlogic_or' },
                    { kind: 'block', type: 'jsonlogic_not' },
                    { kind: 'block', type: 'jsonlogic_truthy' },
                    { kind: 'block', type: 'jsonlogic_in' },
                ]
            },
            {
                kind: 'category', name: 'Math', colour: COLOUR_MATH,
                contents: [
                    { kind: 'block', type: 'math_number' },
                    { kind: 'block', type: 'jsonlogic_arithmetic' },
                    { kind: 'block', type: 'jsonlogic_min' },
                    { kind: 'block', type: 'jsonlogic_max' },
                ]
            },
            {
                kind: 'category', name: 'Text', colour: COLOUR_TEXT,
                contents: [
                    { kind: 'block', type: 'text' },
                    { kind: 'block', type: 'jsonlogic_cat' },
                    { kind: 'block', type: 'jsonlogic_substr' },
                ]
            },
            {
                kind: 'category', name: 'Data', colour: COLOUR_DATA,
                contents: [
                    { kind: 'block', type: 'jsonlogic_var' },
                    { kind: 'block', type: 'jsonlogic_var_default' },
                    { kind: 'block', type: 'jsonlogic_array_element' },
                    { kind: 'block', type: 'jsonlogic_array_element_end' },
                    { kind: 'block', type: 'jsonlogic_property' },
                    { kind: 'block', type: 'jsonlogic_property_end' },
                    { kind: 'block', type: 'jsonlogic_path_text' },
                    { kind: 'block', type: 'jsonlogic_path_text_end' },
                    { kind: 'block', type: 'jsonlogic_var_current' },
                    { kind: 'block', type: 'jsonlogic_var_reduce_accumulator' },
                    { kind: 'block', type: 'jsonlogic_missing' },
                    { kind: 'block', type: 'jsonlogic_missing_some' },
                    { kind: 'block', type: 'logic_boolean' },
                    { kind: 'block', type: 'jsonlogic_null' },
                ]
            },
            {
                kind: 'category', name: 'Arrays', colour: COLOUR_ARRAY,
                contents: [
                    { kind: 'block', type: 'jsonlogic_array' },
                    { kind: 'block', type: 'jsonlogic_merge' },
                    { kind: 'block', type: 'jsonlogic_map' },
                    { kind: 'block', type: 'jsonlogic_filter' },
                    { kind: 'block', type: 'jsonlogic_reduce' },
                    { kind: 'block', type: 'jsonlogic_all' },
                    { kind: 'block', type: 'jsonlogic_some' },
                    { kind: 'block', type: 'jsonlogic_none' },
                ]
            },
            {
                kind: 'category', name: 'Convert', colour: '#CF63CF',
                contents: [
                    { kind: 'block', type: 'jsonlogic_cast' },
                ]
            },
        ]
    };

    // ══════════════════════════════════════════════════════════════════
    // WORKSPACE MANAGEMENT
    // ══════════════════════════════════════════════════════════════════

    let workspace = null;
    let dotNetRef = null;
    let suppressChange = false;
    let changeTimeout = null;
    let resizeObserver = null;

    // Current sample data for context-aware dropdowns
    let currentSampleData = null;

    // Current schema tree for schema-aware dropdowns (replaces data when set)
    let currentSchemaTree = null;

    // Custom operator definitions from Roslyn compilation
    // Array of { name, minArgs, maxArgs, description }
    let customOperatorDefs = [];

    var COLOUR_CUSTOM = '#7B1FA2'; // dark purple for custom operator blocks

    /**
     * Register a Blockly block definition for a custom operator.
     * Fixed-arity operators get fixed value inputs; variadic ones get expandable +/- buttons.
     */
    function registerCustomOperatorBlock(def) {
        var blockType = 'jsonlogic_custom_' + def.name;
        var isFixed = def.maxArgs !== null && def.minArgs === def.maxArgs;

        if (isFixed) {
            // Fixed-arity block
            Blockly.Blocks[blockType] = {
                init: function () {
                    this.appendDummyInput('LABEL').appendField(def.name);
                    for (var i = 0; i < def.minArgs; i++) {
                        this.appendValueInput('ARG_' + i).setCheck(VALUE_TYPES);
                    }
                    this.setOutput(true, 'Value');
                    this.setColour(COLOUR_CUSTOM);
                    this.setInputsInline(true);
                    this.setTooltip(def.description || ('Custom operator: ' + def.name));
                }
            };
        } else {
            // Variadic block with +/- buttons
            var minArgs = def.minArgs || 0;
            Blockly.Blocks[blockType] = {
                init: function () {
                    this.itemCount_ = Math.max(minArgs, 1);
                    this.appendDummyInput('HEADER').appendField(def.name);
                    for (var i = 0; i < this.itemCount_; i++) {
                        this.appendValueInput('ARG_' + i).setCheck(VALUE_TYPES);
                    }
                    this.appendDummyInput('BUTTONS')
                        .appendField(new Blockly.FieldImage(ICON_PLUS, 20, 20, '+', function (field) {
                            field.getSourceBlock().addItem_();
                        }))
                        .appendField(new Blockly.FieldImage(ICON_MINUS, 20, 20, '−', function (field) {
                            field.getSourceBlock().removeItem_();
                        }));
                    this.setOutput(true, 'Value');
                    this.setColour(COLOUR_CUSTOM);
                    this.setInputsInline(true);
                    this.setTooltip(def.description || ('Custom operator: ' + def.name));
                },
                addItem_: function () {
                    var max = def.maxArgs;
                    if (max !== null && this.itemCount_ >= max) return;
                    this.removeInput('BUTTONS');
                    this.appendValueInput('ARG_' + this.itemCount_).setCheck(VALUE_TYPES);
                    this.itemCount_++;
                    this.appendDummyInput('BUTTONS')
                        .appendField(new Blockly.FieldImage(ICON_PLUS, 20, 20, '+', function (field) {
                            field.getSourceBlock().addItem_();
                        }))
                        .appendField(new Blockly.FieldImage(ICON_MINUS, 20, 20, '−', function (field) {
                            field.getSourceBlock().removeItem_();
                        }));
                },
                removeItem_: function () {
                    if (this.itemCount_ <= (def.minArgs || 0)) return;
                    this.removeInput('BUTTONS');
                    this.itemCount_--;
                    this.removeInput('ARG_' + this.itemCount_);
                    this.appendDummyInput('BUTTONS')
                        .appendField(new Blockly.FieldImage(ICON_PLUS, 20, 20, '+', function (field) {
                            field.getSourceBlock().addItem_();
                        }))
                        .appendField(new Blockly.FieldImage(ICON_MINUS, 20, 20, '−', function (field) {
                            field.getSourceBlock().removeItem_();
                        }));
                },
            };
        }
    }

    /**
     * Build the full toolbox definition, including a Custom category if any custom operators are defined.
     */
    function buildToolboxWithCustomOps() {
        var categories = TOOLBOX.contents.filter(function (c) {
            return c.name !== 'Custom';
        });

        if (customOperatorDefs.length > 0) {
            var customContents = [];
            for (var i = 0; i < customOperatorDefs.length; i++) {
                customContents.push({
                    kind: 'block',
                    type: 'jsonlogic_custom_' + customOperatorDefs[i].name,
                });
            }
            categories.push({
                kind: 'category',
                name: 'Custom',
                colour: COLOUR_CUSTOM,
                contents: customContents,
            });
        }

        return { kind: 'categoryToolbox', contents: categories };
    }

    /**
     * Find JSON pointer paths for all instances of a custom operator in the current rule.
     * Uses the JSON Logic output (same depth-first order the evaluator uses).
     */
    function findBlockPaths(ws, operatorName) {
        var paths = [];
        var topBlocks = ws.getTopBlocks(true);
        if (topBlocks.length === 0) return paths;

        // Build JSON Logic from the workspace, then find all paths of the operator
        var json = blockToJsonLogic(topBlocks[0]);
        if (json) {
            findOpPathsRecursive(json, '', operatorName, paths);
        }
        return paths;
    }

    function findOpPathsRecursive(node, currentPath, opName, paths) {
        if (node === null || typeof node !== 'object') return;
        if (Array.isArray(node)) {
            for (var i = 0; i < node.length; i++) {
                findOpPathsRecursive(node[i], currentPath + '/' + i, opName, paths);
            }
        } else {
            var keys = Object.keys(node);
            for (var k = 0; k < keys.length; k++) {
                var key = keys[k];
                var propPath = currentPath + '/' + key;
                if (key === opName) {
                    paths.push(propPath);
                }
                findOpPathsRecursive(node[key], propPath, opName, paths);
            }
        }
    }

    /**
     * Initialize the Blockly workspace.
     * @param {string} elementId - The div to inject Blockly into.
     * @param {object} dnRef - .NET DotNetObjectReference for callbacks.
     */
    window.BlocklyJsonLogic = {
        init: function (elementId, dnRef) {
            dotNetRef = dnRef;
            const container = document.getElementById(elementId);
            if (!container) {
                console.error('BlocklyJsonLogic: container not found:', elementId);
                return;
            }

            workspace = Blockly.inject(elementId, {
                toolbox: TOOLBOX,
                renderer: 'zelos',         // Scratch-style renderer
                theme: darkTheme,              // Default to dark; synced by setTheme()
                grid: { spacing: 20, length: 3, colour: '#444', snap: true },
                zoom: { controls: true, wheel: true, startScale: 1.0, maxScale: 3, minScale: 0.3, scaleSpeed: 1.2 },
                trashcan: true,
                move: { scrollbars: true, drag: true, wheel: true },
            });

            // Auto-resize when container changes size (maximize/minimize, window resize)
            resizeObserver = new ResizeObserver(function () {
                Blockly.svgResize(workspace);
            });
            resizeObserver.observe(container);

            // Listen for changes with debounce
            workspace.addChangeListener(function (event) {
                if (suppressChange) return;
                // Only react to block changes, not UI events like scrolling
                if (event.isUiEvent) return;

                // When a field changes or blocks are reconnected, refresh all
                // data dropdown labels (the ⚠ prefix may appear/disappear).
                if (event.type === Blockly.Events.BLOCK_CHANGE
                    || event.type === Blockly.Events.BLOCK_MOVE) {
                    if (_refreshTimeout) clearTimeout(_refreshTimeout);
                    _refreshTimeout = setTimeout(refreshDataDropdowns, 50);
                }

                if (changeTimeout) clearTimeout(changeTimeout);
                changeTimeout = setTimeout(function () {
                    if (dotNetRef) {
                        var json = BlocklyJsonLogic.getJsonLogic();
                        dotNetRef.invokeMethodAsync('OnBlocklyChanged', json);
                    }
                }, 300);
            });
        },

        /**
         * Resize the workspace (call after container becomes visible).
         */
        resize: function () {
            if (workspace) {
                Blockly.svgResize(workspace);
            }
        },

        /**
         * Update the sample data used for context-aware dropdowns.
         * @param {string} jsonString - The sample data JSON.
         */
        setData: function (jsonString) {
            try {
                currentSampleData = jsonString ? JSON.parse(jsonString) : null;
            } catch (e) {
                currentSampleData = null;
            }
            refreshDataDropdowns();
        },

        /**
         * Update the schema tree used for schema-aware dropdowns.
         * When set, schema replaces data as the dropdown source.
         * @param {string|null} schemaTreeJson - JSON string of SchemaNode tree, or null to clear.
         */
        setSchema: function (schemaTreeJson) {
            try {
                currentSchemaTree = schemaTreeJson ? JSON.parse(schemaTreeJson) : null;
            } catch (e) {
                currentSchemaTree = null;
            }
            refreshDataDropdowns();
        },

        /**
         * Set custom operators from compiled C# code.
         * @param {string} operatorsJson - JSON array of {name, minArgs, maxArgs, description}
         */
        setCustomOperators: function (operatorsJson) {
            var newDefs;
            try {
                newDefs = JSON.parse(operatorsJson);
            } catch (e) {
                newDefs = [];
            }

            // Remove old custom block definitions
            for (var i = 0; i < customOperatorDefs.length; i++) {
                var oldName = 'jsonlogic_custom_' + customOperatorDefs[i].name;
                delete Blockly.Blocks[oldName];
            }

            customOperatorDefs = newDefs || [];

            // Register new custom block definitions
            for (var i = 0; i < customOperatorDefs.length; i++) {
                registerCustomOperatorBlock(customOperatorDefs[i]);
            }

            // Rebuild toolbox with updated custom category
            if (workspace) {
                workspace.updateToolbox(buildToolboxWithCustomOps());
            }
        },

        /**
         * Highlight blocks belonging to a custom operator that caused an error.
         * Pass null to clear highlighting.
         * @param {string|null} operatorName - The operator name (e.g. "clamp") or null to clear.
         * @param {string} pathsJson - JSON array of JSON Pointer paths to the error location(s).
         */
        highlightErrorOperator: function (operatorName, pathsJson, errorMessage) {
            if (!workspace) return;

            // Clear previous error highlights
            if (workspace._errorHighlightedBlocks) {
                for (var i = 0; i < workspace._errorHighlightedBlocks.length; i++) {
                    var block = workspace._errorHighlightedBlocks[i];
                    if (block && !block.isDeadOrDying()) {
                        block.setWarningText(null);
                        block.removeSelect();
                    }
                }
                workspace._errorHighlightedBlocks = null;
            }

            if (!operatorName) return;

            var paths = [];
            try { paths = JSON.parse(pathsJson); } catch (e) { /* ignore */ }

            var blockType = 'jsonlogic_custom_' + operatorName;
            var allBlocks = workspace.getBlocksByType(blockType, false);

            var toHighlight;
            if (paths.length > 0 && allBlocks.length > 1) {
                // Match specific blocks by depth-first index
                // The paths contain the operator name as the last segment;
                // the blocks array is in workspace order which matches depth-first.
                // We find which depth-first indices are in the paths list.
                var allPaths = findBlockPaths(workspace, operatorName);
                toHighlight = [];
                for (var i = 0; i < allPaths.length; i++) {
                    for (var j = 0; j < paths.length; j++) {
                        if (allPaths[i] === paths[j]) {
                            if (i < allBlocks.length) {
                                toHighlight.push(allBlocks[i]);
                            }
                            break;
                        }
                    }
                }
                if (toHighlight.length === 0) toHighlight = allBlocks;
            } else {
                toHighlight = allBlocks;
            }

            if (toHighlight.length > 0) {
                workspace._errorHighlightedBlocks = toHighlight;
                for (var i = 0; i < toHighlight.length; i++) {
                    toHighlight[i].setWarningText(errorMessage || 'Runtime error in this operator');
                }
                // Visually highlight and scroll the first one into view
                toHighlight[0].addSelect();
                toHighlight[0].select();
            }
        },

        /**
         * Dispose the workspace.
         */
        dispose: function () {
            if (resizeObserver) {
                resizeObserver.disconnect();
                resizeObserver = null;
            }
            if (workspace) {
                workspace.dispose();
                workspace = null;
            }
            dotNetRef = null;
        },

        /**
         * Set the theme (dark/light).
         */
        setTheme: function (isDark) {
            if (workspace) {
                workspace.setTheme(isDark ? darkTheme : lightTheme);
            }
        },

        // ══════════════════════════════════════════════════════════════
        // WORKSPACE → JSON LOGIC
        // ══════════════════════════════════════════════════════════════

        getJsonLogic: function () {
            if (!workspace) return '';
            var topBlocks = workspace.getTopBlocks(true);
            if (topBlocks.length === 0) return '';
            // Take the first top-level block as the root expression
            var root = topBlocks[0];
            var result = blockToJsonLogic(root);
            return JSON.stringify(result, null, 2);
        },

        // ══════════════════════════════════════════════════════════════
        // JSON LOGIC → WORKSPACE
        // ══════════════════════════════════════════════════════════════

        loadJsonLogic: function (jsonString, dataJsonString) {
            if (!workspace) return;
            // Update sample data before building blocks so dropdowns have context
            if (dataJsonString !== undefined && dataJsonString !== null) {
                try { currentSampleData = JSON.parse(dataJsonString); }
                catch (e) { currentSampleData = null; }
            }
            suppressChange = true;
            Blockly.Events.disable();
            try {
                workspace.clear();
                if (!jsonString || jsonString.trim() === '') return;
                var logic = JSON.parse(jsonString);
                var block = jsonLogicToBlock(logic, workspace);
                if (block) {
                    block.moveBy(20, 20);
                    block.initSvg();
                    block.render();
                }
            } catch (e) {
                console.warn('BlocklyJsonLogic.loadJsonLogic error:', e);
            } finally {
                Blockly.Events.enable();
                suppressChange = false;
            }
            // Refresh dropdowns now that the full block tree is connected
            refreshDataDropdowns();
        },
    };

    // ══════════════════════════════════════════════════════════════════
    // BLOCK → JSON LOGIC conversion
    // ══════════════════════════════════════════════════════════════════

    function blockToJsonLogic(block) {
        if (!block) return null;
        var type = block.type;

        switch (type) {
            case 'math_number':
                return block.getFieldValue('NUM') * 1;

            case 'text':
                return block.getFieldValue('TEXT');

            case 'logic_boolean':
                return block.getFieldValue('BOOL') === 'TRUE';

            case 'jsonlogic_null':
                return null;

            case 'jsonlogic_var': {
                var pathBlock = block.getInput('PATH').connection
                    && block.getInput('PATH').connection.targetBlock();
                var path = pathBlock ? collectPathChain(pathBlock) : '';
                return { 'var': path };
            }

            case 'jsonlogic_var_default': {
                var pathBlock = block.getInput('PATH').connection
                    && block.getInput('PATH').connection.targetBlock();
                var path = pathBlock ? collectPathChain(pathBlock) : '';
                var def = valueInput(block, 'DEFAULT');
                return { 'var': [path, def] };
            }

            case 'jsonlogic_array_element':
            case 'jsonlogic_array_element_end':
            case 'jsonlogic_property':
            case 'jsonlogic_property_end':
            case 'jsonlogic_path_text':
            case 'jsonlogic_path_text_end': {
                // Path segment blocks at top level — wrap as var
                return { 'var': collectPathChain(block) };
            }

            case 'jsonlogic_var_current': {
                // Inside a reduce's EXPR input, emit "current"; otherwise emit ""
                return { 'var': isInsideReduce(block) ? 'current' : '' };
            }

            case 'jsonlogic_var_reduce_accumulator': {
                return { 'var': 'accumulator' };
            }

            case 'jsonlogic_if': {
                var condition = valueInput(block, 'CONDITION');
                var thenVal = statementToValue(block, 'THEN');
                var elseVal = statementToValue(block, 'ELSE');
                var args = [condition || null];
                if (thenVal !== undefined) args.push(thenVal);
                else args.push(null);
                // Flatten nested if in else position into chained if array
                if (elseVal !== undefined) {
                    if (isIfExpression(elseVal)) {
                        args = args.concat(elseVal['if']);
                    } else {
                        args.push(elseVal);
                    }
                }
                return { 'if': args };
            }

            case 'jsonlogic_yield': {
                return valueInput(block, 'VALUE');
            }

            case 'jsonlogic_comparison': {
                var op = block.getFieldValue('OP');
                var left = valueInput(block, 'LEFT');
                var right = valueInput(block, 'RIGHT');
                var result = {};
                result[op] = [left, right];
                return result;
            }

            case 'jsonlogic_between': {
                // Both OP and OP2 should be the same for valid JSON Logic 3-arg form.
                // We use OP for the emitted operator.
                var op = block.getFieldValue('OP');
                var low = valueInput(block, 'LOW');
                var val = valueInput(block, 'VALUE');
                var high = valueInput(block, 'HIGH');
                var result = {};
                result[op] = [low, val, high];
                return result;
            }

            case 'jsonlogic_and': {
                var a = valueInput(block, 'A');
                var b = valueInput(block, 'B');
                // Flatten nested and: and(a, and(b, c)) → [a, b, c]
                var args = [a];
                if (b && typeof b === 'object' && b.and) {
                    args = args.concat(b.and);
                } else {
                    args.push(b);
                }
                return { 'and': args };
            }

            case 'jsonlogic_or': {
                var a = valueInput(block, 'A');
                var b = valueInput(block, 'B');
                var args = [a];
                if (b && typeof b === 'object' && b.or) {
                    args = args.concat(b.or);
                } else {
                    args.push(b);
                }
                return { 'or': args };
            }

            case 'jsonlogic_not': {
                var val = valueInput(block, 'VALUE');
                return { '!': [val] };
            }

            case 'jsonlogic_truthy': {
                var val = valueInput(block, 'VALUE');
                return { '!!': [val] };
            }

            case 'jsonlogic_arithmetic': {
                var op = block.getFieldValue('OP');
                var left = valueInput(block, 'LEFT');
                var right = valueInput(block, 'RIGHT');
                var result = {};
                result[op] = [left, right];
                return result;
            }

            case 'jsonlogic_min': {
                return { 'min': [valueInput(block, 'A'), valueInput(block, 'B')] };
            }
            case 'jsonlogic_max': {
                return { 'max': [valueInput(block, 'A'), valueInput(block, 'B')] };
            }

            case 'jsonlogic_cat': {
                return { 'cat': [valueInput(block, 'A'), valueInput(block, 'B')] };
            }

            case 'jsonlogic_substr': {
                var args = [valueInput(block, 'STR'), valueInput(block, 'START')];
                var len = valueInput(block, 'LEN');
                if (len !== null && len !== undefined) args.push(len);
                return { 'substr': args };
            }

            case 'jsonlogic_in': {
                return { 'in': [valueInput(block, 'VALUE'), valueInput(block, 'ARRAY')] };
            }

            case 'jsonlogic_merge': {
                return { 'merge': [valueInput(block, 'A'), valueInput(block, 'B')] };
            }

            case 'jsonlogic_map': {
                return { 'map': [valueInput(block, 'DATA'), valueInput(block, 'EXPR')] };
            }
            case 'jsonlogic_filter': {
                return { 'filter': [valueInput(block, 'DATA'), valueInput(block, 'EXPR')] };
            }
            case 'jsonlogic_reduce': {
                return { 'reduce': [valueInput(block, 'DATA'), valueInput(block, 'EXPR'), valueInput(block, 'INIT')] };
            }

            case 'jsonlogic_array': {
                var items = [];
                for (var i = 0; i < block.itemCount_; i++) {
                    items.push(valueInput(block, 'ITEM_' + i));
                }
                return items;
            }

            case 'jsonlogic_all': {
                return { 'all': [valueInput(block, 'DATA'), valueInput(block, 'EXPR')] };
            }
            case 'jsonlogic_some': {
                return { 'some': [valueInput(block, 'DATA'), valueInput(block, 'EXPR')] };
            }
            case 'jsonlogic_none': {
                return { 'none': [valueInput(block, 'DATA'), valueInput(block, 'EXPR')] };
            }

            case 'jsonlogic_missing': {
                var keys = [];
                for (var i = 0; i < block.itemCount_; i++) {
                    var v = valueInput(block, 'ITEM_' + i);
                    if (v !== null) keys.push(v);
                }
                return { 'missing': keys };
            }

            case 'jsonlogic_missing_some': {
                var min = block.getFieldValue('MIN') * 1;
                var keys = [];
                for (var i = 0; i < block.itemCount_; i++) {
                    var v = valueInput(block, 'ITEM_' + i);
                    if (v !== null) keys.push(v);
                }
                return { 'missing_some': [min, keys] };
            }

            case 'jsonlogic_cast': {
                var op = block.getFieldValue('OP');
                var val = valueInput(block, 'VALUE');
                var result = {};
                result[op] = [val];
                return result;
            }

            default: {
                // Check if this is a custom operator block
                if (type.startsWith('jsonlogic_custom_')) {
                    var opName = type.substring('jsonlogic_custom_'.length);
                    var args = [];
                    var argIdx = 0;
                    while (block.getInput('ARG_' + argIdx)) {
                        args.push(valueInput(block, 'ARG_' + argIdx));
                        argIdx++;
                    }
                    var result = {};
                    result[opName] = args;
                    return result;
                }
                console.warn('Unknown block type:', type);
                return null;
            }
        }
    }

    /** Get the JSON Logic value from a value input connection. */
    function valueInput(block, name) {
        var input = block.getInput(name);
        if (!input) return null;
        var target = input.connection && input.connection.targetBlock();
        if (!target) return null;
        return blockToJsonLogic(target);
    }

    /** Helper: is this block type a property path segment (chainable or terminal)? */
    function isPropertyBlock(type) {
        return type === 'jsonlogic_property' || type === 'jsonlogic_property_end'
            || type === 'jsonlogic_path_text' || type === 'jsonlogic_path_text_end';
    }

    /** Helper: is this block type an array-element path segment (chainable or terminal)? */
    function isArrayElementBlock(type) {
        return type === 'jsonlogic_array_element' || type === 'jsonlogic_array_element_end';
    }

    /** Walk FORWARD through a chain of property/array_element blocks linked
     *  via NEXT inputs, collecting path segments into a dotted string.
     *  Terminal (_end) blocks have no NEXT and end the chain.
     *  @param {Blockly.Block} block - The first path-segment block.
     *  @returns {string} The dotted path, e.g. "order.total". */
    function collectPathChain(block) {
        var segments = [];
        var current = block;
        while (current) {
            if (isPropertyBlock(current.type)) {
                // PROP for dropdown blocks, SEGMENT for text blocks
                var val = current.getFieldValue('PROP') || current.getFieldValue('SEGMENT');
                segments.push(val);
            } else if (isArrayElementBlock(current.type)) {
                segments.push(String(current.getFieldValue('INDEX') * 1));
            } else {
                break;
            }
            var next = current.getInput('NEXT');
            current = next && next.connection && next.connection.targetBlock();
        }
        return segments.join('.');
    }

    /** Set of block types that are array iterators with DATA + EXPR inputs.
     *  Inside EXPR, var references resolve against the current array element,
     *  not the root data object. */
    var ITERATOR_BLOCK_TYPES = {
        'jsonlogic_map': true,
        'jsonlogic_filter': true,
        'jsonlogic_reduce': true,
        'jsonlogic_all': true,
        'jsonlogic_some': true,
        'jsonlogic_none': true,
    };

    /** Determine the data context for a property block's dropdown by walking
     *  UP through parent blocks.  Each ancestor property/array_element adds
     *  a segment; the walk stops at a jsonlogic_var or jsonlogic_var_default.
     *  When a schema tree is set, uses schema for property names instead of data.
     *  @param {Blockly.Block} block - The property block whose context we want.
     *  @returns {*} An object whose keys are the valid property names at this position. */
    function getPropertyContext(block) {
        // Collect path segments by walking up through parents
        var segments = [];
        var current = block;
        while (current) {
            var parent = null;
            try { parent = current.getParent(); } catch (e) { break; }
            if (!parent) break;

            if (parent.type === 'jsonlogic_var'
                || parent.type === 'jsonlogic_var_default') {
                // Reached the var wrapper — now check if it's inside an iterator's EXPR
                var iteratorCtx = getIteratorElementContext(parent);
                if (iteratorCtx !== undefined) {
                    // Resolve path segments against the iterator element context
                    if (currentSchemaTree && iteratorCtx.__schemaNode) {
                        return resolveSchemaContext(segments, iteratorCtx.__schemaNode);
                    }
                    if (segments.length === 0) return iteratorCtx;
                    return resolveDataPathFrom(iteratorCtx, segments);
                }
                break; // not inside an iterator — use root context
            }
            if (isPropertyBlock(parent.type)) {
                segments.unshift(parent.getFieldValue('PROP') || parent.getFieldValue('SEGMENT'));
            } else if (isArrayElementBlock(parent.type)) {
                segments.unshift(String(parent.getFieldValue('INDEX') * 1));
            } else {
                break;
            }
            current = parent;
        }

        // Schema-driven: navigate the schema tree
        if (currentSchemaTree) {
            return resolveSchemaContext(segments);
        }

        // Data-driven fallback
        if (!currentSampleData) return undefined;
        if (segments.length === 0) return currentSampleData;
        return resolveDataPath(segments.join('.'));
    }

    /** Walk UP from a var block to find the nearest enclosing iterator block
     *  (filter, map, reduce, all, some, none).  If the var is inside the
     *  iterator's EXPR input, resolve the DATA input to find the array and
     *  return the first element as the context for property suggestions.
     *  @param {Blockly.Block} varBlock - The jsonlogic_var or jsonlogic_var_default block.
     *  @returns {*} The element context, or undefined if not inside an iterator EXPR. */
    function getIteratorElementContext(varBlock) {
        var child = varBlock;
        var ancestor = null;
        try { ancestor = child.getParent(); } catch (e) { return undefined; }

        while (ancestor) {
            if (ITERATOR_BLOCK_TYPES[ancestor.type]) {
                // Check: is the child connected to the EXPR input (not DATA)?
                var exprInput = ancestor.getInput('EXPR');
                if (exprInput && exprInput.connection) {
                    var exprTarget = exprInput.connection.targetBlock();
                    if (isDescendantOf(child, exprTarget)) {
                        // We're inside EXPR — resolve DATA to find the array
                        return resolveIteratorDataContext(ancestor);
                    }
                }
                // Connected to DATA, not EXPR — stop looking
                return undefined;
            }

            // Keep walking up
            child = ancestor;
            try { ancestor = ancestor.getParent(); } catch (e) { break; }
        }

        return undefined;
    }

    /** Check if block is the same as or a descendant of root. */
    function isDescendantOf(block, root) {
        if (!root || !block) return false;
        var current = block;
        while (current) {
            if (current.id === root.id) return true;
            try { current = current.getParent(); } catch (e) { return false; }
        }
        return false;
    }

    /** Resolve an iterator block's DATA input to find the array value,
     *  then return the first element (for data-driven) or the items
     *  schema node (for schema-driven).
     *  @param {Blockly.Block} iteratorBlock - The filter/map/etc. block.
     *  @returns {*} The element context. */
    function resolveIteratorDataContext(iteratorBlock) {
        var dataInput = iteratorBlock.getInput('DATA');
        if (!dataInput || !dataInput.connection) return undefined;
        var dataBlock = dataInput.connection.targetBlock();
        if (!dataBlock) return undefined;

        // The DATA is typically a var block — extract its path
        var path = extractVarPath(dataBlock);
        if (path === undefined) return undefined;

        // Schema-driven
        if (currentSchemaTree) {
            var segments = path ? path.split('.') : [];
            var node = currentSchemaTree;
            for (var i = 0; i < segments.length; i++) {
                if (!node) return undefined;
                if (node.properties && node.properties[segments[i]]) {
                    node = node.properties[segments[i]];
                } else {
                    return undefined;
                }
            }
            // Navigate into array items
            if (node && node.items) {
                var result = {};
                if (node.items.properties) {
                    for (var key in node.items.properties) {
                        if (node.items.properties.hasOwnProperty(key)) {
                            result[key] = true;
                        }
                    }
                }
                result.__schemaNode = node.items;
                return result;
            }
            return undefined;
        }

        // Data-driven
        if (!currentSampleData) return undefined;
        var arrayVal = path ? resolveDataPath(path) : currentSampleData;
        if (Array.isArray(arrayVal) && arrayVal.length > 0) {
            return arrayVal[0]; // First element as representative
        }
        return undefined;
    }

    /** Extract the dotted var path from a var block tree.
     *  Returns the path string, or '' for bare var, or undefined if not a var. */
    function extractVarPath(block) {
        if (!block) return undefined;
        if (block.type === 'jsonlogic_var' || block.type === 'jsonlogic_var_default') {
            var pathInput = block.getInput('PATH');
            if (pathInput && pathInput.connection) {
                var pathBlock = pathInput.connection.targetBlock();
                if (pathBlock) {
                    return collectPathChain(pathBlock);
                }
            }
            return ''; // bare var (no path)
        }
        return undefined;
    }

    /** Resolve path segments against a given root object (not currentSampleData).
     *  Used for iterator element contexts. */
    function resolveDataPathFrom(root, segments) {
        if (!root || segments.length === 0) return root;
        var val = root;
        for (var i = 0; i < segments.length; i++) {
            if (val === null || val === undefined) return undefined;
            var seg = segments[i];
            if (Array.isArray(val)) {
                var idx = parseInt(seg, 10);
                if (isNaN(idx)) return undefined;
                val = val[idx];
            } else if (typeof val === 'object') {
                val = val[seg];
            } else {
                return undefined;
            }
        }
        return val;
    }

    /** Navigate the schema tree using collected path segments.
     *  Returns a synthetic object whose keys are the property names at this level.
     *  @param {string[]} segments - Path segments to navigate.
     *  @param {object} [startNode] - Optional starting schema node (default: currentSchemaTree). */
    function resolveSchemaContext(segments, startNode) {
        var node = startNode || currentSchemaTree;
        for (var i = 0; i < segments.length; i++) {
            if (!node) return undefined;
            var seg = segments[i];

            // Check if this is a numeric index (array element)
            var idx = parseInt(seg, 10);
            if (!isNaN(idx) && node.items) {
                // Navigate into array items
                node = node.items;
                continue;
            }

            // Navigate into a named property
            if (node.properties && node.properties[seg]) {
                node = node.properties[seg];
            } else {
                return undefined;
            }
        }

        // Return an object whose keys match the schema's property names
        if (node && node.properties) {
            // Return a synthetic object with the property names as keys
            var result = {};
            for (var key in node.properties) {
                if (node.properties.hasOwnProperty(key)) {
                    result[key] = true;
                }
            }
            return result;
        }

        return undefined;
    }

    /** Resolve a dotted path against currentSampleData.
     *  Returns the value at the path, or undefined if not found. */
    function resolveDataPath(path) {
        if (!currentSampleData || !path) return currentSampleData;
        var segments = path.split('.');
        var val = currentSampleData;
        for (var i = 0; i < segments.length; i++) {
            if (val === null || val === undefined) return undefined;
            var seg = segments[i];
            if (Array.isArray(val)) {
                var idx = parseInt(seg, 10);
                if (isNaN(idx)) return undefined;
                val = val[idx];
            } else if (typeof val === 'object') {
                val = val[seg];
            } else {
                return undefined;
            }
        }
        return val;
    }

    /** Get the JSON Logic value from a statement input (unwrap jsonlogic_yield,
     *  or serialize a directly-nested if block). */
    function statementToValue(block, name) {
        var input = block.getInput(name);
        if (!input) return undefined;
        var target = input.connection && input.connection.targetBlock();
        if (!target) return undefined;
        // jsonlogic_yield wraps a value expression
        if (target.type === 'jsonlogic_yield') {
            return valueInput(target, 'VALUE');
        }
        // Directly-nested block (e.g. jsonlogic_if)
        return blockToJsonLogic(target);
    }

    /** Walk up the block tree to check if this block is inside a reduce's EXPR input. */
    function isInsideReduce(block) {
        var b = block.getParent();
        while (b) {
            if (b.type === 'jsonlogic_reduce') return true;
            if (b.type === 'jsonlogic_map' || b.type === 'jsonlogic_filter'
                || b.type === 'jsonlogic_all' || b.type === 'jsonlogic_some'
                || b.type === 'jsonlogic_none') return false;
            b = b.getParent();
        }
        return false;
    }

    // ══════════════════════════════════════════════════════════════════
    // JSON LOGIC → BLOCK conversion
    // ══════════════════════════════════════════════════════════════════

    function jsonLogicToBlock(logic, ws) {
        if (logic === null || logic === undefined) {
            return createBlock(ws, 'jsonlogic_null');
        }
        if (typeof logic === 'number') {
            var b = createBlock(ws, 'math_number');
            b.setFieldValue(String(logic), 'NUM');
            return b;
        }
        if (typeof logic === 'string') {
            var b = createBlock(ws, 'text');
            b.setFieldValue(logic, 'TEXT');
            return b;
        }
        if (typeof logic === 'boolean') {
            var b = createBlock(ws, 'logic_boolean');
            b.setFieldValue(logic ? 'TRUE' : 'FALSE', 'BOOL');
            return b;
        }
        if (Array.isArray(logic)) {
            // Array literal → jsonlogic_array with expandable slots
            var b = createBlock(ws, 'jsonlogic_array');
            // Size the block to match the array length
            while (b.itemCount_ < logic.length) b.addItem_();
            while (b.itemCount_ > logic.length) b.removeItem_();
            for (var i = 0; i < logic.length; i++) {
                connectValue(b, 'ITEM_' + i, jsonLogicToBlock(logic[i], ws));
            }
            return b;
        }

        // Object: single-key operator
        var keys = Object.keys(logic);
        if (keys.length !== 1) {
            console.warn('Multi-key JSON Logic object:', logic);
            return createBlock(ws, 'jsonlogic_null');
        }

        var op = keys[0];
        var args = logic[op];
        if (!Array.isArray(args)) {
            args = [args]; // e.g. {"var": "x"} → ["x"]
        }

        switch (op) {
            case 'var': {
                var path = typeof args[0] === 'string' ? args[0] : String(args[0] || '');
                var hasDefault = args.length >= 2;

                if (path === '' || path === 'current') {
                    return createBlock(ws, 'jsonlogic_var_current');
                }
                if (path === 'accumulator') {
                    return createBlock(ws, 'jsonlogic_var_reduce_accumulator');
                }

                // 2-arg form: var with default
                if (hasDefault) {
                    var b = createBlock(ws, 'jsonlogic_var_default');
                    var chain = buildPathChain(path, ws);
                    if (chain) connectValue(b, 'PATH', chain);
                    connectValue(b, 'DEFAULT', jsonLogicToBlock(args[1], ws));
                    return b;
                }

                // Expand dotted paths into var + path chain
                return buildVarChain(path, ws);
            }

            case 'if': {
                return buildIfBlock(args, ws);
            }

            case '==': case '===': case '!=': case '!==':
            case '<': case '<=': case '>': case '>=': {
                // 3-arg < or <= is "between"
                if ((op === '<' || op === '<=') && args.length === 3) {
                    var b = createBlock(ws, 'jsonlogic_between');
                    b.setFieldValue(op, 'OP');
                    b.setFieldValue(op, 'OP2');
                    connectValue(b, 'LOW', jsonLogicToBlock(args[0], ws));
                    connectValue(b, 'VALUE', jsonLogicToBlock(args[1], ws));
                    connectValue(b, 'HIGH', jsonLogicToBlock(args[2], ws));
                    return b;
                }
                var b = createBlock(ws, 'jsonlogic_comparison');
                b.setFieldValue(op, 'OP');
                if (args[0] !== undefined) connectValue(b, 'LEFT', jsonLogicToBlock(args[0], ws));
                if (args[1] !== undefined) connectValue(b, 'RIGHT', jsonLogicToBlock(args[1], ws));
                return b;
            }

            case 'and': {
                return buildBinaryChain('jsonlogic_and', 'and', args, ws);
            }
            case 'or': {
                return buildBinaryChain('jsonlogic_or', 'or', args, ws);
            }

            case '!': {
                var b = createBlock(ws, 'jsonlogic_not');
                if (args[0] !== undefined) connectValue(b, 'VALUE', jsonLogicToBlock(args[0], ws));
                return b;
            }

            case '+': case '-': case '*': case '/': case '%': {
                var b = createBlock(ws, 'jsonlogic_arithmetic');
                b.setFieldValue(op, 'OP');
                if (args[0] !== undefined) connectValue(b, 'LEFT', jsonLogicToBlock(args[0], ws));
                if (args[1] !== undefined) connectValue(b, 'RIGHT', jsonLogicToBlock(args[1], ws));
                return b;
            }

            case 'min': {
                var b = createBlock(ws, 'jsonlogic_min');
                if (args[0] !== undefined) connectValue(b, 'A', jsonLogicToBlock(args[0], ws));
                if (args[1] !== undefined) connectValue(b, 'B', jsonLogicToBlock(args[1], ws));
                return b;
            }
            case 'max': {
                var b = createBlock(ws, 'jsonlogic_max');
                if (args[0] !== undefined) connectValue(b, 'A', jsonLogicToBlock(args[0], ws));
                if (args[1] !== undefined) connectValue(b, 'B', jsonLogicToBlock(args[1], ws));
                return b;
            }

            case 'cat': {
                var b = createBlock(ws, 'jsonlogic_cat');
                if (args[0] !== undefined) connectValue(b, 'A', jsonLogicToBlock(args[0], ws));
                if (args[1] !== undefined) connectValue(b, 'B', jsonLogicToBlock(args[1], ws));
                return b;
            }

            case 'substr': {
                var b = createBlock(ws, 'jsonlogic_substr');
                if (args[0] !== undefined) connectValue(b, 'STR', jsonLogicToBlock(args[0], ws));
                if (args[1] !== undefined) connectValue(b, 'START', jsonLogicToBlock(args[1], ws));
                if (args[2] !== undefined) connectValue(b, 'LEN', jsonLogicToBlock(args[2], ws));
                return b;
            }

            case 'in': {
                var b = createBlock(ws, 'jsonlogic_in');
                if (args[0] !== undefined) connectValue(b, 'VALUE', jsonLogicToBlock(args[0], ws));
                if (args[1] !== undefined) connectValue(b, 'ARRAY', jsonLogicToBlock(args[1], ws));
                return b;
            }

            case 'merge': {
                var b = createBlock(ws, 'jsonlogic_merge');
                if (args[0] !== undefined) connectValue(b, 'A', jsonLogicToBlock(args[0], ws));
                if (args[1] !== undefined) connectValue(b, 'B', jsonLogicToBlock(args[1], ws));
                return b;
            }

            case 'map': {
                var b = createBlock(ws, 'jsonlogic_map');
                if (args[0] !== undefined) connectValue(b, 'DATA', jsonLogicToBlock(args[0], ws));
                if (args[1] !== undefined) connectValue(b, 'EXPR', jsonLogicToBlock(args[1], ws));
                return b;
            }
            case 'filter': {
                var b = createBlock(ws, 'jsonlogic_filter');
                if (args[0] !== undefined) connectValue(b, 'DATA', jsonLogicToBlock(args[0], ws));
                if (args[1] !== undefined) connectValue(b, 'EXPR', jsonLogicToBlock(args[1], ws));
                return b;
            }
            case 'reduce': {
                var b = createBlock(ws, 'jsonlogic_reduce');
                if (args[0] !== undefined) connectValue(b, 'DATA', jsonLogicToBlock(args[0], ws));
                if (args[1] !== undefined) connectValue(b, 'EXPR', jsonLogicToBlock(args[1], ws));
                if (args[2] !== undefined) connectValue(b, 'INIT', jsonLogicToBlock(args[2], ws));
                return b;
            }

            case '!!': {
                var b = createBlock(ws, 'jsonlogic_truthy');
                if (args[0] !== undefined) connectValue(b, 'VALUE', jsonLogicToBlock(args[0], ws));
                return b;
            }

            case 'all': {
                var b = createBlock(ws, 'jsonlogic_all');
                if (args[0] !== undefined) connectValue(b, 'DATA', jsonLogicToBlock(args[0], ws));
                if (args[1] !== undefined) connectValue(b, 'EXPR', jsonLogicToBlock(args[1], ws));
                return b;
            }
            case 'some': {
                var b = createBlock(ws, 'jsonlogic_some');
                if (args[0] !== undefined) connectValue(b, 'DATA', jsonLogicToBlock(args[0], ws));
                if (args[1] !== undefined) connectValue(b, 'EXPR', jsonLogicToBlock(args[1], ws));
                return b;
            }
            case 'none': {
                var b = createBlock(ws, 'jsonlogic_none');
                if (args[0] !== undefined) connectValue(b, 'DATA', jsonLogicToBlock(args[0], ws));
                if (args[1] !== undefined) connectValue(b, 'EXPR', jsonLogicToBlock(args[1], ws));
                return b;
            }

            case 'missing': {
                var b = createBlock(ws, 'jsonlogic_missing');
                // args is array of key strings
                while (b.itemCount_ < args.length) b.addItem_();
                while (b.itemCount_ > args.length) b.removeItem_();
                for (var i = 0; i < args.length; i++) {
                    connectValue(b, 'ITEM_' + i, jsonLogicToBlock(args[i], ws));
                }
                return b;
            }

            case 'missing_some': {
                var b = createBlock(ws, 'jsonlogic_missing_some');
                // args = [minRequired, [key1, key2, ...]]
                if (args[0] !== undefined) b.setFieldValue(String(args[0]), 'MIN');
                var keyList = (args[1] && Array.isArray(args[1])) ? args[1] : [];
                while (b.itemCount_ < keyList.length) b.addItem_();
                while (b.itemCount_ > keyList.length) b.removeItem_();
                for (var i = 0; i < keyList.length; i++) {
                    connectValue(b, 'ITEM_' + i, jsonLogicToBlock(keyList[i], ws));
                }
                return b;
            }

            case 'asDouble': case 'asLong': case 'asBigNumber': case 'asBigInteger': {
                var b = createBlock(ws, 'jsonlogic_cast');
                b.setFieldValue(op, 'OP');
                if (args[0] !== undefined) connectValue(b, 'VALUE', jsonLogicToBlock(args[0], ws));
                return b;
            }

            default: {
                // Check if this is a custom operator
                var customBlockType = 'jsonlogic_custom_' + op;
                if (Blockly.Blocks[customBlockType]) {
                    var b = createBlock(ws, customBlockType);
                    // Expand variadic blocks if needed
                    if (b.addItem_) {
                        while ((b.itemCount_ || 0) < args.length) b.addItem_();
                    }
                    for (var i = 0; i < args.length; i++) {
                        if (b.getInput('ARG_' + i)) {
                            connectValue(b, 'ARG_' + i, jsonLogicToBlock(args[i], ws));
                        }
                    }
                    return b;
                }
                // Unknown operator — show as null
                console.warn('Unknown JSON Logic operator:', op);
                return createBlock(ws, 'jsonlogic_null');
            }
        }
    }

    /** Build an if block from a flat JSON Logic if-args array. */
    function buildIfBlock(args, ws) {
        // args = [condition, then, ...rest]
        // Nest chained ifs: [c1,v1,c2,v2,else] → if(c1,v1,if(c2,v2,else))
        if (args.length <= 1) {
            // Degenerate: just a condition, no then
            var b = createBlock(ws, 'jsonlogic_if');
            if (args[0] !== undefined) connectValue(b, 'CONDITION', jsonLogicToBlock(args[0], ws));
            return b;
        }

        var condition = args[0];
        var thenVal = args[1];
        var rest = args.slice(2);

        var b = createBlock(ws, 'jsonlogic_if');
        connectValue(b, 'CONDITION', jsonLogicToBlock(condition, ws));

        // Connect then-value: if it's an if, nest directly; otherwise wrap in yield
        connectMouthValue(b, 'THEN', thenVal, ws);

        // Handle else
        if (rest.length === 1) {
            // Simple else value
            connectMouthValue(b, 'ELSE', rest[0], ws);
        } else if (rest.length >= 2) {
            // Chained: rest is [c2, v2, ...] — nest as another if block directly
            var nestedIf = buildIfBlock(rest, ws);
            connectStatement(b, 'ELSE', nestedIf);
        }
        // If rest.length === 0, no else clause

        return b;
    }

    /** Connect a value into a C-block mouth.
     *  If the value is an if-expression, nest the if block directly.
     *  Otherwise wrap in a jsonlogic_yield statement block. */
    function connectMouthValue(parentBlock, mouthName, logic, ws) {
        if (isIfExpression(logic)) {
            // Nested if connects directly — no yield wrapper needed
            var ifBlock = buildIfBlock(logic['if'], ws);
            connectStatement(parentBlock, mouthName, ifBlock);
        } else {
            var yieldBlock = createBlock(ws, 'jsonlogic_yield');
            connectValue(yieldBlock, 'VALUE', jsonLogicToBlock(logic, ws));
            connectStatement(parentBlock, mouthName, yieldBlock);
        }
    }

    /** Check if a JSON Logic value is an if expression. */
    function isIfExpression(val) {
        return val && typeof val === 'object' && !Array.isArray(val) && val['if'] && Array.isArray(val['if']);
    }

    /** Build nested binary chain: and(a, and(b, c)) from [a, b, c]. */
    function buildBinaryChain(blockType, op, args, ws) {
        if (args.length === 0) return createBlock(ws, 'jsonlogic_null');
        if (args.length === 1) return jsonLogicToBlock(args[0], ws);
        if (args.length === 2) {
            var b = createBlock(ws, blockType);
            connectValue(b, 'A', jsonLogicToBlock(args[0], ws));
            connectValue(b, 'B', jsonLogicToBlock(args[1], ws));
            return b;
        }
        // 3+ args: nest right
        var b = createBlock(ws, blockType);
        connectValue(b, 'A', jsonLogicToBlock(args[0], ws));
        connectValue(b, 'B', buildBinaryChain(blockType, op, args.slice(1), ws));
        return b;
    }

    /** Build just the property/array_element chain for a dotted path.
     *  Returns the first path-segment block (or null for empty paths).
     *  Connects all blocks via NEXT, then sets field values so context
     *  is available when dropdown generators run. */
    function buildPathChain(path, ws) {
        if (!path || path === '') return null;
        var segments = path.split('.');
        var blocks = [];

        // 1. Create all path-segment blocks — use _end variant for the last one
        for (var i = 0; i < segments.length; i++) {
            var seg = segments[i];
            var isLast = (i === segments.length - 1);
            if (/^\d+$/.test(seg)) {
                var blockType = isLast ? 'jsonlogic_array_element_end' : 'jsonlogic_array_element';
                blocks.push({ block: createBlock(ws, blockType), seg: seg, isIndex: true });
            } else {
                var blockType = isLast ? 'jsonlogic_property_end' : 'jsonlogic_property';
                blocks.push({ block: createBlock(ws, blockType), seg: seg, isIndex: false });
            }
        }

        // 2. Wire NEXT connections so the chain is fully linked (last block has no NEXT)
        for (var i = 0; i < blocks.length - 1; i++) {
            connectValue(blocks[i].block, 'NEXT', blocks[i + 1].block);
        }

        // 3. Set field values (context is available because chain is connected)
        for (var i = 0; i < blocks.length; i++) {
            var pb = blocks[i];
            if (pb.isIndex) {
                pb.block.setFieldValue(parseInt(pb.seg, 10), 'INDEX');
            } else {
                pb.block.setFieldValue(pb.seg, 'PROP');
            }
        }

        return blocks[0].block;
    }

    /** Expand a dotted var path into a jsonlogic_var block wrapping a
     *  chain of property/array_element blocks linked via NEXT. */
    function buildVarChain(path, ws) {
        var varBlock = createBlock(ws, 'jsonlogic_var');
        var chain = buildPathChain(path, ws);
        if (chain) {
            connectValue(varBlock, 'PATH', chain);
        }
        return varBlock;
    }

    // ── Helpers ──

    function createBlock(ws, type) {
        var block = ws.newBlock(type);
        block.initSvg();
        return block;
    }

    function connectValue(parentBlock, inputName, childBlock) {
        if (!childBlock) return;
        var input = parentBlock.getInput(inputName);
        if (input && input.connection && childBlock.outputConnection) {
            input.connection.connect(childBlock.outputConnection);
        }
    }

    function connectStatement(parentBlock, inputName, childBlock) {
        if (!childBlock) return;
        var input = parentBlock.getInput(inputName);
        if (input && input.connection && childBlock.previousConnection) {
            input.connection.connect(childBlock.previousConnection);
        }
    }

})();

// Monaco Editor language definition for JSONPath (RFC 9535).
// Provides syntax highlighting via a Monarch tokenizer.

/**
 * Registers the "jsonpath" language with a Monaco Editor instance.
 * @param {object} monacoInstance - The global `monaco` object.
 */
export function registerJsonPathLanguage(monacoInstance) {
    monacoInstance.languages.register({ id: 'jsonpath' });

    monacoInstance.languages.setMonarchTokensProvider('jsonpath', {
        functions: [
            'length', 'count', 'match', 'search', 'value',
        ],

        tokenizer: {
            root: [
                // Whitespace
                [/\s+/, 'white'],

                // Double-quoted string
                [/"/, 'string', '@doubleString'],

                // Single-quoted string
                [/'/, 'string', '@singleString'],

                // Numbers (including negative and decimal)
                [/-?\d+(\.\d+)?/, 'number'],

                // Root identifier
                [/\$/, 'keyword'],

                // Current node
                [/@/, 'keyword'],

                // Recursive descent
                [/\.\./, 'keyword'],

                // Multi-character operators (logical)
                [/\|\|/, 'keyword'],
                [/&&/, 'keyword'],

                // Comparison operators
                [/[!=]=/, 'operators'],
                [/[<>]=?/, 'operators'],

                // Not operator
                [/!/, 'keyword'],

                // Dot accessor
                [/\./, 'delimiter'],

                // Wildcard
                [/\*/, 'keyword'],

                // Filter bracket
                [/\[\?/, 'keyword'],

                // Brackets
                [/[[\]()]/, 'delimiter.bracket'],

                // Colon and comma
                [/[:,]/, 'delimiter'],

                // Function names (identifier followed by optional whitespace and open paren)
                [/[A-Za-z_][A-Za-z0-9_]*(?=\s*\()/, {
                    cases: {
                        '@functions': 'keyword',
                        '@default': 'variable',
                    },
                }],

                // Plain identifiers (field references)
                [/[A-Za-z_][A-Za-z0-9_]*/, 'variable'],
            ],

            // Double-quoted string (JSON escapes)
            doubleString: [
                [/[^"\\]+/, 'string'],
                [/\\["\\\/bfnrt]/, 'string.escape'],
                [/\\u[0-9A-Fa-f]{4}/, 'string.escape'],
                [/\\./, 'string'],
                [/"/, 'string', '@pop'],
            ],

            // Single-quoted string (RFC 9535 uses \' as escape)
            singleString: [
                [/[^'\\]+/, 'string'],
                [/\\'/, 'string.escape'],
                [/\\./, 'string'],
                [/'/, 'string', '@pop'],
            ],
        },
    });

    monacoInstance.languages.setLanguageConfiguration('jsonpath', {
        brackets: [
            ['[', ']'],
            ['(', ')'],
        ],
        autoClosingPairs: [
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '"', close: '"', notIn: ['string'] },
            { open: "'", close: "'", notIn: ['string'] },
        ],
        surroundingPairs: [
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '"', close: '"' },
            { open: "'", close: "'" },
        ],
    });
}

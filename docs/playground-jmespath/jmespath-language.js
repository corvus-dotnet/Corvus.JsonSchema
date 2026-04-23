// Monaco Editor language definition for JMESPath.
// Provides syntax highlighting via a Monarch tokenizer.

/**
 * Registers the "jmespath" language with a Monaco Editor instance.
 * @param {object} monacoInstance - The global `monaco` object.
 */
export function registerJMESPathLanguage(monacoInstance) {
    monacoInstance.languages.register({ id: 'jmespath' });

    monacoInstance.languages.setMonarchTokensProvider('jmespath', {
        functions: [
            'abs', 'avg', 'ceil', 'contains', 'ends_with', 'floor',
            'join', 'keys', 'length', 'map', 'max', 'max_by', 'merge',
            'min', 'min_by', 'not_null', 'reverse', 'sort', 'sort_by',
            'starts_with', 'sum', 'to_array', 'to_number', 'to_string',
            'type', 'values',
        ],

        tokenizer: {
            root: [
                // Whitespace
                [/\s+/, 'white'],

                // Backtick-delimited JSON literal
                [/`/, 'string', '@literal'],

                // Raw string (single-quoted); only \' is an escape
                [/'/, 'string', '@rawString'],

                // Quoted identifier (double-quoted, JSON string rules)
                [/"/, 'string', '@quotedIdentifier'],

                // Numbers (including negative)
                [/-?\d+/, 'number'],

                // Current node
                [/@/, 'keyword'],

                // Expression-type reference
                [/&/, 'keyword'],

                // Multi-character operators (logical)
                [/\|\|/, 'keyword'],
                [/&&/, 'keyword'],

                // Comparison operators
                [/[!=]=/, 'operators'],
                [/[<>]=?/, 'operators'],

                // Not operator
                [/!/, 'keyword'],

                // Pipe
                [/\|/, 'operators'],

                // Dot accessor
                [/\./, 'delimiter'],

                // Wildcard
                [/\*/, 'keyword'],

                // Filter bracket
                [/\[\?/, 'keyword'],

                // Flatten
                [/\[\]/, 'delimiter.bracket'],

                // Brackets
                [/[[\](){}]/, 'delimiter.bracket'],

                // Colon and comma
                [/[:,]/, 'delimiter'],

                // Function names (identifier followed by optional whitespace and open paren)
                [/[A-Za-z_][A-Za-z0-9_]*(?=\s*\()/, {
                    cases: {
                        '@functions': 'keyword',
                        '@default': 'variable',
                    },
                }],

                // Hash keys (identifier followed by optional whitespace and colon)
                [/[A-Za-z_][A-Za-z0-9_]*(?=\s*:)/, 'type'],

                // Plain identifiers (field references)
                [/[A-Za-z_][A-Za-z0-9_]*/, 'variable'],
            ],

            // Backtick literal: scan until unescaped backtick
            literal: [
                [/[^`\\]+/, 'string'],
                [/\\`/, 'string.escape'],
                [/\\./, 'string'],
                [/`/, 'string', '@pop'],
            ],

            // Raw string: only \' is special
            rawString: [
                [/[^'\\]+/, 'string'],
                [/\\'/, 'string.escape'],
                [/\\./, 'string'],
                [/'/, 'string', '@pop'],
            ],

            // Quoted identifier (double-quoted string with JSON escapes)
            quotedIdentifier: [
                [/[^"\\]+/, 'string'],
                [/\\["\\\/bfnrt]/, 'string.escape'],
                [/\\u[0-9A-Fa-f]{4}/, 'string.escape'],
                [/\\./, 'string'],
                [/"/, 'string', '@pop'],
            ],
        },
    });

    monacoInstance.languages.setLanguageConfiguration('jmespath', {
        brackets: [
            ['[', ']'],
            ['(', ')'],
            ['{', '}'],
        ],
        autoClosingPairs: [
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '{', close: '}' },
            { open: '"', close: '"', notIn: ['string'] },
            { open: "'", close: "'", notIn: ['string'] },
            { open: '`', close: '`', notIn: ['string'] },
        ],
        surroundingPairs: [
            { open: '[', close: ']' },
            { open: '(', close: ')' },
            { open: '{', close: '}' },
            { open: '"', close: '"' },
            { open: "'", close: "'" },
            { open: '`', close: '`' },
        ],
    });
}

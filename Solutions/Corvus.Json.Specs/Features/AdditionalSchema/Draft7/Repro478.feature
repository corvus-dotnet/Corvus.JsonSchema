@draft7

Feature: Repro 478 draft7

Scenario Outline: Generation error with regular expressions.
	Given a schema file
		"""
		{
			"$schema": "http://json-schema.org/draft-07/schema#",
			"additionalProperties": false,
			"definitions": {
				"CacheFormat": {
					"enum": [
						"legacy",
						"universal"
					],
					"type": "string"
				},
				"CacheSettings": {
					"additionalProperties": false,
					"properties": {
						"cacheFormat": {
							"$ref": "#/definitions/CacheFormat",
							"default": "legacy",
							"description": "Format of the cache file.\n- `legacy` - use absolute paths in the cache file\n- `universal` - use a sharable format.",
							"markdownDescription": "Format of the cache file.\n- `legacy` - use absolute paths in the cache file\n- `universal` - use a sharable format."
						},
						"cacheLocation": {
							"$ref": "#/definitions/FSPathResolvable",
							"description": "Path to the cache location. Can be a file or a directory. If none specified `.cspellcache` will be used. Relative paths are relative to the config file in which it is defined.\n\nA prefix of `${cwd}` is replaced with the current working directory.",
							"markdownDescription": "Path to the cache location. Can be a file or a directory.\nIf none specified `.cspellcache` will be used.\nRelative paths are relative to the config file in which it\nis defined.\n\nA prefix of `${cwd}` is replaced with the current working directory."
						},
						"cacheStrategy": {
							"$ref": "#/definitions/CacheStrategy",
							"default": "metadata",
							"description": "Strategy to use for detecting changed files, default: metadata",
							"markdownDescription": "Strategy to use for detecting changed files, default: metadata"
						},
						"useCache": {
							"default": false,
							"description": "Store the results of processed files in order to only operate on the changed ones.",
							"markdownDescription": "Store the results of processed files in order to only operate on the changed ones.",
							"type": "boolean"
						}
					},
					"type": "object"
				},
				"CacheStrategy": {
					"description": "The Strategy to use to detect if a file has changed.\n- `metadata` - uses the file system timestamp and size to detect changes (fastest).\n- `content` - uses a hash of the file content to check file changes (slower - more accurate).",
					"enum": [
						"metadata",
						"content"
					],
					"markdownDescription": "The Strategy to use to detect if a file has changed.\n- `metadata` - uses the file system timestamp and size to detect changes (fastest).\n- `content` - uses a hash of the file content to check file changes (slower - more accurate).",
					"type": "string"
				},
				"CharacterSet": {
					"description": "This is a set of characters that can include `-` or `|`\n- `-` - indicates a range of characters: `a-c` => `abc`\n- `|` - is a group separator, indicating that the characters on either side    are not related.",
					"markdownDescription": "This is a set of characters that can include `-` or `|`\n- `-` - indicates a range of characters: `a-c` => `abc`\n- `|` - is a group separator, indicating that the characters on either side\n   are not related.",
					"type": "string"
				},
				"CharacterSetCosts": {
					"additionalProperties": false,
					"properties": {
						"characters": {
							"$ref": "#/definitions/CharacterSet",
							"description": "This is a set of characters that can include `-` or `|`\n- `-` - indicates a range of characters: `a-c` => `abc`\n- `|` - is a group separator, indicating that the characters on either side    are not related.",
							"markdownDescription": "This is a set of characters that can include `-` or `|`\n- `-` - indicates a range of characters: `a-c` => `abc`\n- `|` - is a group separator, indicating that the characters on either side\n   are not related."
						},
						"cost": {
							"description": "the cost to insert / delete / replace / swap the characters in a group",
							"markdownDescription": "the cost to insert / delete / replace / swap the characters in a group",
							"type": "number"
						},
						"penalty": {
							"description": "The penalty cost to apply if the accent is used. This is used to discourage",
							"markdownDescription": "The penalty cost to apply if the accent is used.\nThis is used to discourage",
							"type": "number"
						}
					},
					"required": [
						"characters",
						"cost"
					],
					"type": "object"
				},
				"CostMapDefInsDel": {
					"additionalProperties": false,
					"properties": {
						"description": {
							"description": "A description to describe the purpose of the map.",
							"markdownDescription": "A description to describe the purpose of the map.",
							"type": "string"
						},
						"insDel": {
							"description": "The cost to insert/delete one of the substrings in the map. Note: insert/delete costs are symmetrical.",
							"markdownDescription": "The cost to insert/delete one of the substrings in the map. Note: insert/delete costs are symmetrical.",
							"type": "number"
						},
						"map": {
							"description": "The set of substrings to map, these are generally single character strings.\n\nMultiple sets can be defined by using a `|` to separate them.\n\nExample: `\"eéê|aåá\"` contains two different sets.\n\nTo add a multi-character substring use `()`.\n\nExample: `\"f(ph)(gh)\"` results in the following set: `f`, `ph`, `gh`.\n\n- To match the beginning of a word, use `^`: `\"(^I)\"\"`.\n- To match the end of a word, use `$`: `\"(e$)(ing$)\"`.",
							"markdownDescription": "The set of substrings to map, these are generally single character strings.\n\nMultiple sets can be defined by using a `|` to separate them.\n\nExample: `\"eéê|aåá\"` contains two different sets.\n\nTo add a multi-character substring use `()`.\n\nExample: `\"f(ph)(gh)\"` results in the following set: `f`, `ph`, `gh`.\n\n- To match the beginning of a word, use `^`: `\"(^I)\"\"`.\n- To match the end of a word, use `$`: `\"(e$)(ing$)\"`.",
							"type": "string"
						},
						"penalty": {
							"description": "Add a penalty to the final cost. This is used to discourage certain suggestions.\n\nExample: ```yaml # Match adding/removing `-` to the end of a word. map: \"$(-$)\" replace: 50 penalty: 100 ```\n\nThis makes adding a `-` to the end of a word more expensive.\n\nThink of it as taking the toll way for speed but getting the bill later.",
							"markdownDescription": "Add a penalty to the final cost.\nThis is used to discourage certain suggestions.\n\nExample:\n```yaml\n# Match adding/removing `-` to the end of a word.\nmap: \"$(-$)\"\nreplace: 50\npenalty: 100\n```\n\nThis makes adding a `-` to the end of a word more expensive.\n\nThink of it as taking the toll way for speed but getting the bill later.",
							"type": "number"
						},
						"replace": {
							"description": "The cost to replace of of the substrings in the map with another substring in the map. Example: Map['a', 'i'] This would be the cost to substitute `a` with `i`: Like `bat` to `bit` or the reverse.",
							"markdownDescription": "The cost to replace of of the substrings in the map with another substring in the map.\nExample: Map['a', 'i']\nThis would be the cost to substitute `a` with `i`: Like `bat` to `bit` or the reverse.",
							"type": "number"
						},
						"swap": {
							"description": "The cost to swap two adjacent substrings found in the map. Example: Map['e', 'i'] This represents the cost to change `ei` to `ie` or the reverse.",
							"markdownDescription": "The cost to swap two adjacent substrings found in the map.\nExample: Map['e', 'i']\nThis represents the cost to change `ei` to `ie` or the reverse.",
							"type": "number"
						}
					},
					"required": [
						"insDel",
						"map"
					],
					"type": "object"
				},
				"CostMapDefReplace": {
					"additionalProperties": false,
					"properties": {
						"description": {
							"description": "A description to describe the purpose of the map.",
							"markdownDescription": "A description to describe the purpose of the map.",
							"type": "string"
						},
						"insDel": {
							"description": "The cost to insert/delete one of the substrings in the map. Note: insert/delete costs are symmetrical.",
							"markdownDescription": "The cost to insert/delete one of the substrings in the map. Note: insert/delete costs are symmetrical.",
							"type": "number"
						},
						"map": {
							"description": "The set of substrings to map, these are generally single character strings.\n\nMultiple sets can be defined by using a `|` to separate them.\n\nExample: `\"eéê|aåá\"` contains two different sets.\n\nTo add a multi-character substring use `()`.\n\nExample: `\"f(ph)(gh)\"` results in the following set: `f`, `ph`, `gh`.\n\n- To match the beginning of a word, use `^`: `\"(^I)\"\"`.\n- To match the end of a word, use `$`: `\"(e$)(ing$)\"`.",
							"markdownDescription": "The set of substrings to map, these are generally single character strings.\n\nMultiple sets can be defined by using a `|` to separate them.\n\nExample: `\"eéê|aåá\"` contains two different sets.\n\nTo add a multi-character substring use `()`.\n\nExample: `\"f(ph)(gh)\"` results in the following set: `f`, `ph`, `gh`.\n\n- To match the beginning of a word, use `^`: `\"(^I)\"\"`.\n- To match the end of a word, use `$`: `\"(e$)(ing$)\"`.",
							"type": "string"
						},
						"penalty": {
							"description": "Add a penalty to the final cost. This is used to discourage certain suggestions.\n\nExample: ```yaml # Match adding/removing `-` to the end of a word. map: \"$(-$)\" replace: 50 penalty: 100 ```\n\nThis makes adding a `-` to the end of a word more expensive.\n\nThink of it as taking the toll way for speed but getting the bill later.",
							"markdownDescription": "Add a penalty to the final cost.\nThis is used to discourage certain suggestions.\n\nExample:\n```yaml\n# Match adding/removing `-` to the end of a word.\nmap: \"$(-$)\"\nreplace: 50\npenalty: 100\n```\n\nThis makes adding a `-` to the end of a word more expensive.\n\nThink of it as taking the toll way for speed but getting the bill later.",
							"type": "number"
						},
						"replace": {
							"description": "The cost to replace of of the substrings in the map with another substring in the map. Example: Map['a', 'i'] This would be the cost to substitute `a` with `i`: Like `bat` to `bit` or the reverse.",
							"markdownDescription": "The cost to replace of of the substrings in the map with another substring in the map.\nExample: Map['a', 'i']\nThis would be the cost to substitute `a` with `i`: Like `bat` to `bit` or the reverse.",
							"type": "number"
						},
						"swap": {
							"description": "The cost to swap two adjacent substrings found in the map. Example: Map['e', 'i'] This represents the cost to change `ei` to `ie` or the reverse.",
							"markdownDescription": "The cost to swap two adjacent substrings found in the map.\nExample: Map['e', 'i']\nThis represents the cost to change `ei` to `ie` or the reverse.",
							"type": "number"
						}
					},
					"required": [
						"map",
						"replace"
					],
					"type": "object"
				},
				"CostMapDefSwap": {
					"additionalProperties": false,
					"properties": {
						"description": {
							"description": "A description to describe the purpose of the map.",
							"markdownDescription": "A description to describe the purpose of the map.",
							"type": "string"
						},
						"insDel": {
							"description": "The cost to insert/delete one of the substrings in the map. Note: insert/delete costs are symmetrical.",
							"markdownDescription": "The cost to insert/delete one of the substrings in the map. Note: insert/delete costs are symmetrical.",
							"type": "number"
						},
						"map": {
							"description": "The set of substrings to map, these are generally single character strings.\n\nMultiple sets can be defined by using a `|` to separate them.\n\nExample: `\"eéê|aåá\"` contains two different sets.\n\nTo add a multi-character substring use `()`.\n\nExample: `\"f(ph)(gh)\"` results in the following set: `f`, `ph`, `gh`.\n\n- To match the beginning of a word, use `^`: `\"(^I)\"\"`.\n- To match the end of a word, use `$`: `\"(e$)(ing$)\"`.",
							"markdownDescription": "The set of substrings to map, these are generally single character strings.\n\nMultiple sets can be defined by using a `|` to separate them.\n\nExample: `\"eéê|aåá\"` contains two different sets.\n\nTo add a multi-character substring use `()`.\n\nExample: `\"f(ph)(gh)\"` results in the following set: `f`, `ph`, `gh`.\n\n- To match the beginning of a word, use `^`: `\"(^I)\"\"`.\n- To match the end of a word, use `$`: `\"(e$)(ing$)\"`.",
							"type": "string"
						},
						"penalty": {
							"description": "Add a penalty to the final cost. This is used to discourage certain suggestions.\n\nExample: ```yaml # Match adding/removing `-` to the end of a word. map: \"$(-$)\" replace: 50 penalty: 100 ```\n\nThis makes adding a `-` to the end of a word more expensive.\n\nThink of it as taking the toll way for speed but getting the bill later.",
							"markdownDescription": "Add a penalty to the final cost.\nThis is used to discourage certain suggestions.\n\nExample:\n```yaml\n# Match adding/removing `-` to the end of a word.\nmap: \"$(-$)\"\nreplace: 50\npenalty: 100\n```\n\nThis makes adding a `-` to the end of a word more expensive.\n\nThink of it as taking the toll way for speed but getting the bill later.",
							"type": "number"
						},
						"replace": {
							"description": "The cost to replace of of the substrings in the map with another substring in the map. Example: Map['a', 'i'] This would be the cost to substitute `a` with `i`: Like `bat` to `bit` or the reverse.",
							"markdownDescription": "The cost to replace of of the substrings in the map with another substring in the map.\nExample: Map['a', 'i']\nThis would be the cost to substitute `a` with `i`: Like `bat` to `bit` or the reverse.",
							"type": "number"
						},
						"swap": {
							"description": "The cost to swap two adjacent substrings found in the map. Example: Map['e', 'i'] This represents the cost to change `ei` to `ie` or the reverse.",
							"markdownDescription": "The cost to swap two adjacent substrings found in the map.\nExample: Map['e', 'i']\nThis represents the cost to change `ei` to `ie` or the reverse.",
							"type": "number"
						}
					},
					"required": [
						"map",
						"swap"
					],
					"type": "object"
				},
				"CustomDictionaryPath": {
					"$ref": "#/definitions/FsDictionaryPath",
					"description": "A File System Path to a dictionary file.",
					"markdownDescription": "A File System Path to a dictionary file."
				},
				"CustomDictionaryScope": {
					"description": "Specifies the scope of a dictionary.",
					"enum": [
						"user",
						"workspace",
						"folder"
					],
					"markdownDescription": "Specifies the scope of a dictionary.",
					"type": "string"
				},
				"DictionaryDefinition": {
					"anyOf": [
						{
							"$ref": "#/definitions/DictionaryDefinitionPreferred"
						},
						{
							"$ref": "#/definitions/DictionaryDefinitionCustom"
						},
						{
							"$ref": "#/definitions/DictionaryDefinitionAugmented"
						},
						{
							"$ref": "#/definitions/DictionaryDefinitionInline"
						},
						{
							"$ref": "#/definitions/DictionaryDefinitionAlternate"
						}
					]
				},
				"DictionaryDefinitionAlternate": {
					"additionalProperties": false,
					"deprecated": true,
					"deprecationMessage": "Use `DictionaryDefinitionPreferred` instead.",
					"description": "Only for legacy dictionary definitions.",
					"markdownDescription": "Only for legacy dictionary definitions.",
					"properties": {
						"description": {
							"description": "Optional description.",
							"markdownDescription": "Optional description.",
							"type": "string"
						},
						"file": {
							"$ref": "#/definitions/DictionaryPath",
							"deprecated": true,
							"deprecationMessage": "Use `path` instead.",
							"description": "Path to the file, only for legacy dictionary definitions.",
							"markdownDescription": "Path to the file, only for legacy dictionary definitions."
						},
						"name": {
							"$ref": "#/definitions/DictionaryId",
							"description": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`.",
							"markdownDescription": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`."
						},
						"noSuggest": {
							"description": "Indicate that suggestions should not come from this dictionary. Words in this dictionary are considered correct, but will not be used when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in this dictionary, it will be removed from the set of possible suggestions.",
							"markdownDescription": "Indicate that suggestions should not come from this dictionary.\nWords in this dictionary are considered correct, but will not be\nused when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in\nthis dictionary, it will be removed from the set of\npossible suggestions.",
							"type": "boolean"
						},
						"repMap": {
							"$ref": "#/definitions/ReplaceMap",
							"description": "Replacement pairs.",
							"markdownDescription": "Replacement pairs."
						},
						"type": {
							"$ref": "#/definitions/DictionaryFileTypes",
							"default": "S",
							"description": "Type of file:\n- S - single word per line,\n- W - each line can contain one or more words separated by space,\n- C - each line is treated like code (Camel Case is allowed).\n\nDefault is S.\n\nC is the slowest to load due to the need to split each line based upon code splitting rules.",
							"markdownDescription": "Type of file:\n- S - single word per line,\n- W - each line can contain one or more words separated by space,\n- C - each line is treated like code (Camel Case is allowed).\n\nDefault is S.\n\nC is the slowest to load due to the need to split each line based upon code splitting rules."
						},
						"useCompounds": {
							"description": "Use Compounds.",
							"markdownDescription": "Use Compounds.",
							"type": "boolean"
						}
					},
					"required": [
						"file",
						"name"
					],
					"type": "object"
				},
				"DictionaryDefinitionAugmented": {
					"additionalProperties": false,
					"description": "Used to provide extra data related to the dictionary",
					"markdownDescription": "Used to provide extra data related to the dictionary",
					"properties": {
						"description": {
							"description": "Optional description.",
							"markdownDescription": "Optional description.",
							"type": "string"
						},
						"dictionaryInformation": {
							"$ref": "#/definitions/DictionaryInformation"
						},
						"name": {
							"$ref": "#/definitions/DictionaryId",
							"description": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`.",
							"markdownDescription": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`."
						},
						"noSuggest": {
							"description": "Indicate that suggestions should not come from this dictionary. Words in this dictionary are considered correct, but will not be used when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in this dictionary, it will be removed from the set of possible suggestions.",
							"markdownDescription": "Indicate that suggestions should not come from this dictionary.\nWords in this dictionary are considered correct, but will not be\nused when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in\nthis dictionary, it will be removed from the set of\npossible suggestions.",
							"type": "boolean"
						},
						"path": {
							"$ref": "#/definitions/DictionaryPath",
							"description": "Path to the file.",
							"markdownDescription": "Path to the file."
						},
						"repMap": {
							"$ref": "#/definitions/ReplaceMap",
							"description": "Replacement pairs.",
							"markdownDescription": "Replacement pairs."
						},
						"type": {
							"$ref": "#/definitions/DictionaryFileTypes",
							"default": "S",
							"description": "Type of file:\n- S - single word per line,\n- W - each line can contain one or more words separated by space,\n- C - each line is treated like code (Camel Case is allowed).\n\nDefault is S.\n\nC is the slowest to load due to the need to split each line based upon code splitting rules.",
							"markdownDescription": "Type of file:\n- S - single word per line,\n- W - each line can contain one or more words separated by space,\n- C - each line is treated like code (Camel Case is allowed).\n\nDefault is S.\n\nC is the slowest to load due to the need to split each line based upon code splitting rules."
						},
						"useCompounds": {
							"description": "Use Compounds.",
							"markdownDescription": "Use Compounds.",
							"type": "boolean"
						}
					},
					"required": [
						"name",
						"path"
					],
					"type": "object"
				},
				"DictionaryDefinitionCustom": {
					"additionalProperties": false,
					"description": "For Defining Custom dictionaries. They are generally scoped to a `user`, `workspace`, or `folder`. When `addWords` is true, indicates that the spell checker can add words to the file.\n\nNote: only plain text files with one word per line are supported at this moment.",
					"markdownDescription": "For Defining Custom dictionaries. They are generally scoped to a\n`user`, `workspace`, or `folder`.\nWhen `addWords` is true, indicates that the spell checker can add words\nto the file.\n\nNote: only plain text files with one word per line are supported at this moment.",
					"properties": {
						"addWords": {
							"description": "When `true`, let's the spell checker know that words can be added to this dictionary.",
							"markdownDescription": "When `true`, let's the spell checker know that words can be added to this dictionary.",
							"type": "boolean"
						},
						"description": {
							"description": "Optional description.",
							"markdownDescription": "Optional description.",
							"type": "string"
						},
						"name": {
							"$ref": "#/definitions/DictionaryId",
							"description": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`.",
							"markdownDescription": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`."
						},
						"noSuggest": {
							"description": "Indicate that suggestions should not come from this dictionary. Words in this dictionary are considered correct, but will not be used when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in this dictionary, it will be removed from the set of possible suggestions.",
							"markdownDescription": "Indicate that suggestions should not come from this dictionary.\nWords in this dictionary are considered correct, but will not be\nused when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in\nthis dictionary, it will be removed from the set of\npossible suggestions.",
							"type": "boolean"
						},
						"path": {
							"$ref": "#/definitions/CustomDictionaryPath",
							"description": "Path to custom dictionary text file.",
							"markdownDescription": "Path to custom dictionary text file."
						},
						"repMap": {
							"$ref": "#/definitions/ReplaceMap",
							"description": "Replacement pairs.",
							"markdownDescription": "Replacement pairs."
						},
						"scope": {
							"anyOf": [
								{
									"$ref": "#/definitions/CustomDictionaryScope"
								},
								{
									"items": {
										"$ref": "#/definitions/CustomDictionaryScope"
									},
									"type": "array"
								}
							],
							"description": "Defines the scope for when words will be added to the dictionary.\n\nScope values: `user`, `workspace`, `folder`.",
							"markdownDescription": "Defines the scope for when words will be added to the dictionary.\n\nScope values: `user`, `workspace`, `folder`."
						},
						"type": {
							"$ref": "#/definitions/DictionaryFileTypes",
							"default": "S",
							"description": "Type of file:\n- S - single word per line,\n- W - each line can contain one or more words separated by space,\n- C - each line is treated like code (Camel Case is allowed).\n\nDefault is S.\n\nC is the slowest to load due to the need to split each line based upon code splitting rules.",
							"markdownDescription": "Type of file:\n- S - single word per line,\n- W - each line can contain one or more words separated by space,\n- C - each line is treated like code (Camel Case is allowed).\n\nDefault is S.\n\nC is the slowest to load due to the need to split each line based upon code splitting rules."
						},
						"useCompounds": {
							"description": "Use Compounds.",
							"markdownDescription": "Use Compounds.",
							"type": "boolean"
						}
					},
					"required": [
						"addWords",
						"name",
						"path"
					],
					"type": "object"
				},
				"DictionaryDefinitionInline": {
					"anyOf": [
						{
							"$ref": "#/definitions/DictionaryDefinitionInlineWords"
						},
						{
							"$ref": "#/definitions/DictionaryDefinitionInlineIgnoreWords"
						},
						{
							"$ref": "#/definitions/DictionaryDefinitionInlineFlagWords"
						}
					],
					"description": "Inline Dictionary Definitions",
					"markdownDescription": "Inline Dictionary Definitions"
				},
				"DictionaryDefinitionInlineFlagWords": {
					"additionalProperties": false,
					"properties": {
						"description": {
							"description": "Optional description.",
							"markdownDescription": "Optional description.",
							"type": "string"
						},
						"flagWords": {
							"description": "List of words to always be considered incorrect. Words found in `flagWords` override `words`.\n\nFormat of `flagWords`\n- single word entry - `word`\n- with suggestions - `word:suggestion` or `word->suggestion, suggestions`\n\nExample: ```ts \"flagWords\": [   \"color: colour\",   \"incase: in case, encase\",   \"canot->cannot\",   \"cancelled->canceled\" ] ```",
							"items": {
								"type": "string"
							},
							"markdownDescription": "List of words to always be considered incorrect. Words found in `flagWords` override `words`.\n\nFormat of `flagWords`\n- single word entry - `word`\n- with suggestions - `word:suggestion` or `word->suggestion, suggestions`\n\nExample:\n```ts\n\"flagWords\": [\n  \"color: colour\",\n  \"incase: in case, encase\",\n  \"canot->cannot\",\n  \"cancelled->canceled\"\n]\n```",
							"type": "array"
						},
						"ignoreWords": {
							"description": "List of words to be ignored. An ignored word will not show up as an error, even if it is also in the `flagWords`.",
							"items": {
								"type": "string"
							},
							"markdownDescription": "List of words to be ignored. An ignored word will not show up as an error, even if it is\nalso in the `flagWords`.",
							"type": "array"
						},
						"name": {
							"$ref": "#/definitions/DictionaryId",
							"description": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`.",
							"markdownDescription": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`."
						},
						"noSuggest": {
							"description": "Indicate that suggestions should not come from this dictionary. Words in this dictionary are considered correct, but will not be used when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in this dictionary, it will be removed from the set of possible suggestions.",
							"markdownDescription": "Indicate that suggestions should not come from this dictionary.\nWords in this dictionary are considered correct, but will not be\nused when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in\nthis dictionary, it will be removed from the set of\npossible suggestions.",
							"type": "boolean"
						},
						"repMap": {
							"$ref": "#/definitions/ReplaceMap",
							"description": "Replacement pairs.",
							"markdownDescription": "Replacement pairs."
						},
						"suggestWords": {
							"description": "A list of suggested replacements for words. Suggested words provide a way to make preferred suggestions on word replacements. To hint at a preferred change, but not to require it.\n\nFormat of `suggestWords`\n- Single suggestion (possible auto fix)     - `word: suggestion`     - `word->suggestion`\n- Multiple suggestions (not auto fixable)    - `word: first, second, third`    - `word->first, second, third`",
							"items": {
								"type": "string"
							},
							"markdownDescription": "A list of suggested replacements for words.\nSuggested words provide a way to make preferred suggestions on word replacements.\nTo hint at a preferred change, but not to require it.\n\nFormat of `suggestWords`\n- Single suggestion (possible auto fix)\n    - `word: suggestion`\n    - `word->suggestion`\n- Multiple suggestions (not auto fixable)\n   - `word: first, second, third`\n   - `word->first, second, third`",
							"type": "array"
						},
						"type": {
							"$ref": "#/definitions/DictionaryFileTypes",
							"default": "S",
							"description": "Type of file:\n- S - single word per line,\n- W - each line can contain one or more words separated by space,\n- C - each line is treated like code (Camel Case is allowed).\n\nDefault is S.\n\nC is the slowest to load due to the need to split each line based upon code splitting rules.",
							"markdownDescription": "Type of file:\n- S - single word per line,\n- W - each line can contain one or more words separated by space,\n- C - each line is treated like code (Camel Case is allowed).\n\nDefault is S.\n\nC is the slowest to load due to the need to split each line based upon code splitting rules."
						},
						"useCompounds": {
							"description": "Use Compounds.",
							"markdownDescription": "Use Compounds.",
							"type": "boolean"
						},
						"words": {
							"description": "List of words to be considered correct.",
							"items": {
								"type": "string"
							},
							"markdownDescription": "List of words to be considered correct.",
							"type": "array"
						}
					},
					"required": [
						"flagWords",
						"name"
					],
					"type": "object"
				},
				"DictionaryDefinitionInlineIgnoreWords": {
					"additionalProperties": false,
					"properties": {
						"description": {
							"description": "Optional description.",
							"markdownDescription": "Optional description.",
							"type": "string"
						},
						"flagWords": {
							"description": "List of words to always be considered incorrect. Words found in `flagWords` override `words`.\n\nFormat of `flagWords`\n- single word entry - `word`\n- with suggestions - `word:suggestion` or `word->suggestion, suggestions`\n\nExample: ```ts \"flagWords\": [   \"color: colour\",   \"incase: in case, encase\",   \"canot->cannot\",   \"cancelled->canceled\" ] ```",
							"items": {
								"type": "string"
							},
							"markdownDescription": "List of words to always be considered incorrect. Words found in `flagWords` override `words`.\n\nFormat of `flagWords`\n- single word entry - `word`\n- with suggestions - `word:suggestion` or `word->suggestion, suggestions`\n\nExample:\n```ts\n\"flagWords\": [\n  \"color: colour\",\n  \"incase: in case, encase\",\n  \"canot->cannot\",\n  \"cancelled->canceled\"\n]\n```",
							"type": "array"
						},
						"ignoreWords": {
							"description": "List of words to be ignored. An ignored word will not show up as an error, even if it is also in the `flagWords`.",
							"items": {
								"type": "string"
							},
							"markdownDescription": "List of words to be ignored. An ignored word will not show up as an error, even if it is\nalso in the `flagWords`.",
							"type": "array"
						},
						"name": {
							"$ref": "#/definitions/DictionaryId",
							"description": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`.",
							"markdownDescription": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`."
						},
						"noSuggest": {
							"description": "Indicate that suggestions should not come from this dictionary. Words in this dictionary are considered correct, but will not be used when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in this dictionary, it will be removed from the set of possible suggestions.",
							"markdownDescription": "Indicate that suggestions should not come from this dictionary.\nWords in this dictionary are considered correct, but will not be\nused when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in\nthis dictionary, it will be removed from the set of\npossible suggestions.",
							"type": "boolean"
						},
						"repMap": {
							"$ref": "#/definitions/ReplaceMap",
							"description": "Replacement pairs.",
							"markdownDescription": "Replacement pairs."
						},
						"suggestWords": {
							"description": "A list of suggested replacements for words. Suggested words provide a way to make preferred suggestions on word replacements. To hint at a preferred change, but not to require it.\n\nFormat of `suggestWords`\n- Single suggestion (possible auto fix)     - `word: suggestion`     - `word->suggestion`\n- Multiple suggestions (not auto fixable)    - `word: first, second, third`    - `word->first, second, third`",
							"items": {
								"type": "string"
							},
							"markdownDescription": "A list of suggested replacements for words.\nSuggested words provide a way to make preferred suggestions on word replacements.\nTo hint at a preferred change, but not to require it.\n\nFormat of `suggestWords`\n- Single suggestion (possible auto fix)\n    - `word: suggestion`\n    - `word->suggestion`\n- Multiple suggestions (not auto fixable)\n   - `word: first, second, third`\n   - `word->first, second, third`",
							"type": "array"
						},
						"type": {
							"$ref": "#/definitions/DictionaryFileTypes",
							"default": "S",
							"description": "Type of file:\n- S - single word per line,\n- W - each line can contain one or more words separated by space,\n- C - each line is treated like code (Camel Case is allowed).\n\nDefault is S.\n\nC is the slowest to load due to the need to split each line based upon code splitting rules.",
							"markdownDescription": "Type of file:\n- S - single word per line,\n- W - each line can contain one or more words separated by space,\n- C - each line is treated like code (Camel Case is allowed).\n\nDefault is S.\n\nC is the slowest to load due to the need to split each line based upon code splitting rules."
						},
						"useCompounds": {
							"description": "Use Compounds.",
							"markdownDescription": "Use Compounds.",
							"type": "boolean"
						},
						"words": {
							"description": "List of words to be considered correct.",
							"items": {
								"type": "string"
							},
							"markdownDescription": "List of words to be considered correct.",
							"type": "array"
						}
					},
					"required": [
						"ignoreWords",
						"name"
					],
					"type": "object"
				},
				"DictionaryDefinitionInlineWords": {
					"additionalProperties": false,
					"properties": {
						"description": {
							"description": "Optional description.",
							"markdownDescription": "Optional description.",
							"type": "string"
						},
						"flagWords": {
							"description": "List of words to always be considered incorrect. Words found in `flagWords` override `words`.\n\nFormat of `flagWords`\n- single word entry - `word`\n- with suggestions - `word:suggestion` or `word->suggestion, suggestions`\n\nExample: ```ts \"flagWords\": [   \"color: colour\",   \"incase: in case, encase\",   \"canot->cannot\",   \"cancelled->canceled\" ] ```",
							"items": {
								"type": "string"
							},
							"markdownDescription": "List of words to always be considered incorrect. Words found in `flagWords` override `words`.\n\nFormat of `flagWords`\n- single word entry - `word`\n- with suggestions - `word:suggestion` or `word->suggestion, suggestions`\n\nExample:\n```ts\n\"flagWords\": [\n  \"color: colour\",\n  \"incase: in case, encase\",\n  \"canot->cannot\",\n  \"cancelled->canceled\"\n]\n```",
							"type": "array"
						},
						"ignoreWords": {
							"description": "List of words to be ignored. An ignored word will not show up as an error, even if it is also in the `flagWords`.",
							"items": {
								"type": "string"
							},
							"markdownDescription": "List of words to be ignored. An ignored word will not show up as an error, even if it is\nalso in the `flagWords`.",
							"type": "array"
						},
						"name": {
							"$ref": "#/definitions/DictionaryId",
							"description": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`.",
							"markdownDescription": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`."
						},
						"noSuggest": {
							"description": "Indicate that suggestions should not come from this dictionary. Words in this dictionary are considered correct, but will not be used when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in this dictionary, it will be removed from the set of possible suggestions.",
							"markdownDescription": "Indicate that suggestions should not come from this dictionary.\nWords in this dictionary are considered correct, but will not be\nused when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in\nthis dictionary, it will be removed from the set of\npossible suggestions.",
							"type": "boolean"
						},
						"repMap": {
							"$ref": "#/definitions/ReplaceMap",
							"description": "Replacement pairs.",
							"markdownDescription": "Replacement pairs."
						},
						"suggestWords": {
							"description": "A list of suggested replacements for words. Suggested words provide a way to make preferred suggestions on word replacements. To hint at a preferred change, but not to require it.\n\nFormat of `suggestWords`\n- Single suggestion (possible auto fix)     - `word: suggestion`     - `word->suggestion`\n- Multiple suggestions (not auto fixable)    - `word: first, second, third`    - `word->first, second, third`",
							"items": {
								"type": "string"
							},
							"markdownDescription": "A list of suggested replacements for words.\nSuggested words provide a way to make preferred suggestions on word replacements.\nTo hint at a preferred change, but not to require it.\n\nFormat of `suggestWords`\n- Single suggestion (possible auto fix)\n    - `word: suggestion`\n    - `word->suggestion`\n- Multiple suggestions (not auto fixable)\n   - `word: first, second, third`\n   - `word->first, second, third`",
							"type": "array"
						},
						"type": {
							"$ref": "#/definitions/DictionaryFileTypes",
							"default": "S",
							"description": "Type of file:\n- S - single word per line,\n- W - each line can contain one or more words separated by space,\n- C - each line is treated like code (Camel Case is allowed).\n\nDefault is S.\n\nC is the slowest to load due to the need to split each line based upon code splitting rules.",
							"markdownDescription": "Type of file:\n- S - single word per line,\n- W - each line can contain one or more words separated by space,\n- C - each line is treated like code (Camel Case is allowed).\n\nDefault is S.\n\nC is the slowest to load due to the need to split each line based upon code splitting rules."
						},
						"useCompounds": {
							"description": "Use Compounds.",
							"markdownDescription": "Use Compounds.",
							"type": "boolean"
						},
						"words": {
							"description": "List of words to be considered correct.",
							"items": {
								"type": "string"
							},
							"markdownDescription": "List of words to be considered correct.",
							"type": "array"
						}
					},
					"required": [
						"name",
						"words"
					],
					"type": "object"
				},
				"DictionaryDefinitionPreferred": {
					"additionalProperties": false,
					"properties": {
						"description": {
							"description": "Optional description.",
							"markdownDescription": "Optional description.",
							"type": "string"
						},
						"name": {
							"$ref": "#/definitions/DictionaryId",
							"description": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`.",
							"markdownDescription": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`."
						},
						"noSuggest": {
							"description": "Indicate that suggestions should not come from this dictionary. Words in this dictionary are considered correct, but will not be used when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in this dictionary, it will be removed from the set of possible suggestions.",
							"markdownDescription": "Indicate that suggestions should not come from this dictionary.\nWords in this dictionary are considered correct, but will not be\nused when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in\nthis dictionary, it will be removed from the set of\npossible suggestions.",
							"type": "boolean"
						},
						"path": {
							"$ref": "#/definitions/DictionaryPath",
							"description": "Path to the file.",
							"markdownDescription": "Path to the file."
						},
						"repMap": {
							"$ref": "#/definitions/ReplaceMap",
							"description": "Replacement pairs.",
							"markdownDescription": "Replacement pairs."
						},
						"type": {
							"$ref": "#/definitions/DictionaryFileTypes",
							"default": "S",
							"description": "Type of file:\n- S - single word per line,\n- W - each line can contain one or more words separated by space,\n- C - each line is treated like code (Camel Case is allowed).\n\nDefault is S.\n\nC is the slowest to load due to the need to split each line based upon code splitting rules.",
							"markdownDescription": "Type of file:\n- S - single word per line,\n- W - each line can contain one or more words separated by space,\n- C - each line is treated like code (Camel Case is allowed).\n\nDefault is S.\n\nC is the slowest to load due to the need to split each line based upon code splitting rules."
						},
						"useCompounds": {
							"description": "Use Compounds.",
							"markdownDescription": "Use Compounds.",
							"type": "boolean"
						}
					},
					"required": [
						"name",
						"path"
					],
					"type": "object"
				},
				"DictionaryFileTypes": {
					"enum": [
						"S",
						"W",
						"C",
						"T"
					],
					"type": "string"
				},
				"DictionaryId": {
					"description": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`.",
					"markdownDescription": "This is the name of a dictionary.\n\nName Format:\n- Must contain at least 1 number or letter.\n- Spaces are allowed.\n- Leading and trailing space will be removed.\n- Names ARE case-sensitive.\n- Must not contain `*`, `!`, `;`, `,`, `{`, `}`, `[`, `]`, `~`.",
					"pattern": "^(?=[^!*,;{}[\\]~\\n]+$)(?=(.*\\w)).+$",
					"type": "string"
				},
				"DictionaryInformation": {
					"additionalProperties": false,
					"description": "Use by dictionary authors to help improve the quality of suggestions given from the dictionary.\n\nAdded with `v5.16.0`.",
					"markdownDescription": "Use by dictionary authors to help improve the quality of suggestions\ngiven from the dictionary.\n\nAdded with `v5.16.0`.",
					"properties": {
						"accents": {
							"anyOf": [
								{
									"$ref": "#/definitions/CharacterSet"
								},
								{
									"items": {
										"$ref": "#/definitions/CharacterSetCosts"
									},
									"type": "array"
								}
							],
							"description": "The accent characters.\n\nDefault: `\"\\u0300-\\u0341\"`",
							"markdownDescription": "The accent characters.\n\nDefault: `\"\\u0300-\\u0341\"`"
						},
						"adjustments": {
							"description": "A collection of patterns to test against the suggested words. If the word matches the pattern, then the penalty is applied.",
							"items": {
								"$ref": "#/definitions/PatternAdjustment"
							},
							"markdownDescription": "A collection of patterns to test against the suggested words.\nIf the word matches the pattern, then the penalty is applied.",
							"type": "array"
						},
						"alphabet": {
							"anyOf": [
								{
									"$ref": "#/definitions/CharacterSet"
								},
								{
									"items": {
										"$ref": "#/definitions/CharacterSetCosts"
									},
									"type": "array"
								}
							],
							"default": "a-zA-Z",
							"description": "The alphabet to use.",
							"markdownDescription": "The alphabet to use."
						},
						"costs": {
							"$ref": "#/definitions/EditCosts",
							"description": "Define edit costs.",
							"markdownDescription": "Define edit costs."
						},
						"hunspellInformation": {
							"$ref": "#/definitions/HunspellInformation",
							"description": "Used by dictionary authors",
							"markdownDescription": "Used by dictionary authors"
						},
						"ignore": {
							"$ref": "#/definitions/CharacterSet",
							"description": "An optional set of characters that can possibly be removed from a word before checking it.\n\nThis is useful in languages like Arabic where Harakat accents are optional.\n\nNote: All matching characters are removed or none. Partial removal is not supported.",
							"markdownDescription": "An optional set of characters that can possibly be removed from a word before\nchecking it.\n\nThis is useful in languages like Arabic where Harakat accents are optional.\n\nNote: All matching characters are removed or none. Partial removal is not supported."
						},
						"locale": {
							"description": "The locale of the dictionary. Example: `nl,nl-be`",
							"markdownDescription": "The locale of the dictionary.\nExample: `nl,nl-be`",
							"type": "string"
						},
						"suggestionEditCosts": {
							"$ref": "#/definitions/SuggestionCostsDefs",
							"description": "Used in making suggestions. The lower the value, the more likely the suggestion will be near the top of the suggestion list.",
							"markdownDescription": "Used in making suggestions. The lower the value, the more likely the suggestion\nwill be near the top of the suggestion list."
						}
					},
					"type": "object"
				},
				"DictionaryNegRef": {
					"description": "This a negative reference to a named dictionary.\n\nIt is used to exclude or include a dictionary by name.\n\nThe reference starts with 1 or more `!`.\n- `!<dictionary_name>` - Used to exclude the dictionary matching `<dictionary_name>`.\n- `!!<dictionary_name>` - Used to re-include a dictionary matching `<dictionary_name>`.    Overrides `!<dictionary_name>`.\n- `!!!<dictionary_name>` - Used to exclude a dictionary matching `<dictionary_name>`.    Overrides `!!<dictionary_name>`.",
					"markdownDescription": "This a negative reference to a named dictionary.\n\nIt is used to exclude or include a dictionary by name.\n\nThe reference starts with 1 or more `!`.\n- `!<dictionary_name>` - Used to exclude the dictionary matching `<dictionary_name>`.\n- `!!<dictionary_name>` - Used to re-include a dictionary matching `<dictionary_name>`.\n   Overrides `!<dictionary_name>`.\n- `!!!<dictionary_name>` - Used to exclude a dictionary matching `<dictionary_name>`.\n   Overrides `!!<dictionary_name>`.",
					"pattern": "^(?=!+[^!*,;{}[\\]~\\n]+$)(?=(.*\\w)).+$",
					"type": "string"
				},
				"DictionaryPath": {
					"description": "A File System Path to a dictionary file.",
					"markdownDescription": "A File System Path to a dictionary file.",
					"pattern": "^.*\\.(?:txt|trie)(?:\\.gz)?$",
					"type": "string"
				},
				"DictionaryRef": {
					"$ref": "#/definitions/DictionaryId",
					"description": "This a reference to a named dictionary. It is expected to match the name of a dictionary.",
					"markdownDescription": "This a reference to a named dictionary.\nIt is expected to match the name of a dictionary."
				},
				"DictionaryReference": {
					"anyOf": [
						{
							"$ref": "#/definitions/DictionaryRef"
						},
						{
							"$ref": "#/definitions/DictionaryNegRef"
						}
					],
					"description": "Reference to a dictionary by name. One of:\n-  {@link  DictionaryRef } \n-  {@link  DictionaryNegRef }",
					"markdownDescription": "Reference to a dictionary by name.\nOne of:\n-  {@link  DictionaryRef } \n-  {@link  DictionaryNegRef }"
				},
				"EditCosts": {
					"additionalProperties": false,
					"properties": {
						"accentCosts": {
							"default": 1,
							"description": "The cost to add / remove an accent This should be very cheap, it helps with fixing accent issues.",
							"markdownDescription": "The cost to add / remove an accent\nThis should be very cheap, it helps with fixing accent issues.",
							"type": "number"
						},
						"baseCost": {
							"default": 100,
							"description": "This is the base cost for making an edit.",
							"markdownDescription": "This is the base cost for making an edit.",
							"type": "number"
						},
						"capsCosts": {
							"default": 1,
							"description": "The cost to change capitalization. This should be very cheap, it helps with fixing capitalization issues.",
							"markdownDescription": "The cost to change capitalization.\nThis should be very cheap, it helps with fixing capitalization issues.",
							"type": "number"
						},
						"firstLetterPenalty": {
							"default": 4,
							"description": "The extra cost incurred for changing the first letter of a word. This value should be less than `100 - baseCost`.",
							"markdownDescription": "The extra cost incurred for changing the first letter of a word.\nThis value should be less than `100 - baseCost`.",
							"type": "number"
						},
						"nonAlphabetCosts": {
							"default": 110,
							"description": "This is the cost for characters not in the alphabet.",
							"markdownDescription": "This is the cost for characters not in the alphabet.",
							"type": "number"
						}
					},
					"type": "object"
				},
				"FSPathResolvable": {
					"$ref": "#/definitions/FsPath",
					"description": "A File System Path.\n\nSpecial Properties:\n- `${cwd}` prefix - will be replaced with the current working directory.\n- Relative paths are relative to the configuration file.",
					"markdownDescription": "A File System Path.\n\nSpecial Properties:\n- `${cwd}` prefix - will be replaced with the current working directory.\n- Relative paths are relative to the configuration file."
				},
				"FeatureEnableOnly": {
					"type": "boolean"
				},
				"Features": {
					"additionalProperties": false,
					"description": "Features are behaviors or settings that can be explicitly configured.",
					"markdownDescription": "Features are behaviors or settings that can be explicitly configured.",
					"properties": {
						"weighted-suggestions": {
							"$ref": "#/definitions/FeatureEnableOnly",
							"description": "Enable/disable using weighted suggestions.",
							"markdownDescription": "Enable/disable using weighted suggestions."
						}
					},
					"type": "object"
				},
				"FsDictionaryPath": {
					"description": "A File System Path. Relative paths are relative to the configuration file.",
					"markdownDescription": "A File System Path. Relative paths are relative to the configuration file.",
					"type": "string"
				},
				"FsPath": {
					"description": "A File System Path. Relative paths are relative to the configuration file.",
					"markdownDescription": "A File System Path. Relative paths are relative to the configuration file.",
					"type": "string"
				},
				"Glob": {
					"$ref": "#/definitions/SimpleGlob",
					"description": "These are glob expressions.",
					"markdownDescription": "These are glob expressions."
				},
				"HunspellInformation": {
					"additionalProperties": false,
					"properties": {
						"aff": {
							"description": "Selected Hunspell AFF content. The content must be UTF-8\n\nSections:\n- TRY\n- MAP\n- REP\n- KEY\n- ICONV\n- OCONV\n\nExample: ```hunspell # Comment TRY aeistlunkodmrvpgjhäõbüoöfcwzxðqþ` MAP aàâäAÀÂÄ MAP eéèêëEÉÈÊË MAP iîïyIÎÏY MAP oôöOÔÖ MAP (IJ)(Ĳ) ```",
							"markdownDescription": "Selected Hunspell AFF content.\nThe content must be UTF-8\n\nSections:\n- TRY\n- MAP\n- REP\n- KEY\n- ICONV\n- OCONV\n\nExample:\n```hunspell\n# Comment\nTRY aeistlunkodmrvpgjhäõbüoöfcwzxðqþ`\nMAP aàâäAÀÂÄ\nMAP eéèêëEÉÈÊË\nMAP iîïyIÎÏY\nMAP oôöOÔÖ\nMAP (IJ)(Ĳ)\n```",
							"type": "string"
						},
						"costs": {
							"additionalProperties": false,
							"description": "The costs to apply when using the hunspell settings",
							"markdownDescription": "The costs to apply when using the hunspell settings",
							"properties": {
								"accentCosts": {
									"default": 1,
									"description": "The cost to add / remove an accent This should be very cheap, it helps with fixing accent issues.",
									"markdownDescription": "The cost to add / remove an accent\nThis should be very cheap, it helps with fixing accent issues.",
									"type": "number"
								},
								"baseCost": {
									"default": 100,
									"description": "This is the base cost for making an edit.",
									"markdownDescription": "This is the base cost for making an edit.",
									"type": "number"
								},
								"capsCosts": {
									"default": 1,
									"description": "The cost to change capitalization. This should be very cheap, it helps with fixing capitalization issues.",
									"markdownDescription": "The cost to change capitalization.\nThis should be very cheap, it helps with fixing capitalization issues.",
									"type": "number"
								},
								"firstLetterPenalty": {
									"default": 4,
									"description": "The extra cost incurred for changing the first letter of a word. This value should be less than `100 - baseCost`.",
									"markdownDescription": "The extra cost incurred for changing the first letter of a word.\nThis value should be less than `100 - baseCost`.",
									"type": "number"
								},
								"ioConvertCost": {
									"default": 30,
									"description": "The cost to convert between convert pairs.\n\nThe value should be slightly higher than the mapCost.",
									"markdownDescription": "The cost to convert between convert pairs.\n\nThe value should be slightly higher than the mapCost.",
									"type": "number"
								},
								"keyboardCost": {
									"default": 99,
									"description": "The cost of replacing or swapping any adjacent keyboard characters.\n\nThis should be slightly cheaper than `tryCharCost`.",
									"markdownDescription": "The cost of replacing or swapping any adjacent keyboard characters.\n\nThis should be slightly cheaper than `tryCharCost`.",
									"type": "number"
								},
								"mapCost": {
									"default": 25,
									"description": "mapSet replacement cost is the cost to substitute one character with another from the same set.\n\nMap characters are considered very similar to each other and are often the cause of simple mistakes.",
									"markdownDescription": "mapSet replacement cost is the cost to substitute one character with another from\nthe same set.\n\nMap characters are considered very similar to each other and are often\nthe cause of simple mistakes.",
									"type": "number"
								},
								"nonAlphabetCosts": {
									"default": 110,
									"description": "This is the cost for characters not in the alphabet.",
									"markdownDescription": "This is the cost for characters not in the alphabet.",
									"type": "number"
								},
								"replaceCosts": {
									"default": 75,
									"description": "The cost to substitute pairs found in the replace settings.",
									"markdownDescription": "The cost to substitute pairs found in the replace settings.",
									"type": "number"
								},
								"tryCharCost": {
									"description": "The cost of inserting / deleting / or swapping any `tryChars` Defaults to `baseCosts`",
									"markdownDescription": "The cost of inserting / deleting / or swapping any `tryChars`\nDefaults to `baseCosts`",
									"type": "number"
								}
							},
							"type": "object"
						}
					},
					"required": [
						"aff"
					],
					"type": "object"
				},
				"LanguageId": {
					"anyOf": [
						{
							"$ref": "#/definitions/LanguageIdSingle"
						},
						{
							"$ref": "#/definitions/LanguageIdMultiple"
						},
						{
							"$ref": "#/definitions/LanguageIdMultipleNeg"
						}
					]
				},
				"LanguageIdMultiple": {
					"description": "This can be 'typescript,cpp,json,literal haskell', etc.",
					"markdownDescription": "This can be 'typescript,cpp,json,literal haskell', etc.",
					"pattern": "^([-\\w_\\s]+)(,[-\\w_\\s]+)*$",
					"type": "string"
				},
				"LanguageIdMultipleNeg": {
					"description": "This can be 'typescript,cpp,json,literal haskell', etc.",
					"markdownDescription": "This can be 'typescript,cpp,json,literal haskell', etc.",
					"pattern": "^(![-\\w_\\s]+)(,![-\\w_\\s]+)*$",
					"type": "string"
				},
				"LanguageIdSingle": {
					"description": "This can be '*', 'typescript', 'cpp', 'json', etc.",
					"markdownDescription": "This can be '*', 'typescript', 'cpp', 'json', etc.",
					"pattern": "^(!?[-\\w_\\s]+)|(\\*)$",
					"type": "string"
				},
				"LanguageSetting": {
					"additionalProperties": false,
					"properties": {
						"allowCompoundWords": {
							"default": false,
							"description": "True to enable compound word checking. See [Case Sensitivity](https://cspell.org/docs/case-sensitive/) for more details.",
							"markdownDescription": "True to enable compound word checking. See [Case Sensitivity](https://cspell.org/docs/case-sensitive/) for more details.",
							"type": "boolean"
						},
						"caseSensitive": {
							"default": false,
							"description": "Determines if words must match case and accent rules.\n\n- `false` - Case is ignored and accents can be missing on the entire word.   Incorrect accents or partially missing accents will be marked as incorrect.\n- `true` - Case and accents are enforced.",
							"markdownDescription": "Determines if words must match case and accent rules.\n\n- `false` - Case is ignored and accents can be missing on the entire word.\n  Incorrect accents or partially missing accents will be marked as incorrect.\n- `true` - Case and accents are enforced.",
							"type": "boolean"
						},
						"description": {
							"description": "Optional description of configuration.",
							"markdownDescription": "Optional description of configuration.",
							"type": "string"
						},
						"dictionaries": {
							"description": "Optional list of dictionaries to use. Each entry should match the name of the dictionary.\n\nTo remove a dictionary from the list, add `!` before the name.\n\nFor example, `!typescript` will turn off the dictionary with the name `typescript`.\n\nSee the [Dictionaries](https://cspell.org/docs/dictionaries/) and [Custom Dictionaries](https://cspell.org/docs/dictionaries-custom/) for more details.",
							"items": {
								"$ref": "#/definitions/DictionaryReference"
							},
							"markdownDescription": "Optional list of dictionaries to use. Each entry should match the name of the dictionary.\n\nTo remove a dictionary from the list, add `!` before the name.\n\nFor example, `!typescript` will turn off the dictionary with the name `typescript`.\n\nSee the [Dictionaries](https://cspell.org/docs/dictionaries/)\nand [Custom Dictionaries](https://cspell.org/docs/dictionaries-custom/) for more details.",
							"type": "array"
						},
						"dictionaryDefinitions": {
							"description": "Define additional available dictionaries.\n\nFor example, you can use the following to add a custom dictionary:\n\n```json \"dictionaryDefinitions\": [   { \"name\": \"custom-words\", \"path\": \"./custom-words.txt\"} ], \"dictionaries\": [\"custom-words\"] ```",
							"items": {
								"$ref": "#/definitions/DictionaryDefinition"
							},
							"markdownDescription": "Define additional available dictionaries.\n\nFor example, you can use the following to add a custom dictionary:\n\n```json\n\"dictionaryDefinitions\": [\n  { \"name\": \"custom-words\", \"path\": \"./custom-words.txt\"}\n],\n\"dictionaries\": [\"custom-words\"]\n```",
							"type": "array"
						},
						"enabled": {
							"default": true,
							"description": "Is the spell checker enabled.",
							"markdownDescription": "Is the spell checker enabled.",
							"type": "boolean"
						},
						"flagWords": {
							"description": "List of words to always be considered incorrect. Words found in `flagWords` override `words`.\n\nFormat of `flagWords`\n- single word entry - `word`\n- with suggestions - `word:suggestion` or `word->suggestion, suggestions`\n\nExample: ```ts \"flagWords\": [   \"color: colour\",   \"incase: in case, encase\",   \"canot->cannot\",   \"cancelled->canceled\" ] ```",
							"items": {
								"type": "string"
							},
							"markdownDescription": "List of words to always be considered incorrect. Words found in `flagWords` override `words`.\n\nFormat of `flagWords`\n- single word entry - `word`\n- with suggestions - `word:suggestion` or `word->suggestion, suggestions`\n\nExample:\n```ts\n\"flagWords\": [\n  \"color: colour\",\n  \"incase: in case, encase\",\n  \"canot->cannot\",\n  \"cancelled->canceled\"\n]\n```",
							"type": "array"
						},
						"id": {
							"description": "Optional identifier.",
							"markdownDescription": "Optional identifier.",
							"type": "string"
						},
						"ignoreRegExpList": {
							"$ref": "#/definitions/RegExpPatternList",
							"description": "List of regular expression patterns or pattern names to exclude from spell checking.\n\nExample: `[\"href\"]` - to exclude html href pattern.\n\nRegular expressions use JavaScript regular expression syntax.\n\nExample: to ignore ALL-CAPS words\n\nJSON ```json \"ignoreRegExpList\": [\"/\\\\b[A-Z]+\\\\b/g\"] ```\n\nYAML ```yaml ignoreRegExpList:   - >-    /\\b[A-Z]+\\b/g ```\n\nBy default, several patterns are excluded. See [Configuration](https://cspell.org/configuration/patterns) for more details.\n\nWhile you can create your own patterns, you can also leverage several patterns that are [built-in to CSpell](https://cspell.org/types/cspell-types/types/PredefinedPatterns.html).",
							"markdownDescription": "List of regular expression patterns or pattern names to exclude from spell checking.\n\nExample: `[\"href\"]` - to exclude html href pattern.\n\nRegular expressions use JavaScript regular expression syntax.\n\nExample: to ignore ALL-CAPS words\n\nJSON\n```json\n\"ignoreRegExpList\": [\"/\\\\b[A-Z]+\\\\b/g\"]\n```\n\nYAML\n```yaml\nignoreRegExpList:\n  - >-\n   /\\b[A-Z]+\\b/g\n```\n\nBy default, several patterns are excluded. See\n[Configuration](https://cspell.org/configuration/patterns) for more details.\n\nWhile you can create your own patterns, you can also leverage several patterns that are\n[built-in to CSpell](https://cspell.org/types/cspell-types/types/PredefinedPatterns.html)."
						},
						"ignoreWords": {
							"description": "List of words to be ignored. An ignored word will not show up as an error, even if it is also in the `flagWords`.",
							"items": {
								"type": "string"
							},
							"markdownDescription": "List of words to be ignored. An ignored word will not show up as an error, even if it is\nalso in the `flagWords`.",
							"type": "array"
						},
						"includeRegExpList": {
							"$ref": "#/definitions/RegExpPatternList",
							"description": "List of regular expression patterns or defined pattern names to match for spell checking.\n\nIf this property is defined, only text matching the included patterns will be checked.\n\nWhile you can create your own patterns, you can also leverage several patterns that are [built-in to CSpell](https://cspell.org/types/cspell-types/types/PredefinedPatterns.html).",
							"markdownDescription": "List of regular expression patterns or defined pattern names to match for spell checking.\n\nIf this property is defined, only text matching the included patterns will be checked.\n\nWhile you can create your own patterns, you can also leverage several patterns that are\n[built-in to CSpell](https://cspell.org/types/cspell-types/types/PredefinedPatterns.html)."
						},
						"languageId": {
							"$ref": "#/definitions/MatchingFileType",
							"description": "The language id.  Ex: \"typescript\", \"html\", or \"php\".  \"*\" -- will match all languages.",
							"markdownDescription": "The language id.  Ex: \"typescript\", \"html\", or \"php\".  \"*\" -- will match all languages."
						},
						"local": {
							"anyOf": [
								{
									"$ref": "#/definitions/LocaleId"
								},
								{
									"items": {
										"$ref": "#/definitions/LocaleId"
									},
									"type": "array"
								}
							],
							"deprecated": true,
							"deprecationMessage": "Use `locale` instead.",
							"description": "Deprecated - The locale filter, matches against the language. This can be a comma separated list. \"*\" will match all locales.",
							"markdownDescription": "Deprecated - The locale filter, matches against the language. This can be a comma separated list. \"*\" will match all locales."
						},
						"locale": {
							"anyOf": [
								{
									"$ref": "#/definitions/LocaleId"
								},
								{
									"items": {
										"$ref": "#/definitions/LocaleId"
									},
									"type": "array"
								}
							],
							"description": "The locale filter, matches against the language. This can be a comma separated list. \"*\" will match all locales.",
							"markdownDescription": "The locale filter, matches against the language. This can be a comma separated list. \"*\" will match all locales."
						},
						"name": {
							"description": "Optional name of configuration.",
							"markdownDescription": "Optional name of configuration.",
							"type": "string"
						},
						"noSuggestDictionaries": {
							"description": "Optional list of dictionaries that will not be used for suggestions. Words in these dictionaries are considered correct, but will not be used when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in one of these dictionaries, it will be removed from the set of possible suggestions.",
							"items": {
								"$ref": "#/definitions/DictionaryReference"
							},
							"markdownDescription": "Optional list of dictionaries that will not be used for suggestions.\nWords in these dictionaries are considered correct, but will not be\nused when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in\none of these dictionaries, it will be removed from the set of\npossible suggestions.",
							"type": "array"
						},
						"patterns": {
							"description": "Defines a list of patterns that can be used with the `ignoreRegExpList` and `includeRegExpList` options.\n\nFor example:\n\n```javascript \"ignoreRegExpList\": [\"comments\"], \"patterns\": [   {     \"name\": \"comment-single-line\",     \"pattern\": \"/#.*​/g\"   },   {     \"name\": \"comment-multi-line\",     \"pattern\": \"/(?:\\\\/\\\\*[\\\\s\\\\S]*?\\\\*\\\\/)/g\"   },   // You can also combine multiple named patterns into one single named pattern   {     \"name\": \"comments\",     \"pattern\": [\"comment-single-line\", \"comment-multi-line\"]   } ] ``` Defines a list of patterns that can be used with the `ignoreRegExpList` and `includeRegExpList` options.",
							"items": {
								"$ref": "#/definitions/RegExpPatternDefinition"
							},
							"markdownDescription": "Defines a list of patterns that can be used with the `ignoreRegExpList` and\n`includeRegExpList` options.\n\nFor example:\n\n```javascript\n\"ignoreRegExpList\": [\"comments\"],\n\"patterns\": [\n  {\n    \"name\": \"comment-single-line\",\n    \"pattern\": \"/#.*​/g\"\n  },\n  {\n    \"name\": \"comment-multi-line\",\n    \"pattern\": \"/(?:\\\\/\\\\*[\\\\s\\\\S]*?\\\\*\\\\/)/g\"\n  },\n  // You can also combine multiple named patterns into one single named pattern\n  {\n    \"name\": \"comments\",\n    \"pattern\": [\"comment-single-line\", \"comment-multi-line\"]\n  }\n]\n```\nDefines a list of patterns that can be used with the `ignoreRegExpList` and\n`includeRegExpList` options.",
							"type": "array"
						},
						"suggestWords": {
							"description": "A list of suggested replacements for words. Suggested words provide a way to make preferred suggestions on word replacements. To hint at a preferred change, but not to require it.\n\nFormat of `suggestWords`\n- Single suggestion (possible auto fix)     - `word: suggestion`     - `word->suggestion`\n- Multiple suggestions (not auto fixable)    - `word: first, second, third`    - `word->first, second, third`",
							"items": {
								"type": "string"
							},
							"markdownDescription": "A list of suggested replacements for words.\nSuggested words provide a way to make preferred suggestions on word replacements.\nTo hint at a preferred change, but not to require it.\n\nFormat of `suggestWords`\n- Single suggestion (possible auto fix)\n    - `word: suggestion`\n    - `word->suggestion`\n- Multiple suggestions (not auto fixable)\n   - `word: first, second, third`\n   - `word->first, second, third`",
							"type": "array"
						},
						"words": {
							"description": "List of words to be considered correct.",
							"items": {
								"type": "string"
							},
							"markdownDescription": "List of words to be considered correct.",
							"type": "array"
						}
					},
					"required": [
						"languageId"
					],
					"type": "object"
				},
				"LocaleId": {
					"description": "This is a written language locale like: 'en', 'en-GB', 'fr', 'es', 'de', etc.",
					"markdownDescription": "This is a written language locale like: 'en', 'en-GB', 'fr', 'es', 'de', etc.",
					"type": "string"
				},
				"MatchingFileType": {
					"anyOf": [
						{
							"$ref": "#/definitions/LanguageId"
						},
						{
							"items": {
								"$ref": "#/definitions/LanguageId"
							},
							"type": "array"
						}
					]
				},
				"OverrideSettings": {
					"additionalProperties": false,
					"properties": {
						"allowCompoundWords": {
							"default": false,
							"description": "True to enable compound word checking. See [Case Sensitivity](https://cspell.org/docs/case-sensitive/) for more details.",
							"markdownDescription": "True to enable compound word checking. See [Case Sensitivity](https://cspell.org/docs/case-sensitive/) for more details.",
							"type": "boolean"
						},
						"caseSensitive": {
							"default": false,
							"description": "Determines if words must match case and accent rules.\n\n- `false` - Case is ignored and accents can be missing on the entire word.   Incorrect accents or partially missing accents will be marked as incorrect.\n- `true` - Case and accents are enforced.",
							"markdownDescription": "Determines if words must match case and accent rules.\n\n- `false` - Case is ignored and accents can be missing on the entire word.\n  Incorrect accents or partially missing accents will be marked as incorrect.\n- `true` - Case and accents are enforced.",
							"type": "boolean"
						},
						"description": {
							"description": "Optional description of configuration.",
							"markdownDescription": "Optional description of configuration.",
							"type": "string"
						},
						"dictionaries": {
							"description": "Optional list of dictionaries to use. Each entry should match the name of the dictionary.\n\nTo remove a dictionary from the list, add `!` before the name.\n\nFor example, `!typescript` will turn off the dictionary with the name `typescript`.\n\nSee the [Dictionaries](https://cspell.org/docs/dictionaries/) and [Custom Dictionaries](https://cspell.org/docs/dictionaries-custom/) for more details.",
							"items": {
								"$ref": "#/definitions/DictionaryReference"
							},
							"markdownDescription": "Optional list of dictionaries to use. Each entry should match the name of the dictionary.\n\nTo remove a dictionary from the list, add `!` before the name.\n\nFor example, `!typescript` will turn off the dictionary with the name `typescript`.\n\nSee the [Dictionaries](https://cspell.org/docs/dictionaries/)\nand [Custom Dictionaries](https://cspell.org/docs/dictionaries-custom/) for more details.",
							"type": "array"
						},
						"dictionaryDefinitions": {
							"description": "Define additional available dictionaries.\n\nFor example, you can use the following to add a custom dictionary:\n\n```json \"dictionaryDefinitions\": [   { \"name\": \"custom-words\", \"path\": \"./custom-words.txt\"} ], \"dictionaries\": [\"custom-words\"] ```",
							"items": {
								"$ref": "#/definitions/DictionaryDefinition"
							},
							"markdownDescription": "Define additional available dictionaries.\n\nFor example, you can use the following to add a custom dictionary:\n\n```json\n\"dictionaryDefinitions\": [\n  { \"name\": \"custom-words\", \"path\": \"./custom-words.txt\"}\n],\n\"dictionaries\": [\"custom-words\"]\n```",
							"type": "array"
						},
						"enableFiletypes": {
							"description": "Enable / Disable checking file types (languageIds).\n\nThese are in additional to the file types specified by `cSpell.enabledLanguageIds`.\n\nTo disable a language, prefix with `!` as in `!json`,\n\nExample: ``` jsonc       // enable checking for jsonc !json       // disable checking for json kotlin      // enable checking for kotlin ```",
							"items": {
								"$ref": "#/definitions/LanguageIdSingle"
							},
							"markdownDescription": "Enable / Disable checking file types (languageIds).\n\nThese are in additional to the file types specified by `cSpell.enabledLanguageIds`.\n\nTo disable a language, prefix with `!` as in `!json`,\n\nExample:\n```\njsonc       // enable checking for jsonc\n!json       // disable checking for json\nkotlin      // enable checking for kotlin\n```",
							"scope": "resource",
							"title": "File Types to Check",
							"type": "array",
							"uniqueItems": true
						},
						"enabled": {
							"default": true,
							"description": "Is the spell checker enabled.",
							"markdownDescription": "Is the spell checker enabled.",
							"type": "boolean"
						},
						"enabledLanguageIds": {
							"description": "languageIds for the files to spell check.",
							"items": {
								"$ref": "#/definitions/LanguageIdSingle"
							},
							"markdownDescription": "languageIds for the files to spell check.",
							"type": "array"
						},
						"filename": {
							"anyOf": [
								{
									"$ref": "#/definitions/Glob"
								},
								{
									"items": {
										"$ref": "#/definitions/Glob"
									},
									"type": "array"
								}
							],
							"description": "Glob pattern or patterns to match against.",
							"markdownDescription": "Glob pattern or patterns to match against."
						},
						"flagWords": {
							"description": "List of words to always be considered incorrect. Words found in `flagWords` override `words`.\n\nFormat of `flagWords`\n- single word entry - `word`\n- with suggestions - `word:suggestion` or `word->suggestion, suggestions`\n\nExample: ```ts \"flagWords\": [   \"color: colour\",   \"incase: in case, encase\",   \"canot->cannot\",   \"cancelled->canceled\" ] ```",
							"items": {
								"type": "string"
							},
							"markdownDescription": "List of words to always be considered incorrect. Words found in `flagWords` override `words`.\n\nFormat of `flagWords`\n- single word entry - `word`\n- with suggestions - `word:suggestion` or `word->suggestion, suggestions`\n\nExample:\n```ts\n\"flagWords\": [\n  \"color: colour\",\n  \"incase: in case, encase\",\n  \"canot->cannot\",\n  \"cancelled->canceled\"\n]\n```",
							"type": "array"
						},
						"id": {
							"description": "Optional identifier.",
							"markdownDescription": "Optional identifier.",
							"type": "string"
						},
						"ignoreRegExpList": {
							"$ref": "#/definitions/RegExpPatternList",
							"description": "List of regular expression patterns or pattern names to exclude from spell checking.\n\nExample: `[\"href\"]` - to exclude html href pattern.\n\nRegular expressions use JavaScript regular expression syntax.\n\nExample: to ignore ALL-CAPS words\n\nJSON ```json \"ignoreRegExpList\": [\"/\\\\b[A-Z]+\\\\b/g\"] ```\n\nYAML ```yaml ignoreRegExpList:   - >-    /\\b[A-Z]+\\b/g ```\n\nBy default, several patterns are excluded. See [Configuration](https://cspell.org/configuration/patterns) for more details.\n\nWhile you can create your own patterns, you can also leverage several patterns that are [built-in to CSpell](https://cspell.org/types/cspell-types/types/PredefinedPatterns.html).",
							"markdownDescription": "List of regular expression patterns or pattern names to exclude from spell checking.\n\nExample: `[\"href\"]` - to exclude html href pattern.\n\nRegular expressions use JavaScript regular expression syntax.\n\nExample: to ignore ALL-CAPS words\n\nJSON\n```json\n\"ignoreRegExpList\": [\"/\\\\b[A-Z]+\\\\b/g\"]\n```\n\nYAML\n```yaml\nignoreRegExpList:\n  - >-\n   /\\b[A-Z]+\\b/g\n```\n\nBy default, several patterns are excluded. See\n[Configuration](https://cspell.org/configuration/patterns) for more details.\n\nWhile you can create your own patterns, you can also leverage several patterns that are\n[built-in to CSpell](https://cspell.org/types/cspell-types/types/PredefinedPatterns.html)."
						},
						"ignoreWords": {
							"description": "List of words to be ignored. An ignored word will not show up as an error, even if it is also in the `flagWords`.",
							"items": {
								"type": "string"
							},
							"markdownDescription": "List of words to be ignored. An ignored word will not show up as an error, even if it is\nalso in the `flagWords`.",
							"type": "array"
						},
						"includeRegExpList": {
							"$ref": "#/definitions/RegExpPatternList",
							"description": "List of regular expression patterns or defined pattern names to match for spell checking.\n\nIf this property is defined, only text matching the included patterns will be checked.\n\nWhile you can create your own patterns, you can also leverage several patterns that are [built-in to CSpell](https://cspell.org/types/cspell-types/types/PredefinedPatterns.html).",
							"markdownDescription": "List of regular expression patterns or defined pattern names to match for spell checking.\n\nIf this property is defined, only text matching the included patterns will be checked.\n\nWhile you can create your own patterns, you can also leverage several patterns that are\n[built-in to CSpell](https://cspell.org/types/cspell-types/types/PredefinedPatterns.html)."
						},
						"language": {
							"$ref": "#/definitions/LocaleId",
							"description": "Sets the locale.",
							"markdownDescription": "Sets the locale."
						},
						"languageId": {
							"$ref": "#/definitions/MatchingFileType",
							"description": "Sets the programming language id to match file type.",
							"markdownDescription": "Sets the programming language id to match file type."
						},
						"languageSettings": {
							"description": "Additional settings for individual languages.\n\nSee [Language Settings](https://cspell.org/configuration/language-settings/) for more details.",
							"items": {
								"$ref": "#/definitions/LanguageSetting"
							},
							"markdownDescription": "Additional settings for individual languages.\n\nSee [Language Settings](https://cspell.org/configuration/language-settings/) for more details.",
							"type": "array"
						},
						"loadDefaultConfiguration": {
							"default": true,
							"description": "By default, the bundled dictionary configurations are loaded. Explicitly setting this to `false` will prevent ALL default configuration from being loaded.",
							"markdownDescription": "By default, the bundled dictionary configurations are loaded. Explicitly setting this to `false`\nwill prevent ALL default configuration from being loaded.",
							"type": "boolean"
						},
						"maxDuplicateProblems": {
							"default": 5,
							"description": "The maximum number of times the same word can be flagged as an error in a file.",
							"markdownDescription": "The maximum number of times the same word can be flagged as an error in a file.",
							"type": "number"
						},
						"maxNumberOfProblems": {
							"default": 10000,
							"description": "The maximum number of problems to report in a file.",
							"markdownDescription": "The maximum number of problems to report in a file.",
							"type": "number"
						},
						"minWordLength": {
							"default": 4,
							"description": "The minimum length of a word before checking it against a dictionary.",
							"markdownDescription": "The minimum length of a word before checking it against a dictionary.",
							"type": "number"
						},
						"name": {
							"description": "Optional name of configuration.",
							"markdownDescription": "Optional name of configuration.",
							"type": "string"
						},
						"noSuggestDictionaries": {
							"description": "Optional list of dictionaries that will not be used for suggestions. Words in these dictionaries are considered correct, but will not be used when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in one of these dictionaries, it will be removed from the set of possible suggestions.",
							"items": {
								"$ref": "#/definitions/DictionaryReference"
							},
							"markdownDescription": "Optional list of dictionaries that will not be used for suggestions.\nWords in these dictionaries are considered correct, but will not be\nused when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in\none of these dictionaries, it will be removed from the set of\npossible suggestions.",
							"type": "array"
						},
						"numSuggestions": {
							"default": 10,
							"description": "Number of suggestions to make.",
							"markdownDescription": "Number of suggestions to make.",
							"type": "number"
						},
						"patterns": {
							"description": "Defines a list of patterns that can be used with the `ignoreRegExpList` and `includeRegExpList` options.\n\nFor example:\n\n```javascript \"ignoreRegExpList\": [\"comments\"], \"patterns\": [   {     \"name\": \"comment-single-line\",     \"pattern\": \"/#.*​/g\"   },   {     \"name\": \"comment-multi-line\",     \"pattern\": \"/(?:\\\\/\\\\*[\\\\s\\\\S]*?\\\\*\\\\/)/g\"   },   // You can also combine multiple named patterns into one single named pattern   {     \"name\": \"comments\",     \"pattern\": [\"comment-single-line\", \"comment-multi-line\"]   } ] ``` Defines a list of patterns that can be used with the `ignoreRegExpList` and `includeRegExpList` options.",
							"items": {
								"$ref": "#/definitions/RegExpPatternDefinition"
							},
							"markdownDescription": "Defines a list of patterns that can be used with the `ignoreRegExpList` and\n`includeRegExpList` options.\n\nFor example:\n\n```javascript\n\"ignoreRegExpList\": [\"comments\"],\n\"patterns\": [\n  {\n    \"name\": \"comment-single-line\",\n    \"pattern\": \"/#.*​/g\"\n  },\n  {\n    \"name\": \"comment-multi-line\",\n    \"pattern\": \"/(?:\\\\/\\\\*[\\\\s\\\\S]*?\\\\*\\\\/)/g\"\n  },\n  // You can also combine multiple named patterns into one single named pattern\n  {\n    \"name\": \"comments\",\n    \"pattern\": [\"comment-single-line\", \"comment-multi-line\"]\n  }\n]\n```\nDefines a list of patterns that can be used with the `ignoreRegExpList` and\n`includeRegExpList` options.",
							"type": "array"
						},
						"pnpFiles": {
							"default": [
								".pnp.js",
								".pnp.cjs"
							],
							"description": "The PnP files to search for. Note: `.mjs` files are not currently supported.",
							"items": {
								"type": "string"
							},
							"markdownDescription": "The PnP files to search for. Note: `.mjs` files are not currently supported.",
							"type": "array"
						},
						"suggestWords": {
							"description": "A list of suggested replacements for words. Suggested words provide a way to make preferred suggestions on word replacements. To hint at a preferred change, but not to require it.\n\nFormat of `suggestWords`\n- Single suggestion (possible auto fix)     - `word: suggestion`     - `word->suggestion`\n- Multiple suggestions (not auto fixable)    - `word: first, second, third`    - `word->first, second, third`",
							"items": {
								"type": "string"
							},
							"markdownDescription": "A list of suggested replacements for words.\nSuggested words provide a way to make preferred suggestions on word replacements.\nTo hint at a preferred change, but not to require it.\n\nFormat of `suggestWords`\n- Single suggestion (possible auto fix)\n    - `word: suggestion`\n    - `word->suggestion`\n- Multiple suggestions (not auto fixable)\n   - `word: first, second, third`\n   - `word->first, second, third`",
							"type": "array"
						},
						"suggestionNumChanges": {
							"default": 3,
							"description": "The maximum number of changes allowed on a word to be considered a suggestions.\n\nFor example, appending an `s` onto `example` -> `examples` is considered 1 change.\n\nRange: between 1 and 5.",
							"markdownDescription": "The maximum number of changes allowed on a word to be considered a suggestions.\n\nFor example, appending an `s` onto `example` -> `examples` is considered 1 change.\n\nRange: between 1 and 5.",
							"type": "number"
						},
						"suggestionsTimeout": {
							"default": 500,
							"description": "The maximum amount of time in milliseconds to generate suggestions for a word.",
							"markdownDescription": "The maximum amount of time in milliseconds to generate suggestions for a word.",
							"type": "number"
						},
						"usePnP": {
							"default": false,
							"description": "Packages managers like Yarn 2 use a `.pnp.cjs` file to assist in loading packages stored in the repository.\n\nWhen true, the spell checker will search up the directory structure for the existence of a PnP file and load it.",
							"markdownDescription": "Packages managers like Yarn 2 use a `.pnp.cjs` file to assist in loading\npackages stored in the repository.\n\nWhen true, the spell checker will search up the directory structure for the existence\nof a PnP file and load it.",
							"type": "boolean"
						},
						"words": {
							"description": "List of words to be considered correct.",
							"items": {
								"type": "string"
							},
							"markdownDescription": "List of words to be considered correct.",
							"type": "array"
						}
					},
					"required": [
						"filename"
					],
					"type": "object"
				},
				"Pattern": {
					"type": "string"
				},
				"PatternAdjustment": {
					"additionalProperties": false,
					"properties": {
						"id": {
							"description": "Id of the Adjustment, i.e. `short-compound`",
							"markdownDescription": "Id of the Adjustment, i.e. `short-compound`",
							"type": "string"
						},
						"penalty": {
							"description": "The amount of penalty to apply.",
							"markdownDescription": "The amount of penalty to apply.",
							"type": "number"
						},
						"regexp": {
							"description": "RegExp pattern to match",
							"markdownDescription": "RegExp pattern to match",
							"type": "string"
						}
					},
					"required": [
						"id",
						"regexp",
						"penalty"
					],
					"type": "object"
				},
				"PatternId": {
					"description": "This matches the name in a pattern definition.",
					"markdownDescription": "This matches the name in a pattern definition.",
					"type": "string"
				},
				"PatternRef": {
					"anyOf": [
						{
							"$ref": "#/definitions/Pattern"
						},
						{
							"$ref": "#/definitions/PatternId"
						},
						{
							"$ref": "#/definitions/PredefinedPatterns"
						}
					],
					"description": "A PatternRef is a Pattern or PatternId.",
					"markdownDescription": "A PatternRef is a Pattern or PatternId."
				},
				"PredefinedPatterns": {
					"enum": [
						"Base64",
						"Base64MultiLine",
						"Base64SingleLine",
						"CStyleComment",
						"CStyleHexValue",
						"CSSHexValue",
						"CommitHash",
						"CommitHashLink",
						"Email",
						"EscapeCharacters",
						"HexValues",
						"href",
						"PhpHereDoc",
						"PublicKey",
						"RsaCert",
						"SshRsa",
						"SHA",
						"HashStrings",
						"SpellCheckerDisable",
						"SpellCheckerDisableBlock",
						"SpellCheckerDisableLine",
						"SpellCheckerDisableNext",
						"SpellCheckerIgnoreInDocSetting",
						"string",
						"UnicodeRef",
						"Urls",
						"UUID",
						"Everything"
					],
					"type": "string"
				},
				"RegExpPatternDefinition": {
					"additionalProperties": false,
					"properties": {
						"description": {
							"description": "Description of the pattern.",
							"markdownDescription": "Description of the pattern.",
							"type": "string"
						},
						"name": {
							"$ref": "#/definitions/PatternId",
							"description": "Pattern name, used as an identifier in ignoreRegExpList and includeRegExpList. It is possible to redefine one of the predefined patterns to override its value.",
							"markdownDescription": "Pattern name, used as an identifier in ignoreRegExpList and includeRegExpList.\nIt is possible to redefine one of the predefined patterns to override its value."
						},
						"pattern": {
							"anyOf": [
								{
									"$ref": "#/definitions/Pattern"
								},
								{
									"items": {
										"$ref": "#/definitions/Pattern"
									},
									"type": "array"
								}
							],
							"description": "RegExp pattern or array of RegExp patterns.",
							"markdownDescription": "RegExp pattern or array of RegExp patterns."
						}
					},
					"required": [
						"name",
						"pattern"
					],
					"type": "object"
				},
				"RegExpPatternList": {
					"description": "A list of pattern names or regular expressions.",
					"items": {
						"$ref": "#/definitions/PatternRef"
					},
					"markdownDescription": "A list of pattern names or regular expressions.",
					"type": "array"
				},
				"ReplaceEntry": {
					"items": {
						"type": "string"
					},
					"maxItems": 2,
					"minItems": 2,
					"type": "array"
				},
				"ReplaceMap": {
					"items": {
						"$ref": "#/definitions/ReplaceEntry"
					},
					"type": "array"
				},
				"ReporterModuleName": {
					"description": "The module or path to the the reporter to load.",
					"markdownDescription": "The module or path to the the reporter to load.",
					"type": "string"
				},
				"ReporterOptions": {
					"$ref": "#/definitions/Serializable",
					"description": "Options to send to the reporter. These are defined by the reporter.",
					"markdownDescription": "Options to send to the reporter. These are defined by the reporter."
				},
				"ReporterSettings": {
					"anyOf": [
						{
							"$ref": "#/definitions/ReporterModuleName"
						},
						{
							"items": {
								"$ref": "#/definitions/ReporterModuleName",
								"title": "name"
							},
							"maxItems": 1,
							"minItems": 1,
							"type": "array"
						},
						{
							"items": [
								{
									"$ref": "#/definitions/ReporterModuleName",
									"title": "name"
								},
								{
									"$ref": "#/definitions/ReporterOptions",
									"title": "options"
								}
							],
							"maxItems": 2,
							"minItems": 2,
							"type": "array"
						}
					],
					"description": "Declare a reporter to use.\n\n`default` - is a special name for the default cli reporter.\n\nExamples:\n- `\"default\"` - to use the default reporter\n- `\"@cspell/cspell-json-reporter\"` - use the cspell JSON reporter.\n- `[\"@cspell/cspell-json-reporter\", { \"outFile\": \"out.json\" }]`",
					"markdownDescription": "Declare a reporter to use.\n\n`default` - is a special name for the default cli reporter.\n\nExamples:\n- `\"default\"` - to use the default reporter\n- `\"@cspell/cspell-json-reporter\"` - use the cspell JSON reporter.\n- `[\"@cspell/cspell-json-reporter\", { \"outFile\": \"out.json\" }]`"
				},
				"Serializable": {
					"anyOf": [
						{
							"type": "number"
						},
						{
							"type": "string"
						},
						{
							"type": "boolean"
						},
						{
							"type": "null"
						},
						{
							"type": "object"
						}
					]
				},
				"SimpleGlob": {
					"description": "Simple Glob string, the root will be globRoot.",
					"markdownDescription": "Simple Glob string, the root will be globRoot.",
					"type": "string"
				},
				"SuggestionCostMapDef": {
					"anyOf": [
						{
							"$ref": "#/definitions/CostMapDefReplace"
						},
						{
							"$ref": "#/definitions/CostMapDefInsDel"
						},
						{
							"$ref": "#/definitions/CostMapDefSwap"
						}
					],
					"description": "A WeightedMapDef enables setting weights for edits between related characters and substrings.\n\nMultiple groups can be defined using a `|`. A multi-character substring is defined using `()`.\n\nFor example, in some languages, some letters sound alike.\n\n```yaml   map: 'sc(sh)(sch)(ss)|t(tt)' # two groups.   replace: 50    # Make it 1/2 the cost of a normal edit to replace a `t` with `tt`. ```\n\nThe following could be used to make inserting, removing, or replacing vowels cheaper. ```yaml   map: 'aeiouy'   insDel: 50     # Make it is cheaper to insert or delete a vowel.   replace: 45    # It is even cheaper to replace one with another. ```\n\nNote: the default edit distance is 100.",
					"markdownDescription": "A WeightedMapDef enables setting weights for edits between related characters and substrings.\n\nMultiple groups can be defined using a `|`.\nA multi-character substring is defined using `()`.\n\nFor example, in some languages, some letters sound alike.\n\n```yaml\n  map: 'sc(sh)(sch)(ss)|t(tt)' # two groups.\n  replace: 50    # Make it 1/2 the cost of a normal edit to replace a `t` with `tt`.\n```\n\nThe following could be used to make inserting, removing, or replacing vowels cheaper.\n```yaml\n  map: 'aeiouy'\n  insDel: 50     # Make it is cheaper to insert or delete a vowel.\n  replace: 45    # It is even cheaper to replace one with another.\n```\n\nNote: the default edit distance is 100."
				},
				"SuggestionCostsDefs": {
					"items": {
						"$ref": "#/definitions/SuggestionCostMapDef"
					},
					"type": "array"
				},
				"Version": {
					"anyOf": [
						{
							"$ref": "#/definitions/VersionLatest"
						},
						{
							"$ref": "#/definitions/VersionLegacy"
						}
					]
				},
				"VersionLatest": {
					"const": "0.2",
					"description": "Configuration File Version.",
					"markdownDescription": "Configuration File Version.",
					"type": "string"
				},
				"VersionLegacy": {
					"const": "0.1",
					"deprecated": true,
					"deprecationMessage": "Use `0.2` instead.",
					"description": "Legacy Configuration File Versions.",
					"markdownDescription": "Legacy Configuration File Versions.",
					"type": "string"
				}
			},
			"properties": {
				"$schema": {
					"default": "https://raw.githubusercontent.com/streetsidesoftware/cspell/main/cspell.schema.json",
					"description": "Url to JSON Schema",
					"markdownDescription": "Url to JSON Schema",
					"type": "string"
				},
				"allowCompoundWords": {
					"default": false,
					"description": "True to enable compound word checking. See [Case Sensitivity](https://cspell.org/docs/case-sensitive/) for more details.",
					"markdownDescription": "True to enable compound word checking. See [Case Sensitivity](https://cspell.org/docs/case-sensitive/) for more details.",
					"type": "boolean"
				},
				"cache": {
					"$ref": "#/definitions/CacheSettings",
					"description": "Define cache settings.",
					"markdownDescription": "Define cache settings."
				},
				"caseSensitive": {
					"default": false,
					"description": "Determines if words must match case and accent rules.\n\n- `false` - Case is ignored and accents can be missing on the entire word.   Incorrect accents or partially missing accents will be marked as incorrect.\n- `true` - Case and accents are enforced.",
					"markdownDescription": "Determines if words must match case and accent rules.\n\n- `false` - Case is ignored and accents can be missing on the entire word.\n  Incorrect accents or partially missing accents will be marked as incorrect.\n- `true` - Case and accents are enforced.",
					"type": "boolean"
				},
				"description": {
					"description": "Optional description of configuration.",
					"markdownDescription": "Optional description of configuration.",
					"type": "string"
				},
				"dictionaries": {
					"description": "Optional list of dictionaries to use. Each entry should match the name of the dictionary.\n\nTo remove a dictionary from the list, add `!` before the name.\n\nFor example, `!typescript` will turn off the dictionary with the name `typescript`.\n\nSee the [Dictionaries](https://cspell.org/docs/dictionaries/) and [Custom Dictionaries](https://cspell.org/docs/dictionaries-custom/) for more details.",
					"items": {
						"$ref": "#/definitions/DictionaryReference"
					},
					"markdownDescription": "Optional list of dictionaries to use. Each entry should match the name of the dictionary.\n\nTo remove a dictionary from the list, add `!` before the name.\n\nFor example, `!typescript` will turn off the dictionary with the name `typescript`.\n\nSee the [Dictionaries](https://cspell.org/docs/dictionaries/)\nand [Custom Dictionaries](https://cspell.org/docs/dictionaries-custom/) for more details.",
					"type": "array"
				},
				"dictionaryDefinitions": {
					"description": "Define additional available dictionaries.\n\nFor example, you can use the following to add a custom dictionary:\n\n```json \"dictionaryDefinitions\": [   { \"name\": \"custom-words\", \"path\": \"./custom-words.txt\"} ], \"dictionaries\": [\"custom-words\"] ```",
					"items": {
						"$ref": "#/definitions/DictionaryDefinition"
					},
					"markdownDescription": "Define additional available dictionaries.\n\nFor example, you can use the following to add a custom dictionary:\n\n```json\n\"dictionaryDefinitions\": [\n  { \"name\": \"custom-words\", \"path\": \"./custom-words.txt\"}\n],\n\"dictionaries\": [\"custom-words\"]\n```",
					"type": "array"
				},
				"enableFiletypes": {
					"description": "Enable / Disable checking file types (languageIds).\n\nThese are in additional to the file types specified by `cSpell.enabledLanguageIds`.\n\nTo disable a language, prefix with `!` as in `!json`,\n\nExample: ``` jsonc       // enable checking for jsonc !json       // disable checking for json kotlin      // enable checking for kotlin ```",
					"items": {
						"$ref": "#/definitions/LanguageIdSingle"
					},
					"markdownDescription": "Enable / Disable checking file types (languageIds).\n\nThese are in additional to the file types specified by `cSpell.enabledLanguageIds`.\n\nTo disable a language, prefix with `!` as in `!json`,\n\nExample:\n```\njsonc       // enable checking for jsonc\n!json       // disable checking for json\nkotlin      // enable checking for kotlin\n```",
					"scope": "resource",
					"title": "File Types to Check",
					"type": "array",
					"uniqueItems": true
				},
				"enableGlobDot": {
					"default": false,
					"description": "Enable scanning files and directories beginning with `.` (period).\n\nBy default, CSpell does not scan `hidden` files.",
					"markdownDescription": "Enable scanning files and directories beginning with `.` (period).\n\nBy default, CSpell does not scan `hidden` files.",
					"type": "boolean"
				},
				"enabled": {
					"default": true,
					"description": "Is the spell checker enabled.",
					"markdownDescription": "Is the spell checker enabled.",
					"type": "boolean"
				},
				"enabledLanguageIds": {
					"description": "languageIds for the files to spell check.",
					"items": {
						"$ref": "#/definitions/LanguageIdSingle"
					},
					"markdownDescription": "languageIds for the files to spell check.",
					"type": "array"
				},
				"failFast": {
					"default": false,
					"description": "Exit with non-zero code as soon as an issue/error is encountered (useful for CI or git hooks)",
					"markdownDescription": "Exit with non-zero code as soon as an issue/error is encountered (useful for CI or git hooks)",
					"type": "boolean"
				},
				"features": {
					"$ref": "#/definitions/Features",
					"description": "Configure CSpell features.",
					"markdownDescription": "Configure CSpell features."
				},
				"files": {
					"description": "Glob patterns of files to be checked.\n\nGlob patterns are relative to the `globRoot` of the configuration file that defines them.",
					"items": {
						"$ref": "#/definitions/Glob"
					},
					"markdownDescription": "Glob patterns of files to be checked.\n\nGlob patterns are relative to the `globRoot` of the configuration file that defines them.",
					"type": "array"
				},
				"flagWords": {
					"description": "List of words to always be considered incorrect. Words found in `flagWords` override `words`.\n\nFormat of `flagWords`\n- single word entry - `word`\n- with suggestions - `word:suggestion` or `word->suggestion, suggestions`\n\nExample: ```ts \"flagWords\": [   \"color: colour\",   \"incase: in case, encase\",   \"canot->cannot\",   \"cancelled->canceled\" ] ```",
					"items": {
						"type": "string"
					},
					"markdownDescription": "List of words to always be considered incorrect. Words found in `flagWords` override `words`.\n\nFormat of `flagWords`\n- single word entry - `word`\n- with suggestions - `word:suggestion` or `word->suggestion, suggestions`\n\nExample:\n```ts\n\"flagWords\": [\n  \"color: colour\",\n  \"incase: in case, encase\",\n  \"canot->cannot\",\n  \"cancelled->canceled\"\n]\n```",
					"type": "array"
				},
				"gitignoreRoot": {
					"anyOf": [
						{
							"$ref": "#/definitions/FsPath"
						},
						{
							"items": {
								"$ref": "#/definitions/FsPath"
							},
							"type": "array"
						}
					],
					"description": "Tells the spell checker to searching for `.gitignore` files when it reaches a matching root.",
					"markdownDescription": "Tells the spell checker to searching for `.gitignore` files when it reaches a matching root."
				},
				"globRoot": {
					"$ref": "#/definitions/FSPathResolvable",
					"description": "The root to use for glob patterns found in this configuration. Default: location of the configuration file.   For compatibility reasons, config files with version 0.1, the glob root will   default to be `${cwd}`.\n\nUse `globRoot` to define a different location. `globRoot` can be relative to the location of this configuration file. Defining globRoot, does not impact imported configurations.\n\nSpecial Values:\n- `${cwd}` - will be replaced with the current working directory.\n- `.` - will be the location of the containing configuration file.",
					"markdownDescription": "The root to use for glob patterns found in this configuration.\nDefault: location of the configuration file.\n  For compatibility reasons, config files with version 0.1, the glob root will\n  default to be `${cwd}`.\n\nUse `globRoot` to define a different location.\n`globRoot` can be relative to the location of this configuration file.\nDefining globRoot, does not impact imported configurations.\n\nSpecial Values:\n- `${cwd}` - will be replaced with the current working directory.\n- `.` - will be the location of the containing configuration file."
				},
				"id": {
					"description": "Optional identifier.",
					"markdownDescription": "Optional identifier.",
					"type": "string"
				},
				"ignorePaths": {
					"description": "Glob patterns of files to be ignored.\n\nGlob patterns are relative to the `globRoot` of the configuration file that defines them.",
					"items": {
						"$ref": "#/definitions/Glob"
					},
					"markdownDescription": "Glob patterns of files to be ignored.\n\nGlob patterns are relative to the `globRoot` of the configuration file that defines them.",
					"type": "array"
				},
				"ignoreRegExpList": {
					"$ref": "#/definitions/RegExpPatternList",
					"description": "List of regular expression patterns or pattern names to exclude from spell checking.\n\nExample: `[\"href\"]` - to exclude html href pattern.\n\nRegular expressions use JavaScript regular expression syntax.\n\nExample: to ignore ALL-CAPS words\n\nJSON ```json \"ignoreRegExpList\": [\"/\\\\b[A-Z]+\\\\b/g\"] ```\n\nYAML ```yaml ignoreRegExpList:   - >-    /\\b[A-Z]+\\b/g ```\n\nBy default, several patterns are excluded. See [Configuration](https://cspell.org/configuration/patterns) for more details.\n\nWhile you can create your own patterns, you can also leverage several patterns that are [built-in to CSpell](https://cspell.org/types/cspell-types/types/PredefinedPatterns.html).",
					"markdownDescription": "List of regular expression patterns or pattern names to exclude from spell checking.\n\nExample: `[\"href\"]` - to exclude html href pattern.\n\nRegular expressions use JavaScript regular expression syntax.\n\nExample: to ignore ALL-CAPS words\n\nJSON\n```json\n\"ignoreRegExpList\": [\"/\\\\b[A-Z]+\\\\b/g\"]\n```\n\nYAML\n```yaml\nignoreRegExpList:\n  - >-\n   /\\b[A-Z]+\\b/g\n```\n\nBy default, several patterns are excluded. See\n[Configuration](https://cspell.org/configuration/patterns) for more details.\n\nWhile you can create your own patterns, you can also leverage several patterns that are\n[built-in to CSpell](https://cspell.org/types/cspell-types/types/PredefinedPatterns.html)."
				},
				"ignoreWords": {
					"description": "List of words to be ignored. An ignored word will not show up as an error, even if it is also in the `flagWords`.",
					"items": {
						"type": "string"
					},
					"markdownDescription": "List of words to be ignored. An ignored word will not show up as an error, even if it is\nalso in the `flagWords`.",
					"type": "array"
				},
				"import": {
					"anyOf": [
						{
							"$ref": "#/definitions/FsPath"
						},
						{
							"items": {
								"$ref": "#/definitions/FsPath"
							},
							"type": "array"
						}
					],
					"description": "Allows this configuration to inherit configuration for one or more other files.\n\nSee [Importing / Extending Configuration](https://cspell.org/configuration/imports/) for more details.",
					"markdownDescription": "Allows this configuration to inherit configuration for one or more other files.\n\nSee [Importing / Extending Configuration](https://cspell.org/configuration/imports/) for more details."
				},
				"includeRegExpList": {
					"$ref": "#/definitions/RegExpPatternList",
					"description": "List of regular expression patterns or defined pattern names to match for spell checking.\n\nIf this property is defined, only text matching the included patterns will be checked.\n\nWhile you can create your own patterns, you can also leverage several patterns that are [built-in to CSpell](https://cspell.org/types/cspell-types/types/PredefinedPatterns.html).",
					"markdownDescription": "List of regular expression patterns or defined pattern names to match for spell checking.\n\nIf this property is defined, only text matching the included patterns will be checked.\n\nWhile you can create your own patterns, you can also leverage several patterns that are\n[built-in to CSpell](https://cspell.org/types/cspell-types/types/PredefinedPatterns.html)."
				},
				"language": {
					"$ref": "#/definitions/LocaleId",
					"default": "en",
					"description": "Current active spelling language. This specifies the language locale to use in choosing the general dictionary.\n\nFor example:\n\n- \"en-GB\" for British English.\n- \"en,nl\" to enable both English and Dutch.",
					"markdownDescription": "Current active spelling language. This specifies the language locale to use in choosing the\ngeneral dictionary.\n\nFor example:\n\n- \"en-GB\" for British English.\n- \"en,nl\" to enable both English and Dutch."
				},
				"languageId": {
					"$ref": "#/definitions/MatchingFileType",
					"description": "Forces the spell checker to assume a give language id. Used mainly as an Override.",
					"markdownDescription": "Forces the spell checker to assume a give language id. Used mainly as an Override."
				},
				"languageSettings": {
					"description": "Additional settings for individual languages.\n\nSee [Language Settings](https://cspell.org/configuration/language-settings/) for more details.",
					"items": {
						"$ref": "#/definitions/LanguageSetting"
					},
					"markdownDescription": "Additional settings for individual languages.\n\nSee [Language Settings](https://cspell.org/configuration/language-settings/) for more details.",
					"type": "array"
				},
				"loadDefaultConfiguration": {
					"default": true,
					"description": "By default, the bundled dictionary configurations are loaded. Explicitly setting this to `false` will prevent ALL default configuration from being loaded.",
					"markdownDescription": "By default, the bundled dictionary configurations are loaded. Explicitly setting this to `false`\nwill prevent ALL default configuration from being loaded.",
					"type": "boolean"
				},
				"maxDuplicateProblems": {
					"default": 5,
					"description": "The maximum number of times the same word can be flagged as an error in a file.",
					"markdownDescription": "The maximum number of times the same word can be flagged as an error in a file.",
					"type": "number"
				},
				"maxNumberOfProblems": {
					"default": 10000,
					"description": "The maximum number of problems to report in a file.",
					"markdownDescription": "The maximum number of problems to report in a file.",
					"type": "number"
				},
				"minWordLength": {
					"default": 4,
					"description": "The minimum length of a word before checking it against a dictionary.",
					"markdownDescription": "The minimum length of a word before checking it against a dictionary.",
					"type": "number"
				},
				"name": {
					"description": "Optional name of configuration.",
					"markdownDescription": "Optional name of configuration.",
					"type": "string"
				},
				"noConfigSearch": {
					"default": false,
					"description": "Prevents searching for local configuration when checking individual documents.",
					"markdownDescription": "Prevents searching for local configuration when checking individual documents.",
					"type": "boolean"
				},
				"noSuggestDictionaries": {
					"description": "Optional list of dictionaries that will not be used for suggestions. Words in these dictionaries are considered correct, but will not be used when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in one of these dictionaries, it will be removed from the set of possible suggestions.",
					"items": {
						"$ref": "#/definitions/DictionaryReference"
					},
					"markdownDescription": "Optional list of dictionaries that will not be used for suggestions.\nWords in these dictionaries are considered correct, but will not be\nused when making spell correction suggestions.\n\nNote: if a word is suggested by another dictionary, but found in\none of these dictionaries, it will be removed from the set of\npossible suggestions.",
					"type": "array"
				},
				"numSuggestions": {
					"default": 10,
					"description": "Number of suggestions to make.",
					"markdownDescription": "Number of suggestions to make.",
					"type": "number"
				},
				"overrides": {
					"description": "Overrides are used to apply settings for specific files in your project.\n\nFor example:\n\n```javascript \"overrides\": [   // Force `*.hrr` and `*.crr` files to be treated as `cpp` files:   {     \"filename\": \"**​/{*.hrr,*.crr}\",     \"languageId\": \"cpp\"   },   // Force `*.txt` to use the Dutch dictionary (Dutch dictionary needs to be installed separately):   {     \"language\": \"nl\",     \"filename\": \"**​/dutch/**​/*.txt\"   } ] ```",
					"items": {
						"$ref": "#/definitions/OverrideSettings"
					},
					"markdownDescription": "Overrides are used to apply settings for specific files in your project.\n\nFor example:\n\n```javascript\n\"overrides\": [\n  // Force `*.hrr` and `*.crr` files to be treated as `cpp` files:\n  {\n    \"filename\": \"**​/{*.hrr,*.crr}\",\n    \"languageId\": \"cpp\"\n  },\n  // Force `*.txt` to use the Dutch dictionary (Dutch dictionary needs to be installed separately):\n  {\n    \"language\": \"nl\",\n    \"filename\": \"**​/dutch/**​/*.txt\"\n  }\n]\n```",
					"type": "array"
				},
				"patterns": {
					"description": "Defines a list of patterns that can be used with the `ignoreRegExpList` and `includeRegExpList` options.\n\nFor example:\n\n```javascript \"ignoreRegExpList\": [\"comments\"], \"patterns\": [   {     \"name\": \"comment-single-line\",     \"pattern\": \"/#.*​/g\"   },   {     \"name\": \"comment-multi-line\",     \"pattern\": \"/(?:\\\\/\\\\*[\\\\s\\\\S]*?\\\\*\\\\/)/g\"   },   // You can also combine multiple named patterns into one single named pattern   {     \"name\": \"comments\",     \"pattern\": [\"comment-single-line\", \"comment-multi-line\"]   } ] ``` Defines a list of patterns that can be used with the `ignoreRegExpList` and `includeRegExpList` options.",
					"items": {
						"$ref": "#/definitions/RegExpPatternDefinition"
					},
					"markdownDescription": "Defines a list of patterns that can be used with the `ignoreRegExpList` and\n`includeRegExpList` options.\n\nFor example:\n\n```javascript\n\"ignoreRegExpList\": [\"comments\"],\n\"patterns\": [\n  {\n    \"name\": \"comment-single-line\",\n    \"pattern\": \"/#.*​/g\"\n  },\n  {\n    \"name\": \"comment-multi-line\",\n    \"pattern\": \"/(?:\\\\/\\\\*[\\\\s\\\\S]*?\\\\*\\\\/)/g\"\n  },\n  // You can also combine multiple named patterns into one single named pattern\n  {\n    \"name\": \"comments\",\n    \"pattern\": [\"comment-single-line\", \"comment-multi-line\"]\n  }\n]\n```\nDefines a list of patterns that can be used with the `ignoreRegExpList` and\n`includeRegExpList` options.",
					"type": "array"
				},
				"pnpFiles": {
					"default": [
						".pnp.js",
						".pnp.cjs"
					],
					"description": "The PnP files to search for. Note: `.mjs` files are not currently supported.",
					"items": {
						"type": "string"
					},
					"markdownDescription": "The PnP files to search for. Note: `.mjs` files are not currently supported.",
					"type": "array"
				},
				"readonly": {
					"default": false,
					"description": "Indicate that the configuration file should not be modified. This is used to prevent tools like the VS Code Spell Checker from modifying the file to add words and other configuration.",
					"markdownDescription": "Indicate that the configuration file should not be modified.\nThis is used to prevent tools like the VS Code Spell Checker from\nmodifying the file to add words and other configuration.",
					"type": "boolean"
				},
				"reporters": {
					"default": [
						"default"
					],
					"description": "Define which reports to use. `default` - is a special name for the default cli reporter.\n\nExamples:\n- `[\"default\"]` - to use the default reporter\n- `[\"@cspell/cspell-json-reporter\"]` - use the cspell JSON reporter.\n- `[[\"@cspell/cspell-json-reporter\", { \"outFile\": \"out.json\" }]]`\n- `[ \"default\", [\"@cspell/cspell-json-reporter\", { \"outFile\": \"out.json\" }]]` - Use both the default reporter and the cspell-json-reporter.",
					"items": {
						"$ref": "#/definitions/ReporterSettings"
					},
					"markdownDescription": "Define which reports to use.\n`default` - is a special name for the default cli reporter.\n\nExamples:\n- `[\"default\"]` - to use the default reporter\n- `[\"@cspell/cspell-json-reporter\"]` - use the cspell JSON reporter.\n- `[[\"@cspell/cspell-json-reporter\", { \"outFile\": \"out.json\" }]]`\n- `[ \"default\", [\"@cspell/cspell-json-reporter\", { \"outFile\": \"out.json\" }]]` - Use both the default reporter and the cspell-json-reporter.",
					"type": "array"
				},
				"showStatus": {
					"deprecated": true,
					"description": "Show status.",
					"markdownDescription": "Show status.",
					"type": "boolean"
				},
				"spellCheckDelayMs": {
					"deprecated": true,
					"description": "Delay in ms after a document has changed before checking it for spelling errors.",
					"markdownDescription": "Delay in ms after a document has changed before checking it for spelling errors.",
					"type": "number"
				},
				"suggestWords": {
					"description": "A list of suggested replacements for words. Suggested words provide a way to make preferred suggestions on word replacements. To hint at a preferred change, but not to require it.\n\nFormat of `suggestWords`\n- Single suggestion (possible auto fix)     - `word: suggestion`     - `word->suggestion`\n- Multiple suggestions (not auto fixable)    - `word: first, second, third`    - `word->first, second, third`",
					"items": {
						"type": "string"
					},
					"markdownDescription": "A list of suggested replacements for words.\nSuggested words provide a way to make preferred suggestions on word replacements.\nTo hint at a preferred change, but not to require it.\n\nFormat of `suggestWords`\n- Single suggestion (possible auto fix)\n    - `word: suggestion`\n    - `word->suggestion`\n- Multiple suggestions (not auto fixable)\n   - `word: first, second, third`\n   - `word->first, second, third`",
					"type": "array"
				},
				"suggestionNumChanges": {
					"default": 3,
					"description": "The maximum number of changes allowed on a word to be considered a suggestions.\n\nFor example, appending an `s` onto `example` -> `examples` is considered 1 change.\n\nRange: between 1 and 5.",
					"markdownDescription": "The maximum number of changes allowed on a word to be considered a suggestions.\n\nFor example, appending an `s` onto `example` -> `examples` is considered 1 change.\n\nRange: between 1 and 5.",
					"type": "number"
				},
				"suggestionsTimeout": {
					"default": 500,
					"description": "The maximum amount of time in milliseconds to generate suggestions for a word.",
					"markdownDescription": "The maximum amount of time in milliseconds to generate suggestions for a word.",
					"type": "number"
				},
				"useGitignore": {
					"default": false,
					"description": "Tells the spell checker to load `.gitignore` files and skip files that match the globs in the `.gitignore` files found.",
					"markdownDescription": "Tells the spell checker to load `.gitignore` files and skip files that match the globs in the `.gitignore` files found.",
					"type": "boolean"
				},
				"usePnP": {
					"default": false,
					"description": "Packages managers like Yarn 2 use a `.pnp.cjs` file to assist in loading packages stored in the repository.\n\nWhen true, the spell checker will search up the directory structure for the existence of a PnP file and load it.",
					"markdownDescription": "Packages managers like Yarn 2 use a `.pnp.cjs` file to assist in loading\npackages stored in the repository.\n\nWhen true, the spell checker will search up the directory structure for the existence\nof a PnP file and load it.",
					"type": "boolean"
				},
				"userWords": {
					"description": "Words to add to global dictionary -- should only be in the user config file.",
					"items": {
						"type": "string"
					},
					"markdownDescription": "Words to add to global dictionary -- should only be in the user config file.",
					"type": "array"
				},
				"validateDirectives": {
					"description": "Verify that the in-document directives are correct.",
					"markdownDescription": "Verify that the in-document directives are correct.",
					"type": "boolean"
				},
				"version": {
					"$ref": "#/definitions/Version",
					"default": "0.2",
					"description": "Configuration format version of the settings file.\n\nThis controls how the settings in the configuration file behave.",
					"markdownDescription": "Configuration format version of the settings file.\n\nThis controls how the settings in the configuration file behave."
				},
				"words": {
					"description": "List of words to be considered correct.",
					"items": {
						"type": "string"
					},
					"markdownDescription": "List of words to be considered correct.",
					"type": "array"
				}
			},
			"type": "object"
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                                                                                                                                                                                                                                                                | valid |
	| {"version": "0.2", "language": "en", "words": ["aes", "ecies", "eciespy", "fastapi", "secp256k1", "Spacefile", "uvicorn"], "flagWords": ["hte"], "ignorePaths": [".git", ".github", ".gitignore", ".cspell.jsonc", ".pre-commit-config.yaml", "LICENSE", "poetry.lock"]} | true  |
@draft2020-12

Feature: optional-ecmascript-regex draft2020-12
    In order to use json-schema
    As a developer
    I want to support optional-ecmascript-regex in draft2020-12

Scenario Outline: ECMA 262 regex $ does not match trailing newline
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^abc$"
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # abc\n
        | #/000/tests/000/data | false | matches in Python, but not in ECMA 262                                           |
        # abc
        | #/000/tests/001/data | true  | matches                                                                          |

Scenario Outline: ECMA 262 regex converts \t to horizontal tab
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^\\t$"
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # \t
        | #/001/tests/000/data | false | does not match                                                                   |
        #  
        | #/001/tests/001/data | true  | matches                                                                          |

Scenario Outline: ECMA 262 regex escapes control codes with \c and upper letter
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^\\cC$"
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # \cC
        | #/002/tests/000/data | false | does not match                                                                   |
        # 
        | #/002/tests/001/data | true  | matches                                                                          |

Scenario Outline: ECMA 262 regex escapes control codes with \c and lower letter
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^\\cc$"
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # \cc
        | #/003/tests/000/data | false | does not match                                                                   |
        # 
        | #/003/tests/001/data | true  | matches                                                                          |

Scenario Outline: ECMA 262 \d matches ascii digits only
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^\\d$"
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 0
        | #/004/tests/000/data | true  | ASCII zero matches                                                               |
        # ߀
        | #/004/tests/001/data | false | NKO DIGIT ZERO does not match (unlike e.g. Python)                               |
        # ߀
        | #/004/tests/002/data | false | NKO DIGIT ZERO (as \u escape) does not match                                     |

Scenario Outline: ECMA 262 \D matches everything but ascii digits
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^\\D$"
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 0
        | #/005/tests/000/data | false | ASCII zero does not match                                                        |
        # ߀
        | #/005/tests/001/data | true  | NKO DIGIT ZERO matches (unlike e.g. Python)                                      |
        # ߀
        | #/005/tests/002/data | true  | NKO DIGIT ZERO (as \u escape) matches                                            |

Scenario Outline: ECMA 262 \w matches ascii letters only
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^\\w$"
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # a
        | #/006/tests/000/data | true  | ASCII 'a' matches                                                                |
        # é
        | #/006/tests/001/data | false | latin-1 e-acute does not match (unlike e.g. Python)                              |

Scenario Outline: ECMA 262 \W matches everything but ascii letters
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^\\W$"
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # a
        | #/007/tests/000/data | false | ASCII 'a' does not match                                                         |
        # é
        | #/007/tests/001/data | true  | latin-1 e-acute matches (unlike e.g. Python)                                     |

Scenario Outline: ECMA 262 \s matches whitespace
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^\\s$"
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        #  
        | #/008/tests/000/data | true  | ASCII space matches                                                              |
        #  
        | #/008/tests/001/data | true  | Character tabulation matches                                                     |
        #  
        | #/008/tests/002/data | true  | Line tabulation matches                                                          |
        #  
        | #/008/tests/003/data | true  | Form feed matches                                                                |
        #  
        | #/008/tests/004/data | true  | latin-1 non-breaking-space matches                                               |
        # ﻿
        | #/008/tests/005/data | true  | zero-width whitespace matches                                                    |
        #  
        | #/008/tests/006/data | true  | line feed matches (line terminator)                                              |
        #  
        | #/008/tests/007/data | true  | paragraph separator matches (line terminator)                                    |
        #  
        | #/008/tests/008/data | true  | EM SPACE matches (Space_Separator)                                               |
        # 
        | #/008/tests/009/data | false | Non-whitespace control does not match                                            |
        # –
        | #/008/tests/010/data | false | Non-whitespace does not match                                                    |

Scenario Outline: ECMA 262 \S matches everything but whitespace
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^\\S$"
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        #  
        | #/009/tests/000/data | false | ASCII space does not match                                                       |
        #  
        | #/009/tests/001/data | false | Character tabulation does not match                                              |
        #  
        | #/009/tests/002/data | false | Line tabulation does not match                                                   |
        #  
        | #/009/tests/003/data | false | Form feed does not match                                                         |
        #  
        | #/009/tests/004/data | false | latin-1 non-breaking-space does not match                                        |
        # ﻿
        | #/009/tests/005/data | false | zero-width whitespace does not match                                             |
        #  
        | #/009/tests/006/data | false | line feed does not match (line terminator)                                       |
        #  
        | #/009/tests/007/data | false | paragraph separator does not match (line terminator)                             |
        #  
        | #/009/tests/008/data | false | EM SPACE does not match (Space_Separator)                                        |
        # 
        | #/009/tests/009/data | true  | Non-whitespace control matches                                                   |
        # –
        | #/009/tests/010/data | true  | Non-whitespace matches                                                           |

Scenario Outline: patterns always use unicode semantics with pattern
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "pattern": "\\p{Letter}cole"
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # Les hivers de mon enfance etaient des saisons longues, longues. Nous vivions en trois lieux: l'ecole, l'eglise et la patinoire; mais la vraie vie etait sur la patinoire.
        | #/010/tests/000/data | true  | ascii character in json string                                                   |
        # Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.
        | #/010/tests/001/data | true  | literal unicode character in json string                                         |
        # Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.
        | #/010/tests/002/data | true  | unicode character in hex format in string                                        |
        # LES HIVERS DE MON ENFANCE ÉTAIENT DES SAISONS LONGUES, LONGUES. NOUS VIVIONS EN TROIS LIEUX: L'ÉCOLE, L'ÉGLISE ET LA PATINOIRE; MAIS LA VRAIE VIE ÉTAIT SUR LA PATINOIRE.
        | #/010/tests/003/data | false | unicode matching is case-sensitive                                               |

Scenario Outline: \w in patterns matches array[A-Za-z0-9_], not unicode letters
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "pattern": "\\wcole"
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/11/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # Les hivers de mon enfance etaient des saisons longues, longues. Nous vivions en trois lieux: l'ecole, l'eglise et la patinoire; mais la vraie vie etait sur la patinoire.
        | #/011/tests/000/data | true  | ascii character in json string                                                   |
        # Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.
        | #/011/tests/001/data | false | literal unicode character in json string                                         |
        # Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.
        | #/011/tests/002/data | false | unicode character in hex format in string                                        |
        # LES HIVERS DE MON ENFANCE ÉTAIENT DES SAISONS LONGUES, LONGUES. NOUS VIVIONS EN TROIS LIEUX: L'ÉCOLE, L'ÉGLISE ET LA PATINOIRE; MAIS LA VRAIE VIE ÉTAIT SUR LA PATINOIRE.
        | #/011/tests/003/data | false | unicode matching is case-sensitive                                               |

Scenario Outline: pattern with ASCII ranges
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "pattern": "[a-z]cole"
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/12/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.
        | #/012/tests/000/data | false | literal unicode character in json string                                         |
        # Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.
        | #/012/tests/001/data | false | unicode character in hex format in string                                        |
        # Les hivers de mon enfance etaient des saisons longues, longues. Nous vivions en trois lieux: l'ecole, l'eglise et la patinoire; mais la vraie vie etait sur la patinoire.
        | #/012/tests/002/data | true  | ascii characters match                                                           |

Scenario Outline: \d in pattern matches array[0-9], not unicode digits
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "pattern": "^\\d+$"
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/13/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 42
        | #/013/tests/000/data | true  | ascii digits                                                                     |
        # -%#
        | #/013/tests/001/data | false | ascii non-digits                                                                 |
        # ৪২
        | #/013/tests/002/data | false | non-ascii digits (BENGALI DIGIT FOUR, BENGALI DIGIT TWO)                         |

Scenario Outline: pattern with non-ASCII digits
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "pattern": "^\\p{digit}+$"
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/14/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 42
        | #/014/tests/000/data | true  | ascii digits                                                                     |
        # -%#
        | #/014/tests/001/data | false | ascii non-digits                                                                 |
        # ৪২
        | #/014/tests/002/data | true  | non-ascii digits (BENGALI DIGIT FOUR, BENGALI DIGIT TWO)                         |

Scenario Outline: patterns always use unicode semantics with patternProperties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "patternProperties": {
                "\\p{Letter}cole": true
            },
            "additionalProperties": false
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/15/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "l'ecole": "pas de vraie vie" }
        | #/015/tests/000/data | true  | ascii character in json string                                                   |
        # { "l'école": "pas de vraie vie" }
        | #/015/tests/001/data | true  | literal unicode character in json string                                         |
        # { "l'\u00e9cole": "pas de vraie vie" }
        | #/015/tests/002/data | true  | unicode character in hex format in string                                        |
        # { "L'ÉCOLE": "PAS DE VRAIE VIE" }
        | #/015/tests/003/data | false | unicode matching is case-sensitive                                               |

Scenario Outline: \w in patternProperties matches array[A-Za-z0-9_], not unicode letters
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "patternProperties": {
                "\\wcole": true
            },
            "additionalProperties": false
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/16/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "l'ecole": "pas de vraie vie" }
        | #/016/tests/000/data | true  | ascii character in json string                                                   |
        # { "l'école": "pas de vraie vie" }
        | #/016/tests/001/data | false | literal unicode character in json string                                         |
        # { "l'\u00e9cole": "pas de vraie vie" }
        | #/016/tests/002/data | false | unicode character in hex format in string                                        |
        # { "L'ÉCOLE": "PAS DE VRAIE VIE" }
        | #/016/tests/003/data | false | unicode matching is case-sensitive                                               |

Scenario Outline: patternProperties with ASCII ranges
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "patternProperties": {
                "[a-z]cole": true
            },
            "additionalProperties": false
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/17/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "l'école": "pas de vraie vie" }
        | #/017/tests/000/data | false | literal unicode character in json string                                         |
        # { "l'\u00e9cole": "pas de vraie vie" }
        | #/017/tests/001/data | false | unicode character in hex format in string                                        |
        # { "l'ecole": "pas de vraie vie" }
        | #/017/tests/002/data | true  | ascii characters match                                                           |

Scenario Outline: \d in patternProperties matches array[0-9], not unicode digits
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "patternProperties": {
                "^\\d+$": true
            },
            "additionalProperties": false
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/18/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "42": "life, the universe, and everything" }
        | #/018/tests/000/data | true  | ascii digits                                                                     |
        # { "-%#": "spending the year dead for tax reasons" }
        | #/018/tests/001/data | false | ascii non-digits                                                                 |
        # { "৪২": "khajit has wares if you have coin" }
        | #/018/tests/002/data | false | non-ascii digits (BENGALI DIGIT FOUR, BENGALI DIGIT TWO)                         |

Scenario Outline: patternProperties with non-ASCII digits
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "patternProperties": {
                "^\\p{digit}+$": true
            },
            "additionalProperties": false
        }
*/
    Given the input JSON file "optional/ecmascript-regex.json"
    And the schema at "#/19/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "42": "life, the universe, and everything" }
        | #/019/tests/000/data | true  | ascii digits                                                                     |
        # { "-%#": "spending the year dead for tax reasons" }
        | #/019/tests/001/data | false | ascii non-digits                                                                 |
        # { "৪২": "khajit has wares if you have coin" }
        | #/019/tests/002/data | true  | non-ascii digits (BENGALI DIGIT FOUR, BENGALI DIGIT TWO)                         |

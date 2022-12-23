@draft2019-09

Feature: const draft2019-09
    In order to use json-schema
    As a developer
    I want to support const in draft2019-09

Scenario Outline: const validation
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "const": 2
        }
*/
    Given the input JSON file "const.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | same value is valid                                                              |
        | #/000/tests/001/data | false | another value is invalid                                                         |
        | #/000/tests/002/data | false | another type is invalid                                                          |

Scenario Outline: const with object
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "const": {"foo": "bar", "baz": "bax"}
        }
*/
    Given the input JSON file "const.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | same object is valid                                                             |
        | #/001/tests/001/data | true  | same object with different property order is valid                               |
        | #/001/tests/002/data | false | another object is invalid                                                        |
        | #/001/tests/003/data | false | another type is invalid                                                          |

Scenario Outline: const with array
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "const": [{ "foo": "bar" }]
        }
*/
    Given the input JSON file "const.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | same array is valid                                                              |
        | #/002/tests/001/data | false | another array item is invalid                                                    |
        | #/002/tests/002/data | false | array with additional items is invalid                                           |

Scenario Outline: const with null
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "const": null
        }
*/
    Given the input JSON file "const.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | null is valid                                                                    |
        | #/003/tests/001/data | false | not null is invalid                                                              |

Scenario Outline: const with false does not match 0
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "const": false
        }
*/
    Given the input JSON file "const.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | false is valid                                                                   |
        | #/004/tests/001/data | false | integer zero is invalid                                                          |
        | #/004/tests/002/data | false | float zero is invalid                                                            |

Scenario Outline: const with true does not match 1
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "const": true
        }
*/
    Given the input JSON file "const.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | true is valid                                                                    |
        | #/005/tests/001/data | false | integer one is invalid                                                           |
        | #/005/tests/002/data | false | float one is invalid                                                             |

Scenario Outline: const with array[false] does not match array[0]
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "const": [false]
        }
*/
    Given the input JSON file "const.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | [false] is valid                                                                 |
        | #/006/tests/001/data | false | [0] is invalid                                                                   |
        | #/006/tests/002/data | false | [0.0] is invalid                                                                 |

Scenario Outline: const with array[true] does not match array[1]
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "const": [true]
        }
*/
    Given the input JSON file "const.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/007/tests/000/data | true  | [true] is valid                                                                  |
        | #/007/tests/001/data | false | [1] is invalid                                                                   |
        | #/007/tests/002/data | false | [1.0] is invalid                                                                 |

Scenario Outline: const with {"a": false} does not match {"a": 0}
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "const": {"a": false}
        }
*/
    Given the input JSON file "const.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/008/tests/000/data | true  | {"a": false} is valid                                                            |
        | #/008/tests/001/data | false | {"a": 0} is invalid                                                              |
        | #/008/tests/002/data | false | {"a": 0.0} is invalid                                                            |

Scenario Outline: const with {"a": true} does not match {"a": 1}
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "const": {"a": true}
        }
*/
    Given the input JSON file "const.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/009/tests/000/data | true  | {"a": true} is valid                                                             |
        | #/009/tests/001/data | false | {"a": 1} is invalid                                                              |
        | #/009/tests/002/data | false | {"a": 1.0} is invalid                                                            |

Scenario Outline: const with 0 does not match other zero-like types
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "const": 0
        }
*/
    Given the input JSON file "const.json"
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/010/tests/000/data | false | false is invalid                                                                 |
        | #/010/tests/001/data | true  | integer zero is valid                                                            |
        | #/010/tests/002/data | true  | float zero is valid                                                              |
        | #/010/tests/003/data | false | empty object is invalid                                                          |
        | #/010/tests/004/data | false | empty array is invalid                                                           |
        | #/010/tests/005/data | false | empty string is invalid                                                          |

Scenario Outline: const with 1 does not match true
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "const": 1
        }
*/
    Given the input JSON file "const.json"
    And the schema at "#/11/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/011/tests/000/data | false | true is invalid                                                                  |
        | #/011/tests/001/data | true  | integer one is valid                                                             |
        | #/011/tests/002/data | true  | float one is valid                                                               |

Scenario Outline: const with -2.0 matches integer and float types
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "const": -2.0
        }
*/
    Given the input JSON file "const.json"
    And the schema at "#/12/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/012/tests/000/data | true  | integer -2 is valid                                                              |
        | #/012/tests/001/data | false | integer 2 is invalid                                                             |
        | #/012/tests/002/data | true  | float -2.0 is valid                                                              |
        | #/012/tests/003/data | false | float 2.0 is invalid                                                             |
        | #/012/tests/004/data | false | float -2.00001 is invalid                                                        |

Scenario Outline: float and integers are equal up to 64-bit representation limits
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "const": 9007199254740992
        }
*/
    Given the input JSON file "const.json"
    And the schema at "#/13/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/013/tests/000/data | true  | integer is valid                                                                 |
        | #/013/tests/001/data | false | integer minus one is invalid                                                     |
        | #/013/tests/002/data | true  | float is valid                                                                   |
        | #/013/tests/003/data | false | float minus one is invalid                                                       |

Scenario Outline: nul characters in strings
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "const": "hello\u0000there"
        }
*/
    Given the input JSON file "const.json"
    And the schema at "#/14/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/014/tests/000/data | true  | match string with nul                                                            |
        | #/014/tests/001/data | false | do not match string lacking nul                                                  |

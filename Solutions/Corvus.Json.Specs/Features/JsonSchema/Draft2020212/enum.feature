@draft2020-12

Feature: enum draft2020-12
    In order to use json-schema
    As a developer
    I want to support enum in draft2020-12

Scenario Outline: simple enum validation
/* Schema: 
{"enum": [1, 2, 3]}
*/
    Given the input JSON file "enum.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | one of the enum is valid                                                         |
        | #/000/tests/001/data | false | something else is invalid                                                        |

Scenario Outline: heterogeneous enum validation
/* Schema: 
{"enum": [6, "foo", [], true, {"foo": 12}]}
*/
    Given the input JSON file "enum.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | one of the enum is valid                                                         |
        | #/001/tests/001/data | false | something else is invalid                                                        |
        | #/001/tests/002/data | false | objects are deep compared                                                        |
        | #/001/tests/003/data | true  | valid object matches                                                             |
        | #/001/tests/004/data | false | extra properties in object is invalid                                            |

Scenario Outline: heterogeneous enum-with-null validation
/* Schema: 
{ "enum": [6, null] }
*/
    Given the input JSON file "enum.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | null is valid                                                                    |
        | #/002/tests/001/data | true  | number is valid                                                                  |
        | #/002/tests/002/data | false | something else is invalid                                                        |

Scenario Outline: enums in properties
/* Schema: 
{
            "type":"object",
            "properties": {
                "foo": {"enum":["foo"]},
                "bar": {"enum":["bar"]}
            },
            "required": ["bar"]
        }
*/
    Given the input JSON file "enum.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | both properties are valid                                                        |
        | #/003/tests/001/data | false | wrong foo value                                                                  |
        | #/003/tests/002/data | false | wrong bar value                                                                  |
        | #/003/tests/003/data | true  | missing optional property is valid                                               |
        | #/003/tests/004/data | false | missing required property is invalid                                             |
        | #/003/tests/005/data | false | missing all properties is invalid                                                |

Scenario Outline: enum with escaped characters
/* Schema: 
{
            "enum": ["foo\nbar", "foo\rbar"]
        }
*/
    Given the input JSON file "enum.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | member 1 is valid                                                                |
        | #/004/tests/001/data | true  | member 2 is valid                                                                |
        | #/004/tests/002/data | false | another string is invalid                                                        |

Scenario Outline: enum with false does not match 0
/* Schema: 
{"enum": [false]}
*/
    Given the input JSON file "enum.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | false is valid                                                                   |
        | #/005/tests/001/data | false | integer zero is invalid                                                          |
        | #/005/tests/002/data | false | float zero is invalid                                                            |

Scenario Outline: enum with true does not match 1
/* Schema: 
{"enum": [true]}
*/
    Given the input JSON file "enum.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | true is valid                                                                    |
        | #/006/tests/001/data | false | integer one is invalid                                                           |
        | #/006/tests/002/data | false | float one is invalid                                                             |

Scenario Outline: enum with 0 does not match false
/* Schema: 
{"enum": [0]}
*/
    Given the input JSON file "enum.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/007/tests/000/data | false | false is invalid                                                                 |
        | #/007/tests/001/data | true  | integer zero is valid                                                            |
        | #/007/tests/002/data | true  | float zero is valid                                                              |

Scenario Outline: enum with 1 does not match true
/* Schema: 
{"enum": [1]}
*/
    Given the input JSON file "enum.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/008/tests/000/data | false | true is invalid                                                                  |
        | #/008/tests/001/data | true  | integer one is valid                                                             |
        | #/008/tests/002/data | true  | float one is valid                                                               |

Scenario Outline: nul characters in strings
/* Schema: 
{ "enum": [ "hello\u0000there" ] }
*/
    Given the input JSON file "enum.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/009/tests/000/data | true  | match string with nul                                                            |
        | #/009/tests/001/data | false | do not match string lacking nul                                                  |

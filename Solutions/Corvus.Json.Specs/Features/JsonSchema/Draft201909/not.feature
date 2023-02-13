@draft2019-09

Feature: not draft2019-09
    In order to use json-schema
    As a developer
    I want to support not in draft2019-09

Scenario Outline: not
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "not": {"type": "integer"}
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | allowed                                                                          |
        | #/000/tests/001/data | false | disallowed                                                                       |

Scenario Outline: not multiple types
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "not": {"type": ["integer", "boolean"]}
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | valid                                                                            |
        | #/001/tests/001/data | false | mismatch                                                                         |
        | #/001/tests/002/data | false | other mismatch                                                                   |

Scenario Outline: not more complex schema
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "not": {
                "type": "object",
                "properties": {
                    "foo": {
                        "type": "string"
                    }
                }
             }
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | match                                                                            |
        | #/002/tests/001/data | true  | other match                                                                      |
        | #/002/tests/002/data | false | mismatch                                                                         |

Scenario Outline: forbidden property
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "properties": {
                "foo": { 
                    "not": {}
                }
            }
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | false | property present                                                                 |
        | #/003/tests/001/data | true  | property absent                                                                  |

Scenario Outline: not with boolean schema true
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "not": true
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | false | any value is invalid                                                             |

Scenario Outline: not with boolean schema false
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "not": false
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | any value is valid                                                               |

Scenario Outline: collect annotations inside a 'not', even if collection is disabled
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "not": {
                "$comment": "this subschema must still produce annotations internally, even though the 'not' will ultimately discard them",
                "anyOf": [
                    true,
                    { "properties": { "foo": true } }
                ],
                "unevaluatedProperties": false
            }
        }
*/
    Given the input JSON file "not.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | no properties to evaluate                                                        |
        | #/006/tests/001/data | false | annotations are still collected inside a 'not'                                   |

@draft2020-12

Feature: content draft2020-12
    In order to use json-schema
    As a developer
    I want to support content in draft2020-12

Scenario Outline: validation of string-encoded content based on media type
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "contentMediaType": "application/json"
        }
*/
    Given the input JSON file "content.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | a valid JSON document                                                            |
        | #/000/tests/001/data | true  | an invalid JSON document; validates true                                         |
        | #/000/tests/002/data | true  | ignores non-strings                                                              |

Scenario Outline: validation of binary string-encoding
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "contentEncoding": "base64"
        }
*/
    Given the input JSON file "content.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | a valid base64 string                                                            |
        | #/001/tests/001/data | true  | an invalid base64 string (% is not a valid character); validates true            |
        | #/001/tests/002/data | true  | ignores non-strings                                                              |

Scenario Outline: validation of binary-encoded media type documents
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "contentMediaType": "application/json",
            "contentEncoding": "base64"
        }
*/
    Given the input JSON file "content.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | a valid base64-encoded JSON document                                             |
        | #/002/tests/001/data | true  | a validly-encoded invalid JSON document; validates true                          |
        | #/002/tests/002/data | true  | an invalid base64 string that is valid JSON; validates true                      |
        | #/002/tests/003/data | true  | ignores non-strings                                                              |

Scenario Outline: validation of binary-encoded media type documents with schema
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "contentMediaType": "application/json",
            "contentEncoding": "base64",
            "contentSchema": { "required": ["foo"], "properties": { "foo": { "type": "string" } } }
        }
*/
    Given the input JSON file "content.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | a valid base64-encoded JSON document                                             |
        | #/003/tests/001/data | true  | another valid base64-encoded JSON document                                       |
        | #/003/tests/002/data | true  | an invalid base64-encoded JSON document; validates true                          |
        | #/003/tests/003/data | true  | an empty object as a base64-encoded JSON document; validates true                |
        | #/003/tests/004/data | true  | an empty array as a base64-encoded JSON document                                 |
        | #/003/tests/005/data | true  | a validly-encoded invalid JSON document; validates true                          |
        | #/003/tests/006/data | true  | an invalid base64 string that is valid JSON; validates true                      |
        | #/003/tests/007/data | true  | ignores non-strings                                                              |

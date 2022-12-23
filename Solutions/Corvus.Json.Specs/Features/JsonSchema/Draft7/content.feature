@draft7

Feature: content draft7
    In order to use json-schema
    As a developer
    I want to support content in draft7

Scenario Outline: validation of string-encoded content based on media type
/* Schema: 
{
            "contentMediaType": "application/json"
        }
*/
    Given the input JSON file "optional/content.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | a valid JSON document                                                            |
        | #/000/tests/001/data | false | an invalid JSON document                                                         |
        | #/000/tests/002/data | true  | ignores non-strings                                                              |

Scenario Outline: validation of binary string-encoding
/* Schema: 
{
            "contentEncoding": "base64"
        }
*/
    Given the input JSON file "optional/content.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | a valid base64 string                                                            |
        | #/001/tests/001/data | false | an invalid base64 string (% is not a valid character)                            |
        | #/001/tests/002/data | true  | ignores non-strings                                                              |

Scenario Outline: validation of binary-encoded media type documents
/* Schema: 
{
            "contentMediaType": "application/json",
            "contentEncoding": "base64"
        }
*/
    Given the input JSON file "optional/content.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | a valid base64-encoded JSON document                                             |
        | #/002/tests/001/data | false | a validly-encoded invalid JSON document                                          |
        | #/002/tests/002/data | false | an invalid base64 string that is valid JSON                                      |
        | #/002/tests/003/data | true  | ignores non-strings                                                              |

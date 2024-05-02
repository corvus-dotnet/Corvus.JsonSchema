@draft2020-12

Feature: format-assertion draft2020-12
    In order to use json-schema
    As a developer
    I want to support format-assertion in draft2020-12

Scenario Outline: schema that uses custom metaschema with format-assertion: false
/* Schema: 
{
            "$id": "https://schema/using/format-assertion/false",
            "$schema": "http://localhost:1234/draft2020-12/format-assertion-false.json",
            "format": "ipv4"
        }
*/
    Given the input JSON file "optional/format-assertion.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 127.0.0.1
        | #/000/tests/000/data | true  | format-assertion: false: valid string                                            |
        # not-an-ipv4
        | #/000/tests/001/data | false | format-assertion: false: invalid string                                          |

Scenario Outline: schema that uses custom metaschema with format-assertion: true
/* Schema: 
{
            "$id": "https://schema/using/format-assertion/true",
            "$schema": "http://localhost:1234/draft2020-12/format-assertion-true.json",
            "format": "ipv4"
        }
*/
    Given the input JSON file "optional/format-assertion.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 127.0.0.1
        | #/001/tests/000/data | true  | format-assertion: true: valid string                                             |
        # not-an-ipv4
        | #/001/tests/001/data | false | format-assertion: true: invalid string                                           |

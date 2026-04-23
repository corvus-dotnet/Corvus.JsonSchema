@draft2020-12

Feature: optional-format-ecmascript-regex draft2020-12
    In order to use json-schema
    As a developer
    I want to support optional-format-ecmascript-regex in draft2020-12

Scenario Outline: \a is not an ECMA 262 control escape
/* Schema: 
{
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "format": "regex"
    }
*/
    Given the input JSON file "optional/format/ecmascript-regex.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # \a
        | #/000/tests/000/data | false | when used as a pattern                                                           |

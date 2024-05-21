@draft2019-09

Feature: defs draft2019-09
    In order to use json-schema
    As a developer
    I want to support defs in draft2019-09

Scenario Outline: validate definition against metaschema
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "https://json-schema.org/draft/2019-09/schema"
        }
*/
    Given the input JSON file "defs.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"$defs": {"foo": {"type": "integer"}}}
        | #/000/tests/000/data | true  | valid definition schema                                                          |
        # {"$defs": {"foo": {"type": 1}}}
        | #/000/tests/001/data | false | invalid definition schema                                                        |

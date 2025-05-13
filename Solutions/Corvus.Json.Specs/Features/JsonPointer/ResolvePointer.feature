Feature: Json Pointer resolution

Scenario: Resolve a top-level property pointer to a line and offset
    Given a JSON file
        """
        {
            "property1": {
                "nested1": {
                    "foo": "bar"
                }
            },
            "property2": {
                "nested1": {
                    "foo": "bar"
                }
            }
        }
        """
    When I try to get the line and offset for pointer '#/property2'
    Then TryGetLineAndOffsetForPointer returns true
    And the pointer resolves to line 6, character 17

Scenario Outline: Resolve a top-level array to a line and offset
    Given a JSON file
        """
        [
            {
                "nested1": {
                    "foo": "bar"
                }
            },
            {
                "nested1": {
                    "foo": "bar"
                }
            }
        }
        """
    When I try to get the line and offset for pointer '<pointer>'
    Then TryGetLineAndOffsetForPointer returns true
    And the pointer resolves to line <line>, character <character>

    Examples:
    | pointer | line | character |
    | #/0     | 1    | 4         |
    | #/1     | 6    | 4         |

    
Scenario Outline: Try to resolve a non-existent top-level pointer
    Given a JSON file
        """
        {
            "property1": {
                "nested1": {
                    "foo": "bar"
                }
            },
            "property2": {
                "nested1": {
                    "foo": "bar"
                }
            }
        }
        """
    When I try to get the line and offset for pointer '<pointer>'
    Then TryGetLineAndOffsetForPointer returns false

    Examples:
    | pointer     |
    | #/property3 |
    | #/nested1   |
    | #/foo       |
    | #/0         |

Scenario Outline: Resolve a nested pointer to a line and offset with a top-level object
    Given a JSON file
        """
        {
            "property1": {
                "nested1": {
                    "foo": "bar"
                }
            },
            "property2": {
                "nested1": {
                    "foo": "bar"
                }
            },
            "array": [
                { "property3": { "nested": { "foo": "bar" } } },
                { "property3": { "nested": { "foo": "bar" } } },
            ]
        }
        """
    When I try to get the line and offset for pointer '<pointer>'
    Then TryGetLineAndOffsetForPointer returns true
    And the pointer resolves to line <line>, character <character>

    Examples:
    | pointer                        | line | character |
    | #/property1/nested1            | 2    | 19        |
    | #/property1/nested1/foo        | 3    | 19        |
    | #/property2/nested1            | 7    | 19        |
    | #/property2/nested1/foo        | 8    | 19        |
    | #/array/0                      | 12   | 8         |
    | #/array/0/property3            | 12   | 23        |
    | #/array/0/property3/nested     | 12   | 35        |
    | #/array/0/property3/nested/foo | 12   | 44        |

Scenario Outline: Resolve nested property in a top-level array to a line and offset
    Given a JSON file
        """
        [
            {
                "nested1": {
                    "foo": "bar"
                }
            },
            {
                "nested1": {
                    "foo": "bar"
                }
            }
        }
        """
    When I try to get the line and offset for pointer '<pointer>'
    Then TryGetLineAndOffsetForPointer returns true
    And the pointer resolves to line <line>, character <character>

    Examples:
    | pointer | line | character |
    | #/0     | 1    | 4         |
    | #/1     | 6    | 4         |

Scenario: Resolve a nested pointer to a line and offset when JSON comments are present
    Given a JSON file
        """
        {
            "property1": {
                "nested1": {
                    "foo": "bar"
                }
            },
            // This is a comment
            "property2": {
                "nested1": {
                    "foo": "bar"
                }
            }
        }
        """
    When I try to get the line and offset for pointer '#/property2/nested1' with skip comments enabled
    Then TryGetLineAndOffsetForPointer returns true
    And the pointer resolves to line 8, character 19

Scenario Outline: Try to resolve a non-existent nested pointer
    Given a JSON file
        """
        {
            "property1": {
                "nested1": {
                    "foo": "bar"
                }
            },
            "property2": {
                "nested1": {
                    "foo": "bar"
                }
            }
        }
        """
    When I try to get the line and offset for pointer '<pointer>'
    Then TryGetLineAndOffsetForPointer returns false

    Examples:
    | pointer         |
    | #/property1/foo |
    | #/property2/foo |
    | #/nested1/foo |

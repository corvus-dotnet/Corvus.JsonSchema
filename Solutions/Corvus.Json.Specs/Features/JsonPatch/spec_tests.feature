
Feature: spec_tests

Scenario: 4.1. add with missing object [c65e41a1-4353-40b2-810c-d48abbab3204]
	Given the document {"q":{"bar":2}}
	And the patch [{"op":"add","path":"/a/b","value":1}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.1.  Adding an Object Member [cc1ffcde-5e7d-4e4d-9923-d64742324ac5]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/baz","value":"qux"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux","foo":"bar"}

Scenario: A.2.  Adding an Array Element [7828f56a-98a7-44f4-a211-e1e4c20f7a00]
	Given the document {"foo":["bar","baz"]}
	And the patch [{"op":"add","path":"/foo/1","value":"qux"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["bar","qux","baz"]}

Scenario: A.3.  Removing an Object Member [2993017e-97a8-45cb-b8cb-eb214233518b]
	Given the document {"baz":"qux","foo":"bar"}
	And the patch [{"op":"remove","path":"/baz"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar"}

Scenario: A.4.  Removing an Array Element [a67f508e-f991-435c-81fe-ed329a7312ec]
	Given the document {"foo":["bar","qux","baz"]}
	And the patch [{"op":"remove","path":"/foo/1"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["bar","baz"]}

Scenario: A.5.  Replacing a Value [0a8da2af-d6ab-4329-a35d-676e533e1f2c]
	Given the document {"baz":"qux","foo":"bar"}
	And the patch [{"op":"replace","path":"/baz","value":"boo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"boo","foo":"bar"}

Scenario: A.6.  Moving a Value [9d7c86e5-eade-42ba-b97c-88a76e46fc21]
	Given the document {"foo":{"bar":"baz","waldo":"fred"},"qux":{"corge":"grault"}}
	And the patch [{"op":"move","from":"/foo/waldo","path":"/qux/thud"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"bar":"baz"},"qux":{"corge":"grault","thud":"fred"}}

Scenario: A.7.  Moving an Array Element [dc95c5b5-10fa-481c-9346-78b83099691e]
	Given the document {"foo":["all","grass","cows","eat"]}
	And the patch [{"op":"move","from":"/foo/1","path":"/foo/3"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["all","cows","eat","grass"]}

Scenario: A.8.  Testing a Value: Success [08f80592-ee11-4b56-b1d8-2f44a3eec732]
	Given the document {"baz":"qux","foo":["a",2,"c"]}
	And the patch [{"op":"test","path":"/baz","value":"qux"},{"op":"test","path":"/foo/1","value":2}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux","foo":["a",2,"c"]}

Scenario: A.9.  Testing a Value: Error [edda6c6b-68bd-4f13-af1a-65f300870e9a]
	Given the document {"baz":"qux"}
	And the patch [{"op":"test","path":"/baz","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.10.  Adding a nested Member Object [b7d68493-6dcf-4aad-86af-a561814e2aee]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/child","value":{"grandchild":{}}}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar","child":{"grandchild":{}}}

Scenario: A.11.  Ignoring Unrecognized Elements [7dc7f9ea-bf55-4864-95b0-5bb40e7764c6]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/baz","value":"qux","xyz":123}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar","baz":"qux"}

Scenario: A.12.  Adding to a Non-existent Target [22c8a16b-3893-4f29-be92-84957cb644fe]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/baz/bat","value":"qux"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.14. ~ Escape Ordering [61ce3ee1-3757-4094-9a59-6e5096edf4f8]
	Given the document {"/":9,"~1":10}
	And the patch [{"op":"test","path":"/~01","value":10}]
	When I apply the patch to the document
	Then the transformed document should equal {"/":9,"~1":10}

Scenario: A.15. Comparing Strings and Numbers [14ffd972-53a1-4c3e-b624-bd9736df3061]
	Given the document {"/":9,"~1":10}
	And the patch [{"op":"test","path":"/~01","value":"10"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.16. Adding an Array Value [3e4ec449-a50b-4819-ac69-ebbb0d1b0367]
	Given the document {"foo":["bar"]}
	And the patch [{"op":"add","path":"/foo/-","value":["abc","def"]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["bar",["abc","def"]]}

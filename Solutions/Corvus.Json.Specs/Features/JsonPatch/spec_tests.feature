
Feature: spec_tests

Scenario: 4.1. add with missing object [62428a97-8960-48a0-afe2-a34598f9beb6]
	Given the document {"q":{"bar":2}}
	And the patch [{"op":"add","path":"/a/b","value":1}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.1.  Adding an Object Member [011683ab-e4dd-4242-9187-31bc6a4c3976]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/baz","value":"qux"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux","foo":"bar"}

Scenario: A.2.  Adding an Array Element [7e0fa1f5-763a-458b-896b-35fe717d593f]
	Given the document {"foo":["bar","baz"]}
	And the patch [{"op":"add","path":"/foo/1","value":"qux"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["bar","qux","baz"]}

Scenario: A.3.  Removing an Object Member [790703c5-8611-44ef-9aa6-7d1a7591417a]
	Given the document {"baz":"qux","foo":"bar"}
	And the patch [{"op":"remove","path":"/baz"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar"}

Scenario: A.4.  Removing an Array Element [007dc2f7-9a6e-411a-a90b-e608751572ba]
	Given the document {"foo":["bar","qux","baz"]}
	And the patch [{"op":"remove","path":"/foo/1"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["bar","baz"]}

Scenario: A.5.  Replacing a Value [e5040009-3d1d-4ea9-bac5-1da9fe9ab723]
	Given the document {"baz":"qux","foo":"bar"}
	And the patch [{"op":"replace","path":"/baz","value":"boo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"boo","foo":"bar"}

Scenario: A.6.  Moving a Value [01762316-f53a-4ac6-a9fd-45d0f212a29e]
	Given the document {"foo":{"bar":"baz","waldo":"fred"},"qux":{"corge":"grault"}}
	And the patch [{"op":"move","from":"/foo/waldo","path":"/qux/thud"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"bar":"baz"},"qux":{"corge":"grault","thud":"fred"}}

Scenario: A.7.  Moving an Array Element [21f40379-211a-44d0-83ae-c557af598a33]
	Given the document {"foo":["all","grass","cows","eat"]}
	And the patch [{"op":"move","from":"/foo/1","path":"/foo/3"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["all","cows","eat","grass"]}

Scenario: A.8.  Testing a Value: Success [51cf65d1-2e1a-4c18-b1ad-45c07d077bed]
	Given the document {"baz":"qux","foo":["a",2,"c"]}
	And the patch [{"op":"test","path":"/baz","value":"qux"},{"op":"test","path":"/foo/1","value":2}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux","foo":["a",2,"c"]}

Scenario: A.9.  Testing a Value: Error [fa5c782d-8bbb-46d4-9582-006413eaa5f3]
	Given the document {"baz":"qux"}
	And the patch [{"op":"test","path":"/baz","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.10.  Adding a nested Member Object [580e24bc-f079-4d6a-b77d-72d35d3a41da]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/child","value":{"grandchild":{}}}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar","child":{"grandchild":{}}}

Scenario: A.11.  Ignoring Unrecognized Elements [ea1ed60d-3692-4e13-89df-0f14548f1464]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/baz","value":"qux","xyz":123}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar","baz":"qux"}

Scenario: A.12.  Adding to a Non-existent Target [7257bcfc-7297-49fc-8768-236da6f43c68]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/baz/bat","value":"qux"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.14. ~ Escape Ordering [d1a9821e-7e8b-4237-9578-6c8bd807664d]
	Given the document {"/":9,"~1":10}
	And the patch [{"op":"test","path":"/~01","value":10}]
	When I apply the patch to the document
	Then the transformed document should equal {"/":9,"~1":10}

Scenario: A.15. Comparing Strings and Numbers [87d46e9e-3eb3-473d-8b81-3d223c420895]
	Given the document {"/":9,"~1":10}
	And the patch [{"op":"test","path":"/~01","value":"10"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.16. Adding an Array Value [a777ecbc-cb96-4df0-82a1-25af5adc74f2]
	Given the document {"foo":["bar"]}
	And the patch [{"op":"add","path":"/foo/-","value":["abc","def"]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["bar",["abc","def"]]}

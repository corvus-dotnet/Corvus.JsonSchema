
Feature: builder_spec_tests

Scenario: 4.1. add with missing object [cab90cd0-b915-4425-85fb-6702f7d28439]
	Given the document {"q":{"bar":2}}
	When I build the patch [{"op":"add","path":"/a/b","value":1}]
	Then a patch exception should be thrown

Scenario: A.1.  Adding an Object Member [a145a017-3508-4e5f-bb15-e66a013f9fa6]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/baz","value":"qux"}]
	Then the patch result should equal {"baz":"qux","foo":"bar"}

Scenario: A.2.  Adding an Array Element [b767e3d1-5aa8-44f2-9ddd-f7227e3a1151]
	Given the document {"foo":["bar","baz"]}
	When I build the patch [{"op":"add","path":"/foo/1","value":"qux"}]
	Then the patch result should equal {"foo":["bar","qux","baz"]}

Scenario: A.3.  Removing an Object Member [bb4ae6e6-b00c-478c-a83e-60fdb70c92e7]
	Given the document {"baz":"qux","foo":"bar"}
	When I build the patch [{"op":"remove","path":"/baz"}]
	Then the patch result should equal {"foo":"bar"}

Scenario: A.4.  Removing an Array Element [989ea11c-3db9-49e9-9489-d923d0daf3e2]
	Given the document {"foo":["bar","qux","baz"]}
	When I build the patch [{"op":"remove","path":"/foo/1"}]
	Then the patch result should equal {"foo":["bar","baz"]}

Scenario: A.5.  Replacing a Value [2efe7bf3-35e8-46f8-904b-1401bb70c0a0]
	Given the document {"baz":"qux","foo":"bar"}
	When I build the patch [{"op":"replace","path":"/baz","value":"boo"}]
	Then the patch result should equal {"baz":"boo","foo":"bar"}

Scenario: A.6.  Moving a Value [5f5e65bd-e00e-469e-b1a1-18b26c8447e7]
	Given the document {"foo":{"bar":"baz","waldo":"fred"},"qux":{"corge":"grault"}}
	When I build the patch [{"op":"move","from":"/foo/waldo","path":"/qux/thud"}]
	Then the patch result should equal {"foo":{"bar":"baz"},"qux":{"corge":"grault","thud":"fred"}}

Scenario: A.7.  Moving an Array Element [e5a50db1-68d8-487d-9a94-b1ec0c425176]
	Given the document {"foo":["all","grass","cows","eat"]}
	When I build the patch [{"op":"move","from":"/foo/1","path":"/foo/3"}]
	Then the patch result should equal {"foo":["all","cows","eat","grass"]}

Scenario: A.8.  Testing a Value: Success [c932300b-e311-4498-a1fb-1d7835fced3d]
	Given the document {"baz":"qux","foo":["a",2,"c"]}
	When I build the patch [{"op":"test","path":"/baz","value":"qux"},{"op":"test","path":"/foo/1","value":2}]
	Then the patch result should equal {"baz":"qux","foo":["a",2,"c"]}

Scenario: A.9.  Testing a Value: Error [5d183b8c-8dc7-43b9-acb0-b11169d2aac6]
	Given the document {"baz":"qux"}
	When I build the patch [{"op":"test","path":"/baz","value":"bar"}]
	Then a patch exception should be thrown

Scenario: A.10.  Adding a nested Member Object [db69aa90-564d-49ac-a039-ce378a6540ce]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/child","value":{"grandchild":{}}}]
	Then the patch result should equal {"foo":"bar","child":{"grandchild":{}}}

Scenario: A.11.  Ignoring Unrecognized Elements [ed1388d6-5bf9-406a-8682-916b7b4a3736]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/baz","value":"qux","xyz":123}]
	Then the patch result should equal {"foo":"bar","baz":"qux"}

Scenario: A.12.  Adding to a Non-existent Target [cc49fc5c-635b-483d-bae6-bca259775b59]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/baz/bat","value":"qux"}]
	Then a patch exception should be thrown

Scenario: A.14. ~ Escape Ordering [d4fe7003-0bc8-4499-bc52-acc25d9d7576]
	Given the document {"/":9,"~1":10}
	When I build the patch [{"op":"test","path":"/~01","value":10}]
	Then the patch result should equal {"/":9,"~1":10}

Scenario: A.15. Comparing Strings and Numbers [215f2085-5421-4eca-83db-d5e36609353a]
	Given the document {"/":9,"~1":10}
	When I build the patch [{"op":"test","path":"/~01","value":"10"}]
	Then a patch exception should be thrown

Scenario: A.16. Adding an Array Value [cec39f8d-a025-4154-b7c8-366c80725151]
	Given the document {"foo":["bar"]}
	When I build the patch [{"op":"add","path":"/foo/-","value":["abc","def"]}]
	Then the patch result should equal {"foo":["bar",["abc","def"]]}

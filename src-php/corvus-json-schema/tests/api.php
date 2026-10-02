<?php
// The extension's API: how PHP values are read, options, results and errors.
declare(strict_types=1);

use Corvus\JsonSchema\{Collector, CompilationException, DepthException, Dialect, InvalidJsonException,
    JsonSchemaException, ResultsLevel, Validator};

$checks = 0;
$failures = [];
function check(string $what, mixed $expected, mixed $actual): void
{
    global $checks, $failures;
    $checks++;
    if ($expected !== $actual) {
        $failures[] = "$what: expected " . var_export($expected, true) . ', got ' . var_export($actual, true);
    }
}
function throws(string $what, string $class, callable $f): void
{
    global $checks, $failures;
    $checks++;
    try {
        $f();
        $failures[] = "$what: expected $class, nothing was thrown";
    } catch (Throwable $e) {
        if (!$e instanceof $class) {
            $failures[] = "$what: expected $class, got " . get_class($e) . ': ' . $e->getMessage();
        }
    }
}

$person = Validator::compile([
    'type' => 'object',
    'required' => ['name'],
    'properties' => ['name' => ['type' => 'string'], 'age' => ['type' => 'integer', 'minimum' => 0]],
]);
check('array as object', true, $person->isValid(['name' => 'Ada', 'age' => 36]));
check('array as object, invalid', false, $person->isValid(['name' => 'Ada', 'age' => -1]));
check('stdClass', true, $person->isValid((object) ['name' => 'Ada']));
check('stdClass, invalid', false, $person->isValid((object) ['age' => 1]));
check('JSON text', true, $person->isValidJson('{"name": "Ada"}'));
check('JSON text, invalid', false, $person->isValidJson('{"name": 1}'));

$array = Validator::compile(['type' => 'array']);
$object = Validator::compile(['type' => 'object']);
check('a list is an array', true, $array->isValid([1, 2, 3]));
check('the empty array is an array', true, $array->isValid([]));
check('an empty stdClass is an object', true, $object->isValid(new stdClass()));
check('keys out of order make an object', true, $object->isValid([1 => 'a', 0 => 'b']));
check('a hole makes an object', true, $object->isValid(array_filter(['a', '', 'b'])));
$list = [0 => 'a', 1 => 'b'];
$list['x'] = 1;
unset($list['x']);
check('a hash-form list is an array', true, $array->isValid($list));

$keys = Validator::compile(['properties' => ['1' => ['type' => 'string'], '-2' => ['type' => 'string']], 'required' => ['1']]);
check('integer keys read as text', true, $keys->isValid([1 => 'one', -2 => 'minus two']));
check('integer keys read as text, invalid', false, $keys->isValid([1 => 1]));
check('integer keys in a stdClass', true, $keys->isValid((object) ['1' => 'one']));
check('a hole-y packed array as an object', false, $keys->isValid(array_filter([0, 7])));
$names = Validator::compile(['propertyNames' => ['pattern' => '^[0-9]+$']]);
check('integer keys are names', true, $names->isValid([5 => 'a', 7 => 'b']));

// More than eight properties: names are looked up by hash.
$wide = [];
for ($i = 0; $i < 20; $i++) {
    $wide["p$i"] = $i;
    $wide[100 + $i] = $i;
}
$lookup = Validator::compile(['properties' => ['p13' => ['const' => 13], '113' => ['const' => 13]], 'required' => ['p13', '113']]);
check('lookup in a large array', true, $lookup->isValid($wide));
check('lookup in a large stdClass', true, $lookup->isValid((object) $wide));
$wide['p13'] = 0;
check('lookup in a large array, invalid', false, $lookup->isValid($wide));

$x = 'text';
$refs = [&$x];
check('references are followed', true, Validator::compile(['items' => ['type' => 'string']])->isValid($refs));

final class Point implements JsonSerializable
{
    public function __construct(private int $x) {}
    public function jsonSerialize(): mixed { return ['x' => $this->x]; }
}
enum Colour: string { case Red = 'red'; }
enum Plain { case One; }
final class Pair { public int $a = 1; protected int $b = 2; private int $c = 3; }
$point = Validator::compile(['properties' => ['x' => ['minimum' => 0]], 'required' => ['x']]);
check('JsonSerializable', true, $point->isValid(new Point(3)));
check('JsonSerializable, invalid', false, $point->isValid(new Point(-3)));
check('a backed enum is its value', true, Validator::compile(['const' => 'red'])->isValid(Colour::Red));
throws('a pure enum is not JSON', TypeError::class, fn () => Validator::compile(['type' => 'string'])->isValid(Plain::One));
check('other objects: public properties', true,
    Validator::compile(['required' => ['a'], 'maxProperties' => 1])->isValid(new Pair()));

check('values are read only as examined', true, $object->isValid(['a' => fopen('php://memory', 'r')]));
throws('a resource is not JSON', TypeError::class, fn () => Validator::compile(['items' => ['type' => 'string']])->isValid([fopen('php://memory', 'r')]));
throws('NaN is not JSON', ValueError::class, fn () => Validator::compile(['type' => 'number'])->isValid(NAN));
throws('invalid UTF-8', ValueError::class, fn () => Validator::compile(['type' => 'string'])->isValid("\xff"));
check('big integers are doubles', true, Validator::compile(['type' => 'number'])->isValid(PHP_INT_MAX));

// Options.
check('default dialect', true, Validator::compile(['type' => 'integer'], ['defaultDialect' => Dialect::Draft4])->isValid(1));
check('draft 4 exclusiveMaximum', false,
    Validator::compile(['maximum' => 3, 'exclusiveMaximum' => true], ['defaultDialect' => Dialect::Draft4])->isValid(3));
check('format not asserted by default', true, Validator::compile(['format' => 'email'])->isValid('nope'));
check('assertFormat', false, Validator::compile(['format' => 'email'], ['assertFormat' => true])->isValid('nope'));
$even = Validator::compile(['format' => 'even'], ['assertFormat' => true, 'formats' => ['even' => fn (string $s) => strlen($s) % 2 === 0]]);
check('custom format', true, $even->isValid('ab'));
check('custom format, invalid', false, $even->isValid('abc'));
check('custom format, JSON text', false, $even->isValidJson('"abc"'));
$boom = Validator::compile(['format' => 'boom'], ['assertFormat' => true, 'formats' => ['boom' => fn ($s) => throw new RuntimeException('boom')]]);
throws('a format validator\'s exception propagates', RuntimeException::class, fn () => $boom->isValid('x'));
throws('... from JSON text too', RuntimeException::class, fn () => $boom->isValidJson('"x"'));
check('the validator works after an exception', true, $boom->isValid(1));
$remote = Validator::compile(['$ref' => 'https://example.com/positive'], ['resolver' => fn (string $uri) => $uri === 'https://example.com/positive' ? ['minimum' => 1] : null]);
check('resolver', false, $remote->isValid(0));
check('resolver returning JSON text', true,
    Validator::compile(['$ref' => 'https://example.com/s'], ['resolver' => fn ($uri) => '{"type": "string"}'])->isValid('a'));
throws('an unknown document', CompilationException::class, fn () => Validator::compile(['$ref' => 'https://example.com/none'], ['resolver' => fn ($uri) => null]));
throws('a resolver\'s exception propagates', LogicException::class,
    fn () => Validator::compile(['$ref' => 'https://example.com/x'], ['resolver' => fn ($uri) => throw new LogicException('no')]));
check('baseUri', true, Validator::compile(['$ref' => 'item', '$defs' => ['item' => ['$id' => 'item', 'type' => 'string']]], ['baseUri' => 'https://example.com/root'])->isValid('a'));
check('entryPoint', false, Validator::compile(['$defs' => ['item' => ['type' => 'string']]], ['entryPoint' => '#/$defs/item'])->isValid(1));
throws('an unknown option', ValueError::class, fn () => Validator::compile([], ['nope' => 1]));
throws('a mistyped option', TypeError::class, fn () => Validator::compile([], ['assertFormat' => 'yes']));

// Schemas.
check('a JSON text schema', false, Validator::compile('{"type": "string"}')->isValid(1));
check('a boolean schema', false, Validator::compile(false)->isValid(1));
throws('an invalid JSON text schema', InvalidJsonException::class, fn () => Validator::compile('{'));
throws('an invalid pattern', CompilationException::class, fn () => Validator::compile(['pattern' => '(']));
check('exceptions share a base', true, is_subclass_of(CompilationException::class, JsonSchemaException::class));

// Errors.
throws('invalid JSON text', InvalidJsonException::class, fn () => $person->isValidJson('{'));
$deep = Validator::compile(['$ref' => '#'], ['maxDepth' => 8]);
throws('depth', DepthException::class, fn () => $deep->isValid(1));

// Results.
$c = new Collector(ResultsLevel::Detailed);
check('evaluate', false, $person->evaluate(['name' => 1], $c));
$rows = $c->results();
check('results rows', true, count($rows) > 0);
check('results keys', ['isMatch', 'message', 'evaluationLocation', 'schemaLocation', 'instanceLocation'], array_keys($rows[0]));
check('a failure located', true, in_array('/name', array_column($rows, 'instanceLocation'), true));
$c->clear();
check('clear', [], $c->results());
$v = new Collector(ResultsLevel::Verbose);
Validator::compile(['title' => 'T', 'properties' => ['a' => ['title' => 'A']]])->evaluate(['a' => 1], $v);
$annotations = $v->annotations();
check('annotations', 'A', $annotations['/a']['title']['#/properties/a'] ?? null);
check('default collector level: no failures recorded', [], (function () use ($person) {
    $basic = new Collector();
    $person->evaluate(['name' => 'Ada'], $basic);
    return array_filter($basic->results(), fn ($r) => !$r['isMatch']);
})());
check('crate_version', 1, preg_match('/^\d+\.\d+\.\d+$/', Corvus\JsonSchema\crate_version()));
check('the extension\'s name', true, extension_loaded('corvus_json_schema'));
check('the extension\'s version', 1, preg_match('/^\d+\.\d+\.\d+$/', (string) phpversion('corvus_json_schema')));

if ($failures) {
    fwrite(STDERR, count($failures) . " of $checks checks failed:\n" . implode("\n", $failures) . "\n");
    exit(1);
}
echo "$checks checks passed\n";

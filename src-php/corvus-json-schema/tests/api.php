<?php
// The extension's API: how PHP values are read, options, results and errors.
declare(strict_types=1);

use Corvus\JsonSchema\{Collector, CompilationException, DepthException, Dialect, InvalidJsonException,
    JsonSchemaException, PatternMatchLimitException, PatternTooLargeException, ResultsLevel, Validator};

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
// The crate before 0.1.4 kept this pattern's excluded set as ASCII bits and read é as the bits of C and ).
$excluded = Validator::compile(['pattern' => '^(?=[^é]+$)(?=(.*\w)).+$']);
check('a class member outside ASCII: C', true, $excluded->isValid('C1'));
check('a class member outside ASCII: )', true, $excluded->isValid(')a'));
check('a class member outside ASCII: the member', false, $excluded->isValid('é1'));
check('a class member outside ASCII: the member, JSON text', false, $excluded->isValidJson('"é1"'));
// The crate before 0.1.7 dropped a "$" at the end of the repeated group of a separated list, and after its first item, so
// the first two patterns were matched as ^x(,a)*$ and the third as ^(a,)*b$. The answers are V8's.
$anchored = Validator::compile(['pattern' => '^x(,a$)*$']);
check('a list with an anchor in its group: no repeat', true, $anchored->isValid('x'));
check('a list with an anchor in its group: one repeat', true, $anchored->isValid('x,a'));
check('a list with an anchor in its group: two repeats', false, $anchored->isValid('x,a,a'));
check('a list with an anchor in its group: two repeats, JSON text', false, $anchored->isValidJson('"x,a,a"'));
$anchored = Validator::compile(['pattern' => '^x$(,a)*$']);
check('a list with an anchor after its first item: no repeat', true, $anchored->isValid('x'));
check('a list with an anchor after its first item: one repeat', false, $anchored->isValid('x,a'));
$anchored = Validator::compile(['pattern' => '^(a,$)*b$']);
check('a list with an anchor before its last item: no repeat', true, $anchored->isValid('b'));
check('a list with an anchor before its last item: one repeat', false, $anchored->isValid('a,b'));
// An escaped "$" is a literal character.
$escaped = Validator::compile(['pattern' => '^x(,a\$)*$']);
check('a list with an escaped anchor: two repeats', true, $escaped->isValid('x,a$,a$'));
check('a list with an escaped anchor: no "$" in the text', false, $escaped->isValid('x,a,a'));
check('exceptions share a base', true, is_subclass_of(CompilationException::class, JsonSchemaException::class));

// The limits on a regular expression. A schema and a value may both come from outside, so a match of a pattern has a
// limit of time (one second unless patternMatchLimit says otherwise) and a pattern has limits of size. Reaching either
// throws an exception of its own, and is never an answer.
$neverAnswers = '^(?:(a+)\\1*)+$';
$neverAnswered = str_repeat('a', 60) . 'b';
$limited = Validator::compile(['pattern' => $neverAnswers], ['patternMatchLimit' => 0.05]);
foreach ([
    'isValid' => fn () => $limited->isValid($neverAnswered),
    'isValidJson' => fn () => $limited->isValidJson(json_encode($neverAnswered)),
    'evaluate' => fn () => $limited->evaluate($neverAnswered, new Collector(ResultsLevel::Basic)),
] as $what => $f) {
    $start = microtime(true);
    try {
        $f();
        check("the match limit, $what: nothing was thrown", true, false);
    } catch (PatternMatchLimitException $e) {
        $took = microtime(true) - $start;
        check("the match limit, $what: the message names the pattern", true, str_contains($e->getMessage(), $neverAnswers));
        check("the match limit, $what: the message names the limit", true, str_contains($e->getMessage(), '50ms'));
        check("the match limit, $what: abandoned at the limit", true, $took >= 0.05 && $took < 20);
    }
}
check('a string that matches, with a limit', true, $limited->isValid('aaaa'));
check('a string that does not match, with a limit', false, $limited->isValid('b'));
check('the match limit is not the depth', false, is_subclass_of(PatternMatchLimitException::class, DepthException::class));
check('the match limit shares the base', true, is_subclass_of(PatternMatchLimitException::class, JsonSchemaException::class));
// The default is one second.
$default = Validator::compile(['patternProperties' => [$neverAnswers => ['type' => 'integer']]]);
$start = microtime(true);
throws('the default match limit', PatternMatchLimitException::class, fn () => $default->isValid([$neverAnswered => 1]));
$took = microtime(true) - $start;
check('the default match limit is one second', true, $took >= 1.0 && $took < 21);
// Off: the match runs to its end. Fourteen characters take a moment, and the answer is no.
foreach ([null, 0, 0.0, -1] as $off) {
    $unlimited = Validator::compile(['pattern' => $neverAnswers], ['patternMatchLimit' => $off]);
    check('the match limit turned off with ' . var_export($off, true), false, $unlimited->isValid(str_repeat('a', 14) . 'b'));
}
throws('a match limit that is not a number', TypeError::class, fn () => Validator::compile([], ['patternMatchLimit' => 'soon']));
throws('the memory of a match', PatternMatchLimitException::class,
    fn () => Validator::compile(['pattern' => '(?:a?){2147483647}'])->isValid(''));
// A pattern over a limit of size.
$nest = fn (int $depth) => str_repeat('(?:', $depth) . 'a' . str_repeat(')', $depth);
foreach ([
    [str_repeat('a', 16384), str_repeat('a', 16385), 'the pattern has a size of more than 1048576 bytes when it is read'],
    [$nest(999), $nest(1000), 'the groups of the pattern nest more than 1000 deep'],
] as [$at, $over, $reason]) {
    check("a pattern at the limit compiles ($reason)", true, Validator::compile(['pattern' => $at]) instanceof Validator);
    foreach ([['pattern' => $over], ['patternProperties' => [$over => true]]] as $schema) {
        try {
            Validator::compile($schema);
            check("a pattern over the limit: nothing was thrown ($reason)", true, false);
        } catch (PatternTooLargeException $e) {
            check("a pattern over the limit: the message ($reason)", true,
                str_ends_with($e->getMessage(), "is too large to read as an ECMA-262 regular expression: $reason."));
            check('a pattern over the limit is not said to be invalid', false, str_contains(strtolower($e->getMessage()), 'invalid'));
        }
    }
}
check('too large is a compilation exception', true, is_subclass_of(PatternTooLargeException::class, CompilationException::class));
try {
    Validator::compile(['pattern' => 'a**']);
    check('a pattern that is not valid: nothing was thrown', true, false);
} catch (CompilationException $e) {
    check('a pattern that is not valid is not too large', false, $e instanceof PatternTooLargeException);
    check('a pattern that is not valid: the message', true, str_starts_with($e->getMessage(), 'Invalid regular expression'));
}

// Errors.
throws('invalid JSON text', InvalidJsonException::class, fn () => $person->isValidJson('{'));
$deep = Validator::compile(['$ref' => '#'], ['maxDepth' => 8]);
throws('depth', DepthException::class, fn () => $deep->isValid(1));
// The crate before 0.1.6 evaluated a not without the guard on in-place recursion, so the schemas whose not leads back to
// the schema it is in overflowed the stack, which ends the process.
$looping = ['allOf' => [['$ref' => '#/$defs/loop']]];
$notOnACycle = [
    ['not' => ['$ref' => '#']],
    ['not' => ['not' => ['$ref' => '#']]],
    ['type' => 'integer', 'not' => ['$ref' => '#']],
    ['allOf' => [['not' => ['$ref' => '#']]]],
    ['$defs' => ['a' => ['not' => ['$ref' => '#/$defs/b']], 'b' => ['not' => ['$ref' => '#/$defs/a']]], '$ref' => '#/$defs/a'],
    ['$defs' => ['loop' => $looping], 'not' => ['$ref' => '#/$defs/loop']],
    ['$defs' => ['loop' => $looping], 'not' => ['not' => ['$ref' => '#/$defs/loop']]],
    ['$defs' => ['loop' => $looping], 'properties' => ['a' => ['not' => ['$ref' => '#/$defs/loop']]]],
    ['unevaluatedProperties' => false, 'not' => ['$ref' => '#']],
];
foreach ($notOnACycle as $s => $schema) {
    $not = Validator::compile($schema, ['maxDepth' => 16]);
    foreach ([1, 'a', ['a' => 1], [1]] as $instance) {
        // Only an object with the property reaches the loop of the eighth schema, and anything but an integer fails the
        // type of the third before its not is reached, when failing fast.
        if (($s === 7 && $instance !== ['a' => 1]) || ($s === 2 && $instance !== 1)) {
            continue;
        }
        $what = 'a not on a cycle: ' . json_encode($schema) . ' with ' . json_encode($instance);
        throws($what, DepthException::class, fn () => $not->isValid($instance));
        throws("$what, JSON text", DepthException::class, fn () => $not->isValidJson(json_encode($instance)));
        foreach ([ResultsLevel::Basic, ResultsLevel::Detailed, ResultsLevel::Verbose] as $level) {
            throws("$what, evaluate", DepthException::class, fn () => $not->evaluate($instance, new Collector($level)));
        }
    }
}

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

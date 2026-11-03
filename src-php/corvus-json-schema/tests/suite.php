<?php
// The JSON-Schema-Test-Suite (the repository's submodule, or JSON_SCHEMA_TEST_SUITE): every case with its data read in
// place (json_decode to stdClass objects, which keeps {} and [] apart), as JSON text, and at the verbose level through
// a collector, which must give the same verdict. Every dialect's cases are in one tree (validation/tests, required and
// optional): each case's compatibility selects the drafts it applies to, its tags whether format is asserted, and its
// externalSchemas are served to that case alone.
declare(strict_types=1);

use Corvus\JsonSchema\{Collector, Dialect, JsonSchemaException, ResultsLevel, Validator};

$root = getenv('JSON_SCHEMA_TEST_SUITE') ?: __DIR__ . '/../../../JSON-Schema-Test-Suite';
$drafts = [
    'draft4' => [Dialect::Draft4, 4], 'draft6' => [Dialect::Draft6, 6], 'draft7' => [Dialect::Draft7, 7],
    'draft2019-09' => [Dialect::Draft201909, 2019], 'draft2020-12' => [Dialect::Draft202012, 2020],
];
// As the other runners: a JSON parser cannot tell 1.0 from 1 here (draft 4 does not count 1.0 as an integer).
$excluded = ['draft4/optional/zero-terminated-floats.json'];
// As the crate's runner, test cases left out by name (file and test description): the high-precision cases of
// optional/bignum.json, whose digits the crate's serde_json values do not hold.
$excludedCases = [
    'bignum.json|below the exclusive maximum is valid',
    'bignum.json|above the exclusive minimum is valid',
    'bignum.json|just above the maximum is invalid',
    'bignum.json|just below the minimum is invalid',
    'bignum.json|a slightly smaller decimal is invalid',
    'bignum.json|a slightly larger decimal is invalid',
];

$tests = "$root/validation/tests";
if (!is_dir($tests)) {
    fwrite(STDERR, "JSON-Schema-Test-Suite not found at $root\n");
    exit(1);
}

// Whether a test case's compatibility (a comma-separated list of minimums, <= maximums and = releases, from the
// smallest release to the largest) admits the release, by the algorithm of the suite's validation/README.md.
function compatible(?string $compatibility, int $release): bool
{
    if ($compatibility === null) {
        return true;
    }
    $valid = true;
    foreach (explode(',', $compatibility) as $constraint) {
        [$op, $version] = str_starts_with($constraint, '<=') ? ['<=', substr($constraint, 2)]
            : (str_starts_with($constraint, '=') ? ['=', substr($constraint, 1)] : ['', $constraint]);
        $number = (int) trim($version);
        $valid = $op === '<=' ? $release <= $number : ($op === '=' ? $release === $number : $release >= $number);
        if ($release <= $number) {
            break;
        }
    }
    return $valid;
}

// A resolver serving a test case's externalSchemas at their retrieval URIs, as JSON text.
function resolverFor(object $group): ?Closure
{
    if (!isset($group->externalSchemas)) {
        return null;
    }
    $schemas = $group->externalSchemas;
    return function (string $uri) use ($schemas): ?string {
        return property_exists($schemas, $uri)
            ? json_encode($schemas->$uri, JSON_PRESERVE_ZERO_FRACTION | JSON_UNESCAPED_SLASHES | JSON_THROW_ON_ERROR)
            : null;
    };
}

$total = 0;
$failures = [];
$files = array_merge(glob("$tests/*.json"), glob("$tests/optional/*.json"));
foreach ($drafts as $draft => [$dialect, $release]) {
    foreach ($files as $file) {
        $label = "$draft/" . substr($file, strlen("$tests/"));
        if (in_array($label, $excluded, true)) {
            continue;
        }
        $fileName = basename($file);
        foreach (json_decode(file_get_contents($file), false, 512, JSON_THROW_ON_ERROR)->tests as $group) {
            if (!compatible($group->compatibility ?? null, $release)) {
                continue;
            }
            $tags = $group->tags ?? [];
            // The evaluator asserts format by default up to draft7, so a format-annotation case does not apply there.
            if (in_array('format-annotation', $tags, true) && $release <= 7) {
                continue;
            }
            $assertFormat = in_array('format-assertion', $tags, true);
            try {
                $validator = Validator::compile($group->schema, [
                    'defaultDialect' => $dialect,
                    'assertFormat' => $assertFormat ? true : null,
                    'resolver' => resolverFor($group),
                ]);
            } catch (JsonSchemaException $e) {
                $failures[] = "$label [$group->description]: " . get_class($e) . ': ' . $e->getMessage();
                continue;
            }
            foreach ($group->tests as $test) {
                if (in_array("$fileName|$test->description", $excludedCases, true)) {
                    continue;
                }
                $total++;
                $what = "$label [$group->description] $test->description";
                $expected = $test->valid;
                $actual = $validator->isValid($test->data);
                if ($actual !== $expected) {
                    $failures[] = "$what: expected " . var_export($expected, true) . ', got ' . var_export($actual, true);
                }
                $text = $validator->isValidJson(json_encode($test->data, JSON_PRESERVE_ZERO_FRACTION | JSON_THROW_ON_ERROR));
                if ($text !== $expected) {
                    $failures[] = "$what: isValidJson gave " . var_export($text, true);
                }
                $collected = $validator->evaluate($test->data, new Collector(ResultsLevel::Verbose));
                if ($collected !== $expected) {
                    $failures[] = "$what: evaluate gave " . var_export($collected, true);
                }
            }
        }
    }
}
if ($total < 8000) {
    fwrite(STDERR, "only $total cases ran\n");
    exit(1);
}
if ($failures) {
    fwrite(STDERR, count($failures) . " of $total failed:\n" . implode("\n", array_slice($failures, 0, 40)) . "\n");
    exit(1);
}
echo "$total cases: every one read in place, as JSON text and through a collector\n";

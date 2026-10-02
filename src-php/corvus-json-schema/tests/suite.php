<?php
// The JSON-Schema-Test-Suite (the repository's submodule, or JSON_SCHEMA_TEST_SUITE): every case with its data read in
// place (json_decode to stdClass objects, which keeps {} and [] apart), as JSON text, and at the verbose level through
// a collector, which must give the same verdict. Remote references are served by a resolver over the suite's remotes.
declare(strict_types=1);

use Corvus\JsonSchema\{Collector, Dialect, JsonSchemaException, ResultsLevel, Validator};

$root = getenv('JSON_SCHEMA_TEST_SUITE') ?: __DIR__ . '/../../../JSON-Schema-Test-Suite';
$drafts = [
    'draft4' => Dialect::Draft4, 'draft6' => Dialect::Draft6, 'draft7' => Dialect::Draft7,
    'draft2019-09' => Dialect::Draft201909, 'draft2020-12' => Dialect::Draft202012,
];
// As the other runners: a JSON parser cannot tell 1.0 from 1 here (draft 4 does not count 1.0 as an integer).
$excluded = ['draft4/optional/zeroTerminatedFloats.json'];

if (!is_dir("$root/tests")) {
    fwrite(STDERR, "JSON-Schema-Test-Suite not found at $root\n");
    exit(1);
}
$resolver = function (string $uri) use ($root): ?string {
    if (!str_starts_with($uri, 'http://localhost:1234/')) {
        return null;
    }
    $path = "$root/remotes/" . substr($uri, strlen('http://localhost:1234/'));
    return is_file($path) ? file_get_contents($path) : null;
};

$total = 0;
$failures = [];
foreach ($drafts as $draft => $dialect) {
    $dir = "$root/tests/$draft";
    $files = [];
    foreach ([glob("$dir/*.json"), glob("$dir/optional/*.json")] as $list) {
        foreach ($list as $f) {
            $files[] = [$f, false];
        }
    }
    foreach (glob("$dir/optional/format/*.json") as $f) {
        $files[] = [$f, true];
    }
    foreach ($files as [$file, $assertFormat]) {
        $label = substr($file, strlen("$root/tests/"));
        if (in_array($label, $excluded, true)) {
            continue;
        }
        foreach (json_decode(file_get_contents($file), false, 512, JSON_THROW_ON_ERROR) as $group) {
            try {
                $validator = Validator::compile($group->schema, [
                    'defaultDialect' => $dialect,
                    'assertFormat' => $assertFormat ? true : null,
                    'resolver' => $resolver,
                ]);
            } catch (JsonSchemaException $e) {
                $failures[] = "$label [$group->description]: " . get_class($e) . ': ' . $e->getMessage();
                continue;
            }
            foreach ($group->tests as $test) {
                $total++;
                $what = "$label [$group->description] $test->description";
                if ($assertFormat && str_contains(strtolower($what), 'leap second')) {
                    continue;
                }
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
if ($total < 7000) {
    fwrite(STDERR, "only $total cases ran\n");
    exit(1);
}
if ($failures) {
    fwrite(STDERR, count($failures) . " of $total failed:\n" . implode("\n", array_slice($failures, 0, 40)) . "\n");
    exit(1);
}
echo "$total cases: every one read in place, as JSON text and through a collector\n";

const fs = require('fs');
const xml = fs.readFileSync('TestResults/full.trx', 'utf8');
const re = /<UnitTestResult[^>]*testName="([^"]*)"[^>]*>([\s\S]*?)<\/UnitTestResult>/g;
let fb = [];
let m;
while ((m = re.exec(xml)) !== null) {
    const name = m[1];
    const body = m[2];
    const stdm = body.match(/<StdOut>([\s\S]*?)<\/StdOut>/);
    const stdout = stdm ? stdm[1] : '';
    if (!stdout.includes('Inlined:    True')) {
        const g = name.match(/group: &quot;([^&]+)&/);
        const c = name.match(/caseName: &quot;([^&]+)&/);
        const e = stdout.match(/Expression: (.+)/);
        fb.push({group: g?g[1]:'?', case_name: c?c[1]:'?', expr: e?e[1].trim():'?'});
    }
}
const cats = ['literals','comments','blocks','variables','range-operator','wildcards','sorting','flattening','token-conversion','default-operator','context','numeric-operators','performance','transforms','transform','object-constructor','function-each','function-sift','hof-single','hof-map','hof-reduce','hof-filter','hof-zip-map','higher-order-functions','function-string','function-sort'];
for (const g of cats) {
    const cases = fb.filter(x => x.group === g);
    if (cases.length > 0) {
        console.log(`=== ${g} (${cases.length}) ===`);
        cases.forEach(x => console.log(`  ${x.case_name}: ${x.expr}`));
    }
}

import xml.etree.ElementTree as ET, re, sys
tree = ET.parse('TestResults/full.trx')
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
results = tree.findall('.//t:UnitTestResult', ns)
fb = []
for r in results:
    stdout = ''
    out = r.find('.//t:StdOut', ns)
    if out is not None and out.text: stdout = out.text
    if 'Inlined:' not in stdout or 'Inlined:    True' not in stdout:
        m1 = re.search(r'group: "([^"]+)"', r.get('testName',''))
        m2 = re.search(r'caseName: "([^"]+)"', r.get('testName',''))
        m3 = re.search(r'Expression: (.+)', stdout)
        group = m1.group(1) if m1 else '?'
        case = m2.group(1) if m2 else '?'
        expr = m3.group(1).strip() if m3 else '?'
        fb.append((group, case, expr))
for g in ['literals','comments','blocks','variables','range-operator','wildcards','sorting','flattening','token-conversion','default-operator','context','numeric-operators','performance','transforms','transform','object-constructor','function-each','function-sift','hof-single','hof-map','hof-reduce','hof-filter','hof-zip-map','higher-order-functions','function-string','function-sort']:
    cases = [(c,e) for (grp,c,e) in fb if grp == g]
    if cases:
        print(f'=== {g} ({len(cases)}) ===')
        for c,e in cases: print(f'  {c}: {e}')

import hashlib,json,pathlib,re,subprocess
r=pathlib.Path(__file__).resolve().parents[1]
baseline=json.loads((r/'docs/evidence/pre-m0-files.json').read_text())
for name,digest in baseline.items():
    p=r/name
    assert p.exists() and hashlib.sha256(p.read_bytes()).hexdigest()==digest,'Existing asset/package changed: '+name
print(f'PASS {len(baseline)} pre-existing asset/package hashes preserved')
assets=r/'apps/unity/Assets'
for p in assets.rglob('*'):
    if p.suffix!='.meta': assert pathlib.Path(str(p)+'.meta').exists(),'Missing Unity metadata: '+str(p)
guids=[]
for p in assets.rglob('*.meta'):
    match=re.search(r'^guid: ([a-f0-9]+)',p.read_text(),re.M)
    if match: guids.append(match.group(1))
assert len(guids)==len(set(guids)),'Duplicate asset GUID'
print('PASS metadata presence and unique GUIDs')
paths=subprocess.check_output(['git','ls-files','--cached','--others','--exclude-standard'],cwd=r,text=True).splitlines()
for name in paths:
    p=r/name
    if not p.is_file() or p.suffix.lower() in ['.png','.jpg','.ttf']:continue
    text=p.read_text(encoding='utf-8',errors='replace')
    # Bounded heuristic, not a substitute for a production secret scanner.
    for pattern in [r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',r'\bsk-[A-Za-z0-9]{32,}',r'\bAKIA[A-Z0-9]{16}\b']:
        assert not re.search(pattern,text),'Potential secret in '+name
assert subprocess.run(['git','check-ignore','.env','apps/unity/Library/test','artifacts/test'],cwd=r,stdout=subprocess.DEVNULL).returncode==0
print('PASS baseline secret-pattern and ignore checks (not a comprehensive security audit)')

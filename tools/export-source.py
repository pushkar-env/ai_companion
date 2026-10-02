"""Create an isolated, cache-free source snapshot without committing or modifying source files."""
import pathlib,subprocess,shutil,json,hashlib
r=pathlib.Path(__file__).resolve().parents[1]
target=r/'artifacts'/'source-check-m0'
if target.exists():raise SystemExit('Snapshot already exists; use it or choose a new destination explicitly. No files overwritten.')
target.mkdir(parents=True)
files=subprocess.check_output(['git','ls-files','--cached','--others','--exclude-standard'],cwd=r,text=True).splitlines()
manifest={}
for name in files:
    source=r/name
    if not source.is_file():continue
    dest=target/name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,dest)
    manifest[name]=hashlib.sha256(source.read_bytes()).hexdigest()
subprocess.run(['git','init',str(target)],check=True,stdout=subprocess.DEVNULL)
(r/'docs/evidence/source-snapshot.json').write_text(json.dumps(manifest,indent=2)+'\n')
print('Exported '+str(len(manifest))+' source files to '+str(target)+'; excluded caches, .env and artifacts.')

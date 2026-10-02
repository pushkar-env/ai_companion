"""Verify an isolated Unity build's source copy and record its exact input hashes."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path


def fingerprint(project):
    files = {}
    for folder in ("Assets", "Packages", "ProjectSettings"):
        directory = project / folder
        if not directory.is_dir():
            raise ValueError(f"Missing required input directory: {directory}")
        for path in sorted(directory.rglob("*")):
            if path.is_file():
                digest = hashlib.sha256()
                with path.open("rb") as stream:
                    for block in iter(lambda: stream.read(1024 * 1024), b""):
                        digest.update(block)
                files[path.relative_to(project).as_posix()] = digest.hexdigest()
    return files


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--snapshot", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    source, snapshot, output = (p.resolve() for p in (args.source, args.snapshot, args.output))
    if source == snapshot:
        parser.error("Source and snapshot must be different projects")
    if any(output.is_relative_to(project / folder) for project in (source, snapshot)
           for folder in ("Assets", "Packages", "ProjectSettings")):
        parser.error("Evidence must be outside the source input folders")
    if output.exists():
        parser.error("Evidence already exists; choose a new output path")
    original, copied = fingerprint(source), fingerprint(snapshot)
    differences = [name for name in sorted(original.keys() | copied.keys())
                   if original.get(name) != copied.get(name)]
    report = {
        "checked_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "source": str(source), "snapshot": str(snapshot),
        "scope": "Assets, Packages, ProjectSettings including metadata; excludes Library and UserSettings",
        "passed": not differences, "differing_paths": differences,
        "source_files": original, "snapshot_files": copied,
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"{'FAIL' if differences else 'PASS'} build snapshot: {len(copied)} files, {len(differences)} differences")
    raise SystemExit(1 if differences else 0)


if __name__ == "__main__":
    main()

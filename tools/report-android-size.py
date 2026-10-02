"""Offline APK size evidence. Does not extract, alter, install, or execute the package."""
import argparse
import json
import pathlib
import zipfile


def inspect(path):
    with zipfile.ZipFile(path) as archive:
        entries = sorted(archive.infolist(), key=lambda item: item.compress_size, reverse=True)
        compressed = sum(item.compress_size for item in entries)
        return {
            "apk": str(path.resolve()),
            "apk_bytes": path.stat().st_size,
            "zip_payload_bytes": compressed,
            "zip_headers_signing_and_alignment_bytes": path.stat().st_size - compressed,
            "largest_entries": [
                {"path": item.filename, "packaged_bytes": item.compress_size, "uncompressed_bytes": item.file_size}
                for item in entries[:20]
            ],
        }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=pathlib.Path)
    parser.add_argument("--baseline", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()
    # Never overwrite a package if an output path is mistyped.
    if args.output.resolve() in {args.apk.resolve(), args.baseline.resolve() if args.baseline else None}:
        parser.error("Report output must differ from APK inputs")
    result = inspect(args.apk)
    result["target_bytes"] = 200_000_000
    result["within_size_target"] = result["apk_bytes"] <= result["target_bytes"]
    result["scope"] = "Development diagnostic APK only; not production install, memory or performance acceptance"
    if args.baseline:
        baseline = inspect(args.baseline)
        result["baseline_apk_bytes"] = baseline["apk_bytes"]
        result["saved_bytes"] = baseline["apk_bytes"] - result["apk_bytes"]
        result["reduction_percent"] = round(result["saved_bytes"] * 100 / baseline["apk_bytes"], 2)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"APK {result['apk_bytes']:,} bytes; 200 MB diagnostic size target: {'met' if result['within_size_target'] else 'not met'}")
    if args.baseline:
        print(f"Change from baseline: {result['saved_bytes']:,} bytes saved ({result['reduction_percent']}%)")


if __name__ == "__main__":
    main()

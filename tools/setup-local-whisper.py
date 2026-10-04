"""One-time public model download. Run with the project's whisper-env Python."""
import os
from pathlib import Path
os.environ['HF_HUB_DISABLE_IMPLICIT_TOKEN'] = '1'
os.environ['HF_HUB_DISABLE_TELEMETRY'] = '1'
from huggingface_hub import snapshot_download
root = Path(__file__).resolve().parents[1]
snapshot_download('Systran/faster-whisper-small',
    revision='536b0662742c02347bc0e980a01041f333bce120',
    local_dir=root / 'artifacts/whisper-small',
    allow_patterns=['config.json', 'model.bin', 'tokenizer.json', 'vocabulary.*'])
print('Local Whisper model ready. Runtime transcription uses offline files only.')

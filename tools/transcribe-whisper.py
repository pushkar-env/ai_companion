"""Bounded, offline English PCM transcription. Never writes microphone audio."""
import os
os.environ['HF_HUB_OFFLINE'] = '1'
os.environ['HF_HUB_DISABLE_TELEMETRY'] = '1'
import base64
import json
import math
import sys
from pathlib import Path
import numpy as np
from faster_whisper import WhisperModel
from faster_whisper.vad import get_speech_timestamps, VadOptions

def transcribe(data):
    if data.get('sampleRate') != 16000 or not isinstance(data.get('pcm'), str):
        raise ValueError('Invalid audio')
    pcm = base64.b64decode(data['pcm'], validate=True)
    if not 8000 <= len(pcm) <= 640000 or len(pcm) % 2:
        raise ValueError('Invalid audio length')
    audio = np.frombuffer(pcm, dtype='<i2').astype(np.float32) / 32768
    audio -= np.mean(audio)  # Remove DC without trimming speech or internal pauses.
    peak = float(np.max(np.abs(audio)))
    rms = float(np.sqrt(np.mean(audio * audio)))
    warning = 'clipped' if float(np.mean(np.abs(audio) >= .98)) > .005 else ''
    if rms < .0001:
        return dict(text='', confidence=0, warning='no_signal')
    if peak < .025:
        warning = 'quiet'
    # Bounded gain; no claim of noise removal or repair of clipped speech.
    audio *= min(8, .7 / max(peak, .0001)) if peak < .35 else 1
    ranges = get_speech_timestamps(audio, VadOptions(min_silence_duration_ms=1000, speech_pad_ms=300))
    if not ranges:
        return dict(text='', confidence=0, warning=warning or 'no_speech')
    model_path = Path(__file__).resolve().parents[1] / 'artifacts/whisper-small'
    model = WhisperModel(str(model_path), device='cpu', compute_type='int8',
                         cpu_threads=4, num_workers=1, local_files_only=True)
    parts = []
    # Decode separate utterances separately; concatenating across long pauses can
    # cause repeated real phrases to be mistaken for decoder repetition.
    for span in ranges:
        segments, _ = model.transcribe(audio[span['start']:span['end']], language='en', task='transcribe',
            beam_size=5, temperature=0, condition_on_previous_text=False, vad_filter=False)
        parts.extend(segments)
    text = ' '.join(s.text.strip() for s in parts).strip()
    if len(text) > 500:
        raise ValueError('Transcript too long')
    confidence = min((math.exp(min(0, s.avg_logprob)) for s in parts), default=0)
    if not warning and text and (confidence < .55 or any(s.no_speech_prob > .4 for s in parts)):
        warning = 'uncertain'
    audio.fill(0)
    return dict(text=text, confidence=confidence, warning=warning)

if __name__ == '__main__':
    try:
        payload = sys.stdin.buffer.read(860001)
        if len(payload) > 860000:
            raise ValueError('Input too large')
        print(json.dumps(transcribe(json.loads(payload)), ensure_ascii=True))
    except Exception:
        # Do not print audio, recognized text, request values or traceback data.
        print('Local Whisper transcription failed.', file=sys.stderr)
        sys.exit(1)

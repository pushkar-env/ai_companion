// Read-only checks: no inference, audio capture, downloads or model loading.
export async function checkLocalAi(model, fetcher = fetch) {
  try {
    const response = await fetcher('http://127.0.0.1:11434/api/tags', { signal: AbortSignal.timeout(2500) });
    if (!response.ok) return 'engine_unavailable';
    const data = await response.json();
    if (!Array.isArray(data.models)) return 'engine_unavailable';
    const canonical = name => name.includes(':') ? name : name + ':latest';
    return data.models.some(item => typeof item?.name === 'string' && canonical(item.name) === canonical(model)) ? 'ready' : 'model_missing';
  } catch { return 'engine_unavailable'; }
}

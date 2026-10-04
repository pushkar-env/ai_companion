// Keep sentence boundaries, abbreviations and Unicode handling in the platform segmenter.
export function speechChunks(text) {
  if (typeof text !== 'string' || !text.trim() || text.length > 600) throw new Error('invalid_model_reply');
  const parts = [...new Intl.Segmenter('en', { granularity: 'sentence' }).segment(text)].map(s => s.segment.trim()).filter(Boolean);
  // Bound work/queue size even when a model produces many short sentences.
  return parts.length > 3 ? [parts[0], parts[1], parts.slice(2).join(' ')] : parts;
}

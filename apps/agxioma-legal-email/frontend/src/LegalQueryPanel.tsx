import { useState } from 'react';
import { queryLegalCodes } from './api';

type QueryMode = 'baseline' | 'reranker' | 'hyde' | 'hyde+reranker';

const QUERY_MODES: { id: QueryMode; label: string; color: string; desc: string }[] = [
  { id: 'baseline',      label: 'Baseline',       color: '#6b7280', desc: 'solo coseno vettoriale' },
  { id: 'reranker',      label: 'Reranker',        color: '#7c3aed', desc: 'LLM cross-encoder' },
  { id: 'hyde',          label: 'HyDE',            color: '#0ea5e9', desc: 'doc ipotetico DeepSeek' },
  { id: 'hyde+reranker', label: 'HyDE + Reranker', color: '#d97706', desc: 'massima qualità' },
];

export default function LegalQueryPanel() {
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<any>(null);
  const [mode, setMode] = useState<QueryMode>('baseline');

  const useReranker = mode === 'reranker' || mode === 'hyde+reranker';
  const useHyde     = mode === 'hyde'     || mode === 'hyde+reranker';

  async function search() {
    if (!query.trim() || loading) return;
    setLoading(true);
    setResult(null);
    try {
      const res = await queryLegalCodes(query, useReranker, useHyde);
      setResult(res);
    } catch (e) {
      setResult({ answer: 'Errore di connessione.', contextFound: false });
    } finally {
      setLoading(false);
    }
  }

  const EXAMPLES = [
    'Art. 2043 codice civile',
    'reato di furto con scasso',
    'responsabilità contrattuale inadempimento',
    'risarcimento danno ingiusto',
    'Art. 624 codice penale',
  ];

  return (
    <div>
      <h2 style={{ marginBottom: 8 }}>🔍 Ricerca Codici Legali</h2>
      <p style={{ color: 'var(--text2)', fontSize: 13, marginBottom: 12 }}>
        Interroga il database vettoriale di Codice Civile e Codice Penale.
      </p>

      <div className="examples">
        {EXAMPLES.map((ex, i) => (
          <div key={i} className="ex-btn" onClick={() => { setQuery(ex); }}>{ex}</div>
        ))}
      </div>

      <div className="form">
        <input value={query} onChange={e => setQuery(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && search()}
          placeholder="Es: Art. 2043 codice civile" disabled={loading} />

        <label style={{ fontSize: 12, color: 'var(--text2)', marginBottom: 4, display: 'block' }}>
          Modalità ricerca
        </label>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 8 }}>
          {QUERY_MODES.map(m => {
            const active = mode === m.id;
            return (
              <button key={m.id} onClick={() => setMode(m.id)} disabled={loading}
                style={{
                  padding: '5px 12px', borderRadius: 8, fontSize: 12, cursor: 'pointer',
                  border: `1.5px solid ${active ? m.color : 'var(--border, #374151)'}`,
                  background: active ? m.color + '22' : 'transparent',
                  color: active ? m.color : 'var(--text2)',
                  fontWeight: active ? 700 : 400, transition: '.15s', textAlign: 'left',
                }}>
                <div>{m.label}</div>
                <div style={{ fontSize: 10, opacity: .8 }}>{m.desc}</div>
              </button>
            );
          })}
        </div>

        <button onClick={search} disabled={loading}>
          {loading ? 'Ricerca...' : 'Cerca'}
        </button>
      </div>

      {result && (
        <div className="query-result">
          {result.contextFound ? (
            <>
              <h3>Risposta</h3>
              <div className="answer">{result.answer}</div>
              {result.sources?.length > 0 && (
                <>
                  <h4 style={{ marginTop: 16, display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                    <span>Fonti</span>
                    {result.hydeUsed && (
                      <span style={{ fontSize: 11, padding: '1px 8px', borderRadius: 10,
                        background: '#0ea5e922', border: '1px solid #0ea5e988', color: '#38bdf8' }}>
                        🧪 HyDE
                      </span>
                    )}
                    {result.rerankerUsed && (
                      <span style={{ fontSize: 11, padding: '1px 8px', borderRadius: 10,
                        background: '#7c3aed22', border: '1px solid #7c3aed88', color: '#a78bfa' }}>
                        ⟳ Reranker
                      </span>
                    )}
                    {!result.hydeUsed && !result.rerankerUsed && (
                      <span style={{ fontSize: 11, color: 'var(--text2)' }}>(ordine vettoriale)</span>
                    )}
                  </h4>
                  {result.sources.map((s: any, i: number) => (
                    <div key={i} className="source">
                      <strong>{s.slug || s.documentId}</strong>
                      <span style={{ fontSize: 11, color: 'var(--text2)', marginLeft: 8 }}>
                        score: {s.score?.toFixed(3) ?? s.score?.toFixed(3) ?? '—'}
                        {s.embedScore != null && <> · embed: {s.embedScore.toFixed(2)}</>}
                        {s.rerankScore != null && <> · rerank: {s.rerankScore.toFixed(1)}/10</>}
                      </span>
                      <p>{s.snippet}</p>
                    </div>
                  ))}
                </>
              )}
            </>
          ) : (
            <p style={{ color: 'var(--text2)' }}>Nessun risultato trovato. Il database è stato popolato?</p>
          )}
        </div>
      )}
    </div>
  );
}

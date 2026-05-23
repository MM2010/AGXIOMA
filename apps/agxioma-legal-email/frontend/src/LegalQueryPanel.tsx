import { useState } from 'react';
import { queryLegalCodes } from './api';

export default function LegalQueryPanel() {
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<any>(null);
  const [useReranker, setUseReranker] = useState(false);

  async function search() {
    if (!query.trim() || loading) return;
    setLoading(true);
    setResult(null);
    try {
      const res = await queryLegalCodes(query, useReranker);
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

        <label style={{ display: 'flex', alignItems: 'center', gap: 10, margin: '8px 0', cursor: 'pointer', userSelect: 'none' }}>
          <span style={{
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            width: 36, height: 20, borderRadius: 10, cursor: 'pointer', transition: '.2s',
            background: useReranker ? 'var(--accent, #7c3aed)' : 'var(--border, #374151)',
          }} onClick={() => setUseReranker(v => !v)}>
            <span style={{
              width: 14, height: 14, borderRadius: '50%', background: '#fff',
              transform: useReranker ? 'translateX(8px)' : 'translateX(-8px)',
              transition: '.2s', display: 'block',
            }} />
          </span>
          <span style={{ fontSize: 13 }}>Reranker</span>
          <span style={{
            fontSize: 11, padding: '1px 7px', borderRadius: 10,
            background: useReranker ? '#7c3aed22' : 'transparent',
            border: '1px solid ' + (useReranker ? '#7c3aed88' : 'var(--border, #374151)'),
            color: useReranker ? '#a78bfa' : 'var(--text2)',
          }}>
            {useReranker ? 'ON — LLM cross-encoder' : 'OFF — solo similarità vettoriale'}
          </span>
        </label>

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
              <h4 style={{ marginTop: 16 }}>
                  Fonti {result.rerankerUsed ? '⟳ riordinate dal reranker' : '(ordine vettoriale)'}
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

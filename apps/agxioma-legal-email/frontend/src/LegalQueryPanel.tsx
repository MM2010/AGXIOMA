import { useState } from 'react';
import { queryLegalCodes } from './api';

export default function LegalQueryPanel() {
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<any>(null);

  async function search() {
    if (!query.trim() || loading) return;
    setLoading(true);
    setResult(null);
    try {
      const res = await queryLegalCodes(query);
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
                  <h4 style={{ marginTop: 16 }}>Fonti (riordinate da reranker)</h4>
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

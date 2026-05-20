import { useState } from 'react';
import { analyzeTopic, describeApiError } from './api';

export default function AnalyzePanel() {
  const [topic, setTopic] = useState('');
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<{
    synthesis: string;
    analysis: string;
    findings: string[];
    phases: { phase: string; durationS: number }[];
  } | null>(null);

  async function analyze() {
    if (!topic.trim() || loading) return;
    setLoading(true);
    setResult(null);
    try {
      const res = await analyzeTopic(topic);
      setResult(res);
    } catch (error) {
      setResult({
        synthesis: describeApiError(error, 'Errore di connessione. Verifica che il backend sia attivo su http://localhost:5012.'),
        analysis: '',
        findings: [],
        phases: [],
      });
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="analyze-page">
      <h2 style={{ marginBottom: 12 }}>🔍 Analisi Deep Research</h2>
      <p style={{ color: 'var(--text2)', marginBottom: 16, fontSize: 13 }}>
        Il <strong>HeavySwarm</strong> genera domande di ricerca, le distribuisce
        a worker paralleli e sintetizza un report strutturato.
      </p>

      <div className="analyze-input">
        <input
          value={topic}
          onChange={(e) => setTopic(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && analyze()}
          placeholder="Es: Analizza l'impatto dei ticket API 401 del mese scorso"
          disabled={loading}
        />
        <button onClick={analyze} disabled={loading}>
          {loading ? 'Analisi in corso...' : 'Analizza'}
        </button>
      </div>

      {loading && (
        <div className="analyze-report">
          <h3>⏳ Ricerca in corso...</h3>
          <p style={{ color: 'var(--text2)' }}>
            Il HeavySwarm sta distribuendo il task a worker paralleli.
            L'operazione può richiedere 20-60 secondi.
          </p>
        </div>
      )}

      {result && (
        <>
          {result.phases.length > 0 && (
            <div className="analyze-phases">
              {result.phases.map((p, i) => (
                <div key={i} className="analyze-phase">
                  <span className="name">{p.phase}</span>{' '}
                  <span className="dur">({p.durationS.toFixed(1)}s)</span>
                </div>
              ))}
            </div>
          )}

          {result.analysis && (
            <div className="analyze-report" style={{ marginBottom: 16 }}>
              <h3>📊 Analisi</h3>
              {result.analysis}
            </div>
          )}

          {result.synthesis && (
            <div className="analyze-report">
              <h3>📝 Report Sintesi</h3>
              {result.synthesis}
            </div>
          )}
        </>
      )}
    </div>
  );
}

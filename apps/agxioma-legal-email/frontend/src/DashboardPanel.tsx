import { useState, useEffect } from 'react';
import { getDashboard, getIngestionStatus } from './api';

export default function DashboardPanel() {
  const [dash, setDash] = useState<any>(null);
  const [ingestion, setIngestion] = useState<any>(null);

  useEffect(() => {
    async function load() {
      try {
        const [d, i] = await Promise.all([getDashboard(), getIngestionStatus()]);
        setDash(d);
        setIngestion(i);
      } catch { /* ignore */ }
    }
    load();
    const id = setInterval(load, 5000);
    return () => clearInterval(id);
  }, []);

  return (
    <div>
      <h2 style={{ marginBottom: 8 }}>Dashboard</h2>

      <div className="ingestion-status" style={{ marginBottom: 16 }}>
        <h3>📚 Stato Ingestion Codici</h3>
        <p>
          {ingestion === null ? 'Caricamento...' :
            ingestion.ingested
              ? `Ingestito — ${ingestion.chunks} chunks in DB`
              : 'Prima ingestione in corso...'}
        </p>
      </div>

      {dash && (
        <div className="bo-grid">
          <div className="bo-stat">
            <div className="num">{dash.totalEmails}</div>
            <div className="lbl">Email Ricevute</div>
          </div>
          <div className="bo-stat">
            <div className="num">{dash.inHitl}</div>
            <div className="lbl">In Revisione (HITL)</div>
          </div>
          <div className="bo-stat">
            <div className="num">{dash.completed}</div>
            <div className="lbl">Completate</div>
          </div>
          <div className="bo-stat">
            <div className="num">{dash.pendingReview}</div>
            <div className="lbl">In Attesa Review</div>
          </div>
        </div>
      )}

      {!dash && <p style={{ color: 'var(--text2)' }}>Nessun dato ancora. Invia un'email per iniziare.</p>}
    </div>
  );
}

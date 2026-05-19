import { useEffect, useState, useCallback } from 'react';
import { getDashboard, getRuns, getTickets, closeRun, submitSurvey } from './api';

interface Dashboard {
  runtimeMode: string;
  canonicalStore: string;
  runsTotal: number;
  runsPaused: number;
  runsClosed: number;
  ticketsTotal: number;
  surveysCompleted: number;
  notificationsSent: number;
  notificationsOutbox: number;
  notificationsFailed: number;
  recentRuns: any[];
  recentTickets: any[];
  recentSurveys: any[];
}

export default function BackofficePanel() {
  const [dash, setDash] = useState<Dashboard | null>(null);
  const [runs, setRuns] = useState<any[]>([]);
  const [tickets, setTickets] = useState<any[]>([]);
  const [surveyTicket, setSurveyTicket] = useState<string | null>(null);
  const [surveyScore, setSurveyScore] = useState(0);
  const [surveyComment, setSurveyComment] = useState('');

  const load = useCallback(async () => {
    try {
      const [d, r, t] = await Promise.all([getDashboard(), getRuns(), getTickets()]);
      setDash(d);
      setRuns(r);
      setTickets(t);
    } catch {
      // silently fail
    }
  }, []);

  useEffect(() => {
    load();
    const id = setInterval(load, 10000);
    return () => clearInterval(id);
  }, [load]);

  async function handleClose(runId: string) {
    await closeRun(runId);
    load();
  }

  async function handleSurvey() {
    if (!surveyTicket || surveyScore < 1 || surveyScore > 5) return;
    await submitSurvey(surveyTicket, surveyScore, surveyComment || undefined);
    setSurveyTicket(null);
    setSurveyScore(0);
    setSurveyComment('');
    load();
  }

  return (
    <div className="bo-page">
      <h2 style={{ marginBottom: 4 }}>📊 Backoffice Dashboard</h2>
      <p style={{ color: 'var(--text2)', fontSize: 13, marginBottom: 12 }}>
        Store: <strong>{dash?.canonicalStore ?? '...'}</strong>
        {dash && ` · ${dash.runsTotal} runs totali · ${dash.ticketsTotal} ticket`}
      </p>

      {dash && (
        <div className="bo-grid">
          <div className="bo-stat"><div className="num">{dash.runsTotal}</div><div className="lbl">Run Totali</div></div>
          <div className="bo-stat"><div className="num">{dash.runsPaused}</div><div className="lbl">In Attesa Conf.</div></div>
          <div className="bo-stat"><div className="num">{dash.runsClosed}</div><div className="lbl">Chiusi</div></div>
          <div className="bo-stat"><div className="num">{dash.ticketsTotal}</div><div className="lbl">Ticket Aperti</div></div>
          <div className="bo-stat"><div className="num">{dash.surveysCompleted}</div><div className="lbl">Survey Completate</div></div>
          <div className="bo-stat"><div className="num">{dash.notificationsSent}</div><div className="lbl">Notifiche Inviate</div></div>
        </div>
      )}

      <div className="bo-tables">
        <div className="card" style={{ display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          <h2>Run Recenti</h2>
          <div className="bo-table-wrap">
            <table>
              <thead>
                <tr><th>ID</th><th>Area</th><th>Priorità</th><th>Stato</th><th></th></tr>
              </thead>
              <tbody>
                {runs.map((r: any) => (
                  <tr key={r.id}>
                    <td style={{ fontFamily: 'monospace', fontSize: 11 }}>{r.id?.slice(0, 8)}</td>
                    <td>{r.area}</td>
                    <td>{r.priority}</td>
                    <td>
                      <span className={`status-badge ${r.status === 'closed' ? 'closed' : r.status?.includes('paused') ? 'paused' : 'open'}`}>
                        {r.status}
                      </span>
                    </td>
                    <td>
                      {r.status !== 'closed' && (
                        <button
                          onClick={() => handleClose(r.id)}
                          style={{
                            fontSize: 11,
                            padding: '2px 8px',
                            border: '1px solid var(--border)',
                            borderRadius: 4,
                            background: 'var(--surface2)',
                            color: 'var(--text)',
                            cursor: 'pointer',
                          }}
                        >
                          Chiudi
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
                {runs.length === 0 && (
                  <tr><td colSpan={5} style={{ color: 'var(--text2)', textAlign: 'center', padding: 20 }}>
                    Nessuna run. Crea un ticket dal backoffice via API.
                  </td></tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        <div className="card" style={{ display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          <h2>Ticket</h2>
          <div className="bo-table-wrap">
            <table>
              <thead>
                <tr><th>ID</th><th>Area</th><th>Oggetto</th><th>Priorità</th><th>Stato</th><th></th></tr>
              </thead>
              <tbody>
                {tickets.map((t: any) => (
                  <tr key={t.id}>
                    <td style={{ fontFamily: 'monospace', fontSize: 11 }}>{t.id?.slice(0, 8)}</td>
                    <td>{t.area}</td>
                    <td style={{ maxWidth: 160, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {t.subject}
                    </td>
                    <td>{t.priority}</td>
                    <td><span className={`status-badge ${t.status === 'closed' ? 'closed' : 'open'}`}>{t.status}</span></td>
                    <td>
                      <button
                        onClick={() => setSurveyTicket(t.id)}
                        style={{
                          fontSize: 11,
                          padding: '2px 8px',
                          border: '1px solid var(--border)',
                          borderRadius: 4,
                          background: 'var(--surface2)',
                          color: 'var(--text)',
                          cursor: 'pointer',
                        }}
                      >
                        Survey
                      </button>
                    </td>
                  </tr>
                ))}
                {tickets.length === 0 && (
                  <tr><td colSpan={6} style={{ color: 'var(--text2)', textAlign: 'center', padding: 20 }}>
                    Nessun ticket.
                  </td></tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      {surveyTicket && (
        <div className="survey-overlay" onClick={() => setSurveyTicket(null)}>
          <div className="survey-box" onClick={(e) => e.stopPropagation()}>
            <h2>Valuta il servizio</h2>
            <div className="survey-stars">
              {[1, 2, 3, 4, 5].map((n) => (
                <button
                  key={n}
                  className={n <= surveyScore ? 'active' : ''}
                  onClick={() => setSurveyScore(n)}
                >
                  ⭐
                </button>
              ))}
            </div>
            <textarea
              rows={3}
              placeholder="Commento (opzionale)..."
              value={surveyComment}
              onChange={(e) => setSurveyComment(e.target.value)}
            />
            <button onClick={handleSurvey} disabled={surveyScore < 1}>
              Invia Survey
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

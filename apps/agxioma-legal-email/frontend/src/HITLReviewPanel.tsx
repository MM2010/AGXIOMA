import { useState, useEffect } from 'react';
import { approveReview, rejectReview, getDraft } from './api';

interface Props { emailId: string | null; }

export default function HITLReviewPanel({ emailId }: Props) {
  const [inputId, setInputId] = useState(emailId ?? '');
  const [notes, setNotes] = useState('');
  const [message, setMessage] = useState('');
  const [draft, setDraft] = useState('');
  const [loadingDraft, setLoadingDraft] = useState(false);
  const [pipelineState, setPipelineState] = useState('');

  // Sync emailId from prop (auto-navigate) and auto-load draft
  useEffect(() => {
    if (emailId) {
      setInputId(emailId);
      loadDraftById(emailId);
    }
  }, [emailId]);

  async function loadDraft() {
    if (!inputId) return;
    await loadDraftById(inputId);
  }

  async function loadDraftById(id: string) {
    setLoadingDraft(true);
    setDraft('');
    setMessage('');
    // First fetch pipeline state to show progress
    try {
      const p = await fetch(`/api/email/${id}/pipeline`).then(r => r.json());
      setPipelineState(p.state ?? '');
    } catch { /* ignore */ }
    // Then try to get draft — poll up to 60s if not ready
    for (let attempt = 0; attempt < 30; attempt++) {
      try {
        const res = await getDraft(id);
        if (res.draft) {
          setDraft(res.draft);
          setLoadingDraft(false);
          setMessage('✅ Bozza pronta per revisione!');
          return;
        }
      } catch { /* ignore */ }
      // Wait 2s before retry
      await new Promise(r => setTimeout(r, 2000));
    }
    setLoadingDraft(false);
    setDraft('⚠️ Bozza non ancora disponibile. La pipeline agenti è ancora in esecuzione. Torna tra qualche istante e clicca "Carica Bozza".');
  }

  async function handleApprove() {
    if (!inputId) return;
    try {
      await approveReview(inputId, 'avv-001');
      setMessage('✅ Risposta approvata e inviata al cliente.');
      setDraft('');
    } catch (e) {
      setMessage('❌ Errore: ' + (e instanceof Error ? e.message : 'connessione'));
    }
  }

  async function handleReject() {
    if (!inputId || !notes.trim()) return;
    try {
      await rejectReview(inputId, 'avv-001', notes);
      setMessage('🔄 Bozza rigettata. Richiesta revisione.');
      setDraft('');
    } catch (e) {
      setMessage('❌ Errore: ' + (e instanceof Error ? e.message : 'connessione'));
    }
  }

  return (
    <div>
      <h2 style={{ marginBottom: 8 }}>👩‍⚖️ Revisione Avvocato</h2>
      <p style={{ color: 'var(--text2)', fontSize: 13, marginBottom: 16 }}>
        Approva o rigetta la bozza di risposta generata dalla pipeline agenti.
      </p>

      <div className="form">
        <label>ID Email</label>
        <div style={{ display: 'flex', gap: 8 }}>
          <input value={inputId} onChange={e => setInputId(e.target.value)} placeholder="Incolla l'ID email" style={{ flex: 1 }} />
          <button onClick={loadDraft} disabled={!inputId || loadingDraft} style={{ whiteSpace: 'nowrap' }}>
            {loadingDraft ? 'Caricamento...' : 'Carica Bozza'}
          </button>
        </div>

        {pipelineState && !draft && (
          <div style={{ fontSize: 12, color: 'var(--text2)', marginTop: 8 }}>
            Stato pipeline: <strong>{pipelineState}</strong> — attendere il completamento...
          </div>
        )}

        {draft && (
          <div className="draft-preview">
            <h3>📝 Bozza di Risposta</h3>
            <pre style={{ whiteSpace: 'pre-wrap', fontSize: 12, lineHeight: 1.6, background: 'var(--surface2)', padding: 12, borderRadius: 6, maxHeight: 300, overflow: 'auto' }}>
              {draft}
            </pre>
          </div>
        )}

        <div className="btn-row">
          <button className="btn-approve" onClick={handleApprove} disabled={!inputId || !draft}>
            ✅ Approva e Invia
          </button>
        </div>

        <label>Note di Rigetto (se applicabile)</label>
        <textarea value={notes} onChange={e => setNotes(e.target.value)} placeholder="Spiega cosa va corretto..." rows={4} />
        <button className="btn-reject" onClick={handleReject} disabled={!inputId || !notes.trim()}>
          ❌ Rigetta con Note
        </button>

        {message && <div className="success">{message}</div>}
      </div>
    </div>
  );
}

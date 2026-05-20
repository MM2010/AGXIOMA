import { useState } from 'react';
import { ingestEmail } from './api';

const EXAMPLES = [
  'Buongiorno, ho subito un furto in appartamento la settimana scorsa. Vorrei sapere quali sono i miei diritti e come procedere con la denuncia.',
  'Il mio vicino ha costruito un muro sul confine senza permesso. Cosa posso fare?',
  'Ho ricevuto una lettera di licenziamento che ritengo ingiusta. Vorrei impugnarlo.',
  'Mio padre è deceduto e non ha lasciato testamento. Come funziona la successione?',
  'Ho avuto un incidente stradale per colpa di un automobilista che non si è fermato allo stop. Ho diritto a un risarcimento?',
];

interface Props { onSelectEmail: (id: string | null) => void; }

export default function EmailInboxPanel({ onSelectEmail }: Props) {
  const [sender, setSender] = useState('cliente@example.com');
  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');
  const [loading, setLoading] = useState(false);
  const [resultId, setResultId] = useState<string | null>(null);

  async function send() {
    if (!subject.trim() || !body.trim() || loading) return;
    setLoading(true);
    setResultId(null);
    try {
      const res = await ingestEmail(sender, subject, body);
      setResultId(res.emailId);
      onSelectEmail(res.emailId);
    } catch (e) {
      alert('Errore: ' + (e instanceof Error ? e.message : 'connessione'));
    } finally {
      setLoading(false);
    }
  }

  function fillExample(ex: string) {
    setSubject('Richiesta di consulenza legale');
    setBody(ex);
  }

  return (
    <div>
      <h2 style={{ marginBottom: 8 }}>📥 Invia Email (Simulata)</h2>
      <p style={{ color: 'var(--text2)', fontSize: 13, marginBottom: 12 }}>
        Invia una richiesta di consulenza — verrà processata dalla pipeline agenti.
      </p>

      <div className="examples">
        {EXAMPLES.map((ex, i) => (
          <div key={i} className="ex-btn" onClick={() => fillExample(ex)}>
            {ex.slice(0, 80)}…
          </div>
        ))}
      </div>

      <div className="form">
        <label>Mittente</label>
        <input value={sender} onChange={e => setSender(e.target.value)} disabled={loading} />

        <label>Oggetto</label>
        <input value={subject} onChange={e => setSubject(e.target.value)} placeholder="Oggetto della richiesta" disabled={loading} />

        <label>Corpo</label>
        <textarea value={body} onChange={e => setBody(e.target.value)} placeholder="Descrivi il tuo caso..." rows={6} disabled={loading} />

        <button onClick={send} disabled={loading}>
          {loading ? 'Invio in corso...' : '📨 Invia Email'}
        </button>

        {resultId && (
          <div className="success">
            ✅ Email inviata! ID: <strong>{resultId}</strong>
            <p>La pipeline è stata avviata. Controlla la colonna Timeline a destra.</p>
          </div>
        )}
      </div>
    </div>
  );
}

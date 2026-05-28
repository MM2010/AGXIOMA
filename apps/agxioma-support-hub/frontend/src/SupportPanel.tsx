import { useState, useRef, useEffect } from 'react';
import { describeApiError, supportChat } from './api';

interface Msg {
  role: 'user' | 'agent';
  text: string;
  tree?: string;
  agent?: string;
  keywords?: string[];
  durationMs?: number;
}

const EXAMPLES = [
  'Ho un problema con la fattura INV-9001, mi hanno addebitato 249€ ma non mi torna',
  'La API sta dando errore 401, il token di autenticazione non funziona più',
  'Vorrei informazioni sul piano Enterprise, quanti utenti supporta?',
  'Ciao, come posso resettare la password del mio account?',
  'Non riesco a fare login, ho dimenticato la password',
];

export default function SupportPanel() {
  const [msgs, setMsgs] = useState<Msg[]>([
    { role: 'agent', text: '👋 Benvenuto! Scrivi un messaggio di supporto o clicca un esempio qui a fianco.' },
  ]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [msgs]);

  async function send(msg?: string) {
    const text = msg ?? input;
    if (!text.trim() || loading) return;
    setInput('');
    setLoading(true);
    setMsgs((p) => [...p, { role: 'user', text }]);
    try {
      const res = await supportChat(text);
      setMsgs((p) => [
        ...p,
        {
          role: 'agent',
          text: res.response,
          tree: res.tree,
          agent: res.routedTo,
          keywords: res.matchedKeywords,
          durationMs: res.durationMs,
        },
      ]);
    } catch (error) {
      setMsgs((p) => [...p, {
        role: 'agent',
        text: describeApiError(error, 'Errore di connessione. Il backend è in esecuzione?'),
      }]);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="chat-page">
      <div className="chat-sidebar">
        <h3>Esempi rapidi</h3>
        {EXAMPLES.map((ex, i) => (
          <div key={i} className="example" onClick={() => send(ex)}>
            {ex}
          </div>
        ))}
        <h3 style={{ marginTop: 20 }}>Come funziona</h3>
        <p style={{ fontSize: 12, color: 'var(--text2)', lineHeight: 1.6 }}>
          Il <strong>ForestSwarm</strong> analizza il messaggio e lo instrada
          all'agente specializzato: billing, technical, sales o general.
          Ogni agente usa tool dedicati (fatture, stato sistema, prezzi).
        </p>
      </div>
      <div className="chat-main">
        <div className="chat-msgs">
          {msgs.map((m, i) => (
            <div key={i} className={`msg ${m.role}`}>
              {m.text}
              {m.role === 'agent' && (m.tree || m.agent) && (
                <div className="meta">
                  {m.tree && <span className="tree">🌲 {m.tree}</span>}
                  {m.agent && <span className="tag">{m.agent}</span>}
                  {m.durationMs && <span>⏱ {(m.durationMs / 1000).toFixed(1)}s</span>}
                </div>
              )}
            </div>
          ))}
          {loading && (
            <div className="msg agent">
              <span style={{ opacity: 0.6 }}>🤔 L'agente sta pensando...</span>
            </div>
          )}
          <div ref={bottomRef} />
        </div>
        <div className="chat-input">
          <input
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && send()}
            placeholder="Scrivi un messaggio di supporto..."
            disabled={loading}
          />
          <button onClick={() => send()} disabled={loading}>
            Invia
          </button>
        </div>
      </div>
    </div>
  );
}

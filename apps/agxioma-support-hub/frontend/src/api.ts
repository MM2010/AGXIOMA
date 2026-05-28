const BASE = '/api';

export class ApiError extends Error {
  status?: number;
  code?: string;

  constructor(message: string, status?: number, code?: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

async function ensureOk(r: Response) {
  if (r.ok) return;

  let message = `HTTP ${r.status}`;
  let code: string | undefined;

  try {
    const body = await r.json();
    if (typeof body?.message === 'string' && body.message.trim()) message = body.message;
    else if (typeof body?.error === 'string' && body.error.trim()) message = body.error;

    if (typeof body?.agxiomeCode === 'string' && body.agxiomeCode.trim()) code = body.agxiomeCode;
    else if (typeof body?.code === 'string' && body.code.trim()) code = body.code;
  } catch {
    try {
      const text = await r.text();
      if (text.trim()) message = text;
    } catch {
      // Keep the HTTP status message when no structured body is available.
    }
  }

  throw new ApiError(message, r.status, code);
}

export function describeApiError(error: unknown, fallback = 'Errore di connessione. Verifica che il backend sia attivo.') {
  if (error instanceof ApiError) {
    if (error.status === 401) return '⚠️ Accesso negato dal backend (401). Controlla autenticazione e permessi.';
    if (error.status && error.status >= 500) {
      const code = error.code ? ` ${error.code}` : '';
      return `⚠️ Errore backend (${error.status})${code}: ${error.message}`;
    }
    return `⚠️ Richiesta non riuscita${error.status ? ` (${error.status})` : ''}: ${error.message}`;
  }

  if (error instanceof TypeError) return `⚠️ ${fallback}`;
  if (error instanceof Error && error.message.trim()) return `⚠️ ${error.message}`;
  return `⚠️ ${fallback}`;
}

export async function supportChat(msg: string) {
  const r = await fetch(`${BASE}/support`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message: msg }),
  });
  await ensureOk(r);
  return r.json() as Promise<{
    message: string;
    routedTo: string;
    tree: string;
    matchedKeywords: string[];
    response: string;
    durationMs: number;
  }>;
}

export async function analyzeTopic(topic: string) {
  const r = await fetch(`${BASE}/analyze`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ topic }),
  });
  await ensureOk(r);
  return r.json() as Promise<{
    topic: string;
    synthesis: string;
    analysis: string;
    findings: string[];
    phases: { phase: string; durationS: number }[];
  }>;
}

export async function createBackofficeRun(req: {
  customerId: string;
  customerEmail: string;
  subject: string;
  message: string;
  preferredLanguage: string;
  preferredChannels: string[];
}) {
  const r = await fetch(`${BASE}/backoffice`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  });
  await ensureOk(r);
  return r.json();
}

export async function getDashboard() {
  const r = await fetch(`${BASE}/backoffice/dashboard`);
  await ensureOk(r);
  return r.json();
}

export async function getRuns() {
  const r = await fetch(`${BASE}/backoffice/runs`);
  await ensureOk(r);
  return r.json();
}

export async function getTickets() {
  const r = await fetch(`${BASE}/backoffice/tickets`);
  await ensureOk(r);
  return r.json();
}

export async function closeRun(runId: string) {
  const r = await fetch(`${BASE}/backoffice/${runId}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ action: 'close' }),
  });
  return r.json();
}

export async function submitSurvey(ticketId: string, score: number, comment?: string) {
  const r = await fetch(`${BASE}/backoffice/survey`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ticketId, score, comment }),
  });
  return r.json();
}

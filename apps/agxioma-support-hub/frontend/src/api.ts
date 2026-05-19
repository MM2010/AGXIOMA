const BASE = '/api';

export async function supportChat(msg: string) {
  const r = await fetch(`${BASE}/support`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message: msg }),
  });
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
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
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
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
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
  return r.json();
}

export async function getDashboard() {
  const r = await fetch(`${BASE}/backoffice/dashboard`);
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
  return r.json();
}

export async function getRuns() {
  const r = await fetch(`${BASE}/backoffice/runs`);
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
  return r.json();
}

export async function getTickets() {
  const r = await fetch(`${BASE}/backoffice/tickets`);
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
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

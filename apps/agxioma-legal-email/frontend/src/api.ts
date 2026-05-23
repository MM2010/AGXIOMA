import * as signalR from '@microsoft/signalr';

const BASE = '/api';

export interface PipelineEntry { phase: string; state: string; description: string; duration: number | null; timestamp: string; }
export interface TimelineResponse { emailId: string; state: string; timeline: PipelineEntry[]; }
interface DashboardData { totalEmails: number; inHitl: number; completed: number; pendingReview: number; avgResponseSeconds: number; }
interface QueryResult { question: string; answer: string; sources: { slug: string; title: string; snippet: string; score: number; embedScore?: number; rerankScore?: number; }[]; contextFound: boolean; }

/** Create a persistent SignalR connection to receive real-time pipeline updates */
export function createHubConnection(
  onStateChanged: (emailId: string, state: string, timeline: PipelineEntry[]) => void
): signalR.HubConnection {
  const conn = new signalR.HubConnectionBuilder()
    .withUrl('/hub/pipeline')
    .withAutomaticReconnect()
    .build();

  conn.on('PipelineStateChanged', (emailId: string, state: string, timeline: PipelineEntry[]) => {
    onStateChanged(emailId, state, timeline);
  });

  conn.start().catch(() => {}); // non-critical
  return conn;
}

export async function ingestEmail(sender: string, subject: string, body: string, useReranker = false) {
  const r = await fetch(`${BASE}/email/ingest`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sender, subject, body, attachments: [], useReranker }),
  });
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
  return r.json() as Promise<{ emailId: string; state: string; timeline: PipelineEntry[] }>;
}

export async function getPipeline(emailId: string) {
  const r = await fetch(`${BASE}/email/${emailId}/pipeline`);
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
  return r.json() as Promise<TimelineResponse>;
}

export async function approveReview(emailId: string, lawyerId: string) {
  const r = await fetch(`${BASE}/review/approve`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ emailId, lawyerId }),
  });
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
  return r.json();
}

export async function rejectReview(emailId: string, lawyerId: string, notes: string) {
  const r = await fetch(`${BASE}/review/reject`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ emailId, lawyerId, notes }),
  });
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
  return r.json();
}

export async function queryLegalCodes(q: string, useReranker = false) {
  const r = await fetch(`${BASE}/legal/query?q=${encodeURIComponent(q)}&rerank=${useReranker}`);
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
  return r.json() as Promise<QueryResult & { rerankerUsed?: boolean }>;
}

export async function getDashboard() {
  const r = await fetch(`${BASE}/dashboard`);
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
  return r.json() as Promise<DashboardData>;
}

export async function getIngestionStatus() {
  const r = await fetch(`${BASE}/legal/ingestion-status`);
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
  return r.json() as Promise<{ ingested: boolean; chunks: number }>;
}

export async function getDraft(emailId: string) {
  const r = await fetch(`${BASE}/email/${emailId}/draft`);
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
  return r.json() as Promise<{ draft: string }>;
}

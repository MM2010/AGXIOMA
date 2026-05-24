import { useRealtimePipeline, transportLabel } from './useRealtimePipeline';
import { getPipeline } from './api';
import type { TimelineResponse } from './api';
import { useState, useEffect } from 'react';

const STATE_ICONS: Record<string, string> = {
  Ingested: '📥', Categorized: '🏷️', QueryExploded: '💥', RagCompleted: '📚',
  AnalysisDone: '🔍', DraftReady: '📝', HitlPending: '⏳', Approved: '✅',
  Rejected: '❌', ReplySent: '📤', AwaitingRevision: '🔄', FollowupDone: '✔️',
};

interface Props { emailId: string | null; }

function formatDuration(d: any): string {
  if (d === null || d === undefined) return '';
  if (typeof d === 'number') return `${(d / 1000).toFixed(1)}s`;
  const parts = (d as string).split(':');
  if (parts.length === 3) {
    const h = parseInt(parts[0]) || 0;
    const m = parseInt(parts[1]) || 0;
    const sec = parseFloat(parts[2]) || 0;
    const totalSec = h * 3600 + m * 60 + sec;
    return `${totalSec.toFixed(1)}s`;
  }
  return String(d);
}

function formatTokens(n: number): string {
  return n >= 1000 ? `${(n / 1000).toFixed(1)}k` : String(n);
}

export default function PipelineTimelinePanel({ emailId }: Props) {
  const { state, timeline, transport } = useRealtimePipeline(emailId);
  const [costData, setCostData] = useState<Pick<TimelineResponse, 'totalTokensUsed' | 'estimatedCostUsd'>>({});

  // Fetch totalTokensUsed + estimatedCostUsd from REST when pipeline completes
  useEffect(() => {
    if (!emailId) { setCostData({}); return; }
    const terminal = ['HitlPending', 'Approved', 'ReplySent', 'AwaitingRevision', 'FollowupDone'];
    if (!terminal.includes(state ?? '')) return;
    getPipeline(emailId).then(r => setCostData({
      totalTokensUsed: r.totalTokensUsed,
      estimatedCostUsd: r.estimatedCostUsd,
    })).catch(() => {});
  }, [emailId, state]);

  // Compute totals from live timeline as fallback
  const liveTotal = timeline.reduce((acc, e) => acc + (e.tokensUsed ?? 0), 0);
  const totalTokens = costData.totalTokensUsed ?? liveTotal;
  // Blended rate: $0.19/M (65% input @$0.14, 35% output @$0.28)
  const estimatedCost = costData.estimatedCostUsd ?? (totalTokens * 0.00000019);

  return (
    <div>
      <h3 style={{ marginBottom: 12 }}>📋 Timeline Pipeline</h3>

      {!emailId && (
        <p style={{ color: 'var(--text2)', fontSize: 12 }}>
          Invia un'email per vedere la timeline in tempo reale.
        </p>
      )}

      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
        {state && (
          <div className="current-state">
            Stato: <strong>{state}</strong>
          </div>
        )}
        <span style={{ fontSize: 10, color: 'var(--text2)', background: 'var(--surface2)', padding: '2px 6px', borderRadius: 4 }}>
          {transportLabel(transport)}
        </span>
      </div>

      <div className="timeline">
        {timeline.map((entry, i) => (
          <div key={i} className={`tl-item ${entry.state === state ? 'active' : ''}`}>
            <div className="tl-icon">{STATE_ICONS[entry.state] || '●'}</div>
            <div className="tl-content">
              <div className="tl-phase">{entry.phase}</div>
              <div className="tl-desc">{entry.description}</div>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginTop: 2 }}>
                {entry.duration != null && <div className="tl-dur">⏱ {formatDuration(entry.duration)}</div>}
                {(entry.tokensUsed ?? 0) > 0 && (
                  <span style={{
                    fontSize: 10, padding: '1px 6px', borderRadius: 10,
                    background: '#0ea5e922', color: '#0ea5e9',
                    border: '1px solid #0ea5e933', fontFamily: 'monospace',
                  }}>
                    🔢 {formatTokens(entry.tokensUsed!)} tok
                  </span>
                )}
              </div>
            </div>
          </div>
        ))}
      </div>

      {totalTokens > 0 && (
        <div style={{
          marginTop: 16, padding: '12px 14px', borderRadius: 8,
          background: 'var(--surface2)', border: '1px solid var(--border)',
          fontSize: 12,
        }}>
          <div style={{ fontWeight: 600, marginBottom: 6, color: 'var(--text1)' }}>
            💰 Costo Elaborazione (DeepSeek v4-flash)
          </div>
          <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap', color: 'var(--text2)' }}>
            <span>
              🔢 Token totali: <strong style={{ color: 'var(--text1)', fontFamily: 'monospace' }}>
                {totalTokens.toLocaleString('it-IT')}
              </strong>
            </span>
            <span>
              💵 Costo stimato: <strong style={{ color: '#22c55e', fontFamily: 'monospace' }}>
                ${estimatedCost.toFixed(6)}
              </strong>
            </span>
            <span style={{ color: '#0ea5e9' }}>
              ≈ {(estimatedCost * 100).toFixed(4)}¢ / email
            </span>
          </div>
          <div style={{ marginTop: 6, fontSize: 10, color: 'var(--text2)', opacity: 0.7 }}>
            Stima: tasso blended $0.19/M tok (65% input @$0.14 + 35% output @$0.28) — cache miss.
          </div>
        </div>
      )}
    </div>
  );
}

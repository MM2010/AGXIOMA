import { useState, useEffect } from 'react';
import { getPipeline } from './api';

const STATE_ICONS: Record<string, string> = {
  Ingested: '📥', Categorized: '🏷️', QueryExploded: '💥', RagCompleted: '📚',
  AnalysisDone: '🔍', DraftReady: '📝', HitlPending: '⏳', Approved: '✅',
  Rejected: '❌', ReplySent: '📤', AwaitingRevision: '🔄', FollowupDone: '✔️',
};

interface Props { emailId: string | null; }

function formatDuration(d: any): string {
  if (d === null || d === undefined) return '';
  if (typeof d === 'number') return `${(d / 1000).toFixed(1)}s`;
  // TimeSpan string format: "00:00:23.1636578"
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

export default function PipelineTimelinePanel({ emailId }: Props) {
  const [timeline, setTimeline] = useState<any[]>([]);
  const [state, setState] = useState('');

  useEffect(() => {
    if (!emailId) return;
    const id = setInterval(async () => {
      try {
        const res = await getPipeline(emailId);
        setTimeline(res.timeline);
        setState(res.state);
      } catch { /* ignore */ }
    }, 2000);
    return () => clearInterval(id);
  }, [emailId]);

  return (
    <div>
      <h3 style={{ marginBottom: 12 }}>📋 Timeline Pipeline</h3>

      {!emailId && (
        <p style={{ color: 'var(--text2)', fontSize: 12 }}>
          Invia un'email per vedere la timeline in tempo reale.
        </p>
      )}

      {state && (
        <div className="current-state">
          Stato: <strong>{state}</strong>
        </div>
      )}

      <div className="timeline">
        {timeline.map((entry, i) => (
          <div key={i} className={`tl-item ${entry.state === state ? 'active' : ''}`}>
            <div className="tl-icon">{STATE_ICONS[entry.state] || '●'}</div>
            <div className="tl-content">
              <div className="tl-phase">{entry.phase}</div>
              <div className="tl-desc">{entry.description}</div>
              {entry.duration != null && <div className="tl-dur">⏱ {formatDuration(entry.duration)}</div>}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

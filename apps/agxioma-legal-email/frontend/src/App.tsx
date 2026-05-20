import { useState, useEffect, useCallback } from 'react';
import EmailInboxPanel from './EmailInboxPanel';
import PipelineTimelinePanel from './PipelineTimelinePanel';
import HITLReviewPanel from './HITLReviewPanel';
import LegalQueryPanel from './LegalQueryPanel';
import DashboardPanel from './DashboardPanel';
import { createHubConnection, type PipelineEntry } from './api';

type Tab = 'inbox' | 'hitl' | 'query' | 'dashboard';

export default function App() {
  const [tab, setTab] = useState<Tab>('inbox');
  const [selectedEmailId, setSelectedEmailId] = useState<string | null>(null);
  const [pendingIds, setPendingIds] = useState<string[]>([]);

  // SignalR: receive real-time pipeline updates → switch to HITL tab when ready
  const onStateChanged = useCallback((_emailId: string, _state: string, _timeline: PipelineEntry[]) => {
    // timeline panel polls every 2s, but this keeps the connection warm
  }, []);

  useEffect(() => {
    const conn = createHubConnection(onStateChanged);
    return () => { conn.stop(); };
  }, [onStateChanged]);

  function handleEmailSent(id: string | null) {
    if (!id) return;
    setSelectedEmailId(id);
    setPendingIds(prev => [...prev, id]);
    // Use polling to detect when pipeline reaches HITL_PENDING
    const poll = setInterval(async () => {
      try {
        const r = await fetch(`/api/email/${id}/pipeline`);
        const data = await r.json();
        if (data.state === 'HitlPending' || data.state === 'DraftReady') {
          clearInterval(poll);
          setPendingIds(prev => prev.filter(x => x !== id));
          setTab('hitl');
        }
      } catch { /* ignore */ }
    }, 2000);
  }

  return (
    <div className="layout">
      <header className="topbar">
        <h1>AGXIOME</h1>
        <span className="badge">Studio Legale Demo</span>
        <nav>
          <a href="#" className={tab === 'inbox' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('inbox'); }}>📥 Email</a>
          <a href="#" className={tab === 'hitl' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('hitl'); }}>👩‍⚖️ Revisioni</a>
          <a href="#" className={tab === 'query' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('query'); }}>🔍 Codici</a>
          <a href="#" className={tab === 'dashboard' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('dashboard'); }}>📊 Dashboard</a>
        </nav>
      </header>
      <div className="main-layout">
        <div className="main-content">
          {tab === 'inbox' && <EmailInboxPanel onSelectEmail={handleEmailSent} />}
          {tab === 'hitl' && <HITLReviewPanel emailId={selectedEmailId} />}
          {tab === 'query' && <LegalQueryPanel />}
          {tab === 'dashboard' && <DashboardPanel />}
        </div>
        <aside className="dx-sidebar">
          <PipelineTimelinePanel emailId={selectedEmailId} />
        </aside>
      </div>
    </div>
  );
}

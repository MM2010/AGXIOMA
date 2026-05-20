import { useState, useEffect } from 'react';
import EmailInboxPanel from './EmailInboxPanel';
import PipelineTimelinePanel from './PipelineTimelinePanel';
import HITLReviewPanel from './HITLReviewPanel';
import LegalQueryPanel from './LegalQueryPanel';
import DashboardPanel from './DashboardPanel';
import { useRealtimePipeline } from './useRealtimePipeline';

type Tab = 'inbox' | 'hitl' | 'query' | 'dashboard';

export default function App() {
  const [tab, setTab] = useState<Tab>('inbox');
  const [selectedEmailId, setSelectedEmailId] = useState<string | null>(null);
  const { state } = useRealtimePipeline(selectedEmailId);

  // When pipeline completes → auto-navigate to HITL tab
  useEffect(() => {
    if (state === 'HitlPending' || state === 'DraftReady') {
      setTab('hitl');
    }
  }, [state]);

  function handleEmailSent(id: string | null) {
    if (!id) return;
    setSelectedEmailId(id);
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

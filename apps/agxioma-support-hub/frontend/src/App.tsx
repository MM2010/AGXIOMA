import { useState } from 'react';
import SupportPanel from './SupportPanel';
import AnalyzePanel from './AnalyzePanel';
import BackofficePanel from './BackofficePanel';

type Tab = 'chat' | 'analyze' | 'backoffice';

export default function App() {
  const [tab, setTab] = useState<Tab>('chat');

  return (
    <div className="layout">
      <header className="topbar">
        <h1>AGXIOME</h1>
        <span className="badge">Support Hub Demo</span>
        <nav>
          <a
            href="#"
            className={tab === 'chat' ? 'active' : ''}
            onClick={(e) => { e.preventDefault(); setTab('chat'); }}
          >
            💬 Chat Supporto
          </a>
          <a
            href="#"
            className={tab === 'analyze' ? 'active' : ''}
            onClick={(e) => { e.preventDefault(); setTab('analyze'); }}
          >
            🔍 Analisi Deep
          </a>
          <a
            href="#"
            className={tab === 'backoffice' ? 'active' : ''}
            onClick={(e) => { e.preventDefault(); setTab('backoffice'); }}
          >
            📊 Backoffice
          </a>
        </nav>
      </header>
      <div className="main">
        {tab === 'chat' && <SupportPanel />}
        {tab === 'analyze' && <AnalyzePanel />}
        {tab === 'backoffice' && <BackofficePanel />}
      </div>
    </div>
  );
}

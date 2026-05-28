import { useState, useEffect, useRef, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { getPipeline } from './api';
import type { PipelineEntry } from './api';

type Transport = 'signalr' | 'sse' | 'polling' | 'none';

interface ActiveEmail {
  emailId: string;
  state: string;
  timeline: PipelineEntry[];
}

type Listener = (update: ActiveEmail) => void;

// ── Global SignalR connection (singleton across the app) ───────────────

let _globalConn: signalR.HubConnection | null = null;
let _globalTransport: Transport = 'none';
let _globalListeners = new Set<Listener>();
let _globalPollTimers = new Map<string, ReturnType<typeof setInterval>>();
let _globalSseSource: EventSource | null = null;

function notifyListeners(emailId: string, state: string, timeline: PipelineEntry[]) {
  const update: ActiveEmail = { emailId, state, timeline };
  _globalListeners.forEach(fn => fn(update));
}

// ── SignalR setup ─────────────────────────────────────────────────────

function startSignalR() {
  if (_globalConn) return;

  _globalConn = new signalR.HubConnectionBuilder()
    .withUrl('/hub/pipeline')
    .withAutomaticReconnect()
    .build();

  _globalConn.on('PipelineStateChanged', (emailId: string, state: string, timeline: PipelineEntry[]) => {
    // SignalR is working → stop any fallback for this email
    stopFallback(emailId);
    _globalTransport = 'signalr';
    notifyListeners(emailId, state, timeline);
  });

  _globalConn.onreconnecting(() => { /* will fall back to SSE/polling */ });
  _globalConn.onreconnected(() => {
    _globalTransport = 'signalr';
    // Reconnected → stop all fallbacks
    stopAllFallbacks();
  });
  _globalConn.onclose(() => {
    // SignalR permanently closed → start SSE fallback
    if (_globalTransport === 'signalr') {
      _globalTransport = 'none';
      startSSE();
    }
  });

  _globalConn.start()
    .then(() => { _globalTransport = 'signalr'; })
    .catch(() => {
      // SignalR failed to start → fall back
      startSSE();
    });
}

// ── SSE fallback ──────────────────────────────────────────────────────

function startSSE() {
  if (_globalSseSource) return;

  try {
    const source = new EventSource('/api/events/pipeline');
    _globalSseSource = source;
    _globalTransport = 'sse';

    source.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data) as ActiveEmail;
        // SSE is working → stop polling for this email
        stopFallback(data.emailId);
        notifyListeners(data.emailId, data.state, data.timeline);
      } catch { /* ignore malformed */ }
    };

    source.onerror = () => {
      // SSE failed → fall back to polling globally
      source.close();
      _globalSseSource = null;
      if (_globalTransport === 'sse') {
        _globalTransport = 'polling';
      }
    };
  } catch {
    _globalTransport = 'polling';
  }
}

// ── Polling fallback (per-email) ──────────────────────────────────────

function startPolling(emailId: string) {
  if (_globalPollTimers.has(emailId)) return;

  const timer = setInterval(async () => {
    try {
      const res = await getPipeline(emailId);
      if (_globalTransport === 'none') _globalTransport = 'polling';
      notifyListeners(emailId, res.state, res.timeline);
    } catch { /* ignore */ }
  }, 2000);

  _globalPollTimers.set(emailId, timer);
}

function stopFallback(emailId: string) {
  const timer = _globalPollTimers.get(emailId);
  if (timer) {
    clearInterval(timer);
    _globalPollTimers.delete(emailId);
  }
}

function stopAllFallbacks() {
  _globalPollTimers.forEach(t => clearInterval(t));
  _globalPollTimers.clear();
  if (_globalSseSource) {
    _globalSseSource.close();
    _globalSseSource = null;
  }
}

// ── Hook ──────────────────────────────────────────────────────────────

/**
 * React hook that subscribes to real-time pipeline updates for a given emailId.
 *
 * Transport priority: SignalR → SSE → polling
 * - SignalR: persistent WebSocket, primary channel
 * - SSE: Server-Sent Events, automatic fallback
 * - Polling: last resort, 2s interval
 *
 * When a higher-priority transport recovers, lower ones are automatically stopped.
 */
export function useRealtimePipeline(emailId: string | null) {
  const [state, setState] = useState<string>('');
  const [timeline, setTimeline] = useState<PipelineEntry[]>([]);
  const [transport, setTransport] = useState<Transport>('none');
  const lastEmailRef = useRef<string | null>(null);

  const listener = useCallback((update: ActiveEmail) => {
    if (update.emailId === emailId) {
      setState(update.state);
      setTimeline(update.timeline);
    }
  }, [emailId]);

  // Subscribe/unsubscribe to global listener
  useEffect(() => {
    if (!emailId) {
      setState('');
      setTimeline([]);
      return;
    }

    _globalListeners.add(listener);

    // If we just started tracking a new email and are on polling, start its timer
    if (_globalTransport === 'polling' || _globalTransport === 'none') {
      startPolling(emailId);
    }

    return () => {
      _globalListeners.delete(listener);
      stopFallback(emailId);
    };
  }, [emailId, listener]);

  // Bootstrap SignalR once
  useEffect(() => {
    startSignalR();
    return () => {
      // Cleanup on unmount (app closes)
      stopAllFallbacks();
      _globalListeners.clear();
    };
  }, []);

  // Expose current transport for debugging
  useEffect(() => {
    const id = setInterval(() => {
      setTransport(_globalTransport);
    }, 1000);
    return () => clearInterval(id);
  }, []);

  return { state, timeline, transport };
}

/** Get a human-readable label for the current transport */
export function transportLabel(t: Transport): string {
  switch (t) {
    case 'signalr': return '⚡ real-time';
    case 'sse':     return '📡 SSE';
    case 'polling': return '🔄 polling';
    default:        return '○';
  }
}

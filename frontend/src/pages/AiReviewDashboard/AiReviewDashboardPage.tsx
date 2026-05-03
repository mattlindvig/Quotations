import { useEffect, useState, useCallback } from 'react';
import apiClient from '../../services/apiClient';

interface AiReviewStats {
  counts: Record<string, number>;
  total: number;
  averageScores: {
    quoteAccuracy: number | null;
    attribution: number | null;
    source: number | null;
  };
  errorCount: number;
}

interface RecentReview {
  quotationId: string;
  text: string;
  authorName: string;
  reviewedAt: string;
  modelUsed: string;
  summary: string | null;
  scores: {
    quoteAccuracy: number | null;
    attribution: number | null;
    source: number | null;
  };
}

interface AiReviewError {
  quotationId: string;
  text: string;
  authorName: string;
  lastError: string;
  retryCount: number;
  failedAt: string;
}

const STATUS_CONFIG: Record<string, { label: string; color: string; bg: string }> = {
  NotReviewed: { label: 'Not Reviewed', color: '#6c757d', bg: '#f8f9fa' },
  Pending:     { label: 'Pending',      color: '#fd7e14', bg: '#fff3cd' },
  InProgress:  { label: 'In Progress',  color: '#0d6efd', bg: '#cfe2ff' },
  Reviewed:    { label: 'Reviewed',     color: '#198754', bg: '#d1e7dd' },
  Failed:      { label: 'Failed',       color: '#dc3545', bg: '#f8d7da' },
};

function ScoreBadge({ score }: { score: number | null }) {
  if (score === null || score === undefined) return <span style={{ color: '#adb5bd' }}>—</span>;
  const color = score >= 8 ? '#198754' : score >= 5 ? '#fd7e14' : '#dc3545';
  return <span style={{ fontWeight: 600, color }}>{score}/10</span>;
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString();
}

export default function AiReviewDashboardPage() {
  const [stats, setStats] = useState<AiReviewStats | null>(null);
  const [recent, setRecent] = useState<RecentReview[]>([]);
  const [errors, setErrors] = useState<AiReviewError[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [autoProcessing, setAutoProcessing] = useState(true);
  const [togglingAuto, setTogglingAuto] = useState(false);
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [statsRes, recentRes, errorsRes, settingsRes] = await Promise.all([
        apiClient.get<any>('/ai-review/stats'),
        apiClient.get<any>('/ai-review/recent?limit=20'),
        apiClient.get<any>('/ai-review/errors?page=1&pageSize=20'),
        apiClient.get<any>('/ai-review/settings'),
      ]);
      setStats(statsRes.data);
      setRecent(recentRes.data);
      setErrors(errorsRes.data.items);
      setAutoProcessing(settingsRes.data.autoProcessingEnabled);
    } catch (e: any) {
      setError(e.message || 'Failed to load AI review data');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const toggleAutoProcessing = useCallback(async () => {
    setTogglingAuto(true);
    try {
      const res = await apiClient.post<any>('/ai-review/settings/auto-processing', {
        enabled: !autoProcessing,
      });
      setAutoProcessing(res.data.autoProcessingEnabled);
    } finally {
      setTogglingAuto(false);
    }
  }, [autoProcessing]);

  const processNow = useCallback(async (quotationId: string) => {
    setActionInProgress(quotationId);
    try {
      await apiClient.post(`/ai-review/process/${quotationId}`);
      await load();
    } finally {
      setActionInProgress(null);
    }
  }, [load]);

  const requeueOne = useCallback(async (quotationId: string) => {
    setActionInProgress(`requeue-${quotationId}`);
    try {
      await apiClient.post(`/ai-review/errors/${quotationId}/requeue`);
      await load();
    } finally {
      setActionInProgress(null);
    }
  }, [load]);

  const requeueAll = useCallback(async () => {
    setActionInProgress('all');
    try {
      await apiClient.post('/ai-review/errors/requeue-all');
      await load();
    } finally {
      setActionInProgress(null);
    }
  }, [load]);

  if (loading) return <div style={{ padding: '2rem', textAlign: 'center' }}>Loading…</div>;
  if (error) return <div style={{ padding: '2rem', color: '#dc3545' }}>Error: {error}</div>;
  if (!stats) return null;

  const reviewed = stats.counts['Reviewed'] ?? 0;
  const progressPct = stats.total > 0 ? Math.round((reviewed / stats.total) * 100) : 0;
  const busy = actionInProgress !== null || togglingAuto;

  return (
    <div style={{ maxWidth: '1100px', margin: '0 auto', padding: '2rem' }}>

      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '2rem', gap: '1rem', flexWrap: 'wrap' }}>
        <div>
          <h1 style={{ margin: 0 }}>AI Review Dashboard</h1>
          <p style={{ margin: '0.25rem 0 0', color: '#6c757d' }}>
            Background AI review status and activity log
          </p>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', flexWrap: 'wrap' }}>
          {/* Auto-processing toggle */}
          <div style={{
            display: 'flex', alignItems: 'center', gap: '0.6rem',
            padding: '0.5rem 0.85rem',
            border: '1px solid #dee2e6',
            borderRadius: '6px',
            background: '#fff',
          }}>
            <span style={{ fontSize: '0.85rem', color: '#495057', whiteSpace: 'nowrap' }}>
              Auto-processing
            </span>
            <button
              onClick={toggleAutoProcessing}
              disabled={togglingAuto}
              title={autoProcessing ? 'Click to disable auto-processing' : 'Click to enable auto-processing'}
              style={{
                position: 'relative',
                width: '40px',
                height: '22px',
                borderRadius: '11px',
                border: 'none',
                background: autoProcessing ? '#198754' : '#adb5bd',
                cursor: togglingAuto ? 'not-allowed' : 'pointer',
                transition: 'background 0.2s',
                padding: 0,
                flexShrink: 0,
              }}
            >
              <span style={{
                position: 'absolute',
                top: '3px',
                left: autoProcessing ? '21px' : '3px',
                width: '16px',
                height: '16px',
                borderRadius: '50%',
                background: '#fff',
                transition: 'left 0.2s',
              }} />
            </button>
            <span style={{
              fontSize: '0.8rem',
              fontWeight: 600,
              color: autoProcessing ? '#198754' : '#6c757d',
              minWidth: '36px',
            }}>
              {autoProcessing ? 'On' : 'Off'}
            </span>
          </div>

          <button onClick={load} disabled={busy} style={{ padding: '0.5rem 1rem', cursor: busy ? 'not-allowed' : 'pointer' }}>
            Refresh
          </button>
        </div>
      </div>

      {/* Status cards */}
      <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap', marginBottom: '1.5rem' }}>
        {Object.entries(STATUS_CONFIG).map(([key, cfg]) => (
          <div key={key} style={{
            flex: '1 1 140px',
            padding: '1rem',
            borderRadius: '8px',
            background: cfg.bg,
            border: `1px solid ${cfg.color}33`,
            textAlign: 'center',
          }}>
            <div style={{ fontSize: '1.75rem', fontWeight: 700, color: cfg.color }}>
              {(stats.counts[key] ?? 0).toLocaleString()}
            </div>
            <div style={{ fontSize: '0.85rem', color: cfg.color, marginTop: '0.25rem' }}>
              {cfg.label}
            </div>
          </div>
        ))}
        <div style={{
          flex: '1 1 140px',
          padding: '1rem',
          borderRadius: '8px',
          background: '#f8f9fa',
          border: '1px solid #dee2e6',
          textAlign: 'center',
        }}>
          <div style={{ fontSize: '1.75rem', fontWeight: 700 }}>{stats.total.toLocaleString()}</div>
          <div style={{ fontSize: '0.85rem', color: '#6c757d', marginTop: '0.25rem' }}>Total</div>
        </div>
      </div>

      {/* Progress bar */}
      <div style={{ marginBottom: '2rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85rem', color: '#6c757d', marginBottom: '0.25rem' }}>
          <span>Review progress</span>
          <span>{reviewed.toLocaleString()} of {stats.total.toLocaleString()} reviewed ({progressPct}%)</span>
        </div>
        <div style={{ height: '8px', background: '#dee2e6', borderRadius: '4px', overflow: 'hidden' }}>
          <div style={{ height: '100%', width: `${progressPct}%`, background: '#198754', borderRadius: '4px', transition: 'width 0.3s' }} />
        </div>
      </div>

      {/* Average scores */}
      {stats.averageScores.quoteAccuracy !== null && (
        <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap', marginBottom: '2rem' }}>
          {[
            { label: 'Avg Quote Accuracy', value: stats.averageScores.quoteAccuracy },
            { label: 'Avg Attribution',    value: stats.averageScores.attribution },
            { label: 'Avg Source',         value: stats.averageScores.source },
          ].map(({ label, value }) => (
            <div key={label} style={{
              flex: '1 1 180px',
              padding: '1rem',
              borderRadius: '8px',
              background: '#fff',
              border: '1px solid #dee2e6',
              textAlign: 'center',
            }}>
              <div style={{ fontSize: '1.5rem', fontWeight: 700 }}>
                <ScoreBadge score={value !== null ? Math.round(value!) : null} />
              </div>
              <div style={{ fontSize: '0.8rem', color: '#6c757d', marginTop: '0.25rem' }}>{label}</div>
            </div>
          ))}
        </div>
      )}

      {/* Recent reviews */}
      <h2 style={{ marginBottom: '0.75rem' }}>Recent Reviews</h2>
      {recent.length === 0 ? (
        <p style={{ color: '#6c757d' }}>No reviews completed yet.</p>
      ) : (
        <div style={{ overflowX: 'auto', marginBottom: '2rem' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.9rem' }}>
            <thead>
              <tr style={{ background: '#f8f9fa', borderBottom: '2px solid #dee2e6' }}>
                <th style={th}>Quote</th>
                <th style={th}>Author</th>
                <th style={{ ...th, textAlign: 'center' }}>Quote</th>
                <th style={{ ...th, textAlign: 'center' }}>Attr.</th>
                <th style={{ ...th, textAlign: 'center' }}>Source</th>
                <th style={th}>Reviewed At</th>
                <th style={th}></th>
              </tr>
            </thead>
            <tbody>
              {recent.map((r) => (
                <tr key={r.quotationId} style={{ borderBottom: '1px solid #dee2e6' }}>
                  <td style={{ ...td, maxWidth: '300px', color: '#495057' }}>{r.text}</td>
                  <td style={td}>{r.authorName}</td>
                  <td style={{ ...td, textAlign: 'center' }}><ScoreBadge score={r.scores.quoteAccuracy} /></td>
                  <td style={{ ...td, textAlign: 'center' }}><ScoreBadge score={r.scores.attribution} /></td>
                  <td style={{ ...td, textAlign: 'center' }}><ScoreBadge score={r.scores.source} /></td>
                  <td style={{ ...td, whiteSpace: 'nowrap', color: '#6c757d' }}>{formatDate(r.reviewedAt)}</td>
                  <td style={td}>
                    <button
                      onClick={() => processNow(r.quotationId)}
                      disabled={busy}
                      title="Re-run AI review immediately"
                      style={smallBtn('#0d6efd', busy)}
                    >
                      {actionInProgress === r.quotationId ? '…' : 'Re-run'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Error log */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', marginBottom: '0.75rem' }}>
        <h2 style={{ margin: 0 }}>
          Error Log
          {stats.errorCount > 0 && (
            <span style={{
              marginLeft: '0.5rem',
              fontSize: '0.8rem',
              background: '#f8d7da',
              color: '#dc3545',
              padding: '0.2rem 0.5rem',
              borderRadius: '12px',
            }}>
              {stats.errorCount}
            </span>
          )}
        </h2>
        {errors.length > 0 && (
          <button
            onClick={requeueAll}
            disabled={busy}
            style={smallBtn('#dc3545', busy)}
          >
            {actionInProgress === 'all' ? 'Requeueing…' : 'Requeue All'}
          </button>
        )}
      </div>
      {errors.length === 0 ? (
        <p style={{ color: '#6c757d' }}>No errors recorded.</p>
      ) : (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.9rem' }}>
            <thead>
              <tr style={{ background: '#f8f9fa', borderBottom: '2px solid #dee2e6' }}>
                <th style={th}>Quote</th>
                <th style={th}>Author</th>
                <th style={th}>Error</th>
                <th style={{ ...th, textAlign: 'center' }}>Retries</th>
                <th style={th}>Failed At</th>
                <th style={th}></th>
              </tr>
            </thead>
            <tbody>
              {errors.map((e, i) => (
                <tr key={i} style={{ borderBottom: '1px solid #dee2e6', background: '#fff5f5' }}>
                  <td style={{ ...td, maxWidth: '250px' }}>{e.text}</td>
                  <td style={td}>{e.authorName}</td>
                  <td style={{ ...td, color: '#dc3545', maxWidth: '300px', fontFamily: 'monospace', fontSize: '0.8rem' }}>{e.lastError}</td>
                  <td style={{ ...td, textAlign: 'center' }}>{e.retryCount}</td>
                  <td style={{ ...td, whiteSpace: 'nowrap', color: '#6c757d' }}>{formatDate(e.failedAt)}</td>
                  <td style={{ ...td, whiteSpace: 'nowrap' }}>
                    <div style={{ display: 'flex', gap: '0.4rem' }}>
                      <button
                        onClick={() => processNow(e.quotationId)}
                        disabled={busy}
                        title="Process this quotation immediately"
                        style={smallBtn('#198754', busy)}
                      >
                        {actionInProgress === e.quotationId ? '…' : 'Process Now'}
                      </button>
                      <button
                        onClick={() => requeueOne(e.quotationId)}
                        disabled={busy}
                        title="Reset and add back to the automatic queue"
                        style={smallBtn('#6c757d', busy)}
                      >
                        {actionInProgress === `requeue-${e.quotationId}` ? '…' : 'Requeue'}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

const smallBtn = (color: string, disabled: boolean): React.CSSProperties => ({
  padding: '0.25rem 0.6rem',
  fontSize: '0.8rem',
  background: color,
  color: '#fff',
  border: 'none',
  borderRadius: '4px',
  cursor: disabled ? 'not-allowed' : 'pointer',
  opacity: disabled ? 0.65 : 1,
  whiteSpace: 'nowrap',
});

const th: React.CSSProperties = {
  padding: '0.6rem 0.75rem',
  textAlign: 'left',
  fontWeight: 600,
  fontSize: '0.8rem',
  color: '#495057',
};

const td: React.CSSProperties = {
  padding: '0.6rem 0.75rem',
  verticalAlign: 'top',
};

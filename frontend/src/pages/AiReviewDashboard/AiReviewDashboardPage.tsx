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
  aiChangesApplied: boolean;
  scores: {
    quoteAccuracy: number | null;
    attribution: number | null;
    source: number | null;
  };
}

interface UnreviewedItem {
  quotationId: string;
  text: string;
  authorName: string;
  sourcTitle: string;
  submittedAt: string;
  status: string;
}

interface ScoreDetail {
  score: number;
  reasoning: string;
  suggestedValue: string | null;
  suggestionConfidence: number | null;
}

interface AiRevisionChange {
  field: string;
  previousValue: string;
  newValue: string;
  reasoning: string;
  confidence: number;
}

interface AiRevision {
  appliedAt: string;
  modelUsed: string;
  changes: AiRevisionChange[];
}

interface AuthenticityMeta {
  isLikelyAuthentic: boolean | null;
  reasoning: string | null;
  approximateEra: string | null;
  knownVariants: string[];
}

interface ReviewDetail {
  quotationId: string;
  text: string;
  authorName: string;
  sourceTitle: string;
  originalText: string | null;
  originalAuthorName: string | null;
  originalSourceTitle: string | null;
  tags: string[];
  modelUsed: string | null;
  reviewedAt: string | null;
  summary: string | null;
  suggestedTags: string[];
  authenticity: AuthenticityMeta | null;
  scores: {
    quoteAccuracy: ScoreDetail | null;
    attribution: ScoreDetail | null;
    source: ScoreDetail | null;
  } | null;
  revisions: AiRevision[];
}

interface AiReviewError {
  quotationId: string;
  text: string;
  authorName: string;
  lastError: string;
  retryCount: number;
  failedAt: string;
}

interface Pagination {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
  hasPrevious: boolean;
}

type Tab = 'overview' | 'unreviewed' | 'recent' | 'errors';

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

function PaginationBar({ pagination, onPage }: { pagination: Pagination; onPage: (p: number) => void }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginTop: '1rem', fontSize: '0.85rem' }}>
      <button
        onClick={() => onPage(pagination.page - 1)}
        disabled={!pagination.hasPrevious}
        style={pageBtn(!pagination.hasPrevious)}
      >
        ← Prev
      </button>
      <span style={{ color: '#6c757d' }}>
        Page {pagination.page} of {pagination.totalPages} &nbsp;·&nbsp; {pagination.totalCount.toLocaleString()} total
      </span>
      <button
        onClick={() => onPage(pagination.page + 1)}
        disabled={!pagination.hasNext}
        style={pageBtn(!pagination.hasNext)}
      >
        Next →
      </button>
    </div>
  );
}

export default function AiReviewDashboardPage() {
  const [tab, setTab] = useState<Tab>('overview');
  const [stats, setStats] = useState<AiReviewStats | null>(null);
  const [recent, setRecent] = useState<RecentReview[]>([]);
  const [errors, setErrors] = useState<AiReviewError[]>([]);
  const [unreviewed, setUnreviewed] = useState<UnreviewedItem[]>([]);
  const [unreviewedPagination, setUnreviewedPagination] = useState<Pagination | null>(null);
  const [unreviewedPage, setUnreviewedPage] = useState(1);
  const [errorPagination, setErrorPagination] = useState<Pagination | null>(null);
  const [errorPage, setErrorPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [autoProcessing, setAutoProcessing] = useState(false);
  const [togglingAuto, setTogglingAuto] = useState(false);
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [detailCache, setDetailCache] = useState<Record<string, ReviewDetail>>({});
  const [detailLoading, setDetailLoading] = useState<string | null>(null);

  const loadStats = useCallback(async () => {
    const [statsRes, settingsRes] = await Promise.all([
      apiClient.get<any>('/ai-review/stats'),
      apiClient.get<any>('/ai-review/settings'),
    ]);
    setStats(statsRes.data);
    setAutoProcessing(settingsRes.data.autoProcessingEnabled);
  }, []);

  const loadRecent = useCallback(async () => {
    const res = await apiClient.get<any>('/ai-review/recent?limit=20');
    setRecent(res.data);
  }, []);

  const loadErrors = useCallback(async (page = errorPage) => {
    const res = await apiClient.get<any>(`/ai-review/errors?page=${page}&pageSize=20`);
    setErrors(res.data.items);
    setErrorPagination(res.data.pagination);
  }, [errorPage]);

  const loadUnreviewed = useCallback(async (page = unreviewedPage) => {
    const res = await apiClient.get<any>(`/ai-review/unreviewed?page=${page}&pageSize=20`);
    setUnreviewed(res.data.items);
    setUnreviewedPagination(res.data.pagination);
  }, [unreviewedPage]);

  const loadAll = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      await Promise.all([loadStats(), loadRecent(), loadErrors(1), loadUnreviewed(1)]);
    } catch (e: any) {
      setError(e.message || 'Failed to load AI review data');
    } finally {
      setLoading(false);
    }
  }, [loadStats, loadRecent, loadErrors, loadUnreviewed]);

  useEffect(() => { loadAll(); }, []);

  // Reload unreviewed when page changes
  useEffect(() => {
    if (!loading) loadUnreviewed(unreviewedPage);
  }, [unreviewedPage]);

  useEffect(() => {
    if (!loading) loadErrors(errorPage);
  }, [errorPage]);

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
      // Refresh whichever lists are currently visible
      await Promise.all([loadStats(), loadUnreviewed(unreviewedPage), loadRecent()]);
    } finally {
      setActionInProgress(null);
    }
  }, [loadStats, loadUnreviewed, loadRecent, unreviewedPage]);

  const requeueOne = useCallback(async (quotationId: string) => {
    setActionInProgress(`requeue-${quotationId}`);
    try {
      await apiClient.post(`/ai-review/errors/${quotationId}/requeue`);
      await Promise.all([loadStats(), loadErrors(errorPage)]);
    } finally {
      setActionInProgress(null);
    }
  }, [loadStats, loadErrors, errorPage]);

  const toggleDetail = useCallback(async (quotationId: string) => {
    if (expandedId === quotationId) {
      setExpandedId(null);
      return;
    }
    setExpandedId(quotationId);
    if (detailCache[quotationId]) return;
    setDetailLoading(quotationId);
    try {
      const res = await apiClient.get<any>(`/ai-review/detail/${quotationId}`);
      setDetailCache(prev => ({ ...prev, [quotationId]: res.data }));
    } finally {
      setDetailLoading(null);
    }
  }, [expandedId, detailCache]);

  const revertLast = useCallback(async (quotationId: string) => {
    setActionInProgress(`revert-${quotationId}`);
    try {
      await apiClient.post(`/ai-review/revisions/${quotationId}/revert-last`);
      setDetailCache(prev => { const next = { ...prev }; delete next[quotationId]; return next; });
      await Promise.all([loadRecent(), loadStats()]);
    } finally {
      setActionInProgress(null);
    }
  }, [loadRecent, loadStats]);

  const requeueAll = useCallback(async () => {
    setActionInProgress('all');
    try {
      await apiClient.post('/ai-review/errors/requeue-all');
      await Promise.all([loadStats(), loadErrors(1)]);
      setErrorPage(1);
    } finally {
      setActionInProgress(null);
    }
  }, [loadStats, loadErrors]);

  if (loading) return <div style={{ padding: '2rem', textAlign: 'center' }}>Loading…</div>;
  if (error) return <div style={{ padding: '2rem', color: '#dc3545' }}>Error: {error}</div>;
  if (!stats) return null;

  const reviewed = stats.counts['Reviewed'] ?? 0;
  const progressPct = stats.total > 0 ? Math.round((reviewed / stats.total) * 100) : 0;
  const busy = actionInProgress !== null || togglingAuto;

  return (
    <div style={{ maxWidth: '1100px', margin: '0 auto', padding: '2rem' }}>

      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '1.5rem', gap: '1rem', flexWrap: 'wrap' }}>
        <div>
          <h1 style={{ margin: 0 }}>AI Review Dashboard</h1>
          <p style={{ margin: '0.25rem 0 0', color: '#6c757d' }}>Background AI review status and activity log</p>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', flexWrap: 'wrap' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem', padding: '0.5rem 0.85rem', border: '1px solid #dee2e6', borderRadius: '6px', background: '#fff' }}>
            <span style={{ fontSize: '0.85rem', color: '#495057', whiteSpace: 'nowrap' }}>Auto-processing</span>
            <button
              onClick={toggleAutoProcessing}
              disabled={togglingAuto}
              title={autoProcessing ? 'Click to disable' : 'Click to enable'}
              style={{ position: 'relative', width: '40px', height: '22px', borderRadius: '11px', border: 'none', background: autoProcessing ? '#198754' : '#adb5bd', cursor: togglingAuto ? 'not-allowed' : 'pointer', transition: 'background 0.2s', padding: 0, flexShrink: 0 }}
            >
              <span style={{ position: 'absolute', top: '3px', left: autoProcessing ? '21px' : '3px', width: '16px', height: '16px', borderRadius: '50%', background: '#fff', transition: 'left 0.2s' }} />
            </button>
            <span style={{ fontSize: '0.8rem', fontWeight: 600, color: autoProcessing ? '#198754' : '#6c757d', minWidth: '36px' }}>
              {autoProcessing ? 'On' : 'Off'}
            </span>
          </div>
          <button onClick={loadAll} disabled={busy} style={{ padding: '0.5rem 1rem', cursor: busy ? 'not-allowed' : 'pointer' }}>
            Refresh
          </button>
        </div>
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', gap: 0, borderBottom: '2px solid #dee2e6', marginBottom: '1.5rem' }}>
        {([
          { key: 'overview', label: 'Overview' },
          { key: 'unreviewed', label: `Unreviewed (${(stats.counts['NotReviewed'] ?? 0).toLocaleString()})` },
          { key: 'recent', label: 'Recent Reviews' },
          { key: 'errors', label: `Errors${stats.errorCount > 0 ? ` (${stats.errorCount})` : ''}` },
        ] as { key: Tab; label: string }[]).map(({ key, label }) => (
          <button
            key={key}
            onClick={() => setTab(key)}
            style={{
              padding: '0.6rem 1.1rem',
              border: 'none',
              borderBottom: tab === key ? '2px solid #0d6efd' : '2px solid transparent',
              marginBottom: '-2px',
              background: 'none',
              cursor: 'pointer',
              fontWeight: tab === key ? 600 : 400,
              color: tab === key ? '#0d6efd' : '#495057',
              fontSize: '0.9rem',
              whiteSpace: 'nowrap',
            }}
          >
            {label}
          </button>
        ))}
      </div>

      {/* Overview tab */}
      {tab === 'overview' && (
        <>
          <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap', marginBottom: '1.5rem' }}>
            {Object.entries(STATUS_CONFIG).map(([key, cfg]) => (
              <div key={key} style={{ flex: '1 1 140px', padding: '1rem', borderRadius: '8px', background: cfg.bg, border: `1px solid ${cfg.color}33`, textAlign: 'center' }}>
                <div style={{ fontSize: '1.75rem', fontWeight: 700, color: cfg.color }}>{(stats.counts[key] ?? 0).toLocaleString()}</div>
                <div style={{ fontSize: '0.85rem', color: cfg.color, marginTop: '0.25rem' }}>{cfg.label}</div>
              </div>
            ))}
            <div style={{ flex: '1 1 140px', padding: '1rem', borderRadius: '8px', background: '#f8f9fa', border: '1px solid #dee2e6', textAlign: 'center' }}>
              <div style={{ fontSize: '1.75rem', fontWeight: 700 }}>{stats.total.toLocaleString()}</div>
              <div style={{ fontSize: '0.85rem', color: '#6c757d', marginTop: '0.25rem' }}>Total</div>
            </div>
          </div>

          <div style={{ marginBottom: '2rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85rem', color: '#6c757d', marginBottom: '0.25rem' }}>
              <span>Review progress</span>
              <span>{reviewed.toLocaleString()} of {stats.total.toLocaleString()} reviewed ({progressPct}%)</span>
            </div>
            <div style={{ height: '8px', background: '#dee2e6', borderRadius: '4px', overflow: 'hidden' }}>
              <div style={{ height: '100%', width: `${progressPct}%`, background: '#198754', borderRadius: '4px', transition: 'width 0.3s' }} />
            </div>
          </div>

          {stats.averageScores.quoteAccuracy !== null && (
            <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap' }}>
              {[
                { label: 'Avg Quote Accuracy', value: stats.averageScores.quoteAccuracy },
                { label: 'Avg Attribution',    value: stats.averageScores.attribution },
                { label: 'Avg Source',         value: stats.averageScores.source },
              ].map(({ label, value }) => (
                <div key={label} style={{ flex: '1 1 180px', padding: '1rem', borderRadius: '8px', background: '#fff', border: '1px solid #dee2e6', textAlign: 'center' }}>
                  <div style={{ fontSize: '1.5rem', fontWeight: 700 }}>
                    <ScoreBadge score={value !== null ? Math.round(value!) : null} />
                  </div>
                  <div style={{ fontSize: '0.8rem', color: '#6c757d', marginTop: '0.25rem' }}>{label}</div>
                </div>
              ))}
            </div>
          )}
        </>
      )}

      {/* Unreviewed tab */}
      {tab === 'unreviewed' && (
        <>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
            <p style={{ margin: 0, color: '#6c757d', fontSize: '0.9rem' }}>
              Quotes that have not yet been processed by the AI. Click <strong>Process Now</strong> to run a review immediately.
            </p>
          </div>

          {unreviewed.length === 0 ? (
            <p style={{ color: '#6c757d' }}>No unreviewed quotations — all caught up!</p>
          ) : (
            <>
              <div style={{ overflowX: 'auto' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.9rem' }}>
                  <thead>
                    <tr style={{ background: '#f8f9fa', borderBottom: '2px solid #dee2e6' }}>
                      <th style={th}>Quote</th>
                      <th style={th}>Author</th>
                      <th style={th}>Source</th>
                      <th style={th}>Submitted</th>
                      <th style={th}></th>
                    </tr>
                  </thead>
                  <tbody>
                    {unreviewed.map((item) => (
                      <tr key={item.quotationId} style={{ borderBottom: '1px solid #dee2e6' }}>
                        <td style={{ ...td, maxWidth: '340px', color: '#495057', fontStyle: 'italic' }}>"{item.text}"</td>
                        <td style={td}>{item.authorName}</td>
                        <td style={{ ...td, color: '#6c757d' }}>{item.sourcTitle}</td>
                        <td style={{ ...td, whiteSpace: 'nowrap', color: '#6c757d' }}>{new Date(item.submittedAt).toLocaleDateString()}</td>
                        <td style={td}>
                          <button
                            onClick={() => processNow(item.quotationId)}
                            disabled={busy}
                            title="Run AI review on this quote now"
                            style={smallBtn('#198754', busy)}
                          >
                            {actionInProgress === item.quotationId ? 'Processing…' : 'Process Now'}
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {unreviewedPagination && (
                <PaginationBar
                  pagination={unreviewedPagination}
                  onPage={(p) => setUnreviewedPage(p)}
                />
              )}
            </>
          )}
        </>
      )}

      {/* Recent Reviews tab */}
      {tab === 'recent' && (
        <>
          {recent.length === 0 ? (
            <p style={{ color: '#6c757d' }}>No reviews completed yet.</p>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.9rem' }}>
                <thead>
                  <tr style={{ background: '#f8f9fa', borderBottom: '2px solid #dee2e6' }}>
                    <th style={th}>Quote</th>
                    <th style={th}>Author</th>
                    <th style={{ ...th, textAlign: 'center' }}>Accuracy</th>
                    <th style={{ ...th, textAlign: 'center' }}>Attr.</th>
                    <th style={{ ...th, textAlign: 'center' }}>Source</th>
                    <th style={th}>Reviewed At</th>
                    <th style={th}></th>
                  </tr>
                </thead>
                <tbody>
                  {recent.map((r) => {
                    const isExpanded = expandedId === r.quotationId;
                    const detail = detailCache[r.quotationId];
                    const isLoadingDetail = detailLoading === r.quotationId;
                    return (
                      <>
                        <tr
                          key={r.quotationId}
                          style={{ borderBottom: isExpanded ? 'none' : '1px solid #dee2e6', background: isExpanded ? '#f0f4ff' : undefined, cursor: 'pointer' }}
                          onClick={() => toggleDetail(r.quotationId)}
                        >
                          <td style={{ ...td, maxWidth: '300px', color: '#495057' }}>{r.text}</td>
                          <td style={td}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', flexWrap: 'wrap' }}>
                              {r.authorName}
                              {r.aiChangesApplied && (
                                <span style={{ fontSize: '0.7rem', fontWeight: 600, padding: '0.1rem 0.4rem', borderRadius: '10px', background: '#fff3cd', color: '#856404', border: '1px solid #ffc107', whiteSpace: 'nowrap' }}>
                                  AI Modified
                                </span>
                              )}
                            </div>
                          </td>
                          <td style={{ ...td, textAlign: 'center' }}><ScoreBadge score={r.scores.quoteAccuracy} /></td>
                          <td style={{ ...td, textAlign: 'center' }}><ScoreBadge score={r.scores.attribution} /></td>
                          <td style={{ ...td, textAlign: 'center' }}><ScoreBadge score={r.scores.source} /></td>
                          <td style={{ ...td, whiteSpace: 'nowrap', color: '#6c757d' }}>{formatDate(r.reviewedAt)}</td>
                          <td style={td} onClick={e => e.stopPropagation()}>
                            <div style={{ display: 'flex', gap: '0.4rem' }}>
                              <button
                                onClick={() => toggleDetail(r.quotationId)}
                                style={smallBtn(isExpanded ? '#6c757d' : '#495057', false)}
                                title={isExpanded ? 'Collapse' : 'Expand details'}
                              >
                                {isExpanded ? '▲' : '▼'}
                              </button>
                              <button
                                onClick={() => processNow(r.quotationId)}
                                disabled={busy}
                                title="Re-run AI review immediately"
                                style={smallBtn('#0d6efd', busy)}
                              >
                                {actionInProgress === r.quotationId ? '…' : 'Re-run'}
                              </button>
                              {r.aiChangesApplied && (
                                <button
                                  onClick={() => revertLast(r.quotationId)}
                                  disabled={busy}
                                  title="Revert last AI-applied changes"
                                  style={smallBtn('#dc3545', busy)}
                                >
                                  {actionInProgress === `revert-${r.quotationId}` ? '…' : 'Revert'}
                                </button>
                              )}
                            </div>
                          </td>
                        </tr>
                        {isExpanded && (
                          <tr key={`${r.quotationId}-detail`} style={{ borderBottom: '1px solid #dee2e6' }}>
                            <td colSpan={7} style={{ padding: '1rem 1.25rem', background: '#f8f9ff' }}>
                              {isLoadingDetail && <p style={{ color: '#6c757d', margin: 0 }}>Loading…</p>}
                              {detail && <ReviewDetailPanel detail={detail} />}
                            </td>
                          </tr>
                        )}
                      </>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}

      {/* Errors tab */}
      {tab === 'errors' && (
        <>
          <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', marginBottom: '0.75rem' }}>
            {errors.length > 0 && (
              <button onClick={requeueAll} disabled={busy} style={smallBtn('#dc3545', busy)}>
                {actionInProgress === 'all' ? 'Requeueing…' : 'Requeue All'}
              </button>
            )}
          </div>
          {errors.length === 0 ? (
            <p style={{ color: '#6c757d' }}>No errors recorded.</p>
          ) : (
            <>
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
                            <button onClick={() => processNow(e.quotationId)} disabled={busy} style={smallBtn('#198754', busy)}>
                              {actionInProgress === e.quotationId ? '…' : 'Process Now'}
                            </button>
                            <button onClick={() => requeueOne(e.quotationId)} disabled={busy} style={smallBtn('#6c757d', busy)}>
                              {actionInProgress === `requeue-${e.quotationId}` ? '…' : 'Requeue'}
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {errorPagination && (
                <PaginationBar
                  pagination={errorPagination}
                  onPage={(p) => setErrorPage(p)}
                />
              )}
            </>
          )}
        </>
      )}
    </div>
  );
}

function ReviewDetailPanel({ detail }: { detail: ReviewDetail }) {
  const scoreItems = [
    { label: 'Quote Accuracy', data: detail.scores?.quoteAccuracy },
    { label: 'Attribution',    data: detail.scores?.attribution },
    { label: 'Source',         data: detail.scores?.source },
  ];

  const hasOriginalText   = detail.originalText        && detail.originalText        !== detail.text;
  const hasOriginalAuthor = detail.originalAuthorName  && detail.originalAuthorName  !== detail.authorName;
  const hasOriginalSource = detail.originalSourceTitle && detail.originalSourceTitle !== detail.sourceTitle;
  const showOriginals     = hasOriginalText || hasOriginalAuthor || hasOriginalSource;

  const auth = detail.authenticity;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>

      {/* Summary */}
      {detail.summary && (
        <div>
          <div style={detailLabel}>Summary</div>
          <p style={{ margin: 0, color: '#495057', fontSize: '0.88rem' }}>{detail.summary}</p>
        </div>
      )}

      {/* Authenticity */}
      {auth && (
        <div style={{ background: '#fff', border: '1px solid #dee2e6', borderRadius: '6px', padding: '0.75rem' }}>
          <div style={detailLabel}>Authenticity Assessment</div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', alignItems: 'flex-start' }}>
            {auth.isLikelyAuthentic !== null && (
              <span style={{
                padding: '0.2rem 0.65rem',
                borderRadius: '12px',
                fontWeight: 600,
                fontSize: '0.82rem',
                background: auth.isLikelyAuthentic ? '#d1e7dd' : '#f8d7da',
                color: auth.isLikelyAuthentic ? '#0f5132' : '#842029',
                border: `1px solid ${auth.isLikelyAuthentic ? '#a3cfbb' : '#f1aeb5'}`,
                whiteSpace: 'nowrap',
                alignSelf: 'center',
              }}>
                {auth.isLikelyAuthentic ? 'Likely Authentic' : 'Possibly Misattributed'}
              </span>
            )}
            {auth.approximateEra && (
              <span style={{ fontSize: '0.82rem', color: '#495057', alignSelf: 'center' }}>
                <span style={{ fontWeight: 600, color: '#6c757d' }}>Era: </span>{auth.approximateEra}
              </span>
            )}
          </div>
          {auth.reasoning && (
            <p style={{ margin: '0.5rem 0 0', fontSize: '0.82rem', color: '#6c757d' }}>{auth.reasoning}</p>
          )}
          {auth.knownVariants.length > 0 && (
            <div style={{ marginTop: '0.6rem' }}>
              <div style={{ fontSize: '0.75rem', fontWeight: 600, color: '#6c757d', marginBottom: '0.25rem', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Known Variants</div>
              <ul style={{ margin: 0, paddingLeft: '1.2rem' }}>
                {auth.knownVariants.map((v, i) => (
                  <li key={i} style={{ fontSize: '0.82rem', color: '#495057', fontStyle: 'italic', marginBottom: '0.15rem' }}>"{v}"</li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}

      {/* Original vs corrected values */}
      {showOriginals && (
        <div>
          <div style={detailLabel}>Original Submitted Values</div>
          <div style={{ background: '#fff', border: '1px solid #dee2e6', borderRadius: '6px', overflow: 'hidden' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
              <thead>
                <tr style={{ background: '#f8f9fa' }}>
                  <th style={{ ...th, width: '100px' }}>Field</th>
                  <th style={th}>Original</th>
                  <th style={th}>Current (AI-corrected)</th>
                </tr>
              </thead>
              <tbody>
                {hasOriginalText && (
                  <tr style={{ borderTop: '1px solid #dee2e6' }}>
                    <td style={{ ...td, fontWeight: 600, color: '#495057' }}>Text</td>
                    <td style={{ ...td, color: '#6c757d', fontStyle: 'italic', maxWidth: '260px' }}>{detail.originalText}</td>
                    <td style={{ ...td, color: '#198754', maxWidth: '260px' }}>{detail.text}</td>
                  </tr>
                )}
                {hasOriginalAuthor && (
                  <tr style={{ borderTop: '1px solid #dee2e6' }}>
                    <td style={{ ...td, fontWeight: 600, color: '#495057' }}>Author</td>
                    <td style={{ ...td, color: '#6c757d' }}>{detail.originalAuthorName}</td>
                    <td style={{ ...td, color: '#198754' }}>{detail.authorName}</td>
                  </tr>
                )}
                {hasOriginalSource && (
                  <tr style={{ borderTop: '1px solid #dee2e6' }}>
                    <td style={{ ...td, fontWeight: 600, color: '#495057' }}>Source</td>
                    <td style={{ ...td, color: '#6c757d' }}>{detail.originalSourceTitle}</td>
                    <td style={{ ...td, color: '#198754' }}>{detail.sourceTitle}</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Score breakdown */}
      <div>
        <div style={detailLabel}>Score Breakdown</div>
        <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
          {scoreItems.map(({ label, data }) => (
            <div key={label} style={{ flex: '1 1 200px', background: '#fff', border: '1px solid #dee2e6', borderRadius: '6px', padding: '0.75rem' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.4rem' }}>
                <span style={{ fontSize: '0.8rem', fontWeight: 600, color: '#495057' }}>{label}</span>
                <ScoreBadge score={data?.score ?? null} />
              </div>
              {data?.reasoning && (
                <p style={{ margin: 0, fontSize: '0.8rem', color: '#6c757d' }}>{data.reasoning}</p>
              )}
              {data?.suggestedValue && (
                <div style={{ marginTop: '0.5rem', padding: '0.4rem 0.5rem', background: '#fff3cd', borderRadius: '4px', fontSize: '0.8rem' }}>
                  <span style={{ fontWeight: 600, color: '#856404' }}>Suggestion ({data.suggestionConfidence}% confidence): </span>
                  <span style={{ color: '#495057' }}>{data.suggestedValue}</span>
                </div>
              )}
            </div>
          ))}
        </div>
      </div>

      {/* Tags */}
      {detail.suggestedTags.length > 0 && (
        <div>
          <div style={detailLabel}>Suggested Tags</div>
          <div style={{ display: 'flex', gap: '0.4rem', flexWrap: 'wrap' }}>
            {detail.suggestedTags.map(tag => {
              const applied = detail.tags.includes(tag);
              return (
                <span key={tag} style={{ padding: '0.2rem 0.55rem', borderRadius: '12px', fontSize: '0.78rem', fontWeight: 500, background: applied ? '#d1e7dd' : '#e9ecef', color: applied ? '#0f5132' : '#495057', border: `1px solid ${applied ? '#a3cfbb' : '#dee2e6'}` }}>
                  {tag}{applied ? ' ✓' : ''}
                </span>
              );
            })}
          </div>
        </div>
      )}

      {/* Revision history */}
      <div>
        <div style={detailLabel}>AI-Applied Changes</div>
        {detail.revisions.length === 0 ? (
          <p style={{ margin: 0, fontSize: '0.85rem', color: '#6c757d' }}>No changes were applied — all fields met the confidence threshold or had no suggestions.</p>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            {detail.revisions.map((rev, i) => (
              <div key={i} style={{ background: '#fff', border: '1px solid #dee2e6', borderRadius: '6px', overflow: 'hidden' }}>
                <div style={{ padding: '0.4rem 0.75rem', background: '#f8f9fa', borderBottom: '1px solid #dee2e6', fontSize: '0.78rem', color: '#6c757d', display: 'flex', gap: '1rem' }}>
                  <span>{formatDate(rev.appliedAt)}</span>
                  <span>Model: {rev.modelUsed}</span>
                </div>
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.82rem' }}>
                  <thead>
                    <tr style={{ background: '#f8f9fa' }}>
                      <th style={{ ...th, width: '80px' }}>Field</th>
                      <th style={th}>Before</th>
                      <th style={th}>After</th>
                      <th style={th}>Reasoning</th>
                      <th style={{ ...th, width: '80px', textAlign: 'center' }}>Confidence</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rev.changes.map((c, j) => (
                      <tr key={j} style={{ borderTop: '1px solid #dee2e6' }}>
                        <td style={{ ...td, fontWeight: 600, color: '#495057' }}>{c.field}</td>
                        <td style={{ ...td, color: '#dc3545', maxWidth: '200px' }}>{c.previousValue}</td>
                        <td style={{ ...td, color: '#198754', maxWidth: '200px' }}>{c.newValue}</td>
                        <td style={{ ...td, color: '#6c757d', maxWidth: '260px' }}>{c.reasoning}</td>
                        <td style={{ ...td, textAlign: 'center', fontWeight: 600, color: '#0d6efd' }}>{c.confidence}%</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

const detailLabel: React.CSSProperties = {
  fontSize: '0.75rem',
  fontWeight: 700,
  textTransform: 'uppercase',
  letterSpacing: '0.05em',
  color: '#6c757d',
  marginBottom: '0.4rem',
};

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

const pageBtn = (disabled: boolean): React.CSSProperties => ({
  padding: '0.3rem 0.7rem',
  fontSize: '0.82rem',
  background: '#fff',
  color: disabled ? '#adb5bd' : '#0d6efd',
  border: '1px solid',
  borderColor: disabled ? '#dee2e6' : '#0d6efd',
  borderRadius: '4px',
  cursor: disabled ? 'not-allowed' : 'pointer',
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

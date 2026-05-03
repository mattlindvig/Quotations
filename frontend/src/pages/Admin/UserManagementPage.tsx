import React, { useEffect, useState } from 'react';
import { apiClient } from '../../services/apiClient';
import './UserManagementPage.css';

interface UserRow {
  id: string;
  username: string;
  displayName: string;
  email: string;
  roles: string[];
  isActive: boolean;
  createdAt: string;
}

const ALL_ROLES = ['User', 'Reviewer', 'Admin'] as const;

export const UserManagementPage: React.FC = () => {
  const [users, setUsers] = useState<UserRow[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState<Record<string, boolean>>({});
  const [pendingRoles, setPendingRoles] = useState<Record<string, string[]>>({});
  const [savedIds, setSavedIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    loadUsers();
  }, []);

  const loadUsers = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const res = await apiClient.get<{ success: boolean; data: UserRow[] }>('/admin/users');
      setUsers(res.data);
      const initial: Record<string, string[]> = {};
      res.data.forEach((u) => { initial[u.id] = [...u.roles]; });
      setPendingRoles(initial);
    } catch {
      setError('Failed to load users.');
    } finally {
      setIsLoading(false);
    }
  };

  const toggleRole = (userId: string, role: string) => {
    setPendingRoles((prev) => {
      const current = prev[userId] ?? [];
      if (role === 'User') return prev; // User role is always required
      const next = current.includes(role)
        ? current.filter((r) => r !== role)
        : [...current, role];
      return { ...prev, [userId]: next };
    });
    setSavedIds((prev) => { const s = new Set(prev); s.delete(userId); return s; });
  };

  const saveRoles = async (userId: string) => {
    setSaving((prev) => ({ ...prev, [userId]: true }));
    try {
      await apiClient.put(`/admin/users/${userId}/roles`, { roles: pendingRoles[userId] });
      setUsers((prev) =>
        prev.map((u) => u.id === userId ? { ...u, roles: pendingRoles[userId] } : u)
      );
      setSavedIds((prev) => new Set(prev).add(userId));
    } catch {
      setError(`Failed to update roles for user ${userId}.`);
    } finally {
      setSaving((prev) => ({ ...prev, [userId]: false }));
    }
  };

  const isDirty = (userId: string) => {
    const user = users.find((u) => u.id === userId);
    if (!user) return false;
    const original = [...user.roles].sort().join(',');
    const current = [...(pendingRoles[userId] ?? [])].sort().join(',');
    return original !== current;
  };

  if (isLoading) return <div className="admin-loading">Loading users…</div>;

  return (
    <div className="admin-page">
      <div className="admin-header">
        <h1>User Management</h1>
        <p className="admin-subtitle">Assign roles to control access to reviewer and admin features.</p>
      </div>

      {error && <div className="admin-error">{error}</div>}

      <div className="admin-table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>User</th>
              <th>Email</th>
              {ALL_ROLES.map((role) => (
                <th key={role} className="role-col">{role}</th>
              ))}
              <th></th>
            </tr>
          </thead>
          <tbody>
            {users.map((user) => (
              <tr key={user.id} className={savedIds.has(user.id) ? 'row-saved' : ''}>
                <td>
                  <div className="user-name">{user.displayName}</div>
                  <div className="user-sub">{user.username}</div>
                </td>
                <td className="user-email">{user.email}</td>
                {ALL_ROLES.map((role) => (
                  <td key={role} className="role-col">
                    <input
                      type="checkbox"
                      checked={(pendingRoles[user.id] ?? user.roles).includes(role)}
                      disabled={role === 'User'}
                      onChange={() => toggleRole(user.id, role)}
                      aria-label={`${role} role for ${user.username}`}
                    />
                  </td>
                ))}
                <td className="action-col">
                  {savedIds.has(user.id) ? (
                    <span className="saved-badge">Saved</span>
                  ) : (
                    <button
                      className="save-btn"
                      onClick={() => saveRoles(user.id)}
                      disabled={!isDirty(user.id) || saving[user.id]}
                    >
                      {saving[user.id] ? 'Saving…' : 'Save'}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

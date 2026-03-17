import { useEffect, useState } from 'react';
import {
  Users,
  FileText,
  Brain,
  Target,
  ClipboardCheck,
  Shield,
  TrendingUp,
  LayoutDashboard,
} from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Notice } from '@/components/ui/notice';
import { getDashboardStats, getRecentUsers } from '../api/admin-api';
import type { AdminDashboardStats, AdminUser } from '@/types/api';
import type { LucideIcon } from 'lucide-react';

export function AdminDashboardPage() {
  const [stats, setStats] = useState<AdminDashboardStats | null>(null);
  const [recentUsers, setRecentUsers] = useState<AdminUser[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      try {
        const [s, u] = await Promise.all([getDashboardStats(), getRecentUsers(10)]);
        setStats(s);
        setRecentUsers(u);
      } catch {
        setError('Failed to load dashboard stats.');
      } finally {
        setIsLoading(false);
      }
    }
    load();
  }, []);

  if (isLoading) {
    return (
      <div className="flex justify-center py-20">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (error || !stats) {
    return (
      <div className="max-w-5xl mx-auto">
        <Notice variant="error" title={error ?? 'Unknown error'} />
      </div>
    );
  }

  return (
    <div className="max-w-6xl mx-auto space-y-6" data-testid="admin-dashboard">
      <div className="flex items-center gap-3 mb-2">
        <LayoutDashboard size={22} strokeWidth={1.8} className="text-brand-teal-500" />
        <h1 className="text-2xl font-serif text-brand-slate-800">Admin Dashboard</h1>
      </div>

      {/* Top row - Key metrics */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard
          icon={Users}
          label="Total Users"
          value={stats.totalUsers}
          delta={stats.newUsersLast7Days}
          color="teal"
        />
        <StatCard
          icon={Users}
          label="Active Children"
          value={stats.totalChildren}
          color="teal"
        />
        <StatCard
          icon={FileText}
          label="IEP Documents"
          value={stats.totalDocuments}
          delta={stats.newDocumentsLast7Days}
          color="teal"
        />
        <StatCard
          icon={Brain}
          label="AI Analyses"
          value={stats.totalAnalyses}
          delta={stats.analysesLast7Days}
          color="teal"
        />
      </div>

      {/* Second row - Status breakdowns */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <StatusBreakdownCard
          title="Document Status"
          icon={FileText}
          items={[
            { label: 'Created', value: stats.documentsCreated, color: 'bg-brand-slate-300' },
            { label: 'Parsed', value: stats.documentsParsed, color: 'bg-brand-teal-500' },
            { label: 'Error', value: stats.documentsError, color: 'bg-red-400' },
          ]}
          total={stats.totalDocuments}
        />
        <StatusBreakdownCard
          title="Analysis Status"
          icon={Brain}
          items={[
            { label: 'Completed', value: stats.analysesCompleted, color: 'bg-brand-teal-500' },
            { label: 'Pending', value: stats.totalAnalyses - stats.analysesCompleted - stats.analysesError, color: 'bg-brand-amber-400' },
            { label: 'Error', value: stats.analysesError, color: 'bg-red-400' },
          ]}
          total={stats.totalAnalyses}
        />
        <StatusBreakdownCard
          title="Subscription Status"
          icon={Shield}
          items={Object.entries(stats.usersBySubscriptionStatus).map(([label, value]) => ({
            label: label.charAt(0).toUpperCase() + label.slice(1),
            value,
            color: label === 'active' ? 'bg-brand-teal-500' : label === 'none' ? 'bg-brand-slate-300' : 'bg-brand-amber-400',
          }))}
          total={stats.totalUsers}
        />
      </div>

      {/* Third row - Breakdowns */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <BreakdownCard
          title="Children by Disability Category"
          data={stats.childrenByDisabilityCategory}
        />
        <BreakdownCard
          title="Goals by Category"
          data={stats.goalsByCategory}
        />
      </div>

      {/* Fourth row - Recent Activity */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2">
          <RecentUsersTable users={recentUsers} />
        </div>
        <div className="space-y-4">
          <BetaCodesCard total={stats.totalBetaCodes} redeemed={stats.redeemedBetaCodes} />
          <QuickStatsCard
            items={[
              { icon: Target, label: 'Advocacy Goals', value: stats.totalGoals },
              { icon: ClipboardCheck, label: 'Meeting Checklists', value: stats.totalChecklists },
              { icon: TrendingUp, label: 'Total AI Usage', value: stats.totalAnalysisUsage + stats.totalMeetingPrepUsage },
            ]}
          />
        </div>
      </div>
    </div>
  );
}

/* ---------- Stat Card ---------- */

interface StatCardProps {
  icon: LucideIcon;
  label: string;
  value: number;
  delta?: number;
  color: string;
}

function StatCard({ icon: Icon, label, value, delta }: StatCardProps) {
  return (
    <Card data-testid={`admin-stat-${label.toLowerCase().replace(/\s+/g, '-')}`}>
      <div className="flex items-start justify-between">
        <div className="flex items-center justify-center w-10 h-10 rounded-full bg-brand-teal-50">
          <Icon size={20} strokeWidth={1.8} className="text-brand-teal-500" />
        </div>
        {delta !== undefined && delta > 0 && (
          <Badge variant="success">+{delta} this week</Badge>
        )}
      </div>
      <p className="mt-4 text-3xl font-semibold text-brand-slate-800">{value.toLocaleString()}</p>
      <p className="text-sm text-brand-slate-400 mt-1">{label}</p>
    </Card>
  );
}

/* ---------- Status Breakdown Card ---------- */

interface StatusItem {
  label: string;
  value: number;
  color: string;
}

interface StatusBreakdownCardProps {
  title: string;
  icon: LucideIcon;
  items: StatusItem[];
  total: number;
}

function StatusBreakdownCard({ title, icon: Icon, items, total }: StatusBreakdownCardProps) {
  return (
    <Card>
      <div className="flex items-center gap-2 mb-4">
        <Icon size={16} strokeWidth={1.8} className="text-brand-slate-500" />
        <h3 className="text-sm font-medium text-brand-slate-700">{title}</h3>
      </div>

      {/* Bar */}
      <div className="flex h-3 rounded-full overflow-hidden bg-brand-slate-100 mb-3">
        {items.map((item) => {
          const pct = total > 0 ? (item.value / total) * 100 : 0;
          if (pct === 0) return null;
          return (
            <div
              key={item.label}
              className={`${item.color} transition-all`}
              style={{ width: `${pct}%` }}
            />
          );
        })}
      </div>

      <div className="space-y-1.5">
        {items.map((item) => (
          <div key={item.label} className="flex items-center justify-between text-xs">
            <div className="flex items-center gap-2">
              <span className={`w-2.5 h-2.5 rounded-full ${item.color}`} />
              <span className="text-brand-slate-600">{item.label}</span>
            </div>
            <span className="font-medium text-brand-slate-700">{item.value}</span>
          </div>
        ))}
      </div>
    </Card>
  );
}

/* ---------- Breakdown Card (horizontal bars) ---------- */

interface BreakdownCardProps {
  title: string;
  data: Record<string, number>;
}

const barColors = [
  'bg-brand-teal-500',
  'bg-brand-teal-400',
  'bg-brand-amber-400',
  'bg-brand-teal-300',
  'bg-brand-slate-400',
  'bg-brand-teal-200',
];

function BreakdownCard({ title, data }: BreakdownCardProps) {
  const entries = Object.entries(data).sort((a, b) => b[1] - a[1]);
  const max = entries.length > 0 ? entries[0][1] : 1;

  return (
    <Card>
      <h3 className="text-sm font-medium text-brand-slate-700 mb-4">{title}</h3>
      {entries.length === 0 ? (
        <p className="text-xs text-brand-slate-400">No data yet.</p>
      ) : (
        <div className="space-y-3">
          {entries.map(([label, value], i) => (
            <div key={label}>
              <div className="flex justify-between text-xs mb-1">
                <span className="text-brand-slate-600 capitalize">{label.replace(/_/g, ' ')}</span>
                <span className="font-medium text-brand-slate-700">{value}</span>
              </div>
              <div className="h-2 rounded-full bg-brand-slate-100 overflow-hidden">
                <div
                  className={`h-full rounded-full ${barColors[i % barColors.length]} transition-all`}
                  style={{ width: `${(value / max) * 100}%` }}
                />
              </div>
            </div>
          ))}
        </div>
      )}
    </Card>
  );
}

/* ---------- Recent Users Table ---------- */

interface RecentUsersTableProps {
  users: AdminUser[];
}

function RecentUsersTable({ users }: RecentUsersTableProps) {
  return (
    <Card data-testid="admin-recent-users">
      <h3 className="text-sm font-medium text-brand-slate-700 mb-4">Recent Users</h3>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-brand-slate-100">
              <th className="text-left pb-2 text-xs font-medium text-brand-slate-400">Name</th>
              <th className="text-left pb-2 text-xs font-medium text-brand-slate-400">Email</th>
              <th className="text-left pb-2 text-xs font-medium text-brand-slate-400">Joined</th>
              <th className="text-left pb-2 text-xs font-medium text-brand-slate-400">Status</th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id} className="border-b border-brand-slate-50 last:border-0">
                <td className="py-2.5 text-brand-slate-700">{u.firstName} {u.lastName}</td>
                <td className="py-2.5 text-brand-slate-500">{u.email}</td>
                <td className="py-2.5 text-brand-slate-500">
                  {new Date(u.createdAt).toLocaleDateString()}
                </td>
                <td className="py-2.5">
                  <Badge variant={u.isActive ? 'success' : 'error'}>
                    {u.isActive ? 'Active' : 'Inactive'}
                  </Badge>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {users.length === 0 && (
          <p className="text-xs text-brand-slate-400 text-center py-6">No users yet.</p>
        )}
      </div>
    </Card>
  );
}

/* ---------- Beta Codes Card ---------- */

interface BetaCodesCardProps {
  total: number;
  redeemed: number;
}

function BetaCodesCard({ total, redeemed }: BetaCodesCardProps) {
  const pct = total > 0 ? (redeemed / total) * 100 : 0;
  return (
    <Card>
      <h3 className="text-sm font-medium text-brand-slate-700 mb-3">Beta Codes</h3>
      <div className="flex justify-between text-xs text-brand-slate-500 mb-2">
        <span>{redeemed} redeemed</span>
        <span>{total} total</span>
      </div>
      <div className="h-2.5 rounded-full bg-brand-slate-100 overflow-hidden">
        <div
          className="h-full rounded-full bg-brand-teal-500 transition-all"
          style={{ width: `${pct}%` }}
        />
      </div>
    </Card>
  );
}

/* ---------- Quick Stats Card ---------- */

interface QuickStatsItem {
  icon: LucideIcon;
  label: string;
  value: number;
}

interface QuickStatsCardProps {
  items: QuickStatsItem[];
}

function QuickStatsCard({ items }: QuickStatsCardProps) {
  return (
    <Card>
      <h3 className="text-sm font-medium text-brand-slate-700 mb-3">Quick Stats</h3>
      <div className="space-y-3">
        {items.map(({ icon: Icon, label, value }) => (
          <div key={label} className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Icon size={14} strokeWidth={1.8} className="text-brand-slate-400" />
              <span className="text-xs text-brand-slate-600">{label}</span>
            </div>
            <span className="text-sm font-semibold text-brand-slate-700">{value}</span>
          </div>
        ))}
      </div>
    </Card>
  );
}

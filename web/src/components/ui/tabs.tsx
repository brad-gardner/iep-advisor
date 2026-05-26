import { NavLink } from "react-router-dom";

export function TabsNav({ children }: { children: React.ReactNode }) {
  return (
    <div
      role="tablist"
      className="flex border-b border-brand-slate-200 overflow-x-auto"
    >
      {children}
    </div>
  );
}

interface TabLinkProps {
  to: string;
  end?: boolean;
  testId?: string;
  children: React.ReactNode;
}

export function TabLink({ to, end, testId, children }: TabLinkProps) {
  return (
    <NavLink
      to={to}
      end={end}
      role="tab"
      data-testid={testId}
      className={({ isActive }) =>
        `px-4 py-2 text-[13px] font-medium transition-colors whitespace-nowrap ${
          isActive
            ? "text-brand-slate-800 border-b-2 border-brand-teal-500"
            : "text-brand-slate-400 hover:text-brand-slate-800"
        }`
      }
    >
      {children}
    </NavLink>
  );
}

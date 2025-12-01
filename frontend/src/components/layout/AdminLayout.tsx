import { Link, useLocation } from 'react-router-dom';
import type { ReactNode } from 'react';

interface AdminLayoutProps {
  children: ReactNode;
}

export function AdminLayout({ children }: AdminLayoutProps) {
  const location = useLocation();

  const tabs = [
    { path: '/admin', label: 'Season Management' },
    { path: '/admin/pick-rules', label: 'Pick Rules' },
    { path: '/admin/backfill', label: 'Backfill Picks' },
    { path: '/admin/approvals', label: 'Season Approvals' },
    { path: '/admin/eliminations', label: 'Eliminations' },
  ];

  return (
    <div className="container mx-auto p-6">
      {/* Admin Tabs Navigation */}
      <div className="mb-6 border-b">
        <nav className="-mb-px flex space-x-8 overflow-x-auto">
          {tabs.map((tab) => {
            const isActive = location.pathname === tab.path;
            return (
              <Link
                key={tab.path}
                to={tab.path}
                className={`
                  whitespace-nowrap border-b-2 py-4 px-1 text-sm font-medium transition-colors
                  ${
                    isActive
                      ? 'border-primary text-primary'
                      : 'border-transparent text-muted-foreground hover:border-gray-300 hover:text-foreground'
                  }
                `}
              >
                {tab.label}
              </Link>
            );
          })}
        </nav>
      </div>

      {/* Page Content */}
      <div>{children}</div>
    </div>
  );
}

import { Link } from "react-router-dom";
import { School } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { DistrictOverviewCard } from "@/features/district-admin/components/district-overview-card";
import { DistrictDashboardTiles } from "@/features/district-admin/components/district-dashboard-tiles";
import { SetupChecklistCard } from "@/features/district-admin/components/setup-checklist-card";
import { ORG_ROLE } from "../types";
import type { EducatorProfile } from "../types";

interface EducatorDashboardProps {
  profile: EducatorProfile;
}

/**
 * Operational "what do I do next" home body. Identity (school/district name,
 * role, state) lives in the page header now, so this renders only actionable
 * modules: the district oversight tiles for admins, and a focused caseload
 * module for teachers — never a bare identity card.
 */
export function EducatorDashboard({ profile }: EducatorDashboardProps) {
  const isDistrictAdmin = profile.orgRoleId === ORG_ROLE.DistrictAdmin;
  const isAdmin = isDistrictAdmin || profile.orgRoleId === ORG_ROLE.SchoolAdmin;

  if (isAdmin) {
    // DistrictAdmin: first-run checklist + compact district summary + the full
    // oversight tiles. SchoolAdmin: the tiles only (server-sliced to their
    // school); the district-wide checklist/overview are DistrictAdmin-only.
    return (
      <div className="space-y-6" data-testid="educator-dashboard">
        {isDistrictAdmin && <SetupChecklistCard />}
        {isDistrictAdmin && <DistrictOverviewCard />}
        <DistrictDashboardTiles />
      </div>
    );
  }

  // Teacher: a purposeful launchpad into their caseload rather than a data dump
  // or a bare profile card.
  return (
    <div className="space-y-6" data-testid="educator-dashboard">
      <Card>
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-start gap-4">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-card bg-brand-teal-50 text-brand-teal-500">
              <School size={20} strokeWidth={1.8} aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <h2 className="font-serif text-xl">Your students</h2>
              <p className="mt-1 max-w-prose text-sm text-brand-slate-500">
                Open the students on your caseload to review their profiles,
                documents, and prepare for upcoming meetings.
              </p>
            </div>
          </div>
          <div className="shrink-0 sm:pl-4">
            <Link to="/educator/students">
              <Button data-testid="educator-students-caseload-link">
                Go to your students
              </Button>
            </Link>
          </div>
        </div>
      </Card>
    </div>
  );
}

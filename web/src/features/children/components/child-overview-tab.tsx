import { useState } from "react";
import { useOutletContext } from "react-router-dom";
import { Share2 } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { ShareChildDialog } from "@/features/sharing/components/share-child-dialog";
import { AccessList } from "@/features/sharing/components/access-list";
import type { ChildOutletContext } from "./child-detail-page";

export function ChildOverviewTab() {
  const { child, childId } = useOutletContext<ChildOutletContext>();
  const [showShareDialog, setShowShareDialog] = useState(false);
  const [accessListKey, setAccessListKey] = useState(0);
  const isOwner = child.role === "owner";

  return (
    <div className="space-y-6">
      <Card data-testid="child-profile-section">
        <h2 className="font-serif mb-4">Profile</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {child.dateOfBirth && (
            <ProfileField
              label="Date of Birth"
              value={new Date(child.dateOfBirth).toLocaleDateString()}
            />
          )}
          {child.gradeLevel && (
            <ProfileField label="Grade Level" value={child.gradeLevel} />
          )}
          {child.disabilityCategory && (
            <ProfileField
              label="Disability Category"
              value={child.disabilityCategory}
            />
          )}
          {child.schoolDistrict && (
            <ProfileField
              label="School District"
              value={child.schoolDistrict}
            />
          )}
        </div>
      </Card>

      {isOwner && (
        <Card data-testid="sharing-section">
          <div className="flex items-center justify-between mb-4">
            <h2 className="font-serif">Sharing & Access</h2>
            <Button
              onClick={() => setShowShareDialog(!showShareDialog)}
              data-testid="share-invite-button"
            >
              <Share2
                className="w-4 h-4 mr-1.5"
                strokeWidth={1.8}
                aria-hidden="true"
              />
              Invite Someone
            </Button>
          </div>
          <p className="text-sm text-brand-slate-400 mb-4">
            Share {child.firstName}'s IEP information with a co-parent,
            advocate, or attorney. They'll get their own login and can view or
            collaborate depending on the role you assign.
          </p>
          {showShareDialog && (
            <div className="mb-4">
              <ShareChildDialog
                childId={childId}
                onInvited={() => {
                  setAccessListKey((k) => k + 1);
                  setShowShareDialog(false);
                }}
                onCancel={() => setShowShareDialog(false)}
              />
            </div>
          )}
          <AccessList
            key={accessListKey}
            childId={childId}
            isOwner={isOwner}
          />
        </Card>
      )}
    </div>
  );
}

function ProfileField({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-brand-slate-50 rounded-card p-3 border-[0.5px] border-brand-slate-200">
      <p className="text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold">
        {label}
      </p>
      <p className="text-sm font-medium text-brand-slate-800 mt-1">{value}</p>
    </div>
  );
}

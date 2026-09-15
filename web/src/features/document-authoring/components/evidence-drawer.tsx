import { useEffect, useId, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Drawer } from '@/components/ui/drawer';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { getStudentEvidence, type EvidenceItem, type EvidenceKind, type StudentEvidenceBundle } from '../api/evidence-api';
import type { ActiveFieldTarget } from '../hooks/document-editor-context';

interface EvidenceDrawerProps {
  open: boolean;
  onClose: () => void;
  studentId: number;
  /** The field that last had focus (null when nothing has been focused yet). */
  activeField: ActiveFieldTarget | null;
}

const GROUPS: Array<{ kinds: EvidenceKind[]; title: string }> = [
  { kinds: ['Identity', 'TeamMember'], title: 'Student & team' },
  { kinds: ['PresentLevels', 'EtrFinding'], title: 'Present levels & evaluation' },
  { kinds: ['PriorGoal', 'PriorService', 'PriorAccommodation', 'PriorTransition'], title: 'Prior plan' },
  { kinds: ['StudentVoice'], title: 'Student voice' },
  { kinds: ['ParentContribution'], title: 'From the family' },
];

const ROLE_LABEL: Record<EvidenceItem['authorRole'], string> = {
  school: 'School',
  student: 'Student — shared',
  family: 'Family — shared',
  system: 'System',
};

/**
 * Everything on record for this student that AI and prefill are allowed to
 * see — nothing more. Each item shows its source and date; "Insert" writes the
 * text into whichever field last had focus, as a plain copy.
 */
export function EvidenceDrawer({ open, onClose, studentId, activeField }: EvidenceDrawerProps) {
  const [bundle, setBundle] = useState<StudentEvidenceBundle | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);

  const targetHintId = useId();
  const targetLabel = activeField?.label() ?? null;

  // Fetched once per editor mount; a failed load is retried the next time the
  // drawer opens (the previous error stays visible until the retry answers).
  useEffect(() => {
    if (!open || loaded) return;
    let active = true;
    getStudentEvidence(studentId)
      .then((res) => {
        if (!active) return;
        if (res.success && res.data) {
          setBundle(res.data);
          setError(null);
          setLoaded(true);
        } else setError(res.message ?? 'Could not load evidence.');
      })
      .catch(() => {
        if (active) setError('Could not load evidence.');
      });
    return () => {
      active = false;
    };
  }, [open, loaded, studentId]);

  return (
    <Drawer open={open} onClose={onClose} title="Evidence on record">
      <div className="space-y-5" data-testid="evidence-drawer">
        <p className="text-[13px] text-brand-slate-500">
          Only what the school, the student (shared entries) and the family (shared notes) have put on
          record. Private family notes and analyses never appear here.
        </p>
        <p
          id={targetHintId}
          className="rounded-card border border-brand-slate-200 bg-brand-slate-50 px-3 py-2 text-[13px] text-brand-slate-600"
          data-testid="evidence-target"
        >
          {targetLabel ? (
            <>
              Inserting into: <span className="font-medium text-brand-slate-800">{targetLabel}</span>. Inserted text is
              added after what is already there.
            </>
          ) : (
            'Close this panel and click into a field to choose where to insert.'
          )}
        </p>
        {!loaded && !error && (
          <div className="space-y-2" role="status" aria-label="Loading evidence">
            <Skeleton className="h-5 w-3/4" />
            <Skeleton className="h-5 w-1/2" />
            <span className="sr-only">Loading…</span>
          </div>
        )}
        {error && (
          <Notice variant="error" title="Could not load evidence">
            {error}
          </Notice>
        )}
        {bundle && bundle.items.length === 0 && (
          <Notice variant="info" title="Nothing on record yet">
            Finalized documents, shared student entries and shared family notes will show up here.
          </Notice>
        )}
        {bundle &&
          GROUPS.map((g) => {
            const items = bundle.items.filter((i) => g.kinds.includes(i.kind));
            if (items.length === 0) return null;
            return (
              <section key={g.title} aria-label={g.title}>
                <h3 className="mb-2 text-[13px] font-medium uppercase tracking-wide text-brand-slate-500">{g.title}</h3>
                <ul className="space-y-2">
                  {items.map((item) => (
                    <li
                      key={item.id}
                      className="rounded-card border border-brand-slate-200 bg-white p-3"
                      data-testid={`evidence-${item.id}`}
                    >
                      <div className="mb-1 flex flex-wrap items-center gap-2 text-[11px] text-brand-slate-500">
                        <span className="rounded bg-brand-slate-100 px-1 font-mono text-brand-teal-700">{item.id}</span>
                        <span className="font-medium text-brand-slate-600">{item.sourceLabel}</span>
                        {item.sourceDate && <span>{new Date(item.sourceDate).toLocaleDateString()}</span>}
                        <span className="ml-auto rounded-full border border-brand-slate-200 px-1.5">{ROLE_LABEL[item.authorRole]}</span>
                      </div>
                      <p className="whitespace-pre-wrap text-[13px] leading-relaxed text-brand-slate-700">{item.text}</p>
                      {item.kind !== 'Identity' && item.kind !== 'TeamMember' && (
                        <div className="mt-2 flex justify-end">
                          <Button
                            variant="secondary"
                            size="sm"
                            aria-disabled={!activeField}
                            aria-describedby={targetHintId}
                            aria-label={targetLabel ? `Insert ${item.sourceLabel} into ${targetLabel}` : `Insert ${item.sourceLabel}`}
                            className={activeField ? undefined : 'opacity-60'}
                            onClick={() => activeField?.apply(item.text)}
                            data-testid={`evidence-${item.id}-insert`}
                          >
                            {targetLabel ? `Insert into ${targetLabel}` : 'Insert'}
                          </Button>
                        </div>
                      )}
                    </li>
                  ))}
                </ul>
              </section>
            );
          })}
      </div>
    </Drawer>
  );
}

import { useTranslation } from 'react-i18next';
import { Eye, Lock } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/cn';
import type { StudentWorkspaceEntryDto } from '../types';

interface ShareToggleProps {
  entry: StudentWorkspaceEntryDto;
  onToggle: (id: number, isShareable: boolean) => void;
  testIdPrefix: string;
}

// A clear private/shared switch. Uses a Button with aria-pressed so the state is
// announced and keyboard-operable. The label spells out the consequence.
export function ShareToggle({ entry, onToggle, testIdPrefix }: ShareToggleProps) {
  const { t } = useTranslation('student');
  const { isShareable } = entry;

  return (
    <Button
      type="button"
      variant={isShareable ? 'secondary' : 'ghost'}
      size="sm"
      onClick={() => onToggle(entry.id, !isShareable)}
      aria-pressed={isShareable}
      className={cn('gap-1.5', !isShareable && 'border border-brand-slate-200 bg-white')}
      data-testid={`${testIdPrefix}-share-toggle`}
    >
      {isShareable ? (
        <>
          <Eye className="h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
          {t('shareToggle.shared')}
        </>
      ) : (
        <>
          <Lock className="h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
          {t('shareToggle.private')}
        </>
      )}
    </Button>
  );
}

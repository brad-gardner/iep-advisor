import { useId } from 'react';
import { Link } from 'react-router-dom';
import { MessageCircleQuestion } from 'lucide-react';
import { cn } from '@/lib/cn';
import { advocateHref, formatAbout, type AboutKind, type AboutNavigationState } from '../lib/about';

interface AskAdvocateButtonProps {
  childId: number;
  /** The record the conversation is about — becomes `?about=<kind>:<id>`. */
  about: { kind: AboutKind; id: number };
  /** Human name for the record, shown in the advocate page's context pill ("IEP from March 2026"). */
  label?: string;
  /** `icon` for tight rows (the text stays as the accessible name). */
  appearance?: 'button' | 'icon';
  /** Icon mode only: a more specific accessible name when several launchers share a list. */
  ariaLabel?: string;
  /** Pass `false` when the viewer is known to be unable to ask; unknown roles keep the launcher (the page explains). */
  canAsk?: boolean;
  className?: string;
  'data-testid'?: string;
}

const HELPER_TEXT = 'Opens a private conversation with the Virtual Advocate about this item.';

const buttonClass =
  'inline-flex items-center justify-center rounded-button border-[1.5px] border-brand-teal-300 bg-transparent font-medium leading-[1.3] text-brand-teal-500 transition-colors hover:bg-brand-teal-50 focus:outline-none focus:ring-1 focus:ring-brand-teal-400 focus:ring-offset-2';

/**
 * "Ask the advocate" launcher: a secondary-styled link to a fresh advocate
 * conversation opened about one record. A link rather than a button because
 * it navigates; the human label travels in router state (never the URL), so a
 * direct link still works and the page falls back to a generic pill.
 */
export function AskAdvocateButton({
  childId,
  about,
  label,
  appearance = 'button',
  ariaLabel,
  canAsk = true,
  className,
  'data-testid': testId = 'ask-advocate',
}: AskAdvocateButtonProps) {
  const helperId = useId();
  if (!canAsk) return null;

  const state: AboutNavigationState | undefined = label ? { aboutLabel: label } : undefined;
  const iconOnly = appearance === 'icon';

  return (
    <>
      <Link
        to={advocateHref(childId, formatAbout(about.kind, about.id))}
        state={state}
        title={HELPER_TEXT}
        aria-describedby={helperId}
        aria-label={iconOnly ? (ariaLabel ?? 'Ask the advocate') : undefined}
        className={cn(buttonClass, iconOnly ? 'h-8 w-8 p-0' : 'gap-1.5 px-3 py-1.5 text-xs', className)}
        data-testid={testId}
        data-about={formatAbout(about.kind, about.id)}
      >
        <MessageCircleQuestion className="h-4 w-4 shrink-0" strokeWidth={1.8} aria-hidden="true" />
        {!iconOnly && 'Ask the advocate'}
      </Link>
      <span id={helperId} className="sr-only">
        {HELPER_TEXT}
      </span>
    </>
  );
}

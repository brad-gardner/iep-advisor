import { AlertCircle, Check } from 'lucide-react';
import { Spinner } from '@/components/ui/spinner';
import { cn } from '@/lib/cn';
import type { ToolActivity as ToolActivityRow } from '../hooks/use-advocate-thread';

interface ToolActivityProps {
  tools: ToolActivityRow[];
}

/**
 * "Checking the rules…" rows while the advocate reads the record. Labels come
 * from the server; status is icon + text, never colour alone. The running row
 * uses the shared `Spinner` (its own status role reads the label).
 */
export function ToolActivity({ tools }: ToolActivityProps) {
  if (tools.length === 0) return null;
  return (
    <ul
      className="space-y-1 text-xs text-brand-slate-500"
      data-testid="advocate-tool-activity"
      aria-label="What the advocate is checking"
    >
      {tools.map((tool) => (
        <li key={tool.key} className="flex items-center gap-2" data-testid={`advocate-tool-${tool.status}`}>
          {tool.status === 'started' && <Spinner size="sm" label={`${tool.label}…`} className="shrink-0 border-b-[1.5px]" />}
          {tool.status === 'finished' && <Check className="h-3.5 w-3.5 shrink-0 text-brand-teal-500" aria-hidden="true" />}
          {tool.status === 'failed' && <AlertCircle className="h-3.5 w-3.5 shrink-0 text-brand-amber-500" aria-hidden="true" />}
          <span
            className={cn(tool.status === 'failed' && 'text-brand-amber-500')}
            aria-hidden={tool.status === 'started' ? true : undefined}
          >
            {tool.label}
            {tool.status === 'started' && '…'}
            {tool.status === 'failed' && ' — couldn’t check this'}
          </span>
        </li>
      ))}
    </ul>
  );
}

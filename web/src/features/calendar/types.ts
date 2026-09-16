// Mirrors api/IepAssistant.Api/DTOs/Calendar/*.cs (see plan4-contract.md).
import type { MeetingDto } from '@/features/meetings/types';
import type { ObligationDto } from '@/features/obligations/types';

export type CalendarItemKind = 'Meeting' | 'Obligation';

export interface CalendarItemDto {
  kind: CalendarItemKind;
  /** ISO date or UTC datetime, depending on `kind`. */
  date: string;
  meeting?: MeetingDto | null;
  obligation?: ObligationDto | null;
}

export interface CalendarFeedDto {
  url: string;
  createdAt: string;
}

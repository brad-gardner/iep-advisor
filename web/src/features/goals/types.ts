// Mirrors api/IepAssistant.Api/DTOs/Goals/GoalDtos.cs (plan7-contract.md, Phase 1).
// Goals as first-class records across documents/years, plus provider progress
// observations (plan 7, decision 6).

export const GOAL_RECORD_STATUSES = ['Active', 'Met', 'NotMet', 'Retired', 'Carried'] as const;
export type GoalRecordStatus = (typeof GOAL_RECORD_STATUSES)[number];
export const GOAL_RECORD_STATUS_LABELS: Record<GoalRecordStatus, string> = {
  Active: 'Active',
  Met: 'Met',
  NotMet: 'Not met',
  Retired: 'Retired',
  Carried: 'Carried',
};

export interface GoalObservationDto {
  id: number;
  goalRecordId: number;
  observedAt: string;
  value: number | null;
  unit: string | null;
  note: string | null;
  recordedByUserId: number;
}

export interface GoalTrajectoryPointDto {
  observedAt: string;
  value: number;
}

export interface GoalTrajectoryDto {
  points: GoalTrajectoryPointDto[];
  insufficientData: boolean;
}

export interface GoalRecordDto {
  id: number;
  lineageId: string;
  versionId: number;
  versionNumber: number;
  documentTypeKey: string;
  domain: string | null;
  goalText: string;
  baseline: string | null;
  targetCriteria: string | null;
  measurementMethod: string | null;
  timeframe: string | null;
  status: GoalRecordStatus;
  statusReason: string | null;
  reviewedAt: string | null;
  projectedAt: string;
  lastObservedAt: string | null;
  staleAfterDays: number;
  isStale: boolean;
  observations: GoalObservationDto[];
  trajectory: GoalTrajectoryDto;
}

/** One goal's full record history across finalizes/amendments, newest record first. */
export interface GoalLineageDto {
  lineageId: string;
  records: GoalRecordDto[];
}

export interface CreateGoalObservationRequest {
  observedAt?: string;
  value?: number;
  unit?: string;
  note?: string;
}

/** Only the statuses a user can set explicitly (the server also produces
 *  Retired/Carried via the finalize projection — never posted from the UI). */
export type SettableGoalRecordStatus = Extract<GoalRecordStatus, 'Active' | 'Met' | 'NotMet'>;

export interface UpdateGoalStatusRequest {
  status: SettableGoalRecordStatus;
  reason?: string;
}

export interface CreateGoalRetirementRequest {
  lineageId: string;
  reason: string;
}

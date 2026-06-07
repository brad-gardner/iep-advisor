// Mirrors api/IepAssistant.Api/DTOs/District/*.cs

export interface DistrictOverview {
  id: number;
  name: string;
  stateCode?: string | null;
  activeSchoolCount: number;
  activeStaffCount: number;
}

export interface DistrictSchool {
  id: number;
  name: string;
  stateCode?: string | null;
  activeStudentCount: number;
  activeStaffCount: number;
}

export interface SaveSchoolRequest {
  name: string;
  stateCode?: string;
}

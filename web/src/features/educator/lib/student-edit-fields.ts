import { toDateInputValue } from '@/lib/format-date';
import type {
  DisabilityCategory,
  GradeLevel,
  SchoolStudent,
  UpdateSchoolStudentRequest,
} from '../types';

export interface EditStudentFields {
  firstName: string;
  lastName: string;
  externalStudentId: string;
  dateOfBirth: string;
  stateCode: string;
  gradeLevel: string;
  disabilityCategory: string;
  homeLanguage: string;
  iepDate: string;
  annualReviewDueDate: string;
  etrDate: string;
  reevaluationDueDate: string;
}

export function fieldsFrom(student: SchoolStudent): EditStudentFields {
  return {
    firstName: student.firstName,
    lastName: student.lastName ?? '',
    externalStudentId: student.externalStudentId ?? '',
    dateOfBirth: toDateInputValue(student.dateOfBirth),
    stateCode: student.stateCode ?? '',
    gradeLevel: student.gradeLevel ?? '',
    disabilityCategory: student.disabilityCategory ?? '',
    homeLanguage: student.homeLanguage ?? '',
    iepDate: toDateInputValue(student.iepDate),
    annualReviewDueDate: toDateInputValue(student.annualReviewDueDate),
    etrDate: toDateInputValue(student.etrDate),
    reevaluationDueDate: toDateInputValue(student.reevaluationDueDate),
  };
}

const orNull = (value: string): string | null => value.trim() || null;

// Full-replacement payload: every editable field is sent; blanks clear.
export function toUpdateRequest(fields: EditStudentFields): UpdateSchoolStudentRequest {
  return {
    firstName: fields.firstName.trim(),
    lastName: orNull(fields.lastName),
    dateOfBirth: orNull(fields.dateOfBirth),
    stateCode: orNull(fields.stateCode.toUpperCase()),
    externalStudentId: orNull(fields.externalStudentId),
    gradeLevel: (fields.gradeLevel as GradeLevel) || null,
    disabilityCategory: (fields.disabilityCategory as DisabilityCategory) || null,
    homeLanguage: orNull(fields.homeLanguage),
    iepDate: orNull(fields.iepDate),
    annualReviewDueDate: orNull(fields.annualReviewDueDate),
    etrDate: orNull(fields.etrDate),
    reevaluationDueDate: orNull(fields.reevaluationDueDate),
  };
}

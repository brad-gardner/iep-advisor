import { describe, expect, it } from 'vitest';
import { orgRoleLabel } from './org-role-label';

describe('orgRoleLabel', () => {
  it('maps the seeded role names to human labels', () => {
    expect(orgRoleLabel('DistrictAdmin')).toBe('District administrator');
    expect(orgRoleLabel('SchoolAdmin')).toBe('School administrator');
    expect(orgRoleLabel('Teacher')).toBe('Teacher');
  });

  it('passes unknown role names through unchanged', () => {
    expect(orgRoleLabel('Superintendent')).toBe('Superintendent');
  });

  it('returns an empty string for null/undefined/empty input', () => {
    expect(orgRoleLabel(null)).toBe('');
    expect(orgRoleLabel(undefined)).toBe('');
    expect(orgRoleLabel('')).toBe('');
  });
});

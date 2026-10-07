import i18n from '@/lib/i18n';

/** The stored category values an advocacy goal can carry — the server source of truth, never translated. */
export const ADVOCACY_GOAL_CATEGORIES = ['academic', 'behavioral', 'services', 'placement'] as const;
export type AdvocacyGoalCategory = (typeof ADVOCACY_GOAL_CATEGORIES)[number];

function isKnownCategory(value: string): value is AdvocacyGoalCategory {
  return (ADVOCACY_GOAL_CATEGORIES as readonly string[]).includes(value);
}

/**
 * Translated label for an advocacy goal's category (`advocacy-goals:category.*`),
 * following `orgRoleLabel`'s shape: a plain function over `i18n.t`, callable
 * from render bodies. An unrecognized/legacy value falls back to itself
 * rather than a blank label.
 */
export function advocacyGoalCategoryLabel(category: string | null | undefined): string {
  if (!category) return i18n.t('advocacy-goals:category.none');
  if (isKnownCategory(category)) return i18n.t(`advocacy-goals:category.${category}`);
  return category;
}

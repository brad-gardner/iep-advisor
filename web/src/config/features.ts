/**
 * App-level feature flags.
 *
 * `IEP_AUTHORING_MODE` controls which IEP authoring surface educators land on:
 *  - `'template'` (default): the template-driven document engine (state-specific
 *    IEP/ETR/504 authoring). This is the active cutover.
 *  - `'typed'`: the legacy hardcoded IEP draft editor.
 *
 * The legacy `/educator/students/:id/iep-drafts` routes stay registered in BOTH
 * modes, so flipping this back to `'typed'` is a complete, one-line rollback with
 * no data loss. Finalized legacy IEP versions remain viewable regardless of mode.
 */
export const IEP_AUTHORING_MODE: 'template' | 'typed' = 'template';

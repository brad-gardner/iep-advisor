/**
 * Join Tailwind class names, dropping falsy values so conditional classes read
 * cleanly: `cn('base', isActive && 'active', size === 'sm' && 'text-sm')`.
 *
 * This is a plain join, NOT a Tailwind-aware merge — it does not resolve
 * conflicting utilities (e.g. `cn('p-2', 'p-4')` yields `"p-2 p-4"`, and the
 * later class wins by CSS source order, same as any hand-written className).
 * Callers must not rely on merge semantics; pass the intended final class.
 */
export function cn(...classes: Array<string | false | null | undefined>): string {
  return classes.filter(Boolean).join(' ');
}

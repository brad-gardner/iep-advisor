import { Extension } from '@tiptap/core';
import { Plugin, PluginKey } from '@tiptap/pm/state';
import type { Node as ProseMirrorNode } from '@tiptap/pm/model';

export interface MarkdownLimitOptions {
  /** Maximum length of the *serialized markdown string* — the same value the
   *  server persists and caps, not the plain-text/rendered length. `null`
   *  disables the limit entirely. */
  limit: number | null;
}

/**
 * Blocks transactions that would push the document's serialized markdown
 * past `options.limit`. This is the single source of truth for "is this
 * content over the limit": the stored markdown string is what the server
 * caps, so the extension measures the same thing `RichTextEditor`'s visible
 * counter and `isMarkdownOverLimit` helper measure (`editor.getMarkdown()`
 * output), rather than `@tiptap/extensions`' CharacterCount, which counts
 * plain rendered text and lets formatting (`**`, `- `, `[]()`, headings…)
 * push the real persisted string over the cap unnoticed.
 *
 * Mirrors the shape of CharacterCount's own `filterTransaction`
 * (node_modules/@tiptap/extensions/src/character-count/character-count.ts)
 * but with one deliberate difference: a transaction is rejected only when it
 * is BOTH over the limit AND longer (in serialized markdown) than the
 * document it started from. That means deletions and formatting removals
 * always pass, even while the document is already over the limit — so
 * content that arrived over-limit from outside (see `RichTextEditor`'s
 * external-value sync) can still be edited back down to size, and backspace
 * always works.
 *
 * No auto-trim of over-limit initial content (unlike CharacterCount's
 * `autoTrim`): `RichTextEditor` intentionally leaves an externally-supplied
 * over-limit value untouched and instead surfaces it via the visible counter
 * and `aria-invalid`, so nothing is silently deleted out from under the
 * caller.
 */
export const MarkdownLimit = Extension.create<MarkdownLimitOptions>({
  name: 'markdownLimit',

  addOptions() {
    return { limit: null };
  },

  addProseMirrorPlugins() {
    const serialize = (doc: ProseMirrorNode): string => this.editor.markdown?.serialize(doc.toJSON()) ?? '';

    return [
      new Plugin({
        key: new PluginKey('markdownLimit'),
        filterTransaction: (transaction, state) => {
          const { limit } = this.options;
          if (!transaction.docChanged || limit === null) return true;

          const oldLength = serialize(state.doc).length;
          const newLength = serialize(transaction.doc).length;

          // Only reject a transaction that both exceeds the limit AND grew
          // the serialized string relative to where it started — a shrink
          // (or a same-length change) always passes even over the limit.
          if (newLength > limit && newLength > oldLength) return false;
          return true;
        },
      }),
    ];
  },
});

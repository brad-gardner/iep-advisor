import remarkGfm from 'remark-gfm';
import remarkParse from 'remark-parse';
import { unified } from 'unified';

interface MdNode {
  type: string;
  value?: string;
  children?: MdNode[];
}

// Node types that terminate a run of spoken text (a `break` is a hard line break with no children).
const BLOCK_TYPES = new Set(['paragraph', 'heading', 'listItem', 'tableCell', 'break']);

/**
 * The words a screen reader should say for an advocate answer: the same remark-gfm parse the
 * visual bubble uses, reduced to its text (emphasis, headings, list markers, link URLs, table
 * pipes and raw HTML dropped; link labels, inline code and cell text kept). Using the real
 * parser keeps the announcement in step with what is rendered — intraword `_`/`*` and
 * arithmetic survive, and raw HTML is treated exactly as rehype-sanitize renders it (an inline tag
 * loses its markup but keeps its inner text; a block-level tag disappears) (the server strips the real `<sources>` block before storage anyway).
 */
export function markdownToPlainText(markdown: string): string {
  // remark-parse types the result as mdast `Root`, which is structurally an MdNode — no cast needed.
  const tree: MdNode = unified().use(remarkParse).use(remarkGfm).parse(markdown);
  const parts: string[] = [];
  const walk = (node: MdNode) => {
    if (node.type === 'html') return;
    if (node.type === 'text' || node.type === 'inlineCode' || node.type === 'code') {
      if (node.value) parts.push(node.value);
      if (node.type === 'code') parts.push(' ');
      return;
    }
    node.children?.forEach(walk);
    if (BLOCK_TYPES.has(node.type)) parts.push(' ');
  };
  walk(tree);
  return parts.join('').replace(/\s+/g, ' ').trim();
}

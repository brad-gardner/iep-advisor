import remarkGfm from 'remark-gfm';
import remarkParse from 'remark-parse';
import { unified } from 'unified';

interface MdNode {
  type: string;
  value?: string;
  children?: MdNode[];
}

const BLOCK_TYPES = new Set(['paragraph', 'heading', 'listItem', 'tableCell', 'blockquote', 'code', 'tableRow', 'list', 'table']);

/**
 * The words a screen reader should say for an advocate answer: the same remark-gfm parse the
 * visual bubble uses, reduced to its text (emphasis, headings, list markers, link URLs, table
 * pipes and raw HTML dropped; link labels, inline code and cell text kept). Using the real
 * parser keeps the announcement in step with what is rendered — intraword `_`/`*` and
 * arithmetic survive, and a stray raw tag loses its markup but keeps its inner text, exactly as
 * rehype-sanitize renders it (the server strips the real `<sources>` block before storage anyway).
 */
export function markdownToPlainText(markdown: string): string {
  const tree = unified().use(remarkParse).use(remarkGfm).parse(markdown) as MdNode;
  const parts: string[] = [];
  const walk = (node: MdNode) => {
    if (node.type === 'html') return;
    if (node.type === 'text' || node.type === 'inlineCode' || node.type === 'code') {
      if (node.value) parts.push(node.value);
      return;
    }
    node.children?.forEach(walk);
    if (BLOCK_TYPES.has(node.type)) parts.push(' ');
  };
  walk(tree);
  return parts.join('').replace(/\s+/g, ' ').trim();
}

import { test, expect } from '../helpers/fixtures';
import { KnowledgeBasePage } from '../pages/knowledge-base.page';

test.describe('Knowledge Base', () => {
  test('search returns results', async ({ loggedInPage: page }) => {
    const kb = new KnowledgeBasePage(page);

    await kb.goto();
    await kb.search('FAPE');
    await kb.expectResultVisible('Free Appropriate Public Education');
  });

  test('filter by category', async ({ loggedInPage: page }) => {
    const kb = new KnowledgeBasePage(page);

    await kb.goto();
    await kb.selectCategory('Glossary');
    await kb.expectResultVisible('FAPE');
  });
});

import { test, expect } from '../helpers/fixtures';
import { ChildrenPage } from '../pages/children.page';
import { IepAnalysisPage } from '../pages/iep-analysis.page';

test.describe('IEP Analysis', () => {
  test.setTimeout(120_000);

  test('trigger analysis and view results', async ({ page }) => {
    const children = new ChildrenPage(page);
    const analysis = new IepAnalysisPage(page);

    await children.goto();

    const hasIep = await analysis.navigateToFirstIep();
    if (!hasIep) {
      test.skip(true, 'No parsed IEP available for analysis test');
      return;
    }

    await analysis.clickAnalysisTab();
    await analysis.triggerAnalysisIfNeeded();
    await analysis.expectOverviewVisible();
  });
});

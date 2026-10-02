import { test } from '../helpers/fixtures';
import { ChildrenPage } from '../pages/children.page';
import { IepAnalysisPage } from '../pages/iep-analysis.page';

test.describe('IEP Analysis', () => {
  // A real analysis is one Claude call per document plus polling; allow it to finish.
  test.setTimeout(15 * 60_000);

  test('analyze an IEP from its page and find the same run on the child analysis tab', async ({ page }) => {
    const children = new ChildrenPage(page);
    const analysis = new IepAnalysisPage(page);

    await children.goto();

    const ids = await analysis.navigateToFirstIep();
    if (!ids) {
      test.skip(true, 'No parsed IEP available for analysis test');
      return;
    }

    await analysis.clickAnalysisTab();
    await analysis.triggerAnalysisIfNeeded();
    await analysis.expectOverviewVisible();
    await analysis.expectRunOnChildTimeline(ids.childId, ids.iepId);
  });
});

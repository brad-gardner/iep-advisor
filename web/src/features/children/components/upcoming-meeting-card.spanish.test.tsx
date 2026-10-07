import { afterEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import { makeMeeting } from "@/features/meetings/test/fixtures";
import { renderInSpanish, resetTestLanguage } from "@/test/i18n-test-utils";

const meetingsApi = vi.hoisted(() => ({
  listChildMeetings: vi.fn(),
  rsvpToMeeting: vi.fn(),
}));
vi.mock("@/features/meetings/api/meetings-api", () => meetingsApi);

import { UpcomingMeetingCard } from "./upcoming-meeting-card";

describe("UpcomingMeetingCard in Spanish", () => {
  afterEach(() => resetTestLanguage());

  it("renders the heading in Spanish", async () => {
    meetingsApi.listChildMeetings.mockResolvedValue({
      success: true,
      data: [makeMeeting({ startsAtUtc: "2099-01-01T15:00:00.000Z", myInviteStatus: "Pending" })],
    });

    await renderInSpanish(<UpcomingMeetingCard childId={5} />, { ns: "children" });

    expect(await screen.findByRole("heading", { name: "Próxima reunión" })).toBeInTheDocument();
  });
});

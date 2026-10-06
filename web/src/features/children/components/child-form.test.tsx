import { describe, it, expect, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ChildForm } from "./child-form";
import {
  GRADE_LEVEL_OPTIONS,
  DISABILITY_CATEGORY_OPTIONS,
} from "../lib/child-profile-options";

function makeOnSubmit() {
  return vi.fn().mockResolvedValue({ success: true });
}

function optionLabels(testId: string) {
  const select = screen.getByTestId(testId);
  return within(select)
    .getAllByRole("option")
    .map((option) => option.textContent);
}

describe("ChildForm — Grade Level / Disability Category dropdowns", () => {
  it("renders Grade Level options in order: Not set, Pre-K, Kindergarten, 1st..12th, Ungraded", () => {
    render(
      <ChildForm embedded onSubmit={makeOnSubmit()} submitLabel="Save" />,
    );

    expect(optionLabels("child-grade-level")).toEqual([
      "Not set",
      ...GRADE_LEVEL_OPTIONS,
    ]);
  });

  it("renders Disability Category options in order: Not set, then the 14 IDEA categories + Other", () => {
    render(
      <ChildForm embedded onSubmit={makeOnSubmit()} submitLabel="Save" />,
    );

    expect(optionLabels("child-disability-category")).toEqual([
      "Not set",
      ...DISABILITY_CATEGORY_OPTIONS,
    ]);
  });

  it("defaults both dropdowns to Not set on the create form", () => {
    render(
      <ChildForm embedded onSubmit={makeOnSubmit()} submitLabel="Save" />,
    );

    expect(screen.getByTestId("child-grade-level")).toHaveValue("");
    expect(screen.getByTestId("child-disability-category")).toHaveValue("");
  });

  it("submits undefined grade/disability on create when Not set is selected", async () => {
    const user = userEvent.setup();
    const onSubmit = makeOnSubmit();
    render(<ChildForm embedded onSubmit={onSubmit} submitLabel="Save" />);

    await user.type(screen.getByTestId("child-first-name"), "Ada");
    await user.selectOptions(screen.getByTestId("child-grade-level"), "8th");
    await user.selectOptions(
      screen.getByTestId("child-disability-category"),
      "Autism",
    );
    await user.selectOptions(screen.getByTestId("child-grade-level"), "Not set");
    await user.selectOptions(
      screen.getByTestId("child-disability-category"),
      "Not set",
    );
    await user.click(screen.getByTestId("child-form-submit"));

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({
        gradeLevel: undefined,
        disabilityCategory: undefined,
      }),
    );
  });

  it("preserves an unmatched legacy grade level on submit when left untouched", async () => {
    const user = userEvent.setup();
    const onSubmit = makeOnSubmit();
    render(
      <ChildForm
        embedded
        onSubmit={onSubmit}
        submitLabel="Save Changes"
        initialValues={{
          firstName: "Ada",
          gradeLevel: "8th grade (honors track)",
        }}
      />,
    );

    // The legacy value shows up as its own selected option rather than being
    // silently dropped or coerced to a close match.
    expect(screen.getByTestId("child-grade-level")).toHaveValue(
      "8th grade (honors track)",
    );

    await user.click(screen.getByTestId("child-form-submit"));

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ gradeLevel: "8th grade (honors track)" }),
    );
  });

  it("preserves an unmatched legacy disability category on submit when left untouched", async () => {
    const user = userEvent.setup();
    const onSubmit = makeOnSubmit();
    render(
      <ChildForm
        embedded
        onSubmit={onSubmit}
        submitLabel="Save Changes"
        initialValues={{
          firstName: "Ada",
          disabilityCategory: "Speech/Language (mild)",
        }}
      />,
    );

    expect(screen.getByTestId("child-disability-category")).toHaveValue(
      "Speech/Language (mild)",
    );

    await user.click(screen.getByTestId("child-form-submit"));

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({
        disabilityCategory: "Speech/Language (mild)",
      }),
    );
  });

  it("normalizes a recognized legacy variant to its canonical option on submit", async () => {
    const user = userEvent.setup();
    const onSubmit = makeOnSubmit();
    render(
      <ChildForm
        embedded
        onSubmit={onSubmit}
        submitLabel="Save Changes"
        initialValues={{ firstName: "Ada", gradeLevel: "8th grade" }}
      />,
    );

    expect(screen.getByTestId("child-grade-level")).toHaveValue("8th");

    await user.click(screen.getByTestId("child-form-submit"));

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ gradeLevel: "8th" }),
    );
  });

  it("sends an empty string to clear a stored value when Not set is picked on edit", async () => {
    const user = userEvent.setup();
    const onSubmit = makeOnSubmit();
    render(
      <ChildForm
        embedded
        onSubmit={onSubmit}
        submitLabel="Save Changes"
        initialValues={{
          firstName: "Ada",
          gradeLevel: "4th",
          disabilityCategory: "Autism",
        }}
      />,
    );

    await user.selectOptions(screen.getByTestId("child-grade-level"), "");
    await user.selectOptions(
      screen.getByTestId("child-disability-category"),
      "",
    );
    await user.click(screen.getByTestId("child-form-submit"));

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ gradeLevel: "", disabilityCategory: "" }),
    );
  });

  it("preselects the canonical option for a school-linked disability label", async () => {
    const user = userEvent.setup();
    const onSubmit = makeOnSubmit();
    render(
      <ChildForm
        embedded
        onSubmit={onSubmit}
        submitLabel="Save Changes"
        initialValues={{
          firstName: "Ada",
          disabilityCategory: "Visual Impairment",
        }}
      />,
    );

    expect(screen.getByTestId("child-disability-category")).toHaveValue(
      "Visual impairment (including blindness)",
    );
    expect(optionLabels("child-disability-category")).not.toContain(
      "Visual Impairment (current value)",
    );

    await user.click(screen.getByTestId("child-form-submit"));

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({
        disabilityCategory: "Visual impairment (including blindness)",
      }),
    );
  });

  it("decides clear-vs-unchanged from the value the form opened with, not a later reload", async () => {
    const user = userEvent.setup();
    const onSubmit = makeOnSubmit();
    const { rerender } = render(
      <ChildForm
        embedded
        onSubmit={onSubmit}
        submitLabel="Save Changes"
        initialValues={{ firstName: "Ada", gradeLevel: "" }}
      />,
    );
    rerender(
      <ChildForm
        embedded
        onSubmit={onSubmit}
        submitLabel="Save Changes"
        initialValues={{ firstName: "Ada", gradeLevel: "5th" }}
      />,
    );

    await user.click(screen.getByTestId("child-form-submit"));

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ gradeLevel: undefined }),
    );
  });
});

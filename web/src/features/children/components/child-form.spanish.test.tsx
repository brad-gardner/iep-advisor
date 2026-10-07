import { afterEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import { renderInSpanish, resetTestLanguage } from "@/test/i18n-test-utils";
import { ChildForm } from "./child-form";

describe("ChildForm in Spanish", () => {
  afterEach(() => resetTestLanguage());

  it("renders field labels and the submit button in Spanish", async () => {
    await renderInSpanish(
      <ChildForm embedded onSubmit={vi.fn().mockResolvedValue({ success: true })} submitLabel="Crear perfil" />,
      { ns: "children" }
    );

    expect(screen.getByLabelText("Nombre *")).toBeInTheDocument();
    expect(screen.getByLabelText("Apellido")).toBeInTheDocument();
    expect(screen.getByLabelText("Fecha de nacimiento")).toBeInTheDocument();
    expect(screen.getByLabelText("Grado")).toBeInTheDocument();
    expect(screen.getByLabelText("Categoría de discapacidad")).toBeInTheDocument();
    expect(screen.getByLabelText("Distrito escolar")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Crear perfil" })).toBeInTheDocument();
  });

  it("shows translated option labels, including Not set and current-value fallback", async () => {
    await renderInSpanish(
      <ChildForm
        embedded
        initialValues={{ gradeLevel: "legacy grade 9" }}
        onSubmit={vi.fn().mockResolvedValue({ success: true })}
        submitLabel="Guardar"
      />,
      { ns: "children" }
    );

    const gradeSelect = screen.getByTestId("child-grade-level");
    expect(gradeSelect).toHaveTextContent("Sin establecer");
    expect(gradeSelect).toHaveTextContent("Prekínder");
    expect(gradeSelect).toHaveTextContent("legacy grade 9 (valor actual)");
  });
});

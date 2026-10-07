import { describe, it, expect, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { GeneratedLanguageNotice } from './generated-language-notice';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

describe('GeneratedLanguageNotice', () => {
  afterEach(() => resetTestLanguage());

  it('renders nothing when the artifact matches the viewer language', () => {
    render(<GeneratedLanguageNotice generatedLanguage="en" />);
    expect(screen.queryByTestId('generated-language-notice')).toBeNull();
  });

  it('treats a missing language as English', () => {
    render(<GeneratedLanguageNotice generatedLanguage={null} />);
    expect(screen.queryByTestId('generated-language-notice')).toBeNull();
  });

  it('tells a Spanish viewer the artifact was generated in English', async () => {
    await renderInSpanish(<GeneratedLanguageNotice generatedLanguage={null} />);
    expect(screen.getByTestId('generated-language-notice')).toHaveTextContent('Generado en inglés');
  });

  it('tells an English viewer the artifact was generated in Spanish', () => {
    render(<GeneratedLanguageNotice generatedLanguage="es" />);
    expect(screen.getByTestId('generated-language-notice')).toHaveTextContent('Generated in Spanish');
  });
});

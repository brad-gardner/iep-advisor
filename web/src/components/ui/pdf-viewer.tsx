import { useEffect, useState } from "react";
import { Download, ExternalLink } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Card } from "./card";
import { Notice } from "./notice";

interface PdfViewerProps {
  fileName: string | null;
  parsedNote?: string;
  loadUrl: () => Promise<string | null>;
}

export function PdfViewer({ fileName, parsedNote, loadUrl }: PdfViewerProps) {
  const { t } = useTranslation("common");
  const [url, setUrl] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setIsLoading(true);
    setError(false);
    loadUrl()
      .then((u) => {
        if (cancelled) return;
        if (!u) {
          setError(true);
        } else {
          setUrl(u);
        }
      })
      .catch(() => {
        if (cancelled) return;
        setError(true);
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [loadUrl]);

  return (
    <div className="space-y-3">
      {parsedNote && (
        <Notice variant="success" title={t("pdfViewer.documentParsed")}>
          {parsedNote}
        </Notice>
      )}

      <Card className="p-3">
        <div className="flex items-center justify-between mb-3">
          <div className="text-sm font-medium text-brand-slate-800 truncate">
            {fileName || t("pdfViewer.document")}
          </div>
          <div className="flex gap-2 shrink-0">
            {url && (
              <a
                href={url}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-slate-500 hover:text-brand-teal-500 transition-colors"
              >
                <ExternalLink
                  className="w-3.5 h-3.5"
                  strokeWidth={1.8}
                  aria-hidden="true"
                />
                {t("pdfViewer.openInNewTab")}
              </a>
            )}
            {url && (
              <a
                href={url}
                download={fileName || undefined}
                className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-slate-500 hover:text-brand-teal-500 transition-colors"
              >
                <Download
                  className="w-3.5 h-3.5"
                  strokeWidth={1.8}
                  aria-hidden="true"
                />
                {t("pdfViewer.download")}
              </a>
            )}
          </div>
        </div>

        {isLoading && (
          <div className="flex justify-center py-12">
            <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-brand-teal-500" />
          </div>
        )}

        {!isLoading && error && (
          <Notice variant="error" title={t("pdfViewer.couldNotLoadTitle")}>
            {t("pdfViewer.couldNotLoad")}
          </Notice>
        )}

        {!isLoading && !error && !url && (
          <p className="text-sm text-brand-slate-500 py-8 text-center">
            {t("pdfViewer.noDocument")}
          </p>
        )}

        {!isLoading && !error && url && (
          <PdfFrame url={url} />
        )}
      </Card>
    </div>
  );
}

function PdfFrame({ url }: { url: string }) {
  const { t } = useTranslation("common");
  const [frameError, setFrameError] = useState(false);
  return (
    <div className="w-full" style={{ height: "min(80vh, 900px)" }}>
      {frameError ? (
        <div className="text-center py-8">
          <p className="text-sm text-brand-slate-500 mb-3">
            {t("pdfViewer.previewUnavailable")}
          </p>
          <a
            href={url}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center px-3 py-1.5 rounded-button text-[13px] font-medium bg-brand-slate-100 text-brand-slate-800 hover:bg-brand-slate-200 transition-colors"
          >
            {t("pdfViewer.openInNewTab")}
          </a>
        </div>
      ) : (
        <iframe
          src={url}
          title={t("pdfViewer.documentPreview")}
          className="w-full h-full rounded-card border border-brand-slate-200"
          onError={() => setFrameError(true)}
        />
      )}
    </div>
  );
}

import { useEffect, useState } from "react";
import { Download, ExternalLink } from "lucide-react";
import { Card } from "./card";
import { Notice } from "./notice";

interface PdfViewerProps {
  fileName: string | null;
  parsedNote?: string;
  loadUrl: () => Promise<string | null>;
}

export function PdfViewer({ fileName, parsedNote, loadUrl }: PdfViewerProps) {
  const [url, setUrl] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setIsLoading(true);
    setError(null);
    loadUrl()
      .then((u) => {
        if (cancelled) return;
        if (!u) {
          setError("Couldn't load the document.");
        } else {
          setUrl(u);
        }
      })
      .catch(() => {
        if (cancelled) return;
        setError("Couldn't load the document.");
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
        <Notice variant="success" title="Document parsed">
          {parsedNote}
        </Notice>
      )}

      <Card className="p-3">
        <div className="flex items-center justify-between mb-3">
          <div className="text-sm font-medium text-brand-slate-800 truncate">
            {fileName || "Document"}
          </div>
          <div className="flex gap-2 shrink-0">
            {url && (
              <a
                href={url}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-slate-400 hover:text-brand-teal-500 transition-colors"
              >
                <ExternalLink
                  className="w-3.5 h-3.5"
                  strokeWidth={1.8}
                  aria-hidden="true"
                />
                Open in new tab
              </a>
            )}
            {url && (
              <a
                href={url}
                download={fileName || undefined}
                className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-slate-400 hover:text-brand-teal-500 transition-colors"
              >
                <Download
                  className="w-3.5 h-3.5"
                  strokeWidth={1.8}
                  aria-hidden="true"
                />
                Download
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
          <Notice variant="error" title="Couldn't load the document">
            {error}
          </Notice>
        )}

        {!isLoading && !error && !url && (
          <p className="text-sm text-brand-slate-400 py-8 text-center">
            No document attached.
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
  const [frameError, setFrameError] = useState(false);
  return (
    <div className="w-full" style={{ height: "min(80vh, 900px)" }}>
      {frameError ? (
        <div className="text-center py-8">
          <p className="text-sm text-brand-slate-500 mb-3">
            Inline preview isn't available for this file.
          </p>
          <a
            href={url}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center px-3 py-1.5 rounded-button text-[13px] font-medium bg-brand-slate-100 text-brand-slate-800 hover:bg-brand-slate-200 transition-colors"
          >
            Open in new tab
          </a>
        </div>
      ) : (
        <iframe
          src={url}
          title="Document preview"
          className="w-full h-full rounded-card border border-brand-slate-200"
          onError={() => setFrameError(true)}
        />
      )}
    </div>
  );
}

import { useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
import { Upload } from "lucide-react";
import i18n from "@/lib/i18n";
import { uploadFile } from "../api/progress-reports-api";
import { Notice } from "@/components/ui/notice";
import { Spinner } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";

interface ProgressReportUploadProps {
  progressReportId: number;
  onUploaded: () => void;
}

export function ProgressReportUpload({
  progressReportId,
  onUploaded,
}: ProgressReportUploadProps) {
  const { t } = useTranslation("progress-reports");
  const [isDragging, setIsDragging] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { show } = useToast();

  // `i18n.t` directly: same reasoning as iep-upload.tsx/etr-upload.tsx — a
  // stable callback reference matters more here than reacting to `t`'s
  // identity (see docs/i18n/README.md).
  const handleFile = useCallback(
    async (file: File) => {
      if (!file.name.toLowerCase().endsWith(".pdf")) {
        setError(i18n.t("progress-reports:upload.onlyPdf"));
        return;
      }

      if (file.size > 50 * 1024 * 1024) {
        setError(i18n.t("progress-reports:upload.tooLarge"));
        return;
      }

      setIsUploading(true);
      setError(null);

      try {
        const response = await uploadFile(progressReportId, file);
        if (response.success) {
          show({ message: i18n.t("progress-reports:upload.uploadedToast"), variant: "success" });
          onUploaded();
        } else {
          setError(response.message || i18n.t("progress-reports:upload.uploadFailed"));
        }
      } catch {
        setError(i18n.t("progress-reports:upload.uploadError"));
      } finally {
        setIsUploading(false);
      }
    },
    [progressReportId, onUploaded, show]
  );

  return (
    <div>
      {error && (
        <div className="mb-3">
          <Notice variant="error" title={error} />
        </div>
      )}

      <label
        onDragOver={(e) => {
          e.preventDefault();
          setIsDragging(true);
        }}
        onDragLeave={() => setIsDragging(false)}
        onDrop={(e) => {
          e.preventDefault();
          setIsDragging(false);
          const file = e.dataTransfer.files[0];
          if (file) handleFile(file);
        }}
        data-testid="pr-upload-zone"
        className={`block border-2 border-dashed rounded-card p-6 text-center cursor-pointer transition-colors ${
          isDragging
            ? "border-brand-teal-500 bg-brand-teal-50"
            : "border-brand-slate-200 hover:border-brand-teal-300"
        } ${isUploading ? "opacity-50 pointer-events-none" : ""}`}
      >
        <input
          type="file"
          accept=".pdf,application/pdf"
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (file) handleFile(file);
            e.target.value = "";
          }}
          className="hidden"
          disabled={isUploading}
          data-testid="pr-file-input"
        />
        {isUploading ? (
          <div className="flex flex-col items-center gap-2">
            <Spinner size="sm" label={t("upload.uploadingLabel")} />
            <p className="text-brand-slate-500 text-sm">{t("upload.uploadingEllipsis")}</p>
          </div>
        ) : (
          <div className="flex flex-col items-center gap-1">
            <Upload
              className="w-5 h-5 text-brand-slate-400"
              strokeWidth={1.8}
              aria-hidden="true"
            />
            <p className="text-brand-slate-600 text-sm">{t("upload.attachPdf")}</p>
            <p className="text-brand-slate-500 text-[11px]">
              {t("upload.dropHint")}
            </p>
          </div>
        )}
      </label>
    </div>
  );
}

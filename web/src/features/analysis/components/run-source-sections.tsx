import { Card } from "@/components/ui/card";
import { RunSectionDetail } from "./run-section-detail";
import type { AnalysisRunSection, AnalysisRunSource } from "../types";

interface RunSourceSectionsProps {
  source: AnalysisRunSource;
  sections: AnalysisRunSection[];
}

export function RunSourceSections({ source, sections }: RunSourceSectionsProps) {
  const ordered = [...sections].sort((a, b) => a.displayOrder - b.displayOrder);

  return (
    <Card>
      <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-4">
        {source.sourceLabel ?? `${source.sourceType} #${source.sourceId}`}
      </h2>
      <div className="space-y-8">
        {ordered.map((section) =>
          section.analysis ? (
            <RunSectionDetail key={section.id} section={section.analysis} />
          ) : null
        )}
      </div>
    </Card>
  );
}

import type { CrossDocSynthesis } from "../types";

interface CrossDocSynthesisSectionProps {
  synthesis: CrossDocSynthesis;
}

export function CrossDocSynthesisSection({
  synthesis,
}: CrossDocSynthesisSectionProps) {
  return (
    <section className="space-y-4">
      <div>
        <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-1">
          Cross-Document Synthesis
        </h2>
        <p className="text-sm text-brand-slate-600">{synthesis.summary}</p>
      </div>

      {synthesis.progression && (
        <div>
          <h3 className="text-[10px] font-semibold text-brand-teal-500 uppercase tracking-wide mb-2">
            Progression
          </h3>
          <p className="text-sm text-brand-slate-600">{synthesis.progression}</p>
        </div>
      )}

      {synthesis.timeline.length > 0 && (
        <div>
          <h3 className="text-[10px] font-semibold text-brand-teal-500 uppercase tracking-wide mb-2">
            Timeline
          </h3>
          <ul className="space-y-1.5 list-disc list-inside text-sm text-brand-slate-600">
            {synthesis.timeline.map((item, i) => (
              <li key={i}>{item}</li>
            ))}
          </ul>
        </div>
      )}

      {synthesis.contradictions.length > 0 && (
        <div>
          <h3 className="text-[10px] font-semibold text-brand-teal-500 uppercase tracking-wide mb-2">
            Contradictions
          </h3>
          <ul className="space-y-1.5 list-disc list-inside text-sm text-brand-slate-600">
            {synthesis.contradictions.map((item, i) => (
              <li key={i}>{item}</li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}

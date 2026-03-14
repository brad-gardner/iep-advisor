import type { RedFlag } from '@/types/api';

interface RedFlagCardProps {
  redFlag: RedFlag;
}

export function RedFlagCard({ redFlag }: RedFlagCardProps) {
  const isRed = redFlag.severity === 'red';

  return (
    <div
      className={`rounded-lg border p-4 ${
        isRed
          ? 'bg-red-50 border-red-300'
          : 'bg-yellow-50 border-yellow-400'
      }`}
    >
      <div className="flex items-start gap-2">
        <span className={`text-lg ${isRed ? 'text-red-600' : 'text-yellow-600'}`}>
          {isRed ? '!!' : '!'}
        </span>
        <div className="flex-1">
          <h4 className={`font-medium ${isRed ? 'text-red-700' : 'text-yellow-700'}`}>
            {redFlag.title}
          </h4>
          <p className="text-sm text-gray-600 mt-1">{redFlag.description}</p>
          {redFlag.legalBasis && (
            <p className="text-xs text-gray-500 mt-2 italic">
              Legal basis: {redFlag.legalBasis}
            </p>
          )}
        </div>
      </div>
    </div>
  );
}

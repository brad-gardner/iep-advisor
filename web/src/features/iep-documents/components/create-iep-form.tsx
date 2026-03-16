import { useState } from 'react';
import { Input, Textarea, Select } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { createIep } from '../api/iep-documents-api';

const MEETING_TYPES = [
  { value: 'initial', label: 'Initial IEP' },
  { value: 'annual_review', label: 'Annual Review' },
  { value: 'amendment', label: 'Amendment' },
  { value: 'reevaluation', label: 'Reevaluation' },
] as const;

interface CreateIepFormProps {
  childId: number;
  onCreated: () => void;
  onCancel: () => void;
}

export function CreateIepForm({ childId, onCreated, onCancel }: CreateIepFormProps) {
  const [iepDate, setIepDate] = useState('');
  const [meetingType, setMeetingType] = useState('');
  const [attendees, setAttendees] = useState('');
  const [notes, setNotes] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!iepDate || !meetingType) return;

    setIsSubmitting(true);
    setError(null);

    try {
      const response = await createIep(childId, {
        iepDate,
        meetingType,
        attendees: attendees.trim() || undefined,
        notes: notes.trim() || undefined,
      });
      if (response.success) {
        onCreated();
      } else {
        setError(response.message || 'Failed to create IEP');
      }
    } catch {
      setError('An error occurred while creating the IEP');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      {error && <Notice variant="error" title={error} />}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Input
          label="Meeting Date"
          type="date"
          value={iepDate}
          onChange={(e) => setIepDate(e.target.value)}
          required
        />
        <Select
          label="Meeting Type"
          value={meetingType}
          onChange={(e) => setMeetingType(e.target.value)}
          required
        >
          <option value="">Select type...</option>
          {MEETING_TYPES.map((t) => (
            <option key={t.value} value={t.value}>
              {t.label}
            </option>
          ))}
        </Select>
      </div>

      <div>
        <Textarea
          label="Attendees"
          placeholder="e.g. Teachers, therapists, parents present..."
          value={attendees}
          onChange={(e) => setAttendees(e.target.value)}
          rows={2}
          maxLength={1000}
        />
        <p className="text-[11px] text-brand-slate-300 mt-1">{attendees.length}/1000 characters</p>
      </div>

      <div>
        <Textarea
          label="Notes"
          placeholder="Any notes about this meeting..."
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          rows={3}
          maxLength={2000}
        />
        <p className="text-[11px] text-brand-slate-300 mt-1">{notes.length}/2000 characters</p>
      </div>

      <div className="flex gap-2">
        <Button type="submit" disabled={isSubmitting || !iepDate || !meetingType}>
          {isSubmitting ? 'Creating...' : 'Create IEP'}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

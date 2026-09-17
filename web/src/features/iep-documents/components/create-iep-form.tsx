import { useState } from 'react';
import { Input, Select } from '@/components/ui/input';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { createIep } from '../api/iep-documents-api';

const ATTENDEES_MAX_LENGTH = 1000;
const NOTES_MAX_LENGTH = 2000;

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
    if (isMarkdownOverLimit(attendees, ATTENDEES_MAX_LENGTH) || isMarkdownOverLimit(notes, NOTES_MAX_LENGTH)) return;

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
          data-testid="iep-meeting-date"
        />
        <Select
          label="Meeting Type"
          value={meetingType}
          onChange={(e) => setMeetingType(e.target.value)}
          required
          data-testid="iep-meeting-type"
        >
          <option value="">Select type...</option>
          {MEETING_TYPES.map((t) => (
            <option key={t.value} value={t.value}>
              {t.label}
            </option>
          ))}
        </Select>
      </div>

      <RichTextEditor
        label="Attendees"
        placeholder="e.g. Teachers, therapists, parents present..."
        value={attendees}
        onChange={setAttendees}
        minRows={2}
        maxLength={ATTENDEES_MAX_LENGTH}
        data-testid="iep-attendees"
      />

      <RichTextEditor
        label="Notes"
        placeholder="Any notes about this meeting..."
        value={notes}
        onChange={setNotes}
        minRows={3}
        maxLength={NOTES_MAX_LENGTH}
        data-testid="iep-notes"
      />

      <div className="flex gap-2">
        <Button
          type="submit"
          disabled={
            isSubmitting ||
            !iepDate ||
            !meetingType ||
            isMarkdownOverLimit(attendees, ATTENDEES_MAX_LENGTH) ||
            isMarkdownOverLimit(notes, NOTES_MAX_LENGTH)
          }
          data-testid="iep-create-submit"
        >
          {isSubmitting ? 'Creating...' : 'Create IEP'}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel} data-testid="iep-create-cancel">
          Cancel
        </Button>
      </div>
    </form>
  );
}

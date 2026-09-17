import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { Skeleton } from '@/components/ui/skeleton';
import { apiErrorMessage } from '@/lib/api-error';
import { createMeeting, updateMeeting } from '../api/meetings-api';
import {
  useMeetingParticipantPool,
  type ParticipantRow,
} from '../hooks/use-meeting-participant-pool';
import {
  COMMON_TIME_ZONES,
  defaultMeetingTimeZone,
  utcIsoToZonedParts,
  zonedDateTimeToUtcIso,
} from '../lib/meeting-time';
import { mergeWithMeeting, rowToParticipantInput } from '../lib/merge-participants';
import {
  DEFAULT_MEETING_DURATION_MINUTES,
  MAX_MEETING_DURATION_MINUTES,
  MEETING_TYPES,
  MEETING_TYPE_LABELS,
  MIN_MEETING_DURATION_MINUTES,
} from '../types';
import type { MeetingDto, MeetingType, ParticipantInput } from '../types';
import { ParticipantsField } from './participants-field';

interface ScheduleMeetingFormProps {
  studentId: number;
  /** Present for reschedule/edit; absent for a fresh "schedule meeting". */
  meeting?: MeetingDto;
  onSaved: (meeting: MeetingDto) => void;
  onCancel: () => void;
  /** Lets the host dialog block dismissal while a save is in flight. */
  onSubmittingChange?: (submitting: boolean) => void;
}

/**
 * The schedule/reschedule form body (hosted by `ScheduleMeetingModal` in a
 * `Drawer`). Converts the chosen local date/time + time zone to UTC on
 * submit, and always sends an explicit `participants` array so the checklist
 * below is the single source of truth (see `use-meeting-participant-pool` for
 * why family members are carried as external-by-email rows).
 */
export function ScheduleMeetingForm({
  studentId,
  meeting,
  onSaved,
  onCancel,
  onSubmittingChange,
}: ScheduleMeetingFormProps) {
  const { rows: pool, failed: poolFailed } = useMeetingParticipantPool(studentId);
  const [rows, setRows] = useState<ParticipantRow[] | null>(null);

  // Seed the editable row list once the pool arrives — computed during render
  // (the same "adjust state in response to a change" pattern used by
  // `roster-filters.tsx`), not via an effect, so there's no synchronous
  // setState-in-effect and no extra render pass. `ScheduleMeetingModal`
  // mounts a fresh form per open (the Drawer unmounts closed children), so
  // `meeting` never changes under an already-seeded instance.
  if (rows === null && pool !== null) {
    setRows(mergeWithMeeting(pool, meeting));
  }

  const initialZoned = meeting ? utcIsoToZonedParts(meeting.startsAtUtc, meeting.timeZoneId) : null;
  const [type, setType] = useState<MeetingType>(meeting?.type ?? 'AnnualReview');
  const [title, setTitle] = useState(meeting?.title ?? '');
  const [date, setDate] = useState(initialZoned?.date ?? '');
  const [time, setTime] = useState(initialZoned?.time ?? '');
  const [timeZoneId, setTimeZoneId] = useState(meeting?.timeZoneId ?? defaultMeetingTimeZone());
  const [durationMinutes, setDurationMinutes] = useState(
    meeting?.durationMinutes ?? DEFAULT_MEETING_DURATION_MINUTES
  );
  const [location, setLocation] = useState(meeting?.location ?? '');
  const [videoUrl, setVideoUrl] = useState(meeting?.videoUrl ?? '');
  const [notes, setNotes] = useState(meeting?.notes ?? '');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmittingLocal] = useState(false);
  const setSubmitting = (value: boolean) => {
    setSubmittingLocal(value);
    onSubmittingChange?.(value);
  };

  const toggleRow = (key: string) => {
    setRows((prev) => prev?.map((r) => (r.key === key ? { ...r, checked: !r.checked } : r)) ?? prev);
  };
  const changeRequired = (key: string, isRequired: boolean) => {
    setRows((prev) => prev?.map((r) => (r.key === key ? { ...r, isRequired } : r)) ?? prev);
  };
  const addExternal = ({ name, email }: { name: string; email: string }) => {
    const key = `external-${Date.now()}-${name}-${email}`;
    setRows((prev) => [
      ...(prev ?? []),
      {
        key,
        kind: 'external',
        displayName: name || email,
        externalName: name || undefined,
        externalEmail: email || undefined,
        teamRole: 'Other',
        isRequired: true,
        checked: true,
      },
    ]);
  };
  const removeExternal = (key: string) => {
    setRows((prev) => prev?.filter((r) => r.key !== key) ?? prev);
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setError(null);
    if (rows === null) {
      // Belt-and-suspenders: the submit button is disabled while the pool is
      // still loading, but a keyboard Enter inside a text field submits the
      // form directly without going through the (disabled) button, so this
      // guards against ever sending an explicit `participants: []` that the
      // server would take literally instead of computing its own defaults.
      setError('The participant list is still loading — try again in a moment.');
      return;
    }
    if (!date || !time) {
      setError('Choose a date and time for the meeting');
      return;
    }
    setSubmitting(true);
    try {
      const startsAtUtc = zonedDateTimeToUtcIso(date, time, timeZoneId);
      const participants: ParticipantInput[] = rows.filter((r) => r.checked).map(rowToParticipantInput);
      const payload = {
        type,
        title: title.trim() || undefined,
        startsAtUtc,
        timeZoneId,
        durationMinutes,
        location: location.trim() || undefined,
        videoUrl: videoUrl.trim() || undefined,
        notes: notes.trim() || undefined,
        participants,
      };
      const response = meeting
        ? await updateMeeting(meeting.id, payload)
        : await createMeeting(studentId, payload);
      if (response.success && response.data) {
        onSaved(response.data);
      } else {
        setError(response.message ?? 'Could not save the meeting');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not save the meeting'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      <Select
        id="meeting-type"
        label="Meeting type"
        value={type}
        onChange={(e) => setType(e.target.value as MeetingType)}
      >
        {MEETING_TYPES.map((t) => (
          <option key={t} value={t}>
            {MEETING_TYPE_LABELS[t]}
          </option>
        ))}
      </Select>

      <Input
        id="meeting-title"
        label="Title"
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        placeholder="Optional"
      />

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Input
          id="meeting-date"
          label="Date"
          type="date"
          value={date}
          onChange={(e) => setDate(e.target.value)}
        />
        <Input
          id="meeting-time"
          label="Time"
          type="time"
          value={time}
          onChange={(e) => setTime(e.target.value)}
        />
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Select
          id="meeting-timezone"
          label="Time zone"
          value={timeZoneId}
          onChange={(e) => setTimeZoneId(e.target.value)}
        >
          {COMMON_TIME_ZONES.map((tz) => (
            <option key={tz.id} value={tz.id}>
              {tz.label}
            </option>
          ))}
        </Select>
        <Input
          id="meeting-duration"
          label="Duration (minutes)"
          type="number"
          min={MIN_MEETING_DURATION_MINUTES}
          max={MAX_MEETING_DURATION_MINUTES}
          step={15}
          value={durationMinutes}
          onChange={(e) => setDurationMinutes(Number(e.target.value))}
        />
      </div>

      <Input
        id="meeting-location"
        label="Location"
        value={location}
        onChange={(e) => setLocation(e.target.value)}
        placeholder="Optional"
      />
      <Input
        id="meeting-video-url"
        label="Video call URL"
        type="url"
        value={videoUrl}
        onChange={(e) => setVideoUrl(e.target.value)}
        placeholder="Optional"
      />
      <RichTextEditor
        id="meeting-notes"
        label="Notes"
        value={notes}
        onChange={setNotes}
        minRows={3}
        placeholder="Optional"
      />

      {poolFailed && (
        <Notice variant="warning" title="Could not load the participant directory">
          You can still add participants manually below.
        </Notice>
      )}
      {rows === null ? (
        <div className="space-y-2">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      ) : (
        <ParticipantsField
          rows={rows}
          onToggle={toggleRow}
          onRequiredChange={changeRequired}
          onAddExternal={addExternal}
          onRemoveExternal={removeExternal}
        />
      )}

      <div className="flex items-center justify-end gap-2 pt-2">
        <Button type="button" variant="ghost" onClick={onCancel} disabled={submitting}>
          Cancel
        </Button>
        <Button
          type="submit"
          loading={submitting}
          disabled={rows === null}
          data-testid="schedule-meeting-submit"
        >
          {meeting ? 'Save changes' : 'Schedule meeting'}
        </Button>
      </div>
    </form>
  );
}

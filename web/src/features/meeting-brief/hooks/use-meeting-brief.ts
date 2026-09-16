import { useCallback, useEffect, useRef, useState } from 'react';
import { AxiosError } from 'axios';
import { apiErrorMessage } from '@/lib/api-error';
import { generateBrief, getBrief } from '../api/meeting-brief-api';
import type { MeetingBriefDto } from '../types';

interface UseMeetingBriefResult {
  brief: MeetingBriefDto | null;
  /** True only until the first load settles. */
  isLoading: boolean;
  /** True when the load settled with a 404 — "no brief yet" is a valid state,
   *  distinct from `error` (a genuine failure). */
  notFound: boolean;
  error: string | null;
  isGenerating: boolean;
  generateError: string | null;
  regenerate: () => Promise<void>;
}

/** A meeting's pre-meeting brief (plan 7, decision 2): loads once, and offers
 *  a generate/regenerate action that replaces it in place. */
export function useMeetingBrief(meetingId: number): UseMeetingBriefResult {
  const [brief, setBrief] = useState<MeetingBriefDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isGenerating, setIsGenerating] = useState(false);
  const [generateError, setGenerateError] = useState<string | null>(null);

  // The meeting this hook currently shows; a regenerate that resolves after the route moved to
  // another meeting must not overwrite that meeting's brief (the page is not remounted per id).
  const currentMeetingRef = useRef(meetingId);
  // A meeting switch also resets the generate state during render (the abandoned request's
  // `finally` deliberately leaves it alone), so the new meeting never inherits a stuck spinner
  // or the previous meeting's error.
  const [seenMeetingId, setSeenMeetingId] = useState(meetingId);
  if (meetingId !== seenMeetingId) {
    setSeenMeetingId(meetingId);
    setIsGenerating(false);
    setGenerateError(null);
  }
  useEffect(() => {
    if (!meetingId) return;
    let active = true;
    currentMeetingRef.current = meetingId;
    (async () => {
      try {
        const res = await getBrief(meetingId);
        if (!active) return;
        if (res.success && res.data) {
          setBrief(res.data);
          setNotFound(false);
          setError(null);
        } else {
          setError(res.message ?? 'Could not load the brief.');
        }
      } catch (err) {
        if (!active) return;
        if (err instanceof AxiosError && err.response?.status === 404) {
          setNotFound(true);
          setError(null);
        } else {
          setError(apiErrorMessage(err, 'Could not load the brief.'));
        }
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [meetingId]);

  // Only the most recently started regenerate may apply its result or clear the spinner: a
  // switch-away-and-back re-arms the button (see the render-time reset above), so an older
  // request for the same meeting can still be outstanding when a newer one starts.
  const regenerateTokenRef = useRef(0);
  const regenerate = useCallback(async () => {
    const token = ++regenerateTokenRef.current;
    const isCurrent = () => currentMeetingRef.current === meetingId && regenerateTokenRef.current === token;
    setIsGenerating(true);
    setGenerateError(null);
    try {
      const res = await generateBrief(meetingId);
      if (!isCurrent()) return; // superseded by a meeting switch or a newer regenerate
      if (res.success && res.data) {
        setBrief(res.data);
        setNotFound(false);
        setError(null);
      } else {
        setGenerateError(res.message ?? 'Could not generate the brief.');
      }
    } catch (err) {
      if (!isCurrent()) return;
      setGenerateError(apiErrorMessage(err, 'Could not generate the brief.'));
    } finally {
      if (isCurrent()) setIsGenerating(false);
    }
  }, [meetingId]);

  return { brief, isLoading, notFound, error, isGenerating, generateError, regenerate };
}

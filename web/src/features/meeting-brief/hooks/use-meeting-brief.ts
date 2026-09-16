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

  const regenerate = useCallback(async () => {
    setIsGenerating(true);
    setGenerateError(null);
    try {
      const res = await generateBrief(meetingId);
      if (currentMeetingRef.current !== meetingId) return; // superseded by a meeting switch
      if (res.success && res.data) {
        setBrief(res.data);
        setNotFound(false);
        setError(null);
      } else {
        setGenerateError(res.message ?? 'Could not generate the brief.');
      }
    } catch (err) {
      if (currentMeetingRef.current !== meetingId) return;
      setGenerateError(apiErrorMessage(err, 'Could not generate the brief.'));
    } finally {
      if (currentMeetingRef.current === meetingId) setIsGenerating(false);
    }
  }, [meetingId]);

  return { brief, isLoading, notFound, error, isGenerating, generateError, regenerate };
}

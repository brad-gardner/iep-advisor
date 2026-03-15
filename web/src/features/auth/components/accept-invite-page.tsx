import { useEffect, useState } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { acceptInvite } from '@/features/sharing/api/sharing-api';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';

export function AcceptInvitePage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');
  const [status, setStatus] = useState<'loading' | 'success' | 'error'>('loading');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      setStatus('error');
      setErrorMessage('No invite token provided.');
      return;
    }

    async function accept() {
      try {
        const response = await acceptInvite(token!);
        if (response.success) {
          setStatus('success');
        } else {
          setStatus('error');
          setErrorMessage(response.message || 'Failed to accept invite.');
        }
      } catch {
        setStatus('error');
        setErrorMessage('An error occurred while accepting the invite.');
      }
    }

    accept();
  }, [token]);

  return (
    <div className="max-w-md mx-auto py-12">
      <Card className="text-center">
        <h1 className="font-serif mb-4">Accept Invite</h1>

        {status === 'loading' && (
          <div className="flex justify-center py-6">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
          </div>
        )}

        {status === 'success' && (
          <div className="space-y-4">
            <Notice variant="success" title="Invite accepted!">
              You now have access to the shared child profile.
            </Notice>
            <Link to="/dashboard">
              <Button>Go to Dashboard</Button>
            </Link>
          </div>
        )}

        {status === 'error' && (
          <div className="space-y-4">
            <Notice variant="error" title={errorMessage || 'Something went wrong'} />
            <Link to="/dashboard">
              <Button variant="secondary">Go to Dashboard</Button>
            </Link>
          </div>
        )}
      </Card>
    </div>
  );
}

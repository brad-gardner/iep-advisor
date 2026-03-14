import { useState } from 'react';
import { useAuth } from '../hooks/use-auth';
import { StateSelector } from './state-selector';

export function ProfilePage() {
  const { user, updateProfile } = useAuth();
  const [firstName, setFirstName] = useState(user?.firstName ?? '');
  const [lastName, setLastName] = useState(user?.lastName ?? '');
  const [state, setState] = useState(user?.state ?? '');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setMessage(null);

    const result = await updateProfile({
      firstName,
      lastName,
      state: state || undefined,
    });

    if (result.success) {
      setMessage({ type: 'success', text: 'Profile updated successfully' });
    } else {
      setMessage({ type: 'error', text: result.error ?? 'Update failed' });
    }

    setIsSubmitting(false);
  };

  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-bold text-gray-900">Profile</h1>

      <form onSubmit={handleSubmit} className="bg-white rounded-lg p-6 max-w-lg space-y-4 shadow-sm border border-gray-200">
        {message && (
          <div
            className={`p-3 rounded text-sm ${
              message.type === 'success'
                ? 'bg-green-50 text-green-600'
                : 'bg-red-50 text-red-600'
            }`}
          >
            {message.text}
          </div>
        )}

        <div>
          <label htmlFor="email" className="block text-sm text-gray-500 mb-1">
            Email
          </label>
          <input
            id="email"
            type="text"
            value={user?.email ?? ''}
            disabled
            className="w-full px-3 py-2 bg-gray-100 rounded text-gray-500 cursor-not-allowed border border-gray-200"
          />
        </div>

        <div>
          <label htmlFor="firstName" className="block text-sm text-gray-500 mb-1">
            First Name
          </label>
          <input
            id="firstName"
            type="text"
            value={firstName}
            onChange={(e) => setFirstName(e.target.value)}
            className="w-full px-3 py-2 bg-white rounded text-gray-900 border border-gray-300 focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        <div>
          <label htmlFor="lastName" className="block text-sm text-gray-500 mb-1">
            Last Name
          </label>
          <input
            id="lastName"
            type="text"
            value={lastName}
            onChange={(e) => setLastName(e.target.value)}
            className="w-full px-3 py-2 bg-white rounded text-gray-900 border border-gray-300 focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        <div>
          <label htmlFor="state" className="block text-sm text-gray-500 mb-1">
            State / Jurisdiction
          </label>
          <StateSelector value={state} onChange={setState} />
          <p className="text-xs text-gray-400 mt-1">
            Used to provide state-specific IEP guidance and regulations
          </p>
        </div>

        <button
          type="submit"
          disabled={isSubmitting}
          className="w-full py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 rounded font-medium text-white transition-colors"
        >
          {isSubmitting ? 'Saving...' : 'Save Changes'}
        </button>
      </form>
    </div>
  );
}

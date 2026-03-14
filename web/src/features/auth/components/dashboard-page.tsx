import { Link } from 'react-router-dom';
import { useAuth } from '../hooks/use-auth';

export function DashboardPage() {
  const { user } = useAuth();

  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-bold text-gray-900">Dashboard</h1>

      <div className="bg-white rounded-lg p-6 shadow-sm border border-gray-200">
        <h2 className="text-xl font-semibold mb-4 text-gray-900">Welcome, {user?.firstName}!</h2>
        {!user?.state && (
          <div className="bg-yellow-50 border border-yellow-400 rounded p-3 mb-4">
            <p className="text-yellow-700 text-sm">
              Set your state in{' '}
              <Link to="/profile" className="underline hover:text-yellow-800">
                your profile
              </Link>{' '}
              to get jurisdiction-specific IEP guidance.
            </p>
          </div>
        )}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-gray-50 rounded p-4 border border-gray-200">
            <p className="text-gray-500 text-sm">Email</p>
            <p className="font-medium text-gray-900">{user?.email}</p>
          </div>
          <div className="bg-gray-50 rounded p-4 border border-gray-200">
            <p className="text-gray-500 text-sm">State</p>
            <p className="font-medium text-gray-900">{user?.state || 'Not set'}</p>
          </div>
          <Link to="/children" className="bg-gray-50 rounded p-4 border border-gray-200 hover:bg-gray-100 transition-colors block">
            <p className="text-gray-500 text-sm">Children</p>
            <p className="font-medium text-blue-600">Manage profiles</p>
          </Link>
        </div>
      </div>

      <div className="bg-white rounded-lg p-6 shadow-sm border border-gray-200">
        <h2 className="text-xl font-semibold mb-4 text-gray-900">Quick Actions</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          <Link
            to="/children/new"
            className="bg-gray-50 rounded p-4 text-left hover:bg-gray-100 transition-colors block border border-gray-200"
          >
            <p className="font-medium text-gray-900">Add Child Profile</p>
            <p className="text-gray-500 text-sm">Create a profile for your child</p>
          </Link>
          <button
            disabled
            className="bg-gray-50 rounded p-4 text-left opacity-50 cursor-not-allowed border border-gray-200"
          >
            <p className="font-medium text-gray-900">Upload IEP</p>
            <p className="text-gray-500 text-sm">Coming soon</p>
          </button>
        </div>
      </div>
    </div>
  );
}

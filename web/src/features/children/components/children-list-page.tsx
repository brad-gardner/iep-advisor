import { Link } from 'react-router-dom';
import { useChildren } from '../hooks/use-children';

export function ChildrenListPage() {
  const { children, isLoading } = useChildren();

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-gray-900" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-3xl font-bold text-gray-900">Children</h1>
        <Link
          to="/children/new"
          className="px-4 py-2 bg-blue-600 hover:bg-blue-700 rounded font-medium text-white transition-colors"
        >
          Add Child
        </Link>
      </div>

      {children.length === 0 ? (
        <div className="bg-white rounded-lg p-8 text-center shadow-sm border border-gray-200">
          <p className="text-gray-500 mb-4">No child profiles yet.</p>
          <Link
            to="/children/new"
            className="px-4 py-2 bg-blue-600 hover:bg-blue-700 rounded font-medium text-white transition-colors"
          >
            Add Your First Child
          </Link>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {children.map((child) => (
            <Link
              key={child.id}
              to={`/children/${child.id}`}
              className="bg-white rounded-lg p-5 hover:bg-gray-50 transition-colors block shadow-sm border border-gray-200"
            >
              <h3 className="text-lg font-semibold text-gray-900">
                {child.firstName} {child.lastName}
              </h3>
              <div className="mt-2 flex flex-wrap gap-3 text-sm text-gray-500">
                {child.gradeLevel && <span>Grade: {child.gradeLevel}</span>}
                {child.disabilityCategory && <span>{child.disabilityCategory}</span>}
                {child.schoolDistrict && <span>{child.schoolDistrict}</span>}
              </div>
              <p className="mt-3 text-sm text-gray-400">0 IEPs uploaded</p>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}

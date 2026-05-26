import { useEffect, useState } from "react";
import { Navigate, useParams } from "react-router-dom";
import { getById } from "../api/etr-documents-api";

export function EtrRouteRedirect() {
  const { id } = useParams<{ id: string }>();
  const [childId, setChildId] = useState<number | null>(null);
  const [notFound, setNotFound] = useState(false);

  useEffect(() => {
    if (!id) return;
    getById(Number(id))
      .then((res) => {
        if (res.success && res.data) {
          setChildId(res.data.childProfileId);
        } else {
          setNotFound(true);
        }
      })
      .catch(() => setNotFound(true));
  }, [id]);

  if (notFound) {
    return <Navigate to="/dashboard" replace />;
  }
  if (childId == null) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }
  return <Navigate to={`/children/${childId}/etrs/${id}`} replace />;
}

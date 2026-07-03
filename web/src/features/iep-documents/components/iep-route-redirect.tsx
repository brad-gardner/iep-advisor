import { useEffect, useState } from "react";
import { Navigate, useParams } from "react-router-dom";
import { getIepDocument } from "../api/iep-documents-api";
import { Spinner } from "@/components/ui/spinner";

export function IepRouteRedirect() {
  const { id } = useParams<{ id: string }>();
  const [childId, setChildId] = useState<number | null>(null);
  const [notFound, setNotFound] = useState(false);

  useEffect(() => {
    if (!id) return;
    getIepDocument(Number(id))
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
        <Spinner label="Loading document…" />
      </div>
    );
  }
  return <Navigate to={`/children/${childId}/ieps/${id}`} replace />;
}

namespace IepAssistant.Domain.Entities;

/// <summary>
/// Print/sign status of a finalized <see cref="AuthoredDocumentVersion"/> (plan 7, decision 4). Typed-name
/// e-sign is explicitly not claimed — this reflects whether a scanned/uploaded signed copy
/// (<see cref="SignedArtifact"/>) has been attached, not an in-app signature capture. Stored as a string.
/// </summary>
public enum SignatureStatus
{
    Unsigned,
    PartiallySigned,
    Signed
}

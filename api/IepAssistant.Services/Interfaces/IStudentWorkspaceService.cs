using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// P8a. The student self-advocacy workspace. The student (a Role=Student user with a StudentProfile)
/// owns exactly one workspace, auto-created on first access. Entries are PRIVATE until the student marks
/// them shareable; only shareable entries are ever returned to an educator or parent. There is no backend
/// "pull" surface — pull-into-IEP / pull-into-MeetingPrep are frontend copy-by-value actions (P8b), so a
/// pulled copy is an independent snapshot with no FK to the entry.
/// </summary>
public interface IStudentWorkspaceService
{
    // ---- Student (owner) ----

    /// <summary>Resolves the caller's workspace (auto-create) with ALL their own entries (private + shareable).</summary>
    Task<ServiceResult<StudentWorkspaceModel>> GetMyWorkspaceAsync(int studentUserId, CancellationToken ct = default);

    Task<ServiceResult<StudentWorkspaceEntryModel>> AddEntryAsync(int studentUserId, StudentEntryKind kind, string content, bool isShareable, CancellationToken ct = default);

    Task<ServiceResult<StudentWorkspaceEntryModel>> UpdateEntryAsync(int studentUserId, int entryId, string content, bool isShareable, CancellationToken ct = default);

    Task<ServiceResult> DeleteEntryAsync(int studentUserId, int entryId, CancellationToken ct = default);

    // ---- Educator / parent reads (SHAREABLE ONLY) ----

    /// <summary>Educator: only IsShareable entries of the student linked to <paramref name="schoolStudentId"/>; SchoolId-bound access required.</summary>
    Task<ServiceResult<List<StudentWorkspaceEntryModel>>> GetShareableEntriesForSchoolStudentAsync(int educatorUserId, int schoolStudentId, CancellationToken ct = default);

    /// <summary>Parent: only IsShareable entries of the student linked to <paramref name="childProfileId"/>; AccessService (Viewer+) required.</summary>
    Task<ServiceResult<List<StudentWorkspaceEntryModel>>> GetShareableEntriesForChildAsync(int parentUserId, int childProfileId, CancellationToken ct = default);

    // ---- Optional AI interview ----

    /// <summary>Turns the student's freeform prompt into a polished first-person self-advocacy statement (suggestion only; NOT saved).</summary>
    Task<ServiceResult<StudentInterviewSuggestionModel>> InterviewSuggestAsync(int studentUserId, string prompt, CancellationToken ct = default);
}

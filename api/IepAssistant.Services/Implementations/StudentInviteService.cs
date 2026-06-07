using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using IepAssistant.Services.Security;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// P7a student role + invite + consent. Mirrors the SHA-token pattern from <see cref="ChildLinkService"/>:
/// a 32-byte raw token is emailed, only its SHA256 hash is stored, accept hashes the inbound token to match,
/// the invite is email-bound (case-insensitive), and the token is single-use (cleared on accept).
///
/// Convergence: the StudentProfile is keyed one-per-UserId, so a parent-initiated invite (ChildProfileId)
/// and an educator-initiated invite (SchoolStudentId) accepted by the SAME student user both land on the one
/// profile — the first accept sets one side, the second sets the other. A second invite for a DIFFERENT
/// child/student on an already-linked side is rejected ("exactly one pair").
/// </summary>
public class StudentInviteService : IStudentInviteService
{
    private const int InviteExpiryDays = 14;

    private readonly ApplicationDbContext _context;
    private readonly IAccessService _accessService;
    private readonly IOrgAccessService _orgAccess;
    private readonly IEmailService _emailService;
    private readonly ILogger<StudentInviteService> _logger;

    public StudentInviteService(
        ApplicationDbContext context,
        IAccessService accessService,
        IOrgAccessService orgAccess,
        IEmailService emailService,
        ILogger<StudentInviteService> logger)
    {
        _context = context;
        _accessService = accessService;
        _orgAccess = orgAccess;
        _emailService = emailService;
        _logger = logger;
    }

    // ----------------------------------------------------------------- Parent: invite

    public async Task<ServiceResult<StudentInviteModel>> InviteFromParentAsync(int parentUserId, int childProfileId, string studentEmail, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(studentEmail))
            return ServiceResult<StudentInviteModel>.FailureResult("Student email is required.");

        studentEmail = studentEmail.Trim();

        var isOwner = await _accessService.HasMinimumRoleAsync(childProfileId, parentUserId, AccessRole.Owner, ct);
        if (!isOwner)
            return ServiceResult<StudentInviteModel>.FailureResult("You do not have permission to invite a student for this child.");

        var now = DateTime.UtcNow;

        // Idempotency: an active, pending, unexpired invite for the same (email, child) already exists → return it.
        var existing = await _context.StudentInvites
            .FirstOrDefaultAsync(i => i.ChildProfileId == childProfileId
                                   && i.InviteEmail == studentEmail
                                   && i.IsActive
                                   && i.AcceptedAt == null
                                   && i.InviteExpiresAt > now, ct);
        if (existing != null)
            return ServiceResult<StudentInviteModel>.SuccessResult(MapToModel(existing), "A pending invite already exists.");

        var rawToken = InviteTokenHelper.Generate();
        var invite = new StudentInvite
        {
            ChildProfileId = childProfileId,
            SchoolStudentId = null,
            InvitedByUserId = parentUserId,
            InviteEmail = studentEmail,
            InviteToken = InviteTokenHelper.Hash(rawToken),
            InviteExpiresAt = now.AddDays(InviteExpiryDays),
            IsActive = true,
            CreatedById = parentUserId,
            UpdatedById = parentUserId
        };
        await _context.StudentInvites.AddAsync(invite, ct);
        await _context.SaveChangesAsync(ct);

        var inviter = await _context.Users.FindAsync(new object[] { parentUserId }, ct);
        var inviterName = inviter != null ? inviter.FullName.Trim() : "A parent";
        var childFirstName = await _context.ChildProfiles
            .Where(c => c.Id == childProfileId)
            .Select(c => c.FirstName)
            .FirstOrDefaultAsync(ct);
        var context = $"to contribute to {childFirstName}'s IEP";

        await _emailService.SendStudentInviteEmailAsync(studentEmail, inviterName, context, rawToken, ct);

        _logger.LogInformation("Parent {ParentUserId} invited student {Email} for child {ChildId}", parentUserId, studentEmail, childProfileId);

        return ServiceResult<StudentInviteModel>.SuccessResult(MapToModel(invite), "Invite sent successfully.");
    }

    // ----------------------------------------------------------------- Educator: invite

    public async Task<ServiceResult<StudentInviteModel>> InviteFromEducatorAsync(int educatorUserId, int schoolStudentId, string studentEmail, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(studentEmail))
            return ServiceResult<StudentInviteModel>.FailureResult("Student email is required.");

        studentEmail = studentEmail.Trim();

        var access = await GetEducatorStudentAccessAsync(educatorUserId, schoolStudentId, ct);
        if (access.student == null)
            return ServiceResult<StudentInviteModel>.FailureResult("You do not have permission to invite a student for this record.");

        var student = access.student;
        var now = DateTime.UtcNow;

        // Idempotency: an active, pending, unexpired invite for the same (email, school student) → return it.
        var existing = await _context.StudentInvites
            .FirstOrDefaultAsync(i => i.SchoolStudentId == schoolStudentId
                                   && i.InviteEmail == studentEmail
                                   && i.IsActive
                                   && i.AcceptedAt == null
                                   && i.InviteExpiresAt > now, ct);
        if (existing != null)
            return ServiceResult<StudentInviteModel>.SuccessResult(MapToModel(existing), "A pending invite already exists.");

        var rawToken = InviteTokenHelper.Generate();
        var invite = new StudentInvite
        {
            ChildProfileId = null,
            SchoolStudentId = schoolStudentId,
            InvitedByUserId = educatorUserId,
            InviteEmail = studentEmail,
            InviteToken = InviteTokenHelper.Hash(rawToken),
            InviteExpiresAt = now.AddDays(InviteExpiryDays),
            IsActive = true,
            CreatedById = educatorUserId,
            UpdatedById = educatorUserId
        };
        await _context.StudentInvites.AddAsync(invite, ct);
        await _context.SaveChangesAsync(ct);

        var inviter = await _context.Users.FindAsync(new object[] { educatorUserId }, ct);
        var schoolName = await _context.Schools
            .Where(s => s.Id == student.SchoolId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(ct);
        var inviterName = inviter != null ? inviter.FullName.Trim() : "An educator";
        var context = $"at {schoolName ?? "your school"}";

        await _emailService.SendStudentInviteEmailAsync(studentEmail, inviterName, context, rawToken, ct);

        _logger.LogInformation("Educator {EducatorUserId} invited student {Email} for school student {StudentId}", educatorUserId, studentEmail, schoolStudentId);

        return ServiceResult<StudentInviteModel>.SuccessResult(MapToModel(invite), "Invite sent successfully.");
    }

    // ----------------------------------------------------------------- Student: preview

    public async Task<ServiceResult<StudentInvitePreviewModel>> PreviewInviteAsync(int userId, string token, CancellationToken ct = default)
    {
        var invite = await FindActiveInviteAsync(token, ct);
        if (invite == null)
            return ServiceResult<StudentInvitePreviewModel>.FailureResult("Invalid or expired invite token.");

        var emailCheck = await VerifyEmailMatchAsync(userId, invite, ct);
        if (!emailCheck.Success)
            return ServiceResult<StudentInvitePreviewModel>.FailureResult(emailCheck.Message!);

        var preview = new StudentInvitePreviewModel { InviteExpiresAt = invite.InviteExpiresAt };

        if (invite.ChildProfileId.HasValue)
        {
            var firstName = await _context.ChildProfiles
                .Where(c => c.Id == invite.ChildProfileId.Value)
                .Select(c => c.FirstName)
                .FirstOrDefaultAsync(ct);
            if (firstName == null)
                return ServiceResult<StudentInvitePreviewModel>.FailureResult("The invited record was not found.");
            preview.InviteSource = "Parent";
            preview.LinkedToFirstName = firstName;
        }
        else if (invite.SchoolStudentId.HasValue)
        {
            var student = await _context.SchoolStudents
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == invite.SchoolStudentId.Value, ct);
            if (student == null)
                return ServiceResult<StudentInvitePreviewModel>.FailureResult("The invited record was not found.");
            preview.InviteSource = "Educator";
            preview.LinkedToFirstName = student.FirstName;
            preview.SchoolName = await _context.Schools
                .Where(s => s.Id == student.SchoolId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(ct);
        }
        else
        {
            return ServiceResult<StudentInvitePreviewModel>.FailureResult("Invalid or expired invite token.");
        }

        return ServiceResult<StudentInvitePreviewModel>.SuccessResult(preview);
    }

    // ----------------------------------------------------------------- Student: accept (consent gate + convergence)

    public async Task<ServiceResult<AcceptStudentInviteModel>> AcceptInviteAsync(int studentUserId, string token, bool consentAccepted, CancellationToken ct = default)
    {
        // CONSENT GATE — do NOT activate the account without explicit consent.
        if (!consentAccepted)
            return ServiceResult<AcceptStudentInviteModel>.FailureResult("Consent is required to activate your student account.");

        var invite = await FindActiveInviteAsync(token, ct);
        if (invite == null)
            return ServiceResult<AcceptStudentInviteModel>.FailureResult("Invalid or expired invite token.");

        var user = await _context.Users.FindAsync(new object[] { studentUserId }, ct);
        if (user == null)
            return ServiceResult<AcceptStudentInviteModel>.FailureResult("User not found.");

        if (!string.IsNullOrEmpty(invite.InviteEmail) &&
            !string.Equals(user.Email, invite.InviteEmail, StringComparison.OrdinalIgnoreCase))
            return ServiceResult<AcceptStudentInviteModel>.FailureResult("This invite was sent to a different email address.");

        var now = DateTime.UtcNow;

        // Find-or-create the user's single StudentProfile (unique per UserId).
        var profile = await _context.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == studentUserId, ct);
        if (profile == null)
        {
            profile = new StudentProfile
            {
                UserId = studentUserId,
                CreatedById = studentUserId,
                UpdatedById = studentUserId
            };
            await _context.StudentProfiles.AddAsync(profile, ct);
        }

        // One-pair guard: reject linking a DIFFERENT child/student onto an already-linked side BEFORE we
        // consume the token, so a rejected accept never spends the invite.
        if (invite.ChildProfileId.HasValue
            && profile.ChildProfileId.HasValue
            && profile.ChildProfileId.Value != invite.ChildProfileId.Value)
            return ServiceResult<AcceptStudentInviteModel>.FailureResult("Your student account is already linked to a different child.");

        if (invite.SchoolStudentId.HasValue
            && profile.SchoolStudentId.HasValue
            && profile.SchoolStudentId.Value != invite.SchoolStudentId.Value)
            return ServiceResult<AcceptStudentInviteModel>.FailureResult("Your student account is already linked to a different school record.");

        // Same-person guard: when this invite would set the SECOND side, the two sides must describe
        // the SAME real student — i.e. an active accepted ChildLink already pairs them. Otherwise a
        // parent invite for child A and an unrelated educator invite for school-student X (both sent to
        // this email) could fuse two unrelated children's records onto one workspace.
        var addingChildToExistingSchool = invite.ChildProfileId.HasValue && profile.SchoolStudentId.HasValue;
        var addingSchoolToExistingChild = invite.SchoolStudentId.HasValue && profile.ChildProfileId.HasValue;
        if (addingChildToExistingSchool || addingSchoolToExistingChild)
        {
            var childId = invite.ChildProfileId ?? profile.ChildProfileId!.Value;
            var schoolStudentId = invite.SchoolStudentId ?? profile.SchoolStudentId!.Value;
            var sidesArePaired = await _context.ChildLinks.AnyAsync(
                l => l.ChildProfileId == childId && l.SchoolStudentId == schoolStudentId
                  && l.IsActive && l.AcceptedAt != null, ct);
            if (!sidesArePaired)
                return ServiceResult<AcceptStudentInviteModel>.FailureResult(
                    "This invite is for a different student than your account is already linked to.");
        }

        // Atomically claim the token (single-use) BEFORE writing the links, so two concurrent accepts
        // of the same invite can't both proceed. Loser sees 0 rows → invalid/expired.
        var tokenHash = InviteTokenHelper.Hash(token);
        var claimed = await _context.StudentInvites
            .Where(i => i.Id == invite.Id && i.InviteToken == tokenHash && i.AcceptedAt == null && i.IsActive)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.AcceptedAt, now)
                .SetProperty(i => i.InviteToken, (string?)null), ct);
        if (claimed == 0)
            return ServiceResult<AcceptStudentInviteModel>.FailureResult("Invalid or expired invite token.");
        // Keep the tracked entity consistent with the out-of-band claim.
        invite.AcceptedAt = now;
        invite.InviteToken = null;
        invite.UpdatedById = studentUserId;

        // Activate: single-role model — accepting flips the user's Role Parent->Student.
        user.Role = UserRole.Student;
        user.UpdatedById = studentUserId;

        if (profile.ConsentAcceptedAt == null)
            profile.ConsentAcceptedAt = now;
        profile.UpdatedById = studentUserId;

        // Link the invite's side onto the profile (idempotent: re-setting the same value is a no-op).
        if (invite.ChildProfileId.HasValue)
            profile.ChildProfileId = invite.ChildProfileId.Value;
        if (invite.SchoolStudentId.HasValue)
            profile.SchoolStudentId = invite.SchoolStudentId.Value;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Student {UserId} accepted invite {InviteId}; profile {ProfileId} now child={ChildId} schoolStudent={SchoolStudentId}",
            studentUserId, invite.Id, profile.Id, profile.ChildProfileId, profile.SchoolStudentId);

        return ServiceResult<AcceptStudentInviteModel>.SuccessResult(new AcceptStudentInviteModel
        {
            StudentProfileId = profile.Id,
            ChildProfileId = profile.ChildProfileId,
            SchoolStudentId = profile.SchoolStudentId,
            ConsentAcceptedAt = profile.ConsentAcceptedAt
        }, "Student account activated.");
    }

    // ----------------------------------------------------------------- Helpers

    /// <summary>
    /// Org access check delegated to <see cref="IOrgAccessService"/> (player-coach: admins pass within
    /// scope; teachers need an active SchoolStudentAccess). Loads the (tracked) student when permitted;
    /// returns <c>(null, _)</c> otherwise. The <c>schoolId</c> tuple member is retained for signature
    /// compatibility with existing callers (currently unused by them).
    /// </summary>
    private async Task<(SchoolStudent? student, int schoolId)> GetEducatorStudentAccessAsync(int educatorUserId, int studentId, CancellationToken ct)
    {
        if (!await _orgAccess.CanActOnStudentAsync(educatorUserId, studentId, AccessRole.Viewer, ct))
            return (null, 0);

        var student = await _context.SchoolStudents
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student == null)
            return (null, 0);

        return (student, student.SchoolId);
    }

    private async Task<StudentInvite?> FindActiveInviteAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var tokenHash = InviteTokenHelper.Hash(token);
        return await _context.StudentInvites
            .FirstOrDefaultAsync(i => i.InviteToken == tokenHash
                                   && i.IsActive
                                   && i.AcceptedAt == null
                                   && i.InviteExpiresAt > DateTime.UtcNow, ct);
    }

    private async Task<ServiceResult> VerifyEmailMatchAsync(int userId, StudentInvite invite, CancellationToken ct)
    {
        var user = await _context.Users.FindAsync(new object[] { userId }, ct);
        if (user == null)
            return ServiceResult.FailureResult("User not found.");

        if (!string.IsNullOrEmpty(invite.InviteEmail) &&
            !string.Equals(user.Email, invite.InviteEmail, StringComparison.OrdinalIgnoreCase))
            return ServiceResult.FailureResult("This invite was sent to a different email address.");

        return ServiceResult.SuccessResult();
    }

    private static StudentInviteModel MapToModel(StudentInvite invite) => new()
    {
        Id = invite.Id,
        InviteEmail = invite.InviteEmail,
        ChildProfileId = invite.ChildProfileId,
        SchoolStudentId = invite.SchoolStudentId,
        IsActive = invite.IsActive,
        IsAccepted = invite.AcceptedAt != null,
        AcceptedAt = invite.AcceptedAt,
        InviteExpiresAt = invite.InviteExpiresAt,
        CreatedAt = invite.CreatedAt
    };
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// P3c parent<->school invite/link flow. Mirrors the SHA-token pattern from <see cref="ShareService"/>:
/// a 32-byte raw token is emailed, only its SHA256 hash is stored, accept hashes the inbound token to
/// match, the invite is email-bound (case-insensitive), and the token is single-use (cleared on accept).
/// </summary>
public class ChildLinkService : IChildLinkService
{
    private const int InviteExpiryDays = 14;

    private readonly ApplicationDbContext _context;
    private readonly IAccessService _accessService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ChildLinkService> _logger;

    public ChildLinkService(
        ApplicationDbContext context,
        IAccessService accessService,
        IEmailService emailService,
        ILogger<ChildLinkService> logger)
    {
        _context = context;
        _accessService = accessService;
        _emailService = emailService;
        _logger = logger;
    }

    // ----------------------------------------------------------------- Educator: invite

    public async Task<ServiceResult<ChildLinkModel>> InviteParentAsync(int educatorUserId, int studentId, string parentEmail, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(parentEmail))
            return ServiceResult<ChildLinkModel>.FailureResult("Parent email is required.");

        parentEmail = parentEmail.Trim();

        var access = await GetEducatorStudentAccessAsync(educatorUserId, studentId, ct);
        if (access.student == null)
            return ServiceResult<ChildLinkModel>.FailureResult("You do not have permission to invite a parent for this student.");

        var student = access.student;
        var now = DateTime.UtcNow;

        // Idempotency: an active, pending, unexpired invite for the same (student, email) already exists.
        var existingPending = await _context.ChildLinks
            .AnyAsync(l => l.SchoolStudentId == studentId
                        && l.InviteEmail == parentEmail
                        && l.IsActive
                        && l.AcceptedAt == null
                        && l.InviteExpiresAt > now, ct);
        if (existingPending)
            return ServiceResult<ChildLinkModel>.FailureResult("A pending invite already exists.");

        // Idempotency: the student is already actively linked to a ChildProfile owned by this email.
        var alreadyLinked = await _context.ChildLinks
            .Where(l => l.SchoolStudentId == studentId
                     && l.IsActive
                     && l.AcceptedAt != null
                     && l.ChildProfileId != null)
            .AnyAsync(l => _context.ChildAccesses.Any(ca =>
                    ca.ChildProfileId == l.ChildProfileId
                 && ca.Role == AccessRole.Owner
                 && ca.IsActive
                 && ca.AcceptedAt != null
                 && ca.User != null
                 && ca.User.Email.ToLower() == parentEmail.ToLower()), ct);
        if (alreadyLinked)
            return ServiceResult<ChildLinkModel>.FailureResult("This student is already linked to that parent.");

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = HashToken(rawToken);

        var link = new ChildLink
        {
            SchoolStudentId = studentId,
            ChildProfileId = null,
            InvitedByUserId = educatorUserId,
            InviteEmail = parentEmail,
            InviteToken = tokenHash,
            InviteExpiresAt = now.AddDays(InviteExpiryDays),
            AcceptedAt = null,
            LinkedAt = null,
            IsActive = true,
            CreatedById = educatorUserId,
            UpdatedById = educatorUserId
        };

        await _context.ChildLinks.AddAsync(link, ct);
        await _context.SaveChangesAsync(ct);

        var educator = await _context.Users.FindAsync(new object[] { educatorUserId }, ct);
        var schoolName = await _context.Schools
            .Where(s => s.Id == student.SchoolId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(ct);
        var educatorName = educator != null ? $"{educator.FirstName} {educator.LastName}".Trim() : "An educator";
        var studentName = $"{student.FirstName} {student.LastName}".Trim();

        await _emailService.SendSchoolLinkInviteEmailAsync(
            parentEmail, educatorName, schoolName ?? "the school", studentName, rawToken, ct);

        _logger.LogInformation("School link invite created and email sent for {Email} on student {StudentId}", parentEmail, studentId);

        return ServiceResult<ChildLinkModel>.SuccessResult(MapToModel(link), "Invite sent successfully.");
    }

    // ----------------------------------------------------------------- Parent: preview

    public async Task<ServiceResult<ChildLinkInvitePreviewModel>> PreviewInviteAsync(int parentUserId, string token, CancellationToken ct = default)
    {
        var invite = await FindActiveInviteAsync(token, ct);
        if (invite == null)
            return ServiceResult<ChildLinkInvitePreviewModel>.FailureResult("Invalid or expired invite token.");

        var emailCheck = await VerifyEmailMatchAsync(parentUserId, invite, ct);
        if (!emailCheck.Success)
            return ServiceResult<ChildLinkInvitePreviewModel>.FailureResult(emailCheck.Message!);

        var student = await _context.SchoolStudents
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == invite.SchoolStudentId, ct);
        if (student == null)
            return ServiceResult<ChildLinkInvitePreviewModel>.FailureResult("Student not found.");

        var schoolName = await _context.Schools
            .Where(s => s.Id == student.SchoolId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(ct);

        var ownedChildren = await GetOwnedChildrenAsync(parentUserId, ct);

        return ServiceResult<ChildLinkInvitePreviewModel>.SuccessResult(new ChildLinkInvitePreviewModel
        {
            SchoolStudentId = student.Id,
            StudentFirstName = student.FirstName,
            StudentLastName = student.LastName,
            SchoolName = schoolName,
            ExistingChildren = ownedChildren
        });
    }

    // ----------------------------------------------------------------- Parent: accept (match-or-create)

    public async Task<ServiceResult<ChildLinkModel>> AcceptInviteAsync(int parentUserId, string token, int? linkToChildProfileId, CancellationToken ct = default)
    {
        var invite = await FindActiveInviteAsync(token, ct);
        if (invite == null)
            return ServiceResult<ChildLinkModel>.FailureResult("Invalid or expired invite token.");

        var emailCheck = await VerifyEmailMatchAsync(parentUserId, invite, ct);
        if (!emailCheck.Success)
            return ServiceResult<ChildLinkModel>.FailureResult(emailCheck.Message!);

        // Idempotency: this student already has an active, accepted link to a ChildProfile this parent owns.
        // Return success WITHOUT creating anything (exactly one link).
        var existingLink = await _context.ChildLinks
            .Where(l => l.SchoolStudentId == invite.SchoolStudentId
                     && l.IsActive
                     && l.AcceptedAt != null
                     && l.ChildProfileId != null)
            .Where(l => _context.ChildAccesses.Any(ca =>
                    ca.ChildProfileId == l.ChildProfileId
                 && ca.UserId == parentUserId
                 && ca.Role == AccessRole.Owner
                 && ca.IsActive
                 && ca.AcceptedAt != null))
            .FirstOrDefaultAsync(ct);
        if (existingLink != null)
            return ServiceResult<ChildLinkModel>.SuccessResult(MapToModel(existingLink), "Already linked.");

        var now = DateTime.UtcNow;

        // Validate everything that does NOT depend on winning the token BEFORE claiming it, so a
        // rejected accept (e.g. linking to a child the parent doesn't own) never consumes the invite.
        if (linkToChildProfileId.HasValue)
        {
            var isOwner = await _accessService.HasMinimumRoleAsync(linkToChildProfileId.Value, parentUserId, AccessRole.Owner, ct);
            if (!isOwner)
                return ServiceResult<ChildLinkModel>.FailureResult("You do not have permission to link to that child.");
        }
        else if (!await _context.SchoolStudents.AnyAsync(s => s.Id == invite.SchoolStudentId, ct))
        {
            return ServiceResult<ChildLinkModel>.FailureResult("Student not found.");
        }

        // Atomically claim the invite, so two concurrent accepts of the same token can't both pass
        // FindActiveInviteAsync and each create a duplicate ChildProfile. Only the request whose
        // conditional update matches an as-yet-unaccepted invite proceeds; the loser falls back to
        // the idempotent "already linked" resolution.
        var tokenHash = HashToken(token);
        var claimed = await _context.ChildLinks
            .Where(l => l.Id == invite.Id && l.InviteToken == tokenHash && l.AcceptedAt == null && l.IsActive)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.AcceptedAt, now)
                .SetProperty(l => l.InviteToken, (string?)null), ct);
        if (claimed == 0)
        {
            // Lost the race. Re-resolve: if a concurrent accept already linked this student to a
            // child this parent owns, return it idempotently; otherwise the invite is spent.
            var winner = await _context.ChildLinks
                .Where(l => l.SchoolStudentId == invite.SchoolStudentId
                         && l.IsActive
                         && l.AcceptedAt != null
                         && l.ChildProfileId != null)
                .Where(l => _context.ChildAccesses.Any(ca =>
                        ca.ChildProfileId == l.ChildProfileId
                     && ca.UserId == parentUserId
                     && ca.Role == AccessRole.Owner
                     && ca.IsActive
                     && ca.AcceptedAt != null))
                .FirstOrDefaultAsync(ct);
            return winner != null
                ? ServiceResult<ChildLinkModel>.SuccessResult(MapToModel(winner), "Already linked.")
                : ServiceResult<ChildLinkModel>.FailureResult("Invalid or expired invite token.");
        }

        // Keep the tracked entity consistent with the claim we just committed out-of-band.
        invite.AcceptedAt = now;
        invite.InviteToken = null;

        int resolvedChildProfileId;

        if (linkToChildProfileId.HasValue)
        {
            resolvedChildProfileId = linkToChildProfileId.Value;
        }
        else
        {
            // Create a new ChildProfile + Owner ChildAccess (mirrors ChildProfileService.CreateAsync).
            var student = await _context.SchoolStudents
                .FirstOrDefaultAsync(s => s.Id == invite.SchoolStudentId, ct);
            if (student == null)
                return ServiceResult<ChildLinkModel>.FailureResult("Student not found.");

            var child = new ChildProfile
            {
                UserId = parentUserId,
                FirstName = student.FirstName,
                LastName = student.LastName,
                DateOfBirth = student.DateOfBirth,
                DisabilityCategory = student.DisabilityCategory,
                IsActive = true,
                CreatedById = parentUserId,
                UpdatedById = parentUserId
            };
            await _context.ChildProfiles.AddAsync(child, ct);
            await _context.SaveChangesAsync(ct); // save to get the generated Id

            await _context.ChildAccesses.AddAsync(new ChildAccess
            {
                ChildProfileId = child.Id,
                UserId = parentUserId,
                Role = AccessRole.Owner,
                AcceptedAt = now,
                IsActive = true,
                CreatedById = parentUserId,
                UpdatedById = parentUserId
            }, ct);
            await _context.SaveChangesAsync(ct);

            resolvedChildProfileId = child.Id;
        }

        // AcceptedAt + InviteToken were already set by the atomic claim above; finish the link.
        invite.ChildProfileId = resolvedChildProfileId;
        invite.LinkedAt = now;
        invite.UpdatedById = parentUserId;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Parent {ParentUserId} accepted school link {LinkId} -> child {ChildId}",
            parentUserId, invite.Id, resolvedChildProfileId);

        return ServiceResult<ChildLinkModel>.SuccessResult(MapToModel(invite), "Link accepted successfully.");
    }

    // ----------------------------------------------------------------- Educator: revoke (forward-only)

    public async Task<ServiceResult> RevokeLinkAsync(int educatorUserId, int studentId, int linkId, CancellationToken ct = default)
    {
        // Scope the link to the route's studentId so a {studentId}/{linkId} mismatch can't revoke
        // a different student's link (route-contract integrity / avoids cross-student revokes).
        var link = await _context.ChildLinks
            .FirstOrDefaultAsync(l => l.Id == linkId && l.SchoolStudentId == studentId, ct);
        if (link == null)
            return ServiceResult.FailureResult("Link not found.");

        var access = await GetEducatorStudentAccessAsync(educatorUserId, link.SchoolStudentId, ct);
        if (access.student == null)
            return ServiceResult.FailureResult("You do not have permission to revoke this link.");

        // Forward-only: deactivating the link stops FUTURE version sharing but does NOT retroactively
        // remove anything the parent already received. IepVersion sharing arrives in P5; nothing is shared
        // yet, so the forward-only semantics will be enforced at version-share time in P5.
        link.IsActive = false;
        link.UpdatedById = educatorUserId;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Educator {EducatorUserId} revoked link {LinkId} (forward-only)", educatorUserId, linkId);

        return ServiceResult.SuccessResult("Link revoked. The parent keeps anything already shared; future versions stop flowing.");
    }

    // ----------------------------------------------------------------- Educator: list

    public async Task<ServiceResult<List<ChildLinkModel>>> GetLinksForStudentAsync(int educatorUserId, int studentId, CancellationToken ct = default)
    {
        var access = await GetEducatorStudentAccessAsync(educatorUserId, studentId, ct);
        if (access.student == null)
            return ServiceResult<List<ChildLinkModel>>.FailureResult("You do not have permission to view this student's links.");

        var links = await _context.ChildLinks
            .AsNoTracking()
            .Where(l => l.SchoolStudentId == studentId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        return ServiceResult<List<ChildLinkModel>>.SuccessResult(links.Select(MapToModel).ToList());
    }

    // ----------------------------------------------------------------- Parent: a child's school links

    public async Task<ServiceResult<List<ChildSchoolLinkModel>>> GetChildSchoolLinksAsync(int parentUserId, int childProfileId, CancellationToken ct = default)
    {
        // Parent must have access to the child (any role).
        var role = await _accessService.GetRoleAsync(childProfileId, parentUserId, ct);
        if (role == null)
            return ServiceResult<List<ChildSchoolLinkModel>>.FailureResult("You do not have permission to view this child.");

        var links = await _context.ChildLinks
            .AsNoTracking()
            .Where(l => l.ChildProfileId == childProfileId && l.IsActive && l.AcceptedAt != null)
            .Select(l => new ChildSchoolLinkModel
            {
                Id = l.Id,
                SchoolStudentId = l.SchoolStudentId,
                SchoolName = l.SchoolStudent.School.Name,
                StudentFirstName = l.SchoolStudent.FirstName,
                StudentLastName = l.SchoolStudent.LastName,
                LinkedAt = l.LinkedAt
            })
            .ToListAsync(ct);

        return ServiceResult<List<ChildSchoolLinkModel>>.SuccessResult(links);
    }

    // ----------------------------------------------------------------- Helpers

    /// <summary>
    /// SchoolId-bound educator access check (replicates EducatorService.GetStudentAsync's guard): the
    /// student must be in the educator's TeacherProfile.SchoolId AND an active SchoolStudentAccess must exist.
    /// </summary>
    private async Task<(SchoolStudent? student, int schoolId)> GetEducatorStudentAccessAsync(int educatorUserId, int studentId, CancellationToken ct)
    {
        var schoolId = await _context.TeacherProfiles
            .Where(t => t.UserId == educatorUserId)
            .Select(t => (int?)t.SchoolId)
            .FirstOrDefaultAsync(ct);
        if (schoolId == null)
            return (null, 0);

        var student = await _context.SchoolStudents
            .FirstOrDefaultAsync(s => s.Id == studentId && s.SchoolId == schoolId.Value, ct);
        if (student == null)
            return (null, schoolId.Value);

        var hasAccess = await _context.SchoolStudentAccesses
            .AnyAsync(a => a.SchoolStudentId == studentId && a.UserId == educatorUserId && a.IsActive, ct);
        if (!hasAccess)
            return (null, schoolId.Value);

        return (student, schoolId.Value);
    }

    private async Task<ChildLink?> FindActiveInviteAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var tokenHash = HashToken(token);
        return await _context.ChildLinks
            .FirstOrDefaultAsync(l => l.InviteToken == tokenHash
                                   && l.IsActive
                                   && l.AcceptedAt == null
                                   && l.InviteExpiresAt > DateTime.UtcNow, ct);
    }

    private async Task<ServiceResult> VerifyEmailMatchAsync(int parentUserId, ChildLink invite, CancellationToken ct)
    {
        var user = await _context.Users.FindAsync(new object[] { parentUserId }, ct);
        if (user == null)
            return ServiceResult.FailureResult("User not found.");

        if (!string.IsNullOrEmpty(invite.InviteEmail) &&
            !string.Equals(user.Email, invite.InviteEmail, StringComparison.OrdinalIgnoreCase))
            return ServiceResult.FailureResult("This invite was sent to a different email address.");

        return ServiceResult.SuccessResult();
    }

    /// <summary>Lists the parent's owned children via accepted Owner ChildAccess rows (authz-correct).</summary>
    private async Task<List<LinkableChildModel>> GetOwnedChildrenAsync(int parentUserId, CancellationToken ct)
    {
        return await _context.ChildProfiles
            .AsNoTracking()
            .Where(c => c.IsActive && _context.ChildAccesses.Any(ca =>
                    ca.ChildProfileId == c.Id
                 && ca.UserId == parentUserId
                 && ca.Role == AccessRole.Owner
                 && ca.IsActive
                 && ca.AcceptedAt != null))
            .OrderBy(c => c.FirstName)
            .Select(c => new LinkableChildModel
            {
                ChildProfileId = c.Id,
                FirstName = c.FirstName,
                LastName = c.LastName
            })
            .ToListAsync(ct);
    }

    private static ChildLinkModel MapToModel(ChildLink link) => new()
    {
        Id = link.Id,
        SchoolStudentId = link.SchoolStudentId,
        ChildProfileId = link.ChildProfileId,
        InviteEmail = link.InviteEmail,
        IsActive = link.IsActive,
        IsAccepted = link.AcceptedAt != null,
        AcceptedAt = link.AcceptedAt,
        LinkedAt = link.LinkedAt,
        InviteExpiresAt = link.InviteExpiresAt,
        CreatedAt = link.CreatedAt
    };

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}

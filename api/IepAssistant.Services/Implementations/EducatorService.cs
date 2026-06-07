using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class EducatorService : IEducatorService
{
    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly ILogger<EducatorService> _logger;

    public EducatorService(ApplicationDbContext context, IOrgAccessService orgAccess, ILogger<EducatorService> logger)
    {
        _context = context;
        _orgAccess = orgAccess;
        _logger = logger;
    }

    public async Task<ServiceResult<EducatorProfileModel>> GetMeAsync(int userId, CancellationToken ct = default)
    {
        var profile = await _context.StaffProfiles
            .AsNoTracking()
            .Include(t => t.District)
            .Include(t => t.School)
            .Include(t => t.OrgRole)
            .FirstOrDefaultAsync(t => t.UserId == userId, ct);

        if (profile == null)
            return ServiceResult<EducatorProfileModel>.FailureResult("Educator profile not found.");

        return ServiceResult<EducatorProfileModel>.SuccessResult(BuildProfileModel(profile));
    }

    public async Task<ServiceResult<SchoolStudentModel>> CreateStudentAsync(int userId, CreateSchoolStudentModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.FirstName))
            return ServiceResult<SchoolStudentModel>.FailureResult("Student first name is required.");

        var profile = await _context.StaffProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId && t.IsActive, ct);
        if (profile == null)
            return ServiceResult<SchoolStudentModel>.FailureResult("Educator profile not found.");
        if (profile.SchoolId == null)
            return ServiceResult<SchoolStudentModel>.FailureResult("A school is required to create a student.");

        var student = new SchoolStudent
        {
            SchoolId = profile.SchoolId.Value,
            FirstName = model.FirstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(model.LastName) ? null : model.LastName.Trim(),
            DateOfBirth = model.DateOfBirth,
            StateCode = string.IsNullOrWhiteSpace(model.StateCode) ? null : model.StateCode.Trim(),
            GradeLevel = string.IsNullOrWhiteSpace(model.GradeLevel) ? null : model.GradeLevel.Trim(),
            DisabilityCategory = string.IsNullOrWhiteSpace(model.DisabilityCategory) ? null : model.DisabilityCategory.Trim(),
            IsActive = true,
            CreatedById = userId
        };
        await _context.SchoolStudents.AddAsync(student, ct);
        await _context.SaveChangesAsync(ct);

        await _context.SchoolStudentAccesses.AddAsync(new SchoolStudentAccess
        {
            SchoolStudentId = student.Id,
            UserId = userId,
            Role = AccessRole.Owner,
            IsActive = true,
            CreatedById = userId
        }, ct);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<SchoolStudentModel>.SuccessResult(MapStudent(student));
    }

    public async Task<ServiceResult<List<SchoolStudentModel>>> GetStudentsAsync(int userId, CancellationToken ct = default)
    {
        var profile = await _context.StaffProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId && t.IsActive, ct);
        if (profile == null)
            return ServiceResult<List<SchoolStudentModel>>.FailureResult("Educator profile not found.");
        if (profile.SchoolId == null)
            return ServiceResult<List<SchoolStudentModel>>.SuccessResult(new List<SchoolStudentModel>());

        // SchoolId-bound: only students in the educator's own school.
        var students = await _context.SchoolStudents
            .AsNoTracking()
            .Where(s => s.SchoolId == profile.SchoolId.Value && s.IsActive)
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync(ct);

        return ServiceResult<List<SchoolStudentModel>>.SuccessResult(
            students.Select(MapStudent).ToList());
    }

    public async Task<ServiceResult<SchoolStudentModel>> GetStudentAsync(int userId, int studentId, CancellationToken ct = default)
    {
        // Org access (player-coach: admins pass within scope; teachers need an active SchoolStudentAccess).
        if (!await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Viewer, ct))
            return ServiceResult<SchoolStudentModel>.FailureResult("You do not have permission to access this student.");

        var student = await _context.SchoolStudents
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student == null)
            return ServiceResult<SchoolStudentModel>.FailureResult("Student not found.");

        return ServiceResult<SchoolStudentModel>.SuccessResult(MapStudent(student));
    }

    /// <summary>Builds the profile model from a fully navigation-loaded StaffProfile (GetMe path).</summary>
    private static EducatorProfileModel BuildProfileModel(StaffProfile profile) => new()
    {
        StaffProfileId = profile.Id,
        UserId = profile.UserId,
        OrgRoleId = profile.OrgRoleId,
        OrgRoleName = profile.OrgRole?.Name ?? string.Empty,
        DistrictId = profile.DistrictId,
        DistrictName = profile.District?.Name ?? string.Empty,
        SchoolId = profile.SchoolId,
        SchoolName = profile.School?.Name,
        IsActive = profile.IsActive,
        StateCode = profile.School?.StateCode ?? profile.District?.StateCode,
        Title = profile.Title,
        Credentials = profile.Credentials
    };

    private static SchoolStudentModel MapStudent(SchoolStudent s) => new()
    {
        Id = s.Id,
        SchoolId = s.SchoolId,
        FirstName = s.FirstName,
        LastName = s.LastName,
        DateOfBirth = s.DateOfBirth,
        StateCode = s.StateCode,
        GradeLevel = s.GradeLevel,
        DisabilityCategory = s.DisabilityCategory,
        IsActive = s.IsActive,
        CreatedAt = s.CreatedAt
    };
}

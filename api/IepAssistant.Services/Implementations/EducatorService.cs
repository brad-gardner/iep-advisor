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
    private readonly ILogger<EducatorService> _logger;

    public EducatorService(ApplicationDbContext context, ILogger<EducatorService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ServiceResult<EducatorProfileModel>> OnboardAsync(int userId, OnboardEducatorModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.DistrictName))
            return ServiceResult<EducatorProfileModel>.FailureResult("District name is required.");
        if (string.IsNullOrWhiteSpace(model.SchoolName))
            return ServiceResult<EducatorProfileModel>.FailureResult("School name is required.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
            return ServiceResult<EducatorProfileModel>.FailureResult("User not found.");

        var districtName = model.DistrictName.Trim();
        var schoolName = model.SchoolName.Trim();
        var stateCode = string.IsNullOrWhiteSpace(model.StateCode) ? null : model.StateCode.Trim();

        // Find-or-create the District by (Name, StateCode).
        var district = await _context.Districts
            .FirstOrDefaultAsync(d => d.Name == districtName && d.StateCode == stateCode, ct);
        if (district == null)
        {
            district = new District
            {
                Name = districtName,
                StateCode = stateCode,
                CreatedById = userId
            };
            await _context.Districts.AddAsync(district, ct);
            await _context.SaveChangesAsync(ct);
        }

        // Find-or-create the School by (DistrictId, Name).
        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.DistrictId == district.Id && s.Name == schoolName, ct);
        if (school == null)
        {
            school = new School
            {
                DistrictId = district.Id,
                Name = schoolName,
                StateCode = stateCode,
                CreatedById = userId
            };
            await _context.Schools.AddAsync(school, ct);
            await _context.SaveChangesAsync(ct);
        }

        // Idempotent: reuse an existing TeacherProfile for this user.
        var profile = await _context.TeacherProfiles
            .FirstOrDefaultAsync(t => t.UserId == userId, ct);
        if (profile == null)
        {
            profile = new TeacherProfile
            {
                UserId = userId,
                SchoolId = school.Id,
                CreatedById = userId
            };
            await _context.TeacherProfiles.AddAsync(profile, ct);
        }

        // Flip the user's role to Educator (single-role model).
        if (user.Role != UserRole.Educator)
            user.Role = UserRole.Educator;

        await _context.SaveChangesAsync(ct);

        return ServiceResult<EducatorProfileModel>.SuccessResult(
            BuildProfileModel(profile, school, district));
    }

    public async Task<ServiceResult<EducatorProfileModel>> GetMeAsync(int userId, CancellationToken ct = default)
    {
        var profile = await _context.TeacherProfiles
            .AsNoTracking()
            .Include(t => t.School)
            .ThenInclude(s => s.District)
            .FirstOrDefaultAsync(t => t.UserId == userId, ct);

        if (profile == null)
            return ServiceResult<EducatorProfileModel>.FailureResult("Educator profile not found.");

        return ServiceResult<EducatorProfileModel>.SuccessResult(
            BuildProfileModel(profile, profile.School, profile.School.District));
    }

    public async Task<ServiceResult<SchoolStudentModel>> CreateStudentAsync(int userId, CreateSchoolStudentModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.FirstName))
            return ServiceResult<SchoolStudentModel>.FailureResult("Student first name is required.");

        var profile = await _context.TeacherProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId, ct);
        if (profile == null)
            return ServiceResult<SchoolStudentModel>.FailureResult("Educator profile not found.");

        var student = new SchoolStudent
        {
            SchoolId = profile.SchoolId,
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
        var profile = await _context.TeacherProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId, ct);
        if (profile == null)
            return ServiceResult<List<SchoolStudentModel>>.FailureResult("Educator profile not found.");

        // SchoolId-bound: only students in the educator's own school.
        var students = await _context.SchoolStudents
            .AsNoTracking()
            .Where(s => s.SchoolId == profile.SchoolId && s.IsActive)
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync(ct);

        return ServiceResult<List<SchoolStudentModel>>.SuccessResult(
            students.Select(MapStudent).ToList());
    }

    public async Task<ServiceResult<SchoolStudentModel>> GetStudentAsync(int userId, int studentId, CancellationToken ct = default)
    {
        var profile = await _context.TeacherProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId, ct);
        if (profile == null)
            return ServiceResult<SchoolStudentModel>.FailureResult("Educator profile not found.");

        // Cross-school rejection guard: the student must belong to the educator's school
        // AND the educator must have an active SchoolStudentAccess row.
        var student = await _context.SchoolStudents
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId && s.SchoolId == profile.SchoolId, ct);
        if (student == null)
            return ServiceResult<SchoolStudentModel>.FailureResult("Student not found.");

        var hasAccess = await _context.SchoolStudentAccesses
            .AsNoTracking()
            .AnyAsync(a => a.SchoolStudentId == studentId && a.UserId == userId && a.IsActive, ct);
        if (!hasAccess)
            return ServiceResult<SchoolStudentModel>.FailureResult("You do not have permission to access this student.");

        return ServiceResult<SchoolStudentModel>.SuccessResult(MapStudent(student));
    }

    private static EducatorProfileModel BuildProfileModel(TeacherProfile profile, School school, District district) => new()
    {
        TeacherProfileId = profile.Id,
        UserId = profile.UserId,
        SchoolId = school.Id,
        SchoolName = school.Name,
        DistrictId = district.Id,
        DistrictName = district.Name,
        StateCode = school.StateCode ?? district.StateCode,
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

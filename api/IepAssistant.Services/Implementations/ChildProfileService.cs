using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class ChildProfileService : IChildProfileService
{
    private readonly IChildProfileRepository _repository;
    private readonly IAccessService _accessService;
    private readonly ApplicationDbContext _context;

    public ChildProfileService(IChildProfileRepository repository, IAccessService accessService, ApplicationDbContext context)
    {
        _repository = repository;
        _accessService = accessService;
        _context = context;
    }

    public async Task<IEnumerable<ChildProfileModel>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var profiles = await _repository.GetByUserIdAsync(userId, cancellationToken);
        return profiles.Select(MapToModel);
    }

    public async Task<ChildProfileModel?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetByIdForUserAsync(id, userId, cancellationToken);
        return profile == null ? null : MapToModel(profile);
    }

    public async Task<ServiceResult<ChildProfileModel>> CreateAsync(int userId, CreateChildProfileModel model, CancellationToken cancellationToken = default)
    {
        var entity = new ChildProfile
        {
            UserId = userId,
            FirstName = model.FirstName,
            LastName = model.LastName,
            DateOfBirth = model.DateOfBirth,
            GradeLevel = model.GradeLevel,
            DisabilityCategory = model.DisabilityCategory,
            SchoolDistrict = model.SchoolDistrict,
            CreatedById = userId,
            UpdatedById = userId
        };

        await _repository.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken); // Save first to get the generated Id

        var access = new ChildAccess
        {
            ChildProfileId = entity.Id,
            UserId = userId,
            Role = AccessRole.Owner,
            AcceptedAt = DateTime.UtcNow,
            CreatedById = userId,
            UpdatedById = userId
        };
        await _context.Set<ChildAccess>().AddAsync(access, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult<ChildProfileModel>.SuccessResult(MapToModel(entity), "Child profile created successfully.");
    }

    public async Task<ServiceResult> UpdateAsync(int id, int userId, UpdateChildProfileModel model, CancellationToken cancellationToken = default)
    {
        if (!await _accessService.HasMinimumRoleAsync(id, userId, AccessRole.Owner, cancellationToken))
            return ServiceResult.FailureResult("Child profile not found.");

        var entity = await _repository.GetByIdForUserAsync(id, userId, cancellationToken);
        if (entity == null)
            return ServiceResult.FailureResult("Child profile not found.");

        if (model.FirstName != null)
            entity.FirstName = model.FirstName;

        if (model.LastName != null)
            entity.LastName = model.LastName;

        if (model.DateOfBirth.HasValue)
            entity.DateOfBirth = model.DateOfBirth;

        if (model.GradeLevel != null)
            entity.GradeLevel = model.GradeLevel;

        if (model.DisabilityCategory != null)
            entity.DisabilityCategory = model.DisabilityCategory;

        if (model.SchoolDistrict != null)
            entity.SchoolDistrict = model.SchoolDistrict;

        entity.UpdatedById = userId;
        _repository.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("Child profile updated successfully.");
    }

    public async Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        if (!await _accessService.HasMinimumRoleAsync(id, userId, AccessRole.Owner, cancellationToken))
            return ServiceResult.FailureResult("Child profile not found.");

        var entity = await _repository.GetByIdForUserAsync(id, userId, cancellationToken);
        if (entity == null)
            return ServiceResult.FailureResult("Child profile not found.");

        entity.IsActive = false;
        entity.UpdatedById = userId;
        _repository.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("Child profile deleted successfully.");
    }

    public async Task<ServiceResult> SetCurrentIepAsync(int childId, int iepDocumentId, int userId, CancellationToken cancellationToken = default)
    {
        if (!await _accessService.HasMinimumRoleAsync(childId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult.FailureResult("Child profile not found.");

        var child = await _repository.GetByIdForUserAsync(childId, userId, cancellationToken);
        if (child == null)
            return ServiceResult.FailureResult("Child profile not found.");

        var iep = await _context.IepDocuments
            .FirstOrDefaultAsync(d => d.Id == iepDocumentId && d.ChildProfileId == childId, cancellationToken);
        if (iep == null)
            return ServiceResult.FailureResult("IEP not found for this child.");

        child.CurrentIepDocumentId = iepDocumentId;
        child.UpdatedById = userId;
        _repository.Update(child);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("Current IEP updated.");
    }

    private static ChildProfileModel MapToModel(ChildProfile entity) => new()
    {
        Id = entity.Id,
        FirstName = entity.FirstName,
        LastName = entity.LastName,
        DateOfBirth = entity.DateOfBirth,
        GradeLevel = entity.GradeLevel,
        DisabilityCategory = entity.DisabilityCategory,
        SchoolDistrict = entity.SchoolDistrict,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        CurrentIepDocumentId = entity.CurrentIepDocumentId
    };
}

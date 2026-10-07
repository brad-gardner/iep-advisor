using Microsoft.Extensions.Localization;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<Messages> _localizer;

    public UserService(IUserRepository userRepository, ApplicationDbContext context, IStringLocalizer<Messages> localizer)
    {
        _userRepository = userRepository;
        _context = context;
        _localizer = localizer;
    }

    public async Task<IEnumerable<UserModel>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return users.Select(MapToUserModel);
    }

    public async Task<UserModel?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        return user == null ? null : MapToUserModel(user);
    }

    public async Task<ServiceResult> UpdateUserAsync(int id, UpdateUserModel model, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["Users.NotFound"]);

        if (model.FirstName != null)
            user.FirstName = model.FirstName;

        if (model.LastName != null)
            user.LastName = model.LastName;

        if (model.State != null)
            user.State = model.State;

        if (model.Role != null)
        {
            // Tolerantly accept the legacy "User" alias as well as the enum names.
            var requestedRole = model.Role == "User" ? "Parent" : model.Role;
            if (Enum.TryParse<Domain.Entities.UserRole>(requestedRole, ignoreCase: true, out var parsedRole))
                user.Role = parsedRole;
            else
                return ServiceResult.FailureResult(ServiceErrorKind.Validation, string.Format(_localizer["Users.InvalidRole"].Value, model.Role));
        }

        if (model.IsActive.HasValue)
        {
            user.IsActive = model.IsActive.Value;
            if (!model.IsActive.Value)
                user.SecurityStamp++; // Invalidate existing tokens when deactivating
        }

        user.UpdatedAt = DateTime.UtcNow;

        _userRepository.Update(user);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult(_localizer["Users.UpdatedSuccessfully"]);
    }

    public async Task<ServiceResult> DeleteUserAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["Users.NotFound"]);

        user.IsActive = false;
        user.SecurityStamp++; // Invalidate existing tokens immediately
        user.UpdatedAt = DateTime.UtcNow;

        _userRepository.Update(user);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult(_localizer["Users.DeletedSuccessfully"]);
    }

    private static UserModel MapToUserModel(Domain.Entities.User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        State = user.State,
        Role = user.Role.ToString(),
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt
    };
}

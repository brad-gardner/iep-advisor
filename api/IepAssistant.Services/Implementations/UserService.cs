using IepAssistant.Domain.Data;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ApplicationDbContext _context;

    public UserService(IUserRepository userRepository, ApplicationDbContext context)
    {
        _userRepository = userRepository;
        _context = context;
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
            return ServiceResult.FailureResult("User not found.");

        if (model.FirstName != null)
            user.FirstName = model.FirstName;

        if (model.LastName != null)
            user.LastName = model.LastName;

        if (model.State != null)
            user.State = model.State;

        if (model.Role != null)
            user.Role = model.Role;

        if (model.IsActive.HasValue)
        {
            user.IsActive = model.IsActive.Value;
            if (!model.IsActive.Value)
                user.SecurityStamp++; // Invalidate existing tokens when deactivating
        }

        user.UpdatedAt = DateTime.UtcNow;

        _userRepository.Update(user);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("User updated successfully.");
    }

    public async Task<ServiceResult> DeleteUserAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
            return ServiceResult.FailureResult("User not found.");

        user.IsActive = false;
        user.SecurityStamp++; // Invalidate existing tokens immediately
        user.UpdatedAt = DateTime.UtcNow;

        _userRepository.Update(user);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("User deleted successfully.");
    }

    private static UserModel MapToUserModel(Domain.Entities.User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        State = user.State,
        Role = user.Role,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt
    };
}

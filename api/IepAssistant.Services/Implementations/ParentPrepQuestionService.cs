using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Data.Configurations;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Questions are plain text at rest: the input goes through <see cref="RichTextSanitizer"/> (which removes
/// dangerous blocks with their content), then every remaining tag is stripped and whitespace collapsed, and
/// only THEN is the length measured — so the stored value is what was checked. Access failures and unknown
/// ids collapse to the same "not found" message so a caller can never probe whether a question or child
/// exists.
/// </summary>
public class ParentPrepQuestionService : IParentPrepQuestionService
{
    public const int MaxTextLength = ParentPrepQuestionConfiguration.TextMaxLength;
    public static readonly IReadOnlyList<string> AllowedSources = new[] { ParentPrepQuestion.SourceParent, ParentPrepQuestion.SourceAdvocate };

    private const string ChildNotFound = "Child profile not found.";
    private const string QuestionNotFound = "Prep question not found.";
    private const string TextRequired = "Question text is required.";

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly ApplicationDbContext _context;
    private readonly IAccessService _access;

    public ParentPrepQuestionService(ApplicationDbContext context, IAccessService access)
    {
        _context = context;
        _access = access;
    }

    public async Task<ServiceResult<List<ParentPrepQuestionModel>>> GetForChildAsync(int childId, int userId, CancellationToken ct = default)
    {
        if (!await _access.HasMinimumRoleAsync(childId, userId, AccessRole.Viewer, ct))
            return ServiceResult<List<ParentPrepQuestionModel>>.FailureResult(ChildNotFound);

        var items = await OrderedForChild(_context.ParentPrepQuestions.AsNoTracking(), childId).ToListAsync(ct);
        return ServiceResult<List<ParentPrepQuestionModel>>.SuccessResult(items.Select(Map).ToList());
    }

    public async Task<ServiceResult<ParentPrepQuestionAddResult>> AddAsync(int childId, int userId, string? text, string? source, CancellationToken ct = default)
    {
        if (!await _access.HasMinimumRoleAsync(childId, userId, AccessRole.Collaborator, ct))
            return ServiceResult<ParentPrepQuestionAddResult>.FailureResult(ChildNotFound);

        var (clean, error) = NormalizeText(text);
        if (error != null) return ServiceResult<ParentPrepQuestionAddResult>.FailureResult(error);

        var normalizedSource = (source ?? ParentPrepQuestion.SourceParent).Trim().ToLowerInvariant();
        if (!AllowedSources.Contains(normalizedSource, StringComparer.Ordinal))
            return ServiceResult<ParentPrepQuestionAddResult>.FailureResult("Source must be 'parent' or 'advocate'.");

        // The whole list is small (a parent's own questions) and is needed both for the duplicate check —
        // case-insensitive regardless of the database collation — and for the next display order.
        var existing = await OrderedForChild(_context.ParentPrepQuestions.AsNoTracking(), childId).ToListAsync(ct);
        var duplicate = existing.FirstOrDefault(q => string.Equals(q.Text, clean, StringComparison.OrdinalIgnoreCase));
        if (duplicate != null)
            return ServiceResult<ParentPrepQuestionAddResult>.SuccessResult(new ParentPrepQuestionAddResult { Question = Map(duplicate), AlreadyExisted = true });

        var entity = new ParentPrepQuestion
        {
            ChildProfileId = childId,
            Text = clean!,
            IsChecked = false,
            DisplayOrder = existing.Count == 0 ? 0 : existing.Max(q => q.DisplayOrder) + 1,
            Source = normalizedSource,
            CreatedById = userId,
            UpdatedById = userId
        };
        _context.ParentPrepQuestions.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ServiceResult<ParentPrepQuestionAddResult>.SuccessResult(new ParentPrepQuestionAddResult { Question = Map(entity), AlreadyExisted = false });
    }

    public async Task<ServiceResult<ParentPrepQuestionModel>> UpdateAsync(int id, int userId, string? text, bool? isChecked, CancellationToken ct = default)
    {
        var entity = await _context.ParentPrepQuestions.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (entity == null || !await _access.HasMinimumRoleAsync(entity.ChildProfileId, userId, AccessRole.Collaborator, ct))
            return ServiceResult<ParentPrepQuestionModel>.FailureResult(QuestionNotFound);

        if (text == null && isChecked == null)
            return ServiceResult<ParentPrepQuestionModel>.FailureResult("Provide text or isChecked.");

        if (text != null)
        {
            var (clean, error) = NormalizeText(text);
            if (error != null) return ServiceResult<ParentPrepQuestionModel>.FailureResult(error);
            entity.Text = clean!;
        }
        if (isChecked != null) entity.IsChecked = isChecked.Value;

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult<ParentPrepQuestionModel>.SuccessResult(Map(entity));
    }

    public async Task<ServiceResult<List<ParentPrepQuestionModel>>> ReorderAsync(int childId, int userId, IReadOnlyList<int> orderedIds, CancellationToken ct = default)
    {
        if (!await _access.HasMinimumRoleAsync(childId, userId, AccessRole.Collaborator, ct))
            return ServiceResult<List<ParentPrepQuestionModel>>.FailureResult(ChildNotFound);

        if (orderedIds.Count == 0)
            return ServiceResult<List<ParentPrepQuestionModel>>.FailureResult("ids is required.");
        if (orderedIds.Distinct().Count() != orderedIds.Count)
            return ServiceResult<List<ParentPrepQuestionModel>>.FailureResult("ids must not repeat.");

        var questions = await OrderedForChild(_context.ParentPrepQuestions, childId).ToListAsync(ct);
        var byId = questions.ToDictionary(q => q.Id);
        // An id from another child is reported exactly like a nonexistent one (a 400, worded without
        // "not found" so the controller does not map it to 404).
        if (orderedIds.Any(id => !byId.ContainsKey(id)))
            return ServiceResult<List<ParentPrepQuestionModel>>.FailureResult("Every id must be one of this child's prep questions.");

        var listed = new HashSet<int>(orderedIds);
        var sequence = orderedIds.Select(id => byId[id]).Concat(questions.Where(q => !listed.Contains(q.Id))).ToList();
        var now = DateTime.UtcNow;
        for (var i = 0; i < sequence.Count; i++)
        {
            if (sequence[i].DisplayOrder == i) continue;
            sequence[i].DisplayOrder = i;
            sequence[i].UpdatedAt = now;
            sequence[i].UpdatedById = userId;
        }
        await _context.SaveChangesAsync(ct);
        return ServiceResult<List<ParentPrepQuestionModel>>.SuccessResult(sequence.Select(Map).ToList());
    }

    public async Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken ct = default)
    {
        var entity = await _context.ParentPrepQuestions.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (entity == null || !await _access.HasMinimumRoleAsync(entity.ChildProfileId, userId, AccessRole.Collaborator, ct))
            return ServiceResult.FailureResult(QuestionNotFound);
        _context.ParentPrepQuestions.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return ServiceResult.SuccessResult();
    }

    /// <summary>
    /// Sanitize (dangerous blocks go with their content), strip every remaining tag, collapse whitespace onto
    /// one line, THEN measure. Returns the text to persist, or an error.
    /// </summary>
    internal static (string? Text, string? Error) NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (null, TextRequired);
        var plain = PromptText.StripHtml(RichTextSanitizer.Sanitize(text));
        plain = Whitespace.Replace(plain, " ").Trim();
        if (plain.Length == 0) return (null, TextRequired);
        if (plain.Length > MaxTextLength) return (null, $"Question must be {MaxTextLength} characters or fewer.");
        return (plain, null);
    }

    private static IOrderedQueryable<ParentPrepQuestion> OrderedForChild(IQueryable<ParentPrepQuestion> query, int childId)
        => query.Where(q => q.ChildProfileId == childId).OrderBy(q => q.DisplayOrder).ThenBy(q => q.Id);

    private static ParentPrepQuestionModel Map(ParentPrepQuestion q) => new()
    {
        Id = q.Id,
        ChildProfileId = q.ChildProfileId,
        Text = q.Text,
        IsChecked = q.IsChecked,
        DisplayOrder = q.DisplayOrder,
        Source = q.Source,
        CreatedAt = q.CreatedAt,
        UpdatedAt = q.UpdatedAt,
        CreatedById = q.CreatedById
    };
}

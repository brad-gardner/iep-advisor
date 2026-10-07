using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Localization;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 3: <see cref="MeetingPrepService"/> captures the requester's
/// language at create time (<c>GenerateFromGoalsAsync</c>/<c>GenerateFromIepAsync</c>/<c>GenerateFromEtrAsync</c>
/// all run in-request) because generation itself (<c>GenerateChecklistAsync</c>) runs later in
/// <c>MeetingPrepWorker</c>, outside any request — and re-applies it via <see cref="CultureScope"/> so
/// the Claude system prompt gets the response-language line and the checklist's own
/// <see cref="ChecklistItem"/> JSON failure messages come back in the right language.
/// </summary>
public class MeetingPrepLanguageTests
{
    private sealed class AllowAll : IAccessService
    {
        public Task<AccessRole?> GetRoleAsync(int childId, int userId, CancellationToken ct = default) =>
            Task.FromResult<AccessRole?>(AccessRole.Owner);

        public Task<bool> HasMinimumRoleAsync(int childId, int userId, AccessRole minimumRole, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    private sealed class RecordingClaudeClient : IClaudeClient
    {
        private readonly string? _response;
        public RecordingClaudeClient(string? response) => _response = response;
        public List<ClaudeCompletionRequest> Requests { get; } = [];

        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_response);
        }
    }

    private const string CannedMeetingPrepResponse = """
        {
          "questionsToAsk": [{"text":"What baseline was used?","context":"reading","legalBasis":null}],
          "redFlagsToRaise": [{"text":"Vague goal","context":"measurability","legalBasis":null}],
          "preparationNotes": [{"text":"Bring the last report card","context":"evidence","legalBasis":null}]
        }
        """;

    private static MeetingPrepService Service(ApplicationDbContext context, IClaudeClient claudeClient) =>
        new(
            null!,
            null!,
            new ParentAdvocacyGoalRepository(context),
            new AllowAll(),
            new SubscriptionService(context, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), NullLogger<SubscriptionService>.Instance, TestSupport.TestLocalizers.Messages()),
            context,
            claudeClient,
            TestSupport.TestLocalizers.Ai(),
            NullLogger<MeetingPrepService>.Instance);

    [Fact]
    public async Task GenerateFromGoalsAsync_UnderSpanishCulture_CapturesSpanishOnTheChecklist()
    {
        using var fixture = new AnalysisRunTestFixture();
        using var context = fixture.CreateContext();
        var service = Service(context, new RecordingClaudeClient(null));

        ServiceResult<int> result;
        using (CultureScope.For("es"))
            result = await service.GenerateFromGoalsAsync(fixture.ChildId, fixture.OwnerUserId);

        Assert.True(result.Success);
        var checklist = context.Set<MeetingPrepChecklist>().Single(c => c.Id == result.Data);
        Assert.Equal("es", checklist.Language);
    }

    [Fact]
    public async Task GenerateFromGoalsAsync_UnderEnglishCulture_CapturesEnglishOnTheChecklist()
    {
        using var fixture = new AnalysisRunTestFixture();
        using var context = fixture.CreateContext();
        var service = Service(context, new RecordingClaudeClient(null));

        var result = await service.GenerateFromGoalsAsync(fixture.ChildId, fixture.OwnerUserId);

        Assert.True(result.Success);
        var checklist = context.Set<MeetingPrepChecklist>().Single(c => c.Id == result.Data);
        Assert.Equal("en", checklist.Language);
    }

    [Fact]
    public async Task GenerateChecklistAsync_ChecklistCapturedAsSpanish_AppendsResponseLanguageLine_EvenWithNoAmbientCultureAtGenerateTime()
    {
        using var fixture = new AnalysisRunTestFixture();

        int checklistId;
        using (var context = fixture.CreateContext())
        {
            var checklist = new MeetingPrepChecklist
            {
                ChildProfileId = fixture.ChildId,
                Status = "pending",
                Language = "es", // as if captured by GenerateFromGoalsAsync under a Spanish request
                CreatedById = fixture.OwnerUserId,
                UpdatedById = fixture.OwnerUserId
            };
            context.Set<MeetingPrepChecklist>().Add(checklist);
            context.SaveChanges();
            checklistId = checklist.Id;
        }

        // No CultureScope wraps this call — simulating MeetingPrepWorker's own ambient culture.
        var client = new RecordingClaudeClient(CannedMeetingPrepResponse);
        using (var execContext = fixture.CreateContext())
            await Service(execContext, client).GenerateChecklistAsync(checklistId, CancellationToken.None);

        var spanishMarker = ResponseLanguage.SystemLine(System.Globalization.CultureInfo.GetCultureInfo("es"));
        Assert.Contains(spanishMarker, Assert.Single(client.Requests).SystemPrompt);

        using var verify = fixture.CreateContext();
        Assert.Equal("completed", verify.Set<MeetingPrepChecklist>().Single(c => c.Id == checklistId).Status);
    }

    [Fact]
    public async Task GenerateChecklistAsync_ChecklistCapturedAsEnglish_NeverAppendsResponseLanguageLine()
    {
        using var fixture = new AnalysisRunTestFixture();

        int checklistId;
        using (var context = fixture.CreateContext())
        {
            var checklist = new MeetingPrepChecklist
            {
                ChildProfileId = fixture.ChildId,
                Status = "pending",
                Language = "en",
                CreatedById = fixture.OwnerUserId,
                UpdatedById = fixture.OwnerUserId
            };
            context.Set<MeetingPrepChecklist>().Add(checklist);
            context.SaveChanges();
            checklistId = checklist.Id;
        }

        var client = new RecordingClaudeClient(CannedMeetingPrepResponse);
        using (var execContext = fixture.CreateContext())
            await Service(execContext, client).GenerateChecklistAsync(checklistId, CancellationToken.None);

        Assert.DoesNotContain("RESPONSE LANGUAGE", Assert.Single(client.Requests).SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckItemAsync_InvalidSection_UnderSpanishCulture_MessageIsSpanish_ErrorKindIsValidation()
    {
        using var fixture = new AnalysisRunTestFixture();
        int checklistId;
        using (var context = fixture.CreateContext())
        {
            var checklist = new MeetingPrepChecklist
            {
                ChildProfileId = fixture.ChildId, Status = "completed",
                CreatedById = fixture.OwnerUserId, UpdatedById = fixture.OwnerUserId
            };
            context.Set<MeetingPrepChecklist>().Add(checklist);
            context.SaveChanges();
            checklistId = checklist.Id;
        }

        using var ctx = fixture.CreateContext();
        var service = Service(ctx, new RecordingClaudeClient(null));

        ServiceResult result;
        using (CultureScope.For("es"))
        {
            result = await service.CheckItemAsync(
                checklistId, fixture.OwnerUserId,
                new CheckItemRequest { Section = "not-a-real-section", Index = 0, IsChecked = true },
                CancellationToken.None);
        }

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);
        Assert.Equal("Sección no válida", result.Message);
    }
}

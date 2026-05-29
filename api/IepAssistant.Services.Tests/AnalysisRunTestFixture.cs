using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Builds a real (SQLite in-memory) <see cref="ApplicationDbContext"/>. EF InMemory cannot be
/// used here because <c>TryRecordUsageAsync</c> opens a real serializable transaction, which the
/// InMemory provider does not support. SQLite gives us a real relational engine in-process.
/// The connection is held open for the lifetime of the fixture so the in-memory DB survives.
/// </summary>
public sealed class AnalysisRunTestFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public int OwnerUserId { get; private set; }
    public int ChildId { get; private set; }

    public AnalysisRunTestFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
        Seed(context);
    }

    public ApplicationDbContext CreateContext() => new(_options);

    private void Seed(ApplicationDbContext context)
    {
        var user = new User
        {
            Email = "parent@example.com",
            PasswordHash = "x",
            FirstName = "Pat",
            LastName = "Parent",
            SubscriptionStatus = "active",
            SubscriptionExpiresAt = DateTime.UtcNow.AddYears(1)
        };
        context.Users.Add(user);
        context.SaveChanges();
        OwnerUserId = user.Id;

        var child = new ChildProfile
        {
            UserId = user.Id,
            FirstName = "Casey",
            LastName = "Child",
            IsActive = true
        };
        context.ChildProfiles.Add(child);
        context.SaveChanges();
        ChildId = child.Id;

        // Owner has Collaborator+ access (accepted, active).
        context.ChildAccesses.Add(new ChildAccess
        {
            ChildProfileId = child.Id,
            UserId = user.Id,
            Role = AccessRole.Owner,
            IsActive = true,
            AcceptedAt = DateTime.UtcNow
        });
        context.SaveChanges();
    }

    /// <summary>Creates a parsed IEP document with one section + goal. Returns its id.</summary>
    public int SeedIepDocument(string meetingType = "annual_review")
    {
        using var context = CreateContext();
        var doc = new IepDocument
        {
            ChildProfileId = ChildId,
            MeetingType = meetingType,
            IepDate = new DateTime(2025, 3, 12),
            Status = "parsed",
            IsActive = true
        };
        context.IepDocuments.Add(doc);
        context.SaveChanges();

        var section = new IepSection
        {
            IepDocumentId = doc.Id,
            SectionType = "annual_goals",
            RawText = "Student will improve reading fluency.",
            DisplayOrder = 0
        };
        context.IepSections.Add(section);
        context.SaveChanges();

        context.Goals.Add(new Goal
        {
            IepSectionId = section.Id,
            GoalText = "Read 100 wpm with 90% accuracy",
            Domain = "Reading"
        });
        context.SaveChanges();

        return doc.Id;
    }

    /// <summary>Creates a parsed ETR document with one section. Returns its id.</summary>
    public int SeedEtrDocument()
    {
        using var context = CreateContext();
        var doc = new EtrDocument
        {
            ChildProfileId = ChildId,
            EvaluationType = "reevaluation",
            EvaluationDate = new DateTime(2024, 11, 1),
            Status = "parsed",
            IsActive = true
        };
        context.EtrDocuments.Add(doc);
        context.SaveChanges();

        context.EtrSections.Add(new EtrSection
        {
            EtrDocumentId = doc.Id,
            SectionType = "eligibility",
            RawText = "Student qualifies under SLD.",
            DisplayOrder = 0
        });
        context.SaveChanges();

        return doc.Id;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}

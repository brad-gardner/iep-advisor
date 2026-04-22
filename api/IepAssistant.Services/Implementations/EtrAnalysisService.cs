using System.Text;
using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class EtrAnalysisService : IEtrAnalysisService
{
    private readonly IEtrDocumentRepository _documentRepository;
    private readonly IEtrAnalysisRepository _analysisRepository;
    private readonly IAccessService _accessService;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EtrAnalysisService> _logger;

    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public EtrAnalysisService(
        IEtrDocumentRepository documentRepository,
        IEtrAnalysisRepository analysisRepository,
        IAccessService accessService,
        ApplicationDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<EtrAnalysisService> logger)
    {
        _documentRepository = documentRepository;
        _analysisRepository = analysisRepository;
        _accessService = accessService;
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<EtrAnalysisModel?> GetAnalysisAsync(int etrDocumentId, int userId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(etrDocumentId, cancellationToken);
        if (document == null)
            return null;

        var role = await _accessService.GetRoleAsync(document.ChildProfileId, userId, cancellationToken);
        if (role == null)
            return null;

        var analysis = await _analysisRepository.GetByDocumentAsync(etrDocumentId, cancellationToken);
        if (analysis == null)
            return null;

        return MapToModel(analysis);
    }

    public Task<bool> CheckEtrAnalysisLimitAsync(int userId, int childId, CancellationToken cancellationToken = default)
    {
        // TODO: Enforce a per-child ETR analysis limit distinct from IEP limits.
        // See plan: docs/plans/2026-04-22-001-feat-etr-meeting-workflow-plan.md (Phase 3 — "separate ETR analysis limit in subscription config").
        // This currently returns true unconditionally; wire up a UsageRecord operationType like "etr_analysis"
        // (mirroring ISubscriptionService.CanPerformAnalysisAsync / TryRecordUsageAsync) before shipping to prod.
        return Task.FromResult(true);
    }

    public async Task AnalyzeDocumentAsync(int etrDocumentId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(etrDocumentId, cancellationToken);
        if (document == null)
        {
            _logger.LogWarning("ETR {EtrId} not found for analysis", etrDocumentId);
            return;
        }

        if (document.Status != "parsed")
        {
            _logger.LogWarning("ETR {EtrId} is not in 'parsed' status (current: {Status}); cannot analyze", etrDocumentId, document.Status);
            return;
        }

        var analysis = await _analysisRepository.GetByDocumentAsync(etrDocumentId, cancellationToken);
        if (analysis == null)
        {
            analysis = new EtrAnalysis { EtrDocumentId = etrDocumentId };
            await _analysisRepository.AddAsync(analysis, cancellationToken);
        }

        analysis.Status = "analyzing";
        analysis.ErrorMessage = null;
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var sections = await _context.EtrSections
                .Where(s => s.EtrDocumentId == etrDocumentId)
                .OrderBy(s => s.DisplayOrder)
                .ToListAsync(cancellationToken);

            if (sections.Count == 0)
            {
                analysis.Status = "error";
                analysis.ErrorMessage = "No parsed sections found. ETR must be parsed before analysis.";
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            var etrContent = BuildEtrContentForAnalysis(sections);
            var analysisResult = await AnalyzeWithClaudeAsync(etrContent, cancellationToken);

            if (analysisResult == null)
            {
                analysis.Status = "error";
                analysis.ErrorMessage = "Failed to generate analysis.";
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            analysis.AssessmentCompleteness = analysisResult.AssessmentCompleteness != null
                ? JsonSerializer.Serialize(analysisResult.AssessmentCompleteness, SnakeCaseOptions)
                : null;
            analysis.EligibilityReview = analysisResult.EligibilityReview != null
                ? JsonSerializer.Serialize(analysisResult.EligibilityReview, SnakeCaseOptions)
                : null;
            analysis.OverallRedFlags = JsonSerializer.Serialize(analysisResult.RedFlags, SnakeCaseOptions);
            analysis.SuggestedQuestions = JsonSerializer.Serialize(analysisResult.SuggestedQuestions, SnakeCaseOptions);
            analysis.OverallSummary = analysisResult.OverallSummary;
            analysis.Status = "completed";

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("ETR analysis completed for document {EtrId}", etrDocumentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing ETR {EtrId}", etrDocumentId);
            try
            {
                analysis.Status = "error";
                analysis.ErrorMessage = "An unexpected error occurred during analysis.";
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to persist error status for ETR {EtrId}", etrDocumentId);
            }
            // Do not rethrow — worker must not die.
        }
    }

    private static string BuildEtrContentForAnalysis(List<EtrSection> sections)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== ETR DOCUMENT CONTENT ===\n");

        foreach (var section in sections)
        {
            sb.AppendLine($"--- SECTION: {section.SectionType} ---");
            if (!string.IsNullOrEmpty(section.RawText))
                sb.AppendLine(section.RawText);

            if (!string.IsNullOrEmpty(section.ParsedContent))
            {
                sb.AppendLine("\n[Structured content for this section]:");
                sb.AppendLine(section.ParsedContent);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private async Task<EtrAnalysisResponse?> AnalyzeWithClaudeAsync(string etrContent, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Anthropic:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("Anthropic API key not configured");
            return null;
        }

        var httpClient = _httpClientFactory.CreateClient("Claude");
        var client = new AnthropicClient(apiKey, httpClient);

        var systemPrompt = @"You are an expert analyst of Evaluation Team Reports (ETRs) — the multidisciplinary
evaluation documents U.S. public schools use to determine whether a student is eligible for special education
under IDEA. You are helping a PARENT advocate for their child at an upcoming ETR meeting. ETRs are ELIGIBILITY
evaluations: they determine disability category and whether special education is warranted. ETRs do NOT contain
IEP goals, services, or placement decisions — do not critique the document for missing those.

Analyze the provided ETR content across FOUR PILLARS and return a single JSON object with exactly these top-level keys:

{
  ""assessment_completeness"": {
    ""evaluated_domains"": [
      {
        ""domain"": ""e.g., Cognitive, Academic Achievement, Behavioral/Social-Emotional, Adaptive, Communication/Speech-Language, Motor/Sensory, Health/Vision/Hearing"",
        ""tools_used"": [""WISC-V"", ""BASC-3""],
        ""adequacy_rating"": ""strong"" | ""adequate"" | ""thin"" | ""missing"",
        ""notes"": ""Short note on why the rating was given""
      }
    ],
    ""missing_domains"": [
      { ""domain"": ""e.g., Adaptive behavior"", ""rationale"": ""Why this domain should have been evaluated given the presenting concerns"" }
    ],
    ""overall_completeness_rating"": ""strong"" | ""adequate"" | ""thin"" | ""concerning""
  },

  ""eligibility_review"": {
    ""stated_category"": ""The disability category the team stated (or null if not stated)"",
    ""stated_conclusion"": ""qualifies / does not qualify / deferred / etc., as stated"",
    ""data_supports_conclusion"": true or false,
    ""supporting_evidence"": [""Specific data points from the report that support the stated conclusion""],
    ""contradicting_evidence"": [""Specific data points from the report that contradict or complicate the stated conclusion""],
    ""alternative_considerations"": [""Other disability categories or determinations the data could reasonably support""],
    ""notes"": ""Short summary of the eligibility analysis""
  },

  ""red_flags"": [
    {
      ""severity"": ""high"" | ""medium"" | ""low"",
      ""category"": ""outdated_testing"" | ""missing_domain"" | ""boilerplate"" | ""procedural"" | ""under_evaluation"" | ""other"",
      ""finding"": ""What the concern is, stated plainly"",
      ""why_it_matters"": ""Why this matters for the child and the eligibility decision"",
      ""parent_right_implicated"": ""Optional — the specific parent right relevant here (e.g., right to an Independent Educational Evaluation at public expense, right to prior written notice, right to participate in eligibility determination)""
    }
  ],

  ""suggested_questions"": [
    {
      ""category"": ""clarification"" | ""challenge_eligibility"" | ""iee_request"" | ""procedural"" | ""services_next_steps"",
      ""question"": ""The actual question a parent should ask at the meeting, phrased for the parent to use verbatim"",
      ""rationale"": ""Why this question matters given what the ETR says""
    }
  ],

  ""overall_summary"": ""A 2-4 sentence plain-language summary of what this ETR concludes and what the parent should focus on at the meeting.""
}

KEY CONTEXT — IDEA disability categories the team may rely on:
Autism, Deaf-Blindness, Deafness, Emotional Disturbance, Hearing Impairment, Intellectual Disability,
Multiple Disabilities, Orthopedic Impairment, Other Health Impairment, Specific Learning Disability,
Speech or Language Impairment, Traumatic Brain Injury, Visual Impairment (including Blindness),
Developmental Delay (ages 3 through 9 at state/LEA discretion).

CHILD FIND / SUSPECTED AREAS OF DISABILITY RULE:
Under IDEA (34 CFR 300.304(c)(4), 300.301), a child must be assessed in ALL areas related to the suspected
disability. If the referral reason, background, parent input, or any evaluator's notes raise concern about a
domain (e.g., attention, adaptive functioning, language pragmatics, sensory processing), that domain must be
evaluated. Flag under-evaluation any time a domain of concern was raised but not formally assessed with an
appropriate tool. This is one of the most important issues to surface for a parent.

ANALYSIS PRINCIPLES:
- Do NOT invent data. Every finding must be traceable to content in the ETR sections provided.
- When citing evidence, reference the section_type where the evidence appears (e.g., ""as noted in parent_input"",
  ""per the cognitive section's WISC-V results"").
- Be honest about what the data shows. If the data genuinely supports the team's conclusion, say so; do not
  manufacture concerns. If the data does NOT support it, say that clearly and explain why.
- Watch for outdated testing (over 3 years old generally warrants re-evaluation), boilerplate/copy-paste
  language with no individualized findings, missing domains given the referral concerns, procedural issues
  (missing signatures, late timelines, no parent consent documentation), and thin/single-source evaluations.
- Suggested questions should be SPECIFIC to this ETR, not generic. A parent should be able to walk into the
  meeting and read them aloud.

OUTPUT RULES:
- Return ONLY the JSON object. Do NOT wrap it in markdown code fences. No leading or trailing prose.
- Use the exact top-level keys listed above.
- If a pillar genuinely has nothing to report (e.g., no red flags found), return an empty array — do not omit the key.";

        var content = new List<ContentBase>
        {
            new TextContent
            {
                Text = $"Analyze this Evaluation Team Report across the four pillars and provide a parent-focused analysis per the system instructions.\n\n{etrContent}",
            },
        };

        var messages = new List<Message>
        {
            new Message { Role = RoleType.User, Content = content },
        };

        var parameters = new MessageParameters
        {
            Messages = messages,
            Model = "claude-sonnet-4-20250514",
            MaxTokens = 32000,
            System = [new SystemMessage(systemPrompt)],
        };

        var response = await client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);

        var responseText = (response.Content?.FirstOrDefault() as TextContent)?.Text;
        if (string.IsNullOrEmpty(responseText))
        {
            _logger.LogWarning("Empty response from Claude for ETR analysis");
            return null;
        }

        var stopReason = response.StopReason;
        _logger.LogInformation(
            "ETR analysis Claude response: {Length} chars, stop_reason={StopReason}",
            responseText.Length, stopReason);

        // Strip markdown code fences defensively in case Claude adds them
        responseText = responseText.Trim();
        if (responseText.StartsWith("```"))
        {
            var firstNewline = responseText.IndexOf('\n');
            if (firstNewline >= 0)
                responseText = responseText[(firstNewline + 1)..];
            if (responseText.EndsWith("```"))
                responseText = responseText[..^3];
            responseText = responseText.Trim();
        }

        try
        {
            return JsonSerializer.Deserialize<EtrAnalysisResponse>(responseText, CaseInsensitiveOptions);
        }
        catch (JsonException ex)
        {
            var head = responseText[..Math.Min(500, responseText.Length)];
            var tail = responseText[Math.Max(0, responseText.Length - 500)..];
            _logger.LogError(ex,
                "Failed to parse Claude ETR analysis response as JSON. stop_reason={StopReason}, total_length={Length}. HEAD: {Head} ... TAIL: {Tail}",
                stopReason, responseText.Length, head, tail);
            return null;
        }
    }

    private static EtrAnalysisModel MapToModel(EtrAnalysis entity)
    {
        return new EtrAnalysisModel
        {
            Id = entity.Id,
            EtrDocumentId = entity.EtrDocumentId,
            Status = entity.Status,
            AssessmentCompleteness = DeserializeOrNull<AssessmentCompletenessResult>(entity.AssessmentCompleteness),
            EligibilityReview = DeserializeOrNull<EligibilityReviewResult>(entity.EligibilityReview),
            OverallRedFlags = DeserializeOrEmpty<List<EtrRedFlag>>(entity.OverallRedFlags),
            SuggestedQuestions = DeserializeOrEmpty<List<EtrSuggestedQuestion>>(entity.SuggestedQuestions),
            OverallSummary = entity.OverallSummary,
            ErrorMessage = entity.ErrorMessage,
            CreatedAt = entity.CreatedAt,
        };
    }

    private static T? DeserializeOrNull<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, CaseInsensitiveOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static T DeserializeOrEmpty<T>(string? json) where T : new()
    {
        if (string.IsNullOrEmpty(json))
            return new T();

        try
        {
            return JsonSerializer.Deserialize<T>(json, CaseInsensitiveOptions) ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }
}

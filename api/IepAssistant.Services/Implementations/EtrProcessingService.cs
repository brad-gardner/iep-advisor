using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class EtrProcessingService : IEtrProcessingService
{
    private readonly IEtrDocumentRepository _documentRepository;
    private readonly IEtrSectionRepository _sectionRepository;
    private readonly IAccessService _accessService;
    private readonly IBlobStorageService _blobStorage;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EtrProcessingService> _logger;

    public EtrProcessingService(
        IEtrDocumentRepository documentRepository,
        IEtrSectionRepository sectionRepository,
        IAccessService accessService,
        IBlobStorageService blobStorage,
        ApplicationDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<EtrProcessingService> logger)
    {
        _documentRepository = documentRepository;
        _sectionRepository = sectionRepository;
        _accessService = accessService;
        _blobStorage = blobStorage;
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task ProcessDocumentAsync(int etrId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(etrId, cancellationToken);
        if (document == null)
        {
            _logger.LogWarning("ETR {EtrId} not found for processing", etrId);
            return;
        }

        if (document.Status == "processing")
        {
            _logger.LogInformation("ETR {EtrId} is already processing; skipping", etrId);
            return;
        }

        if (string.IsNullOrEmpty(document.BlobUri))
        {
            _logger.LogWarning("ETR {EtrId} has no uploaded file; cannot process", etrId);
            return;
        }

        try
        {
            document.Status = "processing";
            _documentRepository.Update(document);
            await _context.SaveChangesAsync(cancellationToken);

            var pdfBytes = await DownloadPdfBytesAsync(document.BlobUri, cancellationToken);
            if (pdfBytes.Length == 0)
            {
                _logger.LogWarning("Empty PDF downloaded for ETR {EtrId}", etrId);
                document.Status = "error";
                _documentRepository.Update(document);
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            var parsed = await StructureWithClaudeAsync(pdfBytes, cancellationToken);
            if (parsed == null)
            {
                document.Status = "error";
                _documentRepository.Update(document);
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            // Delete existing sections (supports re-processing)
            await _sectionRepository.DeleteAllByDocumentAsync(etrId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var order = 1;
            foreach (var section in parsed.Sections.OrderBy(s => s.DisplayOrder == 0 ? int.MaxValue : s.DisplayOrder))
            {
                var entity = new EtrSection
                {
                    EtrDocumentId = etrId,
                    SectionType = section.SectionType,
                    RawText = section.RawText,
                    ParsedContent = section.ParsedContent.HasValue
                        ? section.ParsedContent.Value.GetRawText()
                        : null,
                    DisplayOrder = section.DisplayOrder > 0 ? section.DisplayOrder : order,
                };
                await _sectionRepository.AddAsync(entity, cancellationToken);
                order++;
            }
            await _context.SaveChangesAsync(cancellationToken);

            document.Status = "parsed";
            _documentRepository.Update(document);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("ETR {EtrId} processed successfully: {SectionCount} sections", etrId, parsed.Sections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ETR {EtrId}", etrId);
            try
            {
                document.Status = "error";
                _documentRepository.Update(document);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to persist error status for ETR {EtrId}", etrId);
            }
            // Do not rethrow — worker must not die.
        }
    }

    public async Task<IEnumerable<EtrSectionModel>> GetSectionsAsync(int etrId, int userId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(etrId, cancellationToken);
        if (document == null)
            return [];

        var role = await _accessService.GetRoleAsync(document.ChildProfileId, userId, cancellationToken);
        if (role == null)
            return [];

        var sections = await _sectionRepository.GetByDocumentAsync(etrId, cancellationToken);

        return sections.Select(s => new EtrSectionModel
        {
            Id = s.Id,
            SectionType = s.SectionType,
            RawText = s.RawText,
            ParsedContent = s.ParsedContent,
            DisplayOrder = s.DisplayOrder,
        });
    }

    private async Task<byte[]> DownloadPdfBytesAsync(string blobPath, CancellationToken cancellationToken)
    {
        using var stream = await _blobStorage.DownloadAsync(blobPath, cancellationToken);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    private async Task<ParsedEtr?> StructureWithClaudeAsync(byte[] pdfBytes, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Anthropic:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("Anthropic API key not configured");
            return null;
        }

        var httpClient = _httpClientFactory.CreateClient("Claude");
        var client = new AnthropicClient(apiKey, httpClient);

        var systemPrompt = @"You are an expert parser of Evaluation Team Reports (ETRs) — the multidisciplinary
evaluation documents that U.S. public schools use to determine a student's eligibility for special education
services under IDEA. ETRs differ from IEPs: they focus on ASSESSMENT RESULTS and ELIGIBILITY DETERMINATION,
not on goals, services, or placement. Do not try to extract IEP-style goals, services, or accommodations — those
belong to a later IEP document, not the ETR.

Your job is to extract and structure the COMPLETE content of the ETR into a fixed taxonomy of sections,
returned as JSON. Output ONLY the JSON object described below. No prose, no markdown fences, no commentary.

Return this exact shape:
{
  ""sections"": [
    {
      ""section_type"": <one of the allowed values below>,
      ""raw_text"": ""Verbatim text copied from the document for this section. Preserve quantitative data exactly — test names, standard scores, percentiles, confidence intervals, subtest scores, dates, examiner names."",
      ""parsed_content"": { /* structured object whose shape depends on section_type — see below */ },
      ""display_order"": 1
    }
  ]
}

Allowed section_type values (use exactly these strings; choose the single best fit per section):
- referral_reason            — Why the child was referred / presenting concerns
- background                 — Developmental, medical, educational history; previous interventions
- parent_input               — Parent / guardian concerns, observations, input statements
- assessments_administered   — List of every evaluation tool used (e.g., WISC-V, WIAT-4, BASC-3, Vineland, CTOPP-2)
- cognitive                  — Cognitive / intellectual assessment findings (IQ, processing)
- academic                   — Academic achievement findings (reading, math, writing)
- behavioral_social_emotional — BASC, social-emotional, behavioral rating scales, observations
- adaptive                   — Adaptive behavior (Vineland, ABAS)
- communication              — Speech-language evaluation, pragmatics, articulation, language
- motor_sensory              — OT / PT evaluations, sensory processing, fine/gross motor
- health_vision_hearing      — Medical, vision, hearing, health history results
- eligibility_determination  — Team determination: disability category, qualifies/does not qualify, reasoning
- team_recommendations       — Team's recommendations for services, supports, re-evaluation timelines
- other                      — Any clearly identifiable section that does not fit above

parsed_content shape guidance (produce the keys that apply; omit fields you do not find — do not invent data):

- assessments_administered:
  {
    ""assessments"": [
      { ""name"": ""WISC-V"", ""date"": ""2026-01-15"", ""examiner"": ""Dr. Jane Smith, School Psychologist"", ""purpose"": ""Cognitive evaluation"" }
    ]
  }

- cognitive / academic / behavioral_social_emotional / adaptive / communication / motor_sensory:
  {
    ""assessments"": [
      {
        ""name"": ""WISC-V"",
        ""date"": ""2026-01-15"",
        ""examiner"": ""Dr. Jane Smith"",
        ""scores"": [
          { ""subtest"": ""Full Scale IQ"", ""standard_score"": 88, ""percentile"": 21, ""confidence_interval"": ""83-94"", ""classification"": ""Low Average"" }
        ],
        ""narrative"": ""Summary of examiner's interpretation as written in the report.""
      }
    ],
    ""observations"": [""Relevant observation quotes or summaries""],
    ""concerns"": [""Domain-specific concerns the evaluators flagged""]
  }

- health_vision_hearing:
  {
    ""vision"": { ""result"": ""Passed / Failed / Not tested"", ""detail"": ""..."" },
    ""hearing"": { ""result"": ""..."", ""detail"": ""..."" },
    ""medical_history"": [""Relevant medical items""]
  }

- referral_reason / background / parent_input:
  {
    ""summary"": ""Short plain summary"",
    ""key_points"": [""Point 1"", ""Point 2""]
  }

- eligibility_determination:
  {
    ""disability_category"": ""e.g., Specific Learning Disability"",
    ""determination"": ""qualifies"" or ""does_not_qualify"" or ""deferred"",
    ""reasoning"": ""The team's written rationale verbatim or closely paraphrased"",
    ""adverse_educational_impact"": ""How the disability affects educational performance, as stated""
  }

- team_recommendations:
  {
    ""recommendations"": [""Recommendation 1"", ""Recommendation 2""],
    ""next_review_date"": ""YYYY-MM-DD or null""
  }

- other:
  { ""title"": ""Section heading as it appears"", ""summary"": ""..."" }

Rules:
- Return ONLY the JSON object. No markdown code fences, no leading/trailing prose.
- Do NOT invent data. If a section or field is not present in the document, omit it. If an entire section type is absent, do not emit an entry for it.
- Preserve ALL quantitative data verbatim — standard scores, percentiles, confidence intervals, dates, examiner names.
- Use display_order starting at 1, following the order the sections appear in the document.
- Each section_type should appear at most once; combine fragmented discussion of the same domain into a single entry.
- This is an ETR, not an IEP: do NOT emit goals, services, accommodations, or placement content. If the document contains
  such content (some districts mix), capture it under section_type ""other"" rather than forcing an IEP schema.";

        var pdfBase64 = Convert.ToBase64String(pdfBytes);

        var content = new List<ContentBase>
        {
            new DocumentContent
            {
                Source = new DocumentSource
                {
                    MediaType = "application/pdf",
                    Data = pdfBase64,
                },
            },
            new TextContent
            {
                Text = "Parse this ETR document and return structured JSON per the system instructions. Extract every section present, preserve all assessment scores verbatim, and do not invent data.",
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
            _logger.LogWarning("Empty response from Claude for ETR parsing");
            return null;
        }

        var stopReason = response.StopReason;
        _logger.LogInformation(
            "ETR parsing Claude response: {Length} chars, stop_reason={StopReason}",
            responseText.Length, stopReason);

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
            return JsonSerializer.Deserialize<ParsedEtr>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            var head = responseText[..Math.Min(500, responseText.Length)];
            var tail = responseText[Math.Max(0, responseText.Length - 500)..];
            _logger.LogError(ex,
                "Failed to parse Claude ETR response as JSON. stop_reason={StopReason}, total_length={Length}. HEAD: {Head} ... TAIL: {Tail}",
                stopReason, responseText.Length, head, tail);
            return null;
        }
    }
}

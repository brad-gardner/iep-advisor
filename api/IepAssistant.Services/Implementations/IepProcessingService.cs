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

public class IepProcessingService : IIepProcessingService
{
    private readonly IIepDocumentRepository _documentRepository;
    private readonly IChildProfileRepository _childProfileRepository;
    private readonly IAccessService _accessService;
    private readonly IBlobStorageService _blobStorage;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IepProcessingService> _logger;

    public IepProcessingService(
        IIepDocumentRepository documentRepository,
        IChildProfileRepository childProfileRepository,
        IAccessService accessService,
        IBlobStorageService blobStorage,
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<IepProcessingService> logger)
    {
        _documentRepository = documentRepository;
        _childProfileRepository = childProfileRepository;
        _accessService = accessService;
        _blobStorage = blobStorage;
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ProcessDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document == null)
        {
            _logger.LogWarning("Document {DocumentId} not found for processing", documentId);
            return;
        }

        try
        {
            document.Status = "processing";
            _documentRepository.Update(document);
            await _context.SaveChangesAsync(cancellationToken);

            // Step 1: Download PDF bytes
            var pdfBytes = await DownloadPdfBytesAsync(document.BlobUri, cancellationToken);

            if (pdfBytes.Length == 0)
            {
                _logger.LogWarning("Empty PDF downloaded for document {DocumentId}", documentId);
                document.Status = "error";
                _documentRepository.Update(document);
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            // Step 2: Send PDF directly to Claude for structuring
            var parsed = await StructureWithClaudeAsync(pdfBytes, cancellationToken);

            if (parsed == null)
            {
                document.Status = "error";
                _documentRepository.Update(document);
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            // Step 3: Store structured results
            var order = 0;
            foreach (var section in parsed.Sections)
            {
                var sectionEntity = new IepSection
                {
                    IepDocumentId = documentId,
                    SectionType = section.SectionType,
                    RawText = section.Content,
                    ParsedContent = JsonSerializer.Serialize(section),
                    DisplayOrder = order++,
                };

                await _context.IepSections.AddAsync(sectionEntity, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                if (section.Goals != null)
                {
                    foreach (var goal in section.Goals)
                    {
                        var goalEntity = new Goal
                        {
                            IepSectionId = sectionEntity.Id,
                            GoalText = goal.GoalText,
                            Domain = goal.Domain,
                            Baseline = goal.Baseline,
                            TargetCriteria = goal.TargetCriteria,
                            MeasurementMethod = goal.MeasurementMethod,
                            Timeframe = goal.Timeframe,
                        };

                        await _context.Goals.AddAsync(goalEntity, cancellationToken);
                    }
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            document.Status = "parsed";
            _documentRepository.Update(document);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Document {DocumentId} processed successfully: {SectionCount} sections", documentId, parsed.Sections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document {DocumentId}", documentId);
            document.Status = "error";
            _documentRepository.Update(document);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<IepSectionModel>> GetSectionsAsync(int documentId, int userId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdWithChildAsync(documentId, cancellationToken);
        if (document == null)
            return [];

        var role = await _accessService.GetRoleAsync(document.ChildProfileId, userId, cancellationToken);
        if (role == null)
            return [];

        var sections = await _context.IepSections
            .Where(s => s.IepDocumentId == documentId)
            .Include(s => s.Goals)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(cancellationToken);

        return sections.Select(s => new IepSectionModel
        {
            Id = s.Id,
            SectionType = s.SectionType,
            RawText = s.RawText,
            ParsedContent = s.ParsedContent,
            DisplayOrder = s.DisplayOrder,
            Goals = s.Goals.Select(g => new GoalModel
            {
                Id = g.Id,
                GoalText = g.GoalText,
                Domain = g.Domain,
                Baseline = g.Baseline,
                TargetCriteria = g.TargetCriteria,
                MeasurementMethod = g.MeasurementMethod,
                Timeframe = g.Timeframe,
            }).ToList()
        });
    }

    private async Task<byte[]> DownloadPdfBytesAsync(string blobPath, CancellationToken cancellationToken)
    {
        using var stream = await _blobStorage.DownloadAsync(blobPath, cancellationToken);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    private async Task<ParsedIep?> StructureWithClaudeAsync(byte[] pdfBytes, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Anthropic:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("Anthropic API key not configured");
            return null;
        }

        var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var client = new AnthropicClient(apiKey, httpClient);

        var systemPrompt = @"You are an expert IEP (Individualized Education Program) document parser.
Your job is to extract and structure the COMPLETE content of an IEP document into standard sections.

Return a JSON object with the following structure:
{
  ""sections"": [
    {
      ""sectionType"": ""student_profile"" | ""present_levels"" | ""evaluations"" | ""assessments"" | ""eligibility"" | ""annual_goals"" | ""services"" | ""accommodations"" | ""placement"" | ""transition"" | ""progress_monitoring"" | ""other"",
      ""title"": ""The section title as it appears in the document"",
      ""content"": ""The full text content of this section"",
      ""goals"": [  // Only include for annual_goals section
        {
          ""goalText"": ""The full text of the goal"",
          ""domain"": ""e.g., Reading, Math, Behavior, Speech/Language, etc."",
          ""baseline"": ""The student's current performance level for this goal"",
          ""targetCriteria"": ""The measurable target the student should achieve"",
          ""measurementMethod"": ""How progress will be measured"",
          ""timeframe"": ""When the goal should be achieved""
        }
      ]
    }
  ]
}

Section type guide:
- student_profile: Student demographics, contact info, disability classification, meeting participants
- present_levels: Present levels of academic achievement and functional performance
- evaluations: Evaluation reports, psychological evaluations, specialist assessments
- assessments: Standardized test scores, benchmark data, percentiles, standard scores, grade equivalents
- eligibility: Eligibility determination, disability category, qualification criteria
- annual_goals: Measurable annual goals and short-term objectives
- services: Special education services, related services, frequency and duration
- accommodations: Classroom accommodations, testing accommodations, modifications
- placement: Least restrictive environment, placement decisions, participation with nondisabled peers
- transition: Transition planning, post-secondary goals, employment/education plans
- progress_monitoring: Progress reports, data collection methods, reporting schedules
- other: Any section that does not fit the above categories

Rules:
- Extract ALL sections you can identify from the document — do not skip any content
- Preserve ALL quantitative data: test scores, percentiles, standard scores, grade equivalents, age equivalents
- For tables of test scores, format them clearly in the content field (e.g., ""Test Name: Score (Percentile)"")
- Include evaluation dates, evaluator names, and assessment instruments when present
- For the annual_goals section, extract each individual goal with its structured fields
- If a field cannot be determined, set it to null
- Preserve the original text as closely as possible in the content field
- Each unique section in the document should map to exactly one entry — do not create duplicate sectionType entries
- Return ONLY valid JSON, no markdown or other formatting";

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
                Text = "Parse this IEP document and return structured JSON. Extract every section completely, including all test scores, evaluation data, and assessment results.",
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
            MaxTokens = 16384,
            System = [new SystemMessage(systemPrompt)],
        };

        var response = await client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);

        var responseText = (response.Content?.FirstOrDefault() as TextContent)?.Text;
        if (string.IsNullOrEmpty(responseText))
        {
            _logger.LogWarning("Empty response from Claude");
            return null;
        }

        // Strip markdown code fences if present
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
            return JsonSerializer.Deserialize<ParsedIep>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Claude response as JSON");
            return null;
        }
    }
}

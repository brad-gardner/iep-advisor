using IepAssistant.Domain.Entities;

namespace IepAssistant.Api.Seeding;

/// <summary>
/// The narrative content <see cref="DemoSeeder"/> writes into each demo IEP and ETR. Goals, services and
/// accommodations are chosen by the student's disability category and grade band, so two students in the
/// demo district never read like copies of each other — a reading/math pair for a specific learning
/// disability, articulation and language goals for a speech impairment, transition goals once a student
/// reaches grade 9, and so on. All of it is fictional.
/// </summary>
public static class DemoIepContent
{
    /// <param name="Unit">The unit progress observations against this goal are recorded in.</param>
    public sealed record GoalPlan(
        string Domain,
        string GoalText,
        string Baseline,
        string TargetCriteria,
        string MeasurementMethod,
        string Timeframe,
        string Unit);

    public sealed record ServiceLine(string ServiceType, string Frequency, string Duration, string Location, string ProviderRole);

    public sealed record AccommodationLine(string Category, string Text);

    public sealed record TransitionLine(string GoalArea, string PostsecondaryGoal, string Services, string Responsible);

    // ------------------------------------------------------------------------------------------- Goals

    /// <summary>Two to three goals for a student, by disability category. <paramref name="name"/> is the
    /// student's first name; goal text reads the way a case manager writes it.</summary>
    public static IReadOnlyList<GoalPlan> GoalsFor(DisabilityCategory? disability, GradeLevel grade, string name) =>
        disability switch
        {
            DisabilityCategory.SpecificLearningDisability => new[]
            {
                new GoalPlan("Reading",
                    $"Given a grade-level passage, {name} will read aloud with 95% accuracy and answer literal and inferential comprehension questions with 80% accuracy on 4 of 5 trials.",
                    $"{name} currently reads grade-level text at 78% accuracy and answers comprehension questions with 55% accuracy.",
                    "95% decoding accuracy and 80% comprehension accuracy on 4 of 5 consecutive probes.",
                    "Weekly curriculum-based reading probes.",
                    "By the next annual review, reported each grading period.",
                    "WCPM"),
                new GoalPlan("Math",
                    $"{name} will solve multi-step word problems at grade level with 80% accuracy on 3 of 4 consecutive probes.",
                    $"{name} currently solves multi-step word problems with 52% accuracy and needs prompting to identify the operation.",
                    "80% accuracy across 3 consecutive probes.",
                    "Bi-weekly math probes scored against a common rubric.",
                    "By the next annual review, reported each grading period.",
                    "% accuracy"),
                new GoalPlan("Written Expression",
                    $"Given a graphic organizer and a writing prompt, {name} will produce a paragraph with a topic sentence, three supporting details and a conclusion, scoring 3 of 4 or better on the district writing rubric in 3 of 4 samples.",
                    $"{name} currently scores 2 of 4 on the district writing rubric, most often for organization.",
                    "3 of 4 or better on 3 of 4 consecutive writing samples.",
                    "Monthly scored writing samples.",
                    "By the next annual review, reported each grading period.",
                    "rubric points")
            },
            DisabilityCategory.SpeechOrLanguageImpairment => new[]
            {
                new GoalPlan("Speech Production",
                    $"{name} will produce target speech sounds correctly at the sentence level with 90% intelligibility across 3 consecutive sessions.",
                    $"{name} is currently 65% intelligible at the sentence level to an unfamiliar listener.",
                    "90% intelligibility across 3 consecutive sessions.",
                    "Therapist data collection during structured and conversational tasks.",
                    "By the next annual review, reported each grading period.",
                    "% intelligible"),
                new GoalPlan("Expressive Language",
                    $"{name} will use complete sentences with age-appropriate grammar to answer questions and describe events in 8 of 10 opportunities across two settings.",
                    $"{name} currently responds in phrases rather than complete sentences in about 4 of 10 opportunities.",
                    "8 of 10 opportunities across two settings.",
                    "Language sample and classroom observation data.",
                    "By the next annual review, reported each grading period.",
                    "% of opportunities")
            },
            DisabilityCategory.Autism => new[]
            {
                new GoalPlan("Social Communication",
                    $"{name} will initiate and sustain a reciprocal exchange with a peer for at least three turns in 4 of 5 structured opportunities.",
                    $"{name} currently sustains one to two turns with adult prompting in about 2 of 5 opportunities.",
                    "Three or more turns in 4 of 5 opportunities, with no more than one prompt.",
                    "Social skills group data and classroom observation.",
                    "By the next annual review, reported each grading period.",
                    "% of opportunities"),
                new GoalPlan("Self-Regulation",
                    $"When presented with an unexpected change to the schedule, {name} will use a taught coping strategy and return to the activity within three minutes in 80% of observed instances.",
                    $"{name} currently requires adult support to return to the activity, averaging eight minutes.",
                    "80% of observed instances across four consecutive weeks.",
                    "Behavior tracking sheet completed by classroom staff.",
                    "By the next annual review, reported each grading period.",
                    "% of intervals"),
                new GoalPlan("Academic Engagement",
                    $"{name} will begin an assigned task within one minute of the direction and work independently for ten minutes in 4 of 5 opportunities.",
                    $"{name} currently begins tasks after three or more prompts and works independently for about four minutes.",
                    "4 of 5 opportunities across three consecutive weeks.",
                    "Time-sampling data during core instruction.",
                    "By the next annual review, reported each grading period.",
                    "% of intervals")
            },
            DisabilityCategory.EmotionalDisturbance => new[]
            {
                new GoalPlan("Self-Regulation",
                    $"{name} will use a taught de-escalation strategy and remain in the instructional setting in 85% of observed opportunities.",
                    $"{name} currently leaves the instructional setting an average of three times per week.",
                    "85% of observed opportunities across four consecutive weeks.",
                    "Daily behavior point sheet reviewed weekly.",
                    "By the next annual review, reported each grading period.",
                    "% of intervals"),
                new GoalPlan("Peer Interaction",
                    $"{name} will resolve a peer disagreement using words and, when needed, an adult check-in, without physical or verbal escalation, in 4 of 5 observed situations.",
                    $"{name} currently escalates in about 3 of 5 peer disagreements.",
                    "4 of 5 observed situations across three consecutive weeks.",
                    "Incident log and counselor observation.",
                    "By the next annual review, reported each grading period.",
                    "% of opportunities")
            },
            DisabilityCategory.OtherHealthImpairment => new[]
            {
                new GoalPlan("Attention and Work Completion",
                    $"{name} will complete and turn in assigned classwork with 85% completion across a two-week period, using a checklist and one adult check-in per class.",
                    $"{name} currently turns in about 55% of assigned classwork.",
                    "85% completion across two consecutive two-week periods.",
                    "Assignment completion log reviewed with the case manager.",
                    "By the next annual review, reported each grading period.",
                    "% complete"),
                new GoalPlan("Organization",
                    $"{name} will record assignments and due dates in a planner and gather needed materials independently in 4 of 5 daily opportunities.",
                    $"{name} currently records assignments in about 2 of 5 opportunities and needs reminders for materials.",
                    "4 of 5 daily opportunities across three consecutive weeks.",
                    "Weekly planner check by the case manager.",
                    "By the next annual review, reported each grading period.",
                    "% of opportunities")
            },
            DisabilityCategory.IntellectualDisability => new[]
            {
                new GoalPlan("Functional Math",
                    $"{name} will use a calculator and a visual model to solve money problems up to $20.00, including making change, with 80% accuracy on 4 of 5 trials.",
                    $"{name} currently identifies coins and bills but solves money problems with 45% accuracy.",
                    "80% accuracy on 4 of 5 trials.",
                    "Weekly structured tasks with real or simulated money.",
                    "By the next annual review, reported each grading period.",
                    "% accuracy"),
                new GoalPlan("Functional Reading",
                    $"{name} will read and act on 20 functional words and symbols found in school and community settings with 90% accuracy across 3 consecutive sessions.",
                    $"{name} currently reads 9 of the 20 target functional words with accuracy.",
                    "90% accuracy across 3 consecutive sessions.",
                    "Weekly sight-word and environmental-print probes.",
                    "By the next annual review, reported each grading period.",
                    "% accuracy"),
                new GoalPlan("Daily Living",
                    $"{name} will complete a three-step daily living routine independently, using a picture sequence as needed, in 4 of 5 opportunities.",
                    $"{name} currently completes the routine with two to three verbal prompts.",
                    "4 of 5 opportunities across three consecutive weeks.",
                    "Task analysis data collected by classroom staff.",
                    "By the next annual review, reported each grading period.",
                    "% independent")
            },
            DisabilityCategory.DevelopmentalDelay => new[]
            {
                new GoalPlan("Early Literacy",
                    $"{name} will identify all letter names and their most common sounds with 90% accuracy across 3 consecutive sessions.",
                    $"{name} currently identifies 14 letter names and 8 letter sounds.",
                    "90% accuracy across 3 consecutive sessions.",
                    "Weekly letter-naming and letter-sound probes.",
                    "By the next annual review, reported each grading period.",
                    "% accuracy"),
                new GoalPlan("Fine Motor",
                    $"{name} will use a functional pencil grasp to write their first and last name legibly on lined paper in 4 of 5 opportunities.",
                    $"{name} currently writes their first name with inconsistent letter formation and sizing.",
                    "4 of 5 opportunities across three consecutive weeks.",
                    "Weekly handwriting samples scored for legibility.",
                    "By the next annual review, reported each grading period.",
                    "% legible"),
                new GoalPlan("Following Directions",
                    $"{name} will follow a two-step direction given once, without gestures, in 8 of 10 opportunities.",
                    $"{name} currently follows two-step directions in about 4 of 10 opportunities.",
                    "8 of 10 opportunities across two consecutive weeks.",
                    "Classroom observation tally.",
                    "By the next annual review, reported each grading period.",
                    "% of opportunities")
            },
            DisabilityCategory.HearingImpairment or DisabilityCategory.Deafness => new[]
            {
                new GoalPlan("Listening and Access",
                    $"{name} will check and report the working status of their hearing technology at the start of each school day independently in 9 of 10 opportunities.",
                    $"{name} currently reports technology problems only when asked, in about 4 of 10 opportunities.",
                    "9 of 10 opportunities across four consecutive weeks.",
                    "Daily technology check log.",
                    "By the next annual review, reported each grading period.",
                    "% of opportunities"),
                new GoalPlan("Self-Advocacy",
                    $"{name} will request a repetition, a rephrase or preferential seating when they have missed information in 4 of 5 observed opportunities.",
                    $"{name} currently requests clarification in about 1 of 5 opportunities.",
                    "4 of 5 observed opportunities across three consecutive weeks.",
                    "Teacher and itinerant staff observation data.",
                    "By the next annual review, reported each grading period.",
                    "% of opportunities")
            },
            DisabilityCategory.VisualImpairment => new[]
            {
                new GoalPlan("Access and Assistive Technology",
                    $"{name} will independently configure and use screen magnification and a screen reader to complete assigned digital work in 4 of 5 opportunities.",
                    $"{name} currently needs adult setup for digital assignments in most classes.",
                    "4 of 5 opportunities across three consecutive weeks.",
                    "Assistive-technology task checklist.",
                    "By the next annual review, reported each grading period.",
                    "% independent"),
                new GoalPlan("Self-Advocacy",
                    $"{name} will request accessible materials from each teacher at the start of a new unit in 4 of 5 opportunities.",
                    $"{name} currently relies on the case manager to request accessible materials.",
                    "4 of 5 opportunities across a grading period.",
                    "Case manager log of student-initiated requests.",
                    "By the next annual review, reported each grading period.",
                    "% of opportunities")
            },
            DisabilityCategory.OrthopedicImpairment => new[]
            {
                new GoalPlan("Mobility and Access",
                    $"{name} will navigate between classes within the passing period, using their mobility equipment and the accessible route, independently in 9 of 10 opportunities.",
                    $"{name} currently arrives late to two classes per day and needs adult assistance on the ramp route.",
                    "9 of 10 opportunities across four consecutive weeks.",
                    "Arrival log kept by classroom teachers.",
                    "By the next annual review, reported each grading period.",
                    "% independent"),
                new GoalPlan("Written Output",
                    $"{name} will use speech-to-text and a keyboard to produce written work of assigned length within the class period in 4 of 5 opportunities.",
                    $"{name} currently completes about half of assigned written work within the class period.",
                    "4 of 5 opportunities across three consecutive weeks.",
                    "Work samples with time-on-task notes.",
                    "By the next annual review, reported each grading period.",
                    "% complete")
            },
            DisabilityCategory.MultipleDisabilities => new[]
            {
                new GoalPlan("Communication",
                    $"{name} will use their communication device to make a request or a comment in 8 of 10 structured opportunities across two settings.",
                    $"{name} currently uses the device in about 3 of 10 opportunities, most often with a model.",
                    "8 of 10 opportunities across two settings for three consecutive weeks.",
                    "Communication data sheet completed by staff and the SLP.",
                    "By the next annual review, reported each grading period.",
                    "% of opportunities"),
                new GoalPlan("Daily Living",
                    $"{name} will complete the steps of a mealtime routine with no more than one physical prompt in 4 of 5 opportunities.",
                    $"{name} currently requires three or more physical prompts.",
                    "4 of 5 opportunities across three consecutive weeks.",
                    "Task analysis data collected daily.",
                    "By the next annual review, reported each grading period.",
                    "% independent"),
                new GoalPlan("Gross Motor",
                    $"{name} will transition from their wheelchair to a classroom chair with stand-by assistance only in 4 of 5 opportunities.",
                    $"{name} currently requires moderate physical assistance for transfers.",
                    "4 of 5 opportunities across four consecutive weeks.",
                    "Physical therapist data collection.",
                    "By the next annual review, reported each grading period.",
                    "% independent")
            },
            DisabilityCategory.TraumaticBrainInjury => new[]
            {
                new GoalPlan("Working Memory and Organization",
                    $"{name} will use a written checklist and a memory strategy to complete multi-step assignments with 80% accuracy in 4 of 5 opportunities.",
                    $"{name} currently loses track of steps after the second one and completes multi-step work with 50% accuracy.",
                    "80% accuracy in 4 of 5 opportunities across three consecutive weeks.",
                    "Scored multi-step tasks in core classes.",
                    "By the next annual review, reported each grading period.",
                    "% accuracy"),
                new GoalPlan("Attention and Stamina",
                    $"{name} will sustain attention to instruction for 20 minutes, using scheduled breaks, in 4 of 5 class periods.",
                    $"{name} currently sustains attention for about 8 minutes before needing redirection.",
                    "4 of 5 class periods across three consecutive weeks.",
                    "Time-sampling data during core instruction.",
                    "By the next annual review, reported each grading period.",
                    "% of intervals")
            },
            _ => new[]
            {
                new GoalPlan("Reading",
                    $"Given a grade-level passage, {name} will read aloud with 95% accuracy and retell the main idea and two details on 4 of 5 trials.",
                    $"{name} currently reads grade-level text at 80% accuracy and retells the main idea with prompting.",
                    "95% accuracy on 4 of 5 consecutive probes.",
                    "Weekly curriculum-based reading probes.",
                    "By the next annual review, reported each grading period.",
                    "WCPM"),
                new GoalPlan("Written Expression",
                    $"{name} will produce a paragraph on an assigned topic scoring 3 of 4 or better on the district writing rubric in 3 of 4 samples.",
                    $"{name} currently scores 2 of 4 on the district writing rubric.",
                    "3 of 4 or better on 3 of 4 consecutive writing samples.",
                    "Monthly scored writing samples.",
                    "By the next annual review, reported each grading period.",
                    "rubric points")
            }
        };

    // ---------------------------------------------------------------------------------------- Services

    public static IReadOnlyList<ServiceLine> ServicesFor(DisabilityCategory? disability, GradeLevel grade)
    {
        var setting = IsSecondary(grade) ? "Resource room and general education classroom" : "Resource room";
        var lines = new List<ServiceLine>
        {
            new("Specially designed instruction — literacy", "5x per week", "30 minutes", setting, "Intervention Specialist")
        };

        switch (disability)
        {
            case DisabilityCategory.SpecificLearningDisability:
                lines.Add(new("Specially designed instruction — mathematics", "3x per week", "30 minutes", setting, "Intervention Specialist"));
                break;
            case DisabilityCategory.SpeechOrLanguageImpairment:
                lines.Add(new("Speech-language therapy", "2x per week", "30 minutes", "Therapy room", "Speech-Language Pathologist"));
                break;
            case DisabilityCategory.Autism:
                lines.Add(new("Speech-language therapy — social communication", "2x per week", "30 minutes", "Therapy room and general education classroom", "Speech-Language Pathologist"));
                lines.Add(new("Social skills instruction", "2x per week", "30 minutes", "Small group", "Intervention Specialist"));
                break;
            case DisabilityCategory.EmotionalDisturbance:
                lines.Add(new("Counseling as a related service", "1x per week", "30 minutes", "Counselor's office", "School Counselor"));
                lines.Add(new("Behavior support and check-in/check-out", "Daily", "10 minutes", "General education classroom", "Intervention Specialist"));
                break;
            case DisabilityCategory.OtherHealthImpairment:
                lines.Add(new("Organizational and executive-function support", "3x per week", "20 minutes", "Resource room", "Intervention Specialist"));
                break;
            case DisabilityCategory.IntellectualDisability:
                lines.Add(new("Specially designed instruction — functional academics", "5x per week", "45 minutes", "Resource room", "Intervention Specialist"));
                lines.Add(new("Speech-language therapy", "1x per week", "30 minutes", "Therapy room", "Speech-Language Pathologist"));
                break;
            case DisabilityCategory.DevelopmentalDelay:
                lines.Add(new("Occupational therapy", "1x per week", "30 minutes", "Therapy room", "Occupational Therapist"));
                lines.Add(new("Speech-language therapy", "2x per week", "20 minutes", "Therapy room", "Speech-Language Pathologist"));
                break;
            case DisabilityCategory.HearingImpairment:
            case DisabilityCategory.Deafness:
                lines.Add(new("Itinerant hearing services and technology checks", "1x per week", "30 minutes", "General education classroom", "Teacher of the Deaf and Hard of Hearing"));
                break;
            case DisabilityCategory.VisualImpairment:
                lines.Add(new("Itinerant vision services and assistive technology", "2x per week", "30 minutes", "General education classroom", "Teacher of the Visually Impaired"));
                break;
            case DisabilityCategory.OrthopedicImpairment:
                lines.Add(new("Physical therapy", "1x per week", "30 minutes", "Therapy room and hallways", "Physical Therapist"));
                lines.Add(new("Occupational therapy", "1x per week", "30 minutes", "Therapy room", "Occupational Therapist"));
                break;
            case DisabilityCategory.MultipleDisabilities:
                lines.Add(new("Speech-language therapy — augmentative communication", "2x per week", "30 minutes", "Therapy room and classroom", "Speech-Language Pathologist"));
                lines.Add(new("Occupational therapy", "1x per week", "30 minutes", "Therapy room", "Occupational Therapist"));
                lines.Add(new("Physical therapy", "1x per week", "30 minutes", "Therapy room", "Physical Therapist"));
                break;
            case DisabilityCategory.TraumaticBrainInjury:
                lines.Add(new("Specially designed instruction — memory and organization strategies", "3x per week", "30 minutes", setting, "Intervention Specialist"));
                lines.Add(new("Occupational therapy", "1x per week", "30 minutes", "Therapy room", "Occupational Therapist"));
                break;
        }

        return lines;
    }

    // ----------------------------------------------------------------------------------- Accommodations

    public static IReadOnlyList<AccommodationLine> AccommodationsFor(DisabilityCategory? disability, GradeLevel grade)
    {
        var lines = new List<AccommodationLine>
        {
            new("Presentation", "Directions read aloud and repeated as needed"),
            new("Setting", "Preferential seating near the point of instruction"),
            new("Timing", "Extended time (1.5x) on tests, quizzes and in-class writing")
        };

        switch (disability)
        {
            case DisabilityCategory.SpecificLearningDisability:
                lines.Add(new("Response", "Word bank and access to a spell-checker for extended writing"));
                lines.Add(new("Presentation", "Text-to-speech for grade-level reading passages outside reading assessments"));
                break;
            case DisabilityCategory.SpeechOrLanguageImpairment:
                lines.Add(new("Response", "Extra time to formulate and give oral responses"));
                break;
            case DisabilityCategory.Autism:
                lines.Add(new("Setting", "Advance notice of schedule changes and a visual daily schedule"));
                lines.Add(new("Setting", "Access to a quiet break space with a pass"));
                break;
            case DisabilityCategory.EmotionalDisturbance:
                lines.Add(new("Setting", "Scheduled check-in/check-out and access to a break pass"));
                break;
            case DisabilityCategory.OtherHealthImpairment:
                lines.Add(new("Timing", "Assignments broken into chunks with separate due dates"));
                lines.Add(new("Response", "Teacher-provided copy of notes or a note-taking guide"));
                break;
            case DisabilityCategory.IntellectualDisability:
                lines.Add(new("Presentation", "Reduced number of items per page with visual supports"));
                lines.Add(new("Response", "Calculator and number line available for all math work"));
                break;
            case DisabilityCategory.DevelopmentalDelay:
                lines.Add(new("Presentation", "Picture supports paired with written directions"));
                break;
            case DisabilityCategory.HearingImpairment:
            case DisabilityCategory.Deafness:
                lines.Add(new("Presentation", "FM/DM system, captioned media and a clear line of sight to the speaker"));
                break;
            case DisabilityCategory.VisualImpairment:
                lines.Add(new("Presentation", "18-point enlarged print, high-contrast materials and screen magnification"));
                break;
            case DisabilityCategory.OrthopedicImpairment:
                lines.Add(new("Response", "Keyboard or speech-to-text in place of handwriting"));
                lines.Add(new("Setting", "Extra passing time and an accessible route between classes"));
                break;
            case DisabilityCategory.MultipleDisabilities:
                lines.Add(new("Response", "Communication device available and honored in every setting"));
                lines.Add(new("Setting", "Positioning schedule followed as written by the physical therapist"));
                break;
            case DisabilityCategory.TraumaticBrainInjury:
                lines.Add(new("Timing", "Scheduled rest breaks and a reduced-length assignment option"));
                lines.Add(new("Response", "Written checklist for multi-step tasks"));
                break;
        }

        return lines;
    }

    // -------------------------------------------------------------------------------------- Transition

    /// <summary>Ohio requires postsecondary transition planning from age 14 — grade 9 and up here.</summary>
    public static IReadOnlyList<TransitionLine> TransitionFor(GradeLevel grade, string name) =>
        !IsSecondary(grade)
            ? Array.Empty<TransitionLine>()
            : new[]
            {
                new TransitionLine(
                    "Training / education",
                    $"After high school, {name} will enroll in a two-year program at the community college with support from disability services.",
                    "Campus visit, disability services intake meeting, self-advocacy instruction each semester.",
                    "Case manager and student"),
                new TransitionLine(
                    "Employment",
                    $"{name} will hold paid part-time employment in a field of interest within six months of graduation.",
                    "Career interest inventory, resume workshop, referral to Opportunities for Ohioans with Disabilities.",
                    "School counselor and OOD transition coordinator"),
                new TransitionLine(
                    "Independent living",
                    $"{name} will manage their own schedule, transportation and budget for weekly activities.",
                    "Instruction in scheduling and budgeting, practice using public transit routes.",
                    "Case manager and family")
            };

    public static bool IsSecondary(GradeLevel grade) =>
        grade is GradeLevel.G9 or GradeLevel.G10 or GradeLevel.G11 or GradeLevel.G12;
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeBaseEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LegalReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseEntries_Category",
                table: "KnowledgeBaseEntries",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseEntries_State",
                table: "KnowledgeBaseEntries",
                column: "State");

            // Seed data: Rights (8 entries)
            migrationBuilder.Sql(@"
INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Right to Participate in IEP Meetings', N'You have the right to be part of every meeting where decisions are made about your child''s education. The school must invite you, schedule meetings at a time that works for you, and make sure you can meaningfully participate. Your voice matters — you know your child best.', N'rights', N'34 CFR 300.322', NULL, N'meetings,participation,parents', 1, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Prior Written Notice', N'Before the school can change — or refuse to change — your child''s identification, evaluation, placement, or services, they must give you written notice explaining what they want to do and why. This gives you time to understand and respond before anything happens.', N'rights', N'34 CFR 300.503', NULL, N'notice,PWN,changes', 2, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Informed Consent', N'The school must get your written permission before evaluating your child for the first time, before starting special education services, and before any reevaluation. You can say no — and you can withdraw consent at any time.', N'rights', N'34 CFR 300.300', NULL, N'consent,evaluation,permission', 3, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Access to Educational Records', N'You have the right to see and get copies of all records the school keeps about your child. The school must let you review records before any IEP meeting and must respond to your request without unnecessary delay.', N'rights', N'34 CFR 300.501', NULL, N'records,access,FERPA', 4, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Independent Educational Evaluation', N'If you disagree with the school''s evaluation of your child, you can request an Independent Educational Evaluation (IEE) at the school''s expense. The school must either pay for the IEE or file for a due process hearing to prove their evaluation was appropriate.', N'rights', N'34 CFR 300.502', NULL, N'IEE,evaluation,independent', 5, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Stay-Put Rights', N'While any dispute about your child''s placement or services is being resolved, your child stays in their current placement. This is called ""stay-put"" and it protects your child from being moved during disagreements.', N'rights', N'34 CFR 300.518', NULL, N'stay-put,placement,disputes', 6, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Dispute Resolution Options', N'When you disagree with the school, you have three main options: mediation (a neutral person helps you reach agreement), a state complaint (the state investigates), or due process (a formal hearing with a decision-maker). You can use any of these at any time.', N'rights', NULL, NULL, N'disputes,mediation,due process,complaints', 7, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Free Appropriate Public Education (FAPE)', N'Every child with a disability is entitled to a free public education that meets their unique needs. ""Appropriate"" means the program must be designed to help your child make meaningful progress — not just get by. FAPE is the foundation of all special education rights.', N'rights', N'34 CFR 300.17', NULL, N'FAPE,eligibility,rights', 8, 1, GETUTCDATE(), GETUTCDATE());
");

            // Seed data: Provisions (10 entries)
            migrationBuilder.Sql(@"
INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'IEP Content Requirements', N'The law spells out exactly what must be in your child''s IEP: present levels of performance, measurable annual goals, the services your child will receive, how progress will be measured, and more. If something is missing, you have the right to ask for it.', N'provisions', N'34 CFR 300.320', NULL, N'IEP,content,requirements', 1, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Measurable Annual Goals', N'Your child''s IEP must include goals that are specific, measurable, and designed to be achieved within one year. Good goals describe what your child will do, how well they will do it, and under what conditions — so you can track real progress.', N'provisions', N'34 CFR 300.320(a)(2)', NULL, N'goals,measurable,SMART,annual', 2, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Present Levels of Performance', N'The IEP must describe how your child is doing right now — academically and functionally. Present levels should paint a clear picture using data, observations, and input from you. This is the starting point for setting meaningful goals.', N'provisions', N'34 CFR 300.320(a)(1)', NULL, N'present levels,PLAAFP,baseline', 3, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Special Education Services', N'The IEP must list all the special education and related services your child will receive, including how often, how long each session lasts, and where they will be provided. This section is your child''s service commitment from the school.', N'provisions', N'34 CFR 300.320(a)(4)', NULL, N'services,frequency,duration,location', 4, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Least Restrictive Environment', N'Your child should be educated with non-disabled peers to the maximum extent appropriate. Removing a child from the general education classroom should only happen when the nature or severity of the disability means education there cannot be achieved even with supports.', N'provisions', N'34 CFR 300.114-120', NULL, N'LRE,inclusion,placement', 5, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Evaluations and Reevaluations', N'The school must evaluate your child using a variety of tools and strategies — no single test can be the sole basis for decisions. Reevaluations happen at least every three years, or sooner if you or the school requests one, to make sure services still fit your child''s needs.', N'provisions', N'34 CFR 300.300-311', NULL, N'evaluation,reevaluation,testing,assessment', 6, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'IEP Team Development', N'The IEP is developed by a team that includes you, at least one regular education teacher, at least one special education teacher, a school representative, and someone who can interpret evaluation results. You can also bring anyone with knowledge about your child.', N'provisions', N'34 CFR 300.324', NULL, N'IEP team,development,members', 7, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Transition Planning', N'Starting no later than age 16, the IEP must include transition goals for life after high school — whether that means college, employment, or independent living. Your child should be involved in planning their own future, and the IEP should reflect their strengths and interests.', N'provisions', N'34 CFR 300.320(b)', NULL, N'transition,postsecondary,age 16', 8, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Progress Reporting', N'The school must tell you how your child is progressing toward their IEP goals at least as often as report cards go out. Progress reports should be clear, data-driven, and tell you whether your child is on track to meet their goals by the end of the year.', N'provisions', N'34 CFR 300.320(a)(3)', NULL, N'progress,reporting,monitoring', 9, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Accommodations for Assessments', N'The IEP must describe any accommodations your child needs for state and district testing. Accommodations change how your child takes the test — like extra time or a quiet room — without changing what is being measured.', N'provisions', N'34 CFR 300.320(a)(6)', NULL, N'accommodations,testing,assessments', 10, 1, GETUTCDATE(), GETUTCDATE());
");

            // Seed data: Glossary (15 entries)
            migrationBuilder.Sql(@"
INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'FAPE — Free Appropriate Public Education', N'FAPE means your child has the right to a free public education that is individually designed to meet their needs. The school must provide special education and related services at no cost to you, in line with your child''s IEP.', N'glossary', N'34 CFR 300.17', NULL, N'FAPE,rights,free', 1, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'LRE — Least Restrictive Environment', N'LRE is the principle that children with disabilities should learn alongside their non-disabled peers as much as possible. The school should only place your child in a more restrictive setting if they cannot make progress in the general classroom even with supplemental supports.', N'glossary', N'34 CFR 300.114', NULL, N'LRE,inclusion,placement', 2, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'IDEA — Individuals with Disabilities Education Act', N'IDEA is the federal law that guarantees children with disabilities the right to a free appropriate public education. It sets the rules for evaluations, IEPs, placements, and parent rights. Most of what you see in special education comes from this law.', N'glossary', NULL, NULL, N'IDEA,federal law,special education', 3, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'IEP — Individualized Education Program', N'An IEP is a written plan that describes the special education services, goals, and supports your child will receive. It is developed by a team that includes you, and it must be reviewed at least once a year.', N'glossary', NULL, NULL, N'IEP,plan,goals,services', 4, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Related Services', N'Related services are supports your child needs to benefit from special education. Common examples include speech-language therapy, occupational therapy, physical therapy, counseling, and transportation. If your child needs it to access their education, it should be in the IEP.', N'glossary', NULL, NULL, N'related services,speech,OT,PT,counseling', 5, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Transition', N'Transition refers to planning for your child''s life after high school. Starting at age 16 (or earlier in some states), the IEP must include goals and services focused on education, employment, and independent living after graduation.', N'glossary', NULL, NULL, N'transition,postsecondary,planning', 6, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Prior Written Notice (PWN)', N'Prior Written Notice is a document the school must give you whenever they propose or refuse to change your child''s identification, evaluation, placement, or services. It must explain what the school wants to do, why, and what your options are.', N'glossary', N'34 CFR 300.503', NULL, N'PWN,notice,changes', 7, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Due Process', N'Due process is a formal legal proceeding where a neutral hearing officer resolves disputes between parents and schools about special education. It is one of three dispute resolution options available to you under IDEA.', N'glossary', NULL, NULL, N'due process,hearing,disputes', 8, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Accommodation', N'An accommodation is a change in how your child learns or is tested — like extra time, preferential seating, or having instructions read aloud. Accommodations level the playing field without changing what your child is expected to learn.', N'glossary', NULL, NULL, N'accommodation,testing,supports', 9, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Modification', N'A modification changes what your child is expected to learn or demonstrate. For example, a modified math assignment might have fewer problems or cover simpler concepts. Modifications are different from accommodations because they change the standard itself.', N'glossary', NULL, NULL, N'modification,curriculum,standards', 10, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Present Levels (PLAAFP)', N'Present Levels of Academic Achievement and Functional Performance describe how your child is doing right now. This section of the IEP uses data and observations to show your child''s strengths and areas of need, and it drives the goals and services in the rest of the IEP.', N'glossary', NULL, NULL, N'PLAAFP,present levels,baseline', 11, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Annual Goals', N'Annual goals are the specific, measurable objectives your child is expected to achieve within one year. Each goal should be tied to your child''s present levels and address an identified area of need. Progress toward these goals must be reported to you regularly.', N'glossary', NULL, NULL, N'goals,annual,measurable', 12, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Reevaluation', N'A reevaluation reviews whether your child still qualifies for special education and whether their needs have changed. It must happen at least every three years, but you or the school can request one sooner. New testing is not always required if the team agrees existing data is sufficient.', N'glossary', NULL, NULL, N'reevaluation,testing,eligibility', 13, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Eligibility', N'Eligibility is the determination of whether your child qualifies for special education under one of IDEA''s 13 disability categories. To be eligible, your child must have a qualifying disability and need specially designed instruction because of it.', N'glossary', NULL, NULL, N'eligibility,disability categories,qualification', 14, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Extended School Year (ESY)', N'ESY services are special education services provided beyond the regular school year — typically during summer. Your child may qualify for ESY if they would lose critical skills during breaks and take too long to regain them. ESY is not the same as summer school.', N'glossary', NULL, NULL, N'ESY,summer,extended year', 15, 1, GETUTCDATE(), GETUTCDATE());
");

            // Seed data: Process (5 entries)
            migrationBuilder.Sql(@"
INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'What Happens at an IEP Meeting', N'An IEP meeting is where the team reviews your child''s progress, discusses their needs, and writes or updates the IEP. You will hear about present levels, review goals, discuss services, and make decisions together. You are an equal member of this team — come prepared and don''t hesitate to ask questions.', N'process', NULL, NULL, N'meeting,IEP,team', 1, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'The Evaluation Process', N'The evaluation is how the school determines if your child has a disability and what services they need. It involves multiple assessments, observations, and input from you. The school has 60 days (in most states) from your written consent to complete the evaluation.', N'process', NULL, NULL, N'evaluation,testing,referral', 2, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Annual Review', N'The IEP team must meet at least once a year to review your child''s progress and update the IEP. This is your opportunity to discuss what is working, what is not, and what changes need to be made. You can request an IEP meeting at any time — you don''t have to wait for the annual review.', N'process', NULL, NULL, N'annual review,meeting,update', 3, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Three-Year Reevaluation', N'Every three years, the school must reevaluate your child to decide whether they still qualify for special education and whether their needs have changed. The team looks at existing data and decides if new testing is needed. You can agree to skip new testing if everyone agrees the current information is enough.', N'process', NULL, NULL, N'reevaluation,three-year,triennial', 4, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Transfer of Rights at Age 18', N'In most states, when your child turns 18, all special education rights transfer from you to them. This means your child will make their own decisions about their IEP. If your child needs support making these decisions, explore options like power of attorney or supported decision-making before they turn 18.', N'process', NULL, NULL, N'transfer of rights,age 18,adult', 5, 1, GETUTCDATE(), GETUTCDATE());
");

            // Seed data: Tips (5 entries)
            migrationBuilder.Sql(@"
INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'How to Prepare for Your First IEP Meeting', N'Before your first IEP meeting, gather any reports or evaluations you have, write down your concerns and questions, and think about what you want for your child this year. Bring a notebook to take notes, and remember — you can bring a friend, advocate, or anyone who knows your child to support you.', N'tips', NULL, NULL, N'first meeting,preparation,tips', 1, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Questions to Ask About Goals', N'When reviewing IEP goals, ask: How will progress be measured? How often will I get updates? What happens if my child isn''t making progress? Is this goal based on my child''s current levels? These questions help make sure goals are meaningful and trackable.', N'tips', NULL, NULL, N'goals,questions,progress', 2, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'What to Do If You Disagree', N'If you disagree with something in the IEP, say so clearly and ask that your concerns be documented. You don''t have to sign the IEP on the spot — you can take it home to review. If you can''t reach agreement, you have the right to mediation, a state complaint, or due process.', N'tips', NULL, NULL, N'disagreement,advocacy,disputes', 3, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Keeping Records and Documentation', N'Keep a folder or binder with copies of all IEPs, evaluations, progress reports, and any communication with the school. Follow up important conversations with an email summarizing what was discussed. Good records are your best tool if you ever need to resolve a disagreement.', N'tips', NULL, NULL, N'records,documentation,organization', 4, 1, GETUTCDATE(), GETUTCDATE());

INSERT INTO KnowledgeBaseEntries (Title, Content, Category, LegalReference, State, Tags, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
VALUES (N'Building a Positive Relationship with the School', N'A good working relationship with your child''s school team goes a long way. Start by assuming good intentions, communicate openly, and express appreciation when things go well. Being collaborative does not mean giving up your rights — it means working together toward the best outcome for your child.', N'tips', NULL, NULL, N'relationships,collaboration,communication', 5, 1, GETUTCDATE(), GETUTCDATE());
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeBaseEntries");
        }
    }
}

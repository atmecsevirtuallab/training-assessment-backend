using Microsoft.EntityFrameworkCore;

namespace TrainingAndAssessmentDataAccessLayer
{
    public class TrainingAssessmentDbContext : DbContext
    {
        public TrainingAssessmentDbContext(DbContextOptions<TrainingAssessmentDbContext> options)
            : base(options)
        {
        }

        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<Student> Students => Set<Student>();
        public DbSet<Trainer> Trainers => Set<Trainer>();
        public DbSet<TrainingProgram> TrainingPrograms => Set<TrainingProgram>();
        public DbSet<TrainingProgramTrainer> TrainingProgramTrainers => Set<TrainingProgramTrainer>();
        public DbSet<Batch> Batches => Set<Batch>();
        public DbSet<BatchStudent> BatchStudents => Set<BatchStudent>();
        public DbSet<Announcement> Announcements => Set<Announcement>();
        public DbSet<FeedbackForm> FeedbackForms => Set<FeedbackForm>();
        public DbSet<FeedbackQuestion> FeedbackQuestions => Set<FeedbackQuestion>();
        public DbSet<FeedbackResponse> FeedbackResponses => Set<FeedbackResponse>();
        public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
        public DbSet<Assessment> Assessments => Set<Assessment>();
        public DbSet<Attendance> Attendance => Set<Attendance>();
        public DbSet<AssessmentSubmission> AssessmentSubmissions => Set<AssessmentSubmission>();
        public DbSet<LoginHistory> LoginHistory => Set<LoginHistory>();
        public DbSet<Report> Reports => Set<Report>();
        public DbSet<VwLoginHistoryWithTotals> VwLoginHistoryWithTotals => Set<VwLoginHistoryWithTotals>();
        public DbSet<VwDashboardSummary> VwDashboardSummary => Set<VwDashboardSummary>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TrainingProgramTrainer>()
                .HasKey(tpt => new { tpt.TrainingId, tpt.TrainerId });

            modelBuilder.Entity<BatchStudent>()
                .HasKey(bs => new { bs.BatchId, bs.StudentId });

            modelBuilder.Entity<VwLoginHistoryWithTotals>()
                .HasNoKey()
                .ToView("vw_LoginHistoryWithTotals");

            modelBuilder.Entity<VwDashboardSummary>()
                .HasNoKey()
                .ToView("vw_DashboardSummary");
        }
    }
}

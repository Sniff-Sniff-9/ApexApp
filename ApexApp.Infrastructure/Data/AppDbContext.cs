using System;
using System.Collections.Generic;
using ApexApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Balance> Balances { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<EventParticipant> EventParticipants { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<Quest> Quests { get; set; }

    public virtual DbSet<QuestParticipation> QuestParticipations { get; set; }

    public virtual DbSet<QuestProof> QuestProofs { get; set; }

    public virtual DbSet<QuestStep> QuestSteps { get; set; }

    public virtual DbSet<QuestStepCompletion> QuestStepCompletions { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SupportMessage> SupportMessages { get; set; }

    public virtual DbSet<SupportTicket> SupportTickets { get; set; }

    public virtual DbSet<Survey> Surveys { get; set; }

    public virtual DbSet<SurveyAnswer> SurveyAnswers { get; set; }

    public virtual DbSet<SurveyQuestion> SurveyQuestions { get; set; }

    public virtual DbSet<SurveyResponse> SurveyResponses { get; set; }

    public virtual DbSet<Thank> Thanks { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<Achievement> Achievements { get; set; }

    public virtual DbSet<UserAchievement> UserAchievements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("auth", "aal_level", new[] { "aal1", "aal2", "aal3" })
            .HasPostgresEnum("auth", "code_challenge_method", new[] { "s256", "plain" })
            .HasPostgresEnum("auth", "factor_status", new[] { "unverified", "verified" })
            .HasPostgresEnum("auth", "factor_type", new[] { "totp", "webauthn", "phone", "recovery_code" })
            .HasPostgresEnum("auth", "oauth_authorization_status", new[] { "pending", "approved", "denied", "expired" })
            .HasPostgresEnum("auth", "oauth_client_type", new[] { "public", "confidential" })
            .HasPostgresEnum("auth", "oauth_registration_type", new[] { "dynamic", "manual" })
            .HasPostgresEnum("auth", "oauth_response_type", new[] { "code" })
            .HasPostgresEnum("auth", "one_time_token_type", new[] { "confirmation_token", "reauthentication_token", "recovery_token", "email_change_token_new", "email_change_token_current", "phone_change_token" })
            .HasPostgresEnum<BalanceType>(name: "balance_type")
            .HasPostgresEnum<OrderStatus>(name: "order_status")
            .HasPostgresEnum("participation_status", new[] { "joined", "in_progress", "completed", "failed", "cancelled" })
            .HasPostgresEnum<QuestStepCompletionType>(name: "quest_step_completion_type")
            .HasPostgresEnum("question_type", new[] { "single", "multiple", "text", "rating", "scale" })
            .HasPostgresEnum("realtime", "action", new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE", "ERROR" })
            .HasPostgresEnum("realtime", "equality_op", new[] { "eq", "neq", "lt", "lte", "gt", "gte", "in", "like", "ilike", "is", "match", "imatch", "isdistinct" })
            .HasPostgresEnum("storage", "buckettype", new[] { "STANDARD", "ANALYTICS", "VECTOR" })
            .HasPostgresEnum<TicketStatus>(name: "ticket_status")
            .HasPostgresEnum<TransactionType>(name: "transaction_type")
            .HasPostgresExtension("extensions", "pg_stat_statements")
            .HasPostgresExtension("extensions", "pgcrypto")
            .HasPostgresExtension("extensions", "uuid-ossp")
            .HasPostgresExtension("vault", "supabase_vault");

        modelBuilder.Entity<Balance>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.Type })
            .HasName("balances_pkey");

            entity.ToTable("balances");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.Type)
            .HasColumnName("type")
            .HasColumnType("balance_type")
            .IsRequired();
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.Version).HasColumnName("version");

            entity.HasOne(d => d.User)
            .WithMany(p => p.Balances)
            .HasForeignKey(d => d.UserId)
            .HasConstraintName("balances_user_id_fkey");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cart_items_pkey");

            entity.ToTable("cart_items");

            entity.HasIndex(e => new { e.UserId, e.ProductId }, "cart_items_user_id_product_id_key").IsUnique();

            entity.HasIndex(e => e.UserId, "idx_cart_user");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.AddedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("added_at");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity)
                .HasDefaultValue(1)
                .HasColumnName("quantity");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Product).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("cart_items_product_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("cart_items_user_id_fkey");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("categories_pkey");

            entity.ToTable("categories");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("events_pkey");

            entity.ToTable("events");

            entity.HasIndex(e => e.StartAt, "idx_events_active_start").HasFilter("(is_active = true)");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EndAt).HasColumnName("end_at");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.Location).HasColumnName("location");
            entity.Property(e => e.MaxParticipants).HasColumnName("max_participants");
            entity.Property(e => e.RewardAmount).HasColumnName("reward_amount");
            entity.Property(e => e.StartAt).HasColumnName("start_at");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Events)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("events_created_by_fkey");
        });

        modelBuilder.Entity<EventParticipant>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("event_participants_pkey");

            entity.ToTable("event_participants");

            entity.HasIndex(e => new { e.EventId, e.UserId }, "event_participants_user_unique").IsUnique();

            entity.HasIndex(e => new { e.UserId, e.Status }, "idx_event_participants_user");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.AttendedAt).HasColumnName("attended_at");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.RegisteredAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("registered_at");
            entity.Property(e => e.RewardClaimed).HasColumnName("reward_claimed");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'registered'::text")
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Event).WithMany(p => p.EventParticipants)
                .HasForeignKey(d => d.EventId)
                .HasConstraintName("event_participants_event_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.EventParticipants)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("event_participants_user_id_fkey");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("orders_pkey");

            entity.ToTable("orders");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "idx_orders_user_created").IsDescending(false, true);

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasColumnType("order_status")
                .IsRequired()
                .HasDefaultValue(OrderStatus.Pending);
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ProcessedBy).HasColumnName("processed_by");
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.ProcessedByNavigation).WithMany(p => p.OrderProcessedByNavigations)
                .HasForeignKey(d => d.ProcessedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("orders_processed_by_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.OrderUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("orders_user_id_fkey");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("order_items_pkey");

            entity.ToTable("order_items");

            entity.HasIndex(e => e.OrderId, "idx_order_items_order");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.DigitalCode)
                .HasMaxLength(255)
                .HasColumnName("digital_code");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.PriceAtPurchase).HasColumnName("price_at_purchase");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("order_items_order_id_fkey");

            entity.HasOne(d => d.Product).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("order_items_product_id_fkey");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("products_pkey");

            entity.ToTable("products");

            entity.HasIndex(e => e.IsActive, "idx_products_active");

            entity.HasIndex(e => e.CategoryId, "idx_products_category");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ImageUrl).HasColumnName("image_url");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsDigital).HasColumnName("is_digital");
            entity.Property(e => e.Metadata)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("metadata");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Price).HasColumnName("price");
            entity.Property(e => e.Stock).HasColumnName("stock");
            entity.Property(e => e.Tags).HasColumnName("tags");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("products_category_id_fkey");
        });

        modelBuilder.Entity<Quest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("quests_pkey");

            entity.ToTable("quests");

            entity.HasIndex(e => new { e.StartAt, e.EndAt }, "idx_quests_active_dates").HasFilter("(is_active = true)");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EndAt).HasColumnName("end_at");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.RewardAmount).HasColumnName("reward_amount");
            entity.Property(e => e.StartAt).HasColumnName("start_at");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Quests)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("quests_created_by_fkey");
        });

        modelBuilder.Entity<QuestParticipation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("quest_participations_pkey");

            entity.ToTable("quest_participations");

            entity.HasIndex(e => new { e.UserId, e.Status }, "idx_quest_participations_user_status");

            entity.HasIndex(e => new { e.Id, e.QuestId }, "quest_participations_id_quest_unique").IsUnique();

            entity.HasIndex(e => new { e.QuestId, e.UserId }, "quest_participations_user_unique").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.QuestId).HasColumnName("quest_id");
            entity.Property(e => e.RewardClaimed).HasColumnName("reward_claimed");
            entity.Property(e => e.StartedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("started_at");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'in_progress'::text")
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Quest).WithMany(p => p.QuestParticipations)
                .HasForeignKey(d => d.QuestId)
                .HasConstraintName("quest_participations_quest_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.QuestParticipations)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("quest_participations_user_id_fkey");
        });

        modelBuilder.Entity<QuestProof>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("quest_proofs_pkey");

            entity.ToTable("quest_proofs");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "idx_quest_proofs_status");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CompletionId).HasColumnName("completion_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'pending'::text")
                .HasColumnName("status");
            entity.Property(e => e.StoragePath).HasColumnName("storage_path");

            entity.HasOne(d => d.Completion).WithMany(p => p.QuestProofs)
                .HasForeignKey(d => d.CompletionId)
                .HasConstraintName("quest_proofs_completion_id_fkey");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.QuestProofs)
                .HasForeignKey(d => d.ReviewedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("quest_proofs_reviewed_by_fkey");
        });

        modelBuilder.Entity<QuestStep>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("quest_steps_pkey");

            entity.ToTable("quest_steps");

            entity.HasIndex(e => new { e.QuestId, e.SortOrder }, "idx_quest_steps_quest_order");

            entity.HasIndex(e => new { e.Id, e.QuestId }, "quest_steps_id_quest_unique").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CompletionType)
                .HasColumnName("completion_type")
                .HasColumnType("quest_step_completion_type")
                .IsRequired()
                .HasDefaultValue(QuestStepCompletionType.Manual);
            entity.Property(e => e.CompletionData)
                .HasColumnType("jsonb")
                .HasColumnName("completion_data");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsRequired)
                .HasDefaultValue(true)
                .HasColumnName("is_required");
            entity.Property(e => e.QuestId).HasColumnName("quest_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");

            entity.HasOne(d => d.Quest).WithMany(p => p.QuestSteps)
                .HasForeignKey(d => d.QuestId)
                .HasConstraintName("quest_steps_quest_id_fkey");
        });

        modelBuilder.Entity<QuestStepCompletion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("quest_step_completions_pkey");

            entity.ToTable("quest_step_completions");

            entity.HasIndex(e => e.ParticipationId, "idx_quest_completions_participation");

            entity.HasIndex(e => new { e.ParticipationId, e.StepId }, "quest_step_completion_unique").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CompletedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("completed_at");
            entity.Property(e => e.ParticipationId).HasColumnName("participation_id");
            entity.Property(e => e.Proof)
                .HasColumnType("jsonb")
                .HasColumnName("proof");
            entity.Property(e => e.QuestId).HasColumnName("quest_id");
            entity.Property(e => e.StepId).HasColumnName("step_id");

            entity.HasOne(d => d.QuestParticipation).WithMany(p => p.QuestStepCompletions)
                .HasPrincipalKey(p => new { p.Id, p.QuestId })
                .HasForeignKey(d => new { d.ParticipationId, d.QuestId })
                .HasConstraintName("quest_completion_participation_fk");

            entity.HasOne(d => d.QuestStep).WithMany(p => p.QuestStepCompletions)
                .HasPrincipalKey(p => new { p.Id, p.QuestId })
                .HasForeignKey(d => new { d.StepId, d.QuestId })
                .HasConstraintName("quest_completion_step_fk");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");

            entity.ToTable("roles");

            entity.HasIndex(e => e.Code, "roles_code_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<SupportMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("support_messages_pkey");

            entity.ToTable("support_messages");

            entity.HasIndex(e => new { e.TicketId, e.CreatedAt }, "idx_support_messages_ticket");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Ticket).WithMany(p => p.SupportMessages)
                .HasForeignKey(d => d.TicketId)
                .HasConstraintName("support_messages_ticket_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.SupportMessages)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("support_messages_user_id_fkey");
        });

        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("support_tickets_pkey");

            entity.ToTable("support_tickets");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "idx_support_tickets_user").IsDescending(false, true);

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasColumnType("ticket_status")
                .IsRequired()
                .HasDefaultValue(TicketStatus.Open);
            entity.Property(e => e.AssignedTo).HasColumnName("assigned_to");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Subject)
                .HasMaxLength(255)
                .HasColumnName("subject");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.AssignedToNavigation).WithMany(p => p.SupportTicketAssignedToNavigations)
                .HasForeignKey(d => d.AssignedTo)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("support_tickets_assigned_to_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.SupportTicketUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("support_tickets_user_id_fkey");
        });

        modelBuilder.Entity<Survey>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("surveys_pkey");

            entity.ToTable("surveys");

            entity.HasIndex(e => new { e.StartAt, e.EndAt }, "idx_surveys_active_dates").HasFilter("(is_active = true)");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.AllowMultipleResponses).HasColumnName("allow_multiple_responses");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EndAt).HasColumnName("end_at");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.StartAt).HasColumnName("start_at");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Surveys)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("surveys_created_by_fkey");
        });

        modelBuilder.Entity<SurveyAnswer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("survey_answers_pkey");

            entity.ToTable("survey_answers");

            entity.HasIndex(e => e.ResponseId, "idx_survey_answers_response");

            entity.HasIndex(e => new { e.ResponseId, e.QuestionId }, "survey_answer_question_unique").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Answer)
                .HasColumnType("jsonb")
                .HasColumnName("answer");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.QuestionId).HasColumnName("question_id");
            entity.Property(e => e.ResponseId).HasColumnName("response_id");
            entity.Property(e => e.SurveyId).HasColumnName("survey_id");

            entity.HasOne(d => d.SurveyQuestion).WithMany(p => p.SurveyAnswers)
                .HasPrincipalKey(p => new { p.Id, p.SurveyId })
                .HasForeignKey(d => new { d.QuestionId, d.SurveyId })
                .HasConstraintName("survey_answer_question_fk");

            entity.HasOne(d => d.SurveyResponse).WithMany(p => p.SurveyAnswers)
                .HasPrincipalKey(p => new { p.Id, p.SurveyId })
                .HasForeignKey(d => new { d.ResponseId, d.SurveyId })
                .HasConstraintName("survey_answer_response_fk");
        });

        modelBuilder.Entity<SurveyQuestion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("survey_questions_pkey");

            entity.ToTable("survey_questions");

            entity.HasIndex(e => new { e.SurveyId, e.SortOrder }, "idx_survey_questions_survey_order");

            entity.HasIndex(e => new { e.Id, e.SurveyId }, "survey_questions_id_survey_unique").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IsRequired)
                .HasDefaultValue(true)
                .HasColumnName("is_required");
            entity.Property(e => e.Options)
                .HasColumnType("jsonb")
                .HasColumnName("options");
            entity.Property(e => e.QuestionText).HasColumnName("question_text");
            entity.Property(e => e.QuestionType).HasColumnName("question_type");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.SurveyId).HasColumnName("survey_id");

            entity.HasOne(d => d.Survey).WithMany(p => p.SurveyQuestions)
                .HasForeignKey(d => d.SurveyId)
                .HasConstraintName("survey_questions_survey_id_fkey");
        });

        modelBuilder.Entity<SurveyResponse>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("survey_responses_pkey");

            entity.ToTable("survey_responses");

            entity.HasIndex(e => new { e.UserId, e.SurveyId }, "idx_survey_responses_user");

            entity.HasIndex(e => new { e.Id, e.SurveyId }, "survey_responses_id_survey_unique").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.SubmittedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("submitted_at");
            entity.Property(e => e.SurveyId).HasColumnName("survey_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Survey).WithMany(p => p.SurveyResponses)
                .HasForeignKey(d => d.SurveyId)
                .HasConstraintName("survey_responses_survey_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.SurveyResponses)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("survey_responses_user_id_fkey");
        });

        modelBuilder.Entity<Thank>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("thanks_pkey");

            entity.ToTable("thanks");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.FromUserId).HasColumnName("from_user_id");
            entity.Property(e => e.IsPublic)
                .HasDefaultValue(true)
                .HasColumnName("is_public");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.ToUserId).HasColumnName("to_user_id");

            entity.HasOne(d => d.FromUser).WithMany(p => p.ThankFromUsers)
                .HasForeignKey(d => d.FromUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("thanks_from_user_id_fkey");

            entity.HasOne(d => d.ToUser).WithMany(p => p.ThankToUsers)
                .HasForeignKey(d => d.ToUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("thanks_to_user_id_fkey");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("transactions_pkey");

            entity.ToTable("transactions");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "idx_transactions_user_created").IsDescending(false, true);

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.BalanceAfter).HasColumnName("balance_after");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Type)
                .HasColumnName("type")
                .HasColumnType("transaction_type")
                .IsRequired();
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.RelatedEntityId).HasColumnName("related_entity_id");
            entity.Property(e => e.RelatedEntityType)
                .HasMaxLength(50)
                .HasColumnName("related_entity_type");
            entity.Property(e => e.RelatedUserId).HasColumnName("related_user_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.TransactionCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("transactions_created_by_fkey");

            entity.HasOne(d => d.RelatedUser).WithMany(p => p.TransactionRelatedUsers)
                .HasForeignKey(d => d.RelatedUserId)
                .HasConstraintName("transactions_related_user_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.TransactionUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("transactions_user_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Department).HasColumnName("department");
            entity.Property(e => e.FirstName)
                .HasMaxLength(100)
                .HasColumnName("first_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.LastName)
                .HasMaxLength(100)
                .HasColumnName("last_name");
            entity.Property(e => e.MiddleName)
                .HasMaxLength(100)
                .HasColumnName("middle_name");
            entity.Property(e => e.Position).HasColumnName("position");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId }).HasName("user_roles_pkey");

            entity.ToTable("user_roles");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("assigned_at");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("user_roles_role_id_fkey");
        });

        modelBuilder.Entity<Achievement>(entity =>
        {
            entity.ToTable("achievements", "public");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .UseIdentityByDefaultColumn();

            entity.Property(e => e.Code)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(e => e.Code)
                .IsUnique();

            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Description);

            entity.Property(e => e.IconUrl)
                .HasColumnName("icon_url")
                .HasMaxLength(500);

            entity.Property(e => e.ConditionType)
                .HasColumnName("condition_type")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.ConditionValue)
                .HasColumnName("condition_value")
                .IsRequired();

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<UserAchievement>(entity =>
        {
            entity.ToTable("user_achievements", "public");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .UseIdentityByDefaultColumn();

            entity.Property(e => e.UserId)
                .HasColumnName("user_id")
                .IsRequired();

            entity.Property(e => e.AchievementId)
                .HasColumnName("achievement_id")
                .IsRequired();

            entity.Property(e => e.EarnedAt)
                .HasColumnName("earned_at")
                .HasDefaultValueSql("now()");

            entity.HasIndex(e => new
            {
                e.UserId,
                e.AchievementId
            }).IsUnique();

            entity.HasOne(e => e.Achievement)
                .WithMany(e => e.UserAchievements)
                .HasForeignKey(e => e.AchievementId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

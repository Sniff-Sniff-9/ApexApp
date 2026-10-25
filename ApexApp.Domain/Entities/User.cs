using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class User
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string? MiddleName { get; set; }

    public string? AvatarUrl { get; set; }

    public string Department { get; set; } = null!;

    public string Position { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public bool MustChangePassword { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<Balance> Balances { get; set; } = new List<Balance>();

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual ICollection<EventParticipant> EventParticipants { get; set; } = new List<EventParticipant>();

    public virtual ICollection<Event> Events { get; set; } = new List<Event>();

    public virtual ICollection<Order> OrderProcessedByNavigations { get; set; } = new List<Order>();

    public virtual ICollection<Order> OrderUsers { get; set; } = new List<Order>();

    public virtual ICollection<QuestParticipation> QuestParticipations { get; set; } = new List<QuestParticipation>();

    public virtual ICollection<QuestProof> QuestProofs { get; set; } = new List<QuestProof>();

    public virtual ICollection<Quest> Quests { get; set; } = new List<Quest>();

    public virtual ICollection<SupportMessage> SupportMessages { get; set; } = new List<SupportMessage>();

    public virtual ICollection<SupportTicket> SupportTicketAssignedToNavigations { get; set; } = new List<SupportTicket>();

    public virtual ICollection<SupportTicket> SupportTicketUsers { get; set; } = new List<SupportTicket>();

    public virtual ICollection<SurveyResponse> SurveyResponses { get; set; } = new List<SurveyResponse>();

    public virtual ICollection<Survey> Surveys { get; set; } = new List<Survey>();

    public virtual ICollection<Thank> ThankFromUsers { get; set; } = new List<Thank>();

    public virtual ICollection<Thank> ThankToUsers { get; set; } = new List<Thank>();

    public virtual ICollection<Transaction> TransactionCreatedByNavigations { get; set; } = new List<Transaction>();

    public virtual ICollection<Transaction> TransactionRelatedUsers { get; set; } = new List<Transaction>();

    public virtual ICollection<Transaction> TransactionUsers { get; set; } = new List<Transaction>();
}

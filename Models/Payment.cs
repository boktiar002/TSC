using System.ComponentModel.DataAnnotations;

namespace TSC.Models;

public class Payment
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select a student.")]
    [Display(Name = "Student")]
    public int StudentId { get; set; }
    public Student? Student { get; set; }

    // The month the fee is *for*, always stored on day 1. PaymentDate is when it was handed
    // over: a September fee paid in October is normal and the ledger has to tell them apart.
    [DataType(DataType.Date), Display(Name = "Fee for month")]
    public DateOnly ForMonth { get; set; }

    [Range(0.01, 1000000, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [DataType(DataType.Date), Display(Name = "Paid on")]
    public DateOnly PaymentDate { get; set; }

    [StringLength(30), Display(Name = "Method")]
    public string? PaymentMethod { get; set; }

    [StringLength(250)]
    public string? Note { get; set; }

    // A mis-keyed receipt is voided, never deleted. The guardian may be holding the paper
    // copy, and a fee ledger that can silently lose rows cannot be reconciled against the
    // cash box. Voided rows are kept out of every total by a query filter and shown struck
    // through on the ledger.
    public bool IsVoided { get; set; }

    [Display(Name = "Voided on")]
    public DateTime? VoidedAt { get; set; } // timestamptz: must be UTC

    // The admin who voided it, so a disputed receipt has someone to ask.
    public string? VoidedByUserId { get; set; }
}

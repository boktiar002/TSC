using System.ComponentModel.DataAnnotations;

namespace TSC.Models;

public class Notice
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000), DataType(DataType.MultilineText)]
    public string Content { get; set; } = string.Empty;

    [Display(Name = "Published")]
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow; // timestamptz: must be UTC

    [Display(Name = "Visible to students and teachers")]
    public bool IsActive { get; set; } = true;
}

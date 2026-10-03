using System.Security.Claims;

namespace TSC.Models;

public record NavItem(
    string Label,
    string Icon,
    string Area,
    string Controller,
    string Action,
    // Shown in the phone's bottom bar. Only a handful fit, so this is the shortlist of the
    // things someone actually taps while standing up.
    bool OnBottomBar = false,
    string? ShortLabel = null,
    // Heading this item sits under. Only rendered when the person holds more than one role and
    // the menu is therefore a merge of two lists.
    string Section = "")
{
    public string Short => ShortLabel ?? Label;
}

// One menu definition for three renderings: the desktop sidebar, the phone drawer and the
// phone's bottom bar. They drifted apart when each was written out by hand in the layout.
//
// Roles are additive, not exclusive. The person running the centre is also one of its teachers:
// they need the office menu AND their own class pages, so the lists are concatenated rather than
// the first matching one being returned.
public static class NavMenu
{
    public static IReadOnlyList<NavItem> For(ClaimsPrincipal user)
    {
        var items = new List<NavItem>();

        if (user.IsInRole("Admin"))
            items.AddRange(Admin);

        if (user.IsInRole("Teacher"))
            items.AddRange(Teacher);

        if (user.IsInRole("Student"))
            items.AddRange(Student);

        return items;
    }

    // True when the menu is a merge, which is what tells the renderer to show section headings.
    public static bool IsMerged(IReadOnlyList<NavItem> items) =>
        items.Select(i => i.Section).Distinct().Count() > 1;

    private static readonly NavItem[] Admin =
    {
        new("Dashboard", "bi-grid-1x2", "Admin", "Dashboard", "Index", OnBottomBar: true, ShortLabel: "Home", Section: "Office"),
        new("Students", "bi-person-vcard", "Admin", "Students", "Index", OnBottomBar: true, Section: "Office"),
        new("Attendance", "bi-calendar2-check", "Admin", "Attendance", "Index", OnBottomBar: true, Section: "Office"),
        new("Fees", "bi-cash-coin", "Admin", "Payments", "Index", OnBottomBar: true, Section: "Office"),
        new("Exams & results", "bi-clipboard-check", "Admin", "Exams", "Index", ShortLabel: "Exams", Section: "Office"),
        new("Class progress", "bi-trophy", "Admin", "Exams", "Progress", Section: "Office"),
        new("Classes", "bi-collection", "Admin", "Classes", "Index", Section: "Office"),
        new("Subjects", "bi-book", "Admin", "Subjects", "Index", Section: "Office"),
        new("Teachers", "bi-person-workspace", "Admin", "Teachers", "Index", Section: "Office"),
        new("Portal logins", "bi-key", "Admin", "Logins", "Index", Section: "Office"),
        new("Notices", "bi-megaphone", "Admin", "Notices", "Index", Section: "Office"),
    };

    // Labelled "my" throughout: standalone it reads naturally, and merged into the office menu
    // it is the only thing distinguishing "Attendance" (any class) from "My attendance" (mine).
    private static readonly NavItem[] Teacher =
    {
        new("My dashboard", "bi-easel", "Teacher", "Home", "Index", OnBottomBar: true, ShortLabel: "Home", Section: "My teaching"),
        new("My attendance", "bi-calendar2-check", "Teacher", "Home", "Attendance", OnBottomBar: true, ShortLabel: "Roll call", Section: "My teaching"),
        new("My marks", "bi-clipboard-check", "Teacher", "Home", "Exams", OnBottomBar: true, ShortLabel: "Marks", Section: "My teaching"),
        new("My results", "bi-trophy", "Teacher", "Home", "Progress", OnBottomBar: true, ShortLabel: "Results", Section: "My teaching"),
        new("Notices", "bi-megaphone", "Teacher", "Home", "Notices", Section: "My teaching"),
    };

    private static readonly NavItem[] Student =
    {
        new("My dashboard", "bi-grid-1x2", "Student", "Home", "Index", OnBottomBar: true, ShortLabel: "Home", Section: "My portal"),
        new("Results", "bi-graph-up-arrow", "Student", "Home", "Results", OnBottomBar: true, Section: "My portal"),
        new("Attendance", "bi-calendar2-check", "Student", "Home", "Attendance", OnBottomBar: true, Section: "My portal"),
        new("Fees", "bi-cash-coin", "Student", "Home", "Fees", OnBottomBar: true, Section: "My portal"),
        new("Notices", "bi-megaphone", "Student", "Home", "Notices", Section: "My portal"),
    };
}

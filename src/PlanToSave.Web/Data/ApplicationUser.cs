using Microsoft.AspNetCore.Identity;

namespace PlanToSave.Web.Data;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}


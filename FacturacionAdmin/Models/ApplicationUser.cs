using System;
using Microsoft.AspNetCore.Identity;

namespace FacturacionAdmin.Models
{
    public class ApplicationUser : IdentityUser
    {
        public int NumeroSocio { get; set; }
    }
}


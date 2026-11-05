using DM.Core.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Identity.Models
{
    public sealed class AccountRecord
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public AccessRole Role { get; set; }
        public bool Enabled { get; set; } = true;
        public bool MustChangePassword { get; set; }
        public int FailedCount { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? LastLoginUtc { get; set; }
    }
}

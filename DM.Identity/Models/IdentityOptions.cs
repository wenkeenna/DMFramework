using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Identity.Models
{
    /// <summary>machine.json 的 "Identity" 段，全部可省略。</summary>
    public sealed class IdentityOptions
    {
        public int MaxFailedAttempts { get; set; } = 5;
        public int LockMinutes { get; set; } = 5;
        public int MinPasswordLength { get; set; } = 6;
        public string BootstrapUsername { get; set; } = "admin";
        public string BootstrapPassword { get; set; } = "admin";      // 仅用于首次创建，登录后强制修改
    }
}

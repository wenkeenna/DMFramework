using DM.Core.Common;
using DM.Core.Identity;
using DM.Engine.Hosting;
using DM.Identity.Services;
using DM.Identity.ViewModels;
using DM.Identity.Views;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Identity.Module
{
    public sealed class IdentityModule : ModuleBase
    {
        protected override void Register(IContainerRegistry r)
        {
            r.RegisterSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
            r.RegisterSingleton<IAccountStore>(c => new SqliteAccountStore(c.Resolve<AppPaths>().IdentityDb));

            // 同一个实例同时作为登录服务和账号服务
            r.RegisterSingleton<IdentityService>();
            r.RegisterSingleton<IIdentityService>(c => c.Resolve<IdentityService>());
            r.RegisterSingleton<IAccountService>(c => c.Resolve<IdentityService>());
            r.RegisterSingleton<ICurrentUser, CurrentUser>();

            r.RegisterDialog<SignInDialog, SignInDialogViewModel>("SignInDialog");
            r.RegisterDialog<ChangePasswordDialog, ChangePasswordDialogViewModel>("ChangePasswordDialog");
            r.RegisterDialog<AccountManagementDialog, AccountManagementDialogViewModel>("AccountManagementDialog");
        }

        protected override void Attach(ModuleScope s) { s.Resolve<IdentityService>().Initialize(); }
    }
}

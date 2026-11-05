using DM.Identity.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Identity.Services
{
    public interface IAccountStore
    {
        AccountRecord Find(string username);
        IReadOnlyList<AccountRecord> List();
        int Count();
        int CountEnabledAdmins();
        void Insert(AccountRecord a);
        void Update(AccountRecord a);
        bool Delete(string username);
    }
}

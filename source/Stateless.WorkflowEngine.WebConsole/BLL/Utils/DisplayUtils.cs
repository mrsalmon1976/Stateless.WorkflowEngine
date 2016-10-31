using Nancy.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Utils
{
    public class DisplayUtils
    {
        public static bool HasAnyClaim(IUserIdentity user, params string[] claims)
        {
            if (user.Claims == null)
            {
                return false;
            }
            return user.Claims.Intersect(claims).Any();
        }
        public static bool HasClaim(IUserIdentity user, string claim)
        {
            if (user.Claims == null)
            {
                return false;
            }
            return user.Claims.Contains(claim);
        }
    }
}

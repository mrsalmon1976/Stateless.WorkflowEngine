﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Security
{
    public static class Claims
    {
        static Claims()
        {
            AllClaims = new List<string>(new string[] 
            { 
                ConnectionAdd, 
                ConnectionDelete,
                RemoveWorkflow,
                SuspendWorkflow,
                UnsuspendWorkflow,
                UserAdd,
                UserDelete,
                UserList
            }).AsReadOnly();
        }

        public const string ConnectionAdd = "ConnectionAdd";

        public const string ConnectionDelete = "ConnectionDelete";

        public const string RemoveWorkflow = "RemoveWorkflow";

        public const string UserAdd = "UserAdd";

        public const string UserDelete = "UserDelete";

        public const string UserList = "UserList";

        public const string SuspendWorkflow = "SuspendWorkflow";

        public const string UnsuspendWorkflow = "UnsuspendWorkflow";

        public static IReadOnlyList<string> AllClaims { get; private set; }
    }
}

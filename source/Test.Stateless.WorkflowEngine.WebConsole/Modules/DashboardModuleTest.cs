using Nancy.Testing;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.Modules;

namespace Test.Stateless.WorkflowEngine.WebConsole.Modules
{
    [TestFixture]
    public class DashboardModuleTest
    {
        [SetUp]
        public void SetUp_DashboardModuleTest()
        {

        }

        


        #region Private Methods

        private Browser CreateBrowser(UserIdentity currentUser)
        {
            var browser = new Browser((bootstrapper) =>
                            bootstrapper.Module(new DashboardModule())
                                .RootPathProvider(new TestRootPathProvider())
                                .RequestStartup((container, pipelines, context) => {
                                    context.CurrentUser = currentUser;
                                })
                            );
            return browser;
        }

        #endregion


    }
}

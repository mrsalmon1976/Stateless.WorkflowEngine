using Nancy.Testing;
using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
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
            var userStore = Substitute.For<IUserStore>();
            var browser = new Browser((bootstrapper) =>
                            bootstrapper.Module(new DashboardModule(userStore))
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

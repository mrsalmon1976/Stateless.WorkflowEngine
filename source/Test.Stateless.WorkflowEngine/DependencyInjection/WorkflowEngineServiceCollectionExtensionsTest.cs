using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Linq;
using System.Threading.Tasks;
using Test.Stateless.WorkflowEngine.Logging;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Test.Stateless.WorkflowEngine.Workflows.Basic.Actions;
using Test.Stateless.WorkflowEngine.Workflows.Scoped;

namespace Test.Stateless.WorkflowEngine.DependencyInjection
{
    [TestFixture]
    public class WorkflowEngineServiceCollectionExtensionsTest
    {
        #region AddStatelessWorkflowEngine Tests

        [Test]
        public void AddStatelessWorkflowEngine_WithMemoryStore_ServerAndClientShareTheStore()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine().WithMemoryStore();

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IWorkflowStore store = serviceProvider.GetRequiredService<IWorkflowStore>();
            IWorkflowServer server = serviceProvider.GetRequiredService<IWorkflowServer>();
            WorkflowClient client = (WorkflowClient)serviceProvider.GetRequiredService<IWorkflowClient>();

            Assert.That(store, Is.InstanceOf<MemoryWorkflowStore>());
            Assert.That(server.WorkflowStore, Is.SameAs(store));
            Assert.That(client.WorkflowStore, Is.SameAs(store));
            Assert.That(serviceProvider.GetRequiredService<IWorkflowServer>(), Is.SameAs(server));
        }

        [Test]
        public void AddStatelessWorkflowEngine_ConfigureDelegate_AppliedToServerOptions()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine(options => options.ClaimBatchSize = 3).WithMemoryStore();
            services.Configure<WorkflowServerOptions>(options => options.NodeName = "configured");

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IWorkflowServer server = serviceProvider.GetRequiredService<IWorkflowServer>();

            Assert.That(server.Options.ClaimBatchSize, Is.EqualTo(3));
            Assert.That(server.Options.NodeName, Is.EqualTo("configured"));
        }

        [Test]
        public void AddStatelessWorkflowEngine_Server_UsesServiceProvider()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine().WithMemoryStore();

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IWorkflowServer server = serviceProvider.GetRequiredService<IWorkflowServer>();

            // singleton factories get the root scope rather than the provider itself, which resolves the same singletons
            Assert.That(server.ServiceProvider.GetRequiredService<IWorkflowStore>(), Is.SameAs(serviceProvider.GetRequiredService<IWorkflowStore>()));
        }

        [Test]
        public void AddStatelessWorkflowEngine_NoStore_ThrowsWorkflowException()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine();

            using ServiceProvider serviceProvider = services.BuildServiceProvider();

            WorkflowException ex = Assert.Throws<WorkflowException>(() => serviceProvider.GetRequiredService<IWorkflowServer>());
            Assert.That(ex.Message, Does.Contain("No workflow store registered"));
            Assert.Throws<WorkflowException>(() => serviceProvider.GetRequiredService<IWorkflowClient>());
        }

        [Test]
        public void AddStatelessWorkflowEngine_LoggerFactoryRegistered_ServerLogsThroughIt()
        {
            ListLoggerFactory loggerFactory = new ListLoggerFactory();
            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddStatelessWorkflowEngine().WithMemoryStore();

            using (ServiceProvider serviceProvider = services.BuildServiceProvider())
            {
                serviceProvider.GetRequiredService<IWorkflowServer>().ExecuteWorkflows(1);
            }

            Assert.That(loggerFactory.WithEventId(1100).Count(), Is.EqualTo(1));
        }

        [Test]
        public void DisposeServiceProvider_ServerHasExecutedWorkflows_NodeRemoved()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine().WithMemoryStore();

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IWorkflowStore store = serviceProvider.GetRequiredService<IWorkflowStore>();
            serviceProvider.GetRequiredService<IWorkflowServer>().ExecuteWorkflows(1);
            Assert.That(store.GetNodes().Count(), Is.EqualTo(1));

            serviceProvider.Dispose();

            Assert.That(store.GetNodes(), Is.Empty);
        }

        #endregion

        #region Store Tests

        [Test]
        public void WithStore_CalledTwice_LastStoreUsed()
        {
            MemoryWorkflowStore first = new MemoryWorkflowStore();
            MemoryWorkflowStore second = new MemoryWorkflowStore();
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine()
                .WithStore(sp => first)
                .WithStore(sp => second);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();

            Assert.That(services.Count(x => x.ServiceType == typeof(IWorkflowStore)), Is.EqualTo(1));
            Assert.That(serviceProvider.GetRequiredService<IWorkflowServer>().WorkflowStore, Is.SameAs(second));
        }

        [Test]
        public void WithStore_StoreType_StoreCreatedByServiceProvider()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine().WithStore<MemoryWorkflowStore>();

            using ServiceProvider serviceProvider = services.BuildServiceProvider();

            Assert.That(serviceProvider.GetRequiredService<IWorkflowStore>(), Is.InstanceOf<MemoryWorkflowStore>());
        }

        #endregion

        #region AddWorkflowType Tests

        [Test]
        public void AddWorkflowType_ServerCreated_WorkflowTypeRegistered()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine()
                .WithMemoryStore()
                .AddWorkflowType<BasicWorkflow>()
                .AddWorkflowType<BasicWorkflow>();

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IWorkflowStore store = serviceProvider.GetRequiredService<IWorkflowStore>();
            serviceProvider.GetRequiredService<IWorkflowServer>();

            Assert.That(store.GetDefinitionByQualifiedName(typeof(BasicWorkflow).FullName), Is.Not.Null);
            Assert.That(store.GetDefinitions().Count(), Is.EqualTo(1));
        }

        #endregion

        #region AddWorkflowActionsFromAssembly Tests

        [Test]
        public void AddWorkflowActionsFromAssemblyOf_RegistersConcreteActionsAsTransient()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine().AddWorkflowActionsFromAssemblyOf<ScopedWorkflow>();

            ServiceDescriptor[] actions = services.Where(x => typeof(IWorkflowAction).IsAssignableFrom(x.ServiceType) || typeof(IWorkflowActionAsync).IsAssignableFrom(x.ServiceType)).ToArray();

            Assert.That(actions.Select(x => x.ServiceType), Does.Contain(typeof(ScopedAction)).And.Contain(typeof(ScopedAsyncAction)).And.Contain(typeof(DoingStuffAction)));
            Assert.That(actions.Select(x => x.ServiceType), Does.Not.Contain(typeof(AbstractScopedAction)));
            Assert.That(actions.Any(x => x.ServiceType.IsGenericTypeDefinition), Is.False);
            Assert.That(actions.All(x => x.Lifetime == ServiceLifetime.Transient), Is.True);
        }

        [Test]
        public void AddWorkflowActionsFromAssemblyOf_Filter_OnlyMatchingActionsRegistered()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine().WithMemoryStore().AddWorkflowActionsFromAssemblyOf<ScopedWorkflow>(x => x.Namespace == typeof(ScopedWorkflow).Namespace);

            Type[] actionTypes = services.Where(x => typeof(IWorkflowAction).IsAssignableFrom(x.ServiceType) || typeof(IWorkflowActionAsync).IsAssignableFrom(x.ServiceType)).Select(x => x.ServiceType).ToArray();

            Assert.That(actionTypes, Is.EquivalentTo(new[] { typeof(ScopedAction), typeof(ScopedAsyncAction) }));

            // the scoped actions' dependencies are all registered, so validation passes
            services.AddScoped<ScopedDependency>();
            services.AddScoped<AsyncOnlyDependency>();
            Assert.DoesNotThrow(() => services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }).Dispose());
        }

        [Test]
        public void AddWorkflowActionsFromAssemblyOf_ActionAlreadyRegistered_RegistrationKept()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<ScopedAction>(sp => new ScopedAction(new ScopedDependency(new AsyncOnlyDependency())));
            services.AddStatelessWorkflowEngine().AddWorkflowActionsFromAssemblyOf<ScopedWorkflow>();

            ServiceDescriptor descriptor = services.Single(x => x.ServiceType == typeof(ScopedAction));
            Assert.That(descriptor.Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
        }

        [Test]
        public void AddWorkflowActionsFromAssemblyOf_ValidateOnBuildWithMissingDependency_Throws()
        {
            // the test assembly contains actions whose dependencies (e.g. ScopedDependency, or a string) aren't registered
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine().WithMemoryStore().AddWorkflowActionsFromAssemblyOf<ScopedWorkflow>();

            Assert.Throws<AggregateException>(() => services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }));
        }

        #endregion

        #region Action Creation Tests

        [Test]
        public void ExecuteWorkflowAction_UnregisteredAction_CreatedWithInjectedDependencies()
        {
            using ServiceProvider serviceProvider = BuildScopedServiceProvider();
            ScopedWorkflow workflow = CreateWorkflow(serviceProvider);

            workflow.RunAction<ScopedAction>();

            Assert.That(workflow.Dependencies.Single(), Is.Not.Null);
        }

        [Test]
        public void ExecuteWorkflowAction_RegisteredAction_RegistrationUsed()
        {
            ScopedDependency dependency = new ScopedDependency(new AsyncOnlyDependency());
            using ServiceProvider serviceProvider = BuildScopedServiceProvider(services => services.AddTransient(sp => new ScopedAction(dependency)));
            ScopedWorkflow workflow = CreateWorkflow(serviceProvider);

            workflow.RunAction<ScopedAction>();

            Assert.That(workflow.Dependencies.Single(), Is.SameAs(dependency));
        }

        [Test]
        public void ExecuteWorkflowAction_ScopedDependency_NewScopePerActionDisposedAfterwards()
        {
            using ServiceProvider serviceProvider = BuildScopedServiceProvider();
            ScopedWorkflow workflow = CreateWorkflow(serviceProvider);

            workflow.RunAction<ScopedAction>();
            workflow.RunAction<ScopedAction>();

            Assert.That(workflow.Dependencies.Count, Is.EqualTo(2));
            Assert.That(workflow.Dependencies[0], Is.Not.SameAs(workflow.Dependencies[1]));
            Assert.That(workflow.Dependencies.All(x => x.IsDisposed), Is.True);
            Assert.That(workflow.Dependencies.All(x => x.AsyncOnlyDependency.IsDisposed), Is.True);
        }

        [Test]
        public async Task ExecuteWorkflowActionAsync_ScopedDependency_NewScopePerActionDisposedAfterwards()
        {
            using ServiceProvider serviceProvider = BuildScopedServiceProvider();
            ScopedWorkflow workflow = CreateWorkflow(serviceProvider);

            await workflow.RunActionAsync<ScopedAsyncAction>();
            await workflow.RunActionAsync<ScopedAsyncAction>();

            Assert.That(workflow.Dependencies.Count, Is.EqualTo(2));
            Assert.That(workflow.Dependencies[0], Is.Not.SameAs(workflow.Dependencies[1]));
            Assert.That(workflow.Dependencies.All(x => x.IsDisposed), Is.True);
            Assert.That(workflow.Dependencies.All(x => x.AsyncOnlyDependency.IsDisposed), Is.True);
        }

        [Test]
        public void ExecuteWorkflowAction_ScopesValidated_ScopedDependencyResolvedFromScope()
        {
            // scope validation throws if a scoped service is resolved from the root provider
            using ServiceProvider serviceProvider = BuildScopedServiceProvider(validateScopes: true);
            ScopedWorkflow workflow = CreateWorkflow(serviceProvider);

            Assert.DoesNotThrow(() => workflow.RunAction<ScopedAction>());
        }

        [Test]
        public void ExecuteWorkflowAction_CreateWorkflowActionInstanceOverridden_OverrideGivenActionScope()
        {
            using ServiceProvider serviceProvider = BuildScopedServiceProvider(validateScopes: true);
            ActionCreatingScopedWorkflow workflow = new ActionCreatingScopedWorkflow();
            workflow.ServiceProvider = serviceProvider;

            workflow.RunAction<ScopedAction>();

            Assert.That(workflow.ServiceProviders.Single(), Is.Not.Null.And.Not.SameAs(serviceProvider));
            Assert.That(workflow.Dependencies.Single().IsDisposed, Is.True);
        }

        [Test]
        public async Task ExecuteWorkflowActionAsync_CreateWorkflowActionAsyncInstanceOverridden_OverrideGivenActionScope()
        {
            using ServiceProvider serviceProvider = BuildScopedServiceProvider(validateScopes: true);
            ActionCreatingScopedWorkflow workflow = new ActionCreatingScopedWorkflow();
            workflow.ServiceProvider = serviceProvider;

            await workflow.RunActionAsync<ScopedAsyncAction>();

            Assert.That(workflow.ServiceProviders.Single(), Is.Not.Null.And.Not.SameAs(serviceProvider));
            Assert.That(workflow.Dependencies.Single().IsDisposed, Is.True);
        }

        [Test]
        public async Task ExecuteWorkflowAction_CreateWorkflowActionInstanceOverriddenAndNoServiceProvider_OverrideGivenNull()
        {
            ActionCreatingScopedWorkflow workflow = new ActionCreatingScopedWorkflow();

            workflow.RunAction<ScopedAction>();
            await workflow.RunActionAsync<ScopedAsyncAction>();

            Assert.That(workflow.ServiceProviders, Is.EqualTo(new IServiceProvider[] { null, null }));
            Assert.That(workflow.Dependencies.Count, Is.EqualTo(2));
        }

        #endregion

        #region Private Methods

        private static ServiceProvider BuildScopedServiceProvider(Action<IServiceCollection> configure = null, bool validateScopes = false)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine().WithMemoryStore();
            services.AddScoped<ScopedDependency>();
            services.AddScoped<AsyncOnlyDependency>();
            configure?.Invoke(services);
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = validateScopes });
        }

        private static ScopedWorkflow CreateWorkflow(IServiceProvider serviceProvider)
        {
            ScopedWorkflow workflow = new ScopedWorkflow();
            workflow.ServiceProvider = serviceProvider;
            return workflow;
        }

        #endregion
    }
}

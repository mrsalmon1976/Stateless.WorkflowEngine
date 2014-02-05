using NSubstitute;
using StructureMap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Test.Stateless.WorkflowEngine
{
    /// <summary>
    /// Provides mocking utility methods.
    /// </summary>
    public class MockUtils
    {
        /// <summary>
        /// Creates a class of type T, and also registers the type with the underlying IoC container.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T CreateAndRegister<T>() where T : class
        {
            var mock = Substitute.For<T>();
            ObjectFactory.Configure(x => x.For<T>().Use(mock));
            return mock;
        }
    }
}

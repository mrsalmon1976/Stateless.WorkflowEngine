using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using NUnit.Framework;

namespace Test.Stateless.WorkflowEngine.MongoDb
{
    [SetUpFixture]
    public class TestSetUp
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // the store configures its own ids, but Guid properties on custom workflows (GuidPropertyWorkflow) need the
            // application to register a Guid serializer at startup - this is the registration documented in the README
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.CSharpLegacy));
        }
    }
}

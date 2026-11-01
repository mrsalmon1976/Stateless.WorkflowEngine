using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.MongoDb;
using Test.Stateless.WorkflowEngine.MongoDb.Workflows;

namespace Test.Stateless.WorkflowEngine.MongoDb
{
    /// <summary>
    /// Tests that Guids are stored in the same representation as previous versions of the store (MongoDB.Driver 2.x),
    /// so workflows saved before the driver 3 upgrade can still be found and read.
    /// </summary>
    [TestFixture]
    public class MongoDbGuidSerializationTest
    {
        // values in Fixtures/WorkflowSavedByDriver2.json, which was saved by version 5.1 of the store (MongoDB.Driver 2.30)
        private static readonly Guid LegacyWorkflowId = new Guid("0a8f3c1e-5b2d-4e7f-9a61-3c4d5e6f7a8b");
        private static readonly Guid LegacyCorrelationId = new Guid("11111111-2222-3333-4444-555555555555");
        private static readonly Guid LegacyOptionalId = new Guid("66666666-7777-8888-9999-aaaaaaaaaaaa");
        private static readonly Guid LegacyRelatedId = new Guid("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");

        private IMongoDatabase _database = null;

        #region SetUp and TearDown

        [OneTimeSetUp]
        public void MongoDbGuidSerializationTest_OneTimeSetUp()
        {
            var client = new MongoClient(TestSettings.MongoDbConnectionString);
            _database = client.GetDatabase("StatelessWorkflowGuidTest");
        }

        [SetUp]
        public void MongoDbGuidSerializationTest_SetUp()
        {
            if (IsDatabaseTest())
            {
                DeleteTestCollections();
            }
        }

        [TearDown]
        public void MongoDbGuidSerializationTest_TearDown()
        {
            if (IsDatabaseTest())
            {
                DeleteTestCollections();
            }
        }

        #endregion

        #region Class Map Tests

        [TestCase(typeof(Workflow))]
        [TestCase(typeof(WorkflowDefinition))]
        [TestCase(typeof(MongoWorkflow))]
        public void IdMember_StoreCreated_UsesCSharpLegacyRepresentation(Type type)
        {
            // creating a store configures the class maps
            new MongoDbWorkflowStore(Substitute.For<IMongoDatabase>());

            BsonClassMap classMap = BsonClassMap.LookupClassMap(type);

            GuidSerializer serializer = classMap.IdMemberMap.GetSerializer() as GuidSerializer;
            Assert.That(serializer, Is.Not.Null);
            Assert.That(serializer.GuidRepresentation, Is.EqualTo(GuidRepresentation.CSharpLegacy));
        }

        #endregion

        #region Legacy Data Tests

        [Test]
        [Category("RequiresMongoDb")]
        public void GetOrDefault_WorkflowSavedByDriver2_ReturnsWorkflowWithGuidProperties()
        {
            MongoDbWorkflowStore store = InsertLegacyWorkflow();

            GuidPropertyWorkflow workflow = (GuidPropertyWorkflow)store.GetOrDefault(LegacyWorkflowId);

            Assert.That(workflow, Is.Not.Null);
            Assert.That(workflow.Id, Is.EqualTo(LegacyWorkflowId));
            Assert.That(workflow.CorrelationId, Is.EqualTo(LegacyCorrelationId));
            Assert.That(workflow.OptionalId, Is.EqualTo(LegacyOptionalId));
            Assert.That(workflow.RelatedIds, Is.EqualTo(new List<Guid> { LegacyRelatedId }));
            Assert.That(workflow.BasicMetaData, Is.EqualTo("Saved by MongoDB.Driver 2.30"));
            Assert.That(workflow.Priority, Is.EqualTo(3));
        }

        [Test]
        [Category("RequiresMongoDb")]
        public void GetActive_WorkflowSavedByDriver2_ReturnsWorkflow()
        {
            MongoDbWorkflowStore store = InsertLegacyWorkflow();

            List<Workflow> workflows = store.GetActive(10).ToList();

            Assert.That(workflows.Select(w => w.Id), Is.EqualTo(new[] { LegacyWorkflowId }));
        }

        [Test]
        [Category("RequiresMongoDb")]
        public void SuspendAndUnsuspend_WorkflowSavedByDriver2_UpdatesWorkflow()
        {
            MongoDbWorkflowStore store = InsertLegacyWorkflow();

            store.SuspendWorkflow(LegacyWorkflowId);
            Assert.That(store.GetOrDefault(LegacyWorkflowId).IsSuspended, Is.True);

            store.UnsuspendWorkflow(LegacyWorkflowId);
            Assert.That(store.GetOrDefault(LegacyWorkflowId).IsSuspended, Is.False);
        }

        [Test]
        [Category("RequiresMongoDb")]
        public void GetWorkflowAsJson_WorkflowSavedByDriver2_ReturnsJsonWithReadableIds()
        {
            MongoDbWorkflowStore store = InsertLegacyWorkflow();

            string json = store.GetWorkflowAsJson(LegacyWorkflowId);

            Assert.That(json, Does.Contain($"CSUUID(\"{LegacyWorkflowId}\")"));
            Assert.That(json, Does.Contain($"CSUUID(\"{LegacyCorrelationId}\")"));
        }

        [Test]
        [Category("RequiresMongoDb")]
        public void GetIncompleteWorkflowsAsJson_WorkflowSavedByDriver2_JsonCanBeDeserialized()
        {
            MongoDbWorkflowStore store = InsertLegacyWorkflow();

            string json = store.GetIncompleteWorkflowsAsJson(10).Single();
            MongoWorkflow container = BsonSerializer.Deserialize<MongoWorkflow>(json);

            Assert.That(container.Id, Is.EqualTo(LegacyWorkflowId));
            Assert.That(((GuidPropertyWorkflow)container.Workflow).CorrelationId, Is.EqualTo(LegacyCorrelationId));
        }

        [Test]
        [Category("RequiresMongoDb")]
        public void Save_WorkflowSavedByDriver2_ReplacesExistingDocument()
        {
            MongoDbWorkflowStore store = InsertLegacyWorkflow();
            Workflow workflow = store.GetOrDefault(LegacyWorkflowId);
            workflow.RetryCount = 2;

            store.Save(workflow);

            List<BsonDocument> docs = GetRawDocuments(store.CollectionActive);
            Assert.That(docs.Count, Is.EqualTo(1));
            Assert.That(docs[0]["Workflow"]["RetryCount"].AsInt32, Is.EqualTo(2));
            AssertLegacyGuid(docs[0]["_id"], LegacyWorkflowId);
        }

        [Test]
        [Category("RequiresMongoDb")]
        public void Archive_WorkflowSavedByDriver2_MovesWorkflowToCompleted()
        {
            MongoDbWorkflowStore store = InsertLegacyWorkflow();
            Workflow workflow = store.GetOrDefault(LegacyWorkflowId);

            store.Archive(workflow);

            Assert.That(store.GetOrDefault(LegacyWorkflowId), Is.Null);
            Assert.That(store.GetCompletedOrDefault(LegacyWorkflowId), Is.Not.Null);
        }

        [Test]
        [Category("RequiresMongoDb")]
        public void Delete_WorkflowSavedByDriver2_RemovesWorkflow()
        {
            MongoDbWorkflowStore store = InsertLegacyWorkflow();

            store.Delete(LegacyWorkflowId);

            Assert.That(GetRawDocuments(store.CollectionActive), Is.Empty);
        }

        #endregion

        #region New Data Tests

        [Test]
        [Category("RequiresMongoDb")]
        public void Save_NewWorkflow_GuidsStoredAsCSharpLegacy()
        {
            MongoDbWorkflowStore store = new MongoDbWorkflowStore(_database);
            GuidPropertyWorkflow workflow = new GuidPropertyWorkflow();
            workflow.CorrelationId = Guid.NewGuid();

            store.Save(workflow);

            BsonDocument doc = GetRawDocuments(store.CollectionActive).Single();
            AssertLegacyGuid(doc["_id"], workflow.Id);
            AssertLegacyGuid(doc["Workflow"]["_id"], workflow.Id);
            AssertLegacyGuid(doc["Workflow"]["CorrelationId"], workflow.CorrelationId);
        }

        [Test]
        [Category("RequiresMongoDb")]
        public void SaveDefinition_NewDefinition_IdStoredAsCSharpLegacy()
        {
            MongoDbWorkflowStore store = new MongoDbWorkflowStore(_database);
            WorkflowDefinition definition = new WorkflowDefinition { Name = "Test", QualifiedName = "Test.Workflow", Graph = "digraph {}" };

            store.SaveDefinition(definition);

            BsonDocument doc = GetRawDocuments(store.CollectionDefinitions).Single();
            AssertLegacyGuid(doc["_id"], definition.Id);
            Assert.That(store.GetDefinitionByQualifiedName("Test.Workflow").Id, Is.EqualTo(definition.Id));
        }

        #endregion

        #region Private Methods

        private MongoDbWorkflowStore InsertLegacyWorkflow()
        {
            MongoDbWorkflowStore store = new MongoDbWorkflowStore(_database);
            store.RegisterType(typeof(GuidPropertyWorkflow));

            string json = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "WorkflowSavedByDriver2.json"));
            _database.GetCollection<BsonDocument>(store.CollectionActive).InsertOne(BsonDocument.Parse(json));
            return store;
        }

        private List<BsonDocument> GetRawDocuments(string collectionName)
        {
            return _database.GetCollection<BsonDocument>(collectionName).Find(new BsonDocument()).ToList();
        }

        private static void AssertLegacyGuid(BsonValue value, Guid expected)
        {
            BsonBinaryData binary = value.AsBsonBinaryData;
            Assert.That(binary.SubType, Is.EqualTo(BsonBinarySubType.UuidLegacy));
            Assert.That(binary.ToGuid(GuidRepresentation.CSharpLegacy), Is.EqualTo(expected));
        }

        private static bool IsDatabaseTest()
        {
            return TestContext.CurrentContext.Test.Properties["Category"].Contains("RequiresMongoDb");
        }

        private void DeleteTestCollections()
        {
            foreach (string c in _database.ListCollectionNames().ToList())
            {
                _database.DropCollection(c);
            }
        }

        #endregion
    }
}

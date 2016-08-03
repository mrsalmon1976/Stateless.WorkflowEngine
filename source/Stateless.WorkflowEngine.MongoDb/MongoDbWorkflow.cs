using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine.MongoDb
{
    [BsonIgnoreExtraElements]
    internal class MongoDbWorkflow : Workflow
    {
        public override void Fire(string triggerName)
        {
            // no implementation
        }

        public override void Initialise(string initialState)
        {
            // no implementation
        }
    }
}

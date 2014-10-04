using Raven.Abstractions;
using Raven.Database.Linq;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System;
using Raven.Database.Linq.PrivateExtensions;
using Lucene.Net.Documents;
using System.Globalization;
using System.Text.RegularExpressions;
using Raven.Database.Indexing;


public class Index_Auto_2fw0airNhrVO76Ok0v3XTVrg_3d_3d : Raven.Database.Linq.AbstractViewGenerator
{
	public Index_Auto_2fw0airNhrVO76Ok0v3XTVrg_3d_3d()
	{
		this.ViewText = @"from doc in docs.WorkflowContainers
select new { Workflow_IsSuspended = doc.Workflow.IsSuspended, Workflow_RetryCount = doc.Workflow.RetryCount, Workflow_CreatedOn = doc.Workflow.CreatedOn, Workflow_ResumeOn = doc.Workflow.ResumeOn }";
		this.ForEntityNames.Add("WorkflowContainers");
		this.AddMapDefinition(docs => 
			from doc in docs
			where string.Equals(doc["@metadata"]["Raven-Entity-Name"], "WorkflowContainers", System.StringComparison.InvariantCultureIgnoreCase)
			select new {
				Workflow_IsSuspended = doc.Workflow.IsSuspended,
				Workflow_RetryCount = doc.Workflow.RetryCount,
				Workflow_CreatedOn = doc.Workflow.CreatedOn,
				Workflow_ResumeOn = doc.Workflow.ResumeOn,
				__document_id = doc.__document_id
			});
		this.AddField("Workflow_IsSuspended");
		this.AddField("Workflow_RetryCount");
		this.AddField("Workflow_CreatedOn");
		this.AddField("Workflow_ResumeOn");
		this.AddField("__document_id");
		this.AddQueryParameterForMap("Workflow.IsSuspended");
		this.AddQueryParameterForMap("Workflow.RetryCount");
		this.AddQueryParameterForMap("Workflow.CreatedOn");
		this.AddQueryParameterForMap("Workflow.ResumeOn");
		this.AddQueryParameterForMap("__document_id");
		this.AddQueryParameterForReduce("Workflow.IsSuspended");
		this.AddQueryParameterForReduce("Workflow.RetryCount");
		this.AddQueryParameterForReduce("Workflow.CreatedOn");
		this.AddQueryParameterForReduce("Workflow.ResumeOn");
		this.AddQueryParameterForReduce("__document_id");
	}
}

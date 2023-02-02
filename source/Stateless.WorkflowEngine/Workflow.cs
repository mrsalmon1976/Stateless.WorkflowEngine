﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleToAttribute("Test.Stateless.WorkflowEngine")]

namespace Stateless.WorkflowEngine
{
    public abstract class Workflow
    {

        public Workflow() : this("Start")
        {
        }

        public Workflow(string initialState)
        {
            // set default values : always initialise with a unique id - this can be set when an existing workflow is loaded from the store
            this.Id = Guid.NewGuid();
            this.CreatedOn = DateTime.UtcNow;

            // default retry intervals to 5 retries of 5, 10, 15, 30, 60 seconds
            this.RetryIntervals = new[] { 5, 10, 15, 30, 60 };

            this.Initialise(initialState);
        }

        /// <summary>
        /// Gets/sets the workflow unique identifier.
        /// </summary>
        public virtual Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the creation date.
        /// </summary>
        /// <value>
        /// The creation date.
        /// </value>
        public virtual DateTime CreatedOn { get; set; }

        /// <summary>
        /// The date/time the workflow completed.
        /// </summary>
        public virtual DateTime? CompletedOn { get; internal set; }

        /// <summary>
        /// Gets/sets the class used to resolve dependencies.  This will be set by the workflow server before 
        /// executing a workflow step, and if a value is supplied, this will be used to create the workflow 
        /// action.
        /// </summary>
        internal IWorkflowEngineDependencyResolver DependencyResolver { get; set; }

        /// <summary>
        /// Marks a workflow as suspended.  This will be set to true when the maximum retry count is exceeded.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is suspended; otherwise, <c>false</c>.
        /// </value>
        public virtual bool IsSuspended { get; set; }

        /// <summary>
        /// Gets/sets the last exception info pertaining to the workflow.
        /// </summary>
        public virtual string LastException { get; set; }

        /// <summary>
        /// Gets the name of the workflow (class name).  Provides a setter for serialisation into document databases, but 
        /// programmatically setting this value will have no impact.
        /// </summary>
        public virtual string Name 
        { 
            get 
            {
                return this.GetType().Name;
            } 
            set
            {
                // does nothing
            }
        }

        /// <summary>
        /// Gets/sets the time the workflow can be resumed.  Set to DateTime.Min to ensure workflows are picked up - nullable 
        /// properties are not used here as MongoDb's Query does not support it.
        /// </summary>
        public virtual DateTime ResumeOn { get; set; }

        /// <summary>
        /// Gets/sets the priority of the workflow.  Defaults to 0, but can be set at runtime.  Higher priority 
        /// workflows (where 2 is higher than 1) will be executed first.
        /// </summary>
        public virtual int Priority { get; set; }

        /// <summary>
        /// Gets or sets the trigger to be fire when the workflow resumes.
        /// </summary>
        /// <value>
        /// The trigger.
        /// </value>
        public virtual string ResumeTrigger { get; set; }

        /// <summary>
        /// Gets or sets the retry count for the workflow at the current step.  This gets reset to 0 when a step successfully completes, 
        /// and will increment each time a step fails.
        /// </summary>
        /// <value>
        /// The retry count.
        /// </value>
        public virtual int RetryCount { get; set; }

        /// <summary>
        /// Gets/sets the retry intervals, in seconds, of the workflow.
        /// </summary>
        public virtual int[] RetryIntervals { get; set; }

        /// <summary>
        /// Gets the qualified name of the workflow (full qualified class name).  Provides a setter for serialisation into document databases, but 
        /// programmatically setting this value will have no impact.
        /// </summary>
        public virtual string QualifiedName
        {
            get
            {
                return this.GetType().FullName;
            }
            set
            {
                // does nothing
            }
        }

        /// <summary>
        /// Gets/sets the current state of the workflow.
        /// </summary>
        public virtual string CurrentState { get; set; }

        /// <summary>
        /// Gets/sets whether the workflow is complete.
        /// </summary>
        public virtual bool IsComplete { get; set; }

        /// <summary>
        /// Gets whether the worker is a single-instance type workflow worker.
        /// </summary>
        public virtual bool IsSingleInstance { get; set; }

        /// <summary>
        /// Creates a new WorkflowAction instance.  This can be overridden if you'd like to use your 
        /// own DI framework to create the actions.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        protected virtual IWorkflowAction CreateWorkflowActionInstance<T>() where T : IWorkflowAction
        {
            return Activator.CreateInstance<T>();
        }

        /// <summary>
        /// Gets a graph representation of the workflow in the DOT graph language.
        /// </summary>
        /// <returns></returns>
        public abstract string GetGraph();

        /// <summary>
        /// Initialises and configures the workflow.
        /// </summary>
        /// <param name="initialState"></param>
        public abstract void Initialise(string initialState);

        /// <summary>
        /// Fires a trigger - this needs to be a trigger configured for the current state.
        /// </summary>
        /// <param name="triggerName"></param>
        public abstract void Fire(string triggerName);

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        protected virtual void ExecuteWorkflowAction<T>() where T : class, IWorkflowAction
        {
            IWorkflowAction workflowAction;

            if (this.DependencyResolver == null)
            {
                workflowAction = this.CreateWorkflowActionInstance<T>();
            }
            else
            {
                workflowAction = this.DependencyResolver.GetInstance<T>();
            }
            workflowAction.Execute(this);
        }

        /// <summary>
        /// Fires when the workflow is suspended.  This can be overridden to handle any workflow-specific 
        /// suspension activities.
        /// </summary>
        public virtual void OnSuspend()
        {
        }

        /// <summary>
        /// Fires when the workflow completes.  This can be overridden to handle any workflow-specific 
        /// completion activities.
        /// </summary>
        public virtual void OnComplete()
        {
        }

        /// <summary>
        /// Fires when an error happens within the workflow.  This can be overriden to add logging, or any other 
        /// exception management.
        /// </summary>
        public virtual void OnError(Exception ex)
        {
        }

    }
}

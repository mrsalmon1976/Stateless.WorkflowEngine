using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Models;
using Stateless;
using StructureMap;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

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
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the creation date.
        /// </summary>
        /// <value>
        /// The creation date.
        /// </value>
        public DateTime CreatedOn { get; set; }

        /// <summary>
        /// Marks a workflow as suspended.  This will be set to true when the maximum retry count is exceeded.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is suspended; otherwise, <c>false</c>.
        /// </value>
        public bool IsSuspended { get; set; }

        /// <summary>
        /// Gets/sets the last exception info pertaining to the workflow.
        /// </summary>
        public string LastException { get; set; }

        /// <summary>
        /// Gets/sets the time the workflow can be resumed.
        /// </summary>
        public DateTime? ResumeOn { get; set; }

        /// <summary>
        /// Gets or sets the trigger to be fire when the workflow resumes.
        /// </summary>
        /// <value>
        /// The trigger.
        /// </value>
        public string ResumeTrigger { get; set; }

        /// <summary>
        /// Gets or sets the retry count for the workflow at the current step.  This gets reset to 0 when a step successfully completes, 
        /// and will increment each time a step fails.
        /// </summary>
        /// <value>
        /// The retry count.
        /// </value>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets/sets the retry intervals, in seconds, of the workflow.
        /// </summary>
        public int[] RetryIntervals { get; set; }

        /// <summary>
        /// Gets/sets the current state of the workflow.
        /// </summary>
        public virtual string CurrentState { get; set; }

        /// <summary>
        /// Gets/sets whether the workflow is complete.
        /// </summary>
        public bool IsComplete { get; set; }

        /// <summary>
        /// Gets whether the worker is a single-instance type workflow worker.
        /// </summary>
        public bool IsSingleInstance { get; set; }

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
        protected virtual void ExecuteWorkflowAction<T>() where T : IWorkflowAction
        {
            IWorkflowAction workflowAction = ObjectFactory.TryGetInstance<T>();
            if (workflowAction == null)
            {
                workflowAction = Activator.CreateInstance<T>();
            }
            workflowAction.Execute(this);

        }

    }

    public abstract class StateWorkflow<TState, TTrigger> : Workflow
        where TState : struct, IConvertible
        where TTrigger : struct, IConvertible
    {
        private StateMachine<TState, TTrigger> _stateMachine;

        #region Constructors

        #endregion

        public StateWorkflow() : this("Start")
        {
        }

        public StateWorkflow(string initialState) : base(initialState)
        {
            this.Initialise(initialState);
        }


        public override string CurrentState
        {
            get
            {
                if (this._stateMachine == null)
                {
                    throw new WorkflowNotConfiguredException(this.GetType());
                }
                return _stateMachine.State.ToString();
            }
            set
            {
                if (this._stateMachine == null)
                {
                    throw new WorkflowNotConfiguredException(this.GetType());
                }
                this.Initialise(value.ToString());
            }
        }

        public override void Initialise(string initialState)
        {
            TState state = this.ConvertStringState(initialState);
            this._stateMachine = new StateMachine<TState, TTrigger>(state);
        }

        protected StateMachine<TState, TTrigger>.StateConfiguration Configure(TState state)
        {
            Action emptyAction = () => { };
            return this.Configure(state, emptyAction, emptyAction);
        }

        protected StateMachine<TState, TTrigger>.StateConfiguration Configure(TState state, Action onEntry, Action onExit)
        {
            return _stateMachine.Configure(state)
                .OnEntry(onEntry)
                .OnExit(onExit);
        }

        protected TState ConvertStringState(string stateName)
        {
            TState state;
            bool isValidState = Enum.TryParse(stateName, out state);
            if (isValidState)
            {
                return state;
            }
            else
            {
                throw new WorkflowException(String.Format("{0} is not a valid state for workflow {1}", stateName, this.GetType().FullName));
            }
        }

        protected TTrigger ConvertStringTrigger(string triggerName)
        {
            TTrigger trigger;
            bool isValidTrigger = Enum.TryParse(triggerName, out trigger);
            if (isValidTrigger)
            {
                return trigger;
            }
            else
            {
                throw new WorkflowException(String.Format("{0} is not a valid trigger for workflow {1}", triggerName, this.GetType().FullName));
            }
        }


        /// <summary>
        /// Fire a trigger on the workflow by it's name.
        /// </summary>
        /// <param name="triggerName"></param>
        public override void Fire(string triggerName)
        {
            if (String.IsNullOrEmpty(triggerName))
            {
                throw new WorkflowException(String.Format("Unable to fire null or empty trigger name in worker '{0}'", this.GetType().FullName));
            }
            TTrigger trigger = ConvertStringTrigger(triggerName);
            this.Fire(trigger);
        }

        public virtual void Fire(TTrigger trigger)
        {
            this._stateMachine.Fire(trigger);
        }



    }
}

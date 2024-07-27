using Stateless.Graph;
using Stateless.WorkflowEngine.Exceptions;
using System;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine
{
    public abstract class StateWorkflow<TState, TTrigger> : Workflow
        where TState : struct, IConvertible
        where TTrigger : struct, IConvertible
    {
        private StateMachine<TState, TTrigger> _stateMachine;

        #region Constructors

        public StateWorkflow() : this("Start")
        {
        }

        public StateWorkflow(string initialState) : this(initialState, null)
        {
        }

        public StateWorkflow(TState initialState, TTrigger initialTrigger) : this(initialState.ToString(), initialTrigger.ToString())
        {
        }


        public StateWorkflow(string initialState, string initialTrigger) : base(initialState)
        {
            this.Initialise(initialState);

            if (!String.IsNullOrEmpty(initialTrigger))
            {
                this.ConvertStringTrigger(initialTrigger);
                this.ResumeTrigger = initialTrigger;
            }
        }



        #endregion

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

        /// <summary>
        /// Gets a graph representation of the workflow in the DOT graph language.
        /// </summary>
        /// <returns></returns>
        public override string GetGraph()
        {
            if (this._stateMachine == null)
            {
                throw new WorkflowNotConfiguredException(this.GetType());
            }
            return UmlDotGraph.Format(_stateMachine.GetInfo());
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

        /// <summary>
        /// Fire a trigger on the workflow by it's name.
        /// </summary>
        /// <param name="triggerName"></param>
        public override async Task FireAsync(string triggerName)
        {
            if (String.IsNullOrEmpty(triggerName))
            {
                throw new WorkflowException(String.Format("Unable to fire null or empty trigger name in worker '{0}'", this.GetType().FullName));
            }
            TTrigger trigger = ConvertStringTrigger(triggerName);
            await this.FireAsync(trigger);
        }

        public virtual void Fire(TTrigger trigger)
        {
            this._stateMachine.Fire(trigger);
        }

        public virtual async Task FireAsync(TTrigger trigger)
        {
            await this._stateMachine.FireAsync(trigger);
        }


    }

}

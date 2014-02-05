using Stateless.WorkflowEngine;
using System;
using System.IO;
using Test.Stateless.WorkflowEngine.Example.Workflows.FileCreation.Actions;

namespace Test.Stateless.WorkflowEngine.Example.Workflows.FileCreation
{
    /// <summary>
    /// Basic workflow that does nothing.
    /// </summary>
    public class FileCreationWorkflow : StateWorkflow<FileCreationWorkflow.State, FileCreationWorkflow.Trigger>
    {
        public enum State
        {
            Start,
            SendingFirstEmail,
            SendingSecondEmail,
            Complete
        }

        public enum Trigger
        {
            Start,
            SendFirstEmail,
            SendSecondEmail,
            Complete
        }

        public FileCreationWorkflow()
            : this(FileCreationWorkflow.State.Start)
        {
        }

        public FileCreationWorkflow(string initialState) : base(initialState)
        {
            this.IsSingleInstance = true;
        }

        public FileCreationWorkflow(State initialState)
            : this(initialState.ToString())
        {
        }

        public int SecondFileWriteCount { get; set; }

        public string RootFolder { get; set; }

        public string GetFilePath(string prefix)
        {
            string fileName = prefix + "_" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
            return Path.Combine(this.RootFolder, fileName);
        }


        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);

            this.Configure(State.Start)
                .Permit(Trigger.SendFirstEmail, State.SendingFirstEmail);

            this.Configure(State.SendingFirstEmail)
                .OnEntry(() => this.ExecuteWorkflowAction<WriteFirstFileAction>())
                .Permit(Trigger.SendSecondEmail, State.SendingSecondEmail);

            this.Configure(State.SendingSecondEmail)
                .OnEntry(() => this.ExecuteWorkflowAction<WriteSecondFileAction>())
                .Permit(Trigger.SendFirstEmail, State.SendingFirstEmail)
                .Permit(Trigger.Complete, State.Complete);

            this.Configure(State.Complete)
                .OnEntry(() => this.IsComplete = true);
        }
    }
}

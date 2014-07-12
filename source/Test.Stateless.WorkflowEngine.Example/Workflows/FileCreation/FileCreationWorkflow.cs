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
            WritingFirstFile,
            WritingSecondFile,
            CleaningUp,
            Complete
        }

        public enum Trigger
        {
            Start,
            WriteFirstFile,
            WriteSecondFile,
            CleanUp,
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
                .Permit(Trigger.WriteFirstFile, State.WritingFirstFile);

            this.Configure(State.WritingFirstFile)
                .OnEntry(() => this.ExecuteWorkflowAction<WriteFirstFileAction>())
                .Permit(Trigger.WriteSecondFile, State.WritingSecondFile);

            this.Configure(State.WritingSecondFile)
                .OnEntry(() => this.ExecuteWorkflowAction<WriteSecondFileAction>())
                .Permit(Trigger.WriteFirstFile, State.WritingFirstFile)
                .Permit(Trigger.CleanUp, State.CleaningUp);

            this.Configure(State.CleaningUp)
                .OnEntry(() => this.ExecuteWorkflowAction<CleanupAction>())
                .Permit(Trigger.Complete, State.Complete);

            this.Configure(State.Complete)
                .OnEntry(() => this.IsComplete = true);

        }
    }
}

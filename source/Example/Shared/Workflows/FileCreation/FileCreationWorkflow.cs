using Example.Shared.Workflows.FileCreation.Actions;
using MongoDB.Bson.Serialization.Attributes;
using Stateless.WorkflowEngine;
using System;
using System.IO;

namespace Example.Shared.Workflows.FileCreation
{
    [BsonIgnoreExtraElements]
    public class FileCreationWorkflow : StateWorkflow<FileCreationWorkflow.State, FileCreationWorkflow.Trigger>
    {

        public enum State
        {
            Start,
            WritingFirstFile,
            WritingAdditionalFiles,
            CleaningUp,
            Complete
        }

        public enum Trigger
        {
            WriteFirstFile,
            WriteAdditionalFiles,
            CleanUp,
            Complete
        }

        public FileCreationWorkflow() : this(State.Start, Trigger.WriteFirstFile)
        {
        }

        public FileCreationWorkflow(State initialState, Trigger initialTrigger) : base(initialState, initialTrigger)
        {
        }

        public int? FilesToCreateCount { get; set; }

        public int? FilesCreatedCount { get; set; }

        public string? FileNamePrefix { get; set; }

        public string GetFilePath(string rootFolder, string fileName)
        {
            string filePath = FileNamePrefix + "_" + fileName;
            return Path.Combine(rootFolder, filePath);
        }


        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);

            this.Configure(State.Start)
                .Permit(Trigger.WriteFirstFile, State.WritingFirstFile);

            this.Configure(State.WritingFirstFile)
                .OnEntry(() => this.ExecuteWorkflowAction<WriteFirstFileAction>())
                .Permit(Trigger.WriteAdditionalFiles, State.WritingAdditionalFiles);

            this.Configure(State.WritingAdditionalFiles)
                .OnEntry(() => this.ExecuteWorkflowAction<WriteAdditionalFilesAction>())
                .PermitReentry(Trigger.WriteAdditionalFiles)
                .Permit(Trigger.CleanUp, State.CleaningUp);

            this.Configure(State.CleaningUp)
                .OnEntry(() => this.ExecuteWorkflowAction<CleanupAction>())
                .Permit(Trigger.Complete, State.Complete);

            this.Configure(State.Complete)
                .OnEntry(() => this.IsComplete = true);

        }

    }
}

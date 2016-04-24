using System;
using System.Collections.Generic;

namespace Sbs20.Syncotron
{
    public class ReplicatorArgs
    {
        public int MaximumConcurrency { get; set; }
        public string LocalPath { get; set; }
        public string RemotePath { get; set; }
        public DateTime LastRun { get; set; }
        public ReplicationType ReplicationType { get; set; }
        public ReplicationDirection ReplicationDirection { get; set; }
        public ProcessingMode ProcessingMode { get; set; }
        public IList<string> Exclusions { get; private set; }
        public bool IgnoreCertificateErrors { get; set; }

        public ReplicatorArgs()
        {
            this.ReplicationType = ReplicationType.AnalysisOnly;
            this.ReplicationDirection = ReplicationDirection.TwoWay;
            this.ProcessingMode = ProcessingMode.Serial;
            this.Exclusions = new List<string>();
            this.IgnoreCertificateErrors = false;
        }

        public string ToLocalPath(string path)
        {
            return this.LocalPath + path.Substring(this.RemotePath.Length);
        }

        public string ToRemotePath(string path)
        {
            return this.RemotePath + path.Substring(this.LocalPath.Length);
        }

        public IList<string> Errors()
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(this.LocalPath)) errors.Add("LocalPath");
            if (this.RemotePath == null) errors.Add("RemotePath");
            return errors;
        }
    }
}

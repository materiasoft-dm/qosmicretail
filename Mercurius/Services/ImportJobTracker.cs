using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Mercurius.Services
{
    public class ImportJobState
    {
        public string JobId { get; set; } = "";
        // Queued, Parsing, Importing, Completed, Failed
        public string Status { get; set; } = "Queued";
        public int Total { get; set; }
        public int Processed { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }
        public List<string> Log { get; } = new();
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// In-memory, per-process tracker for background import jobs. Deliberately not persisted —
    /// an import realistically completes within the app's lifetime, and losing progress state on
    /// a restart (which would also kill the in-flight job itself) is an acceptable trade-off for
    /// not needing a database table just for this.
    /// </summary>
    public class ImportJobTracker
    {
        private readonly ConcurrentDictionary<string, ImportJobState> _jobs = new();

        public ImportJobState CreateJob(string jobId)
        {
            var job = new ImportJobState { JobId = jobId };
            _jobs[jobId] = job;
            return job;
        }

        public ImportJobState? Get(string jobId)
        {
            return _jobs.TryGetValue(jobId, out var job) ? job : null;
        }

        public void AppendLog(string jobId, string line)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                lock (job)
                {
                    job.Log.Add(line);
                }
            }
        }

        public List<string> SnapshotLog(ImportJobState job)
        {
            lock (job)
            {
                return new List<string>(job.Log);
            }
        }
    }
}

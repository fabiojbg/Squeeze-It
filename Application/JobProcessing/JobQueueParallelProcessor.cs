using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqueezeIt.JobProcessing
{
    public class JobQueueParallelProcessor<TJobData>
    {
        private readonly ConcurrentQueue<TJobData> _jobDataQueue;
        private readonly object _lock = new object();
        private Task _parallelJobsProcessorTask;
        private readonly SemaphoreSlim _parallelWorkLimiterSemaphore;  // controlls the max number of threads allowed to process the queue in parallel
        private readonly int _maxParallelTasks;
        private readonly string _name;
        public Action OnAllItemsProcessed { get; set; }

        /// <summary>
        /// Return is the queue processor is running jobs, Note that jobs can still be running after the queue processor has ended.
        /// To query if jobs are running use <param><name>IsRunningJobs</name></param>
        /// instead.
        /// </summary>
        private bool _isProcessingQueue { get; set; }

        public bool IsRunningJobs => _isProcessingQueue ||
                                     _parallelWorkLimiterSemaphore.CurrentCount != _maxParallelTasks;

        private static readonly ConcurrentDictionary<string, JobQueueParallelProcessor<TJobData>> _processorsCreated =
                                            new ConcurrentDictionary<string, JobQueueParallelProcessor<TJobData>>();

        public static JobQueueParallelProcessor<TJobData> Create(string name,
                                                          int maxParallelTasks)
        {
            if (_processorsCreated.TryGetValue(name, out var processor))
                return processor;

            var newJob = new JobQueueParallelProcessor<TJobData>(name, maxParallelTasks);
            _processorsCreated.TryAdd(name, newJob);
            return newJob;
        }

        private JobQueueParallelProcessor(string name,
                                           int maxParallelTasks)
        {
            _maxParallelTasks = maxParallelTasks;
            _parallelWorkLimiterSemaphore = new SemaphoreSlim(maxParallelTasks);
            _name = name;
            _jobDataQueue = new ConcurrentQueue<TJobData>();
        }

        public JobQueueParallelProcessor<TJobData> AddJobToQueue(TJobData jobData, bool startProcessingImmediatelly = false)
        {
            if (jobData == null)
                return this;

            lock (_lock)
            {
                _jobDataQueue.Enqueue(jobData);

                return this;
            }
        }

        public Task ProcessQueue( Action<TJobData, CancellationToken?> job,
                                  CancellationToken? cancellationToken,
                                  Action<TJobData> jobDoneCallback = null,
                                  Action<TJobData, Exception> jobErrorCallback = null) // callback to treat job exceptions
        {
            lock (_lock) // lock is neessary to avoid multiple threads to start this
            {
                if (_isProcessingQueue || _jobDataQueue.IsEmpty)
                    return _parallelJobsProcessorTask;

                _isProcessingQueue = true;

                _parallelJobsProcessorTask = new SimpleBackgroundJob(_name).RunAsync(cancellationToken,
                        async (jobCancellationToken) =>
                        {
                            TJobData jobData;
                            try
                            {
                                var i = 0;
                                while (jobCancellationToken?.IsCancellationRequested != true &&
                                        _jobDataQueue.TryDequeue(out jobData))
                                {
                                    if (jobCancellationToken != null)
                                    {
                                        await _parallelWorkLimiterSemaphore.WaitAsync(jobCancellationToken.Value).ConfigureAwait(false); // wait if the maximum number of workers are working

                                        if (jobCancellationToken.Value.IsCancellationRequested)
                                            break;
                                    }
                                    else
                                    {
                                        await _parallelWorkLimiterSemaphore.WaitAsync().ConfigureAwait(false); // wait if the maximum number of workers are working
                                    }

                                    i = (i + 1) & 0x7FFFFFFF;
                                    Task backGroundTask = null;
                                    try
                                    {

                                        backGroundTask = new SimpleBackgroundJob<TJobData>($"Job {i} from queue {_name}")
                                                            .RunAsync(jobData, 
                                                                      jobCancellationToken, 
                                                                      job, 
                                                                      jobDoneCallback, 
                                                                      jobErrorCallback);
                                        Debug.WriteLine($"starting Job: {jobData}");
                                        var jobId = jobData.ToString();
                                        backGroundTask?.ContinueWith(
                                            (p) =>
                                            {
                                                Debug.WriteLine($"ContinueWith: {jobId}");
                                                _parallelWorkLimiterSemaphore.Release();
                                            });
                                    }
                                    catch ( Exception ex)
                                    {
                                        if (jobErrorCallback != null)
                                            jobErrorCallback(jobData, ex);
                                    }
                                }
                                _isProcessingQueue = false;

                                while (IsRunningJobs)
                                    Thread.Sleep(200);
                            }
                            catch (TaskCanceledException) { }
                            catch (OperationCanceledException) { }
                            finally
                            {
                                _isProcessingQueue = false;
                            }

                        });

                return _parallelJobsProcessorTask;
            }
        }

    }

}

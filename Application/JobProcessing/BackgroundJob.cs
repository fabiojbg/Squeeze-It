using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqueezeIt.JobProcessing
{
    public class SimpleBackgroundJob
    {
        private Task _jobTask;
        private readonly object _lock = new object();
        private readonly string _jobName;
        public bool IsRunning { get; private set; }


        public SimpleBackgroundJob(string jobName)
        {
            _jobName = jobName;
        }

        public Task RunAsync( CancellationToken? cancellationToken,
                              Func<CancellationToken?, Task> job,
                              Action<Exception> jobErrorCallback = null)
        {
            if (job == null)
                throw new Exception("job parameter cannot be null");

            lock (_lock) // lock is necessary to avoid multiple threads to start this
            {
                if (IsRunning)
                    return _jobTask;
                IsRunning = true;

                _jobTask = Task.Run(async () =>
                {
                    try
                    {
                        await job(cancellationToken).ConfigureAwait(false);
                        if (cancellationToken?.IsCancellationRequested == true)
                            throw new OperationCanceledException();
                    }
                    catch (Exception ex)
                    {
                        // since there is not default logging mechanism for this method, ignore exceptions...they must be treated in the caller
                        if (jobErrorCallback != null)
                            jobErrorCallback(ex);
                    }
                    finally
                    {
                        IsRunning = false;
                    }
                });
                _jobTask.ConfigureAwait(false);
                return _jobTask;
            }
        }
    }

    public class SimpleBackgroundJob<TJobData>
    {
        private Task _jobTask;
        private readonly object _lock = new object();
        private readonly string _jobName;
        public bool IsRunning { get; private set; }
        public TJobData JobData { get; private set; }

        public SimpleBackgroundJob(string jobName)
        {
            _jobName = jobName;
        }

        public Task RunAsync(TJobData jobData,
                              CancellationToken? cancellationToken,
                              Action<TJobData, CancellationToken?> job,
                              Action<TJobData> jobDoneCallback = null, 
                              Action<TJobData, Exception> jobErrorCallback = null
                              )
        {
            if (job == null)
                throw new Exception("job parameter cannot be null");

            lock (_lock) // lock is neessary to avoid multiple threads to start this
            {
                if (IsRunning)
                    return _jobTask;
                IsRunning = true;
                JobData = jobData;
                _jobTask = Task.Run(() =>
                {
                    try
                    {
                        job(jobData, cancellationToken);

                        if( jobDoneCallback != null )
                            jobDoneCallback(jobData);

                        if (cancellationToken?.IsCancellationRequested == true)
                            throw new OperationCanceledException();
                    }
                    catch (Exception ex)
                    {
                        // since there is not default logging mechanism for this method, ignore exceptions...they must be treated in the job caller
                        if (jobErrorCallback != null)
                            jobErrorCallback(jobData, ex);
                    }
                    finally
                    {
                        IsRunning = false;
                    }
                } );

                _jobTask.ConfigureAwait(false);

                return _jobTask;
            }
        }

    }
}

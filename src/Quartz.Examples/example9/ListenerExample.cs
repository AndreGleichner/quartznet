#region License

/* 
 * All content copyright Terracotta, Inc., unless otherwise indicated. All rights reserved. 
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not 
 * use this file except in compliance with the License. You may obtain a copy 
 * of the License at 
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0 
 *   
 * Unless required by applicable law or agreed to in writing, software 
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT 
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations 
 * under the License.
 * 
 */

#endregion

using System;
using System.Threading.Tasks;

using Quartz.Impl;
using Quartz.Impl.Matchers;
using Quartz.Logging;

namespace Quartz.Examples.Example9
{
    /// <summary> 
    /// Demonstrates the behavior of <see cref="IJobListener" />s.  In particular, 
    /// this example will use a job listener to trigger another job after one
    /// job successfully executes.
    /// </summary>
    /// <author>Marko Lahma (.NET)</author>
    public class ListenerExample : IExample
    {
        public string Name
        {
            get { return GetType().Name; }
        }

        public virtual async Task RunAsync()
        {
            ILog log = LogProvider.GetLogger(typeof (ListenerExample));

            log.Info("------- Initializing ----------------------");

            // First we must get a reference to a scheduler
            ISchedulerFactory sf = new StdSchedulerFactory();
            IScheduler sched = await sf.GetScheduler();

            log.Info("------- Initialization Complete -----------");

            log.Info("------- Scheduling Jobs -------------------");

            // schedule a job to run immediately
            IJobDetail job = JobBuilder.Create<SimpleJob1>()
                .WithIdentity("job1")
                .Build();

            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity("trigger1")
                .StartNow()
                .Build();

            // Set up the listener
            IJobListener listener = new Job1Listener();
            IMatcher<JobKey> matcher = KeyMatcher<JobKey>.KeyEquals(job.Key);
            sched.ListenerManager.AddJobListener(listener, matcher);

            // schedule the job to run
            await sched.ScheduleJobAsync(job, trigger);

            // All of the jobs have been added to the scheduler, but none of the jobs
            // will run until the scheduler has been started
            log.Info("------- Starting Scheduler ----------------");
            await sched.StartAsync();

            // wait 30 seconds:
            // note:  nothing will run
            log.Info("------- Waiting 30 seconds... --------------");

            // wait 30 seconds to show jobs
            await Task.Delay(TimeSpan.FromSeconds(30));
            // executing...

            // shut down the scheduler
            log.Info("------- Shutting Down ---------------------");
            await sched.ShutdownAsync(true);
            log.Info("------- Shutdown Complete -----------------");

            SchedulerMetaData metaData = await sched.GetMetaDataAsync();
            log.Info($"Executed {metaData.NumberOfJobsExecuted} jobs.");
        }
    }
}
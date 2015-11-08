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
using System.Collections.Specialized;
using System.Threading.Tasks;
using Quartz.Impl;
using Quartz.Logging;
using Quartz.Simpl;
using Quartz.Util;

namespace Quartz.Examples.example16
{
    /// <summary> 
    /// This example will show hot to run async jobs.
    /// </summary>
    /// <author>Marko Lahma</author>
    public class AsyncExample : IExample
    {
        public string Name => GetType().Name;

        public virtual async Task RunAsync()
        {
            ILog log = LogProvider.GetLogger(typeof (AsyncExample));

            // First we must get a reference to a scheduler
            // we need to use ClrThreadPool to run async jobs
            var properties = new NameValueCollection
            {
                ["quartz.threadPool.type"] = typeof (ClrThreadPool).AssemblyQualifiedNameWithoutVersion()
            };
            ISchedulerFactory sf = new StdSchedulerFactory(properties);
            IScheduler sched = await sf.GetScheduler();

            log.Info("------- Initialization Complete -----------");

            log.Info("------- Scheduling Jobs -------------------");

            IJobDetail job = JobBuilder
                .CreateForAsync<AsyncJob>()
                .WithIdentity("asyncJob")
                .Build();

            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity("triggerForAsyncJob")
                .StartAt(DateTimeOffset.UtcNow.AddSeconds(1))
                .WithSimpleSchedule(x => x.WithIntervalInSeconds(20).RepeatForever())
                .Build();

            await sched.ScheduleJobAsync(job, trigger);

            log.Info("------- Starting Scheduler ----------------");

            // start the schedule 
            await sched.StartAsync();

            log.Info("------- Started Scheduler -----------------");

            await Task.Delay(TimeSpan.FromSeconds(5));
            log.Info("------- Cancelling job via scheduler.Interrupt() -----------------");
            await sched.InterruptAsync(job.Key);

            log.Info("------- Waiting five minutes... -----------");

            // wait five minutes to give our job a chance to run
            await Task.Delay(TimeSpan.FromMinutes(5));

            // shut down the scheduler
            log.Info("------- Shutting Down ---------------------");
            await sched.ShutdownAsync(true);
            log.Info("------- Shutdown Complete -----------------");
        }
    }
}
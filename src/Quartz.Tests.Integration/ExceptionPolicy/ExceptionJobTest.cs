using System.Threading.Tasks;

using NUnit.Framework;

using Quartz.Impl;
using Quartz.Impl.Triggers;
using Quartz.Spi;

namespace Quartz.Tests.Integration.ExceptionPolicy
{
    [TestFixture]
    public class ExceptionHandlingTest : IntegrationTest
    {
		[SetUp]
		public void SetUp()
		{
			ISchedulerFactory sf = new StdSchedulerFactory();
			sched = sf.GetScheduler().GetAwaiter().GetResult();   
		}

        [Test]
        public async Task ExceptionJobUnscheduleFiringTrigger()
        {
            await sched.StartAsync();
            string jobName = "ExceptionPolicyUnscheduleFiringTrigger";
            string jobGroup = "ExceptionPolicyUnscheduleFiringTriggerGroup";
            JobDetailImpl myDesc = new JobDetailImpl(jobName, jobGroup, typeof (ExceptionJob));
            myDesc.Durable = true;
            await sched.AddJobAsync(myDesc, false);
            string trigGroup = "ExceptionPolicyFiringTriggerGroup";
            IOperableTrigger trigger = new CronTriggerImpl("trigName", trigGroup, "0/2 * * * * ?");
            trigger.JobKey = new JobKey(jobName, jobGroup);

            ExceptionJob.ThrowsException = true;
            ExceptionJob.LaunchCount = 0;
            ExceptionJob.Refire = false;
            ExceptionJob.UnscheduleFiringTrigger = true;
            ExceptionJob.UnscheduleAllTriggers = false;

            await sched.ScheduleJobAsync(trigger);

            await Task.Delay(7*1000);
            await sched.DeleteJobAsync(trigger.JobKey);
            Assert.AreEqual(1, ExceptionJob.LaunchCount,
                            "The job shouldn't have been refired (UnscheduleFiringTrigger)");


            ExceptionJob.LaunchCount = 0;
            ExceptionJob.UnscheduleFiringTrigger = true;
            ExceptionJob.UnscheduleAllTriggers = false;

            await sched.AddJobAsync(myDesc, false);
            trigger = new CronTriggerImpl("trigName", trigGroup, "0/2 * * * * ?");
            trigger.JobKey = new JobKey(jobName, jobGroup);
            await sched.ScheduleJobAsync(trigger);
            trigger = new CronTriggerImpl("trigName1", trigGroup, "0/3 * * * * ?");
            trigger.JobKey = new JobKey(jobName, jobGroup);
            await sched.ScheduleJobAsync(trigger);
            await Task.Delay(7*1000);
            await sched.DeleteJobAsync(trigger.JobKey);
            Assert.AreEqual(2, ExceptionJob.LaunchCount,
                            "The job shouldn't have been refired(UnscheduleFiringTrigger)");
        }

        [Test]
        public async Task ExceptionPolicyRestartImmediately()
        {
            await sched.StartAsync();
            JobKey jobKey = new JobKey("ExceptionPolicyRestartJob", "ExceptionPolicyRestartGroup");
            IJobDetail exceptionJob = JobBuilder.Create<ExceptionJob>()
                .WithIdentity(jobKey)
                .StoreDurably()
                .Build();

            await sched.AddJobAsync(exceptionJob, false);

            ExceptionJob.ThrowsException = true;
            ExceptionJob.Refire = true;
            ExceptionJob.UnscheduleAllTriggers = false;
            ExceptionJob.UnscheduleFiringTrigger = false;
            ExceptionJob.LaunchCount = 0;
            await sched.TriggerJobAsync(jobKey);

            int i = 10;
            while ((i > 0) && (ExceptionJob.LaunchCount <= 1))
            {
                i--;
                await Task.Delay(200);
                if (ExceptionJob.LaunchCount > 1)
                {
                    break;
                }
            }
            // to ensure job will not be refired in consequent tests
            // in fact, it would be better to have a separate class
            ExceptionJob.ThrowsException = false;

            await Task.Delay(1000); 
            await sched.DeleteJobAsync(jobKey);
            await Task.Delay(1000);
            Assert.Greater(ExceptionJob.LaunchCount, 1, "The job should have been refired after exception");
        }

        [Test]
        public async Task ExceptionPolicyNoRestartImmediately()
        {
            await sched.StartAsync();
            JobKey jobKey = new JobKey("ExceptionPolicyNoRestartJob", "ExceptionPolicyNoRestartGroup");
            JobDetailImpl exceptionJob = new JobDetailImpl(jobKey.Name, jobKey.Group, typeof (ExceptionJob));
            exceptionJob.Durable = true;
            await sched.AddJobAsync(exceptionJob, false);

            ExceptionJob.ThrowsException = true;
            ExceptionJob.Refire = false;
            ExceptionJob.UnscheduleAllTriggers = false;
            ExceptionJob.UnscheduleFiringTrigger = false;
            ExceptionJob.LaunchCount = 0;
            await sched.TriggerJobAsync(jobKey);

            int i = 10;
            while ((i > 0) && (ExceptionJob.LaunchCount <= 1))
            {
                i--;
                await Task.Delay(200);
                if (ExceptionJob.LaunchCount > 1)
                {
                    break;
                }
            }
            await sched.DeleteJobAsync(jobKey);
            Assert.AreEqual(1, ExceptionJob.LaunchCount, "The job should NOT have been refired after exception");
        }
    }
}
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
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading.Tasks;

using Quartz.Logging;
using Quartz.Spi;
using Quartz.Util;

namespace Quartz.Impl.AdoJobStore
{
    ///<summary>
    /// <see cref="JobStoreCMT" /> is meant to be used in an application-server
    /// or other software framework environment that provides 
    /// container-managed-transactions. No commit / rollback will be handled by this class.
    /// </summary>
    /// <remarks>
    /// If you need commit / rollback, use <see cref="JobStoreTX" />
    /// instead.
    /// </remarks>
    /// <author><a href="mailto:jeff@binaryfeed.org">Jeffrey Wescott</a></author>
    /// <author>James House</author>
    /// <author>Srinivas Venkatarangaiah</author>
    /// <author>Marko Lahma (.NET)</author>
    public class JobStoreCMT : JobStoreSupport
    {
        /// <summary>
        /// Instructs this job store whether connections should be automatically opened.
        /// </summary>
        public virtual bool OpenConnection { protected get; set; }

        /// <summary>
        /// Called by the QuartzScheduler before the <see cref="IJobStore"/> is
        /// used, in order to give the it a chance to Initialize.
        /// </summary>
        /// <param name="loadHelper"></param>
        /// <param name="signaler"></param>
        public override Task InitializeAsync(ITypeLoadHelper loadHelper, ISchedulerSignaler signaler)
        {
            if (LockHandler == null)
            {
                // If the user hasn't specified an explicit lock handler, 
                // then we ///must/// use DB locks with CMT...
                UseDBLocks = true;
            }

            base.InitializeAsync(loadHelper, signaler);

            Log.Info("JobStoreCMT initialized.");
            return TaskUtil.CompletedTask;
        }

        /// <summary>
        /// Called by the QuartzScheduler to inform the <see cref="IJobStore" /> that
        /// it should free up all of it's resources because the scheduler is
        /// shutting down.
        /// </summary>
        public override async Task ShutdownAsync()
        {

            await base.ShutdownAsync().ConfigureAwait(false);

            try
            {
                ConnectionManager.Shutdown(DataSource);
            }
            catch (SqlException sqle)
            {
                Log.WarnException("Database connection shutdown unsuccessful.", sqle);
            }
        }

        /// <summary>
        /// Gets the non managed TX connection.
        /// </summary>
        /// <returns></returns>
        protected override ConnectionAndTransactionHolder GetNonManagedTXConnection()
        {
            DbConnection conn;
            try
            {
                conn = ConnectionManager.GetConnection(DataSource);
                if (OpenConnection)
                {
                    conn.Open();
                }
            }
            catch (SqlException sqle)
            {
                throw new JobPersistenceException(
                    $"Failed to obtain DB connection from data source '{DataSource}': {sqle}", sqle);
            }
            catch (Exception e)
            {
                throw new JobPersistenceException(
                    $"Failed to obtain DB connection from data source '{DataSource}': {e}", e);
            }

            if (conn == null)
            {
                throw new JobPersistenceException(
                    $"Could not get connection from DataSource '{DataSource}'");
            }

            return new ConnectionAndTransactionHolder(conn, null);
        }

        /// <summary>
        /// Execute the given callback having optionally acquired the given lock.  
        /// Because CMT assumes that the connection is already part of a managed
        /// transaction, it does not attempt to commit or rollback the 
        /// enclosing transaction.
        /// </summary>
        /// <seealso cref="JobStoreSupport.ExecuteInNonManagedTXLockAsync" />
        /// <seealso cref="JobStoreSupport.ExecuteInLockAsync" />
        /// <seealso cref="JobStoreSupport.GetNonManagedTXConnection()" />
        /// <seealso cref="JobStoreSupport.GetConnection()" />
        /// <param name="lockName">
        /// The name of the lock to acquire, for example 
        /// "TRIGGER_ACCESS".  If null, then no lock is acquired, but the
        /// txCallback is still executed in a transaction.
        /// </param>
        /// <param name="txCallback">Callback to execute.</param>
        protected override async Task<T> ExecuteInLockAsync<T>(
                string lockName,
                Func<ConnectionAndTransactionHolder, Task<T>> txCallback)
        {
            bool transOwner = false;
            ConnectionAndTransactionHolder conn = null;
            try
            {
                if (lockName != null)
                {
                    // If we aren't using db locks, then delay getting DB connection 
                    // until after acquiring the lock since it isn't needed.
                    if (LockHandler.RequiresConnection)
                    {
                        conn = GetNonManagedTXConnection();
                    }

                    transOwner = await LockHandler.ObtainLockAsync(DbMetadata, conn, lockName).ConfigureAwait(false);
                }

                if (conn == null)
                {
                    conn = GetNonManagedTXConnection();
                }

                return await txCallback(conn).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await ReleaseLockAsync(LockTriggerAccess, transOwner).ConfigureAwait(false);
                }
                finally
                {
                    CleanupConnection(conn);
                }
            }
        }
    }
}

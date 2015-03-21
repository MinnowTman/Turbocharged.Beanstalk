﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Turbocharged.Beanstalk
{
    /// <summary>
    /// Provides methods useful for inserting jobs into Beanstalk.
    /// </summary>
    public interface IServer
    {
        /// <summary>
        /// Retrieves statistics about the connected server.
        /// </summary>
        Task<Statistics> ServerStatisticsAsync();

        /// <summary>
        /// Returns statistics about a specified job.
        /// </summary>
        Task<JobStatistics> JobStatisticsAsync(int id);

        /// <summary>
        /// Retrieves statistics about the specified tube.
        /// </summary>
        Task<TubeStatistics> TubeStatisticsAsync(string tube);
    }
}
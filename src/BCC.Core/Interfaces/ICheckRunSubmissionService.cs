﻿using System.Threading.Tasks;
using BCC.Core.Model.GitHub;

namespace BCC.Core.Interfaces
{
    /// <summary>
    /// This service provides functionality to submit check run data to GitHub.
    /// </summary>
    public interface ICheckRunSubmissionService
    {
        /// <summary>
        /// Analyzes a binary log file and submits it's findings to GitHub.
        /// </summary>
        /// <param name="owner">The name of the owner of the repository.</param>
        /// <param name="repository">The name of the repository.</param>
        /// <param name="sha">The sha this build is for.</param>
        /// <param name="resourcePath">The path to the binary log file being processing.</param>
        /// <returns>A CheckRun object</returns>
        Task<CheckRun> SubmitAsync(string owner, string repository, string sha, string resourcePath);
    }
}
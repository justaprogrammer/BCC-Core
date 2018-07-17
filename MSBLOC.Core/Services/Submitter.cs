﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MSBLOC.Core.Interfaces;
using MSBLOC.Core.Model;
using Octokit;

namespace MSBLOC.Core.Services
{
    public class Submitter : ISubmitter
    {
        private ICheckRunsClient CheckRunsClient { get; }
        private ILogger<Submitter> Logger { get; }

        public Submitter(ICheckRunsClient checkRunsClient,
            ILogger<Submitter> logger = null)
        {
            CheckRunsClient = checkRunsClient;
            Logger = logger ?? new NullLogger<Submitter>();
        }

        public async Task<CheckRun> SubmitCheckRun(string owner, string name, string headSha,
            string checkRunName, ParsedBinaryLog parsedBinaryLog, string checkRunTitle, string checkRunSummary,
            DateTimeOffset? completedAt)
        {

            var newCheckRunAnnotations = new List<NewCheckRunAnnotation>();
            newCheckRunAnnotations.AddRange(parsedBinaryLog.Errors.Select(args => NewCheckRunAnnotation(
                args.File,
                BlobHref(owner, name, headSha, args.File),
                args.LineNumber,
                args.EndLineNumber,
                CheckWarningLevel.Failure,
                args.Message,
                args.Code)));

            newCheckRunAnnotations.AddRange(parsedBinaryLog.Warnings.Select(args => NewCheckRunAnnotation(
                args.File,
                BlobHref(owner, name, headSha, args.File),
                args.LineNumber,
                args.EndLineNumber,
                CheckWarningLevel.Warning,
                args.Message,
                args.Code)));

            var newCheckRun = new NewCheckRun(checkRunName, headSha)
            {
                Output = new NewCheckRunOutput(checkRunTitle, checkRunSummary)
                {
                    Annotations = newCheckRunAnnotations
                },
                Status = CheckStatus.Completed,
                CompletedAt = completedAt,
                Conclusion = parsedBinaryLog.Errors.Any() ? CheckConclusion.Failure : CheckConclusion.Success
            };

            return await CheckRunsClient.Create(owner, name, newCheckRun);
        }

        private static string BlobHref(string owner, string repository, string sha, string file)
        {
            return $"https://github.com/{owner}/{repository}/blob/{sha}/{file}";
        }

        private static NewCheckRunAnnotation NewCheckRunAnnotation(string filename, string blobHref, int lineNumber,
            int endLine, CheckWarningLevel checkWarningLevel, string message, string title)
        {
            if (endLine == 0)
            {
                endLine = lineNumber;
            }

            return new NewCheckRunAnnotation(filename, blobHref, lineNumber,
                endLine, checkWarningLevel, message)
            {
                Title = title
            };
        }
    }
}
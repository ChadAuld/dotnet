namespace StackExchange.Profiling.RavenDb
{
    using System;
    using System.Text;
    using System.Text.RegularExpressions;
    using Raven.Client.Document;
    using Raven.Client.Connection.Profiling;

    public class MiniProfilerRaven
    {
        private static readonly Regex IndexQueryPattern = new Regex(@"/indexes/[A-Za-z/]+");

        private static Timing _previousHeadTiming;
        private static Guid? _previousDocumentSessionId;
        private const int SessionCountSeed = 2;
        private const string BaseTimingName = "raven";
        private static int _sessionCountForCurrentHead = 0;

        /// <summary>
        /// Initialize MiniProfilerRaven for the given DocumentStore (only call once!)
        /// </summary>
        /// <param name="store">The <see cref="DocumentStore"/> to attach to</param>
        public static void InitializeFor(DocumentStore store)
        {

            if (store != null && store.JsonRequestFactory != null)
                store.JsonRequestFactory.LogRequest += IncludeTiming;

        }

        private static void IncludeTiming(object sender, RequestResultArgs request)
        {
            if (MiniProfiler.Current == null || MiniProfiler.Current.Head == null)
                return;

            var formattedRequest = JsonFormatter.FormatRequest(request);

            string timingName = BaseTimingName;

            var profInfo = (IHoldProfilingInformation)sender;

            if (_previousHeadTiming == null)
                _previousHeadTiming = MiniProfiler.Current.Head;
            if (_previousDocumentSessionId == null)
                _previousDocumentSessionId = profInfo.ProfilingInformation.Id;

            var currentHeadTiming = MiniProfiler.Current.Head;
            var currentDocumentSessionId = profInfo.ProfilingInformation.Id;

            bool sameTiming = _previousHeadTiming.Equals(currentHeadTiming);
            bool differentDocumentSession = _previousDocumentSessionId != currentDocumentSessionId;

            if (sameTiming && differentDocumentSession)
            {
                int currentSessionCount = SessionCountSeed + _sessionCountForCurrentHead;
                timingName += ".s" + currentSessionCount;
                _sessionCountForCurrentHead++;
            }
            else if (!_previousHeadTiming.Equals(currentHeadTiming))
            {
                _sessionCountForCurrentHead = 0;
                timingName = BaseTimingName;
            }

            _previousHeadTiming = currentHeadTiming;
            _previousDocumentSessionId = currentDocumentSessionId;

            MiniProfiler.Current.Head.AddCustomTiming(timingName, new CustomTiming(MiniProfiler.Current, BuildCommandString(formattedRequest))
            {
                Id = Guid.NewGuid(),
                DurationMilliseconds = (decimal)formattedRequest.DurationMilliseconds,
                FirstFetchDurationMilliseconds = (decimal)formattedRequest.DurationMilliseconds,
                ExecuteType = formattedRequest.Status.ToString()
            });
        }

        private static string BuildCommandString(RequestResultArgs request)
        {
            var url = request.Url;

            var commandTextBuilder = new StringBuilder();

            // Basic request information
            // HTTP GET - 200 (Cached)
            commandTextBuilder.AppendFormat("HTTP {0} - {1} ({2})\n",
                request.Method,
                request.HttpResult,
                request.Status);

            // Request URL
            commandTextBuilder.AppendFormat("{0}\n\n", FormatUrl(url));

            // Append query
            var query = FormatQuery(url);
            if (!String.IsNullOrWhiteSpace(query))
            {
                commandTextBuilder.AppendFormat("{0}\n\n", query);
            }

            // Append POSTed data, if any (multi-get, PATCH, etc.)
            if (!String.IsNullOrWhiteSpace(request.PostedData))
            {
                commandTextBuilder.Append(request.PostedData);
            }

            // Set the command string to a formatted string
            return commandTextBuilder.ToString();
        }

        private static string FormatUrl(string requestUrl)
        {
            var results = requestUrl.Split('?');

            if (results.Length > 0)
            {
                return results[0];
            }

            return String.Empty;
        }

        private static string FormatQuery(string url)
        {
            var results = url.Split('?');

            if (results.Length > 1)
            {
                string[] items = results[1].Split('&');
                string query = String.Join("\r\n", items).Trim();

                var match = IndexQueryPattern.Match(results[0]);
                if (match.Success)
                {
                    string index = match.Value.Replace("/indexes/", "");

                    if (!String.IsNullOrEmpty(index))
                        query = String.Format("index={0}\r\n", index) + query;
                }

                return Uri.UnescapeDataString(Uri.UnescapeDataString(query));
            }

            return String.Empty;
        }

    }
}
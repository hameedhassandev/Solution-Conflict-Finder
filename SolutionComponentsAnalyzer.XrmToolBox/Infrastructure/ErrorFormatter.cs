using System;
using System.Collections.Generic;
using System.Linq;

namespace SolutionComponentsAnalyzer.XrmToolBox
{
    internal static class ErrorFormatter
    {
        /// <summary>Unwraps AggregateException and joins the inner messages so the real cause is visible.</summary>
        public static string GetMessage(Exception exception)
        {
            var aggregate = exception as AggregateException;
            if (aggregate != null)
                exception = aggregate.Flatten().InnerExceptions.FirstOrDefault() ?? exception;

            var parts = new List<string>();
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (!string.IsNullOrWhiteSpace(current.Message) && !parts.Contains(current.Message))
                    parts.Add(current.Message);
            }

            return string.Join(Environment.NewLine + "  -> ", parts);
        }
    }
}

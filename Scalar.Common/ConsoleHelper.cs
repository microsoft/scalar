using System;
using System.IO;
using System.Threading;

namespace Scalar.Common
{
    public static class ConsoleHelper
    {
        public enum ActionResult
        {
            Success,
            CompletedWithErrors,
            Failure,
        }

        public static bool ShowStatusWhileRunning(
            Func<bool> action,
            string message,
            TextWriter output)
        {
            Func<ActionResult> actionResultAction =
                () =>
                {
                    return action() ? ActionResult.Success : ActionResult.Failure;
                };

            ActionResult result = ShowStatusWhileRunning(
                actionResultAction,
                message,
                output);

            return result == ActionResult.Success;
        }

        public static ActionResult ShowStatusWhileRunning(
            Func<ActionResult> action,
            string message,
            TextWriter output)
        {
            ActionResult result = ActionResult.Failure;
            bool initialMessageWritten = false;

            try
            {

                output.WriteLine(message + "...");
                initialMessageWritten = true;
                result = action();
            }
            finally
            {
                switch (result)
                {
                    case ActionResult.Success:
                        break;

                    case ActionResult.CompletedWithErrors:
                        if (!initialMessageWritten)
                        {
                            output.Write("\r{0}...", message);
                        }

                        output.WriteLine("Completed with errors.");
                        break;

                    case ActionResult.Failure:
                        if (!initialMessageWritten)
                        {
                            output.Write("\r{0}...", message);
                        }

                        output.WriteLine("Failed");
                        break;
                }
            }

            return result;
        }
    }
}

using System;
using System.Threading.Tasks;

namespace Sbs20.Extensions
{
    public static class TaskExtensions
    {
        // Credit: http://stackoverflow.com/a/26006041/1229065
        public static async Task<TResult> WithTimeout<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            if (task == await Task.WhenAny(task, Task.Delay(timeout)))
            {
                return await task;
            }

            throw new TimeoutException();
        }

        public static bool IsInFinalState(this Task task)
        {
            return task.Status == TaskStatus.Faulted ||
                task.Status == TaskStatus.RanToCompletion ||
                task.Status == TaskStatus.Canceled;
        }
    }
}

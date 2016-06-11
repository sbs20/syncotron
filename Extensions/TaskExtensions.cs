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
    }
}

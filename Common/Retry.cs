using log4net;
using System;
using System.Threading.Tasks;

namespace Sbs20.Common
{
    public static class Retry
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Retry));

        public static async Task On<TException>(this Func<Task> f, int retries) where TException : Exception
        {
            for (int attempt = 0; attempt < retries; attempt++)
            {
                try
                {
                    await f();
                }
                catch (TException ex)
                {
                    if (attempt < retries - 1)
                    {
                        log.WarnFormat("Exception {0} ... retrying", ex.Message);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }
    }
}

using System;

namespace Raven.Tests.Helpers.Util
{
    public class CommonInitializationUtil
    {
        public static void Initialize()
        {
            FailFirstTimeOnly();
        }

        private static void FailFirstTimeOnly()
        {
            try
            {
                new Uri("http://fail/first/time?only=%2bplus");
            }
            catch (Exception)
            {
            }
        }
    }
}

namespace Sample.Wcf
{
    /// <summary>
    /// The WCF common.
    /// </summary>
    public static class WcfCommon
    {
        /// <summary>
        /// The _connection string.
        /// </summary>
        private static string connectionString;

        /// <summary>
        /// Gets the connection string.
        /// </summary>
        public static string ConnectionString
        {
            get
            {
                if (connectionString == null)
                    connectionString = "Data Source = " + System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/TestMiniProfiler.sqlite");
                    
                return connectionString;
            }
        }
    }
}
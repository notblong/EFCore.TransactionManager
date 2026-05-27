namespace TransactionManager.IntegrationTests.Seeding
{
    public sealed class SeedResult
    {
        private SeedResult(bool succeeded, Exception? exception)
        {
            Succeeded = succeeded;
            Exception = exception;
        }

        public bool Succeeded { get; }

        public Exception? Exception { get; }

        public static SeedResult Success()
        {
            return new SeedResult(true, null);
        }

        public static SeedResult Failure(Exception exception)
        {
            return new SeedResult(false, exception);
        }
    }
}

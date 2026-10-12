namespace KarateTournamentApp.Models
{
    internal static class LocalIdGenerator
    {
        private static long _lastId;

        public static int Next()
        {
            var id = Interlocked.Increment(ref _lastId);
            if (id > int.MaxValue)
            {
                throw new InvalidOperationException("The local tournament ID range has been exhausted.");
            }

            return (int)id;
        }

        public static void Reserve(int id)
        {
            if (id <= 0)
            {
                return;
            }

            long current;
            do
            {
                current = Volatile.Read(ref _lastId);
                if (current >= id)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref _lastId, id, current) != current);
        }
    }
}

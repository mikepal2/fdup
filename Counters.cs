using System.Diagnostics;
using System.Globalization;

static class Counters
{
    public static readonly Stopwatch Timer = Stopwatch.StartNew();

    public static long TotalFiles = 0;
    public static long HashQueue = 0;
    public static long HashedFilesPartial = 0;
    public static long HashedFilesFull = 0;
    public static long HashedSize = 0;
    public static long Errors = 0;
    public static long UniqueFiles = 0;
    public static long Hardlinks = 0;
    public static long HardlinkChecks = 0;

    public static void Log(bool newline = true)
    {
        Console.Write($"Files total:{Interlocked.Read(ref TotalFiles)} unique:{Interlocked.Read(ref UniqueFiles)} hardlink_checks:{Interlocked.Read(ref HardlinkChecks)} hardlinks: {Interlocked.Read(ref Hardlinks)} hash_queue:{Interlocked.Read(ref HashQueue)} errors:{Interlocked.Read(ref Errors)}, " +
            $"Hashed partial:{Interlocked.Read(ref HashedFilesPartial).ToString("#,0", CultureInfo.InvariantCulture)} full:{Interlocked.Read(ref HashedFilesFull).ToString("#,0", CultureInfo.InvariantCulture)} size:{Interlocked.Read(ref HashedSize).ToString("#,0", CultureInfo.InvariantCulture)}, " + 
            $"Elapsed:{Timer.Elapsed}\r");
        if (newline)
            Console.WriteLine();
    }
}


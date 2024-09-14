using System.Security.Cryptography;

public class HashUtils
{
    public static readonly string HASH_ERROR_KEY = "<ERROR>";

    //public static readonly SemaphoreSlim HashSemaphore = new SemaphoreSlim(8,8);

    public static async ValueTask<string> CalcHashAsyncSHA256(FileInfo info, long sizeLimit = 0)
    {
        string result;
        if (info.Length == 0)
        {
            result = string.Empty;
        }
        else
        {
            //await HashSemaphore.WaitAsync();
            try
            {
                SHA256 sha256 = SHA256.Create();
                byte[] hash;
                using var fs = new FileStream(info.FullName, mode: FileMode.Open, access: FileAccess.Read, share: FileShare.ReadWrite, bufferSize: 0, useAsync: true);
                if (sizeLimit > 0)
                {
                    byte[] buffer = new byte[Math.Min(sizeLimit, info.Length)];
                    var c = await fs.ReadAsync(buffer);
                    hash = sha256.ComputeHash(buffer, 0, c);
                    Interlocked.Add(ref Counters.HashedSize, c);
                    Interlocked.Increment(ref Counters.HashedFilesPartial);
                }
                else
                {
                    hash = await sha256.ComputeHashAsync(fs);
                    Interlocked.Add(ref Counters.HashedSize, info.Length);
                    Interlocked.Increment(ref Counters.HashedFilesFull);
                }
                result = Convert.ToBase64String(hash);
            }
            catch
            {
                Interlocked.Increment(ref Counters.Errors);
                result = HASH_ERROR_KEY;
            }
            finally
            {
                //HashSemaphore.Release();
            }
        }

        return result;
    }
}


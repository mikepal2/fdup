using System.Runtime.Versioning;

public static class FileUtils
{
    public static async Task<IEnumerable<IGrouping<TKey, FileInfo>>> GroupDuplicatesBy<TKey>(IEnumerable<FileInfo> files, Func<FileInfo, ValueTask<TKey>> keySelector, Func<IEnumerable<FileInfo>, IEnumerable<FileInfo>>? filter = null)
    {
        // prefilter files collection
        files = filter != null ? filter(files) : files;

        // calculate hashes
        var fileHashes = await Task.WhenAll(files.Select(async fileinfo => {
            Interlocked.Increment(ref Counters.HashQueue);
            var hash = await keySelector(fileinfo);
            Interlocked.Decrement(ref Counters.HashQueue);
            return new { fileinfo, hash };
        }
        ));

        // group by hashes
        var groups = fileHashes.GroupBy(f => f.hash, f => f.fileinfo).ToList();

        Interlocked.Add(ref Counters.UniqueFiles, groups.Count(g => g.Count() == 1));
        
        var duplicates = groups.Where(g => g.Count() > 1);
        return duplicates;
    }

    public static async Task<IEnumerable<IGrouping<string, FileInfo>>> GroupDuplicatesByHash(IEnumerable<IEnumerable<FileInfo>> fileCollections, long sizeLimit = 0, Func<IEnumerable<FileInfo>, IEnumerable<FileInfo>>? filter = null)
    {
        var groups = await GroupDuplicatesBy(fileCollections, info => HashUtils.CalcHashAsyncSHA256(info, sizeLimit), filter);
        groups = groups.Where(g => g.Key != HashUtils.HASH_ERROR_KEY); // remove files where we cannot calculate hash (no acess or sharing violation)
        return groups;
    }

    public static async Task<IEnumerable<IGrouping<string, FileInfo>>> GroupDuplicatesBy(IEnumerable<IEnumerable<FileInfo>> fileCollections, Func<FileInfo, ValueTask<string>> keySelector, Func<IEnumerable<FileInfo>, IEnumerable<FileInfo>>? filter = null)
    {
        var groups = await Task.WhenAll(fileCollections.AsParallel().Select(async g => await GroupDuplicatesBy(g, keySelector, filter)));
        return groups.SelectMany(g => g); // flatten subgroups
    }

    // iterate through directories skipping subdirectories where user has no access
    // also, skips reparse points (symlinks) because they should not be counted as duplicates
    public static IEnumerable<FileInfo> EnumerateFilesSafe(DirectoryInfo path, string searchPattern, SearchOption searchOpt)
    {
        var files = Enumerable.Empty<FileInfo>();
        try
        {
            files = files.Concat(path.EnumerateFiles(searchPattern));
        }
        catch (UnauthorizedAccessException)
        {
        }

        foreach (var file in files.Where(f => !f.Attributes.HasFlag(FileAttributes.ReparsePoint)))
        {
            Interlocked.Increment(ref Counters.TotalFiles);
            yield return file;
        }

        if (searchOpt == SearchOption.AllDirectories)
        {
            IEnumerable<DirectoryInfo> dirs;
            try
            {
                dirs = path.EnumerateDirectories().Where(d => !d.Attributes.HasFlag(FileAttributes.ReparsePoint));
            }
            catch (UnauthorizedAccessException)
            {
                yield break;
            }

            foreach (var file in dirs.SelectMany(d => EnumerateFilesSafe(d, searchPattern, searchOpt)))
                yield return file;
        }
    }

    [SupportedOSPlatform("Windows")]
    public static IEnumerable<FileInfo> ExcludeHardlinks(IEnumerable<FileInfo> e)
    {
        int countBefore = 0;
        var filtered = e.DistinctBy(f => { 
            countBefore++; 
            return Win32Api.HardLinkHelper.GetFileFirstHardLink(f.FullName); 
        }).ToList();
        Interlocked.Add(ref Counters.Hardlinks, countBefore - filtered.Count);
        return filtered;
    }

    // result contains collection of files grouped by hash
    public static async Task<IEnumerable<IGrouping<string, FileInfo>>> FindDuplicateFiles(string path, bool includeHardlinks)
    {
        const long partialHashSize = 4096;

        // get all files on the path
        var allFiles = await Task.Run(() => EnumerateFilesSafe(new DirectoryInfo(path), "*", SearchOption.AllDirectories));

        // group files by size
#pragma warning disable CS1998 // async labda has no await and will be executed synchronously
        var fileGroups = await Task.Run(() => GroupDuplicatesBy(allFiles, async f => f.Length));
#pragma warning restore CS1998

       // group files by partial hash, also exclude hardlinked files
        var fileGroups2 = (await GroupDuplicatesByHash(fileGroups, sizeLimit: partialHashSize, 
            filter: OperatingSystem.IsWindows() && !includeHardlinks ? ExcludeHardlinks : null)).ToList();

        var smallFiles = fileGroups2.Where(g => g.First().Length <= partialHashSize);
        var largeFiles = fileGroups2.Where(g => g.First().Length > partialHashSize);

        // for large files with matched partial hash we must verify full hash
        var duplicates = smallFiles.Concat(await GroupDuplicatesByHash(largeFiles));

        return duplicates;
    }

}


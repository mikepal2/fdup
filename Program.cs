using Newtonsoft.Json;
using SnapCLI;

internal class Program
{
    public enum OutputFormat
    {
        Text,
        JSON
    }

    [RootCommand(description: "Search for duplicate files in specified directory")]
    public static async ValueTask<int> FindDuplicateFiles(
        [Argument(description: "Directory to search for duplicate files")]
        DirectoryInfo path,

        [Option(name: "out", aliases: ["o"], helpName: "filepath", description: "Output file path")]
        FileInfo? outFile = null,

        [Option(aliases: ["f"], description: "Set output format")]
        OutputFormat format = OutputFormat.Text,

        [Option(name: "hardlinks", aliases: ["h"], description: "Include hardlinks")]
        bool includeHardlinks = false
        )
    {
        // create async task
        var findDuplicatesTask = FileUtils.FindDuplicateFiles(path.FullName, includeHardlinks);

        // report counters wile task is running
        while (!findDuplicatesTask.IsCompleted)
        {
            // report progress
            Counters.Log(newline: false);

            await Task.WhenAny([findDuplicatesTask, Task.Delay(100)]);
        }

        // get results
        var duplicateFiles = await findDuplicatesTask;

        Counters.Log();

        // output results to file or console
        using var textWriter = outFile != null ? new StreamWriter(outFile.FullName) : Console.Out;

        switch (format)
        {
            case OutputFormat.JSON:
                new JsonTextWriter(textWriter).WriteValue(duplicateFiles);
                break;
            case OutputFormat.Text:
                foreach (var group in duplicateFiles)
                {
                    textWriter.WriteLine($"Hash: {group.Key}:");
                    foreach (var file in group)
                        textWriter.WriteLine($"\t{file.FullName}");
                }
                break;
            default:
                throw new NotImplementedException();
        }

        // repeat counter after files
        if (outFile == null && duplicateFiles.Any())
            Counters.Log();

        Console.WriteLine($"Duplicates: {duplicateFiles.Sum(g => g.Count())}");

        return 0;
    }
}
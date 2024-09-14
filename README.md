# fdup
Simple tool to find duplicate files on disk. The tool is aware of symbolic links and hardlinks that may point to the same file. 

```
Usage:
  fdup <path> [options]

Arguments:
  <path>  Directory to search for duplicate files

Options:
  -o, --out <filepath>      Output file path
  -f, --format <JSON|Text>  Set output format [default: Text]
  -h, --hardlinks           Include hardlinks [default: False]
  --version                 Show version information
  -?, -h, --help            Show help and usage information
```
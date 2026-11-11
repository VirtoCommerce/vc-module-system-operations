using System;
using System.Globalization;
using System.IO;

namespace VirtoCommerce.SystemOperations.Data.Diagnostics;

// The memory limit of the container the process runs in, from the Linux control group. The GC's own figure is not it:
// in a container the GC limits its heap to 75% of the container's limit by default, and the working set of the whole
// process (native memory, code, stacks) counts against the container's limit, not the heap's.
public static class ContainerMemoryLimit
{
    public const string CgroupV2File = "/sys/fs/cgroup/memory.max";
    public const string CgroupV1File = "/sys/fs/cgroup/memory/memory.limit_in_bytes";

    // cgroup v1 reports "no limit" as the largest page-aligned 64-bit value, 0x7FFFFFFFFFFFF000.
    private const long UnlimitedFrom = 1L << 62;

    // The limit in bytes, or null when there is no control group or it sets no limit.
    public static long? Read(Func<string, string> readFile = null)
    {
        readFile ??= ReadFileOrNull;

        return Parse(readFile(CgroupV2File)) ?? Parse(readFile(CgroupV1File));
    }

    public static long? Parse(string value)
    {
        value = value?.Trim();

        if (string.IsNullOrEmpty(value) || value == "max" ||
            !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) ||
            bytes <= 0 || bytes >= UnlimitedFrom)
        {
            return null;
        }

        return bytes;
    }

    private static string ReadFileOrNull(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

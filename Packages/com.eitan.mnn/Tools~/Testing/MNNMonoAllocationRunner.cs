using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Filters;

// Standalone host-Mono runner; it never starts Unity or reimplements the tests.
internal static class MNNMonoAllocationRunner
{
    private static int Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Provide assembly, case-list and output XML paths.");
        string[] names = File.ReadAllLines(args[1]);
        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        runner.Load(Assembly.LoadFrom(args[0]), new Dictionary<string, object>());
        var filters = new List<TestFilter>();
        foreach (string name in names) filters.Add(new FullNameFilter(name));
        var result = runner.Run(TestListener.NULL, new OrFilter(filters.ToArray()));
        File.WriteAllText(args[2], result.ToXml(true).OuterXml);
        Console.WriteLine("Host Mono runtime=" + Environment.Version + "; passed=" + result.PassCount +
            "; failed=" + result.FailCount + "; skipped=" + result.SkipCount + "; Unity allocation measured=false");
        return result.PassCount == names.Length && result.FailCount == 0 && result.SkipCount == 0 ? 0 : 1;
    }
}

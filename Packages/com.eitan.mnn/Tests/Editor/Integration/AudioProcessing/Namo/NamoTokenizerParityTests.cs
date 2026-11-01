using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class NamoTokenizerParityTests
    {
        [Serializable] private sealed class Row { public string name, text; public int limit; public int[] ids; }
        [Serializable] private sealed class Cases { public Row[] rows; }

        [Test, Explicit("Requires the frozen NAMO Hugging Face tokenizer oracle and prepared runtime tokenizer.")]
        public void TokensMatchOriginalIncludingAddedTokensAndTruncation()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_NAMO_REFERENCE");
            Assert.That(directory, Is.Not.Null.And.Not.Empty, "Set MNN_TEST_NAMO_REFERENCE to the frozen NAMO study directory.");
            Type type = typeof(MNNInterpreter).Assembly.GetType("MNN.Unity.MNNNamoTokenizer");
            Assert.NotNull(type, "NAMO requires an exact Metaspace BPE tokenizer.");
            object tokenizer = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { Path.Combine(directory, "candidate", "tokenizer.txt") }, null);
            MethodInfo encode = type.GetMethod("Encode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(encode);
            Row[] rows = JsonUtility.FromJson<Cases>(File.ReadAllText(Path.Combine(directory, "tokenizer-reference.json"))).rows;
            Assert.That(rows.Length, Is.GreaterThan(800));
            foreach (Row row in rows)
            {
                int[] actual = (int[])encode.Invoke(tokenizer, new object[] { row.text, row.limit });
                CollectionAssert.AreEqual(row.ids, actual, row.name);
            }
            TestContext.WriteLine("Exact original tokenizer cases=" + rows.Length);
        }
    }
}

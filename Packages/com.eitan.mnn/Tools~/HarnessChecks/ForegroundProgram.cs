using System;
using MNN.Unity.Tests;
using MNN.Samples.SiriAI.Tests;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;

internal static class ForegroundProgram
{
    private static int Main()
    {
        try
        {
            var submissions = new SiriAISubmissionJournalTests();
            submissions.LostAdmissionResponseIsReplayedWithTheOriginalIdentityAfterReopen().GetAwaiter().GetResult();
            Console.WriteLine("PASS lost admission reply / reopen / same identity / intentional repeat");
            submissions.PendingIdentityCannotBeReassignedToAnotherObjective().GetAwaiter().GetResult();
            Console.WriteLine("PASS pending identity cannot be reassigned");
            submissions.LocalWriteFailurePreventsTransmissionAndExplicitRejectionDoesNotReplay().GetAwaiter().GetResult();
            Console.WriteLine("PASS durable-before-send and explicit rejection stops replay");
            var events = new ConversationEventTests();
            events.BackgroundResultUsesTheForegroundModelWithoutUserAcceptanceOrPlanning().GetAwaiter().GetResult();
            Console.WriteLine("PASS background result / foreground model / no user turn / no planning");
            events.BackgroundResultWaitsWhileRecordingOrGenerating().GetAwaiter().GetResult();
            Console.WriteLine("PASS background result waits for foreground ownership");
            events.BackgroundSpeechUsesTtsAndRequiresPresentationEvidence().GetAwaiter().GetResult();
            Console.WriteLine("PASS result TTS / queued is not heard / presentation receipts");
            Regression(new AgentReuseTests());
            Regression(new AgentLifetimeTests());
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Regression(object fixture)
    {
        int count = 0;
        foreach (var method in fixture.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.GetCustomAttributes(typeof(TestAttribute), true).Length == 0 || method.GetParameters().Length != 0) continue;
            var result = method.Invoke(fixture, null);
            if (result is Task task) task.GetAwaiter().GetResult();
            ++count;
        }
        Console.WriteLine("PASS " + fixture.GetType().Name + " regression cases: " + count);
    }
}

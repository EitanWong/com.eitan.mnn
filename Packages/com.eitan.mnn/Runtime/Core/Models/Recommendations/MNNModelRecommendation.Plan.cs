using System;
using System.Collections.Generic;
using System.Linq;

namespace MNN.Unity
{
    public static partial class MNNModelRecommendation
    {
        private sealed class PlanCandidate
        {
            internal MNNModelRecommendationResult[] Selections = Array.Empty<MNNModelRecommendationResult>();
            internal long System, Device;
            internal double Score;
            internal string Key => string.Join("|", Selections.Select(item => item.ModelId));
        }

        /// <summary>Bounded joint search; all stages remain resident. Never grants each stage the entire free memory pool.</summary>
        public static MNNModelRecommendationPlan RecommendPlan(MNNModelHardware hardware, IEnumerable<MNNModelWorkload> workloads,
            IEnumerable<MNNModelResourceProfile> profiles, MNNModelRecommendationPolicy policy = null)
        {
            ValidateHardware(hardware);
            if (workloads == null || profiles == null) throw new ArgumentNullException("workloads/profiles");
            policy = policy ?? new MNNModelRecommendationPolicy(); ValidatePolicy(policy);
            var tasks = workloads.ToArray();
            if (tasks.Length == 0 || tasks.Length > 32) throw new ArgumentException("Choose between one and 32 resident stages.");
            foreach (var task in tasks) ValidateWorkload(task);
            var models = profiles.ToArray();
            foreach (var model in models) ValidateProfile(model);
            hardware = hardware.Copy(); policy = policy.Copy();
            tasks = Array.ConvertAll(tasks, item => item.Copy()); models = Array.ConvertAll(models, item => item.Copy());
            var candidates = new List<PlanCandidate> { new PlanCandidate() };
            foreach (var task in tasks)
            {
                var ranked = Rank(hardware, task, models, policy).Where(item => item.Status != MNNModelRecommendationStatus.Rejected).Take(policy.MaximumCandidatesPerTask).ToArray();
                candidates = ExpandPlans(candidates, ranked, SystemBudget(hardware, policy), DeviceBudget(hardware, policy), policy.MaximumPlanCandidates);
                if (candidates.Count == 0) return new MNNModelRecommendationPlan(false, Array.Empty<MNNModelRecommendationResult>(), 0, 0,
                    new[] { "No jointly feasible model plan for " + task.Task + "; inspect Rank reasons, reduce workload, or use a cloud provider." });
            }
            var best = candidates[0];
            var warnings = best.Selections.Where(item => item.Status == MNNModelRecommendationStatus.Provisional)
                .Select(item => item.ModelId + ": resource/performance estimates require device validation.").ToArray();
            return new MNNModelRecommendationPlan(true, best.Selections, best.System, best.Device, warnings);
        }

        private static List<PlanCandidate> ExpandPlans(IEnumerable<PlanCandidate> plans, IEnumerable<MNNModelRecommendationResult> ranked, long systemBudget, long deviceBudget, int limit)
        {
            var next = new List<PlanCandidate>();
            foreach (var plan in plans)
                foreach (var item in ranked)
                {
                    // Subtract before adding so a malicious/invalid giant model cannot wrap the totals.
                    if (item.PeakSystemBytes > systemBudget - plan.System || item.PeakDeviceBytes > deviceBudget - plan.Device) continue;
                    next.Add(new PlanCandidate { Selections = plan.Selections.Concat(new[] { item }).ToArray(),
                        System = plan.System + item.PeakSystemBytes, Device = plan.Device + item.PeakDeviceBytes, Score = plan.Score + item.Score });
                }
            return next.OrderByDescending(item => item.Score).ThenBy(item => item.System).ThenBy(item => item.Key, StringComparer.Ordinal).Take(limit).ToList();
        }
    }
}

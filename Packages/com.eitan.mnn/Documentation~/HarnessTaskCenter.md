# Background task presentation

`HarnessTaskPresentation` in `Runtime/Core/Language/Agent/Harnesses` provides a
Unity-independent projection of task snapshots. Existing namespaces and assembly
names remain stable. Other applications can consume this API without SiriAI UI:

```csharp
var active = HarnessTaskPresentation.Active(dashboard.Tasks);
var row = new HarnessTaskPresentation(task, configuredBackend.Capabilities);
// Localize StatusKey and PauseHintKey; display Title and ProgressText as plain text.
// Enable controls using CanPause, CanResume and CanCancel, plus connection freshness.
```

`Order` puts input/permission requests first, other live tasks next, and finished
tasks last, ordered by their most recent update. `Active` excludes all terminal
states, including failed, interrupted and cancelled tasks. Paused and queued tasks
remain visible so users can resume or cancel them.

The SiriAI sample binds this API to its existing Harness Controller. Its task
management icon belongs to the draggable window/drawer header. Desktop mode shows
compact task pills beside the orb while live tasks exist; finished tasks disappear
from that strip and remain available in the task center. Every row opens an
independent, draggable detail window with an execution timeline, results, task
identity and controls. The expand button enlarges that window. Closing UI never
cancels work. Host/account changes immediately remove the previous scope's UI.

Window open/close and size changes reuse the sample's spring surface animation.
Independent surfaces own separate input groups, so closing one does not disable
other windows. Icons retain tooltips when a narrow layout hides their text.
Animations follow the existing UI animation preference and quality setting.
The header task icon and its sibling shadow join the final startup reveal stage
alongside conversation navigation and settings, so neither appears ahead of the
layered entrance. Disabled motion leaves both fully visible.

Progress is the reported activity and execution record, not a fabricated percentage.
The sample polls every two seconds, shows the last successful sync, and marks stale
or disconnected snapshots. Controls require a successful sync within ten seconds.
Cancellation remains `Cancelling` until the backend confirms it. Pausing is supported
only before execution starts; a running agent exposes a disabled pause control with
the reason. The current Host has no safe general execution-suspension contract.

The Unity tests cover portrait/landscape windows, literal host text, expand,
close-without-cancel, desktop completion removal, host scope cleanup, independent
input during animation, and disabled-motion final states. Snapshot/UI checks do not
constitute real provider, model, operating-system click-through or device validation.

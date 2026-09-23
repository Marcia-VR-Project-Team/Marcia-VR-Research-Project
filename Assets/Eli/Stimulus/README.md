# Stimulus System — Handoff Documentation (v2)

---

## What Is This System?

This is a self-contained Unity system for triggering **animations and sounds** in response to user input or in-game events. It's designed to be configured almost entirely through the Inspector, with no coding required after initial setup.

There are five components you'll work with:

| Component | What It Does |
|---|---|
| `StimulusBase` | Abstract base class. Never added to a GameObject directly — shared logic lives here |
| `AnimationStimulus` | Triggers an animation by flipping a Bool parameter on an Animator |
| `AudioStimulus` | Plays a one-shot audio clip through an AudioSource |
| `StimulusSequence` | Triggers a list of Stimuli one at a time, in order |
| `StimulusActionTrigger` | Connects a Stimulus or StimulusSequence to a Unity Input Action |
| `StimuliCollector` | Stops all active Stimuli and/or Sequences at once — used by the Control Panel |
| `SessionLogger` | Writes all session data to file from a dedicated background thread. Replaces `StimulusLogger` |
| `TrackingSampler` | Samples head and hand pose at a fixed rate |
| `InputEventLogger` | Logs controller button presses as they happen |
| `SceneEventMarker` | Lets scene content log events via UnityEvents or trigger volumes |
| `StimulusLogger` | **Deprecated.** A thin shim kept so old scenes and old `StimulusLogger.Log(...)` calls still work |

> **Core mental model:** `AnimationStimulus` and `AudioStimulus` are the basic units — one does animation, one does audio, and they're intentionally separate so you can stop all audio independently from all animation and vice versa. A `StimulusSequence` chains any mix of them together in order. Everything else is plumbing.

---

## AnimationStimulus [1]

### What It Does

When triggered, `AnimationStimulus`:
1. Reads the current value of a named **Bool parameter** on an Animator
2. Flips it to the opposite value to trigger the animation
3. After the animation finishes (or after a manual delay), flips it back to reset
4. Locks itself during playback so it can't be triggered twice simultaneously

### The Bool Parameter Requirement

This is the most important thing to understand before setting one up. Your Animator Controller **must use a Bool parameter** to drive the animation you want to trigger — not a Trigger, not an int. The component reads and writes the parameter by name using `GetBool`/`SetBool` [1].

The component captures the **idle state of that Bool when the scene starts** [1]. It assumes whatever state the Animator is in at runtime startup is the **untriggered baseline**, and it toggles away from that on trigger and back to it on reset.

> **This means:** if your Animator is already in the triggered state when Play is pressed, the logic will be inverted — triggering will reset it and resetting will trigger it. Always make sure the scene starts with the Animator in its idle state.

### Inspector Fields

**Animation**

- **Animator** — The Animator to control. Can be on a different GameObject.
- **Animation Trigger Parameter Name** — The exact name (case-sensitive) of the Bool parameter in the Animator Controller.
- **Reset After Trigger** — When ON, the system automatically resets the Animator after the animation plays. Turn this OFF only if your Animator resets itself, or if you're calling `ResetAnimation()` manually from elsewhere. If this is OFF, the Stimulus cannot be re-triggered automatically.
- **Reset After Animation Ends** — When ON (default), resets after the animation clip's actual length. When OFF, waits for the **Manual Animation Reset Delay** instead. Note that **Reset After Trigger must also be ON** for any automatic reset to occur.
- **Manual Animation Reset Delay** — A 0–120 second delay used only when "Reset After Animation Ends" is OFF.

**Logging / Debug**

- **Log Activity** — Logs trigger and animation start/stop events via `StimulusLogger`.
- **Print Debug Statements** — Logs granular debug messages to the Unity Console. Useful during setup.

### How to Set One Up

1. Add `AnimationStimulus` to a GameObject in your scene.
2. In your Animator Controller, create a **Bool parameter** and set up a transition that fires when it changes. Make sure the scene starts in the idle/untriggered state.
3. Assign the **Animator** in the Inspector and type the Bool parameter name exactly into **Animation Trigger Parameter Name**.
4. Adjust the reset settings as needed (defaults are fine for most cases).
5. To trigger it, wire `TriggerStimulus()` to a Button's OnClick, or add a `StimulusActionTrigger` (see below).

---

## AudioStimulus [2]

### What It Does

When triggered, `AudioStimulus`:
1. Calls `PlayOneShot` on an AudioSource with a specified AudioClip
2. Locks itself for the duration of the clip
3. Resets automatically once the clip finishes

Both an AudioSource **and** an AudioClip must be assigned — one without the other will produce a warning and the component will disable itself [2].

### Inspector Fields

- **Audio Source** — The AudioSource component to play through. Can be on any GameObject.
- **Audio Clip** — The clip to play when triggered.
- **Log Activity** — Logs audio start/stop events via `StimulusLogger`.
- **Print Debug Statements** — Logs debug messages to the Unity Console.

### How to Set One Up

1. Add `AudioStimulus` to a GameObject.
2. Assign an **AudioSource** (on any GameObject) and an **AudioClip**.
3. Wire `TriggerStimulus()` to a Button's OnClick or add a `StimulusActionTrigger`.

---

## UI Button Integration (Both Stimulus Types)

Both `AnimationStimulus` and `AudioStimulus` inherit UI button integration from `StimulusBase` [5]. If you assign a UI Button in the Inspector, the system will:

- Automatically set the button's title text to the GameObject's name
- Display the assigned Input Action name (if a `StimulusActionTrigger` is present) or "Click Button" if not
- Show a live progress percentage while the Stimulus is playing
- Reset the button text when playback finishes

For this to work, the Button's child GameObjects must have TextMeshPro components tagged correctly:

- One child tagged **`StimulusTitle`** — displays the Stimulus name
- One child tagged **`StimulusText`** — displays the trigger method and progress
- One additional TMP child (any tag) — displays the "Use Action" / "Progress:" label

These tag names can be changed in the Inspector via **Title Tag** and **Input Text Tag** if you have a custom button setup. The defaults will work with the provided Stimulus Button prefab.

---

## StimulusSequence [7]

### What It Does

`StimulusSequence` triggers a list of Stimuli **one at a time, in order**. For each step it:
1. Triggers the Stimulus
2. Waits for that Stimulus's full duration
3. Waits an additional optional **Delay After**
4. Moves to the next step

The sequence works with any mix of `AnimationStimulus` and `AudioStimulus` steps.

### How to Set One Up

1. Make sure all the individual Stimulus components you want to use are already set up and working on their own.
2. Create a new empty GameObject and add `StimulusSequence` to it.
3. In the **Steps** list, click **+** to add a step. Assign a **Stimulus** reference and a **Delay After** value (in seconds) for each step.
4. Trigger it via a Button's OnClick calling `TriggerStimulusSequence()`, or via a `StimulusActionTrigger`.

The StimulusSequence has the same UI button integration as the individual Stimulus types, and its button will show per-step and overall progress while running [7].

---

## StimulusActionTrigger [4]

### What It Does

`StimulusActionTrigger` listens for a Unity Input Action and calls `TriggerStimulus()` or `TriggerStimulusSequence()` when it fires. It **must be on the same GameObject** as either a Stimulus or a StimulusSequence — it checks for both automatically at startup and hooks into whichever it finds [4].

### How to Set It Up

1. Add `StimulusActionTrigger` to the **same GameObject** as your Stimulus or StimulusSequence.
2. Assign an **Input Action Reference** in the Inspector — this is an action defined in your Input Action Asset (a keyboard key, controller button, etc.).
3. That's it. The component wires itself up automatically.

You can use a Button **and** a `StimulusActionTrigger` at the same time — they don't interfere [4].

---

## StimuliCollector [3]

This component is used by the Control Panel prefab and finds all Stimuli and Sequences in the scene automatically on startup. You generally won't need to configure it yourself. Note that it stops all `StimulusSequence` instances when stopping audio or animation, since the StimulusSequence cannot see whether its steps use audio or animation and therefore stopping either means cancelling any running `StimulusSequence` instances since they cannot be paused.

It exposes three methods, *each of which also stops all running sequences*:

- **`StopAllSound()`** — Stops all `AudioStimulus` instances [3]
- **`StopAllAnimations()`** — Stops all `AnimationStimulus` instances [3]
- **`StopAllStimuli()`** — Stops everything [3]

Because `AudioStimulus` and `AnimationStimulus` are separate types, these three operations are genuinely independent — stopping all audio will not interrupt any animations, and vice versa.

Only one `StimuliCollector` can exist in a scene — if a second one appears, it destroys itself [3].

---

## StimulusLogger [6]

`StimulusLogger` runs automatically in the background when present in the scene (placed on the Control Panel prefab). It writes timestamped log entries to a `.txt` file in `Application.persistentDataPath` on a background thread, so it won't affect performance [6].

On application quit, it opens Windows Explorer to the log file location automatically. This can be toggled off via **Open Explorer On Application Exit** [6].

To see logs from a Stimulus, make sure a `StimulusLogger` exists somewhere in the scene and **Log Activity** is enabled on the Stimulus component.

---

## Common Pitfalls

**"My animation triggers inverted / resets when I expect it to trigger"**
The Animator was not in its idle state when the scene started. `AnimationStimulus` captures the idle Bool value at startup and uses it as the baseline. Start the scene with the Animator in its untriggered state [1].

**"My Stimulus won't re-trigger after the first time"**
Check that **Reset After Trigger** is enabled, or that your Animator isn't stuck in a transition. You can call `ResetAnimation()` on an `AnimationStimulus` to force a manual reset.

**"No sound is playing"**
Both an AudioSource **and** an AudioClip must be assigned to `AudioStimulus`. Check the Console for a warning — if either is missing the component disables itself at startup [2].

**"My StimulusActionTrigger isn't doing anything"**
Make sure it's on the **same GameObject** as the Stimulus or StimulusSequence, and that the Input Action Reference is assigned in the Inspector [4].

**"My UI button text isn't updating"**
Check that the Button's child GameObjects are tagged correctly (`StimulusTitle` and `StimulusText`) and contain TMP Text components. Check the Console for warnings from `SetupButton()` [5].

**"StopAllSound is also stopping my animations"**
This would mean an older version of `StimuliCollector` is in the scene. The current version uses separate arrays for `AudioStimulus` and `AnimationStimulus` and only operates on the correct type per method [3].

---

## Quick Reference

| What you want | How to do it |
|---|---|
| Trigger via UI Button | Wire `TriggerStimulus()` to the Button's OnClick |
| Trigger via code | Call `stimulus.TriggerStimulus()` directly |
| Trigger via keypress / controller | Add `StimulusActionTrigger`, assign Input Action Reference |
| Trigger a sequence | Call `TriggerStimulusSequence()` on the StimulusSequence |
| Stop a single Stimulus | Call `StopStimulus()` on it directly |
| Stop a sequence mid-run | Call `StopSequence()` on the StimulusSequence |
| Stop all audio | Call `StopAllSound()` on the StimuliCollector |
| Stop all animation | Call `StopAllAnimations()` on the StimuliCollector |
| Stop everything | Call `StopAllStimuli()` on the StimuliCollector |

---

## Session Logging (v3)

The logger was rebuilt to support synchronising with an Empatica watch. What changed:

- **Everything is UTC.** Timestamps come from `SessionClock`, which anchors to the wall clock
  once at startup and measures from there with a hardware `Stopwatch`. `DateTime.UtcNow` alone
  ticks only about every 15 ms on Windows, which is too coarse to order events within a frame.
- **Three files per session**, in `Application.persistentDataPath`, sharing one session id:
  `_events.csv` (discrete events), `_tracking.csv` (pose samples), `_meta.txt` (session start
  and clock info).
- **Join with Empatica data on the `unix_ms` column.**
- **Fire a sync marker** at the start and end of every session, at the same moment you press the
  tag button on the watch. Bind a controller button to `InputEventLogger`'s *Sync Marker Action*,
  or call `SessionLogger.SyncMarker("start")`. Two markers let you correct for clock drift over
  the session, not just a constant offset.

### Setup

All three components live on the **Stimulus Logger** prefab (`Assets/Eli/Stimulus Logger.prefab`),
so any scene that already has that prefab is set up. Per scene you only need to:

1. Set **Participant Id** on `SessionLogger`.
2. Assign the actions you want recorded on `InputEventLogger`, including the **Sync Marker Action**.

`TrackingSampler` finds the head and hands by itself at runtime, so it needs nothing assigned. If
it cannot find one, it warns in the console *and* writes a `TRACKING_TARGET_MISSING` row, so a
missing hand shows up in the data rather than being discovered after the participant has left.

### A caveat about sample rate

The file writing genuinely runs on its own thread. Reading the poses cannot — Unity's `Transform`
and XR APIs throw off the main thread. So the sampler reads on the main thread and hands off
immediately. Nothing blocks on disk, but **the sample rate cannot exceed the frame rate**: ask for
90 Hz on a 72 Hz headset and you get 72. Every row carries the UTC time it was actually read at,
so treat the rows as irregularly spaced and resample against the timestamp column.

### Logging your own events

```csharp
SessionLogger.Event("TEACHER_LOOKED_AWAY", gameObject.name, "duration=2.4");
```

Safe to call from any thread, and cheap enough for input callbacks and collision handlers. For
designers, `SceneEventMarker` exposes the same thing as inspector-callable methods.

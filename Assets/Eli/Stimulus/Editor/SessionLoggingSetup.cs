using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds the session logging rig to whichever scene is currently open.
///
/// <para>This exists because the logging setup has to be repeated in every scene that gets
/// tested — each team member has their own classroom — and doing it by hand means someone
/// eventually runs a session with a missing receiver and finds out afterwards. It also works
/// while the editor is open, which editing the scene file on disk does not.</para>
/// </summary>
public static class SessionLoggingSetup
{
    /// <summary>
    /// The logger prefab carrying SessionLogger, TrackingSampler and InputEventLogger.
    /// </summary>
    private const string LOGGER_PREFAB_PATH = "Assets/Eli/Stimulus Logger.prefab";

    /// <summary>
    /// Adds a SessionLogger to the open scene if it has none, and puts a
    /// <see cref="TimelineLogReceiver"/> on every PlayableDirector so timeline markers are logged.
    /// </summary>
    [MenuItem("Tools/Session Logging/Set Up In Current Scene")]
    public static void SetUpCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("No scene open", "Open a scene first.", "OK");
            return;
        }

        int loggersAdded = EnsureLogger();
        int receiversAdded = EnsureTimelineReceivers();

        EditorSceneManager.MarkSceneDirty(scene);

        string summary =
            $"Scene '{scene.name}':\n\n" +
            $"• Session Logger: {(loggersAdded > 0 ? "added" : "already present")}\n" +
            $"• Timeline receivers added: {receiversAdded}\n\n" +
            "Save the scene to keep these changes.";

        Debug.Log("[Session Logging] " + summary.Replace("\n", " "));
        EditorUtility.DisplayDialog("Session logging set up", summary, "OK");
    }

    /// <summary>
    /// Instantiates the logger prefab if the scene has no <see cref="SessionLogger"/>.
    /// </summary>
    /// <returns>1 if one was added, 0 if the scene already had one.</returns>
    private static int EnsureLogger()
    {
        if (Object.FindAnyObjectByType<SessionLogger>(FindObjectsInactive.Include) != null)
            return 0;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LOGGER_PREFAB_PATH);
        if (prefab == null)
        {
            Debug.LogError($"[Session Logging] Could not find the logger prefab at {LOGGER_PREFAB_PATH}.");
            return 0;
        }

        // Instantiated as a prefab instance, not a plain copy, so later changes to the prefab
        // still reach every scene that uses it.
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Add Session Logger");

        return 1;
    }

    /// <summary>
    /// Adds a <see cref="TimelineLogReceiver"/> beside every PlayableDirector that lacks one.
    ///
    /// <para>Directors inside prefab instances are handled too — the component is added to the
    /// instance, which Unity records as a prefab override rather than editing the shared prefab,
    /// so one scene gaining logging does not silently change every other scene.</para>
    /// </summary>
    /// <returns>How many receivers were added.</returns>
    private static int EnsureTimelineReceivers()
    {
        PlayableDirector[] directors =
            Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include,
                                                       FindObjectsSortMode.None);

        int added = 0;

        foreach (PlayableDirector director in directors)
        {
            if (director.GetComponent<TimelineLogReceiver>() != null) continue;

            Undo.AddComponent<TimelineLogReceiver>(director.gameObject);
            added++;

            Debug.Log($"[Session Logging] Added TimelineLogReceiver to '{director.name}'.", director);
        }

        if (directors.Length == 0)
            Debug.LogWarning("[Session Logging] No PlayableDirector found in this scene, so no " +
                             "timeline events will be logged here.");

        return added;
    }

    /// <summary>
    /// Puts a <see cref="TimelineStart"/> on every PlayableDirector and wires its Director field,
    /// so the timeline can be started with the spacebar.
    ///
    /// <para>The director has Play On Awake off, which is the right choice for a session — you
    /// want the lecture to begin once the participant is settled, not while the scene is still
    /// loading. But that means something has to call Play(), and nothing in these scenes did.</para>
    ///
    /// <para>Wiring the Director field is the point of doing this from a tool rather than by
    /// hand: <c>TimelineStart.Update</c> calls <c>director.Play()</c> with no null check, so an
    /// unassigned field throws the moment the key is pressed.</para>
    /// </summary>
    [MenuItem("Tools/Session Logging/Add Spacebar Timeline Starter")]
    public static void AddTimelineStarter()
    {
        PlayableDirector[] directors =
            Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include,
                                                       FindObjectsSortMode.None);

        if (directors.Length == 0)
        {
            EditorUtility.DisplayDialog("No timeline",
                "This scene has no PlayableDirector, so there is nothing to start.", "OK");
            return;
        }

        int added = 0, rewired = 0;

        foreach (PlayableDirector director in directors)
        {
            TimelineStart starter = director.GetComponent<TimelineStart>();

            if (starter == null)
            {
                starter = Undo.AddComponent<TimelineStart>(director.gameObject);
                added++;
            }

            if (starter.director != director)
            {
                Undo.RecordObject(starter, "Wire Timeline Starter");
                starter.director = director;
                EditorUtility.SetDirty(starter);
                rewired++;
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        string msg = $"Timeline starters added: {added}\nDirector fields wired: {rewired}\n\n" +
                     "Press Space during play to start the timeline. Save the scene to keep this.";
        Debug.Log("[Session Logging] " + msg.Replace("\n", " "));
        EditorUtility.DisplayDialog("Spacebar timeline starter", msg, "OK");
    }

    /// <summary>
    /// Reports what the open scene is missing, without changing anything. Worth running before a
    /// session, since every problem it catches is one that otherwise shows up as absent data
    /// after the participant has gone.
    /// </summary>
    [MenuItem("Tools/Session Logging/Check Current Scene")]
    public static void CheckCurrentScene()
    {
        var logger = Object.FindAnyObjectByType<SessionLogger>(FindObjectsInactive.Include);
        var sampler = Object.FindAnyObjectByType<TrackingSampler>(FindObjectsInactive.Include);
        var input = Object.FindAnyObjectByType<InputEventLogger>(FindObjectsInactive.Include);

        PlayableDirector[] directors =
            Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include,
                                                       FindObjectsSortMode.None);
        int withReceiver = directors.Count(d => d.GetComponent<TimelineLogReceiver>() != null);

        // A timeline that never starts produces no events at all, which looks identical to a
        // logging failure. Worth reporting as its own line.
        int withStarter = directors.Count(d =>
        {
            TimelineStart st = d.GetComponent<TimelineStart>();
            return st != null && st.director != null;
        });
        int playOnAwake = directors.Count(d => d.playOnAwake);

        Camera cam = Camera.main;

        string report =
            $"Scene: {SceneManager.GetActiveScene().name}\n\n" +
            $"SessionLogger:      {Mark(logger != null)}\n" +
            $"TrackingSampler:    {Mark(sampler != null)}\n" +
            $"InputEventLogger:   {Mark(input != null)}\n" +
            $"Camera.main:        {Mark(cam != null)}{(cam != null ? $" ({cam.name})" : " - head will not be tracked")}\n" +
            $"PlayableDirectors:  {directors.Length}, with receiver: {withReceiver}\n" +
            $"Timeline will start: {Mark(withStarter > 0 || playOnAwake > 0)}" +
            $" (spacebar starters: {withStarter}, play on awake: {playOnAwake})\n";

        Debug.Log("[Session Logging] " + report.Replace("\n", " | "));
        EditorUtility.DisplayDialog("Session logging check", report, "OK");
    }

    /// <summary>
    /// Renders a pass/fail marker for the report.
    /// </summary>
    private static string Mark(bool ok) => ok ? "OK" : "MISSING";
}

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerHealth))]
[CanEditMultipleObjects]
public sealed class PlayerHealthEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Runtime Health Test", EditorStyles.boldLabel);

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Кнопки здоровья работают только в Play Mode.",
                MessageType.Info
            );
            return;
        }

        foreach (Object selectedTarget in targets)
        {
            PlayerHealth health = selectedTarget as PlayerHealth;
            if (health == null)
                continue;

            EditorGUILayout.LabelField(
                health.gameObject.name,
                string.Format("{0:0.##} / {1:0.##}{2}",
                    health.CurrentHealth,
                    health.MaximumHealth,
                    health.IsDead ? "  DEAD" : string.Empty)
            );
        }

        EditorGUILayout.Space(4f);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("- Health"))
        {
            foreach (Object selectedTarget in targets)
            {
                PlayerHealth health = selectedTarget as PlayerHealth;
                if (health != null)
                    health.DebugRemoveHealth();
            }
        }

        if (GUILayout.Button("+ Health"))
        {
            foreach (Object selectedTarget in targets)
            {
                PlayerHealth health = selectedTarget as PlayerHealth;
                if (health != null)
                    health.DebugAddHealth();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Kill"))
        {
            foreach (Object selectedTarget in targets)
            {
                PlayerHealth health = selectedTarget as PlayerHealth;
                if (health != null)
                    health.DebugKill();
            }
        }

        if (GUILayout.Button("Restore Full"))
        {
            foreach (Object selectedTarget in targets)
            {
                PlayerHealth health = selectedTarget as PlayerHealth;
                if (health != null)
                    health.DebugRestoreFullHealth();
            }
        }
        EditorGUILayout.EndHorizontal();

        Repaint();
    }
}
#endif

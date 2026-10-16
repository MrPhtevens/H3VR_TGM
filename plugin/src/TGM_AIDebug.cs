using System.Collections.Generic;
using FistVR;
using UnityEngine;

namespace TeamsGameMode;

/// <summary>
/// Optional AI debug view (Debug.ShowAIDebug in the config): a label over each Sosig the AI controls
/// and a line to where it is heading. Only shows on the host, where the AI runs.
/// </summary>
public static class TGM_AIDebug
{
    public static readonly Color AttackColor = new Color(1f, 0.45f, 0.35f);
    public static readonly Color DefendColor = new Color(0.4f, 0.6f, 1f);
    public static readonly Color OperatorColor = new Color(1f, 0.9f, 0.2f);

    static readonly Dictionary<Sosig, TGM_AIDebugLabel> labels = new Dictionary<Sosig, TGM_AIDebugLabel>();
    static Font font;

    public static bool Enabled => TGM_Config.ShowAIDebug != null && TGM_Config.ShowAIDebug.Value;

    /// <summary>
    /// Set what a Sosig is doing and where it is going
    /// </summary>
    public static void Show(Sosig s, string text, Color color, Vector3 target)
    {
        if (!Enabled || s == null || s.Links == null || s.Links.Count == 0 || s.Links[0] == null)
            return;

        TGM_AIDebugLabel label;
        if (!labels.TryGetValue(s, out label) || label == null)
        {
            label = CreateLabel(s.Links[0].transform);
            labels[s] = label;
        }
        label.Set(text, color, target);
    }

    /// <summary>
    /// Called every frame, removes everything when the debug view is turned off
    /// </summary>
    public static void Update()
    {
        if (!Enabled && labels.Count > 0)
            Clear();
    }

    public static void Clear()
    {
        foreach (var label in labels.Values)
        {
            if (label != null)
                Object.Destroy(label.gameObject);
        }
        labels.Clear();
    }

    static TGM_AIDebugLabel CreateLabel(Transform follow)
    {
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        GameObject root = new GameObject("TGM_AIDebugLabel");
        TextMesh text = root.AddComponent<TextMesh>();
        text.font = font;
        text.fontSize = 64;
        text.characterSize = 0.012f;
        text.anchor = TextAnchor.LowerCenter;
        text.alignment = TextAlignment.Center;
        root.GetComponent<MeshRenderer>().material = font.material;

        //Thin stretched cube for the line, the built in material always exists
        GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = "TGM_AIDebugLine";
        Object.Destroy(line.GetComponent<Collider>());
        line.transform.SetParent(root.transform, false);

        TGM_AIDebugLabel label = root.AddComponent<TGM_AIDebugLabel>();
        label.Init(follow, text, line.transform, line.GetComponent<Renderer>());
        return label;
    }
}

public class TGM_AIDebugLabel : MonoBehaviour
{
    Transform follow;
    TextMesh text;
    Transform line;
    Renderer lineRenderer;
    Vector3 target;

    public void Init(Transform followTransform, TextMesh textMesh, Transform lineTransform, Renderer lineRend)
    {
        follow = followTransform;
        text = textMesh;
        line = lineTransform;
        lineRenderer = lineRend;
    }

    public void Set(string label, Color color, Vector3 newTarget)
    {
        text.text = label;
        text.color = color;
        lineRenderer.material.color = color;
        target = newTarget;
    }

    void LateUpdate()
    {
        //Sosig is gone
        if (follow == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = follow.position + Vector3.up * 0.6f;

        //Face the player so it is readable
        if (GM.CurrentPlayerBody != null && GM.CurrentPlayerBody.Head != null)
        {
            Vector3 away = transform.position - GM.CurrentPlayerBody.Head.position;
            if (away.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(away);
        }

        //Line from the Sosig's feet-ish to the target
        Vector3 start = follow.position + Vector3.down * 0.5f;
        Vector3 toTarget = target - start;
        float length = toTarget.magnitude;
        line.gameObject.SetActive(length > 0.5f);
        if (length > 0.5f)
        {
            line.position = start + toTarget * 0.5f;
            line.rotation = Quaternion.LookRotation(toTarget);  //World space, ignores the label turning to the player
            line.localScale = new Vector3(0.03f, 0.03f, length);
        }
    }
}

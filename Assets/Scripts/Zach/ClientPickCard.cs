using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class ClientPickCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public UnityEngine.UI.Button button;
    public UnityEngine.UI.Image background;
    public UnityEngine.UI.Image portrait;
    public TMP_Text label;

    [Header("Hover")]
    public Color idleColor = new Color(0.14f, 0.10f, 0.20f, 0.95f);
    public Color hoverColor = new Color(0.35f, 0.22f, 0.45f, 1f);
    public float hoverScale = 1.1f;

    private bool hovered;
    private bool interactable = true;

    public void Bind(string displayName, Sprite sprite, Action onPick)
    {
        if (label != null) label.text = displayName;
        if (portrait != null) { portrait.sprite = sprite; portrait.enabled = sprite != null; }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onPick());
        SetInteractable(true);
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
        if (button != null) button.interactable = value;
        if (!value) hovered = false;
    }

    public void OnPointerEnter(PointerEventData eventData) { if (interactable) hovered = true; }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; }

    void OnEnable()
    {
        hovered = false;
        transform.localScale = Vector3.one;
        if (background != null) background.color = idleColor;
    }

    void Update()
    {
        float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        float target = hovered ? hoverScale : 1f;
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, k);

        if (background != null)
            background.color = Color.Lerp(background.color, hovered ? hoverColor : idleColor, k);
    }
}
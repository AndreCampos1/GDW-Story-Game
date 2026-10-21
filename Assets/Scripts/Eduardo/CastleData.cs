using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCastleData", menuName = "Castle Data")]
public class CastleData : ScriptableObject
{
    public string castleName;
    public AestheticTag npcAesthetic;
    public Sprite npcPortrait;

    [Header("Endings")]
    public EventDialogue goodEnding;
    public EventDialogue mixedEnding;
    public EventDialogue badEnding;

    [Header("Match ratio 0-1")]
    [Range(0f, 1f)] public float goodRatio = 0.9f;
    [Range(0f, 1f)] public float mixedRatio = 0.4f;
}

using System;
using UnityEngine;

[Serializable]
public sealed class SniperSoundPack
{
    [Tooltip("Выстрел")]
    public AudioClip[] shotClips;

    [Tooltip("Дальний выстрел (для FarSound, слышен бегунам на большом расстоянии)")]
    public AudioClip[] farShotClips;

    [Tooltip("Взвод затвора (играется, когда винтовка снова готова к выстрелу)")]
    public AudioClip[] boltClips;

    [Tooltip("Вход в прицел")]
    public AudioClip[] scopeInClips;

    [Tooltip("Выход из прицела")]
    public AudioClip[] scopeOutClips;

    [Tooltip("Изменение кратности прицела")]
    public AudioClip[] zoomClips;

    [Tooltip("Задержка дыхания")]
    public AudioClip[] breathHoldClips;
}

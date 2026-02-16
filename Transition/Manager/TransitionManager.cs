using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using UnityEngine;
using UnityEngine.UI;

public class TransitionManager : MonoBehaviour
{
    public enum Transition
    {
        None,
        Fade,
        ToonScreen,
    }

    public static async UniTask In(Transition transition)
    {
    }

    public static async UniTask Out(Transition transition)
    {
    }
}

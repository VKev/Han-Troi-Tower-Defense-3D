using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Reports taps that land on Start Wave, including the ones the button itself throws away
    /// because it is greyed out.
    /// </summary>
    /// <remarks>
    /// A refused tap is the moment the player is asking why they cannot start, so it is the one
    /// moment the chain hint is worth drawing attention to - but a non-interactable Button hands
    /// back nothing to hang that on. It still swallows the tap: the event system walks up from
    /// whatever was hit, stops at the first object that can handle a click, and the Button can,
    /// so the event never reaches the HUD root. Button.OnPointerClick then returns early because
    /// it is not interactable, and the press vanishes.
    ///
    /// Sitting on the same GameObject as the Button is what recovers it. Once the event system
    /// has picked that object, it runs every click handler on it, so this one is called whether
    /// or not the Button chose to act. Deciding what a refused tap means is left to the HUD,
    /// which knows whether the button was live; this only reports that a tap happened.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class StartWavePressRelay : MonoBehaviour, IPointerClickHandler
    {
        public event Action Pressed;

        public void OnPointerClick(PointerEventData eventData)
        {
            Pressed?.Invoke();
        }
    }
}

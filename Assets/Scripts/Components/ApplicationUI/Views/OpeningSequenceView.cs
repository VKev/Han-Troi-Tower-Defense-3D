using System.Collections;
using TowerDefense3D.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// The opening: black, then the title, then the journey. Both handovers are made behind a
    /// full-screen black curtain, so nothing is ever seen appearing or disappearing.
    /// </summary>
    /// <remarks>
    /// The brand splash used to be the first of three panels here. It is now the player's own
    /// splash screen, set in Player Settings, because the engine draws that one *before* the
    /// first scene is loaded - which is the only place a splash can cover the application boot
    /// rather than merely follow it. A splash built out of UI can never show during the load it
    /// exists to hide.
    ///
    /// Boot runs the moment the application starts, behind the curtain, so the journey menu is
    /// already sitting underneath by the time the last fade uncovers it. If progress could not be
    /// read, what gets uncovered is the blocking error instead - which is why the curtain never
    /// hides anything permanently, it only ever fades back to nothing.
    ///
    /// The curtain is not this component's own any more: it belongs to <see cref="ScreenFadeView"/>,
    /// which the level transitions fade as well. One owner for the alpha means the opening and a
    /// level load cannot end up writing the same colour on the same frame.
    ///
    /// The curtain does not take raycasts, and does not need to: the tap is read straight off the
    /// input devices rather than through the event system, so there is nothing for a full-screen
    /// graphic to swallow.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class OpeningSequenceView : MonoBehaviour
    {
        [Tooltip("The title panel, uncovered on boot and left up until the screen is tapped.")]
        [SerializeField] private GameObject gameStart;

        [Tooltip("The black curtain every handover happens behind. Shared with the level transitions.")]
        [SerializeField] private ScreenFadeView screenFade;

        private ISoundPlayer soundPlayer;

        public void Initialize(ISoundPlayer player)
        {
            soundPlayer = player;
        }

        private void Awake()
        {
            if (gameStart != null)
            {
                gameStart.SetActive(false);
            }
        }

        private void OnEnable()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return ShowTitle();

            // The curtain is already opaque - ScreenFadeView makes it so before the first frame -
            // so the opening starts by lifting it off the title.
            yield return Uncover();
            yield return AwaitTap();
            soundPlayer?.Play(SoundId.GameStarted);

            // The title is only taken down once the curtain is fully up, and the journey only
            // uncovered once it is gone: the whole point of a cover is that the handover happens
            // inside it.
            yield return Cover();

            if (gameStart != null)
            {
                gameStart.SetActive(false);
            }

            yield return Uncover();
        }

        private IEnumerator Cover()
        {
            if (screenFade == null)
            {
                yield break;
            }

            bool covered = false;
            screenFade.Cover(() => covered = true);
            while (!covered)
            {
                yield return null;
            }
        }

        private IEnumerator Uncover()
        {
            if (screenFade == null)
            {
                yield break;
            }

            bool uncovered = false;
            screenFade.Uncover(() => uncovered = true);
            while (!uncovered)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Turns the title on and makes sure it has actually been given a size before the curtain
        /// comes off it.
        /// </summary>
        /// <remarks>
        /// The panel is sized by an <see cref="AspectRatioFitter"/> rather than by its anchors: it
        /// is authored zero by zero, and the fitter grows it to envelope the parent when the
        /// layout is rebuilt. That rebuild happens once, when the object is enabled - and if it
        /// runs before the root canvas has its own size, the fitter envelopes nothing and leaves
        /// the panel at zero by zero. Nothing marks it dirty afterwards, so it stays that way for
        /// good: a full-screen picture that draws no pixels, which is indistinguishable from the
        /// curtain never lifting.
        ///
        /// Waiting a frame lets the canvas establish its size, and the explicit rebuild then sizes
        /// the panel against a parent that is really there. The size is checked rather than
        /// assumed, because this failing silently is the whole problem - a warning in the log is
        /// worth more than a black screen with no explanation.
        /// </remarks>
        private IEnumerator ShowTitle()
        {
            if (gameStart == null)
            {
                yield break;
            }

            gameStart.SetActive(true);
            yield return null;

            var rect = gameStart.transform as RectTransform;
            if (rect == null)
            {
                yield break;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            if (rect.rect.width <= 1f || rect.rect.height <= 1f)
            {
                Debug.LogWarning(
                    "Title panel has no size after layout (" + rect.rect.size
                    + "), so it will not draw. Check its AspectRatioFitter and its parent canvas.");
            }
        }

        /// <summary>
        /// Waits for a press anywhere on the screen.
        /// </summary>
        /// <remarks>
        /// Read off the devices rather than through a full-screen Button. The title is a picture,
        /// not a control, and reading the devices is what makes "anywhere" actually mean
        /// anywhere: the panel is fitted to its artwork's aspect, so on a screen of a different
        /// shape its rect and the display are not the same rectangle, and a Button would leave
        /// dead strips down the sides that look tappable and are not.
        /// </remarks>
        private static IEnumerator AwaitTap()
        {
            // A press already down when the title appears is the tail of an earlier one - the tap
            // that dismissed a system dialog, say. Waiting for it to end first means the title
            // cannot be skipped by a finger that was already on the glass.
            while (IsPressed())
            {
                yield return null;
            }

            while (!WasPressedThisFrame())
            {
                yield return null;
            }
        }

        private static bool IsPressed()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }

        /// <summary>
        /// A touch, a click, or any key. The keyboard is in there for the Editor and for a
        /// desktop build, where there may be no touchscreen to tap at all.
        /// </summary>
        private static bool WasPressedThisFrame()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.anyKey.wasPressedThisFrame;
        }
    }
}

using UnityEngine;
using UnityEngine.Events;

namespace Armuda.Runtime
{
    /// <summary>
    /// Marks a world object as an interactive Armuda glyph/node.
    /// Left click/tap selects it; right click/long press opens its attached HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArmudaGlyphInteraction : MonoBehaviour
    {
        [SerializeField] private GameObject attachedHud;
        [SerializeField] private bool showHudOnSelect;
        [SerializeField] private UnityEvent onSelected = new UnityEvent();
        [SerializeField] private UnityEvent onHudRequested = new UnityEvent();

        public GameObject AttachedHud => attachedHud;

        public void Select()
        {
            onSelected.Invoke();

            if (showHudOnSelect)
            {
                OpenHud();
            }
        }

        public void OpenHud()
        {
            if (attachedHud != null)
            {
                attachedHud.SetActive(true);
            }

            onHudRequested.Invoke();
        }

        public void CloseHud()
        {
            if (attachedHud != null)
            {
                attachedHud.SetActive(false);
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace ViewSystem
{
    /// <summary>
    /// base class of static view
    /// </summary>
    public class OverlayView : MonoBehaviour
    {
        Canvas m_ViewCanvas;
        GraphicRaycaster m_Racaster;

        [SerializeField]
        bool m_NeedRaycaster;

        protected virtual void Awake() => ValidateComponents();

        void ValidateComponents()
        {
            m_ViewCanvas = GetComponent<Canvas>();
            if (m_ViewCanvas == null) m_ViewCanvas = gameObject.AddComponent<Canvas>();
            m_ViewCanvas.overrideSorting = true;
            m_Racaster = GetComponent<GraphicRaycaster>();
            if (!m_NeedRaycaster) return;
            if (m_Racaster == null) m_Racaster = gameObject.AddComponent<GraphicRaycaster>();
            m_Racaster.enabled = m_NeedRaycaster;
        }
    }

    public class OverlayTooltip : IOverlayTooltip
    {
        
    }
    public interface IOverlayTooltip{}
}